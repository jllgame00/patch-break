using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Imports and wires the user-supplied Knight projectile sheet without
/// changing the shared projectile's gameplay root, collider, or Debugger
/// presentation profile.
/// </summary>
public static class PatchBreakKnightProjectileVisualSetup
{
    private const string TargetAssetPath =
        "Assets/Art/VFX/Projectiles/Knight/knight_beam_sheet.png";

    private const string ProjectilePrefabPath =
        "Assets/Prefabs/Enemies/KnightProjectile.prefab";

    private const int FrameCount = 4;
    private const float PixelsPerUnit = 32f;
    private const string CrescentGeometryRevision =
        "PATCH_BREAK_KNIGHT_CRESCENT_FULL_RECT_V2";
    private const float GeometryEpsilon = 0.001f;
    private const string DebuggerBeamAssetPath =
        "Assets/Art/VFX/Projectiles/Debugger/debugger_beam_sheet.png";

    // Captured from the established 56x24 Knight sheet, root scale
    // (0.5, 0.12), and child scale (2, 8.333334): 1.75 world units wide.
    // New sheets are scaled uniformly in world space from this visual target.
    private const float TargetWorldWidth = 1.75f;
    private const byte VisibleAlphaThreshold = 8;
    private const float KnightLowerCoreVisualCenterYRatio =
        -0.45833334f;

    private static readonly string[] FrameNames =
    {
        "Knight_Beam_00",
        "Knight_Beam_01",
        "Knight_Beam_02",
        "Knight_Beam_03"
    };

    private readonly struct SheetInfo
    {
        public readonly int Width;
        public readonly int Height;
        public readonly bool HasTransparency;
        public readonly bool HasVisiblePixels;

        public SheetInfo(
            int width,
            int height,
            bool hasTransparency,
            bool hasVisiblePixels)
        {
            Width = width;
            Height = height;
            HasTransparency = hasTransparency;
            HasVisiblePixels = hasVisiblePixels;
        }

        public int FrameWidth => Width / FrameCount;
    }

    private readonly struct GameplaySnapshot
    {
        private readonly Vector3 rootScale;
        private readonly Vector2 colliderSize;
        private readonly Vector2 colliderOffset;
        private readonly float speed;
        private readonly float lifetime;
        private readonly int damage;
        private readonly Vector2 debuggerColliderWorldSize;
        private readonly float debuggerColliderTrailingWorldOffset;
        private readonly float debuggerColliderWorldYOffset;
        private readonly float debuggerTravelPastTargetDistance;
        private readonly Sprite[] debuggerFrames;
        private readonly Vector3 debuggerScale;

        public GameplaySnapshot(
            GameObject root,
            BoxCollider2D collider,
            KnightProjectile projectile)
        {
            rootScale = root.transform.localScale;
            colliderSize = collider.size;
            colliderOffset = collider.offset;

            SerializedObject data = new SerializedObject(projectile);
            speed = data.FindProperty("speed").floatValue;
            lifetime = data.FindProperty("lifetime").floatValue;
            damage = data.FindProperty("damage").intValue;
            debuggerColliderWorldSize = data.FindProperty(
                "debuggerColliderWorldSize"
            ).vector2Value;
            debuggerColliderTrailingWorldOffset = data.FindProperty(
                "debuggerColliderTrailingWorldOffset"
            ).floatValue;
            debuggerColliderWorldYOffset = data.FindProperty(
                "debuggerColliderWorldYOffset"
            ).floatValue;
            debuggerTravelPastTargetDistance = data.FindProperty(
                "debuggerTravelPastTargetDistance"
            ).floatValue;
            debuggerFrames = projectile.DebuggerBeamFrames.ToArray();
            debuggerScale = projectile.DebuggerVisualLocalScale;
        }

        public void VerifyUnchanged(
            GameObject root,
            BoxCollider2D collider,
            KnightProjectile projectile)
        {
            if (root.transform.localScale != rootScale ||
                collider.size != colliderSize ||
                collider.offset != colliderOffset)
            {
                throw new InvalidOperationException(
                    "Knight beam setup changed projectile root or collider " +
                    "gameplay data."
                );
            }

            SerializedObject data = new SerializedObject(projectile);
            if (!Mathf.Approximately(
                    data.FindProperty("speed").floatValue,
                    speed
                ) ||
                !Mathf.Approximately(
                    data.FindProperty("lifetime").floatValue,
                    lifetime
                ) ||
                data.FindProperty("damage").intValue != damage)
            {
                throw new InvalidOperationException(
                    "Knight beam setup changed movement or damage data."
                );
            }

            if (data.FindProperty("debuggerColliderWorldSize").vector2Value !=
                    debuggerColliderWorldSize ||
                !Mathf.Approximately(
                    data.FindProperty("debuggerColliderTrailingWorldOffset")
                        .floatValue,
                    debuggerColliderTrailingWorldOffset
                ) ||
                !Mathf.Approximately(
                    data.FindProperty("debuggerColliderWorldYOffset").floatValue,
                    debuggerColliderWorldYOffset
                ) ||
                !Mathf.Approximately(
                    data.FindProperty("debuggerTravelPastTargetDistance")
                        .floatValue,
                    debuggerTravelPastTargetDistance
                ))
            {
                throw new InvalidOperationException(
                    "Knight beam setup changed Debugger projectile gameplay " +
                    "profile data."
                );
            }

            if (!Approximately(
                    projectile.DebuggerVisualLocalScale,
                    debuggerScale
                ) ||
                !SameFrameArray(
                    projectile.DebuggerBeamFrames,
                    debuggerFrames
                ))
            {
                throw new InvalidOperationException(
                    "Knight beam setup changed Debugger projectile visuals."
                );
            }
        }
    }

