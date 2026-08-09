using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Configures the user-supplied Debugger projectile sheet in place. The PNG
/// pixels are never regenerated here: this tool only imports, slices, wires,
/// and validates the existing target asset.
/// </summary>
public static class PatchBreakDebuggerProjectileVisualSetup
{
    private const string TargetAssetPath =
        "Assets/Art/VFX/Projectiles/Debugger/debugger_beam_sheet.png";

    private const string ProjectilePrefabPath =
        "Assets/Prefabs/Enemies/KnightProjectile.prefab";

    private const int FrameCount = 4;
    private const float PixelsPerUnit = 32f;
    private const float TargetWorldWidth = 3.6f;
    private const float MinimumWorldWidth = 3.5f;
    private const float MaximumWorldWidth = 3.7f;
    private const float GuardReferenceWidth = 5.4f;
    private const byte AlphaThreshold = 8;

    // These are the established names in the current target metadata and
    // projectile references. Existing SpriteRects with these names retain
    // their SpriteIDs when their rects are rewritten.
    private static readonly string[] FrameNames =
    {
        "Debugger_Beam_00",
        "Debugger_Beam_01",
        "Debugger_Beam_02",
        "Debugger_Beam_03"
    };

    private readonly struct SheetInfo
    {
        public readonly int Width;
        public readonly int Height;
        public readonly bool HasTransparentPixels;
        public readonly bool HasVisiblePixels;

        public SheetInfo(
            int width,
            int height,
            bool hasTransparentPixels,
            bool hasVisiblePixels)
        {
            Width = width;
            Height = height;
            HasTransparentPixels = hasTransparentPixels;
            HasVisiblePixels = hasVisiblePixels;
        }

        public int FrameWidth => Width / FrameCount;
    }

    private readonly struct ProjectileGameplaySnapshot
    {
        private readonly Vector3 rootScale;
        private readonly Vector2 colliderOffset;
        private readonly Vector2 colliderSize;
        private readonly float speed;
        private readonly float lifetime;
        private readonly int damage;

        public ProjectileGameplaySnapshot(
            GameObject root,
            BoxCollider2D collider,
            KnightProjectile projectile)
        {
            rootScale = root.transform.localScale;
            colliderOffset = collider.offset;
            colliderSize = collider.size;

            SerializedObject data = new SerializedObject(projectile);
            speed = data.FindProperty("speed").floatValue;
            lifetime = data.FindProperty("lifetime").floatValue;
            damage = data.FindProperty("damage").intValue;
        }

        public void VerifyUnchanged(
            GameObject root,
            Rigidbody2D body,
            BoxCollider2D collider,
            KnightProjectile projectile)
        {
            if (body == null ||
                root.transform.localScale != rootScale ||
                collider.offset != colliderOffset ||
                collider.size != colliderSize)
            {
                throw new InvalidOperationException(
                    "Debugger projectile setup changed root or collider " +
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
                    "Debugger projectile setup changed movement or damage " +
                    "data."
                );
            }
        }
    }

    [MenuItem("Tools/PATCH BREAK/VFX/Setup Debugger Beam")]
    public static void SetupDebuggerBeam()
    {
        SheetInfo sheet = InspectTargetPngOrThrow();
        string targetGuidBefore = AssetDatabase.AssetPathToGUID(
            TargetAssetPath
        );

        if (string.IsNullOrEmpty(targetGuidBefore))
        {
            throw new InvalidOperationException(
                "Debugger projectile asset GUID is missing at " +
                TargetAssetPath + "."
            );
        }

        TextureImporter importer = RequireImporter();
        ConfigureImporter(importer);
        importer = RequireImporter();

        SpriteRect[] existingRects = GetSpriteRects(importer);
        bool preservingSpriteIds = CanPreserveSpriteIds(existingRects);
        string[] previousSpriteIds = preservingSpriteIds
            ? FrameNames.Select(name => FindRect(existingRects, name)
                .spriteID.ToString()).ToArray()
            : Array.Empty<string>();

        SpriteRect[] desiredRects = BuildSliceRects(
            existingRects,
            sheet,
            preservingSpriteIds
        );
        ApplySpriteRects(importer, desiredRects);

        if (AssetDatabase.AssetPathToGUID(TargetAssetPath) !=
            targetGuidBefore)
        {
            throw new InvalidOperationException(
                "Debugger projectile target GUID changed during setup."
            );
        }

        if (preservingSpriteIds)
        {
            SpriteRect[] importedRects = GetSpriteRects(RequireImporter());

            for (int index = 0; index < FrameCount; index++)
            {
                if (FindRect(importedRects, FrameNames[index])
                    .spriteID.ToString() != previousSpriteIds[index])
                {
                    throw new InvalidOperationException(
                        "Debugger projectile SpriteID changed for " +
                        FrameNames[index] + "."
                    );
                }
            }
        }

        Sprite[] debuggerFrames = ResolveDebuggerFramesOrThrow();
        ConfigureProjectilePrefabOrThrow(debuggerFrames);
        BeamValidation validation = ValidateOrThrow();

        Debug.Log(
            "[Debugger Beam] SETUP PASS\n" +
            validation.ToMultilineString(
                preservingSpriteIds
                    ? "preserved"
                    : "recreated and rewired"
            )
        );
    }

