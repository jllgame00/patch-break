using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Applies resize-safe pixel-art skins to the existing floating Runtime Console.
/// A sliced Frame and a separate, non-stretched Tail are sourced as sub-sprites
/// from each x4 artwork; all console behavior and content hierarchy stay intact.
/// </summary>
public static class PatchBreakConsoleArtSetup
{
    private const string MenuRoot = "Tools/PATCH BREAK/Console Art/";
    private const string SkinName = "ConsoleSkin";
    private const string FrameName = "Frame";
    private const string TailName = "Tail";
    private const string DragHandleName = "TitleBarDragArea";
    private const string ResizeHandleName = "ResizeHandle";
    private const float Tolerance = 0.01f;

    // Pixel analysis of the x4 textures:
    // - opaque rectangular frame: 752 x 232 (texture rect y = 40)
    // - frame borders: left/right/bottom 8, top 48 (header included)
    // - the tail is a separate opaque area below the frame at x = 60
    private static readonly SkinSpec[] SkinSpecs =
    {
        new SkinSpec(
            "Assets/Scenes/Battle.unity",
            "Battle",
            "Ally",
            "Assets/Art/UI/Console/Golem/bubble_ally_x4.png",
            "ConsoleAllyFrame",
            "ConsoleAllyTail",
            new Rect(0f, 40f, 752f, 232f),
            new Rect(60f, 24f, 28f, 16f),
            new Vector4(8f, 8f, 8f, 48f)
        ),
        new SkinSpec(
            "Assets/Scenes/KnightBattle.unity",
            "KnightBattle",
            "System",
            "Assets/Art/UI/Console/Knight/bubble_system_x4.png",
            "ConsoleSystemFrame",
            "ConsoleSystemTail",
            new Rect(0f, 40f, 752f, 232f),
            new Rect(60f, 24f, 28f, 16f),
            new Vector4(8f, 8f, 8f, 48f)
        ),
        new SkinSpec(
            "Assets/Scenes/DebuggerBattle.unity",
            "DebuggerBattle",
            "Boss",
            "Assets/Art/UI/Console/Debugger/bubble_boss_x4.png",
            "ConsoleBossFrame",
            "ConsoleBossTail",
            new Rect(0f, 40f, 752f, 232f),
            new Rect(60f, 16f, 28f, 24f),
            new Vector4(8f, 8f, 8f, 48f)
        )
    };

    [MenuItem(MenuRoot + "Analyze Assets")]
    public static void AnalyzeAssets()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        foreach (SkinSpec spec in SkinSpecs)
        {
            LogAssetAnalysis(spec);
        }

