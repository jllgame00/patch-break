using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PatchBreakBackgroundScrollSetup
{
    private const string MenuRoot = "Tools/PATCH BREAK/Background Scroll/";
    private const string BackgroundRootName = "Background";

    private static readonly SceneSpec[] SceneSpecs =
    {
        new SceneSpec("Assets/Scenes/Battle.unity", "Battle"),
        new SceneSpec("Assets/Scenes/KnightBattle.unity", "KnightBattle"),
        new SceneSpec("Assets/Scenes/DebuggerBattle.unity", "DebuggerBattle")
    };

    [MenuItem(MenuRoot + "Setup Battle First")]
    public static void SetupBattleFirst()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SetupScenes(new[] { SceneSpecs[0] });
        Debug.Log("PATCH_BREAK_BACKGROUND_SCROLL_BATTLE_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Setup All Scenes")]
    public static void SetupAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SetupScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_BACKGROUND_SCROLL_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Validate All Scenes")]
    public static void ValidateAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        ValidateScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_BACKGROUND_SCROLL_VALIDATION_COMPLETE");
    }

    public static void BatchSetupAllScenes()
    {
        try
        {
            SetupScenes(SceneSpecs);
            ValidateScenes(SceneSpecs);
            Debug.Log("PATCH_BREAK_BACKGROUND_SCROLL_BATCH_SUCCESS");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static bool PrepareEditorOperation()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "PATCH//BREAK Background Scroll setup cannot run in Play Mode."
            );
            return false;
        }

        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static void RestorePreviousSceneSetup(SceneSetup[] previousSetup)
    {
        if (previousSetup != null && previousSetup.Length > 0)
        {
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }

    private static void SetupScenes(IEnumerable<SceneSpec> sceneSpecs)
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (SceneSpec sceneSpec in sceneSpecs)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    sceneSpec.Path,
                    OpenSceneMode.Single
                );

                SetupScene(scene, sceneSpec);
                EditorSceneManager.SaveScene(scene);
            }

            foreach (SceneSpec sceneSpec in sceneSpecs)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    sceneSpec.Path,
                    OpenSceneMode.Single
                );
                ValidateScene(scene, sceneSpec, true);
            }
        }
        finally
        {
            RestorePreviousSceneSetup(previousSetup);
        }
    }

    private static void ValidateScenes(IEnumerable<SceneSpec> sceneSpecs)
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (SceneSpec sceneSpec in sceneSpecs)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    sceneSpec.Path,
                    OpenSceneMode.Single
                );
                ValidateScene(scene, sceneSpec, true);
            }
        }
        finally
        {
            RestorePreviousSceneSetup(previousSetup);
        }
    }

    private static void SetupScene(Scene scene, SceneSpec sceneSpec)
    {
        Transform background = FindSingleRoot(scene, BackgroundRootName);
        StageBattleSequenceController sequence =
            FindSingleComponent<StageBattleSequenceController>(scene);
        Transform hero = GetHeroReference(sequence);

        LayerSetup far = SetupLayer(background, "Far", -30);
        LayerSetup mid = SetupLayer(background, "Mid", -20);
        LayerSetup near = SetupLayer(background, "Near", -10);

        InfiniteParallaxBackground parallax =
            GetOrAddSingleComponent<InfiniteParallaxBackground>(
                background.gameObject
            );
        parallax.ConfigureForScene(
            hero,
            far.Container,
            far.TileA,
            far.TileB,
            mid.Container,
            mid.TileA,
            mid.TileB,
            near.Container,
            near.TileA,
            near.TileB
        );

        SerializedObject sequenceObject = new(sequence);
        SerializedProperty parallaxProperty =
            sequenceObject.FindProperty("infiniteParallaxBackground");

        if (parallaxProperty == null)
        {
            throw new InvalidOperationException(
                $"{sceneSpec.Name}: StageBattleSequenceController " +
                "does not expose its parallax reference."
            );
        }

        parallaxProperty.objectReferenceValue = parallax;
        sequenceObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(parallax);
        EditorUtility.SetDirty(sequence);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static LayerSetup SetupLayer(
        Transform background,
        string layerName,
        int sortingOrder
    )
    {
        Transform layer = FindSingleDirectChild(background, layerName);
        SpriteRenderer existingRenderer = layer.GetComponent<SpriteRenderer>();
        Transform existingA = FindOptionalDirectChild(layer, "A");
        Transform existingB = FindOptionalDirectChild(layer, "B");

        SpriteRenderer source = existingA != null
            ? RequireSingleRenderer(existingA, layerName + "/A")
            : existingRenderer;

        if (source == null || source.sprite == null)
        {
            throw new InvalidOperationException(
                $"{layerName}: the original SpriteRenderer or sprite is missing."
            );
        }

        SpriteRendererSettings settings = new(source);
        SpriteRenderer tileA = GetOrCreateTile(layer, "A");
        SpriteRenderer tileB = GetOrCreateTile(layer, "B");

        settings.ApplyTo(tileA, sortingOrder);
        settings.ApplyTo(tileB, sortingOrder);

        if (existingRenderer != null)
        {
            UnityEngine.Object.DestroyImmediate(existingRenderer);
        }

        return new LayerSetup(layer, tileA, tileB);
    }

    private static SpriteRenderer GetOrCreateTile(
        Transform layer,
        string tileName
    )
    {
        Transform tile = FindOptionalDirectChild(layer, tileName);
        if (tile == null)
        {
            GameObject tileObject = new(tileName);
            tile = tileObject.transform;
            tile.SetParent(layer, false);
        }

        tile.localRotation = Quaternion.identity;
        tile.localScale = Vector3.one;

        SpriteRenderer[] renderers = tile.GetComponents<SpriteRenderer>();
        if (renderers.Length > 1)
        {
            throw new InvalidOperationException(
                $"{layer.name}/{tileName}: duplicate SpriteRenderers found."
            );
        }

        return renderers.Length == 1
            ? renderers[0]
            : tile.gameObject.AddComponent<SpriteRenderer>();
    }

    private static void ValidateScene(
        Scene scene,
        SceneSpec sceneSpec,
        bool throwOnError
    )
    {
        List<string> errors = new();

        try
        {
            Transform background = FindSingleRoot(scene, BackgroundRootName);
            StageBattleSequenceController sequence =
                FindSingleComponent<StageBattleSequenceController>(scene);
            InfiniteParallaxBackground[] parallaxComponents =
                background.GetComponents<InfiniteParallaxBackground>();

            if (parallaxComponents.Length != 1)
            {
                errors.Add(
                    $"{sceneSpec.Name}: expected exactly one " +
                    "InfiniteParallaxBackground on Background."
                );
            }
            else if (!parallaxComponents[0].IsConfigurationValid(
                         out string configurationError
                     ))
            {
                errors.Add(
                    $"{sceneSpec.Name}: invalid parallax configuration: " +
                    configurationError
                );
            }

            SerializedObject sequenceObject = new(sequence);
            SerializedProperty parallaxProperty =
                sequenceObject.FindProperty("infiniteParallaxBackground");

            if (parallaxProperty == null ||
                parallaxProperty.objectReferenceValue !=
                    parallaxComponents.SingleOrDefault())
            {
                errors.Add(
                    $"{sceneSpec.Name}: StageBattleSequenceController " +
                    "does not reference the Background parallax component."
                );
            }

            ValidateLayer(background, "Far", -30, errors);
            ValidateLayer(background, "Mid", -20, errors);
            ValidateLayer(background, "Near", -10, errors);
            ValidateNoMissingComponents(scene, errors);
        }
        catch (Exception exception)
        {
            errors.Add($"{sceneSpec.Name}: {exception.Message}");
        }

        if (errors.Count == 0)
        {
            Debug.Log(sceneSpec.Name + ": Background Scroll validation passed.");
            return;
        }

        string message = "PATCH//BREAK Background Scroll validation failed:\n" +
            string.Join("\n", errors);

        if (throwOnError)
        {
            throw new InvalidOperationException(message);
        }

        Debug.LogError(message);
    }

    private static void ValidateLayer(
        Transform background,
        string layerName,
        int sortingOrder,
        List<string> errors
    )
    {
        Transform layer = FindSingleDirectChild(background, layerName);
        if (layer.GetComponent<SpriteRenderer>() != null)
        {
            errors.Add($"{layerName}: container still has a SpriteRenderer.");
        }

        if (layer.childCount != 2)
        {
            errors.Add($"{layerName}: expected exactly A and B children.");
            return;
        }

        Transform tileATransform = FindSingleDirectChild(layer, "A");
        Transform tileBTransform = FindSingleDirectChild(layer, "B");
        SpriteRenderer tileA = RequireSingleRenderer(tileATransform, layerName + "/A");
        SpriteRenderer tileB = RequireSingleRenderer(tileBTransform, layerName + "/B");

        if (tileA.sprite == null || tileB.sprite == null ||
            tileA.sprite != tileB.sprite)
        {
            errors.Add($"{layerName}: A/B sprite mapping is invalid.");
        }

        ValidateTileRenderer(tileA, layerName + "/A", sortingOrder, errors);
        ValidateTileRenderer(tileB, layerName + "/B", sortingOrder, errors);

        float width = GetLayerLocalWidth(layer, tileA);
        if (!IsDefaultTransform(tileATransform, 0f) ||
            !IsDefaultTransform(tileBTransform, width))
        {
            errors.Add(
                $"{layerName}: expected A at zero and B at sprite width."
            );
        }
    }

    private static void ValidateTileRenderer(
        SpriteRenderer renderer,
        string name,
        int sortingOrder,
        List<string> errors
    )
    {
        if (renderer.sortingLayerID != 0 ||
            renderer.sortingOrder != sortingOrder)
        {
            errors.Add($"{name}: sorting configuration is incorrect.");
        }

        if (renderer.color != Color.white || renderer.flipX || renderer.flipY)
        {
            errors.Add($"{name}: visual defaults are incorrect.");
        }
    }

    private static void ValidateNoMissingComponents(
        Scene scene,
        List<string> errors
    )
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Component component in
                     root.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    errors.Add(
                        $"{scene.name}: Missing MonoBehaviour or Component found."
                    );
                    return;
                }
            }
        }
    }

    private static Transform GetHeroReference(
        StageBattleSequenceController sequence
    )
    {
        SerializedObject sequenceObject = new(sequence);
        SerializedProperty heroProperty = sequenceObject.FindProperty("hero");
        Transform hero = heroProperty?.objectReferenceValue as Transform;

        if (hero == null)
        {
            throw new InvalidOperationException(
                "StageBattleSequenceController hero reference is missing."
            );
        }

        return hero;
    }

    private static Transform FindSingleRoot(Scene scene, string name)
    {
        Transform[] matches = scene.GetRootGameObjects()
            .Where(root => root.name == name)
            .Select(root => root.transform)
            .ToArray();

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"{scene.name}: expected one root named {name}, found " +
                matches.Length + "."
            );
        }

        return matches[0];
    }

    private static T FindSingleComponent<T>(Scene scene) where T : Component
    {
        T[] matches = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"{scene.name}: expected one {typeof(T).Name}, found " +
                matches.Length + "."
            );
        }

        return matches[0];
    }

    private static T GetOrAddSingleComponent<T>(GameObject gameObject)
        where T : Component
    {
        T[] components = gameObject.GetComponents<T>();
        if (components.Length > 1)
        {
            throw new InvalidOperationException(
                $"{gameObject.name}: duplicate {typeof(T).Name} components found."
            );
        }

        return components.Length == 1
            ? components[0]
            : gameObject.AddComponent<T>();
    }

    private static Transform FindSingleDirectChild(
        Transform parent,
        string name
    )
    {
        Transform child = FindOptionalDirectChild(parent, name);
        if (child == null)
        {
            throw new InvalidOperationException(
                $"{parent.name}: direct child {name} is missing."
            );
        }

        return child;
    }

    private static Transform FindOptionalDirectChild(
        Transform parent,
        string name
    )
    {
        List<Transform> matches = new();

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child.name == name)
            {
                matches.Add(child);
            }
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"{parent.name}: duplicate direct children named {name} found."
            );
        }

        return matches.SingleOrDefault();
    }

    private static SpriteRenderer RequireSingleRenderer(
        Transform transform,
        string name
    )
    {
        SpriteRenderer[] renderers = transform.GetComponents<SpriteRenderer>();
        if (renderers.Length != 1)
        {
            throw new InvalidOperationException(
                $"{name}: expected one SpriteRenderer, found {renderers.Length}."
            );
        }

        return renderers[0];
    }

    private static float GetLayerLocalWidth(
        Transform layer,
        SpriteRenderer tile
    )
    {
        float parentScaleX = Mathf.Abs(layer.lossyScale.x);
        return parentScaleX <= Mathf.Epsilon
            ? 0f
            : tile.bounds.size.x / parentScaleX;
    }

    private static bool IsDefaultTransform(Transform transform, float expectedX)
    {
        return Mathf.Approximately(transform.localPosition.x, expectedX) &&
               Mathf.Approximately(transform.localPosition.y, 0f) &&
               Mathf.Approximately(transform.localPosition.z, 0f) &&
               transform.localRotation == Quaternion.identity &&
               transform.localScale == Vector3.one;
    }

    private readonly struct SceneSpec
    {
        public SceneSpec(string path, string name)
        {
            Path = path;
            Name = name;
        }

        public string Path { get; }
        public string Name { get; }
    }

    private readonly struct LayerSetup
    {
        public LayerSetup(
            Transform container,
            SpriteRenderer tileA,
            SpriteRenderer tileB
        )
        {
            Container = container;
            TileA = tileA;
            TileB = tileB;
        }

        public Transform Container { get; }
        public SpriteRenderer TileA { get; }
        public SpriteRenderer TileB { get; }
    }

    private readonly struct SpriteRendererSettings
    {
        private readonly Sprite sprite;
        private readonly Color color;
        private readonly bool flipX;
        private readonly bool flipY;
        private readonly int sortingLayerId;
        private readonly SpriteMaskInteraction maskInteraction;
        private readonly SpriteDrawMode drawMode;
        private readonly Vector2 size;
        private readonly float adaptiveModeThreshold;
        private readonly SpriteTileMode tileMode;
        private readonly SpriteSortPoint sortPoint;
        private readonly Material[] materials;

        public SpriteRendererSettings(SpriteRenderer source)
        {
            sprite = source.sprite;
            color = source.color;
            flipX = source.flipX;
            flipY = source.flipY;
            sortingLayerId = source.sortingLayerID;
            maskInteraction = source.maskInteraction;
            drawMode = source.drawMode;
            size = source.size;
            adaptiveModeThreshold = source.adaptiveModeThreshold;
            tileMode = source.tileMode;
            sortPoint = source.spriteSortPoint;
            materials = source.sharedMaterials;
        }

        public void ApplyTo(SpriteRenderer target, int sortingOrder)
        {
            target.sprite = sprite;
            target.color = color;
            target.flipX = flipX;
            target.flipY = flipY;
            target.sortingLayerID = sortingLayerId;
            target.sortingOrder = sortingOrder;
            target.maskInteraction = maskInteraction;
            target.drawMode = drawMode;
            target.size = size;
            target.adaptiveModeThreshold = adaptiveModeThreshold;
            target.tileMode = tileMode;
            target.spriteSortPoint = sortPoint;
            target.sharedMaterials = materials;
        }
    }
}
