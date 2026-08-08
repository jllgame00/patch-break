using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds non-destructive, derived World Health Bar visuals from the supplied
/// state-composited x4 artwork. Runtime health presentation remains the
/// existing WorldHealthBarUI Image.fillAmount implementation.
/// </summary>
public static class PatchBreakHealthBarArtSetup
{
    private const string MenuRoot = "Tools/PATCH BREAK/Health Bar Art/";
    private const string GeneratedRoot =
        "Assets/Generated/PatchBreak/HealthBar";
    private const string EmptyName = "EmptyChannel";
    private const string FillName = "Fill";
    private const string FrameName = "FrameOverlay";
    private const string DecorationName = "StaticDecoration";
    private const float PixelScale = 0.25f;
    private const float Tolerance = 0.01f;

    private static readonly ThemeSpec PlayerTheme = new(
        "Player",
        "Assets/Art/UI/HealthBar/Hero/hp_player_full_x4.png",
        new[]
        {
            "Assets/Art/UI/HealthBar/Hero/hp_player_full_x4.png",
            "Assets/Art/UI/HealthBar/Hero/hp_player_mid_x4.png",
            "Assets/Art/UI/HealthBar/Hero/hp_player_low_x4.png"
        },
        new RectInt(0, 0, 528, 56),
        new RectInt(16, 12, 496, 32),
        new RectInt(0, 56, 200, 36),
        GeneratedRoot + "/Player/hp_player_frame.png",
        GeneratedRoot + "/Player/hp_player_empty.png",
        GeneratedRoot + "/Player/hp_player_fill.png",
        GeneratedRoot + "/Player/hp_player_static_decoration.png"
    );

    private static readonly ThemeSpec GolemTheme = new(
        "Golem",
        "Assets/Art/UI/HealthBar/Golem/hp_golem_full_x4.png",
        new[]
        {
            "Assets/Art/UI/HealthBar/Golem/hp_golem_full_x4.png",
            "Assets/Art/UI/HealthBar/Golem/hp_golem_mid_x4.png"
        },
        new RectInt(0, 0, 944, 64),
        new RectInt(12, 12, 920, 40),
        new RectInt(),
        GeneratedRoot + "/Golem/hp_golem_frame.png",
        GeneratedRoot + "/Golem/hp_golem_empty.png",
        GeneratedRoot + "/Golem/hp_golem_fill.png",
        null
    );

    private static readonly ThemeSpec BossTheme = new(
        "Boss",
        "Assets/Art/UI/HealthBar/Debugger/hp_boss_full_x4.png",
        new[]
        {
            "Assets/Art/UI/HealthBar/Debugger/hp_boss_full_x4.png",
            "Assets/Art/UI/HealthBar/Debugger/hp_boss_mid_x4.png"
        },
        new RectInt(0, 0, 1072, 68),
        new RectInt(12, 16, 1048, 44),
        new RectInt(940, 76, 120, 52),
        GeneratedRoot + "/Boss/hp_boss_frame.png",
        GeneratedRoot + "/Boss/hp_boss_empty.png",
        GeneratedRoot + "/Boss/hp_boss_fill.png",
        GeneratedRoot + "/Boss/hp_boss_static_decoration.png"
    );

    private static readonly SceneSpec[] SceneSpecs =
    {
        new(
            "Assets/Scenes/Battle.unity",
            "Battle",
            new BarSpec("Hero", PlayerTheme, 1.75f),
            new BarSpec("Golem", GolemTheme, 3.25f)
        ),
        new(
            "Assets/Scenes/KnightBattle.unity",
            "KnightBattle",
            new BarSpec("Hero", PlayerTheme, 1.75f),
            new BarSpec("Knight", GolemTheme, 3.25f)
        ),
        new(
            "Assets/Scenes/DebuggerBattle.unity",
            "DebuggerBattle",
            new BarSpec("Hero", PlayerTheme, 1.75f),
            new BarSpec("Debugger", BossTheme, 5.65f)
        )
    };