        AnalyzeConsoleHierarchies();
        Debug.Log("PATCH_BREAK_CONSOLE_ART_ANALYSIS_COMPLETE");
    }

    [MenuItem(MenuRoot + "Setup Battle First")]
    public static void SetupBattleFirst()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SetupScenes(new[] { SkinSpecs[0] });
        Debug.Log("PATCH_BREAK_CONSOLE_ART_BATTLE_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Setup All Scenes")]
    public static void SetupAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SetupScenes(SkinSpecs);
        Debug.Log("PATCH_BREAK_CONSOLE_ART_SETUP_COMPLETE");
    }

    [MenuItem(MenuRoot + "Validate All Scenes")]
    public static void ValidateAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        ValidateImportsOrThrow(SkinSpecs);
        ValidateScenes(SkinSpecs);
        Debug.Log("PATCH_BREAK_CONSOLE_ART_VALIDATION_COMPLETE");
    }

    private static bool PrepareEditorOperation()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "PATCH//BREAK console art setup cannot run in Play Mode."
            );
            return false;
        }

        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static void SetupScenes(IEnumerable<SkinSpec> specs)
    {
        List<SkinSpec> targets = new List<SkinSpec>(specs);
        SceneSetup[] originalSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            // Validate every existing functional reference first. This keeps
            // Setup All from partially skinning a structurally invalid scene.
            foreach (SkinSpec spec in targets)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    spec.ScenePath,
                    OpenSceneMode.Single
                );
                ValidatePrerequisitesOrThrow(scene, spec);
            }

            ConfigureImports(targets);
            ValidateImportsOrThrow(targets);

            foreach (SkinSpec spec in targets)
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

    private static void ValidateScenes(IEnumerable<SkinSpec> specs)
    {
        SceneSetup[] originalSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (SkinSpec spec in specs)
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

    private static void SetupScene(Scene scene, SkinSpec spec)
    {
        RuntimeConsoleUI console = FindSingleConsoleOrThrow(scene, spec.Name);
        RectTransform consoleRect = console.transform as RectTransform;

        if (consoleRect == null)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: RuntimeConsolePanel has no RectTransform."
            );
        }

        ConfigureExistingBackground(console.gameObject);
        ConfigureSkin(consoleRect, spec);
        ConfigureDragVisual(consoleRect);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ConfigureImports(IEnumerable<SkinSpec> specs)
    {
        foreach (SkinSpec spec in specs)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(spec.TexturePath) as TextureImporter;

            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"{spec.TexturePath}: TextureImporter is missing."
                );
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.spritesheet = new[]
            {
                CreateSpriteMeta(
                    spec.FrameSpriteName,
                    spec.FrameRect,
                    spec.FrameBorder
                ),
                CreateSpriteMeta(
                    spec.TailSpriteName,
                    spec.TailRect,
                    Vector4.zero
                )
            };
            importer.SaveAndReimport();
        }
    }

    private static SpriteMetaData CreateSpriteMeta(
        string name,
        Rect rect,
        Vector4 border)
    {
        return new SpriteMetaData
        {
            name = name,
            rect = rect,
            alignment = (int)SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f),
            border = border
        };
    }

    private static void ConfigureExistingBackground(GameObject consoleObject)
    {
        Image background = consoleObject.GetComponent<Image>();
        Outline outline = consoleObject.GetComponent<Outline>();

        if (background == null || outline == null)
        {
            throw new InvalidOperationException(
                "RuntimeConsolePanel is missing its existing background visual."
            );
        }

        // The sliced artwork includes its own body fill and border. Keep the
        // old components for hierarchy stability, but make them visually inert.
        background.color = new Color(1f, 1f, 1f, 0f);
        background.raycastTarget = false;
        outline.enabled = false;
        EditorUtility.SetDirty(background);
        EditorUtility.SetDirty(outline);
    }

    private static void ConfigureSkin(RectTransform consoleRect, SkinSpec spec)
    {
        RectTransform skin = GetOrCreateUiChild(consoleRect, SkinName);
        SetStretchRect(skin);
        skin.SetSiblingIndex(0);

        RectTransform frame = GetOrCreateUiChild(skin, FrameName);
        SetStretchRect(frame);
        Image frameImage = GetOrAddComponent<Image>(frame.gameObject);
        frameImage.sprite = LoadSubSprite(spec.TexturePath, spec.FrameSpriteName);
        frameImage.type = Image.Type.Sliced;
        frameImage.fillCenter = true;
        frameImage.preserveAspect = false;
        frameImage.raycastTarget = false;
        EditorUtility.SetDirty(frameImage);

        RectTransform tail = GetOrCreateUiChild(skin, TailName);
        float tailAnchorX = spec.TailRect.x / spec.FrameRect.width;
        SetRect(
            tail,
            new Vector2(tailAnchorX, 0f),
            new Vector2(tailAnchorX, 0f),
            new Vector2(0f, 1f),
            Vector2.zero,
            spec.TailRect.size
        );
        Image tailImage = GetOrAddComponent<Image>(tail.gameObject);
        tailImage.sprite = LoadSubSprite(spec.TexturePath, spec.TailSpriteName);
        tailImage.type = Image.Type.Simple;
        tailImage.preserveAspect = true;
        tailImage.raycastTarget = false;
        EditorUtility.SetDirty(tailImage);

        // Draw the tail first where it joins the bottom edge; the frame hides
        // any seam while the tail remains outside the window rect when resized.
        tail.SetSiblingIndex(0);
        frame.SetSiblingIndex(1);
    }

    private static void ConfigureDragVisual(RectTransform consoleRect)
    {
        RectTransform dragRect = FindDirectChild(consoleRect, DragHandleName);

        if (dragRect == null)
        {
            throw new InvalidOperationException(
                "RuntimeConsolePanel is missing TitleBarDragArea."
            );
        }

        // x4 header is 48 px tall. The transparent Image keeps the existing
        // drag raycast area while exposing the header artwork underneath.
        SetRect(
            dragRect,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            new Vector2(0f, 48f)
        );

        Image image = dragRect.GetComponent<Image>();

        if (image == null ||
            dragRect.GetComponent<FloatingConsoleDragHandle>() == null)
        {
            throw new InvalidOperationException(
                "TitleBarDragArea is missing its existing drag behavior."
            );
        }

        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
        EditorUtility.SetDirty(image);
    }

    private static void ValidateImportsOrThrow(IEnumerable<SkinSpec> specs)
    {
        List<string> errors = new List<string>();

        foreach (SkinSpec spec in specs)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(spec.TexturePath) as TextureImporter;
            Sprite frame = LoadSubSpriteOrNull(
                spec.TexturePath,
                spec.FrameSpriteName
            );
            Sprite tail = LoadSubSpriteOrNull(
                spec.TexturePath,
                spec.TailSpriteName
            );

            if (importer == null || frame == null || tail == null ||
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Multiple ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, 100f) ||
                importer.filterMode != FilterMode.Point ||
                importer.textureCompression !=
                    TextureImporterCompression.Uncompressed ||
                importer.mipmapEnabled ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                !importer.alphaIsTransparency ||
                !Approximately(frame.rect, spec.FrameRect) ||
                !Approximately(tail.rect, spec.TailRect) ||
                !Approximately(frame.border, spec.FrameBorder) ||
                !Approximately(tail.border, Vector4.zero))
            {
                errors.Add(
                    $"{spec.Theme}: x4 frame/tail import is invalid."
                );
            }
        }

        ThrowIfErrors("Console art import validation failed", errors);
    }

    private static void ValidatePrerequisitesOrThrow(
        Scene scene,
        SkinSpec spec)
    {
        List<string> errors = new List<string>();
        ValidateConsoleFunctionality(scene, spec, errors, requireSkin: false);
        ThrowIfErrors($"{spec.Name}: console art prerequisites failed", errors);
    }

    private static void ValidateSceneOrThrow(Scene scene, SkinSpec spec)
    {
        List<string> errors = new List<string>();
        ValidateConsoleFunctionality(scene, spec, errors, requireSkin: true);
        ValidateNoMissingComponents(scene, spec.Name, errors);
        ValidateBrokenObjectReferences(scene, spec.Name, errors);
        ThrowIfErrors($"{spec.Name}: console art validation failed", errors);
    }

    private static void ValidateConsoleFunctionality(
        Scene scene,
        SkinSpec spec,
        List<string> errors,
        bool requireSkin)
    {
        RuntimeConsoleUI console = TryFindSingleConsole(scene, errors);

        if (console == null)
        {
            return;
        }

        RectTransform consoleRect = console.transform as RectTransform;
        FloatingConsoleWindow window =
            console.GetComponent<FloatingConsoleWindow>();

        if (consoleRect == null || window == null ||
            consoleRect.parent == null ||
            consoleRect.parent.name != "ConsoleWindowLayer")
        {
            errors.Add("RuntimeConsolePanel floating-window setup is invalid.");
            return;
        }

        ValidateWindowReferences(window, consoleRect, errors);
        ValidateRuntimeReferences(console, errors);
        ValidateExistingHandles(consoleRect, window, errors);

        if (!requireSkin)
        {
            return;
        }

        ValidateSkin(consoleRect, spec, errors);
    }

    private static void ValidateWindowReferences(
        FloatingConsoleWindow window,
        RectTransform consoleRect,
        List<string> errors)
    {
        SerializedObject data = new SerializedObject(window);
        SerializedProperty windowRect = data.FindProperty("windowRect");
        SerializedProperty clampArea = data.FindProperty("clampArea");
        SerializedProperty minimumSize = data.FindProperty("minimumSize");

        if (windowRect == null ||
            windowRect.objectReferenceValue != consoleRect ||
            clampArea == null ||
            clampArea.objectReferenceValue != consoleRect.parent ||
            minimumSize == null ||
            !Approximately(minimumSize.vector2Value, new Vector2(420f, 280f)))
        {
            errors.Add("FloatingConsoleWindow references or minimum size changed.");
        }
    }

    private static void ValidateRuntimeReferences(
        RuntimeConsoleUI console,
        List<string> errors)
    {
        SerializedObject data = new SerializedObject(console);

        if (GetReference<TMP_InputField>(data, "codeInput") == null ||
            GetReference<Button>(data, "compileButton") == null ||
            GetReference<TMP_Text>(data, "outputText") == null)
        {
            errors.Add("RuntimeConsoleUI input/output/compile reference is missing.");
        }
    }

    private static void ValidateExistingHandles(
        RectTransform consoleRect,
        FloatingConsoleWindow window,
        List<string> errors)
    {
        RectTransform drag = FindDirectChild(consoleRect, DragHandleName);
        RectTransform resize = FindDirectChild(consoleRect, ResizeHandleName);

        if (drag == null || resize == null ||
            drag.GetComponent<FloatingConsoleDragHandle>() == null ||
            resize.GetComponent<FloatingConsoleResizeHandle>() == null ||
            drag.GetComponent<Image>() == null ||
            resize.GetComponent<Image>() == null)
        {
            errors.Add("Existing drag or resize handle is missing.");
            return;
        }

        ValidateHandleReference(
            drag.GetComponent<FloatingConsoleDragHandle>(),
            window,
            errors
        );
        ValidateHandleReference(
            resize.GetComponent<FloatingConsoleResizeHandle>(),
            window,
            errors
        );
    }

    private static void ValidateHandleReference(
        Component handle,
        FloatingConsoleWindow expectedWindow,
        List<string> errors)
    {
        SerializedProperty windowProperty =
            new SerializedObject(handle).FindProperty("window");

        if (windowProperty == null ||
            windowProperty.objectReferenceValue != expectedWindow)
        {
            errors.Add($"{handle.name}: FloatingConsoleWindow reference changed.");
        }
    }

    private static void ValidateSkin(
        RectTransform consoleRect,
        SkinSpec spec,
        List<string> errors)
    {
        RectTransform skin = FindDirectChild(consoleRect, SkinName);

        if (skin == null || CountDirectChildren(consoleRect, SkinName) != 1 ||
            CountDirectChildren(skin, FrameName) != 1 ||
            CountDirectChildren(skin, TailName) != 1)
        {
            errors.Add("ConsoleSkin/Frame/Tail hierarchy is invalid.");
            return;
        }

        RectTransform frame = FindDirectChild(skin, FrameName);
        RectTransform tail = FindDirectChild(skin, TailName);
        Image frameImage = frame != null ? frame.GetComponent<Image>() : null;
        Image tailImage = tail != null ? tail.GetComponent<Image>() : null;
        Sprite expectedFrame = LoadSubSpriteOrNull(
            spec.TexturePath,
            spec.FrameSpriteName
        );
        Sprite expectedTail = LoadSubSpriteOrNull(
            spec.TexturePath,
            spec.TailSpriteName
        );

        if (frameImage == null || tailImage == null ||
            frameImage.sprite != expectedFrame ||
            frameImage.type != Image.Type.Sliced ||
            frameImage.raycastTarget ||
            tailImage.sprite != expectedTail ||
            tailImage.type != Image.Type.Simple ||
            !tailImage.preserveAspect ||
            tailImage.raycastTarget ||
            !Approximately(tail.sizeDelta, spec.TailRect.size))
        {
            errors.Add($"{spec.Theme}: ConsoleSkin visual setup is invalid.");
        }

        Image background = consoleRect.GetComponent<Image>();
        Outline outline = consoleRect.GetComponent<Outline>();

        if (background == null || background.raycastTarget ||
            background.color.a > Tolerance || outline == null || outline.enabled)
        {
            errors.Add("RuntimeConsolePanel legacy background is still visible.");
        }

        RectTransform drag = FindDirectChild(consoleRect, DragHandleName);
        Image dragImage = drag != null ? drag.GetComponent<Image>() : null;

        if (drag == null || dragImage == null || !dragImage.raycastTarget ||
            dragImage.color.a > Tolerance ||
            !Mathf.Approximately(drag.sizeDelta.y, 48f))
        {
            errors.Add("TitleBarDragArea does not align with the art header.");
        }
    }

    private static RuntimeConsoleUI FindSingleConsoleOrThrow(
        Scene scene,
        string sceneName)
    {
        List<string> errors = new List<string>();
        RuntimeConsoleUI console = TryFindSingleConsole(scene, errors);
        ThrowIfErrors($"{sceneName}: RuntimeConsolePanel lookup failed", errors);
        return console;
    }

    private static RuntimeConsoleUI TryFindSingleConsole(
        Scene scene,
        List<string> errors)
    {
        RuntimeConsoleUI result = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (RuntimeConsoleUI candidate in
                     root.GetComponentsInChildren<RuntimeConsoleUI>(true))
            {
                if (result != null)
                {
                    errors.Add("multiple RuntimeConsoleUI components found.");
                    return null;
                }

                result = candidate;
            }
        }

        if (result == null)
        {
            errors.Add("RuntimeConsoleUI is missing.");
        }

        return result;
    }

    private static T GetReference<T>(
        SerializedObject data,
        string propertyName)
        where T : UnityEngine.Object
    {
        SerializedProperty property = data.FindProperty(propertyName);
        return property == null
            ? null
            : property.objectReferenceValue as T;
    }

    private static RectTransform GetOrCreateUiChild(
        RectTransform parent,
        string name)
    {
        RectTransform child = FindDirectChild(parent, name);

        if (child != null)
        {
            return child;
        }

        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = parent.gameObject.layer;
        RectTransform rect = gameObject.transform as RectTransform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static RectTransform FindDirectChild(
        RectTransform parent,
        string name)
    {
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);

            if (child.name == name)
            {
                return child as RectTransform;
            }
        }

        return null;
    }

    private static int CountDirectChildren(
        RectTransform parent,
        string name)
    {
        int count = 0;

        for (int index = 0; index < parent.childCount; index++)
        {
            if (parent.GetChild(index).name == name)
            {
                count++;
            }
        }

        return count;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject)
        where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component ?? gameObject.AddComponent<T>();
    }

    private static Sprite LoadSubSprite(string texturePath, string spriteName)
    {
        Sprite sprite = LoadSubSpriteOrNull(texturePath, spriteName);

        if (sprite == null)
        {
            throw new InvalidOperationException(
                $"{texturePath}: Sprite '{spriteName}' is missing."
            );
        }

        return sprite;
    }

    private static Sprite LoadSubSpriteOrNull(
        string texturePath,
        string spriteName)
    {
        foreach (UnityEngine.Object asset in
                 AssetDatabase.LoadAllAssetsAtPath(texturePath))
        {
            Sprite sprite = asset as Sprite;

            if (sprite != null && sprite.name == spriteName)
            {
                return sprite;
            }
        }

        return null;
    }

    private static void SetStretchRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        EditorUtility.SetDirty(rect);
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        EditorUtility.SetDirty(rect);
    }

    private static bool Approximately(Vector2 left, Vector2 right)
    {
        return Mathf.Abs(left.x - right.x) <= Tolerance &&
               Mathf.Abs(left.y - right.y) <= Tolerance;
    }

    private static bool Approximately(Rect left, Rect right)
    {
        return Approximately(left.position, right.position) &&
               Approximately(left.size, right.size);
    }

    private static bool Approximately(Vector4 left, Vector4 right)
    {
        return Mathf.Abs(left.x - right.x) <= Tolerance &&
               Mathf.Abs(left.y - right.y) <= Tolerance &&
               Mathf.Abs(left.z - right.z) <= Tolerance &&
               Mathf.Abs(left.w - right.w) <= Tolerance;
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

    private static void LogAssetAnalysis(SkinSpec spec)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            spec.TexturePath
        );
        TextureImporter importer =
            AssetImporter.GetAtPath(spec.TexturePath) as TextureImporter;

        if (texture == null || importer == null)
        {
            Debug.LogError($"{spec.Theme}: console artwork is missing.");
            return;
        }

        AlphaBounds opaque = AnalyzeAlphaBounds(spec.TexturePath);

        Debug.Log(
            "PATCH//BREAK CONSOLE ART ANALYSIS\n" +
            $"theme={spec.Theme}\n" +
            $"texture={spec.TexturePath}\n" +
            $"size={texture.width}x{texture.height}\n" +
            $"opaqueBounds={opaque}\n" +
            $"frameRect={spec.FrameRect}\n" +
            $"frameBorder(L,B,R,T)={spec.FrameBorder}\n" +
            $"tailRect={spec.TailRect}\n" +
            "resizeSafe=true (sliced frame + separate simple tail)\n" +
            $"importMode={importer.spriteImportMode} filter={importer.filterMode} " +
            $"compression={importer.textureCompression} mipmaps={importer.mipmapEnabled}"
        );
    }

    private static AlphaBounds AnalyzeAlphaBounds(string texturePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string fullPath = Path.Combine(projectRoot, texturePath);
        byte[] bytes = File.ReadAllBytes(fullPath);
        Texture2D probe = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        try
        {
            if (!ImageConversion.LoadImage(probe, bytes, false))
            {
                throw new InvalidOperationException(
                    $"{texturePath}: PNG alpha analysis could not load texture."
                );
            }

            int minX = probe.width;
            int minY = probe.height;
            int maxX = -1;
            int maxY = -1;
            Color32[] pixels = probe.GetPixels32();

            for (int y = 0; y < probe.height; y++)
            {
                for (int x = 0; x < probe.width; x++)
                {
                    if (pixels[y * probe.width + x].a <= 16)
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            return maxX < minX
                ? AlphaBounds.Empty
                : new AlphaBounds(minX, minY, maxX, maxY);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(probe);
        }
    }

    private static void AnalyzeConsoleHierarchies()
    {
        SceneSetup[] originalSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (SkinSpec spec in SkinSpecs)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    spec.ScenePath,
                    OpenSceneMode.Single
                );
                List<string> errors = new List<string>();
                RuntimeConsoleUI console = TryFindSingleConsole(scene, errors);

                if (console == null)
                {
                    Debug.LogError(
                        $"{spec.Name}: console hierarchy analysis failed: " +
                        string.Join("; ", errors)
                    );
                    continue;
                }

                RectTransform rect = console.transform as RectTransform;
                FloatingConsoleWindow window =
                    console.GetComponent<FloatingConsoleWindow>();
                Debug.Log(
                    "PATCH//BREAK CONSOLE HIERARCHY\n" +
                    $"scene={spec.Name}\n" +
                    $"root={GetHierarchyPath(console.transform)}\n" +
                    $"size={rect.rect.size}\n" +
                    $"floatingWindow={window != null}\n" +
                    GetHierarchySummary(console.transform)
                );
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

    private static string GetHierarchySummary(Transform root)
    {
        StringBuilder result = new StringBuilder();

        foreach (Transform transform in root.GetComponentsInChildren<Transform>(
                     true
                 ))
        {
            int depth = 0;

            for (Transform parent = transform.parent;
                 parent != null && parent != root.parent;
                 parent = parent.parent)
            {
                depth++;
            }

            result.Append(' ', depth * 2);
            result.Append(transform.name);
            result.Append(" [");
            Component[] components = transform.GetComponents<Component>();

            for (int index = 0; index < components.Length; index++)
            {
                if (index > 0)
                {
                    result.Append(", ");
                }

                result.Append(
                    components[index] == null
                        ? "Missing"
                        : components[index].GetType().Name
                );
            }

            result.AppendLine("]");
        }

        return result.ToString();
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

    private sealed class SkinSpec
    {
        public SkinSpec(
            string scenePath,
            string name,
            string theme,
            string texturePath,
            string frameSpriteName,
            string tailSpriteName,
            Rect frameRect,
            Rect tailRect,
            Vector4 frameBorder)
        {
            ScenePath = scenePath;
            Name = name;
            Theme = theme;
            TexturePath = texturePath;
            FrameSpriteName = frameSpriteName;
            TailSpriteName = tailSpriteName;
            FrameRect = frameRect;
            TailRect = tailRect;
            FrameBorder = frameBorder;
        }

        public string ScenePath { get; }
        public string Name { get; }
        public string Theme { get; }
        public string TexturePath { get; }
        public string FrameSpriteName { get; }
        public string TailSpriteName { get; }
        public Rect FrameRect { get; }
        public Rect TailRect { get; }
        public Vector4 FrameBorder { get; }
    }

    private readonly struct AlphaBounds
    {
        public static AlphaBounds Empty => new AlphaBounds(-1, -1, -1, -1);

        public AlphaBounds(int minX, int minY, int maxX, int maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public int MinX { get; }
        public int MinY { get; }
        public int MaxX { get; }
        public int MaxY { get; }

        public override string ToString()
        {
            return $"X[{MinX},{MaxX}] Y[{MinY},{MaxY}]";
        }
    }
}