    [MenuItem("Tools/PATCH BREAK/VFX/Setup Knight Beam")]
    public static void SetupKnightBeam()
    {
        SheetInfo sheet = InspectPngOrThrow();
        string guidBefore = AssetDatabase.AssetPathToGUID(TargetAssetPath);

        if (string.IsNullOrEmpty(guidBefore))
        {
            throw new InvalidOperationException(
                "Knight projectile asset GUID is missing at " +
                TargetAssetPath + "."
            );
        }

        TextureImporter importer = RequireImporter();
        ConfigureImporter(importer);
        importer = RequireImporter();

        SpriteRect[] existingRects = GetSpriteRects(importer);
        bool reuseCrescentSpriteIds = CanReuseCrescentSpriteIds(
            importer,
            existingRects,
            sheet
        );
        string[] previousIds = reuseCrescentSpriteIds
            ? FrameNames.Select(name => FindRect(existingRects, name)
                .spriteID.ToString()).ToArray()
            : Array.Empty<string>();

        // The former 56x24 beam used different per-sprite geometry.  Its IDs
        // are never carried into the 64x64 crescent sheet: fresh rects are
        // created once, the prefab is explicitly rewired below, and the
        // revision marker makes later setup runs stable.
        ApplySpriteRects(
            importer,
            BuildSliceRects(existingRects, sheet, reuseCrescentSpriteIds)
        );

        if (AssetDatabase.AssetPathToGUID(TargetAssetPath) != guidBefore)
        {
            throw new InvalidOperationException(
                "Knight projectile target GUID changed during setup."
            );
        }

        if (reuseCrescentSpriteIds)
        {
            SpriteRect[] importedRects = GetSpriteRects(RequireImporter());
            for (int index = 0; index < FrameCount; index++)
            {
                if (FindRect(importedRects, FrameNames[index]).spriteID
                        .ToString() != previousIds[index])
                {
                    throw new InvalidOperationException(
                        "Knight crescent SpriteID changed during an " +
                        "idempotent setup run."
                    );
                }
            }
        }

        Sprite[] knightFrames = ResolveKnightFramesOrThrow();
        ConfigureProjectilePrefabOrThrow(knightFrames);
        BeamValidation validation = ValidateOrThrow();
        MarkCrescentGeometryRevision(RequireImporter());
        AssetDatabase.WriteImportSettingsIfDirty(TargetAssetPath);

        Debug.Log(
            "[Knight Beam] SETUP PASS\n" +
            validation.ToMultilineString(
                reuseCrescentSpriteIds
                    ? "validated and rewired"
                    : "recreated and rewired"
            )
        );
    }

    [MenuItem("Tools/PATCH BREAK/VFX/Validate Knight Beam")]
    public static void ValidateKnightBeam()
    {
        BeamValidation validation = ValidateOrThrow();
        Debug.Log(
            "[Knight Beam] PASS\n" +
            validation.ToMultilineString("validated")
        );
    }