    [MenuItem(MenuRoot + "Generate Derived Assets")]
    public static void GenerateDerivedAssetsMenu()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        GenerateDerivedAssetsOrThrow();
        ValidateGeneratedAssetsOrThrow();
        Debug.Log("PATCH_BREAK_HEALTH_BAR_DERIVED_ASSETS_COMPLETE");
    }

    [MenuItem(MenuRoot + "Setup Battle First")]
    public static void SetupBattleFirst()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SetupScenes(new[] { SceneSpecs[0] });
        Debug.Log("PATCH_BREAK_HEALTH_BAR_BATTLE_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Setup All Scenes")]
    public static void SetupAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SetupScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_HEALTH_BAR_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Validate All Scenes")]
    public static void ValidateAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        ValidateGeneratedAssetsOrThrow();
        ValidateScenes(SceneSpecs);
        Debug.Log("PATCH_BREAK_HEALTH_BAR_VALIDATION_COMPLETE");
    }

    // Intended for a Unity batchmode invocation after the normal Editor is
    // closed. It deliberately applies Battle only, matching the manual
    // Battle-first verification policy.
    public static void BatchSetupBattleFirst()
    {
        SetupScenes(new[] { SceneSpecs[0] });
        Debug.Log("PATCH_BREAK_HEALTH_BAR_BATCH_BATTLE_SETUP_COMPLETE");
    }

    private static bool PrepareEditorOperation()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "PATCH//BREAK health bar art setup cannot run in Play Mode."
            );
            return false;
        }

        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static void SetupScenes(IEnumerable<SceneSpec> specs)
    {
        List<SceneSpec> targets = new(specs);
        SceneSetup[] originalSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            // Validate all target scenes before writing any one of them.
            foreach (SceneSpec spec in targets)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    spec.ScenePath,
                    OpenSceneMode.Single
                );
                ValidatePrerequisitesOrThrow(scene, spec);
            }

            GenerateDerivedAssetsOrThrow();
            ValidateGeneratedAssetsOrThrow();

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

                Scene reopened = EditorSceneManager.OpenScene(
                    spec.ScenePath,
                    OpenSceneMode.Single
                );
                ValidateSceneOrThrow(reopened, spec);
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

    private static void SetupScene(Scene scene, SceneSpec spec)
    {
        ConfigureBar(
            FindBarForTargetOrThrow(scene, spec.Hero.TargetName),
            spec.Hero
        );
        ConfigureBar(
            FindBarForTargetOrThrow(scene, spec.Enemy.TargetName),
            spec.Enemy
        );
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ConfigureBar(WorldHealthBarUI controller, BarSpec spec)
    {
        RectTransform root = controller.transform as RectTransform;

        if (root == null)
        {
            throw new InvalidOperationException(
                $"{controller.name}: WorldHealthBar has no RectTransform."
            );
        }

        GeneratedThemeAssets assets = LoadGeneratedAssetsOrThrow(spec.Theme);
        root.sizeDelta = spec.Theme.DisplaySize;
        EditorUtility.SetDirty(root);

        Image empty = FindOrReuseEmptyChannel(root);
        Image fill = FindDirectImage(root, FillName);

        if (fill == null)
        {
            throw new InvalidOperationException(
                $"{controller.name}: existing Fill Image is missing."
            );
        }

        Image frame = GetOrCreateDirectImage(root, FrameName);
        ConfigureCenteredImage(
            empty,
            assets.Empty,
            spec.Theme.FillDisplaySize,
            Vector2.zero,
            Image.Type.Simple
        );
        ConfigureCenteredImage(
            fill,
            assets.Fill,
            spec.Theme.FillDisplaySize,
            Vector2.zero,
            Image.Type.Filled
        );
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;

        ConfigureCenteredImage(
            frame,
            assets.Frame,
            spec.Theme.DisplaySize,
            Vector2.zero,
            Image.Type.Simple
        );

        if (assets.StaticDecoration != null)
        {
            Image decoration = GetOrCreateDirectImage(root, DecorationName);
            ConfigureCenteredImage(
                decoration,
                assets.StaticDecoration,
                spec.Theme.DecorationDisplaySize,
                spec.Theme.DecorationDisplayPosition,
                Image.Type.Simple
            );
        }
        else
        {
            RemoveDirectChild(root, DecorationName);
        }

        empty.transform.SetSiblingIndex(0);
        fill.transform.SetSiblingIndex(1);
        frame.transform.SetSiblingIndex(2);

        Image staticDecoration =
            FindDirectImage(root, DecorationName);
        if (staticDecoration != null)
        {
            staticDecoration.transform.SetSiblingIndex(3);
        }

        SetFillReferenceAndWhiteColor(controller, fill);

        EditorUtility.SetDirty(empty);
        EditorUtility.SetDirty(fill);
        EditorUtility.SetDirty(frame);
        EditorUtility.SetDirty(controller);
    }

    private static void ConfigureCenteredImage(
        Image image,
        Sprite sprite,
        Vector2 size,
        Vector2 localPosition,
        Image.Type type)
    {
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = localPosition;
        rect.sizeDelta = size;

        image.sprite = sprite;
        image.type = type;
        image.color = Color.white;
        image.preserveAspect = false;
        image.raycastTarget = false;
        EditorUtility.SetDirty(rect);
        EditorUtility.SetDirty(image);
    }

    private static void SetFillReferenceAndWhiteColor(
        WorldHealthBarUI controller,
        Image fill)
    {
        SerializedObject data = new(controller);
        SerializedProperty fillImage = data.FindProperty("fillImage");
        SerializedProperty fillColor = data.FindProperty("fillColor");

        if (fillImage == null || fillColor == null)
        {
            throw new InvalidOperationException(
                $"{controller.name}: WorldHealthBarUI serialization changed."
            );
        }

        fillImage.objectReferenceValue = fill;
        fillColor.colorValue = Color.white;
        data.ApplyModifiedPropertiesWithoutUndo();
        fill.color = Color.white;
    }

    private static Image FindOrReuseEmptyChannel(RectTransform root)
    {
        Image empty = FindDirectImage(root, EmptyName);

        if (empty != null)
        {
            return empty;
        }

        Image legacyBackground = FindDirectImage(root, "Background");

        if (legacyBackground != null)
        {
            legacyBackground.gameObject.name = EmptyName;
            EditorUtility.SetDirty(legacyBackground.gameObject);
            return legacyBackground;
        }

        return GetOrCreateDirectImage(root, EmptyName);
    }

    private static Image GetOrCreateDirectImage(
        RectTransform parent,
        string name)
    {
        Image existing = FindDirectImage(parent, name);

        if (existing != null)
        {
            return existing;
        }

        GameObject child = new(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        child.layer = parent.gameObject.layer;
        child.transform.SetParent(parent, false);

        Image image = child.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static Image FindDirectImage(RectTransform parent, string name)
    {
        Image found = null;

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);

            if (child.name != name)
            {
                continue;
            }

            Image image = child.GetComponent<Image>();

            if (image == null)
            {
                throw new InvalidOperationException(
                    $"{GetHierarchyPath(child)}: expected Image is missing."
                );
            }

            if (found != null)
            {
                throw new InvalidOperationException(
                    $"{GetHierarchyPath(parent)}: duplicate '{name}' images."
                );
            }

            found = image;
        }

        return found;
    }

    private static void RemoveDirectChild(RectTransform parent, string name)
    {
        for (int index = parent.childCount - 1; index >= 0; index--)
        {
            Transform child = parent.GetChild(index);

            if (child.name == name)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void GenerateDerivedAssetsOrThrow()
    {
        EnsureFolder("Assets", "Generated");
        EnsureFolder("Assets/Generated", "PatchBreak");
        EnsureFolder("Assets/Generated/PatchBreak", "HealthBar");

        foreach (ThemeSpec theme in GetDistinctThemes())
        {
            GenerateThemeAssetsOrThrow(theme);
        }

        AssetDatabase.SaveAssets();
    }

    private static void GenerateThemeAssetsOrThrow(ThemeSpec theme)
    {
        EnsureFolder(GeneratedRoot, theme.Name);

        PixelImage full = ReadPngOrThrow(theme.FullSourcePath);
        List<PixelImage> stateImages = new();

        foreach (string sourcePath in theme.StateSourcePaths)
        {
            PixelImage state = ReadPngOrThrow(sourcePath);

            if (state.Width != full.Width || state.Height != full.Height)
            {
                throw new InvalidOperationException(
                    $"{theme.Name}: state art dimensions do not match."
                );
            }

            stateImages.Add(state);
        }

        ValidateThemeGeometryOrThrow(theme, full);
        PixelImage frame = ExtractFrame(full, theme.MainRect, theme.FillRect);
        PixelImage empty = ExtractEmptyChannel(
            stateImages.Count > 1 ? stateImages[1] : full,
            theme.FillRect
        );
        PixelImage fill = ExtractRect(full, theme.FillRect);
        PixelImage decoration = ExtractStaticDecoration(
            stateImages,
            theme.MainRect,
            theme.DecorationCandidateRect,
            out RectInt decorationBounds
        );

        WriteDerivedPng(theme.FramePath, frame);
        WriteDerivedPng(theme.EmptyPath, empty);
        WriteDerivedPng(theme.FillPath, fill);

        theme.SetDecorationGeometry(decorationBounds);

        if (decoration != null)
        {
            if (string.IsNullOrEmpty(theme.DecorationPath))
            {
                throw new InvalidOperationException(
                    $"{theme.Name}: unexpected static decoration pixels."
                );
            }

            WriteDerivedPng(theme.DecorationPath, decoration);
        }
        else if (!string.IsNullOrEmpty(theme.DecorationPath) &&
                 File.Exists(ToAbsolutePath(theme.DecorationPath)))
        {
            File.Delete(ToAbsolutePath(theme.DecorationPath));
            AssetDatabase.DeleteAsset(theme.DecorationPath);
        }
    }

    private static IEnumerable<ThemeSpec> GetDistinctThemes()
    {
        yield return PlayerTheme;
        yield return GolemTheme;
        yield return BossTheme;
    }

    private static void ValidateThemeGeometryOrThrow(
        ThemeSpec theme,
        PixelImage source)
    {
        if (!Contains(new RectInt(0, 0, source.Width, source.Height),
                      theme.MainRect) ||
            !Contains(theme.MainRect, theme.FillRect))
        {
            throw new InvalidOperationException(
                $"{theme.Name}: main/fill source rectangles are invalid."
            );
        }

        if (!string.IsNullOrEmpty(theme.DecorationPath) &&
            !Contains(new RectInt(0, 0, source.Width, source.Height),
                      theme.DecorationCandidateRect))
        {
            throw new InvalidOperationException(
                $"{theme.Name}: decoration source rectangle is invalid."
            );
        }
    }

    private static PixelImage ExtractFrame(
        PixelImage full,
        RectInt mainRect,
        RectInt fillRect)
    {
        PixelImage result = ExtractRect(full, mainRect);

        for (int y = fillRect.yMin; y < fillRect.yMax; y++)
        {
            for (int x = fillRect.xMin; x < fillRect.xMax; x++)
            {
                result.SetTopPixel(
                    x - mainRect.xMin,
                    y - mainRect.yMin,
                    new Color32(0, 0, 0, 0)
                );
            }
        }

        return result;
    }

    private static PixelImage ExtractEmptyChannel(
        PixelImage mid,
        RectInt fillRect)
    {
        int sampleX = fillRect.xMax - 1;
        PixelImage result = new(1, fillRect.height);

        for (int y = 0; y < fillRect.height; y++)
        {
            result.SetTopPixel(
                0,
                y,
                mid.GetTopPixel(sampleX, fillRect.yMin + y)
            );
        }

        return result;
    }

    private static PixelImage ExtractRect(PixelImage source, RectInt rect)
    {
        PixelImage result = new(rect.width, rect.height);

        for (int y = 0; y < rect.height; y++)
        {
            for (int x = 0; x < rect.width; x++)
            {
                result.SetTopPixel(
                    x,
                    y,
                    source.GetTopPixel(rect.xMin + x, rect.yMin + y)
                );
            }
        }

        return result;
    }

    private static PixelImage ExtractStaticDecoration(
        IReadOnlyList<PixelImage> states,
        RectInt mainRect,
        RectInt decorationCandidateRect,
        out RectInt bounds)
    {
        PixelImage reference = states[0];
        int minX = reference.Width;
        int minY = reference.Height;
        int maxX = -1;
        int maxY = -1;
        Color32[] common = new Color32[reference.Width * reference.Height];

        for (int y = 0; y < reference.Height; y++)
        {
            for (int x = 0; x < reference.Width; x++)
            {
                if (Contains(mainRect, x, y) ||
                    !Contains(decorationCandidateRect, x, y))
                {
                    continue;
                }

                Color32 color = reference.GetTopPixel(x, y);

                if (color.a == 0 || !IsCommonStatePixel(states, x, y, color))
                {
                    continue;
                }

                common[(reference.Height - 1 - y) * reference.Width + x] =
                    color;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < minX)
        {
            bounds = new RectInt();
            return null;
        }

        bounds = new RectInt(
            minX,
            minY,
            maxX - minX + 1,
            maxY - minY + 1
        );
        PixelImage result = new(bounds.width, bounds.height);

        for (int y = 0; y < bounds.height; y++)
        {
            for (int x = 0; x < bounds.width; x++)
            {
                int sourceX = bounds.xMin + x;
                int sourceY = bounds.yMin + y;
                Color32 color = common[
                    (reference.Height - 1 - sourceY) * reference.Width +
                    sourceX
                ];
                result.SetTopPixel(x, y, color);
            }
        }

        return result;
    }

    private static bool IsCommonStatePixel(
        IReadOnlyList<PixelImage> states,
        int x,
        int y,
        Color32 expected)
    {
        for (int index = 1; index < states.Count; index++)
        {
            if (!SameColor(states[index].GetTopPixel(x, y), expected))
            {
                return false;
            }
        }

        return true;
    }

    private static void WriteDerivedPng(string assetPath, PixelImage image)
    {
        string absolutePath = ToAbsolutePath(assetPath);
        string directory = Path.GetDirectoryName(absolutePath);

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Texture2D texture = new(
            image.Width,
            image.Height,
            TextureFormat.RGBA32,
            false,
            false
        );

        try
        {
            texture.SetPixels32(image.Pixels);
            texture.Apply(false, false);
            byte[] encoded = texture.EncodeToPNG();

            if (!File.Exists(absolutePath) ||
                !BytesEqual(File.ReadAllBytes(absolutePath), encoded))
            {
                File.WriteAllBytes(absolutePath, encoded);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(
            assetPath,
            ImportAssetOptions.ForceUpdate
        );
        ConfigureGeneratedImporter(assetPath);
    }

    private static bool BytesEqual(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
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

    private static void ConfigureGeneratedImporter(string assetPath)
    {
        TextureImporter importer =
            AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer == null)
        {
            throw new InvalidOperationException(
                $"{assetPath}: generated texture importer is missing."
            );
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = Vector4.zero;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression =
            TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        TextureImporterSettings settings = new();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static PixelImage ReadPngOrThrow(string assetPath)
    {
        string fullPath = ToAbsolutePath(assetPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "Health bar source PNG is missing.",
                fullPath
            );
        }

        Texture2D probe = new(2, 2, TextureFormat.RGBA32, false, false);

        try
        {
            if (!ImageConversion.LoadImage(
                    probe,
                    File.ReadAllBytes(fullPath),
                    false
                ))
            {
                throw new InvalidOperationException(
                    $"{assetPath}: PNG could not be decoded."
                );
            }

            return new PixelImage(
                probe.width,
                probe.height,
                probe.GetPixels32()
            );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(probe);
        }
    }

    private static void ValidateGeneratedAssetsOrThrow()
    {
        List<string> errors = new();

        foreach (ThemeSpec theme in GetDistinctThemes())
        {
            if (!string.IsNullOrEmpty(theme.DecorationPath))
            {
                EnsureDecorationGeometryOrThrow(theme);
            }

            ValidateGeneratedAsset(
                theme.FramePath,
                theme.MainRect.size,
                theme.Name + " frame",
                errors
            );
            ValidateGeneratedAsset(
                theme.EmptyPath,
                new Vector2Int(1, theme.FillRect.height),
                theme.Name + " empty channel",
                errors
            );
            ValidateGeneratedAsset(
                theme.FillPath,
                theme.FillRect.size,
                theme.Name + " fill",
                errors
            );

            if (!string.IsNullOrEmpty(theme.DecorationPath))
            {
                Sprite decoration = LoadSpriteOrNull(theme.DecorationPath);

                if (theme.DecorationBounds.width <= 0 ||
                    theme.DecorationBounds.height <= 0 ||
                    decoration == null ||
                    !Approximately(
                        decoration.rect.size,
                        new Vector2(
                            theme.DecorationBounds.width,
                            theme.DecorationBounds.height
                        )
                    ))
                {
                    errors.Add(
                        $"{theme.Name}: static decoration generation is invalid."
                    );
                }
                else
                {
                    ValidateGeneratedImporter(
                        theme.DecorationPath,
                        theme.Name + " static decoration",
                        errors
                    );
                }
            }
        }

        ThrowIfErrors("health bar generated asset validation failed", errors);
    }

    private static void ValidateGeneratedAsset(
        string assetPath,
        Vector2Int expectedSize,
        string label,
        List<string> errors)
    {
        Sprite sprite = LoadSpriteOrNull(assetPath);

        if (sprite == null ||
            !Approximately(
                sprite.rect.size,
                new Vector2(expectedSize.x, expectedSize.y)
            ))
        {
            errors.Add($"{label}: generated sprite dimensions are invalid.");
            return;
        }

        ValidateGeneratedImporter(assetPath, label, errors);
    }

    private static void ValidateGeneratedImporter(
        string assetPath,
        string label,
        List<string> errors)
    {
        TextureImporter importer =
            AssetImporter.GetAtPath(assetPath) as TextureImporter;
        TextureImporterSettings settings = new();

        if (importer != null)
        {
            importer.ReadTextureSettings(settings);
        }

        if (importer == null ||
            importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Single ||
            !Mathf.Approximately(importer.spritePixelsPerUnit, 100f) ||
            settings.spriteMeshType != SpriteMeshType.FullRect ||
            importer.filterMode != FilterMode.Point ||
            importer.textureCompression !=
                TextureImporterCompression.Uncompressed ||
            importer.mipmapEnabled ||
            importer.wrapMode != TextureWrapMode.Clamp ||
            !importer.alphaIsTransparency)
        {
            errors.Add($"{label}: generated import settings are invalid.");
        }
    }

    private static GeneratedThemeAssets LoadGeneratedAssetsOrThrow(
        ThemeSpec theme)
    {
        Sprite frame = LoadSpriteOrNull(theme.FramePath);
        Sprite empty = LoadSpriteOrNull(theme.EmptyPath);
        Sprite fill = LoadSpriteOrNull(theme.FillPath);
        Sprite decoration = string.IsNullOrEmpty(theme.DecorationPath)
            ? null
            : LoadSpriteOrNull(theme.DecorationPath);

        if (frame == null || empty == null || fill == null ||
            (!string.IsNullOrEmpty(theme.DecorationPath) &&
             decoration == null))
        {
            throw new InvalidOperationException(
                $"{theme.Name}: generated health bar sprites are missing."
            );
        }

        return new GeneratedThemeAssets(frame, empty, fill, decoration);
    }

    private static void EnsureDecorationGeometryOrThrow(ThemeSpec theme)
    {
        if (theme.DecorationBounds.width > 0 &&
            theme.DecorationBounds.height > 0)
        {
            return;
        }

        List<PixelImage> states = new();

        foreach (string sourcePath in theme.StateSourcePaths)
        {
            states.Add(ReadPngOrThrow(sourcePath));
        }

        PixelImage decoration = ExtractStaticDecoration(
            states,
            theme.MainRect,
            theme.DecorationCandidateRect,
            out RectInt bounds
        );

        if (decoration == null)
        {
            throw new InvalidOperationException(
                $"{theme.Name}: expected static decoration pixels are missing."
            );
        }

        theme.SetDecorationGeometry(bounds);
    }

    private static Sprite LoadSpriteOrNull(string assetPath)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static void ValidatePrerequisitesOrThrow(
        Scene scene,
        SceneSpec spec)
    {
        List<string> errors = new();
        ValidateBarPrerequisite(scene, spec.Hero, errors);
        ValidateBarPrerequisite(scene, spec.Enemy, errors);
        ThrowIfErrors($"{spec.Name}: health bar prerequisites failed", errors);
    }

    private static void ValidateBarPrerequisite(
        Scene scene,
        BarSpec spec,
        List<string> errors)
    {
        WorldHealthBarUI bar = TryFindBarForTarget(scene, spec.TargetName, errors);

        if (bar == null)
        {
            return;
        }

        SerializedObject data = new(bar);
        Transform target =
            data.FindProperty("trackedTarget")?.objectReferenceValue as Transform;
        Health health =
            data.FindProperty("targetHealth")?.objectReferenceValue as Health;
        Image fill =
            data.FindProperty("fillImage")?.objectReferenceValue as Image;
        RectTransform root = bar.transform as RectTransform;

        if (target == null || target.name != spec.TargetName ||
            health == null || health.transform != target ||
            fill == null || root == null ||
            FindDirectImage(root, FillName) != fill)
        {
            errors.Add(
                $"{spec.TargetName}: existing WorldHealthBar references are invalid."
            );
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

    private static void ValidateSceneOrThrow(Scene scene, SceneSpec spec)
    {
        List<string> errors = new();
        ValidateBar(scene, spec.Hero, errors);
        ValidateBar(scene, spec.Enemy, errors);
        ValidateNoMissingComponents(scene, spec.Name, errors);
        ValidateBrokenObjectReferences(scene, spec.Name, errors);
        ThrowIfErrors($"{spec.Name}: health bar art validation failed", errors);
    }

    private static void ValidateBar(
        Scene scene,
        BarSpec spec,
        List<string> errors)
    {
        WorldHealthBarUI controller =
            TryFindBarForTarget(scene, spec.TargetName, errors);

        if (controller == null)
        {
            return;
        }

        GeneratedThemeAssets assets;

        try
        {
            assets = LoadGeneratedAssetsOrThrow(spec.Theme);
        }
        catch (Exception exception)
        {
            errors.Add($"{spec.TargetName}: {exception.Message}");
            return;
        }

        RectTransform root = controller.transform as RectTransform;
        SerializedObject data = new(controller);
        Transform target =
            data.FindProperty("trackedTarget")?.objectReferenceValue as Transform;
        Image referencedFill =
            data.FindProperty("fillImage")?.objectReferenceValue as Image;
        SerializedProperty offsetProperty = data.FindProperty("worldOffset");
        SerializedProperty fillColorProperty = data.FindProperty("fillColor");
        Vector3 offset = offsetProperty != null
            ? offsetProperty.vector3Value
            : Vector3.zero;
        Color fillColor = fillColorProperty != null
            ? fillColorProperty.colorValue
            : Color.clear;

        Image empty = root != null
            ? FindDirectImage(root, EmptyName)
            : null;
        Image fill = root != null
            ? FindDirectImage(root, FillName)
            : null;
        Image frame = root != null
            ? FindDirectImage(root, FrameName)
            : null;
        Image decoration = root != null
            ? FindDirectImage(root, DecorationName)
            : null;

        if (root == null || target == null || target.name != spec.TargetName ||
            !Approximately(root.sizeDelta, spec.Theme.DisplaySize))
        {
            errors.Add($"{spec.TargetName}: root size or tracked target changed.");
        }

        if (!Mathf.Approximately(offset.y, spec.ExpectedOffsetY))
        {
            errors.Add($"{spec.TargetName}: worldOffset changed.");
        }

        if (fill == null || referencedFill != fill ||
            fill.sprite != assets.Fill || fill.type != Image.Type.Filled ||
            fill.fillMethod != Image.FillMethod.Horizontal ||
            fill.fillOrigin != (int)Image.OriginHorizontal.Left ||
            !Approximately(fill.rectTransform.sizeDelta,
                           spec.Theme.FillDisplaySize) ||
            !IsWhite(fill.color) || fill.raycastTarget ||
            !Mathf.Approximately(fill.fillAmount, 1f))
        {
            errors.Add($"{spec.TargetName}: continuous Fill configuration is invalid.");
        }

        if (empty == null || empty.sprite != assets.Empty ||
            empty.type != Image.Type.Simple ||
            !Approximately(empty.rectTransform.sizeDelta,
                           spec.Theme.FillDisplaySize) ||
            !IsWhite(empty.color) || empty.raycastTarget)
        {
            errors.Add($"{spec.TargetName}: EmptyChannel configuration is invalid.");
        }

        if (frame == null || frame.sprite != assets.Frame ||
            frame.type != Image.Type.Simple ||
            !Approximately(frame.rectTransform.sizeDelta,
                           spec.Theme.DisplaySize) ||
            !IsWhite(frame.color) || frame.raycastTarget)
        {
            errors.Add($"{spec.TargetName}: FrameOverlay configuration is invalid.");
        }

        if (assets.StaticDecoration == null)
        {
            if (decoration != null)
            {
                errors.Add($"{spec.TargetName}: unexpected StaticDecoration.");
            }
        }
        else if (decoration == null || decoration.sprite != assets.StaticDecoration ||
                 !Approximately(decoration.rectTransform.sizeDelta,
                                spec.Theme.DecorationDisplaySize) ||
                 !Approximately(decoration.rectTransform.anchoredPosition,
                                spec.Theme.DecorationDisplayPosition) ||
                 !IsWhite(decoration.color) || decoration.raycastTarget)
        {
            errors.Add($"{spec.TargetName}: StaticDecoration configuration is invalid.");
        }

        if (!IsWhite(fillColor) ||
            empty.transform.GetSiblingIndex() != 0 ||
            fill.transform.GetSiblingIndex() != 1 ||
            frame.transform.GetSiblingIndex() != 2 ||
            (decoration != null && decoration.transform.GetSiblingIndex() != 3))
        {
            errors.Add($"{spec.TargetName}: health bar render order is invalid.");
        }
    }

    private static WorldHealthBarUI FindBarForTargetOrThrow(
        Scene scene,
        string targetName)
    {
        List<string> errors = new();
        WorldHealthBarUI bar = TryFindBarForTarget(scene, targetName, errors);
        ThrowIfErrors($"{scene.name}: WorldHealthBar lookup failed", errors);
        return bar;
    }

    private static WorldHealthBarUI TryFindBarForTarget(
        Scene scene,
        string targetName,
        List<string> errors)
    {
        WorldHealthBarUI found = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (WorldHealthBarUI bar in
                     root.GetComponentsInChildren<WorldHealthBarUI>(true))
            {
                SerializedProperty targetProperty =
                    new SerializedObject(bar).FindProperty("trackedTarget");
                Transform target = targetProperty != null
                    ? targetProperty.objectReferenceValue as Transform
                    : null;

                if (target == null || target.name != targetName)
                {
                    continue;
                }

                if (found != null)
                {
                    errors.Add(
                        $"{targetName}: duplicate WorldHealthBar target."
                    );
                    return null;
                }

                found = bar;
            }
        }

        if (found == null)
        {
            errors.Add($"{targetName}: WorldHealthBar target is missing.");
        }

        return found;
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

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;

        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static bool Contains(RectInt outer, RectInt inner)
    {
        return inner.xMin >= outer.xMin &&
               inner.yMin >= outer.yMin &&
               inner.xMax <= outer.xMax &&
               inner.yMax <= outer.yMax;
    }

    private static bool Contains(RectInt rect, int x, int y)
    {
        return x >= rect.xMin && x < rect.xMax &&
               y >= rect.yMin && y < rect.yMax;
    }

    private static bool SameColor(Color32 left, Color32 right)
    {
        return left.r == right.r && left.g == right.g &&
               left.b == right.b && left.a == right.a;
    }

    private static bool IsWhite(Color color)
    {
        return Approximately(color.r, 1f) &&
               Approximately(color.g, 1f) &&
               Approximately(color.b, 1f) &&
               Approximately(color.a, 1f);
    }

    private static bool Approximately(Vector2 left, Vector2 right)
    {
        return Approximately(left.x, right.x) &&
               Approximately(left.y, right.y);
    }

    private static bool Approximately(float left, float right)
    {
        return Mathf.Abs(left - right) <= Tolerance;
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath);
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

    private sealed class ThemeSpec
    {
        public ThemeSpec(
            string name,
            string fullSourcePath,
            string[] stateSourcePaths,
            RectInt mainRect,
            RectInt fillRect,
            RectInt decorationCandidateRect,
            string framePath,
            string emptyPath,
            string fillPath,
            string decorationPath)
        {
            Name = name;
            FullSourcePath = fullSourcePath;
            StateSourcePaths = stateSourcePaths;
            MainRect = mainRect;
            FillRect = fillRect;
            DecorationCandidateRect = decorationCandidateRect;
            FramePath = framePath;
            EmptyPath = emptyPath;
            FillPath = fillPath;
            DecorationPath = decorationPath;
        }

        public string Name { get; }
        public string FullSourcePath { get; }
        public string[] StateSourcePaths { get; }
        public RectInt MainRect { get; }
        public RectInt FillRect { get; }
        public RectInt DecorationCandidateRect { get; }
        public string FramePath { get; }
        public string EmptyPath { get; }
        public string FillPath { get; }
        public string DecorationPath { get; }
        public RectInt DecorationBounds { get; private set; }

        public Vector2 DisplaySize => new(
            MainRect.width * PixelScale,
            MainRect.height * PixelScale
        );

        public Vector2 FillDisplaySize => new(
            FillRect.width * PixelScale,
            FillRect.height * PixelScale
        );

        public Vector2 DecorationDisplaySize => new(
            DecorationBounds.width * PixelScale,
            DecorationBounds.height * PixelScale
        );

        public Vector2 DecorationDisplayPosition => new(
            (DecorationBounds.center.x - MainRect.center.x) * PixelScale,
            (MainRect.center.y - DecorationBounds.center.y) * PixelScale
        );

        public void SetDecorationGeometry(RectInt bounds)
        {
            DecorationBounds = bounds;
        }
    }

    private sealed class SceneSpec
    {
        public SceneSpec(
            string scenePath,
            string name,
            BarSpec hero,
            BarSpec enemy)
        {
            ScenePath = scenePath;
            Name = name;
            Hero = hero;
            Enemy = enemy;
        }

        public string ScenePath { get; }
        public string Name { get; }
        public BarSpec Hero { get; }
        public BarSpec Enemy { get; }
    }

    private sealed class BarSpec
    {
        public BarSpec(string targetName, ThemeSpec theme, float expectedOffsetY)
        {
            TargetName = targetName;
            Theme = theme;
            ExpectedOffsetY = expectedOffsetY;
        }

        public string TargetName { get; }
        public ThemeSpec Theme { get; }
        public float ExpectedOffsetY { get; }
    }

    private readonly struct GeneratedThemeAssets
    {
        public GeneratedThemeAssets(
            Sprite frame,
            Sprite empty,
            Sprite fill,
            Sprite staticDecoration)
        {
            Frame = frame;
            Empty = empty;
            Fill = fill;
            StaticDecoration = staticDecoration;
        }

        public Sprite Frame { get; }
        public Sprite Empty { get; }
        public Sprite Fill { get; }
        public Sprite StaticDecoration { get; }
    }

    private sealed class PixelImage
    {
        public PixelImage(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new Color32[width * height];
        }

        public PixelImage(int width, int height, Color32[] pixels)
        {
            if (pixels == null || pixels.Length != width * height)
            {
                throw new ArgumentException("Pixel image dimensions are invalid.");
            }

            Width = width;
            Height = height;
            Pixels = pixels;
        }

        public int Width { get; }
        public int Height { get; }
        public Color32[] Pixels { get; }

        public Color32 GetTopPixel(int x, int topY)
        {
            return Pixels[(Height - 1 - topY) * Width + x];
        }

        public void SetTopPixel(int x, int topY, Color32 color)
        {
            Pixels[(Height - 1 - topY) * Width + x] = color;
        }
    }
}
