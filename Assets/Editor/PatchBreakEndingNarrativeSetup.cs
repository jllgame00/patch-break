using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the compact TMP ending presentation through the Editor API. Keeping
/// this scene wiring here avoids hand-editing Ending.unity and makes repeated
/// setup runs deterministic.
/// </summary>
public static class PatchBreakEndingNarrativeSetup
{
    private const string MenuRoot = "Tools/PATCH BREAK/Ending/";
    private const string EndingScenePath = "Assets/Scenes/Ending.unity";
    private const string KoreanFontPath =
        "Assets/Resources/NotoSansKRMenu SDF.asset";
    private const string CanvasName = "EndingNarrativeCanvas";
    private const string NarrativeText =
        "[ SYSTEM ]\nDEBUGGER PROCESS TERMINATED.\n\n" +
        "패턴은 분석되었다.\n" +
        "행동은 예측되었다.\n" +
        "프로그램은 완벽하게 읽혔다.\n\n" +
        "하지만—\n\n" +
        "정해진 코드만으로는\n" +
        "끝까지 살아남을 수 없었다.\n\n" +
        "필요했던 것은\n" +
        "더 완벽한 명령이 아니라,\n\n" +
        "상황을 보고,\n" +
        "틀린 코드를 고치고,\n" +
        "다시 선택하는 것이었다.";

    [MenuItem(MenuRoot + "Setup Narrative Ending")]
    public static void SetupNarrativeEnding()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            TMP_FontAsset font = LoadValidatedKoreanFont();
            Scene scene = EditorSceneManager.OpenScene(
                EndingScenePath,
                OpenSceneMode.Single
            );

            EndingScreenController controller = FindSingleController(scene);
            CanvasGroup canvasGroup = SetupCanvas(scene, font,
                out TMP_Text narrative, out TMP_Text prompt);
            LogFontReference("BeforeSave", narrative, font);

