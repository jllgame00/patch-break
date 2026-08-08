using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sets up the sprite-only, pooled hit VFX listener. Health continues to own
/// damage resolution; this tool only creates a world-space manager that
/// listens to its existing confirmed-damage event.
/// </summary>
public static class PatchBreakHitVfxSetup
{
    private const string MenuRoot = "Tools/PATCH BREAK/Hit VFX/";
    private const string RootName = "HitVfxRoot";
    private const int SlotCount = 4;
    private const int VfxSortingOrder = 5;
    private const float PixelsPerUnit = 32f;
    private const float FramesPerSecond = 15f;
    private const float Epsilon = 0.001f;

    private static readonly SheetSpec NormalSheet = new(
        "Normal Hit",
        "Assets/Art/VFX/Hit/hit_normal_sheet.png",
        24,
        24,
        3
    );

    private static readonly SheetSpec StrongSheet = new(
        "Strong Hit",
        "Assets/Art/VFX/Hit/hit_strong_sheet.png",
        32,
        32,
        4
    );

    private static readonly SheetSpec[] AllSheets =
    {
        NormalSheet,
        StrongSheet
    };

    private static readonly SceneSpec[] SceneSpecs =
    {
        new(
            "Battle",
            "Assets/Scenes/Battle.unity",
            "Hero",
            "Golem"
        ),
        new(
            "KnightBattle",
            "Assets/Scenes/KnightBattle.unity",
            "Hero",
            "Knight"
        ),
        new(
            "DebuggerBattle",
            "Assets/Scenes/DebuggerBattle.unity",
            "Hero",
            "Debugger"
        )
    };

    [MenuItem(MenuRoot + "Analyze Assets")]
    public static void AnalyzeAssets()
    {
        AssetDatabase.Refresh();

        foreach (SheetSpec sheet in AllSheets)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                sheet.Path
            );
            if (texture == null)
            {
                Debug.LogError(
                    "PATCH//BREAK Hit VFX asset missing: " + sheet.Path
                );
                continue;
            }

            Sprite[] frames = LoadFrames(sheet);
            bool dimensionsValid =
                texture.width == sheet.FrameWidth * sheet.FrameCount &&
                texture.height == sheet.FrameHeight;