    [MenuItem("Tools/PATCH BREAK/VFX/Validate Debugger Beam")]
    public static void ValidateDebuggerBeam()
    {
        BeamValidation validation = ValidateOrThrow();
        Debug.Log(
            "[Debugger Beam] PASS\n" +
            validation.ToMultilineString("validated")
        );
    }

    private static SheetInfo InspectTargetPngOrThrow()
    {
        if (!File.Exists(ToAbsolutePath(TargetAssetPath)))
        {
            throw new InvalidOperationException(
                "Debugger projectile PNG is missing at " +
                TargetAssetPath + "."
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
                    File.ReadAllBytes(ToAbsolutePath(TargetAssetPath)),
                    false
                ))
            {
                throw new InvalidOperationException(
                    "Debugger projectile PNG could not be decoded."
                );
            }

            if (texture.width <= 0 || texture.height <= 0 ||
                texture.width % FrameCount != 0)
            {
                throw new InvalidOperationException(
                    "Debugger projectile PNG width must be positive and " +
                    "exactly divisible by four. Actual size: " +
                    texture.width + "x" + texture.height + "."
                );
            }

            bool hasTransparentPixels = false;
            bool hasVisiblePixels = false;

            foreach (Color32 pixel in texture.GetPixels32())
            {
                hasTransparentPixels |= pixel.a == 0;
                hasVisiblePixels |= pixel.a > AlphaThreshold;
            }

            if (!hasTransparentPixels || !hasVisiblePixels)
            {
                throw new InvalidOperationException(
                    "Debugger projectile PNG must contain both visible art " +
                    "and transparent pixels."
                );
            }

            return new SheetInfo(
                texture.width,
                texture.height,
                hasTransparentPixels,
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
                "Debugger projectile TextureImporter is missing at " +
                TargetAssetPath + "."
            );
        }

        return importer;
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

