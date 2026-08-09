using UnityEngine;

/// <summary>
/// DebuggerBattle-only horizontal combat framing. It follows combat bounds
/// inside a dead zone and leaves every gameplay transform, Rigidbody, and
/// projectile untouched. The component is intentionally installed only on
/// DebuggerBattle's Main Camera by the matching editor setup.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class DebuggerCombatCameraFollow : MonoBehaviour
{
    private enum CombatFramingStopReason
    {
        RuntimeStateExit,
        DisableTeardown
    }

    [Header("Scene References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private StageBattleSequenceController stageSequence;
    [SerializeField] private Transform hero;
    [SerializeField] private Transform debugger;
    [SerializeField] private SpriteRenderer heroRenderer;
    [SerializeField] private SpriteRenderer debuggerRenderer;
    [SerializeField] private InfiniteParallaxBackground parallaxBackground;
    [SerializeField] private BoxCollider2D physicalGround;
    [SerializeField] private CameraShake cameraShake;

    [Header("Combat Dead Zone")]
    [SerializeField, Range(0.51f, 0.95f)]
    private float rightFollowViewportX = 0.72f;

    [SerializeField, Range(0.05f, 0.49f)]
    private float leftFollowViewportX = 0.28f;

    [SerializeField, Min(0.1f)]
    private float maxFollowSpeed = 30f;

    [Header("Hard Screen Constraints")]
    [SerializeField, Range(0.51f, 0.99f)]
    private float hardSafeRightViewportX = 0.88f;

    [SerializeField, Range(0.01f, 0.49f)]
    private float hardSafeLeftViewportX = 0.12f;

    [SerializeField, Min(0.01f)]
    private float diagnosticInterval = 0.25f;

    [SerializeField] private bool verboseLogging;

    private bool initialized;
    private bool combatWasActive;
    private bool cameraParallaxActive;
    private float nextDiagnosticTime;
    private bool screenEscapeLogged;
    private float lastRequestedCameraX;
    private float lastAppliedCameraX;
    private float lastCameraDeltaX;
    private bool lastGroundClampApplied;
    private bool constraintConflictLogged;

    /// <summary>
    /// True for the entire DebuggerBattle Combat state, including the portion
    /// where the camera is inside its dead zone. Debugger retreat uses this to
    /// suppress its older actor-delta parallax path and avoid double scroll.
    /// </summary>
    public bool IsCombatFramingActive =>
        isActiveAndEnabled &&
        stageSequence != null &&
        stageSequence.State ==
            StageBattleSequenceController.SequenceState.Combat;

    private void Awake()
    {
        ResolveReferences();
        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            ResolveReferences();
            initialized = true;
        }

        if (!IsCombatFramingActive ||
            targetCamera == null ||
            hero == null ||
            debugger == null)
        {
            StopCombatFraming(
                CombatFramingStopReason.RuntimeStateExit
            );
            return;
        }

        combatWasActive = true;
        FollowCombatBounds();
    }

    private void OnDisable()
    {
        // SceneManager.LoadScene disables scene objects in an unspecified
        // order. The parallax object can therefore already be destroyed here;
        // ending a visual source is still safe, but final viewport coverage
        // validation is no longer meaningful during teardown.
        StopCombatFraming(
            CombatFramingStopReason.DisableTeardown
        );
    }

    private void FollowCombatBounds()
    {
        float currentBaseX = GetBaseLocalPosition().x;
        float desiredBaseX = currentBaseX;

        GetViewportWorldOffsets(
            out float leftViewportOffset,
            out float rightViewportOffset
        );

        float debuggerRight = GetRightEdge(debugger, debuggerRenderer);
        float debuggerLeft = GetLeftEdge(debugger, debuggerRenderer);
        float heroLeft = GetLeftEdge(hero, heroRenderer);

        float preCorrectionDebuggerRightViewport =
            GetViewportX(debuggerRight);
        LogScreenEscapeIfNeeded(preCorrectionDebuggerRightViewport);

        float minimumCameraXForDebugger =
            debuggerRight - rightViewportOffset;
        float maximumCameraXForHero =
            heroLeft - leftViewportOffset;

        bool debuggerNeedsFollow =
            currentBaseX < minimumCameraXForDebugger;
        bool heroNeedsFollow =
            currentBaseX > maximumCameraXForHero;

        // Preserve the requested combat framing priority. In the exceptional
        // case that the actors are wider apart than the dead zone, Debugger's
        // right-edge safety wins over the Hero-left constraint.
        if (debuggerNeedsFollow)
        {
            desiredBaseX = minimumCameraXForDebugger;
        }
        else if (heroNeedsFollow)
        {
            desiredBaseX = maximumCameraXForHero;
        }

        // Soft follow is deliberately allowed to trail a little. It is not
        // the safety mechanism: the hard constraints below run in this same
        // LateUpdate after Rigidbody movement.
        float nextBaseX = Mathf.MoveTowards(
            currentBaseX,
            desiredBaseX,
            maxFollowSpeed * Time.unscaledDeltaTime
        );

        GetHardConstraintCameraBounds(
            debuggerRight,
            Mathf.Min(heroLeft, debuggerLeft),
            out float hardMinimumCameraX,
            out float hardMaximumCameraX
        );

        bool constraintsConflict =
            hardMinimumCameraX > hardMaximumCameraX;
        if (constraintsConflict)
        {
            // The requested priority is Debugger's right-edge safety.
            nextBaseX = Mathf.Max(nextBaseX, hardMinimumCameraX);
            LogConstraintConflictOnce();
        }
        else
        {
            // This is the non-smoothed, same-frame correction. It ensures
            // Debugger's renderer right bound cannot pass hardSafeRight.
            nextBaseX = Mathf.Max(nextBaseX, hardMinimumCameraX);
            nextBaseX = Mathf.Min(nextBaseX, hardMaximumCameraX);
        }

        lastRequestedCameraX = desiredBaseX;
        lastGroundClampApplied = false;
        float cameraDeltaX = nextBaseX - currentBaseX;
        lastCameraDeltaX = cameraDeltaX;
        lastAppliedCameraX = nextBaseX;

        if (Mathf.Abs(cameraDeltaX) > Mathf.Epsilon)
        {
            Vector3 nextBasePosition = GetBaseLocalPosition();
            nextBasePosition.x = nextBaseX;
            SetBaseLocalPosition(nextBasePosition);

            parallaxBackground?.BeginCombatCameraScroll();
            parallaxBackground?.ScrollCombatCameraDelta(
                cameraDeltaX,
                targetCamera
            );
            cameraParallaxActive = parallaxBackground != null;
        }

        if (Mathf.Abs(cameraDeltaX) <= Mathf.Epsilon)
        {
            EndCameraParallaxIfNeeded();
        }

        ValidateHardRightConstraint();
        LogDiagnosticsIfNeeded();
    }

    private void StopCombatFraming(
        CombatFramingStopReason reason)
    {
        EndCameraParallaxIfNeeded();

        if (!combatWasActive)
        {
            return;
        }

        combatWasActive = false;
        screenEscapeLogged = false;
        constraintConflictLogged = false;

        bool coverageValidated =
            reason == CombatFramingStopReason.RuntimeStateExit;
        bool coverageRestored = true;

        if (coverageValidated)
        {
            if (parallaxBackground == null)
            {
                LogCoverageDependencyError("parallaxBackground");
                coverageRestored = false;
            }
            else if (targetCamera == null)
            {
                LogCoverageDependencyError("targetCamera");
                coverageRestored = false;
            }
            else
            {
                coverageRestored =
                    parallaxBackground.EnsureCameraViewportCoverage(
                        targetCamera
                    );
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (coverageValidated && !coverageRestored)
        {
            Debug.LogError(
                "COMBAT_CAMERA_STOP_BACKGROUND_COVERAGE_FAILED",
                this
            );
        }
#endif

        if (verboseLogging)
        {
            Debug.Log(
                "CAMERA_FOLLOW_STOP " +
                $"reason={reason} " +
                $"coverageValidated={coverageValidated} " +
                $"backgroundCoverage={coverageRestored} " +
                $"parallaxAlive={parallaxBackground != null}"
            );
        }
    }

    private void EndCameraParallaxIfNeeded()
    {
        if (!cameraParallaxActive)
        {
            return;
        }

        parallaxBackground?.EndCombatCameraScroll();
        cameraParallaxActive = false;
    }

    private void LogCoverageDependencyError(string fieldName)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogError(
            "DebuggerCombatCameraFollow.StopCombatFraming: " +
            fieldName + " is missing.",
            this
        );
#endif
    }

    private void ResolveReferences()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (cameraShake == null)
        {
            cameraShake = GetComponent<CameraShake>();
        }

        if (stageSequence == null)
        {
            stageSequence =
                Object.FindFirstObjectByType<
                    StageBattleSequenceController>();
        }

        if (parallaxBackground == null)
        {
            parallaxBackground =
                Object.FindFirstObjectByType<
                    InfiniteParallaxBackground>();
        }

        if (debugger == null)
        {
            DebuggerController controller =
                Object.FindFirstObjectByType<DebuggerController>();
            debugger = controller != null
                ? controller.transform
                : null;
        }

        if (hero == null)
        {
            HeroController controller =
                Object.FindFirstObjectByType<HeroController>();
            hero = controller != null ? controller.transform : null;
        }

        if (debuggerRenderer == null && debugger != null)
        {
            debuggerRenderer = debugger.GetComponent<SpriteRenderer>();
        }

        if (heroRenderer == null && hero != null)
        {
            heroRenderer = hero.GetComponent<SpriteRenderer>();
        }

        if (physicalGround == null)
        {
            GameObject ground = GameObject.Find("Ground");
            physicalGround = ground != null
                ? ground.GetComponent<BoxCollider2D>()
                : null;
        }
    }

    private void GetViewportWorldOffsets(
        out float leftOffset,
        out float rightOffset)
    {
        float depth = Mathf.Abs(
            debugger.position.z - targetCamera.transform.position.z
        );
        float cameraX = targetCamera.transform.position.x;
        leftOffset = targetCamera.ViewportToWorldPoint(
            new Vector3(leftFollowViewportX, 0.5f, depth)
        ).x - cameraX;
        rightOffset = targetCamera.ViewportToWorldPoint(
            new Vector3(rightFollowViewportX, 0.5f, depth)
        ).x - cameraX;
    }

    private void GetHardConstraintCameraBounds(
        float debuggerRight,
        float heroLeft,
        out float minimumCameraX,
        out float maximumCameraX)
    {
        float depth = Mathf.Abs(
            debugger.position.z - targetCamera.transform.position.z
        );
        float cameraX = targetCamera.transform.position.x;
        float hardRightOffset = targetCamera.ViewportToWorldPoint(
            new Vector3(hardSafeRightViewportX, 0.5f, depth)
        ).x - cameraX;
        float hardLeftOffset = targetCamera.ViewportToWorldPoint(
            new Vector3(hardSafeLeftViewportX, 0.5f, depth)
        ).x - cameraX;
        float shakeMargin = cameraShake != null
            ? cameraShake.GetMaximumHorizontalOffset()
            : 0f;

        // Camera X is intentionally not ground-clamped. Physical standing is
        // constrained by actor world positions, while framing must remain
        // free to correct a screen overflow immediately.
        minimumCameraX =
            debuggerRight - hardRightOffset + shakeMargin;
        maximumCameraX =
            heroLeft - hardLeftOffset - shakeMargin;
    }

    private float GetViewportX(float worldX)
    {
        float depth = Mathf.Abs(
            debugger.position.z - targetCamera.transform.position.z
        );
        return targetCamera.WorldToViewportPoint(
            new Vector3(worldX, debugger.position.y, depth)
        ).x;
    }

    private void ValidateHardRightConstraint()
    {
        float viewportRight = GetViewportX(
            GetRightEdge(debugger, debuggerRenderer)
        );

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (viewportRight > hardSafeRightViewportX + 0.005f)
        {
            Debug.LogError(
                "CAMERA_CONSTRAINT_FAILED " +
                $"right={viewportRight:F3} " +
                $"safe={hardSafeRightViewportX:F3}",
                this
            );
        }
#endif
    }

    private void LogDiagnosticsIfNeeded()
    {
        if (!verboseLogging ||
            Time.unscaledTime < nextDiagnosticTime)
        {
            return;
        }

        nextDiagnosticTime =
            Time.unscaledTime + diagnosticInterval;

        float debuggerLeft = GetLeftEdge(debugger, debuggerRenderer);
        float debuggerRight = GetRightEdge(debugger, debuggerRenderer);
        float heroLeft = GetLeftEdge(hero, heroRenderer);
        Debug.Log(
            "[DBG_CAMERA] " +
            $"followEnabled={isActiveAndEnabled} " +
            $"combatActive={IsCombatFramingActive} " +
            $"debuggerWorldX={debugger.position.x:F3} " +
            $"debuggerBoundsMinX={debuggerLeft:F3} " +
            $"debuggerBoundsMaxX={debuggerRight:F3} " +
            $"debuggerViewportCenterX=" +
            $"{targetCamera.WorldToViewportPoint(debugger.position).x:F3} " +
            $"debuggerViewportRightX={GetViewportX(debuggerRight):F3} " +
            $"heroViewportLeftX={GetViewportX(heroLeft):F3} " +
            $"cameraWorldX={targetCamera.transform.position.x:F3} " +
            $"cameraIsMain={targetCamera == Camera.main} " +
            $"requestedCameraX={lastRequestedCameraX:F3} " +
            $"appliedCameraX={lastAppliedCameraX:F3} " +
            "cameraClampMin=DISABLED " +
            "cameraClampMax=DISABLED " +
            $"groundClampApplied={lastGroundClampApplied} " +
            $"cameraDelta={lastCameraDeltaX:F3}"
        );
    }

    private void LogScreenEscapeIfNeeded(float viewportRight)
    {
        if (viewportRight <= 0.9f)
        {
            screenEscapeLogged = false;
            return;
        }

        if (screenEscapeLogged)
        {
            return;
        }

        screenEscapeLogged = true;
        Debug.LogWarning(
            "DEBUGGER_SCREEN_ESCAPE " +
            $"rightViewport={viewportRight:F3}",
            this
        );
    }

    private void LogConstraintConflictOnce()
    {
        if (!verboseLogging || constraintConflictLogged)
        {
            return;
        }

        constraintConflictLogged = true;

        Debug.LogWarning(
            "DEBUGGER_CAMERA_CONSTRAINT_CONFLICT: " +
            "Debugger right safety has priority over Hero left safety.",
            this
        );
    }

    private Vector3 GetBaseLocalPosition()
    {
        return cameraShake != null
            ? cameraShake.GetBaseLocalPosition()
            : transform.localPosition;
    }

    private void SetBaseLocalPosition(Vector3 position)
    {
        if (cameraShake != null)
        {
            cameraShake.SetBaseLocalPosition(position);
        }
        else
        {
            transform.localPosition = position;
        }
    }

    private static float GetLeftEdge(
        Transform actor,
        SpriteRenderer renderer)
    {
        return renderer != null
            ? renderer.bounds.min.x
            : actor.position.x;
    }

    private static float GetRightEdge(
        Transform actor,
        SpriteRenderer renderer)
    {
        return renderer != null
            ? renderer.bounds.max.x
            : actor.position.x;
    }
}
