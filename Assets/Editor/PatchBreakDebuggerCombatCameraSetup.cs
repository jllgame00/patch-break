using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Installs DebuggerBattle's combat-only horizontal framing without editing
/// scene YAML. The setup is absolute-value and safe to run repeatedly.
/// </summary>
public static class PatchBreakDebuggerCombatCameraSetup
{
    private const string MenuRoot =
        "Tools/PATCH BREAK/Debugger Combat Camera/";
    private const string ScenePath =
        "Assets/Scenes/DebuggerBattle.unity";
    private const float Tolerance = 0.001f;

    [MenuItem(MenuRoot + "Setup DebuggerBattle")]
    public static void SetupDebuggerBattle()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SceneSetup[] previousSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single
            );
            SetupScene(scene);
            EditorSceneManager.SaveScene(scene);

            scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single
            );
            ValidateSceneOrThrow(scene);
            Debug.Log(
                "PATCH_BREAK_DEBUGGER_COMBAT_CAMERA_SETUP_COMPLETE"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }

    [MenuItem(MenuRoot + "Validate DebuggerBattle")]
    public static void ValidateDebuggerBattle()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SceneSetup[] previousSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single
            );
            ValidateSceneOrThrow(scene);
            Debug.Log(
                "PATCH_BREAK_DEBUGGER_COMBAT_CAMERA_VALIDATION_COMPLETE"
            );
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }

    private static bool PrepareEditorOperation()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "PATCH//BREAK Debugger Combat Camera setup cannot run in " +
                "Play Mode."
            );
            return false;
        }

        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static void SetupScene(Scene scene)
    {
        Camera camera = RequireMainCamera(scene);
        CameraShake shake = camera.GetComponent<CameraShake>();
        if (shake == null)
        {
            throw new InvalidOperationException(
                "DebuggerBattle/Main Camera: CameraShake is required."
            );
        }

        StageBattleSequenceController sequence =
            RequireSingleComponent<StageBattleSequenceController>(scene);
        HeroController hero = RequireSingleComponent<HeroController>(scene);
        DebuggerController debugger =
            RequireSingleComponent<DebuggerController>(scene);
        InfiniteParallaxBackground parallax =
            RequireSingleComponent<InfiniteParallaxBackground>(scene);
        BoxCollider2D ground = RequireGroundCollider(scene);

        ConfigureParallaxForScene(parallax, hero.transform);

        DebuggerCombatCameraFollow follow =
            camera.GetComponent<DebuggerCombatCameraFollow>();
        if (follow == null)
        {
            follow = camera.gameObject.AddComponent<
                DebuggerCombatCameraFollow>();
        }

        SerializedObject followObject = new(follow);
        SetReference(
            followObject,
            "targetCamera",
            camera
        );
        SetReference(
            followObject,
            "stageSequence",
            sequence
        );
        SetReference(followObject, "hero", hero.transform);
        SetReference(followObject, "debugger", debugger.transform);
        SetReference(
            followObject,
            "heroRenderer",
            hero.GetComponent<SpriteRenderer>()
        );
        SetReference(
            followObject,
            "debuggerRenderer",
            debugger.GetComponent<SpriteRenderer>()
        );
        SetReference(
            followObject,
            "parallaxBackground",
            parallax
        );
        SetReference(followObject, "physicalGround", ground);
        SetReference(followObject, "cameraShake", shake);
        SetFloat(followObject, "rightFollowViewportX", 0.72f);
        SetFloat(followObject, "leftFollowViewportX", 0.28f);
        SetFloat(followObject, "hardSafeRightViewportX", 0.88f);
        SetFloat(followObject, "hardSafeLeftViewportX", 0.12f);
        SetFloat(followObject, "diagnosticInterval", 0.25f);
        SetFloat(followObject, "maxFollowSpeed", 30f);
        followObject.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject debuggerObject = new(debugger);
        SetReference(debuggerObject, "combatCamera", camera);
        SetReference(
            debuggerObject,
            "infiniteParallaxBackground",
            parallax
        );
        SetReference(
            debuggerObject,
            "combatCameraFollow",
            follow
        );
        debuggerObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(follow);
        EditorUtility.SetDirty(debugger);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ValidateSceneOrThrow(Scene scene)
    {
        Camera camera = RequireMainCamera(scene);
        DebuggerCombatCameraFollow[] follows =
            FindComponentsInScene<DebuggerCombatCameraFollow>(scene);
        if (follows.Length != 1 || follows[0].gameObject != camera.gameObject)
        {
            throw new InvalidOperationException(
                "DebuggerBattle: exactly one DebuggerCombatCameraFollow " +
                "must exist on Main Camera."
            );
        }

        if (!follows[0].enabled)
        {
            throw new InvalidOperationException(
                "DebuggerBattle/Main Camera: DebuggerCombatCameraFollow is " +
                "disabled."
            );
        }

        DebuggerController debugger =
            RequireSingleComponent<DebuggerController>(scene);
        InfiniteParallaxBackground parallax =
            RequireSingleComponent<InfiniteParallaxBackground>(scene);
        SerializedObject debuggerObject = new(debugger);
        AssertReference(
            debuggerObject,
            "combatCamera",
            camera,
            "Debugger combat Camera"
        );
        AssertReference(
            debuggerObject,
            "infiniteParallaxBackground",
            parallax,
            "Debugger InfiniteParallaxBackground"
        );
        SerializedProperty followProperty =
            RequireProperty(debuggerObject, "combatCameraFollow");
        if (followProperty.objectReferenceValue != follows[0])
        {
            throw new InvalidOperationException(
                "DebuggerBattle/Debugger: combat camera follow reference is " +
                "missing."
            );
        }

        SerializedObject followObject = new(follows[0]);
        AssertReference(
            followObject,
            "targetCamera",
            camera,
            "Main Camera"
        );
        AssertReference(
            followObject,
            "stageSequence",
            RequireSingleComponent<StageBattleSequenceController>(scene),
            "StageBattleSequenceController"
        );
        AssertReference(
            followObject,
            "hero",
            RequireSingleComponent<HeroController>(scene).transform,
            "Hero"
        );
        AssertReference(
            followObject,
            "debugger",
            debugger.transform,
            "Debugger"
        );
        AssertReference(
            followObject,
            "heroRenderer",
            RequireSingleComponent<HeroController>(scene)
                .GetComponent<SpriteRenderer>(),
            "Hero SpriteRenderer"
        );
        AssertReference(
            followObject,
            "debuggerRenderer",
            debugger.GetComponent<SpriteRenderer>(),
            "Debugger SpriteRenderer"
        );
        AssertReference(
            followObject,
            "parallaxBackground",
            parallax,
            "InfiniteParallaxBackground"
        );
        AssertReference(
            followObject,
            "physicalGround",
            RequireGroundCollider(scene),
            "Ground BoxCollider2D"
        );
        AssertReference(
            followObject,
            "cameraShake",
            camera.GetComponent<CameraShake>(),
            "CameraShake"
        );
        AssertFloat(followObject, "rightFollowViewportX", 0.72f);
        AssertFloat(followObject, "leftFollowViewportX", 0.28f);
        AssertFloat(followObject, "hardSafeRightViewportX", 0.88f);
        AssertFloat(followObject, "hardSafeLeftViewportX", 0.12f);
        AssertFloat(followObject, "diagnosticInterval", 0.25f);
        AssertFloat(followObject, "maxFollowSpeed", 30f);

        ValidateParallaxReferencesOrThrow(
            parallax,
            RequireSingleComponent<HeroController>(scene).transform
        );

        float hardLeft = RequireProperty(
            followObject,
            "hardSafeLeftViewportX"
        ).floatValue;
        float followLeft = RequireProperty(
            followObject,
            "leftFollowViewportX"
        ).floatValue;
        float followRight = RequireProperty(
            followObject,
            "rightFollowViewportX"
        ).floatValue;
        float hardRight = RequireProperty(
            followObject,
            "hardSafeRightViewportX"
        ).floatValue;

        if (!(0f < hardLeft &&
              hardLeft < followLeft &&
              followLeft < followRight &&
              followRight < hardRight &&
              hardRight < 1f))
        {
            throw new InvalidOperationException(
                "DebuggerBattle combat camera: dead-zone and hard-safe " +
                "thresholds are not ordered correctly."
            );
        }

        if (RequireGroundCollider(scene).bounds.size.x < 64f - Tolerance)
        {
            throw new InvalidOperationException(
                "DebuggerBattle/Ground: horizontal physics coverage is " +
                "smaller than the required 64 world units."
            );
        }
    }

    private static void SetReference(
        SerializedObject target,
        string propertyName,
        UnityEngine.Object value)
    {
        RequireProperty(target, propertyName).objectReferenceValue = value;
    }

    private static void SetFloat(
        SerializedObject target,
        string propertyName,
        float value)
    {
        RequireProperty(target, propertyName).floatValue = value;
    }

    private static void ConfigureParallaxForScene(
        InfiniteParallaxBackground parallax,
        Transform hero)
    {
        Transform far = RequireDirectChild(parallax.transform, "Far");
        Transform mid = RequireDirectChild(parallax.transform, "Mid");
        Transform near = RequireDirectChild(parallax.transform, "Near");

        SerializedObject parallaxObject = new(parallax);
        SetReference(parallaxObject, "hero", hero);
        ConfigureLayer(
            parallaxObject,
            "far",
            far,
            RequireDirectChildRenderer(far, "A"),
            RequireDirectChildRenderer(far, "B"),
            0.20f
        );
        ConfigureLayer(
            parallaxObject,
            "mid",
            mid,
            RequireDirectChildRenderer(mid, "A"),
            RequireDirectChildRenderer(mid, "B"),
            0.55f
        );
        ConfigureLayer(
            parallaxObject,
            "near",
            near,
            RequireDirectChildRenderer(near, "A"),
            RequireDirectChildRenderer(near, "B"),
            0.95f
        );
        parallaxObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(parallax);
    }

    private static void ConfigureLayer(
        SerializedObject parallaxObject,
        string propertyName,
        Transform container,
        SpriteRenderer tileA,
        SpriteRenderer tileB,
        float multiplier)
    {
        SetReference(
            parallaxObject,
            propertyName + ".container",
            container
        );
        SetReference(
            parallaxObject,
            propertyName + ".tileA",
            tileA
        );
        SetReference(
            parallaxObject,
            propertyName + ".tileB",
            tileB
        );
        SetFloat(
            parallaxObject,
            propertyName + ".multiplier",
            multiplier
        );
    }

    private static void ValidateParallaxReferencesOrThrow(
        InfiniteParallaxBackground parallax,
        Transform expectedHero)
    {
        Transform far = RequireDirectChild(parallax.transform, "Far");
        Transform mid = RequireDirectChild(parallax.transform, "Mid");
        Transform near = RequireDirectChild(parallax.transform, "Near");

        SerializedObject parallaxObject = new(parallax);
        AssertReference(
            parallaxObject,
            "hero",
            expectedHero,
            "InfiniteParallaxBackground Hero"
        );
        AssertLayer(
            parallaxObject,
            "far",
            "Far",
            far,
            RequireDirectChildRenderer(far, "A"),
            RequireDirectChildRenderer(far, "B"),
            0.20f
        );
        AssertLayer(
            parallaxObject,
            "mid",
            "Mid",
            mid,
            RequireDirectChildRenderer(mid, "A"),
            RequireDirectChildRenderer(mid, "B"),
            0.55f
        );
        AssertLayer(
            parallaxObject,
            "near",
            "Near",
            near,
            RequireDirectChildRenderer(near, "A"),
            RequireDirectChildRenderer(near, "B"),
            0.95f
        );

        if (!parallax.IsCameraCoverageConfigurationValid(out string error))
        {
            throw new InvalidOperationException(
                "DebuggerBattle/InfiniteParallaxBackground: " + error
            );
        }
    }

    private static void AssertLayer(
        SerializedObject parallaxObject,
        string propertyName,
        string label,
        Transform container,
        SpriteRenderer tileA,
        SpriteRenderer tileB,
        float multiplier)
    {
        AssertReference(
            parallaxObject,
            propertyName + ".container",
            container,
            label + " container"
        );
        AssertReference(
            parallaxObject,
            propertyName + ".tileA",
            tileA,
            label + " A SpriteRenderer"
        );
        AssertReference(
            parallaxObject,
            propertyName + ".tileB",
            tileB,
            label + " B SpriteRenderer"
        );
        AssertFloat(
            parallaxObject,
            propertyName + ".multiplier",
            multiplier
        );
    }

    private static void AssertReference(
        SerializedObject target,
        string propertyName,
        UnityEngine.Object expected,
        string label)
    {
        if (RequireProperty(target, propertyName).objectReferenceValue !=
            expected)
        {
            throw new InvalidOperationException(
                "DebuggerBattle combat camera: " + label +
                " reference is invalid."
            );
        }
    }

    private static void AssertFloat(
        SerializedObject target,
        string propertyName,
        float expected)
    {
        if (Mathf.Abs(
                RequireProperty(target, propertyName).floatValue - expected
            ) > Tolerance)
        {
            throw new InvalidOperationException(
                "DebuggerBattle combat camera: unexpected " +
                propertyName + "."
            );
        }
    }

    private static SerializedProperty RequireProperty(
        SerializedObject target,
        string name)
    {
        SerializedProperty property = target.FindProperty(name);
        if (property == null)
        {
            throw new InvalidOperationException(
                target.targetObject.GetType().Name +
                " does not expose serialized field " + name + "."
            );
        }

        return property;
    }

    private static Camera RequireMainCamera(Scene scene)
    {
        Camera[] cameras = FindComponentsInScene<Camera>(scene);
        foreach (Camera camera in cameras)
        {
            if (camera.CompareTag("MainCamera"))
            {
                return camera;
            }
        }

        throw new InvalidOperationException(
            "DebuggerBattle: Main Camera is missing."
        );
    }

    private static T RequireSingleComponent<T>(Scene scene)
        where T : Component
    {
        T[] components = FindComponentsInScene<T>(scene);
        if (components.Length != 1)
        {
            throw new InvalidOperationException(
                "DebuggerBattle: expected exactly one " +
                typeof(T).Name + "."
            );
        }

        return components[0];
    }

    private static T[] FindComponentsInScene<T>(Scene scene)
        where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        return System.Array.FindAll(
            all,
            component => component.gameObject.scene == scene
        );
    }

    private static Transform RequireDirectChild(
        Transform parent,
        string childName)
    {
        Transform match = null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name != childName)
            {
                continue;
            }

            if (match != null)
            {
                throw new InvalidOperationException(
                    parent.name + ": multiple direct children named " +
                    childName + "."
                );
            }

            match = child;
        }

        if (match == null)
        {
            throw new InvalidOperationException(
                parent.name + ": direct child " + childName +
                " is missing."
            );
        }

        return match;
    }

    private static SpriteRenderer RequireDirectChildRenderer(
        Transform parent,
        string childName)
    {
        Transform child = RequireDirectChild(parent, childName);
        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            throw new InvalidOperationException(
                parent.name + "/" + childName +
                ": SpriteRenderer is missing."
            );
        }

        if (renderer.sprite == null)
        {
            throw new InvalidOperationException(
                parent.name + "/" + childName + ": sprite is missing."
            );
        }

        return renderer;
    }

    private static BoxCollider2D RequireGroundCollider(Scene scene)
    {
        foreach (BoxCollider2D collider in
                 FindComponentsInScene<BoxCollider2D>(scene))
        {
            if (collider.gameObject.name == "Ground")
            {
                return collider;
            }
        }

        throw new InvalidOperationException(
            "DebuggerBattle: Ground BoxCollider2D is missing."
        );
    }
}
