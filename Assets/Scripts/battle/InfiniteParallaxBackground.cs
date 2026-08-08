using System;
using UnityEngine;

public sealed class InfiniteParallaxBackground : MonoBehaviour
{
    [Serializable]
    public sealed class Layer
    {
        [SerializeField] private Transform container;
        [SerializeField] private SpriteRenderer tileA;
        [SerializeField] private SpriteRenderer tileB;
        [SerializeField, Range(0f, 1f)] private float multiplier;

        public Transform Container => container;
        public SpriteRenderer TileA => tileA;
        public SpriteRenderer TileB => tileB;
        public float Multiplier => multiplier;

        public void Configure(
            Transform layerContainer,
            SpriteRenderer firstTile,
            SpriteRenderer secondTile,
            float scrollMultiplier
        )
        {
            container = layerContainer;
            tileA = firstTile;
            tileB = secondTile;
            multiplier = scrollMultiplier;
        }
    }

    [Header("Hero Source")]
    [SerializeField] private Transform hero;

    [Header("Parallax Layers")]
    [SerializeField] private Layer far = new();
    [SerializeField] private Layer mid = new();
    [SerializeField] private Layer near = new();

    [Header("Debugger Combat Retreat")]
    [SerializeField, Range(0f, 1f)]
    private float combatRetreatMultiplier = 0.7f;

    private bool heroScrolling;
    private bool travelScrolling;
    private bool combatRetreatScrolling;
    private bool combatCameraScrolling;
    private float previousHeroX;
    private Camera coverageCamera;
    private bool coverageLostLogged;

    public bool IsHeroScrolling => heroScrolling;
    public bool IsTravelScrolling => travelScrolling;
    public bool IsCombatRetreatScrolling => combatRetreatScrolling;
    public bool IsCombatCameraScrolling => combatCameraScrolling;
    public float FarMultiplier => far.Multiplier;
    public float MidMultiplier => mid.Multiplier;
    public float NearMultiplier => near.Multiplier;
    public float CombatRetreatMultiplier => combatRetreatMultiplier;

    private void Awake()
    {
        ResetTilePositions();
    }

    public void ConfigureForScene(
        Transform heroTransform,
        Transform farContainer,
        SpriteRenderer farA,
        SpriteRenderer farB,
        Transform midContainer,
        SpriteRenderer midA,
        SpriteRenderer midB,
        Transform nearContainer,
        SpriteRenderer nearA,
        SpriteRenderer nearB
    )
    {
        hero = heroTransform;
        far.Configure(farContainer, farA, farB, 0.20f);
        mid.Configure(midContainer, midA, midB, 0.55f);
        near.Configure(nearContainer, nearA, nearB, 0.95f);
        ResetTilePositions();
    }

    public void BeginHeroScroll()
    {
        if (hero == null)
        {
            Debug.LogError(
                "InfiniteParallaxBackground: Hero reference is missing.",
                this
            );
            return;
        }

        previousHeroX = hero.position.x;
        heroScrolling = true;
        BeginTravelScroll();
        EnsureCameraViewportCoverage();
    }

    public void SyncToHeroPosition()
    {
        if (!heroScrolling || hero == null)
        {
            return;
        }

        float heroDeltaX = hero.position.x - previousHeroX;
        previousHeroX = hero.position.x;

        if (Mathf.Approximately(heroDeltaX, 0f))
        {
            return;
        }

        ScrollLayers(-heroDeltaX);
    }

    public void EndHeroScroll()
    {
        SyncToHeroPosition();
        heroScrolling = false;
        EndTravelScroll();
    }

    public void BeginTravelScroll()
    {
        travelScrolling = true;
    }

    public void ScrollTravelDelta(float positiveDeltaX)
    {
        if (!travelScrolling || positiveDeltaX <= Mathf.Epsilon)
        {
            return;
        }

        ScrollLayers(-positiveDeltaX);
    }

    public void EndTravelScroll()
    {
        heroScrolling = false;
        travelScrolling = false;
    }

