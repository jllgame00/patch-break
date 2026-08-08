using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds and configures CharacterPoseController on the existing character
/// roots. The setup only serializes pose-sprite references and leaves every
/// gameplay, physics, stage, and visual-hierarchy value untouched.
/// </summary>
public static class PatchBreakCharacterPoseSetup
{
    private const string MenuRoot =
        "Tools/PATCH BREAK/Character Pose/";
    private const float Tolerance = 0.002f;

    private static readonly SceneSpec[] SceneSpecs =
    {
        new SceneSpec(
            "Assets/Scenes/Battle.unity",
            "Battle",
            HeroSpec(),
            new CharacterSpec(
                "Golem",
                "Assets/Art/Characters/Golem/Sprites/golem_side_concept_b.png",
                "Assets/Art/Characters/Golem/Sprites/golem_side_concept_b_ready.png",
                null,
                typeof(GolemController),
                new Vector3(1.5f, 1.5f, 1f),
                new Vector2(1.25f, 1.3125f),
                new Vector2(0f, 0.6875f),
                new[]
                {
                    PointSpec.Exact(
                        "AttackPoint",
                        new Vector3(0.8f, 0.8125f, 0f)
                    )
                }
            )
        ),
        new SceneSpec(
            "Assets/Scenes/KnightBattle.unity",
            "KnightBattle",
            HeroSpec(),
            new CharacterSpec(
                "Knight",
                "Assets/Art/Characters/Knight/Sprites/knight_side_concept.png",
                "Assets/Art/Characters/Knight/Sprites/knight_side_concept_ready.png",
                null,
                typeof(KnightController),
                new Vector3(1.5f, 1.5f, 1f),
                new Vector2(0.5f, 1.5f),
                new Vector2(0f, 0.8125f),
                new[]
                {
                    PointSpec.Exact(
                        "MeleeAttackPoint",
                        new Vector3(0.8f, 0.95f, 0f)
                    ),
                    PointSpec.Exact(
                        "ProjectileSpawnPoint",
                        new Vector3(0.7f, 1.06f, 0f)
                    ),
                    PointSpec.YOnly("GuardIndicator", 0.95f)
                }
            )
        ),
        new SceneSpec(
            "Assets/Scenes/DebuggerBattle.unity",
            "DebuggerBattle",
            HeroSpec(),
            new CharacterSpec(
                "Debugger",
                "Assets/Art/Characters/Debugger/Sprites/debugger_boss_silhouette.png",
                null,
                "Assets/Art/Characters/Debugger/Sprites/debugger_boss_silhouette_phase.png",
                typeof(DebuggerController),
                new Vector3(1.8f, 1.8f, 1f),
                new Vector2(1.0625f, 2.03125f),
                new Vector2(0f, 1.203125f),
                new[]
                {
                    PointSpec.Exact(
                        "MeleeAttackPoint",
                        new Vector3(0.8f, 1.34f, 0f)
                    ),
                    PointSpec.Exact(
                        "ProjectileSpawnPoint",
                        new Vector3(0.7f, 1.55f, 0f)
                    ),
                    PointSpec.YOnly("GuardIndicator", 1.34f)
                }
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
        Debug.Log("PATCH_BREAK_CHARACTER_POSE_BATTLE_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Setup All Scenes")]
    public static void SetupAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SetupScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_CHARACTER_POSE_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Validate All Scenes")]
    public static void ValidateAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        ValidateScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_CHARACTER_POSE_VALIDATION_COMPLETE");
    }

    private static CharacterSpec HeroSpec()
    {
        return new CharacterSpec(
            "Hero",
            "Assets/Art/Characters/Hero/Sprites/hero_side_concept_b.png",
            "Assets/Art/Characters/Hero/Sprites/hero_side_concept_b_ready.png",
            null,
            typeof(HeroController),
            Vector3.one,
            new Vector2(0.375f, 1.0625f),
            new Vector2(-0.125f, 0.53125f),
            new[]
            {
                PointSpec.Exact(
                    "AttackPoint",
                    new Vector3(1f, 0.625f, 0f)
                )
            }
        );
    }

    private static bool PrepareEditorOperation()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "PATCH//BREAK character pose setup cannot run in Play Mode."
            );
            return false;
        }

        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static void SetupScenes(IEnumerable<SceneSpec> specs)
    {
        List<SceneSpec> targets = new List<SceneSpec>(specs);
        SceneSetup[] originalSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            // Check all immutable prerequisites before writing any target.
            foreach (SceneSpec spec in targets)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    spec.ScenePath,
                    OpenSceneMode.Single
                );
                ValidatePrerequisitesOrThrow(scene, spec);
            }

            foreach (SceneSpec spec in targets)
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

    private static void SetupScene(Scene scene, SceneSpec spec)
    {
        CharacterRoot hero = FindCharacterRoot(scene, spec.Hero);
        CharacterRoot enemy = FindCharacterRoot(scene, spec.Enemy);
        ConfigurePoseController(hero);
        ConfigurePoseController(enemy);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ConfigurePoseController(CharacterRoot character)
    {
        CharacterPoseController[] controllers =
            character.Root.GetComponents<CharacterPoseController>();

        if (controllers.Length > 1)
        {
            throw new InvalidOperationException(
                $"{character.Spec.RootName}: duplicate CharacterPoseController."
            );
        }

        CharacterPoseController pose = controllers.Length == 1
            ? controllers[0]
            : character.Root.gameObject.AddComponent<CharacterPoseController>();

        SerializedObject data = new SerializedObject(pose);
        SetReference(
            data,
            "targetRenderer",
            character.Renderer,
            character.Spec.RootName
        );
        SetReference(
            data,
            "baseSprite",
            LoadSprite(character.Spec.BaseSpritePath),
            character.Spec.RootName
        );
        SetReference(
            data,
            "readySprite",
            LoadSprite(character.Spec.ReadySpritePath),
            character.Spec.RootName
        );
        SetReference(
            data,
            "phaseSprite",
            LoadSprite(character.Spec.PhaseSpritePath),
            character.Spec.RootName
        );
        data.ApplyModifiedPropertiesWithoutUndo();

        // Scenes always serialize the travel/base pose. Runtime stage hooks
        // perform the one-way Base -> Ready swaps at explicit state changes.
        pose.SetBasePose();
        EditorUtility.SetDirty(pose);
        EditorUtility.SetDirty(character.Renderer);
    }

    private static void SetReference(
        SerializedObject data,
        string propertyName,
        UnityEngine.Object value,
        string rootName)
    {
        SerializedProperty property = data.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                $"{rootName}: CharacterPoseController.{propertyName} is missing."
            );
        }

        property.objectReferenceValue = value;
    }

    private static Sprite LoadSprite(string path)
    {
        return string.IsNullOrEmpty(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void ValidatePrerequisitesOrThrow(
        Scene scene,
        SceneSpec spec)
    {
        List<string> errors = new List<string>();
        ValidateCharacter(
            TryFindCharacterRoot(scene, spec.Hero, errors),
            errors,
            requirePoseController: false
        );
        ValidateCharacter(
            TryFindCharacterRoot(scene, spec.Enemy, errors),
            errors,
            requirePoseController: false
        );
        ValidateGround(scene, errors);
        ValidateStageAndParallax(scene, spec, errors);
        ThrowIfErrors($"{spec.Name}: character pose prerequisites failed", errors);
    }

    private static void ValidateSceneOrThrow(Scene scene, SceneSpec spec)
    {
        List<string> errors = new List<string>();
        CharacterRoot hero = TryFindCharacterRoot(scene, spec.Hero, errors);
        CharacterRoot enemy = TryFindCharacterRoot(scene, spec.Enemy, errors);

        ValidateCharacter(hero, errors, requirePoseController: true);
        ValidateCharacter(enemy, errors, requirePoseController: true);
        ValidateGround(scene, errors);
        ValidateStageAndParallax(scene, spec, errors);
        ValidateNoMissingComponents(scene, spec.Name, errors);
        ValidateBrokenObjectReferences(scene, spec.Name, errors);
        ThrowIfErrors($"{spec.Name}: character pose validation failed", errors);
    }

    private static void ValidateCharacter(
        CharacterRoot character,
        List<string> errors,
        bool requirePoseController)
    {
        if (character == null)
        {
            return;
        }

        Sprite baseSprite = LoadSprite(character.Spec.BaseSpritePath);
        Sprite readySprite = LoadSprite(character.Spec.ReadySpritePath);
        Sprite phaseSprite = LoadSprite(character.Spec.PhaseSpritePath);

        if (baseSprite == null || character.Renderer.sprite != baseSprite)
        {
            errors.Add(
                $"{character.Spec.RootName}: root Base Sprite mapping is invalid."
            );
        }

        if (character.Root.localScale != character.Spec.ExpectedScale ||
            character.Root.GetComponent<Rigidbody2D>() == null ||
            character.Root.GetComponent<Health>() == null ||
            character.Root.GetComponent(character.Spec.ControllerType) == null ||
            !Approximately(character.Collider.size, character.Spec.ColliderSize) ||
            !Approximately(character.Collider.offset, character.Spec.ColliderOffset))
        {
            errors.Add(
                $"{character.Spec.RootName}: existing root setup was changed."
            );
        }

        foreach (PointSpec pointSpec in character.Spec.Points)
        {
            Transform point;

            try
            {
                point = FindUniqueDescendant(character.Root, pointSpec.Name);
            }
            catch (InvalidOperationException exception)
            {
                errors.Add(exception.Message);
                continue;
            }

            if (pointSpec.PreserveX)
            {
                if (!Mathf.Approximately(
                        point.localPosition.y,
                        pointSpec.Position.y))
                {
                    errors.Add(
                        $"{character.Spec.RootName}/{pointSpec.Name}: local Y changed."
                    );
                }
            }
            else if ((point.localPosition - pointSpec.Position).sqrMagnitude >
                     Tolerance * Tolerance)
            {
                errors.Add(
                    $"{character.Spec.RootName}/{pointSpec.Name}: local position changed."
                );
            }
        }

        CharacterPoseController[] controllers =
            character.Root.GetComponents<CharacterPoseController>();

        if (!requirePoseController)
        {
            if (controllers.Length > 1)
            {
                errors.Add(
                    $"{character.Spec.RootName}: duplicate CharacterPoseController."
                );
            }

            return;
        }

        if (controllers.Length != 1)
        {
            errors.Add(
                $"{character.Spec.RootName}: CharacterPoseController is missing."
            );
            return;
        }

        CharacterPoseController pose = controllers[0];

        if (pose.TargetRenderer != character.Renderer ||
            pose.BaseSprite != baseSprite ||
            pose.ReadySprite != readySprite ||
            pose.PhaseSprite != phaseSprite)
        {
            errors.Add(
                $"{character.Spec.RootName}: pose sprite references are invalid."
            );
        }
    }

    private static void ValidateGround(Scene scene, List<string> errors)
    {
        GameObject ground = FindSingleRoot(scene, "Ground", errors);

        if (ground == null)
        {
            return;
        }

        SpriteRenderer renderer = ground.GetComponent<SpriteRenderer>();
        BoxCollider2D collider = ground.GetComponent<BoxCollider2D>();

        if (renderer == null || renderer.enabled || collider == null ||
            !collider.enabled || collider.isTrigger)
        {
            errors.Add("Ground: visual/physics alignment changed.");
        }
    }

    private static void ValidateStageAndParallax(
        Scene scene,
        SceneSpec spec,
        List<string> errors)
    {
        StageBattleSequenceController stage =
            FindSingleComponent<StageBattleSequenceController>(scene);

        if (stage == null)
        {
            errors.Add("StageBattleSequenceController is missing or duplicated.");
            return;
        }

        CharacterRoot hero = TryFindCharacterRoot(scene, spec.Hero, errors);
        CharacterRoot enemy = TryFindCharacterRoot(scene, spec.Enemy, errors);
        SerializedObject data = new SerializedObject(stage);
        ValidateReference(data, "hero", hero != null ? hero.Root : null, errors);
        ValidateReference(data, "enemy", enemy != null ? enemy.Root : null, errors);

        SerializedProperty parallaxProperty =
            data.FindProperty("infiniteParallaxBackground");
        InfiniteParallaxBackground parallax = parallaxProperty != null
            ? parallaxProperty.objectReferenceValue as InfiniteParallaxBackground
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

        if (property == null || property.objectReferenceValue != expected)
        {
            errors.Add(
                $"StageBattleSequenceController.{propertyName} reference is invalid."
            );
        }
    }

    private static CharacterRoot FindCharacterRoot(
        Scene scene,
        CharacterSpec spec)
    {
        List<string> errors = new List<string>();
        CharacterRoot root = TryFindCharacterRoot(scene, spec, errors);
        ThrowIfErrors($"{scene.name}: character lookup failed", errors);
        return root;
    }

    private static CharacterRoot TryFindCharacterRoot(
        Scene scene,
        CharacterSpec spec,
        List<string> errors)
    {
        GameObject root = FindSingleRoot(scene, spec.RootName, errors);

        if (root == null)
        {
            return null;
        }

        SpriteRenderer[] renderers = root.GetComponents<SpriteRenderer>();
        BoxCollider2D collider = root.GetComponent<BoxCollider2D>();

        if (renderers.Length != 1 || collider == null)
        {
            errors.Add(
                $"{spec.RootName}: root renderer or BoxCollider2D is missing."
            );
            return null;
        }

        return new CharacterRoot(root.transform, renderers[0], collider, spec);
    }

    private static GameObject FindSingleRoot(
        Scene scene,
        string rootName,
        List<string> errors)
    {
        GameObject result = null;

        foreach (GameObject candidate in scene.GetRootGameObjects())
        {
            if (candidate.name != rootName)
            {
                continue;
            }

            if (result != null)
            {
                errors.Add($"{scene.name}: duplicate root '{rootName}'.");
                return null;
            }

            result = candidate;
        }

        if (result == null)
        {
            errors.Add($"{scene.name}: root '{rootName}' is missing.");
        }

        return result;
    }

    private static Transform FindUniqueDescendant(
        Transform root,
        string name)
    {
        Transform result = null;

        foreach (Transform transform in
                 root.GetComponentsInChildren<Transform>(true))
        {
            if (transform == root || transform.name != name)
            {
                continue;
            }

            if (result != null)
            {
                throw new InvalidOperationException(
                    $"{root.name}: duplicate descendant '{name}'."
                );
            }

            result = transform;
        }

        if (result == null)
        {
            throw new InvalidOperationException(
                $"{root.name}: descendant '{name}' is missing."
            );
        }

        return result;
    }

    private static T FindSingleComponent<T>(Scene scene)
        where T : Component
    {
        T result = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                if (result != null)
                {
                    return null;
                }

                result = component;
            }
        }

        return result;
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
                foreach (Component component in transform.GetComponents<Component>())
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

                SerializedObject data = new SerializedObject(component);
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

    private static bool Approximately(Vector2 left, Vector2 right)
    {
        return Mathf.Abs(left.x - right.x) <= Tolerance &&
               Mathf.Abs(left.y - right.y) <= Tolerance;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        List<string> names = new List<string>();

        for (Transform current = transform;
             current != null;
             current = current.parent)
        {
            names.Insert(0, current.name);
        }

        return string.Join("/", names);
    }

    private static void ThrowIfErrors(string title, List<string> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        string message = "PATCH//BREAK " + title + ":\n" +
            string.Join("\n", errors);
        Debug.LogError(message);
        throw new InvalidOperationException(message);
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

    private sealed class CharacterSpec
    {
        public CharacterSpec(
            string rootName,
            string baseSpritePath,
            string readySpritePath,
            string phaseSpritePath,
            Type controllerType,
            Vector3 expectedScale,
            Vector2 colliderSize,
            Vector2 colliderOffset,
            PointSpec[] points)
        {
            RootName = rootName;
            BaseSpritePath = baseSpritePath;
            ReadySpritePath = readySpritePath;
            PhaseSpritePath = phaseSpritePath;
            ControllerType = controllerType;
            ExpectedScale = expectedScale;
            ColliderSize = colliderSize;
            ColliderOffset = colliderOffset;
            Points = points;
        }

        public string RootName { get; }
        public string BaseSpritePath { get; }
        public string ReadySpritePath { get; }
        public string PhaseSpritePath { get; }
        public Type ControllerType { get; }
        public Vector3 ExpectedScale { get; }
        public Vector2 ColliderSize { get; }
        public Vector2 ColliderOffset { get; }
        public PointSpec[] Points { get; }
    }

    private sealed class CharacterRoot
    {
        public CharacterRoot(
            Transform root,
            SpriteRenderer renderer,
            BoxCollider2D collider,
            CharacterSpec spec)
        {
            Root = root;
            Renderer = renderer;
            Collider = collider;
            Spec = spec;
        }

        public Transform Root { get; }
        public SpriteRenderer Renderer { get; }
        public BoxCollider2D Collider { get; }
        public CharacterSpec Spec { get; }
    }

    private sealed class PointSpec
    {
        private PointSpec(string name, Vector3 position, bool preserveX)
        {
            Name = name;
            Position = position;
            PreserveX = preserveX;
        }

        public string Name { get; }
        public Vector3 Position { get; }
        public bool PreserveX { get; }

        public static PointSpec Exact(string name, Vector3 position)
        {
            return new PointSpec(name, position, false);
        }

        public static PointSpec YOnly(string name, float y)
        {
            return new PointSpec(name, new Vector3(0f, y, 0f), true);
        }
    }
}
