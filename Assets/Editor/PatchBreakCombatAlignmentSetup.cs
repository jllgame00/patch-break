using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Aligns existing combat-origin child transforms to the current static
/// character art. It never changes combat values, collider/ground data,
/// stage markers, runtime controller code, or scene hierarchy.
/// </summary>
public static class PatchBreakCombatAlignmentSetup
{
    private const string MenuRoot =
        "Tools/PATCH BREAK/Combat Alignment/";
    private const float Tolerance = 0.002f;

    private static readonly SceneSpec[] SceneSpecs =
    {
        new SceneSpec(
            "Assets/Scenes/Battle.unity",
            "Battle",
            CreateHeroSpec(),
            new ActorSpec(
                "Golem",
                "Assets/Art/Characters/Golem/Sprites/golem_side_concept_b.png",
                typeof(GolemController),
                new Vector3(1.5f, 1.5f, 1f),
                new Vector2(1.25f, 1.3125f),
                new Vector2(0f, 0.6875f),
                new OpaqueBounds(7, 63, 1, 45),
                new[]
                {
                    PointSpec.Melee(
                        "AttackPoint",
                        new Vector3(0.8f, 0.8125f, 0f)
                    )
                },
                new[]
                {
                    new ReferenceSpec(
                        typeof(GolemController),
                        "attackPoint",
                        "AttackPoint",
                        false
                    )
                },
                typeof(GolemController),
                "attackRadius",
                1.2f
            )
        ),
        new SceneSpec(
            "Assets/Scenes/KnightBattle.unity",
            "KnightBattle",
            CreateHeroSpec(),
            new ActorSpec(
                "Knight",
                "Assets/Art/Characters/Knight/Sprites/knight_side_concept.png",
                typeof(KnightController),
                new Vector3(1.5f, 1.5f, 1f),
                new Vector2(0.5f, 1.5f),
                new Vector2(0f, 0.8125f),
                new OpaqueBounds(20, 46, 2, 51),
                new[]
                {
                    PointSpec.Melee(
                        "MeleeAttackPoint",
                        new Vector3(0.8f, 0.95f, 0f)
                    ),
                    PointSpec.Projectile(
                        "ProjectileSpawnPoint",
                        new Vector3(0.7f, 1.06f, 0f)
                    ),
                    PointSpec.Guard("GuardIndicator", 0.95f)
                },
                new[]
                {
                    new ReferenceSpec(
                        typeof(KnightController),
                        "meleeAttackPoint",
                        "MeleeAttackPoint",
                        false
                    ),
                    new ReferenceSpec(
                        typeof(KnightController),
                        "projectileSpawnPoint",
                        "ProjectileSpawnPoint",
                        false
                    ),
                    new ReferenceSpec(
                        typeof(KnightController),
                        "guardIndicator",
                        "GuardIndicator",
                        true
                    )
                },
                typeof(KnightController),
                "meleeRadius",
                1.1f
            )
        ),
        new SceneSpec(
            "Assets/Scenes/DebuggerBattle.unity",
            "DebuggerBattle",
            CreateHeroSpec(),
            new ActorSpec(
                "Debugger",
                "Assets/Art/Characters/Debugger/Sprites/debugger_boss_silhouette.png",
                typeof(DebuggerController),
                new Vector3(1.8f, 1.8f, 1f),
                new Vector2(1.0625f, 2.03125f),
                new Vector2(0f, 1.203125f),
                new OpaqueBounds(21, 67, 6, 72),
                new[]
                {
                    PointSpec.Melee(
                        "MeleeAttackPoint",
                        new Vector3(0.8f, 1.34f, 0f)
                    ),
                    PointSpec.Projectile(
                        "ProjectileSpawnPoint",
                        new Vector3(0.7f, 1.55f, 0f)
                    ),
                    PointSpec.Guard("GuardIndicator", 1.34f)
                },
                new[]
                {
                    new ReferenceSpec(
                        typeof(DebuggerController),
                        "meleeAttackPoint",
                        "MeleeAttackPoint",
                        false
                    ),
                    new ReferenceSpec(
                        typeof(DebuggerController),
                        "projectileSpawnPoint",
                        "ProjectileSpawnPoint",
                        false
                    ),
                    new ReferenceSpec(
                        typeof(DebuggerController),
                        "guardIndicator",
                        "GuardIndicator",
                        true
                    )
                },
                typeof(DebuggerController),
                "meleeRadius",
                1.1f
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
        Debug.Log("PATCH_BREAK_COMBAT_ALIGNMENT_BATTLE_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Setup All Scenes")]
    public static void SetupAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SetupScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_COMBAT_ALIGNMENT_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Validate All Scenes")]
    public static void ValidateAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        ValidateScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_COMBAT_ALIGNMENT_VALIDATION_COMPLETE");
    }

    private static ActorSpec CreateHeroSpec()
    {
        return new ActorSpec(
            "Hero",
            "Assets/Art/Characters/Hero/Sprites/hero_side_concept_b.png",
            typeof(HeroController),
            Vector3.one,
            new Vector2(0.375f, 1.0625f),
            new Vector2(-0.125f, 0.53125f),
            new OpaqueBounds(19, 37, 0, 36),
            new[]
            {
                PointSpec.Melee(
                    "AttackPoint",
                    new Vector3(1.0f, 0.625f, 0f)
                )
            },
            new[]
            {
                new ReferenceSpec(
                    typeof(HeroAttack),
                    "attackPoint",
                    "AttackPoint",
                    false
                )
            },
            typeof(HeroAttack),
            "attackRange",
            1.2f
        );
    }

    private static bool PrepareEditorOperation()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "PATCH//BREAK combat alignment cannot run in Play Mode."
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
            // Validate every target before writing any scene, so Setup All is
            // atomic when a scene has not received art/physics setup yet.
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
        ActorRoot hero = FindActorRoot(scene, spec.Hero);
        ActorRoot enemy = FindActorRoot(scene, spec.Enemy);
        ApplyPointPositions(hero);
        ApplyPointPositions(enemy);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ApplyPointPositions(ActorRoot actor)
    {
        foreach (PointSpec pointSpec in actor.Spec.Points)
        {
            Transform point = FindUniqueDescendant(
                actor.Root,
                pointSpec.Name
            );
            Vector3 position = point.localPosition;

            if (pointSpec.PreserveX)
            {
                position.y = pointSpec.Position.y;
            }
            else
            {
                position = pointSpec.Position;
            }

            point.localPosition = position;
            EditorUtility.SetDirty(point);
        }
    }

    private static void ValidatePrerequisitesOrThrow(
        Scene scene,
        SceneSpec spec)
    {
        List<string> errors = new List<string>();
        // The setup starts from the legacy, foot-level positions. Only validate
        // immutable scene structure here; exact point positions are checked
        // after the tool writes the new serialized values.
        ValidateActor(
            TryFindActorRoot(scene, spec.Hero, errors),
            errors,
            validatePointPositions: false
        );
        ValidateActor(
            TryFindActorRoot(scene, spec.Enemy, errors),
            errors,
            validatePointPositions: false
        );
        ValidateGround(scene, errors);
        ThrowIfErrors($"{spec.Name}: combat alignment prerequisites failed", errors);
    }

    private static void ValidateSceneOrThrow(Scene scene, SceneSpec spec)
    {
        List<string> errors = new List<string>();
        ActorRoot hero = TryFindActorRoot(scene, spec.Hero, errors);
        ActorRoot enemy = TryFindActorRoot(scene, spec.Enemy, errors);

        ValidateActor(hero, errors);
        ValidateActor(enemy, errors);
        ValidateGround(scene, errors);
        ValidateStageAndParallax(scene, hero, enemy, errors);
        ValidateNoMissingComponents(scene, spec.Name, errors);
        ValidateBrokenObjectReferences(scene, spec.Name, errors);
        ThrowIfErrors($"{spec.Name}: combat alignment validation failed", errors);
    }

    private static void ValidateActor(
        ActorRoot actor,
        List<string> errors,
        bool validatePointPositions = true)
    {
        if (actor == null)
        {
            return;
        }

        Sprite expectedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            actor.Spec.SpritePath
        );

        if (expectedSprite == null || actor.Renderer.sprite != expectedSprite)
        {
            errors.Add($"{actor.Spec.RootName}: SpriteRenderer mapping is invalid.");
        }

        if (actor.Root.localScale != actor.Spec.ExpectedScale)
        {
            errors.Add($"{actor.Spec.RootName}: root scale was changed.");
        }

        if (actor.Root.GetComponent<Rigidbody2D>() == null ||
            actor.Root.GetComponent<Health>() == null ||
            actor.Root.GetComponent(actor.Spec.ControllerType) == null ||
            !Approximately(actor.Collider.size, actor.Spec.ColliderSize) ||
            !Approximately(actor.Collider.offset, actor.Spec.ColliderOffset))
        {
            errors.Add(
                $"{actor.Spec.RootName}: root physics/gameplay setup changed."
            );
        }

        Component radiusOwner = actor.Root.GetComponent(
            actor.Spec.RadiusOwnerType
        );
        SerializedObject radiusData = radiusOwner != null
            ? new SerializedObject(radiusOwner)
            : null;
        SerializedProperty radiusProperty = radiusData != null
            ? radiusData.FindProperty(actor.Spec.RadiusProperty)
            : null;

        if (radiusProperty == null ||
            !Mathf.Approximately(
                radiusProperty.floatValue,
                actor.Spec.ExpectedRadius))
        {
            errors.Add(
                $"{actor.Spec.RootName}: attack radius was changed."
            );
        }

        BodyBounds bodyBounds;

        try
        {
            bodyBounds = GetBodyBounds(actor);
        }
        catch (InvalidOperationException exception)
        {
            errors.Add(exception.Message);
            return;
        }

        foreach (PointSpec pointSpec in actor.Spec.Points)
        {
            Transform point;

            try
            {
                point = FindUniqueDescendant(actor.Root, pointSpec.Name);
            }
            catch (InvalidOperationException exception)
            {
                errors.Add(exception.Message);
                continue;
            }

            if (validatePointPositions)
            {
                ValidatePoint(actor, point, pointSpec, bodyBounds, errors);
            }
        }

        foreach (ReferenceSpec reference in actor.Spec.References)
        {
            ValidatePointReference(actor, reference, errors);
        }

        ValidateTelegraphParents(actor, errors);
    }