            Undo.RecordObject(controller, "Configure narrative ending");
            controller.ConfigureNarrativePresentation(
                narrative,
                prompt,
                canvasGroup
            );
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorSceneManager.CloseScene(scene, true);
            Scene reloadedScene = EditorSceneManager.OpenScene(
                EndingScenePath,
                OpenSceneMode.Single
            );
            TMP_Text reloadedNarrative = FindText(
                reloadedScene,
                "NarrativeText"
            );
            LogFontReference("AfterSave", reloadedNarrative, font);
            ValidateScene(reloadedScene, font);
        }
        finally
        {
            RestorePreviousSceneSetup(previousSetup);
        }

        Debug.Log(
            "PATCH_BREAK_ENDING_NARRATIVE_SETUP_COMPLETE " +
            "(TMP Korean narrative and Input System progression configured)."
        );
    }

    [MenuItem(MenuRoot + "Validate Narrative Ending")]
    public static void ValidateNarrativeEnding()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            TMP_FontAsset font = LoadValidatedKoreanFont();
            Scene scene = EditorSceneManager.OpenScene(
                EndingScenePath,
                OpenSceneMode.Single
            );
            ValidateScene(scene, font);
        }
        finally
        {
            RestorePreviousSceneSetup(previousSetup);
        }

        Debug.Log("PATCH_BREAK_ENDING_NARRATIVE_VALIDATION_COMPLETE");
    }

    private static bool PrepareEditorOperation()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "PATCH//BREAK Ending setup cannot run while Play Mode is active."
            );
            return false;
        }

        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static TMP_FontAsset LoadValidatedKoreanFont()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            KoreanFontPath
        );
        if (font == null || font.sourceFontFile == null)
        {
            throw new InvalidOperationException(
                "Could not load the Korean TMP font at " + KoreanFontPath + "."
            );
        }

        HashSet<char> missingCharacters = new();
        foreach (char character in NarrativeText)
        {
            if (IsHangulSyllable(character) &&
                !font.sourceFontFile.HasCharacter(character))
            {
                missingCharacters.Add(character);
            }
        }

        if (missingCharacters.Count > 0)
        {
            throw new InvalidOperationException(
                "NotoSansKRMenu.ttf is missing Ending glyph(s): " +
                string.Join(", ", missingCharacters.OrderBy(c => c))
            );
        }

        return font;
    }

    private static CanvasGroup SetupCanvas(
        Scene scene,
        TMP_FontAsset font,
        out TMP_Text narrative,
        out TMP_Text prompt
    )
    {
        GameObject canvasObject = GetOrCreateSingleRoot(scene, CanvasName);
        canvasObject.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = GetOrAddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = GetOrAddComponent<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasObject.GetComponent<RectTransform>();
        Stretch(root);

        Image backdrop = GetOrAddComponent<Image>(
            GetOrCreateSingleChild(root, "Backdrop").gameObject
        );
        backdrop.color = Color.black;
        backdrop.raycastTarget = false;
        Stretch(backdrop.rectTransform);

        RectTransform content = GetOrCreateSingleChild(
            root,
            "NarrativeContent"
        );
        Stretch(content);
        CanvasGroup canvasGroup = GetOrAddComponent<CanvasGroup>(
            content.gameObject
        );
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        TextMeshProUGUI narrativeText = GetOrAddComponent<TextMeshProUGUI>(
            GetOrCreateSingleChild(content, "NarrativeText").gameObject
        );
        ConfigureNarrativeText(narrativeText, font);

        TextMeshProUGUI promptText = GetOrAddComponent<TextMeshProUGUI>(
            GetOrCreateSingleChild(content, "PromptText").gameObject
        );
        ConfigurePromptText(promptText, font);

        narrative = narrativeText;
        prompt = promptText;
        return canvasGroup;
    }

    private static void ConfigureNarrativeText(
        TextMeshProUGUI text,
        TMP_FontAsset font
    )
    {
        AssignExactFont(text, font);
        text.color = new Color(0.82f, 0.94f, 1f, 1f);
        text.fontSize = 36f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 22f;
        text.fontSizeMax = 36f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 28f);
        rect.sizeDelta = new Vector2(1280f, 720f);
    }

    private static void ConfigurePromptText(
        TextMeshProUGUI text,
        TMP_FontAsset font
    )
    {
        AssignExactFont(text, font);
        text.text = "PRESS ANY KEY";
        text.color = new Color(0.56f, 0.86f, 1f, 1f);
        text.fontSize = 21f;
        text.enableAutoSizing = false;
        text.alignment = TextAlignmentOptions.Center;
        text.characterSpacing = 3f;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.11f);
        rect.anchorMax = new Vector2(0.5f, 0.11f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(520f, 60f);
    }

    private static void ValidateScene(Scene scene, TMP_FontAsset font)
    {
        EndingScreenController controller = FindSingleController(scene);
        if (!controller.HasPresentationReferences())
        {
            throw new InvalidOperationException(
                "EndingScreenController narrative references are missing."
            );
        }

        GameObject canvasObject = FindSingleRoot(scene, CanvasName);
        Canvas[] canvases = canvasObject.GetComponents<Canvas>();
        if (canvases.Length != 1 ||
            canvases[0].renderMode != RenderMode.ScreenSpaceOverlay)
        {
            throw new InvalidOperationException(
                "Ending narrative canvas must be one Screen Space Overlay Canvas."
            );
        }

        Transform content = FindSingleDirectChild(
            canvasObject.transform,
            "NarrativeContent"
        );
        if (content.GetComponents<CanvasGroup>().Length != 1)
        {
            throw new InvalidOperationException(
                "Ending narrative content must have one CanvasGroup."
            );
        }

        ValidateText(content, "NarrativeText", font);
        ValidateText(content, "PromptText", font);
        ValidateNoMissingComponents(scene);
    }

    private static void ValidateText(
        Transform root,
        string name,
        TMP_FontAsset expectedFont
    )
    {
        Transform child = FindSingleDirectChild(root, name);
        TMP_Text[] targets = child.GetComponents<TMP_Text>();
        if (targets.Length != 1)
        {
            throw new InvalidOperationException(
                name + ": expected one TMP_Text, found " +
                targets.Length + "."
            );
        }

        TMP_Text target = targets[0];
        SerializedObject serializedTarget = new(target);
        SerializedProperty serializedFont = serializedTarget.FindProperty(
            "m_fontAsset"
        );
        TMP_FontAsset assignedFont = target.font;
        TMP_FontAsset serializedAssignedFont = serializedFont != null
            ? serializedFont.objectReferenceValue as TMP_FontAsset
            : null;

        bool sameRuntimeReference = ReferenceEquals(assignedFont, expectedFont) ||
                                    assignedFont == expectedFont;
        bool sameSerializedReference =
            serializedAssignedFont == expectedFont;
        if (!sameRuntimeReference || !sameSerializedReference)
        {
            string actualPath = assignedFont != null
                ? AssetDatabase.GetAssetPath(assignedFont)
                : "<null>";
            string serializedPath = serializedAssignedFont != null
                ? AssetDatabase.GetAssetPath(serializedAssignedFont)
                : "<null>";
            throw new InvalidOperationException(
                name + " font mismatch.\n" +
                "Expected: " + KoreanFontPath + "\n" +
                "Actual: " + actualPath + "\n" +
                "Serialized: " + serializedPath + "\n" +
                "SameReference=" + sameRuntimeReference + "\n" +
                "SameSerializedReference=" + sameSerializedReference
            );
        }
    }

    private static void AssignExactFont(
        TMP_Text text,
        TMP_FontAsset expectedFont
    )
    {
        if (text == null || expectedFont == null)
        {
            throw new InvalidOperationException(
                "Cannot assign a null Ending TMP text or font asset."
            );
        }

        // TMP components can retain a previous default font material while
        // their scene object already exists. Set the public API values, then
        // pin the serialized references to the exact asset as a second step.
        text.font = expectedFont;
        text.fontSharedMaterial = expectedFont.material;

        SerializedObject serializedText = new(text);
        SerializedProperty fontProperty = serializedText.FindProperty(
            "m_fontAsset"
        );
        SerializedProperty materialProperty = serializedText.FindProperty(
            "m_sharedMaterial"
        );
        if (fontProperty == null || materialProperty == null)
        {
            throw new InvalidOperationException(
                text.name + ": TMP serialized font properties are missing."
            );
        }

        fontProperty.objectReferenceValue = expectedFont;
        materialProperty.objectReferenceValue = expectedFont.material;
        serializedText.ApplyModifiedPropertiesWithoutUndo();
        text.SetAllDirty();
        EditorUtility.SetDirty(text);
    }

    private static void LogFontReference(
        string phase,
        TMP_Text narrative,
        TMP_FontAsset expectedFont
    )
    {
        TMP_FontAsset assignedFont = narrative != null
            ? narrative.font
            : null;
        Debug.Log(
            "[ENDING_FONT] " +
            "Phase=" + phase + " " +
            "ExpectedPath=" + KoreanFontPath + " " +
            "ExpectedName=" +
            (expectedFont != null ? expectedFont.name : "<null>") + " " +
            "ExpectedInstanceID=" +
            (expectedFont != null ? expectedFont.GetInstanceID() : 0) + " " +
            "NarrativeFont=" +
            (assignedFont != null ? assignedFont.name : "<null>") + " " +
            "NarrativeInstanceID=" +
            (assignedFont != null ? assignedFont.GetInstanceID() : 0) + " " +
            "SameReference=" +
            (assignedFont == expectedFont)
        );
    }

    private static TMP_Text FindText(Scene scene, string name)
    {
        GameObject canvasObject = FindSingleRoot(scene, CanvasName);
        Transform content = FindSingleDirectChild(
            canvasObject.transform,
            "NarrativeContent"
        );
        Transform textTransform = FindSingleDirectChild(content, name);
        TMP_Text[] targets = textTransform.GetComponents<TMP_Text>();
        if (targets.Length != 1)
        {
            throw new InvalidOperationException(
                name + ": expected one TMP_Text, found " + targets.Length + "."
            );
        }

        return targets[0];
    }

    private static EndingScreenController FindSingleController(Scene scene)
    {
        EndingScreenController[] controllers = scene.GetRootGameObjects()
            .SelectMany(root =>
                root.GetComponentsInChildren<EndingScreenController>(true)
            )
            .ToArray();
        if (controllers.Length != 1)
        {
            throw new InvalidOperationException(
                "Ending: expected one EndingScreenController, found " +
                controllers.Length + "."
            );
        }

        return controllers[0];
    }

    private static GameObject GetOrCreateSingleRoot(Scene scene, string name)
    {
        GameObject[] matches = scene.GetRootGameObjects()
            .Where(root => root.name == name)
            .ToArray();
        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                "Ending: duplicate root named " + name + "."
            );
        }

        if (matches.Length == 1)
        {
            return matches[0];
        }

        GameObject created = new(name, typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(created, scene);
        return created;
    }

    private static GameObject FindSingleRoot(Scene scene, string name)
    {
        GameObject[] matches = scene.GetRootGameObjects()
            .Where(root => root.name == name)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "Ending: expected one root named " + name + "."
            );
        }

        return matches[0];
    }

    private static RectTransform GetOrCreateSingleChild(
        Transform parent,
        string name
    )
    {
        List<Transform> matches = new();
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child.name == name)
            {
                matches.Add(child);
            }
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                parent.name + ": duplicate child named " + name + "."
            );
        }

        if (matches.Count == 1)
        {
            return matches[0] as RectTransform;
        }

        GameObject created = new(name, typeof(RectTransform));
        RectTransform rect = created.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        created.layer = LayerMask.NameToLayer("UI");
        return rect;
    }

    private static Transform FindSingleDirectChild(Transform parent, string name)
    {
        List<Transform> matches = new();
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child.name == name)
            {
                matches.Add(child);
            }
        }

        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                parent.name + ": expected one child named " + name + "."
            );
        }

        return matches[0];
    }

    private static T GetOrAddComponent<T>(GameObject gameObject)
        where T : Component
    {
        T[] components = gameObject.GetComponents<T>();
        if (components.Length > 1)
        {
            throw new InvalidOperationException(
                gameObject.name + ": duplicate " + typeof(T).Name +
                " components found."
            );
        }

        return components.Length == 1
            ? components[0]
            : gameObject.AddComponent<T>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static bool IsHangulSyllable(char character)
    {
        return character >= '\uAC00' && character <= '\uD7A3';
    }

    private static void ValidateNoMissingComponents(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in
                root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    transform.gameObject
                ) > 0)
                {
                    throw new InvalidOperationException(
                        transform.name + " has a missing MonoBehaviour."
                    );
                }
            }
        }
    }

    private static void RestorePreviousSceneSetup(SceneSetup[] previousSetup)
    {
        if (previousSetup != null && previousSetup.Length > 0)
        {
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }
}
