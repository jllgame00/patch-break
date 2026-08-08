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

    private bool heroScrolling;
    private bool travelScrolling;
    private float previousHeroX;

    public bool IsHeroScrolling => heroScrolling;
    public bool IsTravelScrolling => travelScrolling;
    public float FarMultiplier => far.Multiplier;
    public float MidMultiplier => mid.Multiplier;
    public float NearMultiplier => near.Multiplier;

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

    [ContextMenu("Reset Tile Positions")]
    public void ResetTilePositions()
    {
        travelScrolling = false;
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
