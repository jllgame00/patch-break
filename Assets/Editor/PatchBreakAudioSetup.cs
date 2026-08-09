using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Installs PATCH//BREAK's small persistent 2D audio system. The tool creates
/// no gameplay objects and only attaches the two existing-UI observer hooks
/// needed for command typing and briefing appearance.
/// </summary>
public static class PatchBreakAudioSetup
{
    private const string MenuRoot = "Tools/PATCH BREAK/Audio/";
    private const string ManagerRootName = "PersistentAudioManager";
    private const string BgmSourceName = "BgmSource";
    private const string AmbienceSourceName = "AmbienceSource";
    private const string SfxSourceName = "SfxSource";
    private const string FootstepDirectory = "Assets/Audio/SFX/Footstep";
    private const float Epsilon = 0.001f;

    private static readonly ClipSpec BattleBgm = new(
        "Battle BGM",
        "Assets/Audio/BGM/battle_bgm.wav",
        ClipUsage.Music
    );

    private static readonly ClipSpec DebuggerBgm = new(
        "Debugger BGM",
        "Assets/Audio/BGM/debugger_bgm.wav",
        ClipUsage.Music
    );

    private static readonly ClipSpec CityAmbience = new(
        "City ambience",
        "Assets/Audio/Ambience/city_noise_loop.wav",
        ClipUsage.Ambience
    );

    private static readonly ClipSpec Typing01 = new(
        "Typing 01",
        "Assets/Audio/SFX/UI/typing1.wav",
        ClipUsage.ShortSfx
    );

    private static readonly ClipSpec Typing02 = new(
        "Typing 02",
        "Assets/Audio/SFX/UI/typing2.wav",
        ClipUsage.ShortSfx
    );

    private static readonly ClipSpec Typing03 = new(
        "Typing 03",
        "Assets/Audio/SFX/UI/typing3.wav",
        ClipUsage.ShortSfx
    );

    private static readonly ClipSpec Typing04 = new(
        "Typing 04",
        "Assets/Audio/SFX/UI/typing4.wav",
        ClipUsage.ShortSfx
    );

    private static readonly ClipSpec BriefingAppear = new(
        "Briefing appear",
        "Assets/Audio/SFX/UI/noti.wav",
        ClipUsage.ShortSfx
    );

    private static readonly ClipSpec SwordSwing = new(
        "Sword swing",
        "Assets/Audio/SFX/UI/sword swing.wav",
        ClipUsage.ShortSfx
    );

    private static readonly ClipSpec Projectile = new(
        "Projectile",
        "Assets/Audio/SFX/UI/projectile.wav",
        ClipUsage.ShortSfx
    );

    private static readonly ClipSpec Hit = new(
        "Hit",
        "Assets/Audio/SFX/UI/hit.wav",
        ClipUsage.ShortSfx
    );

    private static readonly ClipSpec[] CoreClips =
    {
        BattleBgm,
        DebuggerBgm,
        CityAmbience,
        Typing01,
        Typing02,
        Typing03,
        Typing04,
        BriefingAppear,
        SwordSwing,
        Projectile,
        Hit
    };

    // Character mapping is resolved from the inventory at setup time. A file
    // must begin with the exact character name followed by '_' or '-' (for
    // example, Debugger_sound) to be assigned. This deliberately leaves
    // GIANT_sound and any similarly non-character-named file unassigned.
    private static readonly FootstepProfile HeroFootstep = new(
        "Hero",
        0.45f,
        0.05f
    );

    private static readonly FootstepProfile GolemFootstep = new(
        "Golem",
        0.45f,
        0.08f
    );

    private static readonly FootstepProfile KnightFootstep = new(
        "Knight",
        0.35f,
        0.12f
    );

    private static readonly FootstepProfile DebuggerFootstep = new(
        "Debugger",
        0.40f,
        0.10f
    );