    /// <summary>
    /// Begins the Debugger-only combat retreat visual. It is separate from
    /// stage travel so Combat movement cannot accidentally enable scrolling.
    /// </summary>
    public void BeginCombatRetreatScroll()
    {
        combatRetreatScrolling = true;
    }

    /// <summary>
    /// Consumes the actual signed world-space Debugger movement delta. A
    /// positive Debugger retreat moves the background left, and vice versa.
    /// This affects visual tiles only; no actor, ground, or projectile moves.
    /// </summary>
    public void ScrollCombatDelta(float signedWorldDeltaX)
    {
        if (!combatRetreatScrolling ||
            Mathf.Abs(signedWorldDeltaX) <= Mathf.Epsilon)
        {
            return;
        }

        ScrollLayers(-signedWorldDeltaX * combatRetreatMultiplier);
    }

    public void EndCombatRetreatScroll()
    {
        combatRetreatScrolling = false;
    }

    /// <summary>
    /// Starts the DebuggerBattle combat-camera parallax source. Camera motion
    /// is kept separate from stage travel and the legacy retreat source so the
    /// caller can never accidentally combine their deltas.
    /// </summary>
    public void BeginCombatCameraScroll()
    {
        combatCameraScrolling = true;
    }

    /// <summary>
    /// Applies one actual Camera X delta. A camera moving right already makes
    /// a static background move left at 1.0x; each layer is therefore moved
    /// right by (1 - multiplier), preserving the perceived 0.95/0.55/0.20
    /// parallax ratios. Tile recycling is camera-relative for this mode,
    /// unlike fixed-camera stage travel recycling.
    /// </summary>
    public void ScrollCombatCameraDelta(
        float signedCameraDeltaX,
        Camera camera)
    {
        if (!combatCameraScrolling ||
            camera == null ||
            Mathf.Abs(signedCameraDeltaX) <= Mathf.Epsilon)
        {
            return;
        }

        CompensateLayerForCamera(far, signedCameraDeltaX);
        CompensateLayerForCamera(mid, signedCameraDeltaX);
        CompensateLayerForCamera(near, signedCameraDeltaX);

        RecycleLayerForCamera(far, camera);
        RecycleLayerForCamera(mid, camera);
        RecycleLayerForCamera(near, camera);
        EnsureCameraViewportCoverage(camera);
    }

    public void EndCombatCameraScroll()
    {
        combatCameraScrolling = false;
    }

    /// <summary>
    /// Recycles the existing A/B tiles around a stationary camera without
    /// applying any visual scroll delta. Combat camera shutdown calls this
    /// once so the final DebuggerBattle frame remains covered until the next
    /// scene replaces it.
    /// </summary>
    public bool EnsureCameraViewportCoverage(Camera camera)
    {
        if (camera == null)
        {
            LogCoverageError(
                "EnsureCameraViewportCoverage: targetCamera is missing."
            );
            return false;
        }

        if (!IsCameraCoverageConfigurationValid(out string error))
        {
            LogCoverageError(
                "EnsureCameraViewportCoverage: " + error
            );
            return false;
        }

        RecycleLayerForCamera(far, camera);
        RecycleLayerForCamera(mid, camera);
        RecycleLayerForCamera(near, camera);

        bool covered =
            IsLayerCoveringCamera(far, camera) &&
            IsLayerCoveringCamera(mid, camera) &&
            IsLayerCoveringCamera(near, camera);
        if (!covered)
        {
            LogCameraCoveragePhase(
                "EnsureCameraViewportCoverage"
            );
            LogCoverageError(
                "EnsureCameraViewportCoverage: A/B tiles do not cover " +
                "the current camera viewport."
            );
        }

        return covered;
    }

    /// <summary>
    /// Uses the active Main Camera as a cached coverage source. The cache is
    /// resolved only when it is absent or inactive; no per-frame Camera.main
    /// search is performed during normal scrolling.
    /// </summary>
    public bool EnsureCameraViewportCoverage()
    {
        Camera camera = ResolveCoverageCamera();
        if (camera == null)
        {
            LogCoverageError(
                "EnsureCameraViewportCoverage: no active Main Camera."
            );
            return false;
        }

        return EnsureCameraViewportCoverage(camera);
    }