    private static void ValidatePoint(
        ActorRoot actor,
        Transform point,
        PointSpec spec,
        BodyBounds body,
        List<string> errors)
    {
        if (spec.PreserveX)
        {
            if (!Mathf.Approximately(point.localPosition.y, spec.Position.y))
            {
                errors.Add(
                    $"{actor.Spec.RootName}/{spec.Name}: local Y is incorrect."
                );
            }
        }
        else if ((point.localPosition - spec.Position).sqrMagnitude >
                 Tolerance * Tolerance)
        {
            errors.Add(
                $"{actor.Spec.RootName}/{spec.Name}: local position is incorrect."
            );
        }

        float normalizedY = (point.localPosition.y - body.MinY) /
            Mathf.Max(0.001f, body.Height);

        if (normalizedY < spec.MinimumBodyHeight ||
            normalizedY > spec.MaximumBodyHeight)
        {
            errors.Add(
                $"{actor.Spec.RootName}/{spec.Name}: not at a valid body height."
            );
        }

        // Combat origins deliberately retain their established local X values
        // to preserve reach and projectile timing. The exact expected X is
        // already checked above; only require that a non-guard point remains
        // on the forward side of its root, not inside the sprite silhouette.
        if (!spec.PreserveX && point.localPosition.x <= 0f)
        {
            errors.Add(
                $"{actor.Spec.RootName}/{spec.Name}: not at a valid forward " +
                "origin position."
            );
        }
    }