        if (changed)
        {
            importer.SaveAndReimport();
        }
        else
        {
            // The user may just have replaced the PNG at this same path.
            AssetDatabase.ImportAsset(
                TargetAssetPath,
                ImportAssetOptions.ForceUpdate
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

    private static bool CanPreserveSpriteIds(SpriteRect[] rects)
    {
        return rects.Length == FrameCount &&
               FrameNames.All(name =>
                   rects.Count(rect => rect.name == name) == 1
               );
    }

    private static SpriteRect[] BuildSliceRects(
        SpriteRect[] existingRects,
        SheetInfo sheet,
        bool preserveSpriteIds)
    {
        SpriteRect[] result = new SpriteRect[FrameCount];

        for (int index = 0; index < FrameCount; index++)
        {
            SpriteRect rect = preserveSpriteIds
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
                "Debugger projectile SpriteRect is missing: " + name + "."
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
        AssetDatabase.SaveAssets();
    }

    private static Sprite[] ResolveDebuggerFramesOrThrow()
    {
        Sprite[] allSprites = AssetDatabase.LoadAllAssetsAtPath(
                TargetAssetPath
            )
            .OfType<Sprite>()
            .ToArray();

        if (allSprites.Length != FrameCount)
        {
            throw new InvalidOperationException(
                "Debugger projectile must import exactly four sprites; " +
                "actual count is " + allSprites.Length + "."
            );
        }

        Sprite[] ordered = new Sprite[FrameCount];

        for (int index = 0; index < FrameCount; index++)
        {
            ordered[index] = allSprites.SingleOrDefault(sprite =>
                sprite.name == FrameNames[index]
            );

            if (ordered[index] == null)
            {
                throw new InvalidOperationException(
                    "Imported Debugger projectile frame is missing: " +
                    FrameNames[index] + "."
                );
            }
        }

        return ordered;
    }

    private static void ConfigureProjectilePrefabOrThrow(
        Sprite[] debuggerFrames)
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
            KnightProjectile projectile = prefabRoot.GetComponent<
                KnightProjectile
            >();
            Rigidbody2D body = prefabRoot.GetComponent<Rigidbody2D>();
            BoxCollider2D collider = prefabRoot.GetComponent<BoxCollider2D>();
            Transform visual = prefabRoot.transform.Find("ProjectileVisual");

            if (projectile == null || body == null || collider == null ||
                visual == null || visual.GetComponent<SpriteRenderer>() == null ||
                visual.GetComponent<SpriteSequencePlayer>() == null)
            {
                throw new InvalidOperationException(
                    "KnightProjectile visual or gameplay setup is incomplete."
                );
            }

            ProjectileGameplaySnapshot gameplay =
                new ProjectileGameplaySnapshot(
                    prefabRoot,
                    collider,
                    projectile
                );
            UnityEngine.Object[] knightFramesBefore = projectile
                .KnightBeamFrames
                .Cast<UnityEngine.Object>()
                .ToArray();
            Vector3 debuggerVisualScale = CalculateDebuggerVisualScale(
                debuggerFrames[0],
                prefabRoot.transform.localScale
            );

            SerializedObject projectileData = new SerializedObject(projectile);
            SerializedProperty debuggerFramesProperty =
                projectileData.FindProperty("debuggerBeamFrames");
            SerializedProperty debuggerScaleProperty =
                projectileData.FindProperty("debuggerVisualLocalScale");

            if (debuggerFramesProperty == null ||
                debuggerScaleProperty == null)
            {
                throw new InvalidOperationException(
                    "KnightProjectile does not expose Debugger visual " +
                    "configuration. Recompile scripts and retry setup."
                );
            }

            debuggerFramesProperty.arraySize = FrameCount;

            for (int index = 0; index < FrameCount; index++)
            {
                debuggerFramesProperty
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue = debuggerFrames[index];
            }

            debuggerScaleProperty.vector3Value = debuggerVisualScale;
            projectileData.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, ProjectilePrefabPath);

            gameplay.VerifyUnchanged(
                prefabRoot,
                body,
                collider,
                projectile
            );
            VerifyKnightFramesUnchanged(projectile, knightFramesBefore);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
    }

    private static void VerifyKnightFramesUnchanged(
        KnightProjectile projectile,
        UnityEngine.Object[] before)
    {
        Sprite[] after = projectile.KnightBeamFrames;

        if (after == null || after.Length != before.Length ||
            after.Where((frame, index) => frame != before[index]).Any())
        {
            throw new InvalidOperationException(
                "Debugger beam setup changed Knight projectile frames."
            );
        }
    }

    private static Vector3 CalculateDebuggerVisualScale(
        Sprite frame,
        Vector3 projectileRootScale)
    {
        Vector2 nativeBounds = frame.bounds.size;
        float rootX = Mathf.Abs(projectileRootScale.x);
        float rootY = Mathf.Abs(projectileRootScale.y);

        if (nativeBounds.x <= 0f || nativeBounds.y <= 0f ||
            rootX <= 0f || rootY <= 0f)
        {
            throw new InvalidOperationException(
                "Debugger projectile frame bounds or root scale is invalid."
            );
        }

        // Use one final world-space multiplier for both axes. The child
        // numbers differ only to cancel the gameplay root's non-uniform
        // visual scale; the crescent therefore keeps its imported aspect.
        float uniformWorldScale = TargetWorldWidth / nativeBounds.x;

        return new Vector3(
            -uniformWorldScale / rootX,
            uniformWorldScale / rootY,
            1f
        );
    }

    private static BeamValidation ValidateOrThrow()
    {
        SheetInfo sheet = InspectTargetPngOrThrow();
        TextureImporter importer = RequireImporter();
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            TargetAssetPath
        );

        if (texture == null || texture.width != sheet.Width ||
            texture.height != sheet.Height ||
            importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Multiple ||
            !Mathf.Approximately(
                importer.spritePixelsPerUnit,
                PixelsPerUnit
            ) ||
            importer.filterMode != FilterMode.Point ||
            importer.textureCompression !=
                TextureImporterCompression.Uncompressed ||
            importer.mipmapEnabled ||
            !importer.alphaIsTransparency ||
            importer.wrapMode != TextureWrapMode.Clamp)
        {
            throw new InvalidOperationException(
                "Debugger projectile importer configuration is invalid."
            );
        }

        SpriteRect[] rects = GetSpriteRects(importer);

        if (rects.Length != FrameCount)
        {
            throw new InvalidOperationException(
                "Debugger projectile must have exactly four SpriteRects."
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
                    "Debugger projectile SpriteRect is invalid for " +
                    FrameNames[index] + "."
                );
            }
        }

