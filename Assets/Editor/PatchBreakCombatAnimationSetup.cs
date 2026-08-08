using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Imports the supplied combat sheets and wires them into the existing
/// characters, projectile prefab, and GuardIndicator objects. This is a
/// visual-only setup: it never changes gameplay timing, physics, points,
/// ranges, AI, or StageBattleSequenceController data.
/// </summary>
public static class PatchBreakCombatAnimationSetup
{
    private const string MenuRoot =
        "Tools/PATCH BREAK/Combat Animation/";
    private const float CharacterPixelsPerUnit = 32f;
    private const float IdleFramesPerSecond = 7f;
    private const float AttackFramesPerSecond = 12f;
    private const float ProjectileFramesPerSecond = 12f;
    private const float GuardFramesPerSecond = 10f;
    private const float Epsilon = 0.001f;

    private static readonly SheetSpec HeroIdle = new SheetSpec(
        "Hero Idle B",
        "Assets/Art/Characters/Hero/Animations/Idle/hero_b_combatidle_sheet.png",
        48,
        48,
        6,
        true,
        IdleFramesPerSecond
    );

    private static readonly SheetSpec HeroAttack = new SheetSpec(
        "Hero Slash",
        "Assets/Art/Characters/Hero/Animations/Attack/hero_slash_sheet.png",
        48,
        48,
        5,
        true,
        AttackFramesPerSecond
    );

    private static readonly SheetSpec GolemIdle = new SheetSpec(
        "Golem Idle B",
        "Assets/Art/Characters/Golem/Animations/Idle/golem_b_combatidle_sheet.png",
        64,
        64,
        6,
        true,
        IdleFramesPerSecond
    );

    private static readonly SheetSpec GolemAttack = new SheetSpec(
        "Golem Punch",
        "Assets/Art/Characters/Golem/Animations/Attack/golem_punch_sheet.png",
        64,
        64,
        5,
        true,
        AttackFramesPerSecond
    );

    private static readonly SheetSpec KnightIdle = new SheetSpec(
        "Knight Idle",
        "Assets/Art/Characters/Knight/Animations/Idle/knight_combatidle_sheet.png",
        64,
        64,
        6,
        true,
        IdleFramesPerSecond
    );

    private static readonly SheetSpec KnightAttack = new SheetSpec(
        "Knight Slash",
        "Assets/Art/Characters/Knight/Animations/Attack/knight_slash_sheet.png",
        64,
        64,
        5,
        true,
        AttackFramesPerSecond
    );

    private static readonly SheetSpec DebuggerIdle = new SheetSpec(
        "Debugger Idle",
        "Assets/Art/Characters/Debugger/Animations/Idle/debugger_combatidle_sheet.png",
        96,
        96,
        6,
        true,
        IdleFramesPerSecond
    );

    private static readonly SheetSpec DebuggerAttack = new SheetSpec(
        "Debugger Melee",
        "Assets/Art/Characters/Debugger/Animations/Attack/debugger_melee_sheet.png",
        96,
        96,
        5,
        true,
        AttackFramesPerSecond
    );

    private static readonly SheetSpec KnightProjectileSheet = new SheetSpec(
        "Knight Beam",
        "Assets/Art/VFX/Projectiles/Knight/knight_beam_sheet.png",
        56,
        24,
        4,
        false,
        ProjectileFramesPerSecond
    );

    private static readonly SheetSpec DebuggerProjectileSheet = new SheetSpec(
        "Debugger Beam",
        "Assets/Art/VFX/Projectiles/Debugger/debugger_beam_sheet.png",
        64,
        26,
        4,
        false,
        ProjectileFramesPerSecond
    );

    private static readonly SheetSpec KnightImpact = new SheetSpec(
        "Knight Projectile Impact",
        "Assets/Art/VFX/Projectiles/Knight/knight_impact_sheet.png",
        32,
        32,
        4,
        false,
        14f
    );

    private static readonly SheetSpec DebuggerImpact = new SheetSpec(
        "Debugger Projectile Impact",
        "Assets/Art/VFX/Projectiles/Debugger/debugger_impact_sheet.png",
        32,
        32,
        4,
        false,
        14f
    );

    private static readonly SheetSpec KnightGuardStart = new SheetSpec(
        "Knight Guard Start",
        "Assets/Art/VFX/Guard/Knight/knight_guard_start_sheet.png",
        64,
        64,
        3,
        false,
        GuardFramesPerSecond
    );

    private static readonly SheetSpec KnightGuardLoop = new SheetSpec(
        "Knight Guard Loop",
        "Assets/Art/VFX/Guard/Knight/knight_guard_loop_sheet.png",
        64,
        64,
        5,
        false,
        GuardFramesPerSecond
    );

