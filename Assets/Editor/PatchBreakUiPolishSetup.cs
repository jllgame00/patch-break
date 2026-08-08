using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Applies the compact terminal typography treatment to the existing floating
/// console. This intentionally changes only visual UI data: TMP settings,
/// button presentation, and short action-label wrapping. Runtime console
/// behaviour, button events, window drag/resize, and the console skin remain
/// owned by their existing systems.
/// </summary>
public static class PatchBreakUiPolishSetup
{
    private const string MenuRoot = "Tools/PATCH BREAK/UI Polish/";
    private const string ConsoleTitleName = "ConsoleTitle";
    private const string TitleBarName = "TitleBarDragArea";
    private const string ResizeHandleName = "ResizeHandle";
    private const float Tolerance = 0.01f;
    private const float DefaultConsoleWidth = 520f;
    private const float MinimumConsoleWidth = 420f;
    private const float InputLayoutSafetyMargin = 8f;

    // These are the ordinary program lines that must remain comfortably
    // readable in the floating console. Longer player-authored source may
    // soft-wrap normally inside TMP_InputField's viewport.
    private static readonly string[] CanonicalDslLines =
    {
        "if enemy.near => slash",
        "if enemy.far => approach",
        "if enemy.attacking => dash.back",
        "if enemy.guarding => dash.forward"
    };

    private static readonly SceneSpec[] SceneSpecs =
    {
        new SceneSpec(
            "Assets/Scenes/Battle.unity",
            "Battle",
            new Color32(13, 14, 26, 255),
            new Color32(62, 240, 122, 255)
        ),
        new SceneSpec(
            "Assets/Scenes/KnightBattle.unity",
            "KnightBattle",
            new Color32(13, 14, 26, 255),
            new Color32(166, 176, 214, 255)
        ),
        new SceneSpec(
            "Assets/Scenes/DebuggerBattle.unity",
            "DebuggerBattle",
            new Color32(13, 14, 26, 255),
            new Color32(255, 43, 61, 255)
        )
    };

