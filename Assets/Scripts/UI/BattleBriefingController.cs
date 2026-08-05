using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BattleBriefingController : MonoBehaviour
{
    [Header("Briefing UI")]
    [SerializeField] private GameObject briefingRoot;
    [SerializeField] private TMP_Text missionLabel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text rulesText;
    [SerializeField] private TMP_Text controlsText;
    [SerializeField] private Button startButton;
    [SerializeField] private TMP_Text startButtonLabel;

    [Header("Briefing Copy")]
    [SerializeField] private string missionText;
    [SerializeField] private string titleCopy;
    [SerializeField, TextArea] private string descriptionCopy;
    [SerializeField, TextArea] private string rulesCopy;
    [SerializeField, TextArea] private string controlsCopy;
    [SerializeField] private string startButtonText = "START MISSION";

    [Header("Existing Runtime UI")]
    [SerializeField] private GameObject runtimeConsoleRoot;
    [SerializeField] private RuntimeConsoleUI runtimeConsoleUI;

    [Header("Knight Override Copy")]
    [SerializeField] private HeroOverrideController overrideController;
    [SerializeField] private TMP_Text overrideUsesText;

    [Header("Localization")]
    [SerializeField] private TMP_FontAsset koreanFontAsset;

    [Header("Diagnostics")]
    [SerializeField] private bool enableBriefingTextDiagnostics;
    [SerializeField] private bool enableKoreanFontDiagnostics;

    private bool hasStartedMission;
    private Coroutine diagnosticsCoroutine;

    private void Awake()
    {
        if (!HasRequiredReferences())
        {
            Debug.LogError(
                "BattleBriefingController: Required briefing UI " +
                "reference is missing."
            );

            enabled = false;
            return;
        }

        ApplyBriefingCopy();
        ApplyTextPresentation();
        ApplyKoreanFont();
        briefingRoot.SetActive(true);
        runtimeConsoleUI.SetEditorInputLocked(true);
        RefreshOverrideUsesText();
        startButton.onClick.AddListener(StartMission);

        if (enableBriefingTextDiagnostics)
        {
            LogBriefingTextStates("AWAKE_AFTER_SETUP");
        }
    }

    private void Start()
    {
        SelectStartButton();

        if (enableBriefingTextDiagnostics)
        {
            diagnosticsCoroutine = StartCoroutine(
                LogBriefingTextStatesNextFrame()
            );
        }
    }

    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartMission);
        }

        if (diagnosticsCoroutine != null)
        {
            StopCoroutine(diagnosticsCoroutine);
        }
    }

    private bool HasRequiredReferences()
    {
        return briefingRoot != null &&
               missionLabel != null &&
               titleText != null &&
               descriptionText != null &&
               rulesText != null &&
               controlsText != null &&
               startButton != null &&
               startButtonLabel != null &&
               runtimeConsoleRoot != null &&
               runtimeConsoleUI != null;
    }

    private void ApplyBriefingCopy()
    {
        missionLabel.text = missionText;
        titleText.text = titleCopy;
        descriptionText.text = descriptionCopy;
        rulesText.text = rulesCopy;
        controlsText.text = controlsCopy;
        startButtonLabel.text = startButtonText;
    }

    private void ApplyTextPresentation()
    {
        ConfigureText(
            missionLabel,
            22f,
            TextAlignmentOptions.Center
        );
        ConfigureText(
            titleText,
            30f,
            TextAlignmentOptions.Center
        );
        ConfigureText(
            descriptionText,
            18f,
            TextAlignmentOptions.TopLeft
        );
        ConfigureText(
            rulesText,
            16f,
            TextAlignmentOptions.TopLeft
        );
        ConfigureText(
            controlsText,
            15f,
            TextAlignmentOptions.TopLeft
        );
        ConfigureText(
            startButtonLabel,
            22f,
            TextAlignmentOptions.Center
        );

        if (overrideUsesText != null)
        {
            ConfigureText(
                overrideUsesText,
                16f,
                TextAlignmentOptions.Center
            );
        }
    }

    private static void ConfigureText(
        TMP_Text target,
        float fontSize,
        TextAlignmentOptions alignment
    )
    {
        target.fontSize = fontSize;
        target.enableAutoSizing = false;
        target.overflowMode = TextOverflowModes.Overflow;
        target.alignment = alignment;
    }

    private void ApplyKoreanFont()
    {
        if (koreanFontAsset == null)
        {
            Debug.LogError(
                "BattleBriefingController: Korean TMP Font Asset reference " +
                "is missing. The briefing remains available, but Korean " +
                "characters cannot render correctly.",
                this
            );
            return;
        }

        TMP_Text[] koreanTextTargets =
        {
            missionLabel,
            titleText,
            descriptionText,
            rulesText,
            controlsText
        };

        foreach (TMP_Text target in koreanTextTargets)
        {
            target.font = koreanFontAsset;
            target.fontSharedMaterial = koreanFontAsset.material;
            target.SetAllDirty();
        }

        if (enableKoreanFontDiagnostics)
        {
            RunKoreanFontDiagnostics();
        }
    }

    private void RunKoreanFontDiagnostics()
    {
        LogKoreanFontAssetState();
        LogKoreanFontTargets("APPLIED_BEFORE_GLYPH_CHECK");

        string hangulCharacters = CollectUniqueHangulCharacters();
        Font sourceFontFile = koreanFontAsset.sourceFontFile;

        Debug.Log(
            $"KOREAN GLYPH DIAGNOSTICS collectedCount=" +
            $"{hangulCharacters.Length}",
            this
        );

        foreach (char character in hangulCharacters)
        {
            Debug.Log(
                $"KOREAN GLYPH character='{character}' " +
                $"unicode=U+{(int)character:X4} " +
                $"sourceFontHasCharacter=" +
                $"{(sourceFontFile != null && sourceFontFile.HasCharacter(character))} " +
                $"tmpFontHasCharacter=" +
                $"{koreanFontAsset.HasCharacter(character, false, false)}",
                this
            );
        }

        LogKoreanFontTargets("AFTER_GLYPH_CHECK");
    }

    private string CollectUniqueHangulCharacters()
    {
        HashSet<char> uniqueCharacters = new HashSet<char>();
        TMP_Text[] allTextTargets =
        {
            missionLabel,
            titleText,
            descriptionText,
            rulesText,
            controlsText,
            startButtonLabel,
            overrideUsesText
        };

        foreach (TMP_Text target in allTextTargets)
        {
            if (target == null || string.IsNullOrEmpty(target.text))
            {
                continue;
            }

            foreach (char character in target.text)
            {
                if (character >= '\uAC00' && character <= '\uD7A3')
                {
                    uniqueCharacters.Add(character);
                }
            }
        }

        StringBuilder result = new StringBuilder(uniqueCharacters.Count);

        foreach (char character in uniqueCharacters)
        {
            result.Append(character);
        }

        return result.ToString();
    }

    private void LogKoreanFontAssetState()
    {
        Font sourceFontFile = koreanFontAsset.sourceFontFile;
        Material material = koreanFontAsset.material;
        Texture mainTexture = material != null ? material.mainTexture : null;

        Debug.Log(
            $"KOREAN FONT ASSET STATE\n" +
            $"fieldIsNull={koreanFontAsset == null}\n" +
            $"fontAssetName={koreanFontAsset.name}\n" +
            $"fontAssetInstanceID={koreanFontAsset.GetInstanceID()}\n" +
            $"sourceFontFileName={sourceFontFile?.name}\n" +
            $"sourceFontFileIsNull={sourceFontFile == null}\n" +
            $"atlasPopulationMode={koreanFontAsset.atlasPopulationMode}\n" +
            $"atlasTextureCount={koreanFontAsset.atlasTextures.Length}\n" +
            $"materialName={material?.name}\n" +
            $"materialShader={material?.shader?.name}\n" +
            $"materialMainTexture={mainTexture?.name}\n" +
            $"characterTableCount={koreanFontAsset.characterTable.Count}\n" +
            $"glyphTableCount={koreanFontAsset.glyphTable.Count}\n" +
            $"fallbackCount={koreanFontAsset.fallbackFontAssetTable.Count}",
            this
        );
    }

    private void LogKoreanFontTargets(string phase)
    {
        TMP_Text[] allTargets =
        {
            missionLabel,
            titleText,
            descriptionText,
            rulesText,
            controlsText,
            startButtonLabel
        };

        foreach (TMP_Text target in allTargets)
        {
            if (target == null)
            {
                Debug.LogWarning(
                    $"KOREAN FONT TARGET STATE phase={phase} target=<missing>",
                    this
                );
                continue;
            }

            target.ForceMeshUpdate(true, true);
            Material material = target.fontSharedMaterial;
            Texture mainTexture = material != null ? material.mainTexture : null;

            Debug.Log(
                $"KOREAN FONT TARGET STATE phase={phase}\n" +
                $"targetName={target.name}\n" +
                $"text='{target.text}'\n" +
                $"textLength={target.text.Length}\n" +
                $"targetFontName={target.font?.name}\n" +
                $"targetFontInstanceID={target.font?.GetInstanceID()}\n" +
                $"targetFontEqualsControllerFont=" +
                $"{target.font == koreanFontAsset}\n" +
                $"targetMaterialName={material?.name}\n" +
                $"targetMaterialShader={material?.shader?.name}\n" +
                $"targetMaterialMainTexture={mainTexture?.name}\n" +
                $"activeInHierarchy={target.gameObject.activeInHierarchy}\n" +
                $"componentEnabled={target.enabled}\n" +
                $"colorAlpha={target.color.a}\n" +
                $"characterCountAfterForceMeshUpdate=" +
                $"{target.textInfo.characterCount}",
                target
            );
        }
    }

    private IEnumerator LogBriefingTextStatesNextFrame()
    {
        yield return null;
        LogBriefingTextStates("START_NEXT_FRAME");
        diagnosticsCoroutine = null;
    }

    private void LogBriefingTextStates(string phase)
    {
        LogBriefingContainerState(phase);

        LogBriefingTextState(missionLabel, phase);
        LogBriefingTextState(titleText, phase);
        LogBriefingTextState(descriptionText, phase);
        LogBriefingTextState(rulesText, phase);
        LogBriefingTextState(controlsText, phase);
        LogBriefingTextState(startButtonLabel, phase);
    }

    private void LogBriefingContainerState(string phase)
    {
        Canvas canvas = briefingRoot.GetComponentInParent<Canvas>();
        StringBuilder groups = new StringBuilder();
        CanvasGroup[] canvasGroups = briefingRoot.GetComponentsInParent<CanvasGroup>(
            true
        );

        foreach (CanvasGroup group in canvasGroups)
        {
            if (groups.Length > 0)
            {
                groups.Append("; ");
            }

            groups.Append(group.name)
                .Append("(alpha=")
                .Append(group.alpha)
                .Append(", interactable=")
                .Append(group.interactable)
                .Append(", blocksRaycasts=")
                .Append(group.blocksRaycasts)
                .Append(')');
        }

        RectTransform rootRect = briefingRoot.GetComponent<RectTransform>();
        RectTransform cardRect = missionLabel.rectTransform.parent as RectTransform;
        GameObject cardObject = cardRect != null ? cardRect.gameObject : null;
        Image rootImage = briefingRoot.GetComponent<Image>();
        Image cardImage = cardObject != null
            ? cardObject.GetComponent<Image>()
            : null;
        Mask[] masks = briefingRoot.GetComponentsInChildren<Mask>(true);
        RectMask2D[] rectMasks = briefingRoot.GetComponentsInChildren<RectMask2D>(
            true
        );

        Debug.Log(
            $"BRIEFING CONTAINER STATE phase={phase}\n" +
            $"rootActive={briefingRoot.activeInHierarchy} " +
            $"rootImageMaskable={rootImage?.maskable}\n" +
            $"rootRect={FormatRect(rootRect)}\n" +
            $"cardActive={cardObject?.activeInHierarchy} " +
            $"cardImageMaskable={cardImage?.maskable}\n" +
            $"cardRect={FormatRect(cardRect)}\n" +
            $"startButtonActive={startButton.gameObject.activeInHierarchy} " +
            $"startButtonInteractable={startButton.interactable} " +
            $"startButtonRect={FormatRect(startButton.GetComponent<RectTransform>())}\n" +
            $"canvas={canvas?.name} sortingOrder={canvas?.sortingOrder} " +
            $"overrideSorting={canvas?.overrideSorting}\n" +
            $"canvasGroups={groups}\n" +
            $"maskCount={masks.Length} rectMaskCount={rectMasks.Length}",
            this
        );
    }

    private void LogBriefingTextState(TMP_Text target, string phase)
    {
        if (target == null)
        {
            Debug.LogWarning(
                $"BRIEFING TEXT STATE phase={phase} target=<missing>",
                this
            );
            return;
        }

        target.SetAllDirty();
        target.ForceMeshUpdate(true, true);

        CanvasRenderer renderer = target.canvasRenderer;
        int vertexCount = 0;

        foreach (TMP_MeshInfo meshInfo in target.textInfo.meshInfo)
        {
            if (meshInfo.vertices != null)
            {
                vertexCount += meshInfo.vertices.Length;
            }
        }

        Debug.Log(
            $"BRIEFING TEXT STATE phase={phase}\n" +
            $"name={target.name}\n" +
            $"activeSelf={target.gameObject.activeSelf}\n" +
            $"activeInHierarchy={target.gameObject.activeInHierarchy}\n" +
            $"componentEnabled={target.enabled}\n" +
            $"text='{target.text}'\n" +
            $"textLength={target.text.Length}\n" +
            $"font={target.font?.name}\n" +
            $"fontMaterial={target.fontMaterial?.name}\n" +
            $"fontSharedMaterial={target.fontSharedMaterial?.name}\n" +
            $"color={target.color} alpha={target.color.a}\n" +
            $"fontSize={target.fontSize} autoSizing={target.enableAutoSizing} " +
            $"overflow={target.overflowMode}\n" +
            $"maskable={target.maskable} raycastTarget={target.raycastTarget}\n" +
            $"rect={FormatRect(target.rectTransform)}\n" +
            $"canvasRendererCull={renderer.cull} " +
            $"canvasRendererAlpha={renderer.GetAlpha()}\n" +
            $"characterCount={target.textInfo.characterCount} " +
            $"meshCount={target.textInfo.meshInfo.Length} vertexCount={vertexCount}\n" +
            $"preferredWidth={target.preferredWidth} " +
            $"preferredHeight={target.preferredHeight} " +
            $"isTextOverflowing={target.isTextOverflowing}",
            target
        );
    }

    private static string FormatRect(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return "<missing>";
        }

        Rect rect = rectTransform.rect;
        return
            $"anchorMin={rectTransform.anchorMin} " +
            $"anchorMax={rectTransform.anchorMax} " +
            $"anchoredPosition={rectTransform.anchoredPosition} " +
            $"sizeDelta={rectTransform.sizeDelta} " +
            $"rect=({rect.width}, {rect.height}) " +
            $"localScale={rectTransform.localScale}";
    }

    private void RefreshOverrideUsesText()
    {
        if (overrideUsesText == null)
        {
            return;
        }

        if (overrideController == null)
        {
            overrideUsesText.gameObject.SetActive(false);
            return;
        }

        overrideUsesText.text =
            $"Q OVERRIDE: {overrideController.MaxCharges} USES";
        overrideUsesText.gameObject.SetActive(true);
    }

    private void StartMission()
    {
        if (hasStartedMission)
        {
            return;
        }

        hasStartedMission = true;
        startButton.interactable = false;
        runtimeConsoleUI.SetEditorInputLocked(false);
        briefingRoot.SetActive(false);

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void SelectStartButton()
    {
        if (hasStartedMission ||
            EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(
            startButton.gameObject
        );
    }
}