    private static void ValidatePointReference(
        ActorRoot actor,
        ReferenceSpec reference,
        List<string> errors)
    {
        Component owner = actor.Root.GetComponent(reference.OwnerType);
        Transform point;

        try
        {
            point = FindUniqueDescendant(actor.Root, reference.PointName);
        }
        catch (InvalidOperationException exception)
        {
            errors.Add(exception.Message);
            return;
        }

        if (owner == null)
        {
            errors.Add(
                $"{actor.Spec.RootName}: {reference.OwnerType.Name} is missing."
            );
            return;
        }

        SerializedProperty property = new SerializedObject(owner).FindProperty(
            reference.PropertyName
        );
        UnityEngine.Object expected = reference.ExpectsGameObject
            ? point.gameObject
            : point;

        if (property == null || property.objectReferenceValue != expected)
        {
            errors.Add(
                $"{actor.Spec.RootName}: {reference.PropertyName} reference " +
                "is invalid."
            );
        }
    }

    private static void ValidateTelegraphParents(
        ActorRoot actor,
        List<string> errors)
    {
        if (actor.Spec.RootName != "Knight" &&
            actor.Spec.RootName != "Debugger")
        {
            return;
        }

        try
        {
            Transform meleePoint = FindUniqueDescendant(
                actor.Root,
                "MeleeAttackPoint"
            );
            Transform projectilePoint = FindUniqueDescendant(
                actor.Root,
                "ProjectileSpawnPoint"
            );
            Transform meleeTelegraph = FindUniqueDescendant(
                meleePoint,
                "MeleeAttackTelegraph"
            );
            Transform projectileTelegraph = FindUniqueDescendant(
                projectilePoint,
                "ProjectileTelegraph"
            );

            if (meleeTelegraph.parent != meleePoint ||
                projectileTelegraph.parent != projectilePoint)
            {
                errors.Add(
                    $"{actor.Spec.RootName}: combat telegraph parenting changed."
                );
            }
        }
        catch (InvalidOperationException exception)
        {
            errors.Add(exception.Message);
        }
    }