    private static SheetInfo InspectPngOrThrow()
    {
        string absolutePath = ToAbsolutePath(TargetAssetPath);
        if (!File.Exists(absolutePath))
        {
            throw new InvalidOperationException(
                "Knight projectile PNG is missing at " + TargetAssetPath + "."
            );
        }

        Texture2D texture = new Texture2D(
            2,
            2,
            TextureFormat.RGBA32,
            false,
            false
        );

        try
        {
            if (!ImageConversion.LoadImage(
                    texture,
                    File.ReadAllBytes(absolutePath),
                    false
                ))
            {
                throw new InvalidOperationException(
                    "Knight projectile PNG could not be decoded."
                );
            }

            if (texture.width <= 0 || texture.height <= 0 ||
                texture.width % FrameCount != 0)
            {
                throw new InvalidOperationException(
                    "Knight projectile PNG width must be positive and " +
                    "exactly divisible by four. Actual size: " +
                    texture.width + "x" + texture.height + "."
                );
            }

            bool hasTransparency = false;
            bool hasVisiblePixels = false;

            foreach (Color32 pixel in texture.GetPixels32())
            {
                hasTransparency |= pixel.a == 0;
                hasVisiblePixels |= pixel.a > VisibleAlphaThreshold;
            }

            if (!hasTransparency || !hasVisiblePixels)
            {
                throw new InvalidOperationException(
                    "Knight projectile PNG must contain visible art and " +
                    "transparent pixels."
                );
            }

            return new SheetInfo(
                texture.width,
                texture.height,
                hasTransparency,
                hasVisiblePixels
            );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static TextureImporter RequireImporter()
    {
        TextureImporter importer = AssetImporter.GetAtPath(TargetAssetPath)
            as TextureImporter;

        if (importer == null)
        {
            throw new InvalidOperationException(
                "Knight projectile TextureImporter is missing at " +
                TargetAssetPath + "."
            );
        }

        return importer;
    }

    private static SpriteMeshType GetSpriteMeshType(
        TextureImporter importer)
    {
        TextureImporterSettings textureSettings =
            new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        return textureSettings.spriteMeshType;
    }

    private static void ConfigureImporter(TextureImporter importer)
    {
        bool changed = false;
        changed |= SetIfDifferent(
            () => importer.textureType,
            value => importer.textureType = value,
            TextureImporterType.Sprite
        );
        changed |= SetIfDifferent(
            () => importer.spriteImportMode,
            value => importer.spriteImportMode = value,
            SpriteImportMode.Multiple
        );
        changed |= !Mathf.Approximately(
            importer.spritePixelsPerUnit,
            PixelsPerUnit
        );
        importer.spritePixelsPerUnit = PixelsPerUnit;
        changed |= SetIfDifferent(
            () => importer.filterMode,
            value => importer.filterMode = value,
            FilterMode.Point
        );
        changed |= SetIfDifferent(
            () => importer.textureCompression,
            value => importer.textureCompression = value,
            TextureImporterCompression.Uncompressed
        );
        changed |= SetIfDifferent(
            () => importer.mipmapEnabled,
            value => importer.mipmapEnabled = value,
            false
        );
        changed |= SetIfDifferent(
            () => importer.alphaIsTransparency,
            value => importer.alphaIsTransparency = value,
            true
        );
        changed |= SetIfDifferent(
            () => importer.wrapMode,
            value => importer.wrapMode = value,
            TextureWrapMode.Clamp
        );

        TextureImporterSettings textureSettings =
            new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        if (textureSettings.spriteMeshType != SpriteMeshType.FullRect)
        {
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
        else
        {
            AssetDatabase.ImportAsset(
                TargetAssetPath,
                ImportAssetOptions.ForceUpdate |
                ImportAssetOptions.ForceSynchronousImport
            );
        }
    }

    private static bool SetIfDifferent<T>(
        Func<T> get,
        Action<T> set,
        T expected)
    {
        if (EqualityComparer<T>.Default.Equals(get(), expected))
        {
            return false;
        }

        set(expected);
        return true;
    }

    private static SpriteRect[] GetSpriteRects(TextureImporter importer)
    {
        SpriteDataProviderFactories factories =
            new SpriteDataProviderFactories();
        factories.Init();

        ISpriteEditorDataProvider provider =
            factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        return provider.GetSpriteRects().ToArray();
    }

    private static bool CanReuseCrescentSpriteIds(
        TextureImporter importer,
        SpriteRect[] rects,
        SheetInfo sheet)
    {
        return importer.userData.Contains(CrescentGeometryRevision) &&
               rects.Length == FrameCount &&
               FrameNames.All(name =>
               {
                   SpriteRect rect = rects.SingleOrDefault(candidate =>
                       candidate.name == name
                   );
                   return rect != null &&
                          rect.rect == new Rect(
                              Array.IndexOf(FrameNames, name) *
                                  sheet.FrameWidth,
                              0f,
                              sheet.FrameWidth,
                              sheet.Height
                          ) &&
                          rect.alignment == SpriteAlignment.Center &&
                          rect.pivot == new Vector2(0.5f, 0.5f);
               });
    }

    private static void MarkCrescentGeometryRevision(TextureImporter importer)
    {
        if (!importer.userData.Contains(CrescentGeometryRevision))
        {
            importer.userData = string.IsNullOrEmpty(importer.userData)
                ? CrescentGeometryRevision
                : importer.userData + "\n" + CrescentGeometryRevision;
        }
    }

    private static SpriteRect[] BuildSliceRects(
        SpriteRect[] existingRects,
        SheetInfo sheet,
        bool reuseCrescentSpriteIds)
    {
        SpriteRect[] result = new SpriteRect[FrameCount];

        for (int index = 0; index < FrameCount; index++)
        {
            SpriteRect rect = reuseCrescentSpriteIds
                ? FindRect(existingRects, FrameNames[index])
                : new SpriteRect
                {
                    name = FrameNames[index],
                    spriteID = GUID.Generate()
                };

            rect.name = FrameNames[index];
            rect.rect = new Rect(
                index * sheet.FrameWidth,
                0f,
                sheet.FrameWidth,
                sheet.Height
            );
            rect.alignment = SpriteAlignment.Center;
            rect.pivot = new Vector2(0.5f, 0.5f);
            result[index] = rect;
        }

        return result;
    }

    private static SpriteRect FindRect(
        IEnumerable<SpriteRect> rects,
        string name)
    {
        SpriteRect rect = rects.SingleOrDefault(candidate =>
            candidate.name == name
        );

        if (rect == null)
        {
            throw new InvalidOperationException(
                "Knight projectile SpriteRect is missing: " + name + "."
            );
        }

        return rect;
    }

    private static void ApplySpriteRects(
        TextureImporter importer,
        SpriteRect[] rects)
    {
        SpriteDataProviderFactories factories =
            new SpriteDataProviderFactories();
        factories.Init();

        ISpriteEditorDataProvider provider =
            factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        provider.SetSpriteRects(rects);
        provider.Apply();
        importer.SaveAndReimport();
        AssetDatabase.ImportAsset(
            TargetAssetPath,
            ImportAssetOptions.ForceUpdate |
            ImportAssetOptions.ForceSynchronousImport
        );
        AssetDatabase.SaveAssets();
    }

    private static Sprite[] ResolveKnightFramesOrThrow()
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(TargetAssetPath)
            .OfType<Sprite>()
            .ToArray();

        if (sprites.Length != FrameCount)
        {
            throw new InvalidOperationException(
                "Knight projectile must import exactly four sprites; actual " +
                "count is " + sprites.Length + "."
            );
        }

        Sprite[] ordered = new Sprite[FrameCount];
        for (int index = 0; index < FrameCount; index++)
        {
            ordered[index] = sprites.SingleOrDefault(sprite =>
                sprite.name == FrameNames[index]
            );

            if (ordered[index] == null)
            {
                throw new InvalidOperationException(
                    "Imported Knight projectile frame is missing: " +
                    FrameNames[index] + "."
                );
            }
        }

        return ordered;
    }

    private static void ConfigureProjectilePrefabOrThrow(Sprite[] knightFrames)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(
            ProjectilePrefabPath
        );

        if (prefabRoot == null)
        {
            throw new InvalidOperationException(
                "KnightProjectile prefab could not be loaded."
            );
        }

        try
        {
            KnightProjectile projectile = prefabRoot.GetComponent<KnightProjectile>();
            BoxCollider2D collider = prefabRoot.GetComponent<BoxCollider2D>();
            Transform visual = prefabRoot.transform.Find("ProjectileVisual");
            SpriteRenderer renderer = visual != null
                ? visual.GetComponent<SpriteRenderer>()
                : null;
            SpriteSequencePlayer sequence = visual != null
                ? visual.GetComponent<SpriteSequencePlayer>()
                : null;

            if (projectile == null || collider == null || visual == null ||
                renderer == null || sequence == null)
            {
                throw new InvalidOperationException(
                    "KnightProjectile visual or gameplay setup is incomplete."
                );
            }

            GameplaySnapshot gameplay = new GameplaySnapshot(
                prefabRoot,
                collider,
                projectile
            );
            Vector3 knightScale = CalculateKnightVisualScale(
                knightFrames[0],
                prefabRoot.transform.localScale
            );

            SerializedObject projectileData = new SerializedObject(projectile);
            SerializedProperty knightFramesProperty = projectileData
                .FindProperty("knightBeamFrames");
            SerializedProperty knightScaleProperty = projectileData
                .FindProperty("knightVisualLocalScale");

            if (knightFramesProperty == null || knightScaleProperty == null)
            {
                throw new InvalidOperationException(
                    "KnightProjectile does not expose Knight visual " +
                    "configuration. Recompile scripts and retry setup."
                );
            }

            knightFramesProperty.arraySize = FrameCount;
            for (int index = 0; index < FrameCount; index++)
            {
                knightFramesProperty.GetArrayElementAtIndex(index)
                    .objectReferenceValue = knightFrames[index];
            }

            knightScaleProperty.vector3Value = knightScale;
            ConfigureKnightCrescentColliderProfile(
                projectileData,
                projectile,
                knightFrames[0],
                prefabRoot.transform.localScale,
                knightScale
            );
            projectileData.ApplyModifiedPropertiesWithoutUndo();

            // Keep prefab preview/default rendering coherent. Runtime still
            // selects the frame loop through SetVisualStyle(Knight).
            renderer.sprite = knightFrames[0];
            renderer.color = Color.white;
            renderer.flipX = false;
            visual.localScale = knightScale;
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, ProjectilePrefabPath);

            gameplay.VerifyUnchanged(prefabRoot, collider, projectile);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
    }

    private static void ConfigureKnightCrescentColliderProfile(
        SerializedObject projectileData,
        KnightProjectile projectile,
        Sprite knightFrame,
        Vector3 rootScale,
        Vector3 knightScale)
    {
        SerializedProperty knightSize = projectileData.FindProperty(
            "knightCrescentColliderWorldSize"
        );
        SerializedProperty knightTrailing = projectileData.FindProperty(
            "knightCrescentColliderTrailingWorldOffset"
        );
        SerializedProperty knightYOffset = projectileData.FindProperty(
            "knightCrescentColliderWorldYOffset"
        );
        SerializedProperty debuggerSize = projectileData.FindProperty(
            "debuggerColliderWorldSize"
        );
        SerializedProperty debuggerTrailing = projectileData.FindProperty(
            "debuggerColliderTrailingWorldOffset"
        );

        if (knightSize == null || knightTrailing == null ||
            knightYOffset == null || debuggerSize == null ||
            debuggerTrailing == null)
        {
            throw new InvalidOperationException(
                "KnightProjectile does not expose the crescent collider " +
                "profile. Recompile scripts and retry setup."
            );
        }

        Sprite debuggerFrame = projectile.DebuggerBeamFrames?
            .FirstOrDefault(frame => frame != null);
        if (debuggerFrame == null)
        {
            throw new InvalidOperationException(
                "Debugger beam frame is required as the Knight crescent " +
                "hit-profile scale reference."
            );
        }

        Vector2 knightWorldBounds = CalculateWorldBounds(
            knightFrame.bounds.size,
            rootScale,
            knightScale
        );
        Vector2 debuggerWorldBounds = CalculateWorldBounds(
            debuggerFrame.bounds.size,
            rootScale,
            projectile.DebuggerVisualLocalScale
        );

        if (debuggerWorldBounds.x <= 0f || debuggerWorldBounds.y <= 0f)
        {
            throw new InvalidOperationException(
                "Debugger visual reference bounds are invalid."
            );
        }

        // Keep the same lower-crescent core ratios as the proven Debugger
        // profile, but express the Knight Y offset from its renderer/root.
        // Debugger's serialized Y is torso-root-relative because its visual
        // is separately moved to sword height, so it cannot be copied as a
        // raw world offset to Knight.
        float widthRatio = knightWorldBounds.x / debuggerWorldBounds.x;
        float heightRatio = knightWorldBounds.y / debuggerWorldBounds.y;
        knightSize.vector2Value = new Vector2(
            debuggerSize.vector2Value.x * widthRatio,
            debuggerSize.vector2Value.y * heightRatio
        );
        knightTrailing.floatValue =
            debuggerTrailing.floatValue * widthRatio;
        knightYOffset.floatValue =
            knightWorldBounds.y * KnightLowerCoreVisualCenterYRatio;
    }

    private static Vector3 CalculateKnightVisualScale(
        Sprite frame,
        Vector3 rootScale)
    {
        Vector2 nativeBounds = frame.bounds.size;
        float rootX = Mathf.Abs(rootScale.x);
        float rootY = Mathf.Abs(rootScale.y);

        if (nativeBounds.x <= 0f || nativeBounds.y <= 0f ||
            rootX <= 0f || rootY <= 0f)
        {
            throw new InvalidOperationException(
                "Knight projectile frame bounds or root scale is invalid."
            );
        }

        // Mirror Debugger's visual policy: cancel the gameplay root's
        // non-uniform scale, retain a 1:1 crescent in world space, and use a
        // visual-child X mirror rather than flipping the gameplay root.
        float uniformWorldScale = TargetWorldWidth / nativeBounds.x;
        return new Vector3(
            -uniformWorldScale / rootX,
            uniformWorldScale / rootY,
            1f
        );
    }

    private static BeamValidation ValidateOrThrow()
    {
        SheetInfo sheet = InspectPngOrThrow();
        TextureImporter importer = RequireImporter();
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            TargetAssetPath
        );

        if (texture == null || texture.width != sheet.Width ||
            texture.height != sheet.Height ||
            importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Multiple ||
            !Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit) ||
            importer.filterMode != FilterMode.Point ||
            importer.textureCompression != TextureImporterCompression.Uncompressed ||
            importer.mipmapEnabled || !importer.alphaIsTransparency ||
            importer.wrapMode != TextureWrapMode.Clamp ||
            GetSpriteMeshType(importer) != SpriteMeshType.FullRect)
        {
            throw new InvalidOperationException(
                "Knight projectile importer configuration is invalid."
            );
        }