    [MenuItem(MenuRoot + "Analyze Typography")]
    public static void AnalyzeTypography()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        SceneSetup[] originalSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (SceneSpec spec in SceneSpecs)
            {
                Scene scene = OpenScene(spec);
                LogTypographyAudit(scene, spec);
            }
        }
        finally
        {
            RestoreSceneSetup(originalSetup);
        }

        Debug.Log("PATCH_BREAK_UI_POLISH_TYPOGRAPHY_AUDIT_COMPLETE");
    }

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

        SceneSetup[] originalSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (SceneSpec spec in SceneSpecs)
            {
                ValidateSceneOrThrow(OpenScene(spec), spec);
            }
        }
        finally
        {
            RestoreSceneSetup(originalSetup);
        }

        Debug.Log("PATCH_BREAK_UI_POLISH_VALIDATION_COMPLETE");
    }

    private static void SetupScenes(IEnumerable<SceneSpec> specs)
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        List<SceneSpec> targets = new List<SceneSpec>(specs);
        SceneSetup[] originalSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            // Check every target before saving any scene. This prevents a bad
            // All-Scenes run from leaving later scenes unapplied.
            foreach (SceneSpec spec in targets)
            {
                ValidatePrerequisitesOrThrow(OpenScene(spec), spec);
            }

            foreach (SceneSpec spec in targets)
            {
                Scene scene = OpenScene(spec);
                ConfigureScene(scene, spec);
                ValidateSceneOrThrow(scene, spec);

                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        $"{spec.Name}: scene could not be saved."
                    );
                }

                ValidateSceneOrThrow(OpenScene(spec), spec);
            }
        }
        finally
        {
            RestoreSceneSetup(originalSetup);
        }

        Debug.Log("PATCH_BREAK_UI_POLISH_SETUP_COMPLETE");
    }

    private static bool PrepareEditorOperation()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "PATCH//BREAK UI polish cannot run while Play Mode is active."
            );
            return false;
        }

        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static Scene OpenScene(SceneSpec spec)
    {
        return EditorSceneManager.OpenScene(
            spec.ScenePath,
            OpenSceneMode.Single
        );
    }

    private static void RestoreSceneSetup(SceneSetup[] originalSetup)
    {
        if (originalSetup != null && originalSetup.Length > 0)
        {
            EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }
    }

    private static void ConfigureScene(Scene scene, SceneSpec spec)
    {
        RuntimeConsoleUI runtimeConsole = FindSingleComponent<RuntimeConsoleUI>(
            scene,
            spec
        );
        ConsoleReferences references = GetConsoleReferences(runtimeConsole);
        int persistentClickCount =
            references.CompileButton.onClick.GetPersistentEventCount();

        ConfigureConsoleTypography(references);
        ConfigureCompileButton(references, spec);
        ConfigureBriefingTypography(scene, spec);
        ConfigureShortStageActionLabels(scene, runtimeConsole.transform);

        if (references.CompileButton.onClick.GetPersistentEventCount() !=
            persistentClickCount)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: Compile button persistent OnClick listeners " +
                "were unexpectedly modified."
            );
        }

        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ConfigureConsoleTypography(ConsoleReferences references)
    {
        ConfigureText(
            references.Title,
            15f,
            TextWrappingModes.NoWrap,
            TextOverflowModes.Ellipsis,
            0f
        );
        references.Title.raycastTarget = false;

        // TMP_InputField owns its text component's wrap mode. MultiLineNewline
        // intentionally retains TMP's Normal wrapping so explicit Enter
        // newlines work through the standard input-field implementation.
        ConfigureCommandInputField(references.CodeInput, 14f);

        // Normal wrapping is intentional for long user-authored input. The
        // layout validator below instead proves that the canonical DSL lines
        // fit on one visual line at both supported console widths.
        ConfigureText(
            references.InputText,
            14f,
            TextWrappingModes.Normal,
            TextOverflowModes.Overflow,
            -1f
        );

        if (references.Placeholder != null)
        {
            ConfigureText(
                references.Placeholder,
                12f,
                TextWrappingModes.NoWrap,
                TextOverflowModes.Ellipsis,
                -1f
            );
            references.Placeholder.raycastTarget = false;
        }

        ConfigureText(
            references.Output,
            12f,
            TextWrappingModes.Normal,
            TextOverflowModes.Overflow,
            -1f
        );
        references.Output.raycastTarget = false;

        ConfigureText(
            references.CompileLabel,
            13f,
            TextWrappingModes.NoWrap,
            TextOverflowModes.Ellipsis,
            0f
        );
        references.CompileLabel.alignment =
            TextAlignmentOptions.Center;
        references.CompileLabel.raycastTarget = false;

        RectTransform labelRect = references.CompileLabel.rectTransform;
        Undo.RecordObject(labelRect, "Polish Compile Button Label Padding");
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.offsetMin = new Vector2(12f, 4f);
        labelRect.offsetMax = new Vector2(-12f, -4f);
        EditorUtility.SetDirty(labelRect);
    }

    private static void ConfigureText(
        TMP_Text text,
        float fontSize,
        TextWrappingModes wrapping,
        TextOverflowModes overflow,
        float lineSpacing)
    {
        Undo.RecordObject(text, "Polish PATCH BREAK Typography");
        text.enableAutoSizing = false;
        text.fontSize = fontSize;
        text.textWrappingMode = wrapping;
        text.overflowMode = overflow;
        text.lineSpacing = lineSpacing;
        EditorUtility.SetDirty(text);
    }

    private static void ConfigureCommandInputField(
        TMP_InputField input,
        float pointSize)
    {
        Undo.RecordObject(input, "Configure Console Command Input");

        // This retains explicit Enter newlines. TMP_InputField deliberately
        // sets multiline text components to Normal wrapping as part of its
        // own validation lifecycle, so no conflicting wrap-mode override is
        // applied here.
        input.lineType = TMP_InputField.LineType.MultiLineNewline;

        // Use TMP_InputField's API rather than writing m_GlobalPointSize
        // through SerializedObject. It updates input and placeholder text
        // consistently with the component's own validation rules.
        input.pointSize = pointSize;
        EditorUtility.SetDirty(input);
    }

    private static void ConfigureCompileButton(
        ConsoleReferences references,
        SceneSpec spec)
    {
        RectTransform buttonRect =
            references.CompileButton.transform as RectTransform;
        Image buttonImage = references.CompileButton.targetGraphic as Image;

        if (buttonRect == null || buttonImage == null)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: Compile button requires a RectTransform Image " +
                "target graphic."
            );
        }

        // The button stays clear of the fixed 24px resize hit area and keeps
        // a low terminal-control profile at both default and minimum sizes.
        Undo.RecordObject(buttonRect, "Polish Compile Button Layout");
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0f);
        buttonRect.anchoredPosition = new Vector2(-48f, 12f);
        buttonRect.sizeDelta = new Vector2(170f, 32f);
        EditorUtility.SetDirty(buttonRect);

        Undo.RecordObject(buttonImage, "Style Compile Terminal Button");
        buttonImage.color = spec.ButtonBody;
        buttonImage.raycastTarget = true;
        EditorUtility.SetDirty(buttonImage);

        Outline outline = references.CompileButton.GetComponent<Outline>();
        if (outline == null)
        {
            outline = Undo.AddComponent<Outline>(
                references.CompileButton.gameObject
            );
        }

        Undo.RecordObject(outline, "Style Compile Terminal Button Border");
        outline.effectColor = new Color(
            spec.Accent.r,
            spec.Accent.g,
            spec.Accent.b,
            0.9f
        );
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
        EditorUtility.SetDirty(outline);

        Undo.RecordObject(
            references.CompileButton,
            "Style Compile Terminal Button States"
        );
        Button.ButtonClickedEvent unused = references.CompileButton.onClick;
        ColorBlock colors = references.CompileButton.colors;
        colors.normalColor = spec.ButtonBody;
        colors.highlightedColor = Color.Lerp(
            spec.ButtonBody,
            spec.Accent,
            0.16f
        );
        colors.pressedColor = Color.Lerp(
            spec.ButtonBody,
            spec.Accent,
            0.3f
        );
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(
            spec.ButtonBody.r,
            spec.ButtonBody.g,
            spec.ButtonBody.b,
            0.45f
        );
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        references.CompileButton.transition = Selectable.Transition.ColorTint;
        references.CompileButton.colors = colors;
        EditorUtility.SetDirty(references.CompileButton);

        // Explicitly retain the existing UnityEvent. The local assignment also
        // makes accidental event replacement conspicuous during review.
        if (unused == null)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: Compile button OnClick event is missing."
            );
        }

        Undo.RecordObject(
            references.CompileLabel,
            "Style Compile Terminal Button Label"
        );
        references.CompileLabel.color = spec.Accent;
        EditorUtility.SetDirty(references.CompileLabel);
    }

    private static void ConfigureShortStageActionLabels(
        Scene scene,
        Transform consoleRoot)
    {
        foreach (Button button in FindComponents<Button>(scene))
        {
            if (button.transform.IsChildOf(consoleRoot))
            {
                continue;
            }

            TMP_Text label = FindButtonLabel(button);
            if (label == null || !IsShortActionButton(button, label))
            {
                continue;
            }

            ConfigureText(
                label,
                GetFittingActionLabelSize(label),
                TextWrappingModes.NoWrap,
                TextOverflowModes.Ellipsis,
                0f
            );
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            EditorUtility.SetDirty(label);
        }
    }

    private static void ConfigureBriefingTypography(
        Scene scene,
        SceneSpec spec)
    {
        // The briefing card retains a clear title, while description, rules,
        // and controls become supporting information instead of competing with
        // the scene art or the terminal window.
        ConfigureNamedText(
            scene,
            spec,
            "BriefingTitle",
            30f,
            TextWrappingModes.NoWrap,
            TextOverflowModes.Ellipsis
        );
        ConfigureNamedText(
            scene,
            spec,
            "DescriptionText",
            24f,
            TextWrappingModes.Normal,
            TextOverflowModes.Overflow
        );
        ConfigureNamedText(
            scene,
            spec,
            "RulesText",
            22f,
            TextWrappingModes.Normal,
            TextOverflowModes.Overflow
        );
        ConfigureNamedText(
            scene,
            spec,
            "ControlsText",
            20f,
            TextWrappingModes.Normal,
            TextOverflowModes.Overflow
        );
    }

    private static void ConfigureNamedText(
        Scene scene,
        SceneSpec spec,
        string objectName,
        float fontSize,
        TextWrappingModes wrapping,
        TextOverflowModes overflow)
    {
        TMP_Text text = FindSingleTextByName(scene, objectName, spec);
        ConfigureText(text, fontSize, wrapping, overflow, 0f);
        text.raycastTarget = false;
        EditorUtility.SetDirty(text);
    }

    private static float GetFittingActionLabelSize(TMP_Text label)
    {
        const float targetSize = 22f;
        const float minimumSize = 16f;
        float availableWidth = label.rectTransform.rect.width;

        if (availableWidth <= 0f || string.IsNullOrWhiteSpace(label.text))
        {
            return targetSize;
        }

        float preferredWidth = label.GetPreferredValues(
            label.text,
            0f,
            0f
        ).x;
        if (preferredWidth <= availableWidth || preferredWidth <= 0f)
        {
            return targetSize;
        }

        return Mathf.Clamp(
            targetSize * (availableWidth / preferredWidth),
            minimumSize,
            targetSize
        );
    }

    private static TMP_Text FindButtonLabel(Button button)
    {
        TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
        return labels.Length == 0 ? null : labels[0];
    }

    private static TMP_Text FindSingleTextByName(
        Scene scene,
        string objectName,
        SceneSpec spec)
    {
        TMP_Text result = null;

        foreach (TMP_Text text in FindComponents<TMP_Text>(scene))
        {
            if (text.gameObject.name != objectName)
            {
                continue;
            }

            if (result != null)
            {
                throw new InvalidOperationException(
                    $"{spec.Name}: expected one {objectName}, found more than one."
                );
            }

            result = text;
        }

        if (result == null)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: missing {objectName}."
            );
        }

        return result;
    }

    private static bool IsShortActionButton(Button button, TMP_Text label)
    {
        string name = button.name.ToUpperInvariant();
        string text = (label.text ?? string.Empty).ToUpperInvariant();

        return ContainsActionKeyword(name) || ContainsActionKeyword(text);
    }

    private static bool ContainsActionKeyword(string value)
    {
        return value.Contains("START") ||
               value.Contains("MISSION") ||
               value.Contains("RETRY") ||
               value.Contains("RESTART") ||
               value.Contains("NEXT") ||
               value.Contains("LIVE PATCH") ||
               value.Contains("Q OVERRIDE");
    }

    private static void ValidatePrerequisitesOrThrow(
        Scene scene,
        SceneSpec spec)
    {
        List<string> errors = new List<string>();
        ValidateNoMissingComponents(scene, spec.Name, errors);
        ValidateBrokenObjectReferences(scene, spec.Name, errors);

        try
        {
            RuntimeConsoleUI runtimeConsole =
                FindSingleComponent<RuntimeConsoleUI>(scene, spec);
            ConsoleReferences references = GetConsoleReferences(runtimeConsole);
            ValidateWindowReferences(runtimeConsole, errors);

            if (references.CompileButton.targetGraphic as Image == null)
            {
                errors.Add("Compile button target graphic is not an Image");
            }
        }
        catch (InvalidOperationException exception)
        {
            errors.Add(exception.Message);
        }

        ThrowIfErrors(spec, errors, "prerequisite validation");
    }

    private static void ValidateSceneOrThrow(Scene scene, SceneSpec spec)
    {
        List<string> errors = new List<string>();
        ValidateNoMissingComponents(scene, spec.Name, errors);
        ValidateBrokenObjectReferences(scene, spec.Name, errors);

        try
        {
            RuntimeConsoleUI runtimeConsole =
                FindSingleComponent<RuntimeConsoleUI>(scene, spec);
            ConsoleReferences references = GetConsoleReferences(runtimeConsole);
            ValidateWindowReferences(runtimeConsole, errors);
            ValidateConsoleTypography(references, spec, errors);
            ValidateBriefingTypography(scene, spec, errors);
            ValidateShortActionLabels(scene, runtimeConsole.transform, errors);
        }
        catch (InvalidOperationException exception)
        {
            errors.Add(exception.Message);
        }

        ThrowIfErrors(spec, errors, "UI polish validation");
        Debug.Log($"{spec.Name}: PATCH//BREAK UI polish is valid.");
    }

    private static void ValidateConsoleTypography(
        ConsoleReferences references,
        SceneSpec spec,
        ICollection<string> errors)
    {
        ValidateText(
            references.Title,
            "Console title",
            15f,
            TextWrappingModes.NoWrap,
            errors
        );
        ValidateText(
            references.InputText,
            "Console input",
            14f,
            TextWrappingModes.Normal,
            errors
        );
        ValidateCanonicalDslLayout(references, spec, errors);
        ValidateText(
            references.Output,
            "Console output",
            12f,
            TextWrappingModes.Normal,
            errors
        );
        ValidateText(
            references.CompileLabel,
            "Compile label",
            13f,
            TextWrappingModes.NoWrap,
            errors
        );

        if (references.Placeholder != null)
        {
            ValidateText(
                references.Placeholder,
                "Console placeholder",
                12f,
                TextWrappingModes.NoWrap,
                errors
            );
        }

        RectTransform buttonRect =
            references.CompileButton.transform as RectTransform;
        if (buttonRect == null ||
            !Approximately(buttonRect.sizeDelta, new Vector2(170f, 32f)))
        {
            errors.Add("Compile button is not the expected 170x32 terminal control");
        }

        Image buttonImage = references.CompileButton.targetGraphic as Image;
        if (buttonImage == null || !Approximately(buttonImage.color, spec.ButtonBody))
        {
            errors.Add("Compile button body does not match the console skin");
        }

        Outline outline = references.CompileButton.GetComponent<Outline>();
        if (outline == null ||
            !Approximately(outline.effectColor, new Color(
                spec.Accent.r,
                spec.Accent.g,
                spec.Accent.b,
                0.9f
            )))
        {
            errors.Add("Compile button is missing the themed terminal border");
        }

        if (!Approximately(references.CompileLabel.color, spec.Accent))
        {
            errors.Add("Compile label does not match the console skin accent");
        }

        if (references.CodeInput.lineType !=
            TMP_InputField.LineType.MultiLineNewline)
        {
            errors.Add("Console input must retain MultiLineNewline behaviour");
        }

        SerializedProperty globalPointSize = new SerializedObject(
            references.CodeInput
        ).FindProperty("m_GlobalPointSize");
        if (globalPointSize == null ||
            !Approximately(globalPointSize.floatValue, 14f))
        {
            errors.Add("Console input global point size is not 14");
        }
    }

    private static void ValidateText(
        TMP_Text text,
        string description,
        float expectedFontSize,
        TextWrappingModes expectedWrapping,
        ICollection<string> errors)
    {
        if (text.enableAutoSizing)
        {
            errors.Add($"{description}: Auto Size must be disabled");
        }

        if (!Approximately(text.fontSize, expectedFontSize))
        {
            errors.Add(
                $"{description}: expected font size {expectedFontSize}, " +
                $"found {text.fontSize}"
            );
        }

        if (text.textWrappingMode != expectedWrapping)
        {
            errors.Add(
                $"{description}: unexpected wrapping mode " +
                $"{text.textWrappingMode}"
            );
        }
    }

    private static void ValidateCanonicalDslLayout(
        ConsoleReferences references,
        SceneSpec spec,
        ICollection<string> errors)
    {
        RectTransform consoleRect = references.CodeInput.transform.parent as
            RectTransform;
        RectTransform inputRect = references.CodeInput.transform as
            RectTransform;
        RectTransform viewport = references.CodeInput.textViewport;

        if (consoleRect == null || inputRect == null || viewport == null)
        {
            errors.Add("Console input is missing a RectTransform or viewport");
            return;
        }

        if (inputRect.parent != consoleRect || viewport.parent != inputRect)
        {
            errors.Add(
                "Console input layout hierarchy changed; cannot measure " +
                "the canonical DSL viewport"
            );
            return;
        }

        float defaultUsableWidth = CalculateInputUsableWidth(
            consoleRect,
            inputRect,
            viewport,
            DefaultConsoleWidth
        );
        float minimumUsableWidth = CalculateInputUsableWidth(
            consoleRect,
            inputRect,
            viewport,
            MinimumConsoleWidth
        );

        float longestPreferredWidth = 0f;
        string longestLine = string.Empty;
        foreach (string line in CanonicalDslLines)
        {
            float preferredWidth = references.InputText.GetPreferredValues(
                line,
                0f,
                0f
            ).x;

            if (preferredWidth > longestPreferredWidth)
            {
                longestPreferredWidth = preferredWidth;
                longestLine = line;
            }
        }

        Debug.Log(
            $"{spec.Name}: Console DSL layout default={defaultUsableWidth:0.##}" +
            $"px minimum={minimumUsableWidth:0.##}px " +
            $"longest={longestPreferredWidth:0.##}px " +
            $"line=\"{longestLine}\""
        );

        if (longestPreferredWidth > defaultUsableWidth)
        {
            errors.Add(
                "Console input canonical DSL does not fit at 520x360 " +
                $"(needs {longestPreferredWidth:0.##}px, has " +
                $"{defaultUsableWidth:0.##}px)"
            );
        }

        if (longestPreferredWidth > minimumUsableWidth)
        {
            errors.Add(
                "Console input canonical DSL does not fit at 420x280 " +
                $"(needs {longestPreferredWidth:0.##}px, has " +
                $"{minimumUsableWidth:0.##}px)"
            );
        }
    }

    private static float CalculateInputUsableWidth(
        RectTransform consoleRect,
        RectTransform inputRect,
        RectTransform viewport,
        float consoleWidth)
    {
        float inputWidth = CalculateAnchoredWidth(
            consoleRect.rect.width,
            consoleWidth,
            inputRect
        );
        float viewportWidth = CalculateAnchoredWidth(
            inputRect.rect.width,
            inputWidth,
            viewport
        );

        // TMP lays out against the text/viewport RectTransform itself. The
        // RectMask2D only clips rendering and its negative padding does not
        // reduce the width TMP uses to decide a soft wrap. Reserve a small
        // visual breathing margin without changing the existing layout.
        return Mathf.Max(0f, viewportWidth - InputLayoutSafetyMargin);
    }

    private static float CalculateAnchoredWidth(
        float currentParentWidth,
        float requestedParentWidth,
        RectTransform child)
    {
        float fixedWidth = child.rect.width -
            currentParentWidth * (child.anchorMax.x - child.anchorMin.x);
        return requestedParentWidth *
            (child.anchorMax.x - child.anchorMin.x) + fixedWidth;
    }

    private static void ValidateShortActionLabels(
        Scene scene,
        Transform consoleRoot,
        ICollection<string> errors)
    {
        foreach (Button button in FindComponents<Button>(scene))
        {
            if (button.transform.IsChildOf(consoleRoot))
            {
                continue;
            }

            TMP_Text label = FindButtonLabel(button);
            if (label != null && IsShortActionButton(button, label) &&
                label.textWrappingMode != TextWrappingModes.NoWrap)
            {
                errors.Add(
                    $"{button.name}: short action label is allowed to wrap"
                );
            }
        }
    }

    private static void ValidateBriefingTypography(
        Scene scene,
        SceneSpec spec,
        ICollection<string> errors)
    {
        ValidateNamedText(
            scene,
            spec,
            "BriefingTitle",
            30f,
            TextWrappingModes.NoWrap,
            errors
        );
        ValidateNamedText(
            scene,
            spec,
            "DescriptionText",
            24f,
            TextWrappingModes.Normal,
            errors
        );
        ValidateNamedText(
            scene,
            spec,
            "RulesText",
            22f,
            TextWrappingModes.Normal,
            errors
        );
        ValidateNamedText(
            scene,
            spec,
            "ControlsText",
            20f,
            TextWrappingModes.Normal,
            errors
        );
    }

    private static void ValidateNamedText(
        Scene scene,
        SceneSpec spec,
        string objectName,
        float expectedFontSize,
        TextWrappingModes expectedWrapping,
        ICollection<string> errors)
    {
        try
        {
            ValidateText(
                FindSingleTextByName(scene, objectName, spec),
                objectName,
                expectedFontSize,
                expectedWrapping,
                errors
            );
        }
        catch (InvalidOperationException exception)
        {
            errors.Add(exception.Message);
        }
    }

    private static void ValidateWindowReferences(
        RuntimeConsoleUI runtimeConsole,
        ICollection<string> errors)
    {
        RectTransform consoleRect = runtimeConsole.transform as RectTransform;
        FloatingConsoleWindow window =
            runtimeConsole.GetComponent<FloatingConsoleWindow>();

        if (consoleRect == null || window == null)
        {
            errors.Add("RuntimeConsolePanel is missing FloatingConsoleWindow");
            return;
        }

        SerializedObject windowData = new SerializedObject(window);
        ValidateReference(windowData, "windowRect", errors);
        ValidateReference(windowData, "clampArea", errors);
        ValidateReference(windowData, "frontLayer", errors);

        SerializedProperty minimumSize = windowData.FindProperty("minimumSize");
        if (minimumSize == null ||
            !Approximately(minimumSize.vector2Value, new Vector2(420f, 280f)))
        {
            errors.Add("FloatingConsoleWindow minimum size was changed");
        }

        FloatingConsoleDragHandle dragHandle = FindDirectChild(
            consoleRect,
            TitleBarName
        )?.GetComponent<FloatingConsoleDragHandle>();
        FloatingConsoleResizeHandle resizeHandle = FindDirectChild(
            consoleRect,
            ResizeHandleName
        )?.GetComponent<FloatingConsoleResizeHandle>();

        if (dragHandle == null || resizeHandle == null)
        {
            errors.Add("Console drag or resize handle is missing");
            return;
        }

        ValidateReference(new SerializedObject(dragHandle), "window", errors);
        ValidateReference(new SerializedObject(resizeHandle), "window", errors);
    }

    private static void ValidateReference(
        SerializedObject data,
        string propertyName,
        ICollection<string> errors)
    {
        SerializedProperty property = data.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == null)
        {
            errors.Add($"missing console reference: {propertyName}");
        }
    }

    private static ConsoleReferences GetConsoleReferences(
        RuntimeConsoleUI runtimeConsole)
    {
        SerializedObject data = new SerializedObject(runtimeConsole);
        TMP_InputField codeInput = GetReference<TMP_InputField>(
            data,
            "codeInput"
        );
        Button compileButton = GetReference<Button>(data, "compileButton");
        TMP_Text output = GetReference<TMP_Text>(data, "outputText");
        TMP_Text title = runtimeConsole.transform.Find(ConsoleTitleName)
            ?.GetComponent<TMP_Text>();
        TMP_Text compileLabel = FindButtonLabel(compileButton);
        TMP_Text inputText = codeInput.textComponent;
        TMP_Text placeholder = codeInput.placeholder as TMP_Text;

        if (title == null || inputText == null || compileLabel == null)
        {
            throw new InvalidOperationException(
                "RuntimeConsoleUI is missing title, input text, or compile label."
            );
        }

        return new ConsoleReferences(
            title,
            codeInput,
            inputText,
            placeholder,
            output,
            compileButton,
            compileLabel
        );
    }

    private static T GetReference<T>(SerializedObject data, string propertyName)
        where T : UnityEngine.Object
    {
        SerializedProperty property = data.FindProperty(propertyName);
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

    private static void LogTypographyAudit(Scene scene, SceneSpec spec)
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("PATCH//BREAK TYPOGRAPHY AUDIT");
        report.AppendLine($"scene={spec.Name}");

        foreach (TMP_Text text in FindComponents<TMP_Text>(scene))
        {
            RectTransform rect = text.rectTransform;
            report.AppendLine(
                $"TMP path={GetHierarchyPath(text.transform)} " +
                $"font={text.font?.name ?? "<missing>"} " +
                $"size={text.fontSize:0.##} auto={text.enableAutoSizing} " +
                $"min={text.fontSizeMin:0.##} max={text.fontSizeMax:0.##} " +
                $"wrap={text.textWrappingMode} overflow={text.overflowMode} " +
                $"rect={rect.rect.width:0.##}x{rect.rect.height:0.##} " +
                $"text=\"{FormatAuditText(text.text)}\""
            );
        }

        foreach (TMP_InputField input in FindComponents<TMP_InputField>(scene))
        {
            report.AppendLine(
                $"Input path={GetHierarchyPath(input.transform)} " +
                $"lineType={input.lineType} " +
                $"text={GetHierarchyPath(input.textComponent?.transform)} " +
                $"placeholder={GetHierarchyPath((input.placeholder as Component)?.transform)}"
            );
        }

        foreach (Button button in FindComponents<Button>(scene))
        {
            TMP_Text label = FindButtonLabel(button);
            report.AppendLine(
                $"Button path={GetHierarchyPath(button.transform)} " +
                $"label={GetHierarchyPath(label?.transform)} " +
                $"persistentOnClick={button.onClick.GetPersistentEventCount()} " +
                $"transition={button.transition}"
            );
        }

        Debug.Log(report.ToString());
    }

    private static string FormatAuditText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string formatted = value.Replace("\n", "\\n");
        return formatted.Length <= 96
            ? formatted
            : formatted.Substring(0, 93) + "...";
    }

    private static T FindSingleComponent<T>(Scene scene, SceneSpec spec)
        where T : Component
    {
        List<T> components = new List<T>(FindComponents<T>(scene));
        if (components.Count != 1)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: expected one {typeof(T).Name}, found " +
                $"{components.Count}."
            );
        }

        return components[0];
    }

    private static IEnumerable<T> FindComponents<T>(Scene scene)
        where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                yield return component;
            }
        }
    }

    private static RectTransform FindDirectChild(
        RectTransform parent,
        string childName)
    {
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child.name == childName)
            {
                return child as RectTransform;
            }
        }

        return null;
    }

    private static void ValidateNoMissingComponents(
        Scene scene,
        string sceneName,
        ICollection<string> errors)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in
                     root.GetComponentsInChildren<Transform>(true))
            {
                foreach (Component component in
                         transform.GetComponents<Component>())
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
        ICollection<string> errors)
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

    private static void ThrowIfErrors(
        SceneSpec spec,
        List<string> errors,
        string context)
    {
        if (errors.Count == 0)
        {
            return;
        }

        string message = $"{spec.Name} {context} failed:\n- " +
                         string.Join("\n- ", errors);
        Debug.LogError(message);
        throw new InvalidOperationException(message);
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return "<none>";
        }

        StringBuilder path = new StringBuilder(transform.name);
        Transform parent = transform.parent;

        while (parent != null)
        {
            path.Insert(0, parent.name + "/");
            parent = parent.parent;
        }

        return path.ToString();
    }

    private static bool Approximately(float left, float right)
    {
        return Mathf.Abs(left - right) <= Tolerance;
    }

    private static bool Approximately(Vector2 left, Vector2 right)
    {
        return Approximately(left.x, right.x) &&
               Approximately(left.y, right.y);
    }

    private static bool Approximately(Color left, Color right)
    {
        return Approximately(left.r, right.r) &&
               Approximately(left.g, right.g) &&
               Approximately(left.b, right.b) &&
               Approximately(left.a, right.a);
    }

    private readonly struct SceneSpec
    {
        public SceneSpec(
            string scenePath,
            string name,
            Color buttonBody,
            Color accent)
        {
            ScenePath = scenePath;
            Name = name;
            ButtonBody = buttonBody;
            Accent = accent;
        }

        public string ScenePath { get; }
        public string Name { get; }
        public Color ButtonBody { get; }
        public Color Accent { get; }
    }

    private readonly struct ConsoleReferences
    {
        public ConsoleReferences(
            TMP_Text title,
            TMP_InputField codeInput,
            TMP_Text inputText,
            TMP_Text placeholder,
            TMP_Text output,
            Button compileButton,
            TMP_Text compileLabel)
        {
            Title = title;
            CodeInput = codeInput;
            InputText = inputText;
            Placeholder = placeholder;
            Output = output;
            CompileButton = compileButton;
            CompileLabel = compileLabel;
        }

        public TMP_Text Title { get; }
        public TMP_InputField CodeInput { get; }
        public TMP_Text InputText { get; }
        public TMP_Text Placeholder { get; }
        public TMP_Text Output { get; }
        public Button CompileButton { get; }
        public TMP_Text CompileLabel { get; }
    }
}