    private static BodyBounds GetBodyBounds(ActorRoot actor)
    {
        Sprite sprite = actor.Renderer.sprite;

        if (sprite == null || sprite.pixelsPerUnit <= 0f)
        {
            throw new InvalidOperationException(
                $"{actor.Spec.RootName}: Sprite bounds cannot be calculated."
            );
        }

        float minX = (actor.Spec.Opaque.MinX - sprite.rect.x -
            sprite.pivot.x) / sprite.pixelsPerUnit;
        float maxX = (actor.Spec.Opaque.MaxX - sprite.rect.x -
            sprite.pivot.x) / sprite.pixelsPerUnit;
        float minY = (actor.Spec.Opaque.MinY - sprite.rect.y -
            sprite.pivot.y) / sprite.pixelsPerUnit;
        float maxY = (actor.Spec.Opaque.MaxY - sprite.rect.y -
            sprite.pivot.y) / sprite.pixelsPerUnit;

        if (maxY <= minY)
        {
            throw new InvalidOperationException(
                $"{actor.Spec.RootName}: opaque body bounds are invalid."
            );
        }

        return new BodyBounds(minX, maxX, minY, maxY);
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
            errors.Add(
                "Ground: visual/physics configuration changed from the physics pass."
            );
        }
    }

    private static void ValidateStageAndParallax(
        Scene scene,
        ActorRoot hero,
        ActorRoot enemy,
        List<string> errors)
    {
        StageBattleSequenceController stage =
            FindSingleComponent<StageBattleSequenceController>(scene);

        if (stage == null)
        {
            errors.Add("StageBattleSequenceController is missing.");
            return;
        }

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

    private static ActorRoot FindActorRoot(Scene scene, ActorSpec spec)
    {
        List<string> errors = new List<string>();
        ActorRoot actor = TryFindActorRoot(scene, spec, errors);
        ThrowIfErrors($"{scene.name}: actor lookup failed", errors);
        return actor;
    }

    private static ActorRoot TryFindActorRoot(
        Scene scene,
        ActorSpec spec,
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
                $"{spec.RootName}: required root renderer or BoxCollider2D is missing."
            );
            return null;
        }

        return new ActorRoot(root.transform, renderers[0], collider, spec);
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
            ActorSpec hero,
            ActorSpec enemy)
        {
            ScenePath = scenePath;
            Name = name;
            Hero = hero;
            Enemy = enemy;
        }

        public string ScenePath { get; }
        public string Name { get; }
        public ActorSpec Hero { get; }
        public ActorSpec Enemy { get; }
    }

    private sealed class ActorSpec
    {
        public ActorSpec(
            string rootName,
            string spritePath,
            Type controllerType,
            Vector3 expectedScale,
            Vector2 colliderSize,
            Vector2 colliderOffset,
            OpaqueBounds opaque,
            PointSpec[] points,
            ReferenceSpec[] references,
            Type radiusOwnerType,
            string radiusProperty,
            float expectedRadius)
        {
            RootName = rootName;
            SpritePath = spritePath;
            ControllerType = controllerType;
            ExpectedScale = expectedScale;
            ColliderSize = colliderSize;
            ColliderOffset = colliderOffset;
            Opaque = opaque;
            Points = points;
            References = references;
            RadiusOwnerType = radiusOwnerType;
            RadiusProperty = radiusProperty;
            ExpectedRadius = expectedRadius;
        }

        public string RootName { get; }
        public string SpritePath { get; }
        public Type ControllerType { get; }
        public Vector3 ExpectedScale { get; }
        public Vector2 ColliderSize { get; }
        public Vector2 ColliderOffset { get; }
        public OpaqueBounds Opaque { get; }
        public PointSpec[] Points { get; }
        public ReferenceSpec[] References { get; }
        public Type RadiusOwnerType { get; }
        public string RadiusProperty { get; }
        public float ExpectedRadius { get; }
    }

    private sealed class ActorRoot
    {
        public ActorRoot(
            Transform root,
            SpriteRenderer renderer,
            BoxCollider2D collider,
            ActorSpec spec)
        {
            Root = root;
            Renderer = renderer;
            Collider = collider;
            Spec = spec;
        }

        public Transform Root { get; }
        public SpriteRenderer Renderer { get; }
        public BoxCollider2D Collider { get; }
        public ActorSpec Spec { get; }
    }

    private sealed class PointSpec
    {
        private const float MeleeMinHeight = 0.40f;
        private const float MeleeMaxHeight = 0.65f;
        private const float ProjectileMinHeight = 0.52f;
        private const float ProjectileMaxHeight = 0.75f;
        private const float GuardMinHeight = 0.45f;
        private const float GuardMaxHeight = 0.68f;

        private PointSpec(
            string name,
            Vector3 position,
            bool preserveX,
            float minimumBodyHeight,
            float maximumBodyHeight)
        {
            Name = name;
            Position = position;
            PreserveX = preserveX;
            MinimumBodyHeight = minimumBodyHeight;
            MaximumBodyHeight = maximumBodyHeight;
        }

        public string Name { get; }
        public Vector3 Position { get; }
        public bool PreserveX { get; }
        public float MinimumBodyHeight { get; }
        public float MaximumBodyHeight { get; }

        public static PointSpec Melee(string name, Vector3 position)
        {
            return new PointSpec(
                name,
                position,
                false,
                MeleeMinHeight,
                MeleeMaxHeight
            );
        }

        public static PointSpec Projectile(string name, Vector3 position)
        {
            return new PointSpec(
                name,
                position,
                false,
                ProjectileMinHeight,
                ProjectileMaxHeight
            );
        }

        public static PointSpec Guard(string name, float y)
        {
            return new PointSpec(
                name,
                new Vector3(0f, y, 0f),
                true,
                GuardMinHeight,
                GuardMaxHeight
            );
        }
    }

    private sealed class ReferenceSpec
    {
        public ReferenceSpec(
            Type ownerType,
            string propertyName,
            string pointName,
            bool expectsGameObject)
        {
            OwnerType = ownerType;
            PropertyName = propertyName;
            PointName = pointName;
            ExpectsGameObject = expectsGameObject;
        }

        public Type OwnerType { get; }
        public string PropertyName { get; }
        public string PointName { get; }
        public bool ExpectsGameObject { get; }
    }

    private readonly struct OpaqueBounds
    {
        public OpaqueBounds(int minX, int maxX, int minY, int maxY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }

        public int MinX { get; }
        public int MaxX { get; }
        public int MinY { get; }
        public int MaxY { get; }
    }

    private readonly struct BodyBounds
    {
        public BodyBounds(float minX, float maxX, float minY, float maxY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }

        public float MinX { get; }
        public float MaxX { get; }
        public float MinY { get; }
        public float MaxY { get; }
        public float Height => MaxY - MinY;
    }
}