        Sprite[] debuggerFrames = ResolveDebuggerFramesOrThrow();
        return ValidatePrefabAndBuildReport(sheet, debuggerFrames);
    }

    private static BeamValidation ValidatePrefabAndBuildReport(
        SheetInfo sheet,
        Sprite[] debuggerFrames)
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
            KnightProjectile projectile = prefabRoot.GetComponent<
                KnightProjectile
            >();
            Transform visual = prefabRoot.transform.Find("ProjectileVisual");
            Rigidbody2D body = prefabRoot.GetComponent<Rigidbody2D>();
            BoxCollider2D collider = prefabRoot.GetComponent<BoxCollider2D>();

            if (projectile == null || visual == null || body == null ||
                collider == null || visual.GetComponent<SpriteRenderer>() == null ||
                visual.GetComponent<SpriteSequencePlayer>() == null)
            {
                throw new InvalidOperationException(
                    "KnightProjectile reference structure is invalid."
                );
            }

            VerifyDebuggerFrameArray(projectile, debuggerFrames);
            VerifyKnightFramesAreUntouched(projectile, debuggerFrames);

            if (!Mathf.Approximately(projectile.BeamFramesPerSecond, 12f))
            {
                throw new InvalidOperationException(
                    "Debugger projectile frame rate must remain 12 FPS."
                );
            }

            Vector3 expectedScale = CalculateDebuggerVisualScale(
                debuggerFrames[0],
                prefabRoot.transform.localScale
            );
            Vector3 actualScale = projectile.DebuggerVisualLocalScale;

            if (!Approximately(actualScale, expectedScale))
            {
                throw new InvalidOperationException(
                    "Debugger projectile visual scale is stale. Run Setup " +
                    "Debugger Beam after replacing the sheet."
                );
            }

            Vector2 nativeBounds = debuggerFrames[0].bounds.size;
            Vector2 worldBounds = CalculateWorldBounds(
                nativeBounds,
                prefabRoot.transform.localScale,
                actualScale
            );

            if (worldBounds.x < MinimumWorldWidth ||
                worldBounds.x > MaximumWorldWidth)
            {
                throw new InvalidOperationException(
                    "Debugger projectile visual width is outside the " +
                    "Guard-scale target range."
                );
            }

            return new BeamValidation(
                sheet,
                nativeBounds,
                prefabRoot.transform.localScale,
                actualScale,
                worldBounds
            );
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void VerifyDebuggerFrameArray(
        KnightProjectile projectile,
        Sprite[] expected)
    {
        Sprite[] actual = projectile.DebuggerBeamFrames;

        if (actual == null || actual.Length != FrameCount)
        {
            throw new InvalidOperationException(
                "Debugger projectile frame array must contain four frames."
            );
        }

        for (int index = 0; index < FrameCount; index++)
        {
            if (actual[index] == null || actual[index] != expected[index])
            {
                throw new InvalidOperationException(
                    "Debugger projectile frame reference/order is invalid " +
                    "at index " + index + "."
                );
            }
        }
    }

    private static void VerifyKnightFramesAreUntouched(
        KnightProjectile projectile,
        Sprite[] debuggerFrames)
    {
        Sprite[] knightFrames = projectile.KnightBeamFrames;

        if (knightFrames == null || knightFrames.Length != FrameCount ||
            knightFrames.Any(frame => frame == null ||
                debuggerFrames.Contains(frame)))
        {
            throw new InvalidOperationException(
                "Knight projectile frame references were changed."
            );
        }
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

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)
            .FullName;

        return Path.Combine(projectRoot, assetPath);
    }

    private readonly struct BeamValidation
    {
        private readonly SheetInfo sheet;
        private readonly Vector2 nativeBounds;
        private readonly Vector3 rootScale;
        private readonly Vector3 visualScale;
        private readonly Vector2 worldBounds;

        public BeamValidation(
            SheetInfo sheet,
            Vector2 nativeBounds,
            Vector3 rootScale,
            Vector3 visualScale,
            Vector2 worldBounds)
        {
            this.sheet = sheet;
            this.nativeBounds = nativeBounds;
            this.rootScale = rootScale;
            this.visualScale = visualScale;
            this.worldBounds = worldBounds;
        }

        public string ToMultilineString(string spriteIdHandling)
        {
            return
                $"Texture: {sheet.Width}x{sheet.Height}\n" +
                $"Frames: {FrameCount}\n" +
                $"Frame Size: {sheet.FrameWidth}x{sheet.Height}\n" +
                $"PPU: {PixelsPerUnit:F0}\n" +
                $"Native Bounds: {nativeBounds.x:F3} x " +
                $"{nativeBounds.y:F3}\n" +
                $"Root Scale: {rootScale}\n" +
                $"Debugger Visual Scale: {visualScale}\n" +
                $"Visual Bounds: {worldBounds.x:F3} x " +
                $"{worldBounds.y:F3}\n" +
                $"Target Width: {TargetWorldWidth:F2} " +
                $"(Guard reference: {GuardReferenceWidth:F2})\n" +
                $"Sprite IDs: {spriteIdHandling}";
        }
    }
}