    private static readonly SheetSpec KnightGuardBreak = new SheetSpec(
        "Knight Guard Break",
        "Assets/Art/VFX/Guard/Knight/knight_guard_break_sheet.png",
        64,
        64,
        3,
        false,
        GuardFramesPerSecond
    );

    private static readonly SheetSpec DebuggerGuardStart = new SheetSpec(
        "Debugger Guard Start",
        "Assets/Art/VFX/Guard/Debugger/debugger_guard_start_sheet.png",
        96,
        96,
        3,
        false,
        GuardFramesPerSecond
    );

    private static readonly SheetSpec DebuggerGuardLoop = new SheetSpec(
        "Debugger Guard Loop",
        "Assets/Art/VFX/Guard/Debugger/debugger_guard_loop_sheet.png",
        96,
        96,
        5,
        false,
        GuardFramesPerSecond
    );

    private static readonly SheetSpec DebuggerGuardBreak = new SheetSpec(
        "Debugger Guard Break",
        "Assets/Art/VFX/Guard/Debugger/debugger_guard_break_sheet.png",
        96,
        96,
        3,
        false,
        GuardFramesPerSecond
    );

    private static readonly SheetSpec HitNormal = new SheetSpec(
        "Hit Normal",
        "Assets/Art/VFX/Hit/hit_normal_sheet.png",
        24,
        24,
        3,
        false,
        14f
    );

    private static readonly SheetSpec HitStrong = new SheetSpec(
        "Hit Strong",
        "Assets/Art/VFX/Hit/hit_strong_sheet.png",
        32,
        32,
        4,
        false,
        14f
    );

    private static readonly SheetSpec[] AllSheets =
    {
        HeroIdle,
        HeroAttack,
        GolemIdle,
        GolemAttack,
        KnightIdle,
        KnightAttack,
        DebuggerIdle,
        DebuggerAttack,
        KnightProjectileSheet,
        DebuggerProjectileSheet,
        KnightImpact,
        DebuggerImpact,
        KnightGuardStart,
        KnightGuardLoop,
        KnightGuardBreak,
        DebuggerGuardStart,
        DebuggerGuardLoop,
        DebuggerGuardBreak,
        HitNormal,
        HitStrong
    };

    private static readonly SceneSpec[] SceneSpecs =
    {
        new SceneSpec(
            "Assets/Scenes/Battle.unity",
            "Battle",
            new ActorSpec("Hero", HeroIdle, HeroAttack),
            new ActorSpec("Golem", GolemIdle, GolemAttack),
            null
        ),
        new SceneSpec(
            "Assets/Scenes/KnightBattle.unity",
            "KnightBattle",
            new ActorSpec("Hero", HeroIdle, HeroAttack),
            new ActorSpec("Knight", KnightIdle, KnightAttack),
            new GuardSpec(
                KnightGuardStart,
                KnightGuardLoop,
                KnightGuardBreak
            )
        ),
        new SceneSpec(
            "Assets/Scenes/DebuggerBattle.unity",
            "DebuggerBattle",
            new ActorSpec("Hero", HeroIdle, HeroAttack),
            new ActorSpec("Debugger", DebuggerIdle, DebuggerAttack),
            new GuardSpec(
                DebuggerGuardStart,
                DebuggerGuardLoop,
                DebuggerGuardBreak
            )
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
                    "PATCH//BREAK combat animation asset missing: " +
                    sheet.Path
                );
                continue;
            }

            bool sizeMatches =
                texture.width == sheet.FrameWidth * sheet.FrameCount &&
                texture.height == sheet.FrameHeight;