    private static readonly FootstepBinding[] FootstepBindings =
    {
        new("Battle", "Hero", HeroFootstep),
        new("Battle", "Golem", GolemFootstep),
        new("KnightBattle", "Hero", HeroFootstep),
        new("KnightBattle", "Knight", KnightFootstep),
        new("DebuggerBattle", "Hero", HeroFootstep),
        new("DebuggerBattle", "Debugger", DebuggerFootstep)
    };

    private static readonly SceneSpec[] SceneSpecs =
    {
        new("MainMenu", "Assets/Scenes/MainMenu.unity", false),
        new("Battle", "Assets/Scenes/Battle.unity", true),
        new("KnightBattle", "Assets/Scenes/KnightBattle.unity", true),
        new("DebuggerBattle", "Assets/Scenes/DebuggerBattle.unity", true),
        new("Ending", "Assets/Scenes/Ending.unity", false)
    };

    [MenuItem(MenuRoot + "Analyze Assets")]
    public static void AnalyzeAssets()
    {
        AssetDatabase.Refresh();

        foreach (ClipSpec spec in GetAllClipSpecs())
        {
            LogClipInventory(spec);
        }

        LogFootstepMappings();
    }

    [MenuItem(MenuRoot + "Analyze Footstep Assets")]
    public static void AnalyzeFootstepAssets()
    {
        AssetDatabase.Refresh();
        foreach (ClipSpec spec in GetFootstepClipSpecs())
        {
            LogClipInventory(spec);
        }

        LogFootstepMappings();
    }

