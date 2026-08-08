using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Aligns the existing character BoxCollider2D components and the existing
/// invisible Ground plane to the already-configured character/near art. This
/// tool deliberately never changes character art, root transforms, gameplay
/// components, stage markers, or attack points.
/// </summary>
public static class PatchBreakCharacterPhysicsSetup
{
    private const string MenuRoot =
        "Tools/PATCH BREAK/Character Physics/";
    private const float CharacterPixelsPerUnit = 32f;
    // Source-texture row of the first full-width, flat floor line. The Near
    // Sprite is alpha-trimmed on import, so this must be converted through
    // Sprite.rect before using renderer bounds.
    private const float NearFlatFloorTextureRowFromTop = 300f;
    private const float AlignmentTolerance = 0.002f;

    private static readonly SceneSpec[] SceneSpecs =
    {
        new SceneSpec(
            "Assets/Scenes/Battle.unity",
            "Battle",
            new CharacterSpec(
                "Hero",
                "Assets/Art/Characters/Hero/Sprites/hero_side_concept_b.png",
                typeof(HeroController),
                new Vector3(1f, 1f, 1f),
                new Vector2(12f / CharacterPixelsPerUnit,
                            34f / CharacterPixelsPerUnit),
                -4f / CharacterPixelsPerUnit,
                0f,
                "AttackPoint"
            ),
            new CharacterSpec(
                "Golem",
                "Assets/Art/Characters/Golem/Sprites/golem_side_concept_b.png",
                typeof(GolemController),
                new Vector3(1.5f, 1.5f, 1f),
                new Vector2(40f / CharacterPixelsPerUnit,
                            42f / CharacterPixelsPerUnit),
                0f,
                1f,
                "AttackPoint"
            )
        ),
        new SceneSpec(
            "Assets/Scenes/KnightBattle.unity",
            "KnightBattle",
            new CharacterSpec(
                "Hero",
                "Assets/Art/Characters/Hero/Sprites/hero_side_concept_b.png",
                typeof(HeroController),
                new Vector3(1f, 1f, 1f),
                new Vector2(12f / CharacterPixelsPerUnit,
                            34f / CharacterPixelsPerUnit),
                -4f / CharacterPixelsPerUnit,
                0f,
                "AttackPoint"
            ),
            new CharacterSpec(
                "Knight",
                "Assets/Art/Characters/Knight/Sprites/knight_side_concept.png",
                typeof(KnightController),
                new Vector3(1.5f, 1.5f, 1f),
                new Vector2(16f / CharacterPixelsPerUnit,
                            48f / CharacterPixelsPerUnit),
                0f,
                2f,
                "MeleeAttackPoint",
                "ProjectileSpawnPoint",
                "GuardIndicator",
                "ProjectileTelegraph"
            )
        ),
        new SceneSpec(
            "Assets/Scenes/DebuggerBattle.unity",
            "DebuggerBattle",
            new CharacterSpec(
                "Hero",
                "Assets/Art/Characters/Hero/Sprites/hero_side_concept_b.png",
                typeof(HeroController),
                new Vector3(1f, 1f, 1f),
                new Vector2(12f / CharacterPixelsPerUnit,
                            34f / CharacterPixelsPerUnit),
                -4f / CharacterPixelsPerUnit,
                0f,
                "AttackPoint"
            ),
            new CharacterSpec(
                "Debugger",
                "Assets/Art/Characters/Debugger/Sprites/debugger_boss_silhouette.png",
                typeof(DebuggerController),
                new Vector3(1.8f, 1.8f, 1f),
                new Vector2(34f / CharacterPixelsPerUnit,
                            65f / CharacterPixelsPerUnit),
                0f,
                6f,
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
        Debug.Log("PATCH_BREAK_CHARACTER_PHYSICS_BATTLE_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Setup All Scenes")]
    public static void SetupAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SetupScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_CHARACTER_PHYSICS_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Validate All Scenes")]
    public static void ValidateAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        ValidateScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_CHARACTER_PHYSICS_VALIDATION_COMPLETE");
    }

    [MenuItem(MenuRoot + "Align Stage Baselines Battle First")]
    public static void AlignStageBaselinesBattleFirst()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        AlignStageBaselines(new[] { SceneSpecs[0] });
        Debug.Log("PATCH_BREAK_STAGE_BASELINES_BATTLE_ALIGNED");
    }