            Debug.Log(
                "PATCH//BREAK HIT VFX ASSET\n" +
                "name=" + sheet.Name + "\n" +
                "path=" + sheet.Path + "\n" +
                "texture=" + texture.width + "x" + texture.height + "\n" +
                "layout=horizontal " + sheet.FrameCount + " x " +
                sheet.FrameWidth + "x" + sheet.FrameHeight + "\n" +
                "pivot=Center\n" +
                "ppu=" + PixelsPerUnit.ToString("F0") + "\n" +
                "fps=" + FramesPerSecond.ToString("F0") + "\n" +
                "dimensionValid=" + dimensionsValid + "\n" +
                "slicedFrames=" + frames.Length
            );
        }
    }

    [MenuItem(MenuRoot + "Setup Import Settings")]
    public static void SetupImportSettings()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        ConfigureAllImportsOrThrow();
        Debug.Log("PATCH_BREAK_HIT_VFX_IMPORT_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Setup Battle First")]
    public static void SetupBattleFirst()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        ConfigureAllImportsOrThrow();
        SetupScenes(new[] { SceneSpecs[0] });
        Debug.Log("PATCH_BREAK_HIT_VFX_BATTLE_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Setup All Scenes")]
    public static void SetupAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        ConfigureAllImportsOrThrow();
        SetupScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_HIT_VFX_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Validate All Scenes")]
    public static void ValidateAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        ValidateAllImportsOrThrow();
        ValidateScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_HIT_VFX_VALIDATION_COMPLETE");
    }

    private static bool PrepareEditorOperation()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "PATCH//BREAK Hit VFX setup cannot run in Play Mode."
            );
            return false;
        }

        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static void ConfigureAllImportsOrThrow()
    {
        AssetDatabase.Refresh();

        foreach (SheetSpec sheet in AllSheets)
        {
            ConfigureImportOrThrow(sheet);
        }

        AssetDatabase.SaveAssets();
    }

    private static void ConfigureImportOrThrow(SheetSpec sheet)
    {
        TextureImporter importer = AssetImporter.GetAtPath(sheet.Path)
            as TextureImporter;
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            sheet.Path
        );

        if (importer == null || texture == null)
        {
            throw new InvalidOperationException(
                sheet.Name + ": TextureImporter or texture is missing at " +
                sheet.Path + "."
            );
        }

        if (texture.width != sheet.FrameWidth * sheet.FrameCount ||
            texture.height != sheet.FrameHeight)
        {
            throw new InvalidOperationException(
                sheet.Name + ": expected a " + sheet.FrameWidth + "x" +
                sheet.FrameHeight + " horizontal sheet with " +
                sheet.FrameCount + " frames."
            );
        }

        // The source sheets are already deterministically sliced by their
        // asset delivery/import step. Do not rewrite sub-sprite IDs here:
        // keeping them stable preserves all references on repeated setup.
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        SetSpriteMeshType(importer, SpriteMeshType.FullRect);
        importer.SaveAndReimport();

        ValidateImportOrThrow(sheet);
    }

    private static void SetSpriteMeshType(
        TextureImporter importer,
        SpriteMeshType meshType)
    {
        TextureImporterSettings settings = new();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = meshType;
        importer.SetTextureSettings(settings);
    }

    private static SpriteMeshType GetSpriteMeshType(
        TextureImporter importer)
    {
        TextureImporterSettings settings = new();
        importer.ReadTextureSettings(settings);
        return settings.spriteMeshType;
    }

    private static void SetupScenes(IEnumerable<SceneSpec> specs)
    {
        List<SceneSpec> targets = new(specs);
        SceneSetup[] originalSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (SceneSpec spec in targets)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    spec.ScenePath,
                    OpenSceneMode.Single
                );
                ValidatePrerequisitesOrThrow(scene, spec);
            }

            Sprite[] normalFrames = LoadFramesOrThrow(NormalSheet);
            Sprite[] strongFrames = LoadFramesOrThrow(StrongSheet);

            foreach (SceneSpec spec in targets)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    spec.ScenePath,
                    OpenSceneMode.Single
                );
                ConfigureScene(scene, spec, normalFrames, strongFrames);
                EditorSceneManager.SaveScene(scene);
            }

            foreach (SceneSpec spec in targets)
            {
                Scene reopened = EditorSceneManager.OpenScene(
                    spec.ScenePath,
                    OpenSceneMode.Single
                );
                ValidateSceneOrThrow(reopened, spec);
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

    private static void ValidateScenes(IEnumerable<SceneSpec> specs)
    {
        SceneSetup[] originalSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (SceneSpec spec in specs)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    spec.ScenePath,
                    OpenSceneMode.Single
                );
                ValidateSceneOrThrow(scene, spec);
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

    private static void ConfigureScene(
        Scene scene,
        SceneSpec spec,
        Sprite[] normalFrames,
        Sprite[] strongFrames)
    {
        GameObject hero = FindRootOrThrow(scene, spec.HeroRootName);
        GameObject enemy = FindRootOrThrow(scene, spec.EnemyRootName);
        Health heroHealth = RequireHealth(hero, spec.Name + "/Hero");
        Health enemyHealth = RequireHealth(enemy, spec.Name + "/Enemy");
        SpriteRenderer heroRenderer = RequireRenderer(
            hero,
            spec.Name + "/Hero"
        );

        GameObject root = FindOrCreateVfxRoot(scene);
        root.transform.SetParent(null, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        HitVfxManager manager = GetOrAddSingleComponent<HitVfxManager>(
            root,
            spec.Name + "/" + RootName
        );
        HitVfxSlot[] slots = new HitVfxSlot[SlotCount];

        for (int index = 0; index < SlotCount; index++)
        {
            Transform slotTransform = FindOrCreateDirectChild(
                root.transform,
                "Slot" + index
            );
            GameObject slotRoot = slotTransform.gameObject;
            slotTransform.localPosition = Vector3.zero;
            slotTransform.localRotation = Quaternion.identity;
            slotTransform.localScale = Vector3.one;

            SpriteRenderer renderer =
                GetOrAddSingleComponent<SpriteRenderer>(
                    slotRoot,
                    spec.Name + "/" + RootName + "/Slot" + index
                );
            SpriteSequencePlayer sequence =
                GetOrAddSingleComponent<SpriteSequencePlayer>(
                    slotRoot,
                    spec.Name + "/" + RootName + "/Slot" + index
                );
            HitVfxSlot slot = GetOrAddSingleComponent<HitVfxSlot>(
                slotRoot,
                spec.Name + "/" + RootName + "/Slot" + index
            );

            ConfigureSequenceRenderer(sequence, renderer, slotRoot.name);
            slot.Configure(renderer, sequence);
            renderer.sortingLayerID = heroRenderer.sortingLayerID;
            renderer.sortingOrder = VfxSortingOrder;
            renderer.color = Color.white;
            sequence.SetStatic(normalFrames[0]);
            slot.HideImmediately();

            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(sequence);
            EditorUtility.SetDirty(slot);
            slots[index] = slot;
        }

        manager.Configure(
            new[] { heroHealth, enemyHealth },
            normalFrames,
            strongFrames,
            FramesPerSecond,
            FramesPerSecond,
            slots
        );

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ValidateAllImportsOrThrow()
    {
        foreach (SheetSpec sheet in AllSheets)
        {
            ValidateImportOrThrow(sheet);
        }
    }

    private static void ValidateImportOrThrow(SheetSpec sheet)
    {
        TextureImporter importer = AssetImporter.GetAtPath(sheet.Path)
            as TextureImporter;
        Sprite[] frames = LoadFrames(sheet);
        List<string> errors = new();

        if (importer == null ||
            importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Multiple ||
            !Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit) ||
            importer.filterMode != FilterMode.Point ||
            importer.textureCompression != TextureImporterCompression.Uncompressed ||
            importer.mipmapEnabled ||
            importer.wrapMode != TextureWrapMode.Clamp ||
            GetSpriteMeshType(importer) != SpriteMeshType.FullRect ||
            frames.Length != sheet.FrameCount)
        {
            errors.Add("import or slice configuration is invalid");
        }

        for (int index = 0; index < frames.Length; index++)
        {
            Sprite frame = frames[index];
            Vector2 normalizedPivot = frame == null
                ? Vector2.zero
                : new Vector2(
                    frame.pivot.x / frame.rect.width,
                    frame.pivot.y / frame.rect.height
                );

            if (frame == null ||
                !Mathf.Approximately(frame.rect.width, sheet.FrameWidth) ||
                !Mathf.Approximately(frame.rect.height, sheet.FrameHeight) ||
                !Approximately(normalizedPivot, new Vector2(0.5f, 0.5f)))
            {
                errors.Add("frame " + index + " is invalid");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                sheet.Name + ": " + string.Join("; ", errors) + "."
            );
        }
    }

    private static void ValidatePrerequisitesOrThrow(
        Scene scene,
        SceneSpec spec)
    {
        GameObject hero = FindRootOrThrow(scene, spec.HeroRootName);
        GameObject enemy = FindRootOrThrow(scene, spec.EnemyRootName);
        RequireHealth(hero, spec.Name + "/Hero");
        RequireHealth(enemy, spec.Name + "/Enemy");
        RequireRenderer(hero, spec.Name + "/Hero");
        RequireRenderer(enemy, spec.Name + "/Enemy");
    }

    private static void ValidateSceneOrThrow(Scene scene, SceneSpec spec)
    {
        List<string> errors = new();
        GameObject hero = FindRootOrAddError(
            scene,
            spec.HeroRootName,
            errors
        );
        GameObject enemy = FindRootOrAddError(
            scene,
            spec.EnemyRootName,
            errors
        );
        GameObject root = FindRootOrAddError(scene, RootName, errors);

        if (hero != null && enemy != null && root != null)
        {
            ValidateVfxRoot(root, hero, enemy, errors);
        }

        ValidateNoMissingComponents(scene, spec.Name, errors);
        ValidateBrokenObjectReferences(scene, spec.Name, errors);
        ThrowIfErrors(spec.Name + ": Hit VFX validation failed", errors);
    }

    private static void ValidateVfxRoot(
        GameObject root,
        GameObject hero,
        GameObject enemy,
        List<string> errors)
    {
        HitVfxManager[] managers = root.GetComponents<HitVfxManager>();
        if (managers.Length != 1)
        {
            errors.Add(RootName + ": exactly one HitVfxManager is required.");
            return;
        }

        Health heroHealth = hero.GetComponent<Health>();
        Health enemyHealth = enemy.GetComponent<Health>();
        Sprite[] normalFrames = LoadFrames(NormalSheet);
        Sprite[] strongFrames = LoadFrames(StrongSheet);
        HitVfxManager manager = managers[0];

        if (root.transform.parent != null ||
            (root.transform.localScale - Vector3.one).sqrMagnitude >
                Epsilon * Epsilon ||
            !HealthArraysMatch(
                manager.ObservedHealth,
                new[] { heroHealth, enemyHealth }
            ) ||
            !SpriteArraysMatch(manager.NormalFrames, normalFrames) ||
            !SpriteArraysMatch(manager.StrongFrames, strongFrames) ||
            !Mathf.Approximately(
                manager.NormalFramesPerSecond,
                FramesPerSecond
            ) ||
            !Mathf.Approximately(
                manager.StrongFramesPerSecond,
                FramesPerSecond
            ) ||
            manager.Slots == null || manager.Slots.Length != SlotCount)
        {
            errors.Add(RootName + ": manager configuration is invalid.");
            return;
        }

        SpriteRenderer heroRenderer = hero.GetComponent<SpriteRenderer>();
        for (int index = 0; index < SlotCount; index++)
        {
            HitVfxSlot slot = manager.Slots[index];
            Transform expectedTransform = FindDirectChild(
                root.transform,
                "Slot" + index
            );

            if (slot == null || expectedTransform == null ||
                slot.transform != expectedTransform ||
                slot.TargetRenderer == null ||
                slot.SequencePlayer == null ||
                slot.SequencePlayer.TargetRenderer != slot.TargetRenderer ||
                slot.TargetRenderer.enabled ||
                slot.TargetRenderer.sortingLayerID != heroRenderer.sortingLayerID ||
                slot.TargetRenderer.sortingOrder != VfxSortingOrder ||
                slot.GetComponents<SpriteSequencePlayer>().Length != 1 ||
                slot.GetComponents<HitVfxSlot>().Length != 1 ||
                (slot.transform.localScale - Vector3.one).sqrMagnitude >
                    Epsilon * Epsilon)
            {
                errors.Add(
                    RootName + "/Slot" + index + ": configuration is invalid."
                );
            }
        }
    }

    private static void ValidateNoMissingComponents(
        Scene scene,
        string sceneName,
        List<string> errors)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Component component in
                     root.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    errors.Add(sceneName + ": Missing MonoBehaviour found.");
                    return;
                }
            }
        }
    }

    private static void ValidateBrokenObjectReferences(
        Scene scene,
        string sceneName,
        List<string> errors)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Component component in
                     root.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    continue;
                }

                SerializedObject serialized = new(component);
                SerializedProperty property = serialized.GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (property.propertyType ==
                            SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue == null &&
                        property.objectReferenceInstanceIDValue != 0)
                    {
                        errors.Add(
                            sceneName + ": Broken PPtr on " +
                            component.GetType().Name + "." +
                            property.propertyPath + "."
                        );
                        return;
                    }
                }
            }
        }
    }

    private static GameObject FindOrCreateVfxRoot(Scene scene)
    {
        GameObject found = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != RootName)
            {
                continue;
            }

            if (found != null)
            {
                throw new InvalidOperationException(
                    scene.name + ": duplicate root '" + RootName + "'."
                );
            }

            found = root;
        }

        if (found != null)
        {
            return found;
        }

        GameObject created = new(RootName);
        SceneManager.MoveGameObjectToScene(created, scene);
        return created;
    }

    private static Transform FindOrCreateDirectChild(
        Transform parent,
        string name)
    {
        Transform child = FindDirectChild(parent, name);
        if (child != null)
        {
            return child;
        }

        GameObject created = new(name);
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        Transform found = null;
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child.name != name)
            {
                continue;
            }

            if (found != null)
            {
                throw new InvalidOperationException(
                    parent.name + ": duplicate child '" + name + "'."
                );
            }

            found = child;
        }

        return found;
    }

    private static T GetOrAddSingleComponent<T>(
        GameObject owner,
        string context)
        where T : Component
    {
        T[] components = owner.GetComponents<T>();
        if (components.Length > 1)
        {
            throw new InvalidOperationException(
                context + ": duplicate " + typeof(T).Name + "."
            );
        }

        return components.Length == 1
            ? components[0]
            : owner.AddComponent<T>();
    }

    private static void ConfigureSequenceRenderer(
        SpriteSequencePlayer sequence,
        SpriteRenderer renderer,
        string context)
    {
        SerializedObject serialized = new(sequence);
        SerializedProperty property = serialized.FindProperty("targetRenderer");
        if (property == null)
        {
            throw new InvalidOperationException(
                context + ": SpriteSequencePlayer targetRenderer is missing."
            );
        }

        property.objectReferenceValue = renderer;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite[] LoadFramesOrThrow(SheetSpec sheet)
    {
        Sprite[] frames = LoadFrames(sheet);
        if (frames.Length != sheet.FrameCount)
        {
            throw new InvalidOperationException(
                sheet.Name + ": expected " + sheet.FrameCount +
                " sliced frames, found " + frames.Length +
                ". Run Setup Import Settings first."
            );
        }

        return frames;
    }

    private static Sprite[] LoadFrames(SheetSpec sheet)
    {
        UnityEngine.Object[] assets =
            AssetDatabase.LoadAllAssetsAtPath(sheet.Path);
        List<Sprite> frames = new();
        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is Sprite sprite)
            {
                frames.Add(sprite);
            }
        }

        frames.Sort((left, right) => left.rect.x.CompareTo(right.rect.x));
        return frames.ToArray();
    }

    private static GameObject FindRootOrThrow(Scene scene, string name)
    {
        GameObject root = FindRootOrAddError(scene, name, null);
        if (root == null)
        {
            throw new InvalidOperationException(
                scene.name + ": root '" + name + "' is missing or duplicated."
            );
        }

        return root;
    }

    private static GameObject FindRootOrAddError(
        Scene scene,
        string name,
        List<string> errors)
    {
        GameObject found = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != name)
            {
                continue;
            }

            if (found != null)
            {
                errors?.Add(scene.name + ": duplicate root '" + name + "'.");
                return null;
            }

            found = root;
        }

        if (found == null)
        {
            errors?.Add(scene.name + ": root '" + name + "' is missing.");
        }

        return found;
    }

    private static Health RequireHealth(GameObject root, string context)
    {
        Health health = root.GetComponent<Health>();
        if (health == null)
        {
            throw new InvalidOperationException(
                context + ": Health is missing."
            );
        }

        return health;
    }

    private static SpriteRenderer RequireRenderer(
        GameObject root,
        string context)
    {
        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            throw new InvalidOperationException(
                context + ": root SpriteRenderer is missing."
            );
        }

        return renderer;
    }

    private static bool SpriteArraysMatch(Sprite[] left, Sprite[] right)
    {
        if (left == null || right == null || left.Length != right.Length)
        {
            return false;
        }

        for (int index = 0; index < left.Length; index++)
        {
            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool HealthArraysMatch(Health[] left, Health[] right)
    {
        if (left == null || right == null || left.Length != right.Length)
        {
            return false;
        }

        for (int index = 0; index < left.Length; index++)
        {
            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool Approximately(Vector2 left, Vector2 right)
    {
        return (left - right).sqrMagnitude < Epsilon * Epsilon;
    }

    private static void ThrowIfErrors(string title, List<string> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            title + ":\n- " + string.Join("\n- ", errors)
        );
    }

    private sealed class SheetSpec
    {
        public readonly string Name;
        public readonly string Path;
        public readonly int FrameWidth;
        public readonly int FrameHeight;
        public readonly int FrameCount;

        public SheetSpec(
            string name,
            string path,
            int frameWidth,
            int frameHeight,
            int frameCount)
        {
            Name = name;
            Path = path;
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
            FrameCount = frameCount;
        }
    }

    private sealed class SceneSpec
    {
        public readonly string Name;
        public readonly string ScenePath;
        public readonly string HeroRootName;
        public readonly string EnemyRootName;

        public SceneSpec(
            string name,
            string scenePath,
            string heroRootName,
            string enemyRootName)
        {
            Name = name;
            ScenePath = scenePath;
            HeroRootName = heroRootName;
            EnemyRootName = enemyRootName;
        }
    }
}
