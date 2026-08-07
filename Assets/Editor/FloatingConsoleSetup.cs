using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FloatingConsoleSetup
{
    private const string MenuRoot = "Tools/PATCH BREAK/Floating Console/";
    private const string LayerName = "ConsoleWindowLayer";
    private const string TitleBarName = "TitleBarDragArea";
    private const string ResizeHandleName = "ResizeHandle";

    private static readonly SceneSpec[] SceneSpecs =
    {
        new SceneSpec("Assets/Scenes/Battle.unity", "Battle"),
        new SceneSpec("Assets/Scenes/KnightBattle.unity", "KnightBattle"),
        new SceneSpec("Assets/Scenes/DebuggerBattle.unity", "DebuggerBattle")
    };

    [MenuItem(MenuRoot + "Setup Battle First")]
    public static void SetupBattleFirst()
    {
        SetupScenes(new[] { SceneSpecs[0] });
    }

    [MenuItem(MenuRoot + "Setup All Scenes")]
    public static void SetupAllScenes()
    {
        SetupScenes(SceneSpecs);
    }

    [MenuItem(MenuRoot + "Validate All Scenes")]
    public static void ValidateAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        foreach (SceneSpec spec in SceneSpecs)
        {
            Scene scene = OpenScene(spec);
            ValidateScene(scene, spec);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
            ValidateScene(OpenScene(spec), spec);
        }

        Debug.Log("Floating Console validation completed.");
    }

    private static void SetupScenes(IEnumerable<SceneSpec> sceneSpecs)
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        foreach (SceneSpec spec in sceneSpecs)
        {
            Scene scene = OpenScene(spec);
            SetupScene(scene, spec);
            ValidateScene(scene, spec);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
            ValidateScene(OpenScene(spec), spec);
        }

        Debug.Log("Floating Console setup completed.");
    }

    private static bool PrepareEditorOperation()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "Floating Console setup cannot run while Play Mode is active."
            );
            return false;
        }

        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static Scene OpenScene(SceneSpec spec)
    {
        return EditorSceneManager.OpenScene(spec.Path, OpenSceneMode.Single);
    }

    private static void SetupScene(Scene scene, SceneSpec spec)
    {
        ThrowIfMissingScripts(scene, spec);

        RuntimeConsoleUI runtimeConsole = FindSingleComponent<RuntimeConsoleUI>(
            scene,
            spec
        );
        RectTransform consoleRect = runtimeConsole.transform as RectTransform;
        Canvas canvas = runtimeConsole.GetComponentInParent<Canvas>();

        if (consoleRect == null || canvas == null)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: RuntimeConsolePanel is not under a Canvas."
            );
        }

        RectTransform canvasRect = canvas.transform as RectTransform;
        RectTransform consoleLayer = EnsureConsoleLayer(
            canvasRect,
            consoleRect
        );
        ConfigureWindowRect(consoleRect);
        ConfigureWindowVisuals(runtimeConsole.gameObject);

        FloatingConsoleWindow floatingWindow =
            GetOrAddComponent<FloatingConsoleWindow>(
                runtimeConsole.gameObject,
                "Add Floating Console Window"
            );
        Undo.RecordObject(
            floatingWindow,
            "Configure Floating Console Window"
        );
        floatingWindow.Configure(consoleLayer, consoleLayer);
        EditorUtility.SetDirty(floatingWindow);

        ConsoleReferences references = GetConsoleReferences(runtimeConsole);
        ConfigureExistingContent(references);
        CreateOrConfigureTitleBar(
            consoleRect,
            floatingWindow
        );
        CreateOrConfigureResizeHandle(
            consoleRect,
            floatingWindow
        );
    }

    private static RectTransform EnsureConsoleLayer(
        RectTransform canvasRect,
        RectTransform consoleRect
    )
    {
        RectTransform layer = FindDirectChild(canvasRect, LayerName);

        if (layer == null)
        {
            int consoleSiblingIndex = consoleRect.GetSiblingIndex();
            layer = CreateUiObject(LayerName, canvasRect);
            layer.SetSiblingIndex(consoleSiblingIndex);
        }

        SetStretchRect(layer);

        if (consoleRect.parent != layer)
        {
            Undo.SetTransformParent(
                consoleRect,
                layer,
                "Move Console Into Floating Window Layer"
            );
        }

        consoleRect.SetAsLastSibling();
        return layer;
    }

    private static void ConfigureWindowRect(RectTransform consoleRect)
    {
        Undo.RecordObject(consoleRect, "Configure Floating Console Rect");
        consoleRect.anchorMin = Vector2.one;
        consoleRect.anchorMax = Vector2.one;
        consoleRect.pivot = new Vector2(0f, 1f);
        consoleRect.anchoredPosition = new Vector2(-548f, -60f);
        consoleRect.sizeDelta = new Vector2(520f, 360f);
    }

    private static void ConfigureWindowVisuals(GameObject consoleObject)
    {
        Image background = GetOrAddComponent<Image>(
            consoleObject,
            "Add Floating Console Background"
        );
        Undo.RecordObject(background, "Style Floating Console Background");
        background.color = new Color(0.012f, 0.028f, 0.055f, 0.96f);
        background.raycastTarget = false;

        Outline outline = GetOrAddComponent<Outline>(
            consoleObject,
            "Add Floating Console Border"
        );
        Undo.RecordObject(outline, "Style Floating Console Border");
        outline.effectColor = new Color(0.1f, 0.95f, 0.88f, 0.72f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
    }

    private static void ConfigureExistingContent(ConsoleReferences references)
    {
        RectTransform titleRect = references.Title.transform as RectTransform;
        RectTransform codeInputRect =
            references.CodeInput.transform as RectTransform;
        RectTransform outputRect = references.Output.transform as RectTransform;
        RectTransform compileRect =
            references.CompileButton.transform as RectTransform;

        if (titleRect == null ||
            codeInputRect == null ||
            outputRect == null ||
            compileRect == null)
        {
            throw new InvalidOperationException(
                "Runtime Console UI is missing a required RectTransform."
            );
        }

        SetRect(
            titleRect,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(16f, -9f),
            new Vector2(-110f, 28f)
        );
        Undo.RecordObject(references.Title, "Configure Console Title Raycast");
        references.Title.raycastTarget = false;

        SetRect(
            codeInputRect,
            new Vector2(0f, 0.5f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -52f),
            new Vector2(-32f, -60f)
        );

        SetRect(
            outputRect,
            Vector2.zero,
            new Vector2(1f, 0.48f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 58f),
            new Vector2(-32f, -8f)
        );
        Undo.RecordObject(references.Output, "Configure Console Output Raycast");
        references.Output.raycastTarget = false;

        SetRect(
            compileRect,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-48f, 12f),
            new Vector2(190f, 38f)
        );
    }

    private static void CreateOrConfigureTitleBar(
        RectTransform consoleRect,
        FloatingConsoleWindow floatingWindow
    )
    {
        RectTransform titleBar = GetOrCreateUiChild(consoleRect, TitleBarName);
        SetRect(
            titleBar,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            new Vector2(0f, 42f)
        );

        Image image = GetOrAddComponent<Image>(
            titleBar.gameObject,
            "Add Console Title Bar"
        );
        Undo.RecordObject(image, "Style Console Title Bar");
        image.color = new Color(0.025f, 0.13f, 0.19f, 0.98f);
        image.raycastTarget = true;

        FloatingConsoleDragHandle dragHandle =
            GetOrAddComponent<FloatingConsoleDragHandle>(
                titleBar.gameObject,
                "Add Console Drag Handle"
            );
        Undo.RecordObject(dragHandle, "Configure Console Drag Handle");
        dragHandle.Configure(floatingWindow);
        EditorUtility.SetDirty(dragHandle);
        titleBar.SetSiblingIndex(0);
    }

    private static void CreateOrConfigureResizeHandle(
        RectTransform consoleRect,
        FloatingConsoleWindow floatingWindow
    )
    {
        RectTransform resizeHandle = GetOrCreateUiChild(
            consoleRect,
            ResizeHandleName
        );
        SetRect(
            resizeHandle,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-4f, 4f),
            new Vector2(24f, 24f)
        );

        Image image = GetOrAddComponent<Image>(
            resizeHandle.gameObject,
            "Add Console Resize Handle"
        );
        Undo.RecordObject(image, "Style Console Resize Handle");
        image.color = new Color(0.1f, 0.95f, 0.88f, 0.7f);
        image.raycastTarget = true;

        FloatingConsoleResizeHandle resizeComponent =
            GetOrAddComponent<FloatingConsoleResizeHandle>(
                resizeHandle.gameObject,
                "Add Console Resize Handle Behaviour"
            );
        Undo.RecordObject(
            resizeComponent,
            "Configure Console Resize Handle"
        );
        resizeComponent.Configure(floatingWindow);
        EditorUtility.SetDirty(resizeComponent);
        resizeHandle.SetAsLastSibling();
    }

    private static ConsoleReferences GetConsoleReferences(
        RuntimeConsoleUI runtimeConsole
    )
    {
        SerializedObject properties = new SerializedObject(runtimeConsole);
        TMP_InputField codeInput = GetReference<TMP_InputField>(
            properties,
            "codeInput"
        );
        Button compileButton = GetReference<Button>(
            properties,
            "compileButton"
        );
        TMP_Text output = GetReference<TMP_Text>(properties, "outputText");
        TMP_Text title = runtimeConsole.transform.Find("ConsoleTitle")
            ?.GetComponent<TMP_Text>();

        if (title == null)
        {
            throw new InvalidOperationException(
                "Runtime Console UI is missing ConsoleTitle."
            );
        }

        return new ConsoleReferences(title, codeInput, output, compileButton);
    }

    private static T GetReference<T>(
        SerializedObject properties,
        string propertyName
    )
        where T : UnityEngine.Object
    {
        SerializedProperty property = properties.FindProperty(propertyName);
        T reference = property == null
            ? null
            : property.objectReferenceValue as T;

        if (reference == null)
        {
            throw new InvalidOperationException(
                $"RuntimeConsoleUI is missing {propertyName}."
            );
        }

        return reference;
    }

    private static RectTransform GetOrCreateUiChild(
        RectTransform parent,
        string name
    )
    {
        RectTransform child = FindDirectChild(parent, name);
        return child == null ? CreateUiObject(name, parent) : child;
    }

    private static RectTransform FindDirectChild(
        RectTransform parent,
        string name
    )
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

    private static RectTransform CreateUiObject(
        string name,
        RectTransform parent
    )
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = parent.gameObject.layer;
        Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");
        RectTransform rectTransform = gameObject.transform as RectTransform;
        rectTransform.SetParent(parent, false);
        return rectTransform;
    }

    private static T GetOrAddComponent<T>(
        GameObject gameObject,
        string undoName
    )
        where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component ?? Undo.AddComponent<T>(gameObject);
    }

    private static void SetStretchRect(RectTransform rectTransform)
    {
        Undo.RecordObject(rectTransform, "Stretch Console Window Layer");
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
    }

    private static void SetRect(
        RectTransform rectTransform,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta
    )
    {
        Undo.RecordObject(rectTransform, "Configure Floating Console Layout");
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;
    }

    private static void ValidateScene(Scene scene, SceneSpec spec)
    {
        List<string> errors = new List<string>();
        CollectMissingScriptErrors(scene, errors);

        RuntimeConsoleUI[] consoles = FindComponents<RuntimeConsoleUI>(scene)
            .ToArray();
        if (consoles.Length != 1)
        {
            errors.Add(
                $"expected one RuntimeConsoleUI, found {consoles.Length}"
            );
        }
        else
        {
            ValidateConsole(consoles[0], errors);
        }

        if (errors.Count > 0)
        {
            string message = $"{spec.Name} Floating Console validation failed:\n- " +
                             string.Join("\n- ", errors);
            Debug.LogError(message);
            throw new InvalidOperationException(message);
        }

        Debug.Log($"{spec.Name}: Floating Console serialization is valid.");
    }

    private static void ValidateConsole(
        RuntimeConsoleUI runtimeConsole,
        ICollection<string> errors
    )
    {
        RectTransform consoleRect = runtimeConsole.transform as RectTransform;
        if (consoleRect == null || consoleRect.parent == null ||
            consoleRect.parent.name != LayerName)
        {
            errors.Add("RuntimeConsolePanel is not inside ConsoleWindowLayer");
            return;
        }

        FloatingConsoleWindow window =
            consoleRect.GetComponent<FloatingConsoleWindow>();
        if (window == null)
        {
            errors.Add("missing FloatingConsoleWindow");
        }
        else
        {
            ValidateObjectReference(
                new SerializedObject(window),
                "windowRect",
                errors
            );
            ValidateObjectReference(
                new SerializedObject(window),
                "clampArea",
                errors
            );
            ValidateObjectReference(
                new SerializedObject(window),
                "frontLayer",
                errors
            );
        }

        FloatingConsoleDragHandle dragHandle =
            FindDirectChild(consoleRect, TitleBarName)
                ?.GetComponent<FloatingConsoleDragHandle>();
        if (dragHandle == null)
        {
            errors.Add("missing TitleBarDragArea or drag handler");
        }
        else
        {
            ValidateObjectReference(
                new SerializedObject(dragHandle),
                "window",
                errors
            );
        }

        FloatingConsoleResizeHandle resizeHandle =
            FindDirectChild(consoleRect, ResizeHandleName)
                ?.GetComponent<FloatingConsoleResizeHandle>();
        if (resizeHandle == null)
        {
            errors.Add("missing ResizeHandle or resize handler");
        }
        else
        {
            ValidateObjectReference(
                new SerializedObject(resizeHandle),
                "window",
                errors
            );
        }

        try
        {
            GetConsoleReferences(runtimeConsole);
        }
        catch (InvalidOperationException exception)
        {
            errors.Add(exception.Message);
        }
    }

    private static void ThrowIfMissingScripts(Scene scene, SceneSpec spec)
    {
        List<string> errors = new List<string>();
        CollectMissingScriptErrors(scene, errors);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: {string.Join(", ", errors)}"
            );
        }
    }

    private static void ValidateObjectReference(
        SerializedObject properties,
        string propertyName,
        ICollection<string> errors
    )
    {
        SerializedProperty property = properties.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == null)
        {
            errors.Add($"missing Floating Console reference: {propertyName}");
        }
    }

    private static void CollectMissingScriptErrors(
        Scene scene,
        ICollection<string> errors
    )
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(
                         true
                     ))
            {
                int missingCount =
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        transform.gameObject
                    );
                if (missingCount > 0)
                {
                    errors.Add(
                        $"{transform.name} has {missingCount} missing script(s)"
                    );
                }
            }
        }
    }

    private static T FindSingleComponent<T>(Scene scene, SceneSpec spec)
        where T : Component
    {
        T[] components = FindComponents<T>(scene).ToArray();
        if (components.Length != 1)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: expected one {typeof(T).Name}, found " +
                $"{components.Length}."
            );
        }

        return components[0];
    }

    private static IEnumerable<T> FindComponents<T>(Scene scene)
        where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(
            root => root.GetComponentsInChildren<T>(true)
        );
    }

    private readonly struct ConsoleReferences
    {
        public ConsoleReferences(
            TMP_Text title,
            TMP_InputField codeInput,
            TMP_Text output,
            Button compileButton
        )
        {
            Title = title;
            CodeInput = codeInput;
            Output = output;
            CompileButton = compileButton;
        }

        public TMP_Text Title { get; }
        public TMP_InputField CodeInput { get; }
        public TMP_Text Output { get; }
        public Button CompileButton { get; }
    }

    private readonly struct SceneSpec
    {
        public SceneSpec(string path, string name)
        {
            Path = path;
            Name = name;
        }

        public string Path { get; }
        public string Name { get; }
    }
}