            Debug.Log(
                "PATCH//BREAK ANIMATION ASSET\n" +
                $"name={sheet.Name}\n" +
                $"path={sheet.Path}\n" +
                $"texture={texture.width}x{texture.height}\n" +
                $"layout=horizontal {sheet.FrameCount} x " +
                $"{sheet.FrameWidth}x{sheet.FrameHeight}\n" +
                $"pivot={(sheet.IsCharacter ? "Bottom Center" : "Center")}\n" +
                $"fps={sheet.FramesPerSecond:F0}\n" +
                $"dimensionValid={sizeMatches}"
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
        Debug.Log("PATCH_BREAK_COMBAT_ANIMATION_IMPORT_SETUP_COMPLETE");
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
        Debug.Log("PATCH_BREAK_COMBAT_ANIMATION_BATTLE_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Setup All Scenes")]
    public static void SetupAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        ConfigureAllImportsOrThrow();
        SetupProjectilePrefabOrThrow();
        SetupScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_COMBAT_ANIMATION_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Validate All Scenes")]
    public static void ValidateAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        ValidateAllImportsOrThrow();
        ValidateProjectilePrefabOrThrow();
        ValidateScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_COMBAT_ANIMATION_VALIDATION_COMPLETE");
    }

    private static bool PrepareEditorOperation()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "PATCH//BREAK combat animation setup cannot run in Play Mode."
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

        if (importer == null)
        {
            throw new InvalidOperationException(
                $"{sheet.Name}: TextureImporter is missing at {sheet.Path}."
            );
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            sheet.Path
        );

        if (texture == null ||
            texture.width != sheet.FrameWidth * sheet.FrameCount ||
            texture.height != sheet.FrameHeight)
        {
            throw new InvalidOperationException(
                $"{sheet.Name}: expected a {sheet.FrameWidth}x" +
                $"{sheet.FrameHeight} horizontal sheet with " +
                $"{sheet.FrameCount} frames."
            );
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = CharacterPixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        SetSpriteMeshType(importer, SpriteMeshType.FullRect);
        importer.spritesheet = CreateSpriteSheetMetadata(sheet);
        importer.SaveAndReimport();
    }

    private static void SetSpriteMeshType(
        TextureImporter importer,
        SpriteMeshType meshType)
    {
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = meshType;
        importer.SetTextureSettings(settings);
    }

    private static SpriteMeshType GetSpriteMeshType(
        TextureImporter importer)
    {
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        return settings.spriteMeshType;
    }

    private static SpriteMetaData[] CreateSpriteSheetMetadata(SheetSpec sheet)
    {
        SpriteMetaData[] metadata = new SpriteMetaData[sheet.FrameCount];
        SpriteAlignment alignment = sheet.IsCharacter
            ? SpriteAlignment.BottomCenter
            : SpriteAlignment.Center;
        Vector2 pivot = sheet.IsCharacter
            ? new Vector2(0.5f, 0f)
            : new Vector2(0.5f, 0.5f);

        for (int index = 0; index < metadata.Length; index++)
        {
            metadata[index] = new SpriteMetaData
            {
                name = $"{sheet.Name.Replace(" ", "_")}_{index:00}",
                rect = new Rect(
                    index * sheet.FrameWidth,
                    0f,
                    sheet.FrameWidth,
                    sheet.FrameHeight
                ),
                alignment = (int)alignment,
                pivot = pivot,
                border = Vector4.zero
            };
        }

        return metadata;
    }

    private static void SetupScenes(IEnumerable<SceneSpec> specs)
    {
        List<SceneSpec> targets = new List<SceneSpec>(specs);
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
                ValidateScenePrerequisitesOrThrow(scene, spec);
            }

            foreach (SceneSpec spec in targets)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    spec.ScenePath,
                    OpenSceneMode.Single
                );
                ConfigureScene(scene, spec);

                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        $"{spec.Name}: scene could not be saved."
                    );
                }

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

    private static void ConfigureScene(Scene scene, SceneSpec spec)
    {
        ConfigureCharacter(FindRootOrThrow(scene, spec.Hero.RootName), spec.Hero);
        ConfigureCharacter(FindRootOrThrow(scene, spec.Enemy.RootName), spec.Enemy);

        if (spec.Guard != null)
        {
            ConfigureGuardIndicator(
                FindRootOrThrow(scene, spec.Enemy.RootName).transform,
                spec.Guard
            );
        }

        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ConfigureCharacter(GameObject root, ActorSpec spec)
    {
        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        CharacterPoseController pose =
            root.GetComponent<CharacterPoseController>();

        if (renderer == null || pose == null)
        {
            throw new InvalidOperationException(
                $"{spec.RootName}: root SpriteRenderer or CharacterPoseController is missing."
            );
        }

        SpriteSequencePlayer sequence = GetOrAddSingleComponent<SpriteSequencePlayer>(
            root,
            spec.RootName
        );
        ConfigureSequenceRenderer(sequence, renderer, spec.RootName);
        ConfigurePoseSequence(
            pose,
            sequence,
            LoadFramesOrThrow(spec.Idle),
            LoadFramesOrThrow(spec.Attack),
            spec.Idle.FramesPerSecond,
            spec.Attack.FramesPerSecond,
            spec.RootName
        );

        // Scene state remains the existing travel/base pose. The stage
        // controller explicitly requests Ready at WaitingForCompile.
        pose.SetBasePose();
        EditorUtility.SetDirty(sequence);
        EditorUtility.SetDirty(pose);
        EditorUtility.SetDirty(renderer);
    }

    private static void ConfigurePoseSequence(
        CharacterPoseController pose,
        SpriteSequencePlayer sequence,
        Sprite[] idleFrames,
        Sprite[] attackFrames,
        float idleFramesPerSecond,
        float attackFramesPerSecond,
        string context)
    {
        SerializedObject data = new SerializedObject(pose);
        SetReference(data, "sequencePlayer", sequence, context);
        SetSpriteArray(data, "readyIdleFrames", idleFrames, context);
        SetSpriteArray(data, "attackFrames", attackFrames, context);
        SetFloat(data, "readyIdleFramesPerSecond", idleFramesPerSecond, context);
        SetFloat(data, "attackFramesPerSecond", attackFramesPerSecond, context);
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureGuardIndicator(
        Transform actorRoot,
        GuardSpec guardSpec)
    {
        Transform indicator = FindUniqueDescendantOrThrow(
            actorRoot,
            "GuardIndicator"
        );
        SpriteRenderer legacyRenderer = indicator.GetComponent<SpriteRenderer>();

        if (legacyRenderer == null)
        {
            throw new InvalidOperationException(
                $"{actorRoot.name}/GuardIndicator: SpriteRenderer is missing."
            );
        }

        // Earlier setup revisions put the loop components on GuardIndicator
        // itself. Migrate only those visual-only components so the persistent
        // root can now host the Break lifetime without duplicate players.
        RemoveLegacyGuardComponents(indicator.gameObject);

        Transform visual = FindOrCreateDirectChild(
            indicator,
            "GuardVisual"
        );
        SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = visual.gameObject.AddComponent<SpriteRenderer>();
        }

        SpriteSequencePlayer sequence = GetOrAddSingleComponent<SpriteSequencePlayer>(
            visual.gameObject,
            actorRoot.name + "/GuardIndicator/GuardVisual"
        );
        GuardVisualLoop guard = GetOrAddSingleComponent<GuardVisualLoop>(
            visual.gameObject,
            actorRoot.name + "/GuardIndicator/GuardVisual"
        );
        Sprite[] startFrames = LoadFramesOrThrow(guardSpec.Start);
        Sprite[] loopFrames = LoadFramesOrThrow(guardSpec.Loop);
        Sprite[] breakFrames = LoadFramesOrThrow(guardSpec.Break);

        ConfigureSequenceRenderer(
            sequence,
            renderer,
            actorRoot.name + "/GuardIndicator/GuardVisual"
        );
        SerializedObject data = new SerializedObject(guard);
        SetReference(data, "targetRenderer", renderer, actorRoot.name + "/GuardIndicator/GuardVisual");
        SetReference(data, "sequencePlayer", sequence, actorRoot.name + "/GuardIndicator/GuardVisual");
        SetSpriteArray(data, "startFrames", startFrames, actorRoot.name + "/GuardIndicator/GuardVisual");
        SetSpriteArray(data, "loopFrames", loopFrames, actorRoot.name + "/GuardIndicator/GuardVisual");
        SetSpriteArray(data, "breakFrames", breakFrames, actorRoot.name + "/GuardIndicator/GuardVisual");
        SetFloat(data, "framesPerSecond", guardSpec.Loop.FramesPerSecond, actorRoot.name + "/GuardIndicator/GuardVisual");
        data.ApplyModifiedPropertiesWithoutUndo();

        // GuardIndicator keeps its combat-aligned local position and receives
        // local X changes from the existing controller. It remains active so
        // Break can finish after gameplay ends; only GuardVisual is hidden.
        indicator.gameObject.SetActive(true);
        indicator.localScale = Vector3.one;
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one;
        renderer.sortingLayerID = legacyRenderer.sortingLayerID;
        renderer.sortingOrder = legacyRenderer.sortingOrder;
        renderer.color = legacyRenderer.color;
        legacyRenderer.enabled = false;
        sequence.SetStatic(startFrames[0]);
        guard.HideImmediately();
        EditorUtility.SetDirty(indicator);
        EditorUtility.SetDirty(visual);
        EditorUtility.SetDirty(sequence);
        EditorUtility.SetDirty(guard);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(legacyRenderer);
    }

    private static void SetupProjectilePrefabOrThrow()
    {
        const string prefabPath =
            "Assets/Prefabs/Enemies/KnightProjectile.prefab";
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        if (prefabRoot == null)
        {
            throw new InvalidOperationException(
                "KnightProjectile prefab could not be loaded."
            );
        }

        try
        {
            KnightProjectile projectile =
                prefabRoot.GetComponent<KnightProjectile>();
            SpriteRenderer legacyRenderer =
                prefabRoot.GetComponent<SpriteRenderer>();

            if (projectile == null || legacyRenderer == null ||
                prefabRoot.GetComponent<Rigidbody2D>() == null ||
                prefabRoot.GetComponent<Collider2D>() == null)
            {
                throw new InvalidOperationException(
                    "KnightProjectile prefab gameplay setup is incomplete."
                );
            }

            Transform visualTransform = prefabRoot.transform.Find(
                "ProjectileVisual"
            );
            GameObject visual = visualTransform != null
                ? visualTransform.gameObject
                : new GameObject("ProjectileVisual");

            if (visualTransform == null)
            {
                visual.transform.SetParent(prefabRoot.transform, false);
            }

            SpriteRenderer visualRenderer =
                visual.GetComponent<SpriteRenderer>();
            if (visualRenderer == null)
            {
                visualRenderer = visual.AddComponent<SpriteRenderer>();
            }

            SpriteSequencePlayer sequence =
                GetOrAddSingleComponent<SpriteSequencePlayer>(
                    visual,
                    "KnightProjectile/ProjectileVisual"
                );
            ConfigureSequenceRenderer(
                sequence,
                visualRenderer,
                "KnightProjectile/ProjectileVisual"
            );

            Vector3 rootScale = prefabRoot.transform.localScale;
            if (Mathf.Abs(rootScale.x) < Epsilon ||
                Mathf.Abs(rootScale.y) < Epsilon)
            {
                throw new InvalidOperationException(
                    "KnightProjectile: root visual scale cannot be zero."
                );
            }

            // The root's old non-uniform scale belongs to its existing
            // temporary hitbox presentation. Keep it untouched and cancel it
            // only on this sprite-only child so the new art renders at PPU 32.
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(
                1f / rootScale.x,
                1f / rootScale.y,
                1f
            );
            visualRenderer.sortingLayerID = legacyRenderer.sortingLayerID;
            visualRenderer.sortingOrder = legacyRenderer.sortingOrder;
            visualRenderer.color = legacyRenderer.color;
            visualRenderer.enabled = true;
            legacyRenderer.enabled = false;

            Sprite[] knightFrames = LoadFramesOrThrow(KnightProjectileSheet);
            Sprite[] debuggerFrames = LoadFramesOrThrow(DebuggerProjectileSheet);
            sequence.SetStatic(knightFrames[0]);

            SerializedObject projectileData = new SerializedObject(projectile);
            SetReference(
                projectileData,
                "visualSequence",
                sequence,
                "KnightProjectile"
            );
            SetSpriteArray(
                projectileData,
                "knightBeamFrames",
                knightFrames,
                "KnightProjectile"
            );
            SetSpriteArray(
                projectileData,
                "debuggerBeamFrames",
                debuggerFrames,
                "KnightProjectile"
            );
            SetFloat(
                projectileData,
                "beamFramesPerSecond",
                ProjectileFramesPerSecond,
                "KnightProjectile"
            );
            projectileData.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
    }

    private static void ValidateAllImportsOrThrow()
    {
        List<string> errors = new List<string>();

        foreach (SheetSpec sheet in AllSheets)
        {
            TextureImporter importer = AssetImporter.GetAtPath(sheet.Path)
                as TextureImporter;
            Sprite[] frames = LoadFrames(sheet);

            if (importer == null ||
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Multiple ||
                !Mathf.Approximately(
                    importer.spritePixelsPerUnit,
                    CharacterPixelsPerUnit
                ) ||
                importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.mipmapEnabled ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                GetSpriteMeshType(importer) != SpriteMeshType.FullRect ||
                frames.Length != sheet.FrameCount)
            {
                errors.Add(sheet.Name + ": import or slice configuration is invalid.");
                continue;
            }

            for (int index = 0; index < frames.Length; index++)
            {
                Sprite frame = frames[index];
                Vector2 expectedPivot = sheet.IsCharacter
                    ? new Vector2(0.5f, 0f)
                    : new Vector2(0.5f, 0.5f);

                Vector2 normalizedPivot = frame != null
                    ? new Vector2(
                        frame.pivot.x / frame.rect.width,
                        frame.pivot.y / frame.rect.height
                    )
                    : Vector2.zero;

                if (frame == null ||
                    !Mathf.Approximately(frame.rect.width, sheet.FrameWidth) ||
                    !Mathf.Approximately(frame.rect.height, sheet.FrameHeight) ||
                    !Approximately(normalizedPivot, expectedPivot))
                {
                    errors.Add(sheet.Name + ": frame " + index + " is invalid.");
                }
            }
        }

        ThrowIfErrors("Combat animation import validation failed", errors);
    }

    private static void ValidateProjectilePrefabOrThrow()
    {
        const string prefabPath =
            "Assets/Prefabs/Enemies/KnightProjectile.prefab";
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        List<string> errors = new List<string>();

        try
        {
            KnightProjectile projectile =
                prefabRoot.GetComponent<KnightProjectile>();
            SpriteRenderer rootRenderer =
                prefabRoot.GetComponent<SpriteRenderer>();
            Transform visual = prefabRoot.transform.Find("ProjectileVisual");
            SpriteRenderer visualRenderer = visual != null
                ? visual.GetComponent<SpriteRenderer>()
                : null;
            SpriteSequencePlayer sequence = visual != null
                ? visual.GetComponent<SpriteSequencePlayer>()
                : null;

            if (projectile == null || rootRenderer == null ||
                prefabRoot.GetComponent<Rigidbody2D>() == null ||
                prefabRoot.GetComponent<Collider2D>() == null ||
                rootRenderer.enabled || visual == null ||
                visualRenderer == null || sequence == null ||
                sequence.TargetRenderer != visualRenderer ||
                projectile.VisualSequence != sequence ||
                projectile.KnightBeamFrames == null ||
                projectile.KnightBeamFrames.Length != KnightProjectileSheet.FrameCount ||
                projectile.DebuggerBeamFrames == null ||
                projectile.DebuggerBeamFrames.Length != DebuggerProjectileSheet.FrameCount ||
                !Mathf.Approximately(
                    projectile.BeamFramesPerSecond,
                    ProjectileFramesPerSecond
                ))
            {
                errors.Add("KnightProjectile: visual prefab configuration is invalid.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        ThrowIfErrors("Combat animation projectile validation failed", errors);
    }

    private static void ValidateScenePrerequisitesOrThrow(
        Scene scene,
        SceneSpec spec)
    {
        GameObject hero = FindRootOrThrow(scene, spec.Hero.RootName);
        GameObject enemy = FindRootOrThrow(scene, spec.Enemy.RootName);
        ValidateActorPrerequisites(hero, spec.Hero);
        ValidateActorPrerequisites(enemy, spec.Enemy);

        if (spec.Guard != null)
        {
            Transform indicator = FindUniqueDescendantOrThrow(
                enemy.transform,
                "GuardIndicator"
            );
            if (indicator.GetComponent<SpriteRenderer>() == null)
            {
                throw new InvalidOperationException(
                    $"{spec.Name}: GuardIndicator SpriteRenderer is missing."
                );
            }
        }
    }

    private static void ValidateActorPrerequisites(
        GameObject root,
        ActorSpec spec)
    {
        if (root.GetComponent<SpriteRenderer>() == null ||
            root.GetComponent<CharacterPoseController>() == null ||
            root.GetComponent<Rigidbody2D>() == null ||
            root.GetComponent<BoxCollider2D>() == null ||
            root.GetComponent<Health>() == null)
        {
            throw new InvalidOperationException(
                $"{spec.RootName}: existing character root prerequisites are missing."
            );
        }
    }

    private static void ValidateSceneOrThrow(Scene scene, SceneSpec spec)
    {
        List<string> errors = new List<string>();
        ValidateActor(scene, spec.Hero, errors);
        ValidateActor(scene, spec.Enemy, errors);

        if (spec.Guard != null)
        {
            GameObject enemy = FindRootOrAddError(
                scene,
                spec.Enemy.RootName,
                errors
            );
            if (enemy != null)
            {
                ValidateGuardIndicator(enemy.transform, spec.Guard, errors);
            }
        }

        ValidateNoMissingComponents(scene, errors);
        ThrowIfErrors(
            spec.Name + ": combat animation validation failed",
            errors
        );
    }

    private static void ValidateActor(
        Scene scene,
        ActorSpec spec,
        List<string> errors)
    {
        GameObject root = FindRootOrAddError(scene, spec.RootName, errors);
        if (root == null)
        {
            return;
        }

        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        CharacterPoseController pose =
            root.GetComponent<CharacterPoseController>();
        SpriteSequencePlayer[] players =
            root.GetComponents<SpriteSequencePlayer>();

        if (renderer == null || pose == null || players.Length != 1 ||
            players[0].TargetRenderer != renderer ||
            pose.SequencePlayer != players[0] ||
            !SpriteArraysMatch(pose.ReadyIdleFrames, LoadFramesOrThrow(spec.Idle)) ||
            !SpriteArraysMatch(pose.AttackFrames, LoadFramesOrThrow(spec.Attack)) ||
            !Mathf.Approximately(
                pose.ReadyIdleFramesPerSecond,
                spec.Idle.FramesPerSecond
            ) ||
            !Mathf.Approximately(
                pose.AttackFramesPerSecond,
                spec.Attack.FramesPerSecond
            ))
        {
            errors.Add(spec.RootName + ": sequence configuration is invalid.");
        }
    }

    private static void ValidateGuardIndicator(
        Transform actorRoot,
        GuardSpec guardSpec,
        List<string> errors)
    {
        Transform indicator;
        try
        {
            indicator = FindUniqueDescendantOrThrow(actorRoot, "GuardIndicator");
        }
        catch (InvalidOperationException exception)
        {
            errors.Add(exception.Message);
            return;
        }

        SpriteRenderer legacyRenderer = indicator.GetComponent<SpriteRenderer>();
        Transform visual = FindDirectChild(indicator, "GuardVisual");
        SpriteRenderer renderer = visual != null
            ? visual.GetComponent<SpriteRenderer>()
            : null;
        SpriteSequencePlayer[] players = visual != null
            ? visual.GetComponents<SpriteSequencePlayer>()
            : Array.Empty<SpriteSequencePlayer>();
        GuardVisualLoop[] loops = visual != null
            ? visual.GetComponents<GuardVisualLoop>()
            : Array.Empty<GuardVisualLoop>();

        if (!indicator.gameObject.activeSelf || legacyRenderer == null ||
            legacyRenderer.enabled || renderer == null || renderer.enabled ||
            indicator.GetComponents<SpriteSequencePlayer>().Length != 0 ||
            indicator.GetComponents<GuardVisualLoop>().Length != 0 ||
            players.Length != 1 || loops.Length != 1 ||
            players[0].TargetRenderer != renderer ||
            loops[0].SequencePlayer != players[0] ||
            loops[0].TargetRenderer != renderer ||
            !SpriteArraysMatch(loops[0].StartFrames, LoadFramesOrThrow(guardSpec.Start)) ||
            !SpriteArraysMatch(loops[0].LoopFrames, LoadFramesOrThrow(guardSpec.Loop)) ||
            !SpriteArraysMatch(loops[0].BreakFrames, LoadFramesOrThrow(guardSpec.Break)) ||
            !Mathf.Approximately(
                loops[0].FramesPerSecond,
                guardSpec.Loop.FramesPerSecond
            ) ||
            visual == null ||
            visual.localPosition.sqrMagnitude > Epsilon * Epsilon ||
            (visual.localScale - Vector3.one).sqrMagnitude > Epsilon * Epsilon ||
            (indicator.localScale - Vector3.one).sqrMagnitude > Epsilon * Epsilon)
        {
            errors.Add(actorRoot.name + "/GuardIndicator: visual sequence is invalid.");
        }
    }

    private static void ValidateNoMissingComponents(
        Scene scene,
        List<string> errors)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Component[] components = root.GetComponentsInChildren<Component>(true);
            foreach (Component component in components)
            {
                if (component == null)
                {
                    errors.Add(scene.name + ": Missing MonoBehaviour found.");
                    return;
                }
            }
        }
    }

    private static void ConfigureSequenceRenderer(
        SpriteSequencePlayer sequence,
        SpriteRenderer renderer,
        string context)
    {
        SerializedObject data = new SerializedObject(sequence);
        SetReference(data, "targetRenderer", renderer, context);
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T GetOrAddSingleComponent<T>(
        GameObject owner,
        string context)
        where T : Component
    {
        T[] existing = owner.GetComponents<T>();
        if (existing.Length > 1)
        {
            throw new InvalidOperationException(
                context + ": duplicate " + typeof(T).Name + "."
            );
        }

        return existing.Length == 1
            ? existing[0]
            : owner.AddComponent<T>();
    }

    private static void RemoveLegacyGuardComponents(GameObject indicator)
    {
        foreach (SpriteSequencePlayer player in
                 indicator.GetComponents<SpriteSequencePlayer>())
        {
            UnityEngine.Object.DestroyImmediate(player);
        }

        foreach (GuardVisualLoop loop in
                 indicator.GetComponents<GuardVisualLoop>())
        {
            UnityEngine.Object.DestroyImmediate(loop);
        }
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

        GameObject created = new GameObject(name);
        created.transform.SetParent(parent, false);
        return created.transform;
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
                    parent.name + ": duplicate direct child '" + name + "'."
                );
            }

            result = child;
        }

        return result;
    }

    private static Sprite[] LoadFramesOrThrow(SheetSpec sheet)
    {
        Sprite[] frames = LoadFrames(sheet);
        if (frames.Length != sheet.FrameCount)
        {
            throw new InvalidOperationException(
                $"{sheet.Name}: expected {sheet.FrameCount} sliced frames, " +
                $"found {frames.Length}. Run Setup Import Settings first."
            );
        }

        return frames;
    }

    private static Sprite[] LoadFrames(SheetSpec sheet)
    {
        UnityEngine.Object[] objects = AssetDatabase.LoadAllAssetsAtPath(
            sheet.Path
        );
        List<Sprite> frames = new List<Sprite>();

        foreach (UnityEngine.Object asset in objects)
        {
            Sprite sprite = asset as Sprite;
            if (sprite != null)
            {
                frames.Add(sprite);
            }
        }

        frames.Sort((left, right) => left.rect.x.CompareTo(right.rect.x));
        return frames.ToArray();
    }

    private static void SetReference(
        SerializedObject data,
        string propertyName,
        UnityEngine.Object value,
        string context)
    {
        SerializedProperty property = data.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                context + ": serialized field '" + propertyName + "' is missing."
            );
        }

        property.objectReferenceValue = value;
    }

    private static void SetSpriteArray(
        SerializedObject data,
        string propertyName,
        Sprite[] sprites,
        string context)
    {
        SerializedProperty property = data.FindProperty(propertyName);
        if (property == null || !property.isArray)
        {
            throw new InvalidOperationException(
                context + ": sprite array '" + propertyName + "' is missing."
            );
        }

        property.arraySize = sprites.Length;
        for (int index = 0; index < sprites.Length; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue =
                sprites[index];
        }
    }

    private static void SetFloat(
        SerializedObject data,
        string propertyName,
        float value,
        string context)
    {
        SerializedProperty property = data.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                context + ": float field '" + propertyName + "' is missing."
            );
        }

        property.floatValue = value;
    }

    private static GameObject FindRootOrThrow(Scene scene, string name)
    {
        GameObject result = FindRootOrAddError(scene, name, null);
        if (result == null)
        {
            throw new InvalidOperationException(
                scene.name + ": root '" + name + "' is missing or duplicated."
            );
        }

        return result;
    }

    private static GameObject FindRootOrAddError(
        Scene scene,
        string name,
        List<string> errors)
    {
        GameObject result = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != name)
            {
                continue;
            }

            if (result != null)
            {
                errors?.Add(scene.name + ": duplicate root '" + name + "'.");
                return null;
            }

            result = root;
        }

        if (result == null)
        {
            errors?.Add(scene.name + ": root '" + name + "' is missing.");
        }

        return result;
    }

    private static Transform FindUniqueDescendantOrThrow(
        Transform root,
        string name)
    {
        Transform result = null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root || child.name != name)
            {
                continue;
            }

            if (result != null)
            {
                throw new InvalidOperationException(
                    root.name + ": duplicate descendant '" + name + "'."
                );
            }

            result = child;
        }

        if (result == null)
        {
            throw new InvalidOperationException(
                root.name + ": descendant '" + name + "' is missing."
            );
        }

        return result;
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
        public readonly bool IsCharacter;
        public readonly float FramesPerSecond;

        public SheetSpec(
            string name,
            string path,
            int frameWidth,
            int frameHeight,
            int frameCount,
            bool isCharacter,
            float framesPerSecond)
        {
            Name = name;
            Path = path;
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
            FrameCount = frameCount;
            IsCharacter = isCharacter;
            FramesPerSecond = framesPerSecond;
        }
    }

    private sealed class ActorSpec
    {
        public readonly string RootName;
        public readonly SheetSpec Idle;
        public readonly SheetSpec Attack;

        public ActorSpec(string rootName, SheetSpec idle, SheetSpec attack)
        {
            RootName = rootName;
            Idle = idle;
            Attack = attack;
        }
    }

    private sealed class GuardSpec
    {
        public readonly SheetSpec Start;
        public readonly SheetSpec Loop;
        public readonly SheetSpec Break;

        public GuardSpec(
            SheetSpec start,
            SheetSpec loop,
            SheetSpec breakFrames)
        {
            Start = start;
            Loop = loop;
            Break = breakFrames;
        }
    }

    private sealed class SceneSpec
    {
        public readonly string ScenePath;
        public readonly string Name;
        public readonly ActorSpec Hero;
        public readonly ActorSpec Enemy;
        public readonly GuardSpec Guard;

        public SceneSpec(
            string scenePath,
            string name,
            ActorSpec hero,
            ActorSpec enemy,
            GuardSpec guard)
        {
            ScenePath = scenePath;
            Name = name;
            Hero = hero;
            Enemy = enemy;
            Guard = guard;
        }
    }
}