        SpriteRect[] rects = GetSpriteRects(importer);
        if (rects.Length != FrameCount)
        {
            throw new InvalidOperationException(
                "Knight projectile must have exactly four SpriteRects."
            );
        }

        for (int index = 0; index < FrameCount; index++)
        {
            SpriteRect rect = FindRect(rects, FrameNames[index]);
            Rect expected = new Rect(
                index * sheet.FrameWidth,
                0f,
                sheet.FrameWidth,
                sheet.Height
            );

            if (rect.rect != expected ||
                rect.alignment != SpriteAlignment.Center ||
                rect.pivot != new Vector2(0.5f, 0.5f))
            {
                throw new InvalidOperationException(
                    "Knight projectile SpriteRect is invalid for " +
                    FrameNames[index] + "."
                );
            }
        }

        return ValidatePrefabOrThrow(sheet, ResolveKnightFramesOrThrow());
    }

    private static BeamValidation ValidatePrefabOrThrow(
        SheetInfo sheet,
        Sprite[] knightFrames)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(
            ProjectilePrefabPath
        );

        if (prefabRoot == null)
        {
            throw new InvalidOperationException(
                "KnightProjectile prefab could not be loaded."
            );
        }

        try
        {
            KnightProjectile projectile = prefabRoot.GetComponent<KnightProjectile>();
            Transform visual = prefabRoot.transform.Find("ProjectileVisual");
            SpriteRenderer renderer = visual != null
                ? visual.GetComponent<SpriteRenderer>()
                : null;
            SpriteSequencePlayer sequence = visual != null
                ? visual.GetComponent<SpriteSequencePlayer>()
                : null;

            if (projectile == null || visual == null || renderer == null ||
                sequence == null)
            {
                throw new InvalidOperationException(
                    "KnightProjectile reference structure is invalid."
                );
            }

            if (!SameFrameArray(projectile.KnightBeamFrames, knightFrames) ||
                renderer.sprite != knightFrames[0])
            {
                throw new InvalidOperationException(
                    "Knight projectile frame references/order are invalid."
                );
            }

            Sprite[] debuggerFrames = projectile.DebuggerBeamFrames;
            if (debuggerFrames == null || debuggerFrames.Length != FrameCount ||
                debuggerFrames.Any(frame => frame == null ||
                    knightFrames.Contains(frame)))
            {
                throw new InvalidOperationException(
                    "Knight beam setup changed Debugger frame references."
                );
            }

            if (!Mathf.Approximately(projectile.BeamFramesPerSecond, 12f))
            {
                throw new InvalidOperationException(
                    "Knight projectile frame rate must remain 12 FPS."
                );
            }

            TextureImporter knightImporter = RequireImporter();
            TextureImporter debuggerImporter = AssetImporter.GetAtPath(
                DebuggerBeamAssetPath
            ) as TextureImporter;
            LogSpriteGeometry(
                "KNIGHT_BEAM_GEOMETRY",
                knightFrames,
                knightImporter,
                renderer
            );
            LogSpriteGeometry(
                "DEBUGGER_BEAM_GEOMETRY",
                debuggerFrames,
                debuggerImporter,
                renderer
            );

            Vector3 expectedScale = CalculateKnightVisualScale(
                knightFrames[0],
                prefabRoot.transform.localScale
            );
            Vector3 actualScale = projectile.KnightVisualLocalScale;
            if (!Approximately(actualScale, expectedScale) ||
                !Approximately(visual.localScale, expectedScale))
            {
                throw new InvalidOperationException(
                    "Knight projectile visual scale is stale. Run Setup Knight " +
                    "Beam after replacing the sheet."
                );
            }

            if (renderer.color != Color.white || renderer.flipX)
            {
                throw new InvalidOperationException(
                    "Knight projectile visual color/orientation does not " +
                    "match the Debugger crescent policy."
                );
            }

            Vector2 nativeBounds = knightFrames[0].bounds.size;
            string geometryReport = ValidateFrameGeometry(
                knightFrames,
                sheet,
                knightImporter
            );
            Vector2 worldBounds = CalculateWorldBounds(
                nativeBounds,
                prefabRoot.transform.localScale,
                actualScale
            );

            ValidateKnightCrescentColliderProfile(
                projectile,
                worldBounds,
                prefabRoot.transform.localScale
            );

            if (!Mathf.Approximately(worldBounds.x, TargetWorldWidth) ||
                !Mathf.Approximately(
                    worldBounds.x / worldBounds.y,
                    nativeBounds.x / nativeBounds.y
                ))
            {
                throw new InvalidOperationException(
                    "Knight projectile visual scale does not preserve the " +
                    "target width and imported aspect ratio."
                );
            }

            return new BeamValidation(
                sheet,
                nativeBounds,
                prefabRoot.transform.localScale,
                actualScale,
                worldBounds,
                GetSpriteIds(knightFrames),
                geometryReport
            );
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ValidateKnightCrescentColliderProfile(
        KnightProjectile projectile,
        Vector2 knightWorldBounds,
        Vector3 rootScale)
    {
        Sprite debuggerFrame = projectile.DebuggerBeamFrames?
            .FirstOrDefault(frame => frame != null);
        if (debuggerFrame == null)
        {
            throw new InvalidOperationException(
                "Knight collider validation cannot resolve a Debugger " +
                "crescent scale reference."
            );
        }

        Vector2 debuggerWorldBounds = CalculateWorldBounds(
            debuggerFrame.bounds.size,
            rootScale,
            projectile.DebuggerVisualLocalScale
        );
        float widthRatio = knightWorldBounds.x / debuggerWorldBounds.x;
        float heightRatio = knightWorldBounds.y / debuggerWorldBounds.y;
        Vector2 expectedSize = new Vector2(
            projectile.DebuggerColliderWorldSize.x * widthRatio,
            projectile.DebuggerColliderWorldSize.y * heightRatio
        );
        float expectedTrailing =
            projectile.DebuggerColliderTrailingWorldOffset * widthRatio;
        float expectedYOffset = knightWorldBounds.y *
            KnightLowerCoreVisualCenterYRatio;

        if (!Approximately(
                projectile.KnightCrescentColliderWorldSize,
                expectedSize
            ) ||
            !Mathf.Approximately(
                projectile.KnightCrescentColliderTrailingWorldOffset,
                expectedTrailing
            ) ||
            !Mathf.Approximately(
                projectile.KnightCrescentColliderWorldYOffset,
                expectedYOffset
            ))
        {
            throw new InvalidOperationException(
                "Knight crescent collider profile is stale. Run Setup Knight " +
                "Beam to rebuild it from the Debugger lower-core ratio."
            );
        }

        Debug.Log(
            "[Knight Crescent Collider] " +
            $"visual={knightWorldBounds.x:F3}x" +
            $"{knightWorldBounds.y:F3} " +
            $"worldSize={projectile.KnightCrescentColliderWorldSize.x:F3}x" +
            $"{projectile.KnightCrescentColliderWorldSize.y:F3} " +
            $"trailing={projectile.KnightCrescentColliderTrailingWorldOffset:F3} " +
            $"visualRelativeY={projectile.KnightCrescentColliderWorldYOffset:F3}"
        );
    }

    private static bool SameFrameArray(Sprite[] first, Sprite[] second)
    {
        return first != null && second != null &&
               first.Length == FrameCount && second.Length == FrameCount &&
               !first.Where((frame, index) => frame != second[index]).Any();
    }

    private static void LogSpriteGeometry(
        string tag,
        IEnumerable<Sprite> frames,
        TextureImporter importer,
        SpriteRenderer renderer)
    {
        string rendererSprite = renderer != null && renderer.sprite != null
            ? renderer.sprite.name + " (" + GetSpriteLocalId(renderer.sprite) + ")"
            : "<none>";
        string meshType = importer != null
            ? GetSpriteMeshType(importer).ToString()
            : "<missing importer>";

        foreach (Sprite frame in frames)
        {
            Vector2[] vertices = frame.vertices;
            string vertexText = vertices == null
                ? "<null>"
                : string.Join(", ", vertices.Select(vertex =>
                    $"({vertex.x:F3},{vertex.y:F3})"
                ));
            string triangleText = frame.triangles == null
                ? "<null>"
                : string.Join(", ", frame.triangles.Select(index =>
                    index.ToString()
                ));

            Debug.Log(
                $"[{tag}]\n" +
                $"name={frame.name}\n" +
                $"rect={frame.rect}\n" +
                $"textureRect={frame.textureRect}\n" +
                $"boundsSize={frame.bounds.size}\n" +
                $"pivot={frame.pivot}\n" +
                $"ppu={frame.pixelsPerUnit:F3}\n" +
                $"vertices={vertices?.Length ?? 0}: {vertexText}\n" +
                $"triangles={triangleText}\n" +
                $"packingMode={frame.packingMode}\n" +
                $"packingRotation={frame.packingRotation}\n" +
                $"meshType={meshType}\n" +
                $"rendererSprite={rendererSprite}"
            );
        }
    }

    private static string ValidateFrameGeometry(
        IEnumerable<Sprite> frames,
        SheetInfo sheet,
        TextureImporter importer)
    {
        List<string> reports = new List<string>();
        int index = 0;

        if (GetSpriteMeshType(importer) != SpriteMeshType.FullRect)
        {
            throw new InvalidOperationException(
                "Knight projectile importer must use SpriteMeshType.FullRect."
            );
        }

        foreach (Sprite frame in frames)
        {
            Rect expectedRect = new Rect(
                index * sheet.FrameWidth,
                0f,
                sheet.FrameWidth,
                sheet.Height
            );
            Vector2 expectedBounds = new Vector2(
                frame.rect.width / frame.pixelsPerUnit,
                frame.rect.height / frame.pixelsPerUnit
            );
            Vector2[] vertices = frame.vertices;
            List<string> failures = GetFullRectFailures(
                frame,
                expectedRect,
                expectedBounds,
                vertices
            );

            if (failures.Count != 0)
            {
                throw new InvalidOperationException(
                    "Knight projectile geometry is not a full " +
                    sheet.FrameWidth + "x" + sheet.Height +
                    " local-space quad for " + frame.name + ". " +
                    string.Join("; ", failures)
                );
            }

            reports.Add(
                $"Frame={frame.name} Rect={frame.rect.width:F0}x" +
                $"{frame.rect.height:F0} Mesh=FullRect Vertices=" +
                $"{vertices.Length} Bounds={frame.bounds.size.x:F3}x" +
                $"{frame.bounds.size.y:F3} PASS"
            );
            index++;
        }

        return string.Join("\n", reports);
    }

    private static List<string> GetFullRectFailures(
        Sprite frame,
        Rect expectedRect,
        Vector2 expectedBounds,
        Vector2[] vertices)
    {
        List<string> failures = new List<string>();

        if (!Approximately(frame.rect, expectedRect))
        {
            failures.Add("rect=" + frame.rect + " expected=" + expectedRect);
        }

        if (!Approximately(frame.textureRect.size, expectedRect.size))
        {
            failures.Add(
                "textureRectSize=" + frame.textureRect.size +
                " expected=" + expectedRect.size
            );
        }

        Vector2 expectedPivot = expectedRect.size * 0.5f;
        if (!Approximately(frame.pivot, expectedPivot))
        {
            failures.Add(
                "pivot=" + frame.pivot + " expected=" + expectedPivot
            );
        }

        Vector2 actualBounds = new Vector2(
            frame.bounds.size.x,
            frame.bounds.size.y
        );
        if (!Approximately(actualBounds, expectedBounds))
        {
            failures.Add(
                "bounds=" + actualBounds + " expected=" + expectedBounds
            );
        }

        if (vertices == null || vertices.Length != 4)
        {
            failures.Add(
                "vertexCount=" + (vertices?.Length ?? 0) + " expected=4"
            );
        }
        else
        {
            float minX = vertices.Min(vertex => vertex.x);
            float maxX = vertices.Max(vertex => vertex.x);
            float minY = vertices.Min(vertex => vertex.y);
            float maxY = vertices.Max(vertex => vertex.y);
            Vector2 vertexExtent = new Vector2(maxX - minX, maxY - minY);

            if (!Approximately(vertexExtent, expectedBounds))
            {
                failures.Add(
                    "vertexExtent=" + vertexExtent +
                    " expectedLocalUnits=" + expectedBounds
                );
            }
        }

        if (frame.triangles == null || frame.triangles.Length != 6 ||
            vertices == null || frame.triangles.Any(index =>
                index < 0 || index >= vertices.Length
            ))
        {
            failures.Add("triangles do not form a four-vertex quad");
        }

        return failures;
    }

    private static bool Approximately(Rect first, Rect second)
    {
        return Approximately(first.x, second.x) &&
               Approximately(first.y, second.y) &&
               Approximately(first.width, second.width) &&
               Approximately(first.height, second.height);
    }

    private static bool Approximately(Vector2 first, Vector2 second)
    {
        return Approximately(first.x, second.x) &&
               Approximately(first.y, second.y);
    }

    private static bool Approximately(float first, float second)
    {
        return Mathf.Abs(first - second) <= GeometryEpsilon;
    }

    private static Vector2 CalculateWorldBounds(
        Vector2 nativeBounds,
        Vector3 rootScale,
        Vector3 visualScale)
    {
        return new Vector2(
            nativeBounds.x * Mathf.Abs(rootScale.x * visualScale.x),
            nativeBounds.y * Mathf.Abs(rootScale.y * visualScale.y)
        );
    }

    private static bool Approximately(Vector3 first, Vector3 second)
    {
        return Mathf.Approximately(first.x, second.x) &&
               Mathf.Approximately(first.y, second.y) &&
               Mathf.Approximately(first.z, second.z);
    }

    private static string GetSpriteIds(IEnumerable<Sprite> sprites)
    {
        return string.Join(", ", sprites.Select(sprite =>
        {
            return sprite.name + "=" + GetSpriteLocalId(sprite);
        }));
    }

    private static long GetSpriteLocalId(Sprite sprite)
    {
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
            sprite,
            out _,
            out long localId
        );
        return localId;
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath);
    }

    private readonly struct BeamValidation
    {
        private readonly SheetInfo sheet;
        private readonly Vector2 nativeBounds;
        private readonly Vector3 rootScale;
        private readonly Vector3 visualScale;
        private readonly Vector2 worldBounds;
        private readonly string spriteIds;
        private readonly string geometryReport;

        public BeamValidation(
            SheetInfo sheet,
            Vector2 nativeBounds,
            Vector3 rootScale,
            Vector3 visualScale,
            Vector2 worldBounds,
            string spriteIds,
            string geometryReport)
        {
            this.sheet = sheet;
            this.nativeBounds = nativeBounds;
            this.rootScale = rootScale;
            this.visualScale = visualScale;
            this.worldBounds = worldBounds;
            this.spriteIds = spriteIds;
            this.geometryReport = geometryReport;
        }

        public string ToMultilineString(string spriteIdHandling)
        {
            return
                $"Texture: {sheet.Width}x{sheet.Height}\n" +
                $"Alpha: transparency={sheet.HasTransparency}, " +
                $"visible={sheet.HasVisiblePixels}\n" +
                $"Frames: {FrameCount}\n" +
                $"Frame Size: {sheet.FrameWidth}x{sheet.Height}\n" +
                $"PPU: {PixelsPerUnit:F0}\n" +
                $"Native Bounds: {nativeBounds.x:F3} x {nativeBounds.y:F3}\n" +
                $"Root Scale: {rootScale}\n" +
                $"Knight Visual Scale: {visualScale}\n" +
                $"Expected World Bounds: {worldBounds.x:F3} x " +
                $"{worldBounds.y:F3}\n" +
                $"Target Width: {TargetWorldWidth:F2}\n" +
                $"Sprite IDs: {spriteIdHandling} ({spriteIds})\n" +
                "[Knight Beam Geometry]\n" + geometryReport;
        }
    }
}