    [MenuItem(MenuRoot + "Setup Import Settings")]
    public static void SetupImportSettings()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        ConfigureAllImportsOrThrow();
        Debug.Log("PATCH_BREAK_AUDIO_IMPORT_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Setup Battle First")]
    public static void SetupBattleFirst()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        ConfigureAllImportsOrThrow();
        SetupScenes(new[] { SceneSpecs[1] });
        Debug.Log("PATCH_BREAK_AUDIO_BATTLE_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Setup Audio System")]
    public static void SetupAudioSystem()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        ConfigureAllImportsOrThrow();
        SetupScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_AUDIO_SETUP_COMPLETE");
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
        Debug.Log("PATCH_BREAK_AUDIO_VALIDATION_COMPLETE");
    }

    private static bool PrepareEditorOperation()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "PATCH//BREAK Audio setup cannot run in Play Mode."
            );
            return false;
        }

        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static void ConfigureAllImportsOrThrow()
    {
        AssetDatabase.Refresh();
        foreach (ClipSpec spec in GetAllClipSpecs())
        {
            ConfigureImportOrThrow(spec);
        }

        AssetDatabase.SaveAssets();
    }

    private static void ConfigureImportOrThrow(ClipSpec spec)
    {
        AudioImporter importer = AssetImporter.GetAtPath(spec.Path)
            as AudioImporter;
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(spec.Path);
        if (importer == null || clip == null)
        {
            throw new InvalidOperationException(
                spec.Name + ": AudioImporter or AudioClip is missing at " +
                spec.Path + "."
            );
        }

        AudioImporterSampleSettings settings =
            importer.defaultSampleSettings;
        bool streaming = spec.Usage == ClipUsage.Music ||
                         spec.Usage == ClipUsage.Ambience;
        settings.loadType = streaming
            ? AudioClipLoadType.Streaming
            : AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = streaming ? 0.7f : 0.85f;
        settings.sampleRateSetting = spec.Usage == ClipUsage.Ambience
            ? AudioSampleRateSetting.OverrideSampleRate
            : AudioSampleRateSetting.PreserveSampleRate;
        if (spec.Usage == ClipUsage.Ambience)
        {
            settings.sampleRateOverride = 48000;
        }

        // Unity 6 stores this per platform/default sample setting, not on
        // AudioImporter itself. Preserve the prior policy exactly.
        settings.preloadAudioData = !streaming;

        importer.defaultSampleSettings = settings;
        importer.forceToMono = false;
        importer.loadInBackground = streaming;
        importer.SaveAndReimport();

        ValidateImportOrThrow(spec);
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
                    spec.Path,
                    OpenSceneMode.Single
                );
                ValidatePrerequisitesOrThrow(scene, spec);
            }

            foreach (SceneSpec spec in targets)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    spec.Path,
                    OpenSceneMode.Single
                );
                ConfigureScene(scene, spec);
                EditorSceneManager.SaveScene(scene);
            }

            foreach (SceneSpec spec in targets)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    spec.Path,
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

    private static void ValidateScenes(IEnumerable<SceneSpec> specs)
    {
        SceneSetup[] originalSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (SceneSpec spec in specs)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    spec.Path,
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
        PersistentAudioManager manager = FindOrCreateManager(scene);
        AudioSource bgm = ConfigureSource(
            manager.transform,
            BgmSourceName,
            loop: true
        );
        AudioSource ambience = ConfigureSource(
            manager.transform,
            AmbienceSourceName,
            loop: true
        );
        AudioSource sfx = ConfigureSource(
            manager.transform,
            SfxSourceName,
            loop: false
        );
        RemoveLegacyTypingSegmentSource(manager.transform);

        manager.Configure(
            bgm,
            ambience,
            sfx,
            LoadClipOrThrow(BattleBgm),
            LoadClipOrThrow(DebuggerBgm),
            LoadClipOrThrow(CityAmbience),
            new[]
            {
                LoadClipOrThrow(Typing01),
                LoadClipOrThrow(Typing02),
                LoadClipOrThrow(Typing03),
                LoadClipOrThrow(Typing04)
            },
            LoadClipOrThrow(BriefingAppear),
            LoadClipOrThrow(SwordSwing),
            LoadClipOrThrow(Projectile),
            LoadClipOrThrow(Hit)
        );

        if (spec.IsBattleScene)
        {
            ConfigureBattleHooks(scene, spec);
        }

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static PersistentAudioManager FindOrCreateManager(Scene scene)
    {
        GameObject managerRoot = FindRootByName(scene, ManagerRootName);
        if (managerRoot == null)
        {
            managerRoot = new GameObject(ManagerRootName);
            SceneManager.MoveGameObjectToScene(managerRoot, scene);
        }

        managerRoot.transform.SetParent(null, false);
        managerRoot.transform.localPosition = Vector3.zero;
        managerRoot.transform.localRotation = Quaternion.identity;
        managerRoot.transform.localScale = Vector3.one;
        return GetOrAddSingleComponent<PersistentAudioManager>(
            managerRoot,
            scene.name + "/" + ManagerRootName
        );
    }

    private static AudioSource ConfigureSource(
        Transform manager,
        string name,
        bool loop)
    {
        Transform sourceTransform = FindOrCreateDirectChild(manager, name);
        sourceTransform.localPosition = Vector3.zero;
        sourceTransform.localRotation = Quaternion.identity;
        sourceTransform.localScale = Vector3.one;

        AudioSource source = GetOrAddSingleComponent<AudioSource>(
            sourceTransform.gameObject,
            manager.name + "/" + name
        );
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.pitch = 1f;
        source.volume = 1f;
        EditorUtility.SetDirty(source);
        return source;
    }

    private static void RemoveLegacyTypingSegmentSource(Transform manager)
    {
        Transform legacy = FindDirectChild(manager, "TypingSegmentSource");
        if (legacy != null)
        {
            UnityEngine.Object.DestroyImmediate(legacy.gameObject);
        }
    }

    private static void ConfigureBattleHooks(Scene scene, SceneSpec spec)
    {
        GameObject console = FindUniqueInSceneByNameOrThrow(
            scene,
            "RuntimeConsolePanel"
        );
        TMP_InputField[] inputs = console.GetComponentsInChildren<
            TMP_InputField>(true);
        if (inputs.Length != 1)
        {
            throw new InvalidOperationException(
                spec.Name + "/RuntimeConsolePanel: exactly one TMP_InputField " +
                "is required."
            );
        }

        ConsoleTypingAudio typing =
            GetOrAddSingleComponent<ConsoleTypingAudio>(
                console,
                spec.Name + "/RuntimeConsolePanel"
            );
        typing.Configure(inputs[0]);

        BattleBriefingController briefing =
            FindSingleComponentInScene<BattleBriefingController>(scene);
        BriefingAppearAudio briefingAudio =
            GetOrAddSingleComponent<BriefingAppearAudio>(
                briefing.gameObject,
                spec.Name + "/BattleBriefingController"
            );
        briefingAudio.Configure(briefing);

        ConfigureFootstepHooks(scene, spec);

        EditorUtility.SetDirty(typing);
        EditorUtility.SetDirty(briefingAudio);
    }

    private static void ConfigureFootstepHooks(Scene scene, SceneSpec spec)
    {
        foreach (FootstepBinding binding in FootstepBindings)
        {
            if (binding.SceneName != spec.Name)
            {
                continue;
            }

            GameObject actor = FindRootByName(scene, binding.RootName);
            if (actor == null)
            {
                throw new InvalidOperationException(
                    spec.Name + "/" + binding.RootName +
                    ": character root is missing for footstep setup."
                );
            }

            CharacterPoseController pose = actor.GetComponent<
                CharacterPoseController>();
            if (pose == null)
            {
                throw new InvalidOperationException(
                    spec.Name + "/" + binding.RootName +
                    ": CharacterPoseController is missing for footstep setup."
                );
            }

            AudioClip clip = ResolveMappedFootstepClipOrNull(
                binding.Profile.CharacterName
            );
            if (clip == null)
            {
                Debug.LogWarning(
                    "PATCH//BREAK footstep unassigned: " +
                    binding.Profile.CharacterName +
                    " has no exact character-named AudioClip in " +
                    FootstepDirectory + ". No substitute clip was used."
                );
            }

            CharacterFootstepAudio footstep =
                GetOrAddSingleComponent<CharacterFootstepAudio>(
                    actor,
                    spec.Name + "/" + binding.RootName
                );
            footstep.Configure(
                pose,
                clip,
                binding.Profile.StepIntervalMultiplier,
                binding.Profile.MinimumInterval,
                binding.Profile.ClipLengthMinimumIntervalMultiplier,
                binding.Profile.InitialDelay,
                binding.Profile.Volume,
                binding.Profile.MinimumPitch,
                binding.Profile.MaximumPitch
            );
            EditorUtility.SetDirty(footstep);
        }
    }

    private static List<ClipSpec> GetAllClipSpecs()
    {
        List<ClipSpec> clips = new(CoreClips);
        clips.AddRange(GetFootstepClipSpecs());
        return clips;
    }

    private static List<ClipSpec> GetFootstepClipSpecs()
    {
        List<ClipSpec> clips = new();
        string[] guids = AssetDatabase.FindAssets(
            "t:AudioClip",
            new[] { FootstepDirectory }
        );

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            clips.Add(new ClipSpec(
                "Footstep " + Path.GetFileName(path),
                path,
                ClipUsage.ShortSfx
            ));
        }

        clips.Sort((left, right) => string.CompareOrdinal(
            left.Path,
            right.Path
        ));
        return clips;
    }

    private static AudioClip ResolveMappedFootstepClipOrNull(
        string characterName)
    {
        List<ClipSpec> matches = FindMappedFootstepClipSpecs(characterName);
        if (matches.Count == 0)
        {
            return null;
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                characterName + ": multiple exact character-named footstep " +
                "clips were found: " + string.Join(
                    ", ",
                    matches.ConvertAll(match => match.Path)
                )
            );
        }

        return LoadClipOrThrow(matches[0]);
    }

    private static List<ClipSpec> FindMappedFootstepClipSpecs(
        string characterName)
    {
        List<ClipSpec> matches = new();
        foreach (ClipSpec clip in GetFootstepClipSpecs())
        {
            string filename = Path.GetFileNameWithoutExtension(clip.Path);
            if (MatchesCharacterFilename(filename, characterName))
            {
                matches.Add(clip);
            }
        }

        return matches;
    }

    private static bool MatchesCharacterFilename(
        string filename,
        string characterName)
    {
        return filename.Equals(
                   characterName,
                   StringComparison.OrdinalIgnoreCase
               ) ||
               filename.StartsWith(
                   characterName + "_",
                   StringComparison.OrdinalIgnoreCase
               ) ||
               filename.StartsWith(
                   characterName + "-",
                   StringComparison.OrdinalIgnoreCase
               );
    }

    private static void LogClipInventory(ClipSpec spec)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(spec.Path);
        AudioImporter importer = AssetImporter.GetAtPath(spec.Path)
            as AudioImporter;
        if (clip == null || importer == null)
        {
            Debug.LogError("PATCH//BREAK audio asset missing: " + spec.Path);
            return;
        }

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        string mapping = spec.Path.StartsWith(
            FootstepDirectory + "/",
            StringComparison.Ordinal
        )
            ? GetFootstepMappingLabel(spec.Path)
            : "N/A";
        Debug.Log(
            "PATCH//BREAK AUDIO ASSET\n" +
            "name=" + spec.Name + "\n" +
            "filename=" + Path.GetFileName(spec.Path) + "\n" +
            "extension=" + Path.GetExtension(spec.Path) + "\n" +
            "path=" + spec.Path + "\n" +
            "mapping=" + mapping + "\n" +
            "duration=" + clip.length.ToString("F3") + "s\n" +
            "channels=" + clip.channels + "\n" +
            "sampleRate=" + clip.frequency + "Hz\n" +
            "usage=" + spec.Usage + "\n" +
            "loadType=" + settings.loadType + "\n" +
            "compression=" + settings.compressionFormat + "\n" +
            "quality=" + settings.quality.ToString("F2") + "\n" +
            "sampleRateSetting=" + settings.sampleRateSetting + "\n" +
            "sampleRateOverride=" + settings.sampleRateOverride + "\n" +
            "preloadAudioData=" + settings.preloadAudioData + "\n" +
            "forceToMono=" + importer.forceToMono + "\n" +
            "loadInBackground=" + importer.loadInBackground
        );
    }

    private static string GetFootstepMappingLabel(string path)
    {
        string filename = Path.GetFileNameWithoutExtension(path);
        List<string> mappings = new();
        foreach (FootstepProfile profile in GetFootstepProfiles())
        {
            if (MatchesCharacterFilename(filename, profile.CharacterName))
            {
                mappings.Add(profile.CharacterName);
            }
        }

        return mappings.Count == 0
            ? "UNMAPPED"
            : string.Join(", ", mappings);
    }

    private static IEnumerable<FootstepProfile> GetFootstepProfiles()
    {
        yield return HeroFootstep;
        yield return GolemFootstep;
        yield return KnightFootstep;
        yield return DebuggerFootstep;
    }

    private static void LogFootstepMappings()
    {
        foreach (FootstepProfile profile in GetFootstepProfiles())
        {
            List<ClipSpec> matches = FindMappedFootstepClipSpecs(
                profile.CharacterName
            );
            if (matches.Count == 0)
            {
                Debug.LogWarning(
                    "PATCH//BREAK FOOTSTEP MAPPING\ncharacter=" +
                    profile.CharacterName + "\nclip=UNASSIGNED\nreason=" +
                    "No exact character-named AudioClip was found."
                );
                continue;
            }

            Debug.Log(
                "PATCH//BREAK FOOTSTEP MAPPING\ncharacter=" +
                profile.CharacterName + "\nclip=" + string.Join(
                    ", ",
                    matches.ConvertAll(match => match.Path)
                )
            );
        }
    }

    private static void ValidateAllImportsOrThrow()
    {
        foreach (ClipSpec spec in GetAllClipSpecs())
        {
            ValidateImportOrThrow(spec);
        }
    }

    private static void ValidateImportOrThrow(ClipSpec spec)
    {
        AudioImporter importer = AssetImporter.GetAtPath(spec.Path)
            as AudioImporter;
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(spec.Path);
        if (importer == null || clip == null)
        {
            throw new InvalidOperationException(
                spec.Name + ": importer or clip is missing."
            );
        }

        AudioImporterSampleSettings settings =
            importer.defaultSampleSettings;
        bool streaming = spec.Usage == ClipUsage.Music ||
                         spec.Usage == ClipUsage.Ambience;
        AudioClipLoadType expectedLoadType = streaming
            ? AudioClipLoadType.Streaming
            : AudioClipLoadType.DecompressOnLoad;
        float expectedQuality = streaming ? 0.7f : 0.85f;
        AudioSampleRateSetting expectedSampleRate =
            spec.Usage == ClipUsage.Ambience
                ? AudioSampleRateSetting.OverrideSampleRate
                : AudioSampleRateSetting.PreserveSampleRate;

        if (settings.loadType != expectedLoadType ||
            settings.compressionFormat != AudioCompressionFormat.Vorbis ||
            !Mathf.Approximately(settings.quality, expectedQuality) ||
            settings.sampleRateSetting != expectedSampleRate ||
            (spec.Usage == ClipUsage.Ambience &&
             settings.sampleRateOverride != 48000) ||
            settings.preloadAudioData != !streaming ||
            importer.forceToMono ||
            importer.loadInBackground != streaming)
        {
            throw new InvalidOperationException(
                spec.Name + ": import settings are invalid."
            );
        }
    }

    private static void ValidatePrerequisitesOrThrow(
        Scene scene,
        SceneSpec spec)
    {
        if (!spec.IsBattleScene)
        {
            return;
        }

        GameObject console = FindUniqueInSceneByNameOrThrow(
            scene,
            "RuntimeConsolePanel"
        );
        if (console.GetComponentsInChildren<TMP_InputField>(true).Length != 1)
        {
            throw new InvalidOperationException(
                spec.Name + "/RuntimeConsolePanel: TMP_InputField prerequisite " +
                "is invalid."
            );
        }

        FindSingleComponentInScene<BattleBriefingController>(scene);
    }

    private static void ValidateSceneOrThrow(Scene scene, SceneSpec spec)
    {
        List<string> errors = new();
        PersistentAudioManager[] managers =
            FindComponentsInScene<PersistentAudioManager>(scene);
        if (managers.Length != 1 || managers[0].gameObject.name != ManagerRootName)
        {
            errors.Add("exactly one PersistentAudioManager root is required.");
        }
        else
        {
            ValidateManager(managers[0], errors);
        }

        if (spec.IsBattleScene)
        {
            ValidateBattleHooks(scene, errors);
            ValidateFootstepHooks(scene, spec, errors);
        }

        ValidateNoMissingComponents(scene, spec.Name, errors);
        ValidateBrokenObjectReferences(scene, spec.Name, errors);
        ThrowIfErrors(spec.Name + ": audio validation failed", errors);
    }

    private static void ValidateManager(
        PersistentAudioManager manager,
        List<string> errors)
    {
        if (manager.transform.parent != null ||
            (manager.transform.localScale - Vector3.one).sqrMagnitude >
                Epsilon * Epsilon ||
            manager.BgmSource == null ||
            manager.AmbienceSource == null ||
            manager.SfxSource == null ||
            manager.BgmSource.transform != FindDirectChild(
                manager.transform,
                BgmSourceName
            ) ||
            manager.AmbienceSource.transform != FindDirectChild(
                manager.transform,
                AmbienceSourceName
            ) ||
            manager.SfxSource.transform != FindDirectChild(
                manager.transform,
                SfxSourceName
            ) ||
            FindDirectChild(manager.transform, "TypingSegmentSource") != null ||
            manager.BattleBgm != LoadClipOrThrow(BattleBgm) ||
            manager.DebuggerBgm != LoadClipOrThrow(DebuggerBgm) ||
            manager.CityAmbience != LoadClipOrThrow(CityAmbience) ||
            manager.BriefingAppearClip != LoadClipOrThrow(BriefingAppear) ||
            manager.SwordSwingClip != LoadClipOrThrow(SwordSwing) ||
            manager.ProjectileClip != LoadClipOrThrow(Projectile) ||
            manager.HitClip != LoadClipOrThrow(Hit) ||
            !ClipArraysMatch(manager.TypingClips, new[]
            {
                LoadClipOrThrow(Typing01),
                LoadClipOrThrow(Typing02),
                LoadClipOrThrow(Typing03),
                LoadClipOrThrow(Typing04)
            }) ||
            !IsConfiguredSource(manager.BgmSource, true) ||
            !IsConfiguredSource(manager.AmbienceSource, true) ||
            !IsConfiguredSource(manager.SfxSource, false))
        {
            errors.Add(ManagerRootName + ": configuration is invalid.");
        }
    }

    private static void ValidateBattleHooks(Scene scene, List<string> errors)
    {
        try
        {
            GameObject console = FindUniqueInSceneByNameOrThrow(
                scene,
                "RuntimeConsolePanel"
            );
            TMP_InputField[] inputs = console.GetComponentsInChildren<
                TMP_InputField>(true);
            ConsoleTypingAudio[] typing =
                console.GetComponents<ConsoleTypingAudio>();
            if (inputs.Length != 1 || typing.Length != 1 ||
                typing[0].InputField != inputs[0])
            {
                errors.Add(
                    "RuntimeConsolePanel: typing audio hook is invalid."
                );
            }

            BattleBriefingController briefing =
                FindSingleComponentInScene<BattleBriefingController>(scene);
            BriefingAppearAudio[] hooks = briefing.GetComponents<
                BriefingAppearAudio>();
            if (hooks.Length != 1 || hooks[0].BriefingController != briefing)
            {
                errors.Add(
                    "BattleBriefingController: appear audio hook is invalid."
                );
            }
        }
        catch (InvalidOperationException exception)
        {
            errors.Add(exception.Message);
        }
    }

    private static void ValidateFootstepHooks(
        Scene scene,
        SceneSpec spec,
        List<string> errors)
    {
        foreach (FootstepBinding binding in FootstepBindings)
        {
            if (binding.SceneName != spec.Name)
            {
                continue;
            }

            try
            {
                GameObject actor = FindRootByName(scene, binding.RootName);
                CharacterPoseController pose = actor != null
                    ? actor.GetComponent<CharacterPoseController>()
                    : null;
                CharacterFootstepAudio[] hooks = actor != null
                    ? actor.GetComponents<CharacterFootstepAudio>()
                    : Array.Empty<CharacterFootstepAudio>();
                AudioClip expectedClip = ResolveMappedFootstepClipOrNull(
                    binding.Profile.CharacterName
                );

                if (actor == null || pose == null || hooks.Length != 1 ||
                    !IsConfiguredFootstepHook(
                        hooks.Length == 1 ? hooks[0] : null,
                        pose,
                        expectedClip,
                        binding.Profile
                    ))
                {
                    errors.Add(
                        binding.RootName +
                        ": footstep presentation configuration is invalid."
                    );
                }
            }
            catch (InvalidOperationException exception)
            {
                errors.Add(exception.Message);
            }
        }
    }

    private static bool IsConfiguredFootstepHook(
        CharacterFootstepAudio hook,
        CharacterPoseController pose,
        AudioClip expectedClip,
        FootstepProfile profile)
    {
        return hook != null &&
               hook.PoseController == pose &&
               hook.FootstepClip == expectedClip &&
               Mathf.Approximately(
                   hook.StepIntervalMultiplier,
                   profile.StepIntervalMultiplier
               ) &&
               Mathf.Approximately(
                   hook.MinimumInterval,
                   profile.MinimumInterval
               ) &&
               Mathf.Approximately(
                   hook.ClipLengthMinimumIntervalMultiplier,
                   profile.ClipLengthMinimumIntervalMultiplier
               ) &&
               Mathf.Approximately(hook.InitialDelay, profile.InitialDelay) &&
               Mathf.Approximately(hook.Volume, profile.Volume) &&
               Mathf.Approximately(hook.MinimumPitch, profile.MinimumPitch) &&
               Mathf.Approximately(hook.MaximumPitch, profile.MaximumPitch);
    }

    private static bool IsConfiguredSource(AudioSource source, bool loop)
    {
        return source != null &&
               !source.playOnAwake &&
               source.loop == loop &&
               Mathf.Approximately(source.spatialBlend, 0f) &&
               Mathf.Approximately(source.pitch, 1f);
    }

    private static T FindSingleComponentInScene<T>(Scene scene)
        where T : Component
    {
        T[] components = FindComponentsInScene<T>(scene);
        if (components.Length != 1)
        {
            throw new InvalidOperationException(
                scene.name + ": exactly one " + typeof(T).Name +
                " is required."
            );
        }

        return components[0];
    }

    private static T[] FindComponentsInScene<T>(Scene scene)
        where T : Component
    {
        List<T> found = new();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            found.AddRange(root.GetComponentsInChildren<T>(true));
        }

        return found.ToArray();
    }

    private static GameObject FindUniqueInSceneByNameOrThrow(
        Scene scene,
        string name)
    {
        GameObject found = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in
                     root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name != name)
                {
                    continue;
                }

                if (found != null)
                {
                    throw new InvalidOperationException(
                        scene.name + ": duplicate object '" + name + "'."
                    );
                }

                found = transform.gameObject;
            }
        }

        if (found == null)
        {
            throw new InvalidOperationException(
                scene.name + ": object '" + name + "' is missing."
            );
        }

        return found;
    }

    private static GameObject FindRootByName(Scene scene, string name)
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
                throw new InvalidOperationException(
                    scene.name + ": duplicate root '" + name + "'."
                );
            }

            found = root;
        }

        return found;
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

    private static AudioClip LoadClipOrThrow(ClipSpec spec)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(spec.Path);
        if (clip == null)
        {
            throw new InvalidOperationException(
                spec.Name + ": AudioClip is missing at " + spec.Path + "."
            );
        }

        return clip;
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

    private static bool ClipArraysMatch(AudioClip[] left, AudioClip[] right)
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

    private sealed class FootstepProfile
    {
        public const float StepIntervalMultiplierDefault = 1f;
        public const float MinimumIntervalDefault = 0.01f;
        public const float ClipLengthMinimumIntervalMultiplierDefault = 0.5f;
        public const float MinimumPitchDefault = 0.97f;
        public const float MaximumPitchDefault = 1.03f;

        public readonly string CharacterName;
        public readonly float Volume;
        public readonly float InitialDelay;
        public readonly float StepIntervalMultiplier;
        public readonly float MinimumInterval;
        public readonly float ClipLengthMinimumIntervalMultiplier;
        public readonly float MinimumPitch;
        public readonly float MaximumPitch;

        public FootstepProfile(
            string characterName,
            float volume,
            float initialDelay)
        {
            CharacterName = characterName;
            Volume = volume;
            InitialDelay = initialDelay;
            StepIntervalMultiplier = StepIntervalMultiplierDefault;
            MinimumInterval = MinimumIntervalDefault;
            ClipLengthMinimumIntervalMultiplier =
                ClipLengthMinimumIntervalMultiplierDefault;
            MinimumPitch = MinimumPitchDefault;
            MaximumPitch = MaximumPitchDefault;
        }
    }

    private sealed class FootstepBinding
    {
        public readonly string SceneName;
        public readonly string RootName;
        public readonly FootstepProfile Profile;

        public FootstepBinding(
            string sceneName,
            string rootName,
            FootstepProfile profile)
        {
            SceneName = sceneName;
            RootName = rootName;
            Profile = profile;
        }
    }

    private enum ClipUsage
    {
        Music,
        Ambience,
        ShortSfx
    }

    private sealed class ClipSpec
    {
        public readonly string Name;
        public readonly string Path;
        public readonly ClipUsage Usage;

        public ClipSpec(string name, string path, ClipUsage usage)
        {
            Name = name;
            Path = path;
            Usage = usage;
        }
    }

    private sealed class SceneSpec
    {
        public readonly string Name;
        public readonly string Path;
        public readonly bool IsBattleScene;

        public SceneSpec(string name, string path, bool isBattleScene)
        {
            Name = name;
            Path = path;
            IsBattleScene = isBattleScene;
        }
    }
}
