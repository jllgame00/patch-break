using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PatchBreakArtAssetsSetup
{
    private const string ArtMenuRoot = "Tools/PATCH BREAK/Art Assets/";
    private const string BackgroundMenuRoot =
        "Tools/PATCH BREAK/Background/";
    private const string BackgroundRootName = "Background";
    private const string DefaultSortingLayer = "Default";

    private static readonly ImportSpec[] ImportSpecs =
    {
        new ImportSpec("Characters", "Assets/Art/Characters", 32f),
        new ImportSpec("Background", "Assets/Art/Backgrounds", 36f),
        new ImportSpec("HealthBar", "Assets/Art/UI/HealthBar", 100f),
        new ImportSpec("Console", "Assets/Art/UI/Console", 100f)
    };

    private static readonly SceneSpec[] SceneSpecs =
    {
        new SceneSpec(
            "Assets/Scenes/Battle.unity",
            "Battle",
            "Assets/Art/Backgrounds/Tutorial/bg_tutorial_green_far.png",
            "Assets/Art/Backgrounds/Tutorial/bg_tutorial_green_mid.png",
            "Assets/Art/Backgrounds/Tutorial/bg_tutorial_green_near.png"
        ),
        new SceneSpec(
            "Assets/Scenes/KnightBattle.unity",
            "KnightBattle",
            "Assets/Art/Backgrounds/Boss/bg_boss_red_far.png",
            "Assets/Art/Backgrounds/Boss/bg_boss_red_mid.png",
            "Assets/Art/Backgrounds/Boss/bg_boss_red_near.png"
        ),
        new SceneSpec(
            "Assets/Scenes/DebuggerBattle.unity",
            "DebuggerBattle",
            "Assets/Art/Backgrounds/Boss/bg_boss_red_far.png",
            "Assets/Art/Backgrounds/Boss/bg_boss_red_mid.png",
            "Assets/Art/Backgrounds/Boss/bg_boss_red_near.png"
        )
    };

    [MenuItem(ArtMenuRoot + "Setup Import Settings")]
    public static void SetupImportSettings()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SetupImportSettingsInternal();
        ValidateImportSettingsInternal(true);
        Debug.Log("PATCH_BREAK_ART_IMPORT_SETUP_COMPLETE");
    }

    [MenuItem(ArtMenuRoot + "Validate Import Settings")]
    public static void ValidateImportSettings()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        ValidateImportSettingsInternal(true);
        Debug.Log("PATCH_BREAK_ART_IMPORT_VALIDATION_COMPLETE");
    }

    [MenuItem(BackgroundMenuRoot + "Setup Battle First")]
    public static void SetupBattleFirst()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SetupScenes(new[] { SceneSpecs[0] });
        Debug.Log("PATCH_BREAK_BACKGROUND_BATTLE_SETUP_COMPLETE");
    }

    [MenuItem(BackgroundMenuRoot + "Setup All Scenes")]
    public static void SetupAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SetupScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_BACKGROUND_SETUP_COMPLETE");
    }

    [MenuItem(BackgroundMenuRoot + "Validate All Scenes")]
    public static void ValidateAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        ValidateScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_BACKGROUND_VALIDATION_COMPLETE");
    }

    // Intended for Unity batchmode. It performs the only authorized work for
    // this art pass: import normalization followed by Background setup.
    public static void BatchSetupImportAndBackgrounds()
    {
        try
        {
            SetupImportSettingsInternal();
            ValidateImportSettingsInternal(true);
            SetupScenes(SceneSpecs);
            ValidateScenes(SceneSpecs);
            AssetDatabase.SaveAssets();
            Debug.Log("PATCH_BREAK_ART_BACKGROUND_BATCH_SUCCESS");
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
                "PATCH//BREAK art setup cannot run while Play Mode is active."
            );
            return false;
        }

        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static void SetupImportSettingsInternal()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        foreach (ImportSpec spec in ImportSpecs)
        {
            foreach (string assetPath in FindPngPaths(spec.Folder))
            {
                // This creates/updates metadata through Unity's importer only.
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceUpdate
                );

                TextureImporter importer =
                    AssetImporter.GetAtPath(assetPath) as TextureImporter;

                if (importer == null)
                {
                    throw new InvalidOperationException(
                        $"{assetPath}: TextureImporter could not be loaded."
                    );
                }

                ConfigureImporter(importer, spec.PixelsPerUnit);
                importer.SaveAndReimport();
            }
        }

        AssetDatabase.SaveAssets();
    }

    private static void ConfigureImporter(
        TextureImporter importer,
        float pixelsPerUnit
    )
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.spritePivot = new Vector2(0.5f, 0.5f);
        TextureImporterSettings settings = new();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
    }

    private static void ValidateImportSettingsInternal(bool throwOnError)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        List<string> errors = new();

        foreach (ImportSpec spec in ImportSpecs)
        {
            List<string> assetPaths = FindPngPaths(spec.Folder);

            if (assetPaths.Count == 0)
            {
                errors.Add($"{spec.Name}: no PNG assets found in {spec.Folder}.");
                continue;
            }

            foreach (string assetPath in assetPaths)
            {
                ValidateImporter(assetPath, spec, errors);
            }
        }

        if (errors.Count > 0)
        {
            string message =
                "PATCH//BREAK art import validation failed:\n" +
                string.Join("\n", errors);

            if (throwOnError)
            {
                throw new InvalidOperationException(message);
            }

            Debug.LogError(message);
            return;
        }

        Debug.Log(
            "PATCH//BREAK art import validation passed for " +
            ImportSpecs.Sum(spec => FindPngPaths(spec.Folder).Count) +
            " PNG assets."
        );
    }

    private static void ValidateImporter(
        string assetPath,
        ImportSpec spec,
        List<string> errors
    )
    {
        TextureImporter importer =
            AssetImporter.GetAtPath(assetPath) as TextureImporter;
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

        if (importer == null)
        {
            errors.Add($"{assetPath}: missing TextureImporter.");
            return;
        }

        if (texture == null)
        {
            errors.Add($"{assetPath}: Texture2D was not imported.");
        }

        if (sprite == null)
        {
            errors.Add($"{assetPath}: Sprite was not generated.");
        }

        if (importer.textureType != TextureImporterType.Sprite)
        {
            errors.Add($"{assetPath}: Texture Type is not Sprite.");
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            errors.Add($"{assetPath}: Sprite Mode is not Single.");
        }

        if (!Mathf.Approximately(
                importer.spritePixelsPerUnit,
                spec.PixelsPerUnit
            ))
        {
            errors.Add(
                $"{assetPath}: PPU is {importer.spritePixelsPerUnit}, " +
                $"expected {spec.PixelsPerUnit}."
            );
        }

        if (importer.filterMode != FilterMode.Point)
        {
            errors.Add($"{assetPath}: Filter Mode is not Point.");
        }

        if (importer.textureCompression !=
            TextureImporterCompression.Uncompressed)
        {
            errors.Add($"{assetPath}: Compression is not None.");
        }

        TextureImporterSettings settings = new();
        importer.ReadTextureSettings(settings);

        if (settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            errors.Add($"{assetPath}: Mesh Type is not Full Rect.");
        }

        if (!importer.alphaIsTransparency)
        {
            errors.Add($"{assetPath}: Alpha Is Transparency is disabled.");
        }

        if (importer.mipmapEnabled)
        {
            errors.Add($"{assetPath}: Mip Maps are enabled.");
        }

        if (importer.wrapMode != TextureWrapMode.Clamp)
        {
            errors.Add($"{assetPath}: Wrap Mode is not Clamp.");
        }

        if (importer.npotScale != TextureImporterNPOTScale.None)
        {
            errors.Add($"{assetPath}: NPOT Scale is not None.");
        }

        if (importer.spriteBorder != Vector4.zero)
        {
            errors.Add(
                $"{assetPath}: Sprite Border must remain zero for this pass."
            );
        }
    }

    private static List<string> FindPngPaths(string folder)
    {
        return AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path =>
                string.Equals(
                    Path.GetExtension(path),
                    ".png",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static void SetupScenes(IEnumerable<SceneSpec> sceneSpecs)
    {
        SceneSetup[] originalSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (SceneSpec spec in sceneSpecs)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    spec.Path,
                    OpenSceneMode.Single
                );
                SetupBackground(scene, spec);
                ValidateScene(scene, spec, true);
                EditorSceneManager.MarkSceneDirty(scene);

                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        $"{spec.Name}: failed to save Background setup."
                    );
                }

                Scene reopenedScene = EditorSceneManager.OpenScene(
                    spec.Path,
                    OpenSceneMode.Single
                );
                ValidateScene(reopenedScene, spec, true);
            }
        }
        finally
        {
            if (originalSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }

    private static void ValidateScenes(IEnumerable<SceneSpec> sceneSpecs)
    {
        SceneSetup[] originalSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (SceneSpec spec in sceneSpecs)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    spec.Path,
                    OpenSceneMode.Single
                );
                ValidateScene(scene, spec, true);
            }
        }
        finally
        {
            if (originalSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }

    private static void SetupBackground(Scene scene, SceneSpec spec)
    {
        ValidateNoMissingComponents(scene, spec.Name);

        GameObject backgroundRoot = GetOrCreateBackgroundRoot(scene, spec);
        ConfigureLayer(
            backgroundRoot.transform,
            spec,
            "Far",
            spec.FarPath,
            -30
        );
        ConfigureLayer(
            backgroundRoot.transform,
            spec,
            "Mid",
            spec.MidPath,
            -20
        );
        ConfigureLayer(
            backgroundRoot.transform,
            spec,
            "Near",
            spec.NearPath,
            -10
        );
    }

    private static GameObject GetOrCreateBackgroundRoot(
        Scene scene,
        SceneSpec spec
    )
    {
        GameObject[] roots = scene.GetRootGameObjects()
            .Where(root => root.name == BackgroundRootName)
            .ToArray();

        if (roots.Length > 1)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: found {roots.Length} root objects named " +
                $"'{BackgroundRootName}'. Resolve the duplicate manually."
            );
        }

        GameObject root = roots.Length == 1
            ? roots[0]
            : new GameObject(BackgroundRootName);

        root.transform.SetPositionAndRotation(
            Vector3.zero,
            Quaternion.identity
        );
        root.transform.localScale = Vector3.one;
        return root;
    }

    private static void ConfigureLayer(
        Transform backgroundRoot,
        SceneSpec spec,
        string layerName,
        string spritePath,
        int sortingOrder
    )
    {
        Transform[] matchingChildren = Enumerable.Range(
                0,
                backgroundRoot.childCount
            )
            .Select(backgroundRoot.GetChild)
            .Where(child => child.name == layerName)
            .ToArray();

        if (matchingChildren.Length > 1)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: Background has {matchingChildren.Length} " +
                $"children named '{layerName}'."
            );
        }

        GameObject layerObject = matchingChildren.Length == 1
            ? matchingChildren[0].gameObject
            : new GameObject(layerName);

        if (layerObject.transform.parent != backgroundRoot)
        {
            layerObject.transform.SetParent(backgroundRoot, false);
        }

        layerObject.transform.localPosition = Vector3.zero;
        layerObject.transform.localRotation = Quaternion.identity;
        layerObject.transform.localScale = Vector3.one;

        SpriteRenderer[] renderers =
            layerObject.GetComponents<SpriteRenderer>();

        if (renderers.Length > 1)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: Background/{layerName} has " +
                "multiple SpriteRenderers."
            );
        }

        SpriteRenderer renderer = renderers.Length == 1
            ? renderers[0]
            : layerObject.AddComponent<SpriteRenderer>();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

        if (sprite == null)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: missing Background Sprite at {spritePath}. " +
                "Run Art Assets/Setup Import Settings first."
            );
        }

        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.flipX = false;
        renderer.flipY = false;
        renderer.sortingLayerName = DefaultSortingLayer;
        renderer.sortingOrder = sortingOrder;
    }

    private static void ValidateScene(
        Scene scene,
        SceneSpec spec,
        bool throwOnError
    )
    {
        List<string> errors = new();
        ValidateNoMissingComponents(scene, spec.Name, errors);

        GameObject[] roots = scene.GetRootGameObjects()
            .Where(root => root.name == BackgroundRootName)
            .ToArray();

        if (roots.Length != 1)
        {
            errors.Add(
                $"{spec.Name}: expected one root '{BackgroundRootName}', " +
                $"found {roots.Length}."
            );
        }
        else
        {
            ValidateBackgroundRoot(roots[0].transform, spec, errors);
        }

        if (errors.Count > 0)
        {
            string message =
                "PATCH//BREAK Background validation failed:\n" +
                string.Join("\n", errors);

            if (throwOnError)
            {
                throw new InvalidOperationException(message);
            }

            Debug.LogError(message);
            return;
        }

        Debug.Log($"{spec.Name}: Background validation passed.");
    }

    private static void ValidateBackgroundRoot(
        Transform backgroundRoot,
        SceneSpec spec,
        List<string> errors
    )
    {
        if (backgroundRoot.position != Vector3.zero ||
            backgroundRoot.rotation != Quaternion.identity ||
            backgroundRoot.localScale != Vector3.one)
        {
            errors.Add(
                $"{spec.Name}: Background root transform is not identity."
            );
        }

        ValidateLayer(
            backgroundRoot,
            spec,
            "Far",
            spec.FarPath,
            -30,
            errors
        );
        ValidateLayer(
            backgroundRoot,
            spec,
            "Mid",
            spec.MidPath,
            -20,
            errors
        );
        ValidateLayer(
            backgroundRoot,
            spec,
            "Near",
            spec.NearPath,
            -10,
            errors
        );
    }

    private static void ValidateLayer(
        Transform backgroundRoot,
        SceneSpec spec,
        string layerName,
        string expectedSpritePath,
        int expectedSortingOrder,
        List<string> errors
    )
    {
        Transform[] children = Enumerable.Range(
                0,
                backgroundRoot.childCount
            )
            .Select(backgroundRoot.GetChild)
            .Where(child => child.name == layerName)
            .ToArray();

        if (children.Length != 1)
        {
            errors.Add(
                $"{spec.Name}: expected one Background/{layerName}, " +
                $"found {children.Length}."
            );
            return;
        }

        Transform child = children[0];
        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        Sprite expectedSprite =
            AssetDatabase.LoadAssetAtPath<Sprite>(expectedSpritePath);

        if (renderer == null)
        {
            errors.Add($"{spec.Name}: Background/{layerName} has no SpriteRenderer.");
            return;
        }

        if (renderer.sprite == null)
        {
            errors.Add($"{spec.Name}: Background/{layerName} has a missing Sprite.");
        }
        else if (renderer.sprite != expectedSprite)
        {
            errors.Add(
                $"{spec.Name}: Background/{layerName} Sprite mapping is wrong."
            );
        }

        if (child.localPosition != Vector3.zero ||
            child.localRotation != Quaternion.identity ||
            child.localScale != Vector3.one)
        {
            errors.Add(
                $"{spec.Name}: Background/{layerName} transform is not identity."
            );
        }

        if (renderer.color != Color.white || renderer.flipX || renderer.flipY)
        {
            errors.Add(
                $"{spec.Name}: Background/{layerName} visual flags are invalid."
            );
        }

        if (renderer.sortingLayerName != DefaultSortingLayer ||
            renderer.sortingOrder != expectedSortingOrder)
        {
            errors.Add(
                $"{spec.Name}: Background/{layerName} sorting is invalid."
            );
        }
    }

    private static void ValidateNoMissingComponents(
        Scene scene,
        string sceneName
    )
    {
        List<string> errors = new();
        ValidateNoMissingComponents(scene, sceneName, errors);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", errors));
        }
    }

    private static void ValidateNoMissingComponents(
        Scene scene,
        string sceneName,
        List<string> errors
    )
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in
                     root.GetComponentsInChildren<Transform>(true))
            {
                Component[] components = transform.GetComponents<Component>();

                if (components.Any(component => component == null))
                {
                    errors.Add(
                        $"{sceneName}: Missing MonoBehaviour on " +
                        GetHierarchyPath(transform) + "."
                    );
                }
            }
        }
    }

    private static string GetHierarchyPath(Transform transform)
    {
        List<string> names = new();

        for (Transform current = transform;
             current != null;
             current = current.parent)
        {
            names.Insert(0, current.name);
        }

        return string.Join("/", names);
    }

    private sealed class ImportSpec
    {
        public string Name { get; }
        public string Folder { get; }
        public float PixelsPerUnit { get; }

        public ImportSpec(string name, string folder, float pixelsPerUnit)
        {
            Name = name;
            Folder = folder;
            PixelsPerUnit = pixelsPerUnit;
        }
    }

    private sealed class SceneSpec
    {
        public string Path { get; }
        public string Name { get; }
        public string FarPath { get; }
        public string MidPath { get; }
        public string NearPath { get; }

        public SceneSpec(
            string path,
            string name,
            string farPath,
            string midPath,
            string nearPath
        )
        {
            Path = path;
            Name = name;
            FarPath = farPath;
            MidPath = midPath;
            NearPath = nearPath;
        }
    }
}
