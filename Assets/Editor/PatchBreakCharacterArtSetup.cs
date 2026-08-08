using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PatchBreakCharacterArtSetup
{
    private const string MenuRoot =
        "Tools/PATCH BREAK/Character Art/";
    private const float CharacterPixelsPerUnit = 32f;
    private const float HealthBarMargin = 0.25f;
    private const float BaselineTolerance = 0.02f;

    private static readonly string[] CharacterSpritePaths =
    {
        "Assets/Art/Characters/Hero/Sprites/hero_side_concept_b.png",
        "Assets/Art/Characters/Hero/Sprites/hero_side_concept_b_ready.png",
        "Assets/Art/Characters/Golem/Sprites/golem_side_concept_b.png",
        "Assets/Art/Characters/Golem/Sprites/golem_side_concept_b_ready.png",
        "Assets/Art/Characters/Knight/Sprites/knight_side_concept.png",
        "Assets/Art/Characters/Knight/Sprites/knight_side_concept_ready.png",
        "Assets/Art/Characters/Debugger/Sprites/debugger_boss_silhouette.png",
        "Assets/Art/Characters/Debugger/Sprites/debugger_boss_silhouette_phase.png"
    };

    private static readonly SceneSpec[] SceneSpecs =
    {
        new(
            "Assets/Scenes/Battle.unity",
            "Battle",
            new CharacterSpec(
                "Hero",
                "Assets/Art/Characters/Hero/Sprites/hero_side_concept_b.png",
                typeof(HeroController),
                new Vector3(1f, 1f, 1f),
                "AttackPoint"
            ),
            new CharacterSpec(
                "Golem",
                "Assets/Art/Characters/Golem/Sprites/golem_side_concept_b.png",
                typeof(GolemController),
                new Vector3(1.5f, 1.5f, 1f),
                "AttackPoint"
            )
        ),
        new(
            "Assets/Scenes/KnightBattle.unity",
            "KnightBattle",
            new CharacterSpec(
                "Hero",
                "Assets/Art/Characters/Hero/Sprites/hero_side_concept_b.png",
                typeof(HeroController),
                new Vector3(1f, 1f, 1f),
                "AttackPoint"
            ),
            new CharacterSpec(
                "Knight",
                "Assets/Art/Characters/Knight/Sprites/knight_side_concept.png",
                typeof(KnightController),
                new Vector3(1.5f, 1.5f, 1f),
                "MeleeAttackPoint",
                "ProjectileSpawnPoint",
                "GuardIndicator",
                "ProjectileTelegraph"
            )
        ),
        new(
            "Assets/Scenes/DebuggerBattle.unity",
            "DebuggerBattle",
            new CharacterSpec(
                "Hero",
                "Assets/Art/Characters/Hero/Sprites/hero_side_concept_b.png",
                typeof(HeroController),
                new Vector3(1f, 1f, 1f),
                "AttackPoint"
            ),
            new CharacterSpec(
                "Debugger",
                "Assets/Art/Characters/Debugger/Sprites/debugger_boss_silhouette.png",
                typeof(DebuggerController),
                new Vector3(1.8f, 1.8f, 1f),
                "MeleeAttackPoint",
                "ProjectileSpawnPoint",
                "GuardIndicator",
                "ProjectileTelegraph"
            )
        )
    };

    [MenuItem(MenuRoot + "Setup Battle First")]
    public static void SetupBattleFirst()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SetupScenes(new[] { SceneSpecs[0] });
        Debug.Log("PATCH_BREAK_CHARACTER_ART_BATTLE_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Setup All Scenes")]
    public static void SetupAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SetupScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_CHARACTER_ART_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Validate All Scenes")]
    public static void ValidateAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        ValidateCharacterImporters();
        ValidateScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_CHARACTER_ART_VALIDATION_COMPLETE");
    }

    // Intended for Unity batchmode after the project is no longer open in
    // another Editor instance.
    public static void BatchSetupAllScenes()
    {
        ConfigureCharacterImporters();
        ValidateCharacterImporters();
        SetupScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_CHARACTER_ART_BATCH_SETUP_COMPLETE");
    }

    private static bool PrepareEditorOperation()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "PATCH//BREAK character art setup cannot run in Play Mode."
            );
            return false;
        }

        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static void SetupScenes(IEnumerable<SceneSpec> specs)
    {
        ConfigureCharacterImporters();
        ValidateCharacterImporters();

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

                SetupScene(scene, spec);

                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        $"{spec.Name}: scene could not be saved."
                    );
                }

                Scene reopenedScene = EditorSceneManager.OpenScene(
                    spec.ScenePath,
                    OpenSceneMode.Single
                );
                ValidateSceneOrThrow(reopenedScene, spec);
            }

            AssetDatabase.SaveAssets();
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

    private static void ConfigureCharacterImporters()
    {
        foreach (string spritePath in CharacterSpritePaths)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(spritePath) as TextureImporter;

            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"{spritePath}: TextureImporter is missing."
                );
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = CharacterPixelsPerUnit;
            importer.spritePivot = new Vector2(0.5f, 0f);
            TextureImporterSettings settings = new();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment =
                (int)SpriteAlignment.BottomCenter;
            settings.spritePivot = new Vector2(0.5f, 0f);
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        AssetDatabase.SaveAssets();
    }

    private static void ValidateCharacterImporters()
    {
        List<string> errors = new();

        foreach (string spritePath in CharacterSpritePaths)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(spritePath) as TextureImporter;
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            TextureImporterSettings settings = new();

            if (importer == null || sprite == null)
            {
                errors.Add($"{spritePath}: sprite import is missing.");
                continue;
            }

            importer.ReadTextureSettings(settings);

            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(
                    importer.spritePixelsPerUnit,
                    CharacterPixelsPerUnit
                ) ||
                settings.spriteAlignment !=
                    (int)SpriteAlignment.BottomCenter ||
                !Mathf.Approximately(settings.spritePivot.x, 0.5f) ||
                !Mathf.Approximately(settings.spritePivot.y, 0f) ||
                importer.filterMode != FilterMode.Point ||
                importer.textureCompression !=
                    TextureImporterCompression.Uncompressed ||
                importer.mipmapEnabled ||
                importer.wrapMode != TextureWrapMode.Clamp)
            {
                errors.Add(
                    $"{spritePath}: required character import settings " +
                    "are not applied."
                );
            }
        }

        ThrowIfErrors("Character import validation failed", errors);
    }

    private static void SetupScene(Scene scene, SceneSpec spec)
    {
        CharacterRoot hero = FindCharacterRoot(scene, spec.Hero);
        CharacterRoot enemy = FindCharacterRoot(scene, spec.Enemy);

        SetupRenderer(hero);
        SetupRenderer(enemy);
        UpdateHealthBarOffset(scene, hero);
        UpdateHealthBarOffset(scene, enemy);

        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void SetupRenderer(CharacterRoot character)
    {
        Sprite expectedSprite =
            AssetDatabase.LoadAssetAtPath<Sprite>(
                character.Spec.SpritePath
            );

        if (expectedSprite == null)
        {
            throw new InvalidOperationException(
                $"{character.Spec.RootName}: expected Sprite is missing."
            );
        }

        character.Renderer.sprite = expectedSprite;
        character.Renderer.color = Color.white;
        character.Renderer.flipX = false;
        character.Renderer.flipY = false;
        EditorUtility.SetDirty(character.Renderer);
    }

    private static void UpdateHealthBarOffset(
        Scene scene,
        CharacterRoot character)
    {
        WorldHealthBarUI healthBar = FindHealthBar(scene, character.Root);
        SerializedObject healthBarData = new(healthBar);
        SerializedProperty targetProperty =
            healthBarData.FindProperty("trackedTarget");
        SerializedProperty offsetProperty =
            healthBarData.FindProperty("worldOffset");

        if (targetProperty == null || offsetProperty == null ||
            targetProperty.objectReferenceValue != character.Root)
        {
            throw new InvalidOperationException(
                $"{character.Spec.RootName}: WorldHealthBar target is " +
                "not the existing character root."
            );
        }

        float topAboveRoot =
            character.Renderer.bounds.max.y -
            character.Root.position.y;
        offsetProperty.vector3Value = new Vector3(
            0f,
            topAboveRoot + HealthBarMargin,
            0f
        );

        healthBarData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(healthBar);
    }

    private static void ValidateSceneOrThrow(
        Scene scene,
        SceneSpec spec)
    {
        List<string> errors = new();
        CharacterRoot hero = ValidateCharacterRoot(scene, spec.Hero, errors);
        CharacterRoot enemy = ValidateCharacterRoot(scene, spec.Enemy, errors);

        if (hero != null)
        {
            ValidateHealthBar(scene, hero, errors);
        }

        if (enemy != null)
        {
            ValidateHealthBar(scene, enemy, errors);
        }

        ValidateStageReferences(scene, hero, enemy, errors);
        ValidateNoMissingComponents(scene, spec.Name, errors);
        ValidateBrokenObjectReferences(scene, spec.Name, errors);
        ThrowIfErrors($"{spec.Name}: character art validation failed", errors);
    }

    private static CharacterRoot ValidateCharacterRoot(
        Scene scene,
        CharacterSpec spec,
        List<string> errors)
    {
        CharacterRoot character = TryFindCharacterRoot(scene, spec, errors);

        if (character == null)
        {
            return null;
        }

        Sprite expectedSprite =
            AssetDatabase.LoadAssetAtPath<Sprite>(spec.SpritePath);

        if (character.Renderer.sprite != expectedSprite)
        {
            errors.Add($"{spec.RootName}: SpriteRenderer sprite is incorrect.");
        }

        if (character.Renderer.color != Color.white ||
            character.Renderer.flipX || character.Renderer.flipY)
        {
            errors.Add($"{spec.RootName}: idle SpriteRenderer flags are incorrect.");
        }

        if (character.Root.localScale != spec.ExpectedScale)
        {
            errors.Add($"{spec.RootName}: root scale was changed.");
        }

        if (character.Root.GetComponent<Rigidbody2D>() == null ||
            character.Root.GetComponent<Collider2D>() == null ||
            character.Root.GetComponent<Health>() == null ||
            character.Root.GetComponent(spec.ControllerType) == null)
        {
            errors.Add(
                $"{spec.RootName}: required root component is missing."
            );
        }

        foreach (string childName in spec.RequiredChildNames)
        {
            if (FindDescendant(character.Root, childName) == null)
            {
                errors.Add(
                    $"{spec.RootName}: required child '{childName}' is missing."
                );
            }
        }

        ValidateControllerRenderer(character, errors);

        float baseline = character.Renderer.bounds.min.y;
        if (!Mathf.Approximately(
                baseline,
                character.Root.position.y) &&
            Mathf.Abs(baseline - character.Root.position.y) >
                BaselineTolerance)
        {
            errors.Add(
                $"{spec.RootName}: Sprite baseline is not on the root Y."
            );
        }

        return character;
    }

    private static void ValidateControllerRenderer(
        CharacterRoot character,
        List<string> errors)
    {
        if (character.Spec.ControllerType == typeof(HeroController))
        {
            return;
        }

        Component controller =
            character.Root.GetComponent(character.Spec.ControllerType);
        SerializedObject data = new(controller);
        SerializedProperty rendererProperty =
            data.FindProperty("spriteRenderer");

        if (rendererProperty == null ||
            rendererProperty.objectReferenceValue != character.Renderer)
        {
            errors.Add(
                $"{character.Spec.RootName}: controller no longer references " +
                "the root SpriteRenderer."
            );
        }
    }

    private static void ValidateHealthBar(
        Scene scene,
        CharacterRoot character,
        List<string> errors)
    {
        WorldHealthBarUI healthBar;

        try
        {
            healthBar = FindHealthBar(scene, character.Root);
        }
        catch (InvalidOperationException exception)
        {
            errors.Add(exception.Message);
            return;
        }

        SerializedObject data = new(healthBar);
        SerializedProperty targetProperty =
            data.FindProperty("trackedTarget");
        SerializedProperty offsetProperty =
            data.FindProperty("worldOffset");

        if (targetProperty == null || offsetProperty == null ||
            targetProperty.objectReferenceValue != character.Root)
        {
            errors.Add(
                $"{character.Spec.RootName}: WorldHealthBar target is invalid."
            );
            return;
        }

        float minimumOffset =
            character.Renderer.bounds.max.y -
            character.Root.position.y + HealthBarMargin;

        if (offsetProperty.vector3Value.y + 0.001f < minimumOffset)
        {
            errors.Add(
                $"{character.Spec.RootName}: WorldHealthBar overlaps the " +
                "new Sprite bounds."
            );
        }
    }

    private static void ValidateStageReferences(
        Scene scene,
        CharacterRoot hero,
        CharacterRoot enemy,
        List<string> errors)
    {
        StageBattleSequenceController stage =
            FindSingleComponent<StageBattleSequenceController>(scene);

        if (stage == null)
        {
            errors.Add("StageBattleSequenceController is missing.");
            return;
        }

        SerializedObject stageData = new(stage);
        ValidateReference(
            stageData,
            "hero",
            hero != null ? hero.Root : null,
            errors
        );
        ValidateReference(
            stageData,
            "enemy",
            enemy != null ? enemy.Root : null,
            errors
        );

        SerializedProperty parallaxProperty =
            stageData.FindProperty("infiniteParallaxBackground");
        InfiniteParallaxBackground parallax =
            parallaxProperty != null
                ? parallaxProperty.objectReferenceValue as
                    InfiniteParallaxBackground
                : null;
        string parallaxError = string.Empty;

        if (parallax == null ||
            !parallax.IsConfigurationValid(out parallaxError))
        {
            errors.Add(
                "InfiniteParallaxBackground references are invalid: " +
                (string.IsNullOrEmpty(parallaxError)
                    ? "missing component."
                    : parallaxError)
            );
        }
    }

    private static void ValidateReference(
        SerializedObject owner,
        string propertyName,
        Transform expected,
        List<string> errors)
    {
        SerializedProperty property = owner.FindProperty(propertyName);

        if (property == null ||
            property.objectReferenceValue != expected)
        {
            errors.Add(
                $"StageBattleSequenceController.{propertyName} reference " +
                "is invalid."
            );
        }
    }

    private static void ValidateNoMissingComponents(
        Scene scene,
        string sceneName,
        List<string> errors)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in
                     root.GetComponentsInChildren<Transform>(true))
            {
                foreach (Component component in
                         transform.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        errors.Add(
                            $"{sceneName}: Missing MonoBehaviour on " +
                            GetHierarchyPath(transform) + "."
                        );
                    }
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

                SerializedObject data = new(component);
                SerializedProperty property = data.GetIterator();
                bool enterChildren = true;

                while (property.Next(enterChildren))
                {
                    enterChildren = false;

                    if (property.propertyType !=
                            SerializedPropertyType.ObjectReference ||
                        property.objectReferenceValue != null ||
                        property.objectReferenceInstanceIDValue == 0)
                    {
                        continue;
                    }

                    errors.Add(
                        $"{sceneName}: Broken PPtr on " +
                        GetHierarchyPath(component.transform) + "/" +
                        component.GetType().Name + "." +
                        property.propertyPath + "."
                    );
                }
            }
        }
    }

    private static CharacterRoot FindCharacterRoot(
        Scene scene,
        CharacterSpec spec)
    {
        List<string> errors = new();
        CharacterRoot character = TryFindCharacterRoot(scene, spec, errors);
        ThrowIfErrors($"{scene.name}: character root lookup failed", errors);
        return character;
    }

    private static CharacterRoot TryFindCharacterRoot(
        Scene scene,
        CharacterSpec spec,
        List<string> errors)
    {
        GameObject root = null;

        foreach (GameObject candidate in scene.GetRootGameObjects())
        {
            if (candidate.name != spec.RootName)
            {
                continue;
            }

            if (root != null)
            {
                errors.Add($"{spec.RootName}: duplicate scene root.");
                return null;
            }

            root = candidate;
        }

        if (root == null)
        {
            errors.Add($"{spec.RootName}: root is missing.");
            return null;
        }

        SpriteRenderer[] renderers =
            root.GetComponents<SpriteRenderer>();

        if (renderers.Length != 1)
        {
            errors.Add(
                $"{spec.RootName}: expected one root SpriteRenderer, " +
                $"found {renderers.Length}."
            );
            return null;
        }

        return new CharacterRoot(root.transform, renderers[0], spec);
    }

    private static WorldHealthBarUI FindHealthBar(
        Scene scene,
        Transform target)
    {
        WorldHealthBarUI matchingBar = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (WorldHealthBarUI bar in
                     root.GetComponentsInChildren<WorldHealthBarUI>(true))
            {
                SerializedObject data = new(bar);
                SerializedProperty targetProperty =
                    data.FindProperty("trackedTarget");

                if (targetProperty == null ||
                    targetProperty.objectReferenceValue != target)
                {
                    continue;
                }

                if (matchingBar != null)
                {
                    throw new InvalidOperationException(
                        $"{target.name}: duplicate WorldHealthBar target."
                    );
                }

                matchingBar = bar;
            }
        }

        if (matchingBar == null)
        {
            throw new InvalidOperationException(
                $"{target.name}: WorldHealthBar target is missing."
            );
        }

        return matchingBar;
    }

    private static T FindSingleComponent<T>(Scene scene)
        where T : Component
    {
        T found = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                if (found != null)
                {
                    return null;
                }

                found = component;
            }
        }

        return found;
    }

    private static Transform FindDescendant(
        Transform root,
        string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != root && child.name == name)
            {
                return child;
            }
        }

        return null;
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

    private static void ThrowIfErrors(
        string summary,
        List<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                summary + ":\n" + string.Join("\n", errors)
            );
        }
    }

    private sealed class CharacterSpec
    {
        public CharacterSpec(
            string rootName,
            string spritePath,
            Type controllerType,
            Vector3 expectedScale,
            params string[] requiredChildNames)
        {
            RootName = rootName;
            SpritePath = spritePath;
            ControllerType = controllerType;
            ExpectedScale = expectedScale;
            RequiredChildNames = requiredChildNames;
        }

        public string RootName { get; }
        public string SpritePath { get; }
        public Type ControllerType { get; }
        public Vector3 ExpectedScale { get; }
        public string[] RequiredChildNames { get; }
    }

    private sealed class SceneSpec
    {
        public SceneSpec(
            string scenePath,
            string name,
            CharacterSpec hero,
            CharacterSpec enemy)
        {
            ScenePath = scenePath;
            Name = name;
            Hero = hero;
            Enemy = enemy;
        }

        public string ScenePath { get; }
        public string Name { get; }
        public CharacterSpec Hero { get; }
        public CharacterSpec Enemy { get; }
    }

    private sealed class CharacterRoot
    {
        public CharacterRoot(
            Transform root,
            SpriteRenderer renderer,
            CharacterSpec spec)
        {
            Root = root;
            Renderer = renderer;
            Spec = spec;
        }

        public Transform Root { get; }
        public SpriteRenderer Renderer { get; }
        public CharacterSpec Spec { get; }
    }
}