    [MenuItem(MenuRoot + "Align Stage Baselines All Scenes")]
    public static void AlignStageBaselinesAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        AlignStageBaselines(SceneSpecs);
        Debug.Log("PATCH_BREAK_STAGE_BASELINES_ALL_ALIGNED");
    }

    [MenuItem(MenuRoot + "Validate Stage Baselines")]
    public static void ValidateStageBaselines()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        ValidateStageBaselines(SceneSpecs);
        Debug.Log("PATCH_BREAK_STAGE_BASELINES_VALIDATED");
    }

    private static bool PrepareEditorOperation()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "PATCH//BREAK character physics setup cannot run in Play Mode."
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
            // Do not partially update Battle if a later scene is still using
            // temporary character art. Physics setup never changes mapping.
            foreach (SceneSpec spec in targets)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    spec.ScenePath,
                    OpenSceneMode.Single
                );
                ValidateSetupPrerequisites(scene, spec);
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

    private static void ValidateSetupPrerequisites(Scene scene, SceneSpec spec)
    {
        CharacterRoot hero = FindCharacterRoot(scene, spec.Hero);
        CharacterRoot enemy = FindCharacterRoot(scene, spec.Enemy);
        FindGround(scene);
        RequireExpectedSprite(hero);
        RequireExpectedSprite(enemy);
        GetNearFlatFloorWorldY(scene);
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

    private static void AlignStageBaselines(IEnumerable<SceneSpec> specs)
    {
        List<SceneSpec> targets = new List<SceneSpec>(specs);
        SceneSetup[] originalSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            // Keep this operation atomic: a scene still on the old art or
            // physics setup must be fixed first rather than receiving a
            // baseline calculated from obsolete collider data.
            foreach (SceneSpec spec in targets)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    spec.ScenePath,
                    OpenSceneMode.Single
                );
                ValidateBaselinePrerequisitesOrThrow(scene, spec);
            }

            foreach (SceneSpec spec in targets)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    spec.ScenePath,
                    OpenSceneMode.Single
                );
                AlignSceneStageBaselines(scene, spec);

                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        $"{spec.Name}: stage baseline alignment could not be saved."
                    );
                }

                Scene reopenedScene = EditorSceneManager.OpenScene(
                    spec.ScenePath,
                    OpenSceneMode.Single
                );
                ValidateStageBaselineOrThrow(reopenedScene, spec);
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

    private static void ValidateStageBaselines(IEnumerable<SceneSpec> specs)
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
                ValidateStageBaselineOrThrow(scene, spec);
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

    private static void AlignSceneStageBaselines(Scene scene, SceneSpec spec)
    {
        CharacterRoot hero = FindCharacterRoot(scene, spec.Hero);
        CharacterRoot enemy = FindCharacterRoot(scene, spec.Enemy);
        GroundRoot ground = FindGround(scene);
        StageMarkers markers = FindStageMarkers(scene, hero, enemy);
        float groundTopY = ground.Collider.bounds.max.y;
        float heroGroundedRootY = CalculateGroundedRootY(hero, groundTopY);
        float enemyGroundedRootY = CalculateGroundedRootY(enemy, groundTopY);

        SetMarkerWorldY(markers.HeroEntranceStart, heroGroundedRootY);
        SetMarkerWorldY(markers.HeroBattlePosition, heroGroundedRootY);
        SetMarkerWorldY(markers.HeroExitPoint, heroGroundedRootY);
        SetMarkerWorldY(markers.EnemyEntranceStart, enemyGroundedRootY);
        SetMarkerWorldY(markers.EnemyBattlePosition, enemyGroundedRootY);

        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void SetMarkerWorldY(Transform marker, float worldY)
    {
        Vector3 position = marker.position;
        position.y = worldY;
        marker.position = position;
        EditorUtility.SetDirty(marker);
    }

    private static float CalculateGroundedRootY(
        CharacterRoot character,
        float groundTopY)
    {
        Vector3 colliderBottomLocal = new Vector3(
            character.Collider.offset.x,
            character.Collider.offset.y -
                character.Collider.size.y * 0.5f,
            0f
        );
        float colliderBottomWorldOffset =
            character.Root.TransformPoint(colliderBottomLocal).y -
            character.Root.position.y;

        return groundTopY - colliderBottomWorldOffset;
    }

    private static void SetupScene(Scene scene, SceneSpec spec)
    {
        CharacterRoot hero = FindCharacterRoot(scene, spec.Hero);
        CharacterRoot enemy = FindCharacterRoot(scene, spec.Enemy);
        GroundRoot ground = FindGround(scene);

        RequireExpectedSprite(hero);
        RequireExpectedSprite(enemy);

        ConfigureCharacterCollider(hero);
        ConfigureCharacterCollider(enemy);
        ConfigureGround(scene, ground);

        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ConfigureCharacterCollider(CharacterRoot character)
    {
        character.Collider.size = character.Spec.ColliderSize;
        character.Collider.offset = GetExpectedColliderOffset(character);
        EditorUtility.SetDirty(character.Collider);
    }

    private static void ConfigureGround(Scene scene, GroundRoot ground)
    {
        float targetTopY = GetNearFlatFloorWorldY(scene);
        float scaleY = ground.Root.lossyScale.y;

        if (Mathf.Approximately(scaleY, 0f))
        {
            throw new InvalidOperationException(
                "Ground has a zero Y scale."
            );
        }

        // Keep the existing horizontal, one-unit-thick Ground collider. Only
        // its local Y offset changes, so its top matches the Near floor line.
        float offsetY =
            (targetTopY - ground.Root.position.y) / scaleY -
            ground.Collider.size.y * 0.5f;

        ground.Collider.offset = new Vector2(
            ground.Collider.offset.x,
            offsetY
        );
        ground.Renderer.enabled = false;

        EditorUtility.SetDirty(ground.Collider);
        EditorUtility.SetDirty(ground.Renderer);
    }

    private static float GetNearFlatFloorWorldY(Scene scene)
    {
        Transform background = FindSingleRoot(scene, "Background");
        Transform near = FindDirectChild(background, "Near");
        SpriteRenderer[] renderers =
            near.GetComponentsInChildren<SpriteRenderer>(true);

        if (renderers.Length != 2 || renderers[0].sprite == null ||
            renderers[1].sprite == null)
        {
            throw new InvalidOperationException(
                $"{scene.name}: Background/Near must contain two valid tiles."
            );
        }

        SpriteRenderer tile = renderers[0];
        Sprite sprite = tile.sprite;
        float textureRowAtSpriteTop = sprite.texture.height -
            (sprite.rect.y + sprite.rect.height);
        float rowWithinSprite = NearFlatFloorTextureRowFromTop -
            textureRowAtSpriteTop;

        if (rowWithinSprite < 0f || rowWithinSprite > sprite.rect.height)
        {
            throw new InvalidOperationException(
                $"{scene.name}: Near flat floor row is outside Sprite.rect."
            );
        }

        // Row 300 is the first full-width horizontal floor row in both Near
        // textures. Renderer.bounds automatically includes the actual
        // center-pivoted, alpha-trimmed Sprite rect and world scale.
        float normalizedRow = rowWithinSprite / sprite.rect.height;
        return tile.bounds.max.y - tile.bounds.size.y * normalizedRow;
    }

    private static void ValidateSceneOrThrow(Scene scene, SceneSpec spec)
    {
        List<string> errors = new List<string>();
        CharacterRoot hero = TryFindCharacterRoot(scene, spec.Hero, errors);
        CharacterRoot enemy = TryFindCharacterRoot(scene, spec.Enemy, errors);
        GroundRoot ground = TryFindGround(scene, errors);

        ValidateCharacter(hero, errors);
        ValidateCharacter(enemy, errors);
        ValidateGround(scene, ground, errors);
        ValidateStageReferences(scene, hero, enemy, errors);
        ValidateWorldHealthBarTarget(scene, hero, errors);
        ValidateWorldHealthBarTarget(scene, enemy, errors);
        ValidateNoMissingComponents(scene, spec.Name, errors);
        ValidateBrokenObjectReferences(scene, spec.Name, errors);

        ThrowIfErrors($"{spec.Name}: character physics validation failed", errors);
    }

    private static void ValidateBaselinePrerequisitesOrThrow(
        Scene scene,
        SceneSpec spec)
    {
        List<string> errors = new List<string>();
        CharacterRoot hero = TryFindCharacterRoot(scene, spec.Hero, errors);
        CharacterRoot enemy = TryFindCharacterRoot(scene, spec.Enemy, errors);
        GroundRoot ground = TryFindGround(scene, errors);

        ValidateCharacter(hero, errors);
        ValidateCharacter(enemy, errors);
        ValidateGround(scene, ground, errors);

        try
        {
            if (hero != null && enemy != null)
            {
                FindStageMarkers(scene, hero, enemy);
            }
        }
        catch (InvalidOperationException exception)
        {
            errors.Add(exception.Message);
        }

        ThrowIfErrors(
            $"{spec.Name}: stage baseline prerequisites failed",
            errors
        );
    }

    private static void ValidateStageBaselineOrThrow(
        Scene scene,
        SceneSpec spec)
    {
        List<string> errors = new List<string>();
        CharacterRoot hero = TryFindCharacterRoot(scene, spec.Hero, errors);
        CharacterRoot enemy = TryFindCharacterRoot(scene, spec.Enemy, errors);
        GroundRoot ground = TryFindGround(scene, errors);

        ValidateCharacter(hero, errors);
        ValidateCharacter(enemy, errors);
        ValidateGround(scene, ground, errors);

        if (hero != null && enemy != null && ground != null)
        {
            try
            {
                StageMarkers markers = FindStageMarkers(scene, hero, enemy);
                float groundTopY = ground.Collider.bounds.max.y;
                float heroGroundedRootY =
                    CalculateGroundedRootY(hero, groundTopY);
                float enemyGroundedRootY =
                    CalculateGroundedRootY(enemy, groundTopY);

                ValidateMarkerY(
                    markers.HeroEntranceStart,
                    heroGroundedRootY,
                    "HeroEntranceStart",
                    errors
                );
                ValidateMarkerY(
                    markers.HeroBattlePosition,
                    heroGroundedRootY,
                    "HeroBattlePosition",
                    errors
                );
                ValidateMarkerY(
                    markers.HeroExitPoint,
                    heroGroundedRootY,
                    "HeroExitPoint",
                    errors
                );
                ValidateMarkerY(
                    markers.EnemyEntranceStart,
                    enemyGroundedRootY,
                    "EnemyEntranceStart",
                    errors
                );
                ValidateMarkerY(
                    markers.EnemyBattlePosition,
                    enemyGroundedRootY,
                    "EnemyBattlePosition",
                    errors
                );

                ValidateColliderBottomAtRootY(
                    hero,
                    heroGroundedRootY,
                    groundTopY,
                    errors
                );
                ValidateColliderBottomAtRootY(
                    enemy,
                    enemyGroundedRootY,
                    groundTopY,
                    errors
                );
            }
            catch (InvalidOperationException exception)
            {
                errors.Add(exception.Message);
            }
        }

        ValidateStageReferences(scene, hero, enemy, errors);
        ValidateNoMissingComponents(scene, spec.Name, errors);
        ValidateBrokenObjectReferences(scene, spec.Name, errors);
        ThrowIfErrors($"{spec.Name}: stage baseline validation failed", errors);
    }

    private static void ValidateMarkerY(
        Transform marker,
        float expectedY,
        string markerName,
        List<string> errors)
    {
        if (Mathf.Abs(marker.position.y - expectedY) > AlignmentTolerance)
        {
            errors.Add(
                $"{markerName}: Y is not aligned to its actor grounded root."
            );
        }
    }

    private static void ValidateColliderBottomAtRootY(
        CharacterRoot character,
        float rootY,
        float groundTopY,
        List<string> errors)
    {
        Vector3 bottomLocal = new Vector3(
            character.Collider.offset.x,
            character.Collider.offset.y -
                character.Collider.size.y * 0.5f,
            0f
        );
        float currentBottomY =
            character.Root.TransformPoint(bottomLocal).y;
        float bottomRelativeY = currentBottomY - character.Root.position.y;
        float expectedBottomY = rootY + bottomRelativeY;

        if (Mathf.Abs(expectedBottomY - groundTopY) > AlignmentTolerance)
        {
            errors.Add(
                $"{character.Spec.RootName}: collider bottom does not meet " +
                "the Ground top at its marker baseline."
            );
        }
    }

    private static void ValidateCharacter(
        CharacterRoot character,
        List<string> errors)
    {
        if (character == null)
        {
            return;
        }

        Sprite expectedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            character.Spec.SpritePath
        );

        if (character.Renderer.sprite != expectedSprite)
        {
            errors.Add(
                $"{character.Spec.RootName}: expected character art is not " +
                "applied; run Character Art setup before physics setup."
            );
        }

        if (character.Root.localScale != character.Spec.ExpectedScale)
        {
            errors.Add($"{character.Spec.RootName}: root scale was changed.");
        }

        if (character.Root.GetComponent<Rigidbody2D>() == null ||
            character.Root.GetComponent<Health>() == null ||
            character.Root.GetComponent(character.Spec.ControllerType) == null)
        {
            errors.Add(
                $"{character.Spec.RootName}: required root component is missing."
            );
        }

        foreach (string childName in character.Spec.RequiredChildNames)
        {
            if (FindDescendant(character.Root, childName) == null)
            {
                errors.Add(
                    $"{character.Spec.RootName}: required child '{childName}' " +
                    "is missing."
                );
            }
        }

        if (!Approximately(
                character.Collider.size,
                character.Spec.ColliderSize) ||
            !Approximately(
                character.Collider.offset,
                GetExpectedColliderOffset(character)))
        {
            errors.Add(
                $"{character.Spec.RootName}: BoxCollider2D does not match " +
                "the serialized character-art specification."
            );
        }

        float colliderBottomY = character.Root.TransformPoint(
            new Vector3(
                character.Collider.offset.x,
                character.Collider.offset.y -
                    character.Collider.size.y * 0.5f,
                0f
            )
        ).y;
        float feetY = character.Root.TransformPoint(
            new Vector3(0f, GetOpaqueFeetInset(character), 0f)
        ).y;

        if (Mathf.Abs(colliderBottomY - feetY) > AlignmentTolerance)
        {
            errors.Add(
                $"{character.Spec.RootName}: collider bottom does not align " +
                "with the opaque sprite-feet baseline."
            );
        }
    }

    private static void ValidateGround(
        Scene scene,
        GroundRoot ground,
        List<string> errors)
    {
        if (ground == null)
        {
            return;
        }

        if (ground.Renderer.enabled)
        {
            errors.Add("Ground: legacy SpriteRenderer must be disabled.");
        }

        if (!ground.Collider.enabled || ground.Collider.isTrigger)
        {
            errors.Add("Ground: BoxCollider2D must remain an enabled solid plane.");
        }

        try
        {
            float nearFloorY = GetNearFlatFloorWorldY(scene);
            float colliderTopY = ground.Collider.bounds.max.y;

            if (Mathf.Abs(colliderTopY - nearFloorY) > AlignmentTolerance)
            {
                errors.Add(
                    "Ground: collider top does not align with the Near flat " +
                    "floor line."
                );
            }
        }
        catch (InvalidOperationException exception)
        {
            errors.Add(exception.Message);
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

        SerializedObject stageData = new SerializedObject(stage);
        ValidateReference(stageData, "hero", hero != null ? hero.Root : null, errors);
        ValidateReference(stageData, "enemy", enemy != null ? enemy.Root : null, errors);

        SerializedProperty parallaxProperty =
            stageData.FindProperty("infiniteParallaxBackground");
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

    private static void ValidateWorldHealthBarTarget(
        Scene scene,
        CharacterRoot character,
        List<string> errors)
    {
        if (character == null)
        {
            return;
        }

        int targetCount = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (WorldHealthBarUI healthBar in
                     root.GetComponentsInChildren<WorldHealthBarUI>(true))
            {
                SerializedObject data = new SerializedObject(healthBar);
                SerializedProperty target = data.FindProperty("trackedTarget");

                if (target != null &&
                    target.objectReferenceValue == character.Root)
                {
                    targetCount++;
                }
            }
        }

        if (targetCount != 1)
        {
            errors.Add(
                $"{character.Spec.RootName}: expected one WorldHealthBar " +
                "trackedTarget reference to the existing root."
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
                $"StageBattleSequenceController.{propertyName} reference is " +
                "invalid."
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

    private static CharacterRoot FindCharacterRoot(
        Scene scene,
        CharacterSpec spec)
    {
        List<string> errors = new List<string>();
        CharacterRoot character = TryFindCharacterRoot(scene, spec, errors);
        ThrowIfErrors($"{scene.name}: character root lookup failed", errors);
        return character;
    }

    private static CharacterRoot TryFindCharacterRoot(
        Scene scene,
        CharacterSpec spec,
        List<string> errors)
    {
        GameObject root;

        try
        {
            root = FindSingleRoot(scene, spec.RootName).gameObject;
        }
        catch (InvalidOperationException exception)
        {
            errors.Add(exception.Message);
            return null;
        }

        SpriteRenderer[] renderers = root.GetComponents<SpriteRenderer>();

        if (renderers.Length != 1)
        {
            errors.Add(
                $"{spec.RootName}: expected one root SpriteRenderer, found " +
                $"{renderers.Length}."
            );
            return null;
        }

        BoxCollider2D collider = root.GetComponent<BoxCollider2D>();

        if (collider == null)
        {
            errors.Add($"{spec.RootName}: root BoxCollider2D is missing.");
            return null;
        }

        return new CharacterRoot(root.transform, renderers[0], collider, spec);
    }

    private static GroundRoot FindGround(Scene scene)
    {
        List<string> errors = new List<string>();
        GroundRoot ground = TryFindGround(scene, errors);
        ThrowIfErrors($"{scene.name}: Ground lookup failed", errors);
        return ground;
    }

    private static GroundRoot TryFindGround(
        Scene scene,
        List<string> errors)
    {
        GameObject root;

        try
        {
            root = FindSingleRoot(scene, "Ground").gameObject;
        }
        catch (InvalidOperationException exception)
        {
            errors.Add(exception.Message);
            return null;
        }

        SpriteRenderer[] renderers = root.GetComponents<SpriteRenderer>();
        BoxCollider2D[] colliders = root.GetComponents<BoxCollider2D>();

        if (renderers.Length != 1 || colliders.Length != 1)
        {
            errors.Add(
                "Ground: expected exactly one root SpriteRenderer and " +
                "one root BoxCollider2D."
            );
            return null;
        }

        return new GroundRoot(root.transform, renderers[0], colliders[0]);
    }

    private static StageMarkers FindStageMarkers(
        Scene scene,
        CharacterRoot hero,
        CharacterRoot enemy)
    {
        StageBattleSequenceController stage =
            FindSingleComponent<StageBattleSequenceController>(scene);

        if (stage == null)
        {
            throw new InvalidOperationException(
                $"{scene.name}: StageBattleSequenceController is missing."
            );
        }

        SerializedObject data = new SerializedObject(stage);
        ValidateStageActorReference(data, "hero", hero.Root, scene.name);
        ValidateStageActorReference(data, "enemy", enemy.Root, scene.name);

        return new StageMarkers(
            GetTransformReference(data, "heroEntranceStart", scene.name),
            GetTransformReference(data, "heroBattlePosition", scene.name),
            GetTransformReference(data, "heroExitPoint", scene.name),
            GetTransformReference(data, "enemyEntranceStart", scene.name),
            GetTransformReference(data, "enemyBattlePosition", scene.name)
        );
    }

    private static void ValidateStageActorReference(
        SerializedObject data,
        string propertyName,
        Transform expected,
        string sceneName)
    {
        SerializedProperty property = data.FindProperty(propertyName);

        if (property == null || property.objectReferenceValue != expected)
        {
            throw new InvalidOperationException(
                $"{sceneName}: StageBattleSequenceController.{propertyName} " +
                "reference is invalid."
            );
        }
    }

    private static Transform GetTransformReference(
        SerializedObject data,
        string propertyName,
        string sceneName)
    {
        SerializedProperty property = data.FindProperty(propertyName);
        Transform transform = property != null
            ? property.objectReferenceValue as Transform
            : null;

        if (transform == null)
        {
            throw new InvalidOperationException(
                $"{sceneName}: StageBattleSequenceController.{propertyName} " +
                "reference is missing."
            );
        }

        return transform;
    }

    private static Transform FindSingleRoot(Scene scene, string rootName)
    {
        Transform result = null;

        foreach (GameObject candidate in scene.GetRootGameObjects())
        {
            if (candidate.name != rootName)
            {
                continue;
            }

            if (result != null)
            {
                throw new InvalidOperationException(
                    $"{scene.name}: duplicate root '{rootName}'."
                );
            }

            result = candidate.transform;
        }

        if (result == null)
        {
            throw new InvalidOperationException(
                $"{scene.name}: root '{rootName}' is missing."
            );
        }

        return result;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        Transform result = null;

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);

            if (child.name != name)
            {
                continue;
            }

            if (result != null)
            {
                throw new InvalidOperationException(
                    $"{parent.name}: duplicate child '{name}'."
                );
            }

            result = child;
        }

        if (result == null)
        {
            throw new InvalidOperationException(
                $"{parent.name}: child '{name}' is missing."
            );
        }

        return result;
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

    private static Transform FindDescendant(Transform root, string name)
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

    private static void RequireExpectedSprite(CharacterRoot character)
    {
        Sprite expectedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            character.Spec.SpritePath
        );

        if (expectedSprite == null || character.Renderer.sprite != expectedSprite)
        {
            throw new InvalidOperationException(
                $"{character.Spec.RootName}: expected art must already be " +
                "applied. This physics tool does not change SpriteRenderer " +
                "mappings."
            );
        }
    }

    private static Vector2 GetExpectedColliderOffset(
        CharacterRoot character)
    {
        return new Vector2(
            character.Spec.ColliderCenterX,
            GetOpaqueFeetInset(character) +
                character.Spec.ColliderSize.y * 0.5f
        );
    }

    private static float GetOpaqueFeetInset(CharacterRoot character)
    {
        Sprite sprite = character.Renderer.sprite;

        if (sprite == null || sprite.pixelsPerUnit <= 0f)
        {
            throw new InvalidOperationException(
                $"{character.Spec.RootName}: SpriteRenderer sprite is missing."
            );
        }

        float localFeetPixels = character.Spec.OpaqueFeetTextureY -
            sprite.rect.y;

        if (localFeetPixels < -AlignmentTolerance ||
            localFeetPixels > sprite.rect.height + AlignmentTolerance)
        {
            throw new InvalidOperationException(
                $"{character.Spec.RootName}: opaque feet are outside the " +
                "current Sprite.rect."
            );
        }

        return Mathf.Max(0f, localFeetPixels) / sprite.pixelsPerUnit;
    }

    private static bool Approximately(Vector2 left, Vector2 right)
    {
        return Mathf.Abs(left.x - right.x) <= AlignmentTolerance &&
               Mathf.Abs(left.y - right.y) <= AlignmentTolerance;
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
            string spritePath,
            Type controllerType,
            Vector3 expectedScale,
            Vector2 colliderSize,
            float colliderCenterX,
            float opaqueFeetTextureY,
            params string[] requiredChildNames)
        {
            RootName = rootName;
            SpritePath = spritePath;
            ControllerType = controllerType;
            ExpectedScale = expectedScale;
            ColliderSize = colliderSize;
            ColliderCenterX = colliderCenterX;
            OpaqueFeetTextureY = opaqueFeetTextureY;
            RequiredChildNames = requiredChildNames;
        }

        public string RootName { get; }
        public string SpritePath { get; }
        public Type ControllerType { get; }
        public Vector3 ExpectedScale { get; }
        public Vector2 ColliderSize { get; }
        public float ColliderCenterX { get; }
        public float OpaqueFeetTextureY { get; }
        public string[] RequiredChildNames { get; }
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

    private sealed class GroundRoot
    {
        public GroundRoot(
            Transform root,
            SpriteRenderer renderer,
            BoxCollider2D collider)
        {
            Root = root;
            Renderer = renderer;
            Collider = collider;
        }

        public Transform Root { get; }
        public SpriteRenderer Renderer { get; }
        public BoxCollider2D Collider { get; }
    }

    private sealed class StageMarkers
    {
        public StageMarkers(
            Transform heroEntranceStart,
            Transform heroBattlePosition,
            Transform heroExitPoint,
            Transform enemyEntranceStart,
            Transform enemyBattlePosition)
        {
            HeroEntranceStart = heroEntranceStart;
            HeroBattlePosition = heroBattlePosition;
            HeroExitPoint = heroExitPoint;
            EnemyEntranceStart = enemyEntranceStart;
            EnemyBattlePosition = enemyBattlePosition;
        }

        public Transform HeroEntranceStart { get; }
        public Transform HeroBattlePosition { get; }
        public Transform HeroExitPoint { get; }
        public Transform EnemyEntranceStart { get; }
        public Transform EnemyBattlePosition { get; }
    }
}