    /// <summary>
    /// Development-only Victory diagnostics. It observes the current state;
    /// it does not reposition a tile or camera.
    /// </summary>
    public void LogCameraCoveragePhase(string phase)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Camera camera = ResolveCoverageCamera();
        if (camera == null)
        {
            Debug.LogError(
                "[BKG_PHASE] phase=" + phase + " camera=missing",
                this
            );
            return;
        }

        bool farCovered = TryDescribeLayerCoverage(
            "Far",
            far,
            camera,
            out string farDetail
        );
        bool midCovered = TryDescribeLayerCoverage(
            "Mid",
            mid,
            camera,
            out string midDetail
        );
        bool nearCovered = TryDescribeLayerCoverage(
            "Near",
            near,
            camera,
            out string nearDetail
        );
        bool covered = farCovered && midCovered && nearCovered;

        Debug.Log(
            "[BKG_PHASE] " +
            "phase=" + phase + " " +
            $"cameraX={camera.transform.position.x:F3} " +
            $"backgroundActive={gameObject.activeInHierarchy} " +
            farDetail + " " + midDetail + " " + nearDetail,
            this
        );

        if (!covered && !coverageLostLogged)
        {
            coverageLostLogged = true;
            string lostLayer = !farCovered
                ? "Far"
                : !midCovered
                    ? "Mid"
                    : "Near";
            string lostDetail = !farCovered
                ? farDetail
                : !midCovered
                    ? midDetail
                    : nearDetail;
            Debug.LogError(
                "BACKGROUND_COVERAGE_LOST " +
                "phase=" + phase + " " +
                "layer=" + lostLayer + " " +
                $"cameraX={camera.transform.position.x:F3} " +
                "tiles=" + lostDetail,
                this
            );
        }
        else if (covered)
        {
            coverageLostLogged = false;
        }
#endif
    }

    /// <summary>
    /// Validates only the data used by camera-relative A/B recycling. Hero
    /// travel is deliberately not part of this check because Victory coverage
    /// must remain valid after its movement source has stopped.
    /// </summary>
    public bool IsCameraCoverageConfigurationValid(out string error)
    {
        return IsLayerCoverageValid("Far", far, out error) &&
               IsLayerCoverageValid("Mid", mid, out error) &&
               IsLayerCoverageValid("Near", near, out error);
    }

    [ContextMenu("Reset Tile Positions")]
    public void ResetTilePositions()
    {
        travelScrolling = false;
        combatRetreatScrolling = false;
        combatCameraScrolling = false;
        ResetLayer(far);
        ResetLayer(mid);
        ResetLayer(near);
    }

    public bool IsConfigurationValid(out string error)
    {
        if (hero == null)
        {
            error = "Hero reference is missing.";
            return false;
        }

        foreach (Layer layer in GetLayers())
        {
            if (!IsLayerValid(layer, out error))
            {
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static void ScrollLayer(Layer layer, float offsetX)
    {
        if (!IsLayerValid(layer, out _))
        {
            return;
        }

        float width = GetLayerLocalWidth(layer);
        if (width <= 0f)
        {
            return;
        }

        MoveTile(layer.TileA.transform, offsetX);
        MoveTile(layer.TileB.transform, offsetX);
        RecycleTile(layer.TileA.transform, width);
        RecycleTile(layer.TileB.transform, width);
    }

    private void ScrollLayers(float offsetX)
    {
        ScrollLayer(far, offsetX * far.Multiplier);
        ScrollLayer(mid, offsetX * mid.Multiplier);
        ScrollLayer(near, offsetX * near.Multiplier);
        EnsureCameraViewportCoverage();
    }

    private static void CompensateLayerForCamera(
        Layer layer,
        float signedCameraDeltaX)
    {
        if (!IsLayerValid(layer, out _))
        {
            return;
        }

        // See ScrollCombatCameraDelta: camera motion accounts for the
        // multiplier portion, so only the remainder is applied to the layer.
        float compensation =
            signedCameraDeltaX * (1f - layer.Multiplier);
        MoveTile(layer.TileA.transform, compensation);
        MoveTile(layer.TileB.transform, compensation);
    }

    private static void RecycleLayerForCamera(
        Layer layer,
        Camera camera)
    {
        if (!IsLayerValid(layer, out _) || camera == null)
        {
            return;
        }

        float width = layer.TileA.bounds.size.x;
        if (width <= Mathf.Epsilon)
        {
            return;
        }

        float depth = Mathf.Abs(
            layer.TileA.transform.position.z -
            camera.transform.position.z
        );
        float viewportLeft = camera.ViewportToWorldPoint(
            new Vector3(0f, 0.5f, depth)
        ).x;
        float viewportRight = camera.ViewportToWorldPoint(
            new Vector3(1f, 0.5f, depth)
        ).x;

        // Camera steps are normally small. The bounded loop also safely
        // handles a larger one-frame follow correction without instantiate or
        // destroy, while preserving the existing A/B two-tile topology.
        for (int i = 0; i < 4; i++)
        {
            bool changed = false;
            changed |= RecycleTileForCamera(
                layer.TileA.transform,
                layer.TileB.transform,
                width,
                viewportLeft,
                viewportRight
            );
            changed |= RecycleTileForCamera(
                layer.TileB.transform,
                layer.TileA.transform,
                width,
                viewportLeft,
                viewportRight
            );

            if (!changed)
            {
                break;
            }
        }
    }

    private static bool RecycleTileForCamera(
        Transform tile,
        Transform otherTile,
        float width,
        float viewportLeft,
        float viewportRight)
    {
        SpriteRenderer renderer = tile.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            return false;
        }

        Bounds bounds = renderer.bounds;
        if (bounds.max.x < viewportLeft)
        {
            Vector3 position = otherTile.position;
            position.x += width;
            tile.position = position;
            return true;
        }

        if (bounds.min.x > viewportRight)
        {
            Vector3 position = otherTile.position;
            position.x -= width;
            tile.position = position;
            return true;
        }

        return false;
    }

    private static bool IsLayerCoveringCamera(
        Layer layer,
        Camera camera)
    {
        if (!IsLayerValid(layer, out _) || camera == null)
        {
            return false;
        }

        float depth = Mathf.Abs(
            layer.TileA.transform.position.z -
            camera.transform.position.z
        );
        float viewportLeft = camera.ViewportToWorldPoint(
            new Vector3(0f, 0.5f, depth)
        ).x;
        float viewportRight = camera.ViewportToWorldPoint(
            new Vector3(1f, 0.5f, depth)
        ).x;

        Bounds first = layer.TileA.bounds;
        Bounds second = layer.TileB.bounds;
        float coveredLeft = Mathf.Min(first.min.x, second.min.x);
        float coveredRight = Mathf.Max(first.max.x, second.max.x);
        const float CoverageEpsilon = 0.001f;

        return coveredLeft <= viewportLeft + CoverageEpsilon &&
               coveredRight >= viewportRight - CoverageEpsilon;
    }

    private Camera ResolveCoverageCamera()
    {
        if (coverageCamera != null &&
            coverageCamera.enabled &&
            coverageCamera.gameObject.activeInHierarchy)
        {
            return coverageCamera;
        }

        coverageCamera = Camera.main;
        return coverageCamera;
    }

    private static bool TryDescribeLayerCoverage(
        string layerName,
        Layer layer,
        Camera camera,
        out string detail)
    {
        if (!IsLayerCoverageValid(layerName, layer, out string error))
        {
            detail = layerName + "={invalid:" + error + "}";
            return false;
        }

        float depth = Mathf.Abs(
            layer.TileA.transform.position.z -
            camera.transform.position.z
        );
        float viewportLeft = camera.ViewportToWorldPoint(
            new Vector3(0f, 0.5f, depth)
        ).x;
        float viewportRight = camera.ViewportToWorldPoint(
            new Vector3(1f, 0.5f, depth)
        ).x;
        Bounds first = layer.TileA.bounds;
        Bounds second = layer.TileB.bounds;
        const float CoverageEpsilon = 0.001f;
        bool covered =
            Mathf.Min(first.min.x, second.min.x) <=
                viewportLeft + CoverageEpsilon &&
            Mathf.Max(first.max.x, second.max.x) >=
                viewportRight - CoverageEpsilon;

        detail =
            layerName + "=" +
            $"A[{first.min.x:F3},{first.max.x:F3}] " +
            $"B[{second.min.x:F3},{second.max.x:F3}] " +
            $"camera[{viewportLeft:F3},{viewportRight:F3}] " +
            $"active={layer.Container.gameObject.activeInHierarchy} " +
            $"covered={covered}";
        return covered;
    }

    private static bool IsLayerCoverageValid(
        string layerName,
        Layer layer,
        out string error)
    {
        if (layer == null)
        {
            error = layerName + " layer is missing.";
            return false;
        }

        if (layer.Container == null)
        {
            error = layerName + ".container is missing.";
            return false;
        }

        if (layer.TileA == null)
        {
            error = layerName + ".tileA SpriteRenderer is missing.";
            return false;
        }

        if (layer.TileB == null)
        {
            error = layerName + ".tileB SpriteRenderer is missing.";
            return false;
        }

        if (layer.TileA.sprite == null)
        {
            error = layerName + ".tileA sprite is missing.";
            return false;
        }

        if (layer.TileB.sprite == null)
        {
            error = layerName + ".tileB sprite is missing.";
            return false;
        }

        if (layer.TileA.transform.parent != layer.Container ||
            layer.TileB.transform.parent != layer.Container)
        {
            error = layerName + " A/B tiles are not direct layer children.";
            return false;
        }

        if (!layer.TileA.enabled ||
            !layer.TileB.enabled ||
            !layer.TileA.gameObject.activeInHierarchy ||
            !layer.TileB.gameObject.activeInHierarchy)
        {
            error = layerName + " A/B renderers are disabled or inactive.";
            return false;
        }

        if (layer.TileA.bounds.size.x <= Mathf.Epsilon ||
            layer.TileB.bounds.size.x <= Mathf.Epsilon)
        {
            error = layerName + " A/B tile width is invalid.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void LogCoverageError(string message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogError("InfiniteParallaxBackground: " + message, this);
#endif
    }

    private static void ResetLayer(Layer layer)
    {
        if (!IsLayerValid(layer, out _))
        {
            return;
        }

        float width = GetLayerLocalWidth(layer);
        if (width <= 0f)
        {
            return;
        }

        SetTileLocalPosition(layer.TileA.transform, 0f);
        SetTileLocalPosition(layer.TileB.transform, width);
    }

    private static bool IsLayerValid(Layer layer, out string error)
    {
        if (layer == null ||
            layer.Container == null ||
            layer.TileA == null ||
            layer.TileB == null)
        {
            error = "Layer references are incomplete.";
            return false;
        }

        if (layer.TileA.sprite == null || layer.TileB.sprite == null)
        {
            error = $"{layer.Container.name}: a tile sprite is missing.";
            return false;
        }

        if (layer.TileA.transform.parent != layer.Container ||
            layer.TileB.transform.parent != layer.Container)
        {
            error = $"{layer.Container.name}: tiles must be direct children.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static float GetLayerLocalWidth(Layer layer)
    {
        float parentScaleX = Mathf.Abs(layer.Container.lossyScale.x);
        if (parentScaleX <= Mathf.Epsilon)
        {
            return 0f;
        }

        return layer.TileA.bounds.size.x / parentScaleX;
    }

    private static void MoveTile(Transform tile, float offsetX)
    {
        Vector3 position = tile.localPosition;
        position.x += offsetX;
        tile.localPosition = position;
    }

    private static void RecycleTile(Transform tile, float width)
    {
        Vector3 position = tile.localPosition;

        while (position.x <= -width)
        {
            position.x += width * 2f;
        }

        while (position.x >= width)
        {
            position.x -= width * 2f;
        }

        tile.localPosition = position;
    }

    private static void SetTileLocalPosition(Transform tile, float x)
    {
        tile.localPosition = new Vector3(x, 0f, 0f);
        tile.localRotation = Quaternion.identity;
        tile.localScale = Vector3.one;
    }

    private Layer[] GetLayers()
    {
        return new[] { far, mid, near };
    }
}
