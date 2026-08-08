using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BattleBriefingController : MonoBehaviour
{
    private const float BubbleWidth = 500f;
    private const float BubbleHeight = 260f;
    private const float BubbleBodyLeftRightPadding = 18f;
    private const float BubbleBodyTopPadding = 46f;
    private const float BubbleFooterHeight = 50f;
    private const float BubblePageHeightTolerance = 0.5f;

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

    [Header("Presentation")]
    [SerializeField] private bool useBubbleBriefing = true;
    [SerializeField] private bool beginBriefingOnAwake = true;

    private bool hasStartedMission;
    private bool isBriefingVisible;
    private bool bubblePresentationActive;
    private int currentPageIndex;
    private int lastAdvanceFrame = -1;
    private Coroutine diagnosticsCoroutine;
    private readonly List<BriefingPage> briefingPages = new();

    private GameObject bubbleRoot;
    private TMP_Text speakerNameText;
    private TMP_Text messageText;
    private TMP_Text nextIndicatorText;
    private TMP_Text continueButtonText;
    private Button bubbleBackgroundButton;
    private Button continueButton;

    public bool UsesBubbleBriefing => useBubbleBriefing;
    public bool IsBriefingVisible => isBriefingVisible;

    public event Action BriefingFinished;

    private sealed class BriefingPage
    {
        public string Speaker { get; }
        public string Message { get; }

        public BriefingPage(string speaker, string message)
        {
            Speaker = speaker;
            Message = message;
        }
    }

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
        runtimeConsoleUI.SetEditorInputLocked(true);
        RefreshOverrideUsesText();

        bubblePresentationActive =
            useBubbleBriefing &&
            TryCreateBubbleBriefing();

        if (bubblePresentationActive)
        {
            BuildBriefingPages();
            bubbleRoot.SetActive(false);
        }
        else
        {
            startButton.onClick.AddListener(StartMission);
        }

        briefingRoot.SetActive(false);

        if (beginBriefingOnAwake)
        {
            BeginBriefing();
        }

        if (enableBriefingTextDiagnostics)
        {
            LogBriefingTextStates("AWAKE_AFTER_SETUP");
        }
    }

    private void Start()
    {
        if (!bubblePresentationActive &&
            isBriefingVisible)
        {
            SelectStartButton();
        }

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

        if (bubbleBackgroundButton != null)
        {
            bubbleBackgroundButton.onClick.RemoveListener(
                AdvanceBriefing
            );
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(AdvanceBriefing);
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

    private void Update()
    {
        if (!bubblePresentationActive ||
            !isBriefingVisible ||
            hasStartedMission ||
            bubbleRoot == null ||
            !bubbleRoot.activeInHierarchy)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space))
        {
            AdvanceBriefing();
        }
    }

    public void BeginBriefing()
    {
        if (hasStartedMission || isBriefingVisible)
        {
            return;
        }

        isBriefingVisible = true;
        currentPageIndex = 0;
        lastAdvanceFrame = -1;
        runtimeConsoleUI.SetEditorInputLocked(true);

        if (bubblePresentationActive)
        {
            bubbleRoot.SetActive(true);
            PresentCurrentPage();
            return;
        }

        startButton.interactable = true;
        briefingRoot.SetActive(true);
        SelectStartButton();
    }

    private bool TryCreateBubbleBriefing()
    {
        Canvas canvas = runtimeConsoleRoot.GetComponentInParent<Canvas>(
            true
        );

        if (canvas == null ||
            canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            Debug.LogWarning(
                "BattleBriefingController: Bubble briefing requires the " +
                "existing Screen Space Overlay battle canvas. Falling back " +
                "to the legacy briefing card.",
                this
            );
            return false;
        }

        bubbleRoot = new GameObject(
            "StageBriefingBubble",
            typeof(RectTransform)
        );
        bubbleRoot.layer = LayerMask.NameToLayer("UI");

        RectTransform rootRect = bubbleRoot.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        // The runtime console occupies the right third of every battle HUD,
        // so keep the briefing on the opposite upper edge.
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(32f, -32f);
        rootRect.sizeDelta = new Vector2(BubbleWidth, BubbleHeight);
        rootRect.SetAsLastSibling();

        CreateBubbleTail(rootRect);

        Image background = CreateImage("BubbleBackground", rootRect);
        Stretch(background.rectTransform, Vector2.zero, Vector2.zero);
        background.color = new Color(0.025f, 0.07f, 0.10f, 0.94f);

        Outline border = background.gameObject.AddComponent<Outline>();
        border.effectColor = new Color(0.08f, 0.92f, 0.79f, 0.88f);
        border.effectDistance = new Vector2(1.2f, -1.2f);

        bubbleBackgroundButton = background.gameObject.AddComponent<Button>();
        ConfigureButton(bubbleBackgroundButton, background);
        bubbleBackgroundButton.onClick.AddListener(AdvanceBriefing);

        speakerNameText = CreateBubbleText("SpeakerName", rootRect);
        SetTopLeftRect(
            speakerNameText.rectTransform,
            new Vector2(18f, -13f),
            new Vector2(360f, 26f)
        );
        ConfigureBubbleText(
            speakerNameText,
            16f,
            TextAlignmentOptions.Left,
            new Color(0.20f, 0.97f, 0.81f, 1f)
        );
        speakerNameText.fontStyle = FontStyles.Bold;
        speakerNameText.textWrappingMode = TextWrappingModes.NoWrap;
        speakerNameText.overflowMode = TextOverflowModes.Ellipsis;

        messageText = CreateBubbleText("MessageText", rootRect);
        Stretch(
            messageText.rectTransform,
            new Vector2(
                BubbleBodyLeftRightPadding,
                BubbleFooterHeight
            ),
            new Vector2(
                -BubbleBodyLeftRightPadding,
                -BubbleBodyTopPadding
            )
        );
        ConfigureBubbleText(
            messageText,
            16f,
            TextAlignmentOptions.TopLeft,
            new Color(0.92f, 0.96f, 0.97f, 1f)
        );
        // The body has a fixed rectangle above the footer. Pages are fitted
        // against this exact rectangle, and Truncate is a last-resort guard
        // against text drawing over the NEXT / START MISSION controls.
        messageText.lineSpacing = -1f;
        messageText.paragraphSpacing = 4f;
        messageText.overflowMode = TextOverflowModes.Truncate;

        nextIndicatorText = CreateBubbleText("NextIndicator", rootRect);
        SetBottomLeftRect(
            nextIndicatorText.rectTransform,
            new Vector2(18f, 13f),
            new Vector2(220f, 21f)
        );
        ConfigureBubbleText(
            nextIndicatorText,
            12f,
            TextAlignmentOptions.Left,
            new Color(0.50f, 0.80f, 0.79f, 1f)
        );
        nextIndicatorText.textWrappingMode = TextWrappingModes.NoWrap;
        nextIndicatorText.overflowMode = TextOverflowModes.Ellipsis;

        continueButton = CreateContinueButton(rootRect);
        continueButton.onClick.AddListener(AdvanceBriefing);

        return true;
    }

    private void CreateBubbleTail(RectTransform rootRect)
    {
        Image tail = CreateImage("BubbleTail", rootRect);
        tail.rectTransform.anchorMin = new Vector2(0.10f, 0f);
        tail.rectTransform.anchorMax = new Vector2(0.10f, 0f);
        tail.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        tail.rectTransform.anchoredPosition = new Vector2(0f, -9f);
        tail.rectTransform.sizeDelta = new Vector2(21f, 21f);
        tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        tail.color = new Color(0.025f, 0.07f, 0.10f, 0.94f);
        tail.raycastTarget = false;

        Outline outline = tail.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.08f, 0.92f, 0.79f, 0.88f);
        outline.effectDistance = new Vector2(1f, -1f);
        tail.transform.SetAsFirstSibling();
    }

    private Button CreateContinueButton(RectTransform rootRect)
    {
        Image buttonImage = CreateImage("ContinueButton", rootRect);
        buttonImage.rectTransform.anchorMin = new Vector2(1f, 0f);
        buttonImage.rectTransform.anchorMax = new Vector2(1f, 0f);
        buttonImage.rectTransform.pivot = new Vector2(1f, 0f);
        buttonImage.rectTransform.anchoredPosition = new Vector2(-16f, 9f);
        buttonImage.rectTransform.sizeDelta = new Vector2(118f, 28f);
        buttonImage.color = new Color(0.06f, 0.28f, 0.27f, 0.92f);

        Outline border = buttonImage.gameObject.AddComponent<Outline>();
        border.effectColor = new Color(0.12f, 0.87f, 0.75f, 0.82f);
        border.effectDistance = new Vector2(1f, -1f);

        Button button = buttonImage.gameObject.AddComponent<Button>();
        ConfigureButton(button, buttonImage);

        continueButtonText = CreateBubbleText("Label", buttonImage.rectTransform);
        Stretch(continueButtonText.rectTransform, Vector2.zero, Vector2.zero);
        ConfigureBubbleText(
            continueButtonText,
            13f,
            TextAlignmentOptions.Center,
            Color.white
        );
        continueButtonText.textWrappingMode = TextWrappingModes.NoWrap;
        continueButtonText.overflowMode = TextOverflowModes.Ellipsis;

        return button;
    }

    private Image CreateImage(string name, Transform parent)
    {
        GameObject imageObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        imageObject.layer = LayerMask.NameToLayer("UI");
        imageObject.transform.SetParent(parent, false);
        return imageObject.GetComponent<Image>();
    }

    private TMP_Text CreateBubbleText(string name, Transform parent)
    {
        GameObject textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        textObject.layer = LayerMask.NameToLayer("UI");
        textObject.transform.SetParent(parent, false);
        return textObject.GetComponent<TMP_Text>();
    }

    private void ConfigureBubbleText(
        TMP_Text target,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        if (koreanFontAsset != null)
        {
            target.font = koreanFontAsset;
            target.fontSharedMaterial = koreanFontAsset.material;
        }

        target.fontSize = fontSize;
        target.enableAutoSizing = false;
        target.textWrappingMode = TextWrappingModes.Normal;
        target.overflowMode = TextOverflowModes.Overflow;
        target.alignment = alignment;
        target.color = color;
        target.raycastTarget = false;
    }

    private static void ConfigureButton(Button button, Image targetGraphic)
    {
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        button.targetGraphic = targetGraphic;
    }

    private static void Stretch(
        RectTransform target,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        target.anchorMin = Vector2.zero;
        target.anchorMax = Vector2.one;
        target.pivot = new Vector2(0.5f, 0.5f);
        target.offsetMin = offsetMin;
        target.offsetMax = offsetMax;
    }

    private static void SetTopLeftRect(
        RectTransform target,
        Vector2 position,
        Vector2 size)
    {
        target.anchorMin = new Vector2(0f, 1f);
        target.anchorMax = new Vector2(0f, 1f);
        target.pivot = new Vector2(0f, 1f);
        target.anchoredPosition = position;
        target.sizeDelta = size;
    }

    private static void SetBottomLeftRect(
        RectTransform target,
        Vector2 position,
        Vector2 size)
    {
        target.anchorMin = Vector2.zero;
        target.anchorMax = Vector2.zero;
        target.pivot = Vector2.zero;
        target.anchoredPosition = position;
        target.sizeDelta = size;
    }

    private void BuildBriefingPages()
    {
        briefingPages.Clear();
        Canvas.ForceUpdateCanvases();

        string missionAndTitle = string.IsNullOrWhiteSpace(titleCopy)
            ? missionText
            : $"{missionText} / {titleCopy}";

        AppendCopyAsLayoutPages(missionAndTitle, descriptionCopy);
        AppendCopyAsLayoutPages(
            $"{missionText} / SYSTEM RULES",
            rulesCopy
        );
        AppendCopyAsLayoutPages(
            $"{missionText} / CONTROLS",
            controlsCopy
        );

        if (briefingPages.Count == 0)
        {
            briefingPages.Add(
                new BriefingPage(missionAndTitle, "READY.")
            );
        }

        ValidateBubblePageLayout();
    }

    private void AppendCopyAsLayoutPages(string speaker, string source)
    {
        List<List<string>> blocks = BuildBubbleBlocks(source);
        if (blocks.Count == 0)
        {
            return;
        }

        List<List<string>> pageBlocks = new();

        foreach (List<string> block in blocks)
        {
            List<List<string>> candidate = new(pageBlocks) { block };
            if (DoesPageFit(candidate))
            {
                pageBlocks.Add(block);
                continue;
            }

            if (pageBlocks.Count > 0)
            {
                AddBubblePage(speaker, pageBlocks);
                pageBlocks.Clear();
            }

            if (DoesPageFit(new List<List<string>> { block }))
            {
                pageBlocks.Add(block);
                continue;
            }

            // A single semantic section is too tall. Split it only at its
            // source lines and repeat an existing section heading as needed,
            // so a header can never be stranded without its own content.
            AppendOversizedBlockAsPages(speaker, block);
        }

        if (pageBlocks.Count > 0)
        {
            AddBubblePage(speaker, pageBlocks);
        }
    }

    private static List<List<string>> BuildBubbleBlocks(string source)
    {
        List<List<string>> blocks = new();
        List<string> currentSection = null;

        foreach (List<string> paragraph in SplitBubbleParagraphs(source))
        {
            bool isHeadingParagraph = paragraph.Count == 1 &&
                IsSectionHeading(paragraph[0]);

            if (isHeadingParagraph)
            {
                if (IsInlineSectionHeading(paragraph[0]) &&
                    currentSection != null)
                {
                    // EXAMPLE is deliberately part of the preceding rule
                    // section. This keeps the heading and its DSL source
                    // together instead of creating an orphaned example page.
                    currentSection.Add(paragraph[0]);
                    continue;
                }

                if (currentSection != null && currentSection.Count > 0)
                {
                    blocks.Add(currentSection);
                }

                currentSection = new List<string>(paragraph);
                continue;
            }

            if (currentSection != null)
            {
                // A heading owns all following source paragraphs until the
                // next heading. This keeps "AVAILABLE ACTIONS" with its
                // actions while allowing ordinary upper-case content lines
                // (for example Debugger capability names) to stay content.
                currentSection.AddRange(paragraph);
                continue;
            }

            // Narrative paragraphs have no section header, so retain their
            // authored paragraph boundary through ComposeBubbleMessage.
            blocks.Add(paragraph);
        }

        if (currentSection != null && currentSection.Count > 0)
        {
            blocks.Add(currentSection);
        }

        return blocks;
    }

    private void AppendOversizedBlockAsPages(
        string speaker,
        List<string> block)
    {
        bool hasSectionHeading = block.Count > 1 &&
            IsSectionHeading(block[0]);
        string sectionHeading = hasSectionHeading ? block[0] : null;
        int lineIndex = hasSectionHeading ? 1 : 0;
        List<string> fragment = hasSectionHeading
            ? new List<string> { sectionHeading }
            : new List<string>();

        while (lineIndex < block.Count)
        {
            fragment.Add(block[lineIndex]);
            if (DoesPageFit(new List<List<string>> { fragment }))
            {
                lineIndex++;
                continue;
            }

            fragment.RemoveAt(fragment.Count - 1);
            bool headingWouldBeOrphaned = hasSectionHeading &&
                fragment.Count == 1;

            if (fragment.Count == 0 || headingWouldBeOrphaned)
            {
                // This should only be reachable for one extraordinarily long
                // source line. Keep the original text rather than silently
                // deleting it; the post-build validation reports the issue.
                fragment.Add(block[lineIndex]);
                AddBubblePage(
                    speaker,
                    new List<List<string>> { fragment }
                );
                lineIndex++;
            }
            else
            {
                AddBubblePage(
                    speaker,
                    new List<List<string>> { fragment }
                );
            }

            fragment = hasSectionHeading
                ? new List<string> { sectionHeading }
                : new List<string>();
        }

        if (fragment.Count > 0 &&
            (!hasSectionHeading || fragment.Count > 1))
        {
            AddBubblePage(
                speaker,
                new List<List<string>> { fragment }
            );
        }
    }

    private void AddBubblePage(
        string speaker,
        List<List<string>> blocks)
    {
        string message = ComposeBubbleMessage(blocks);
        if (!string.IsNullOrEmpty(message))
        {
            briefingPages.Add(new BriefingPage(speaker, message));
        }
    }

    private bool DoesPageFit(List<List<string>> blocks)
    {
        if (messageText == null)
        {
            return false;
        }

        string message = ComposeBubbleMessage(blocks);
        float preferredHeight = messageText.GetPreferredValues(
            message,
            messageText.rectTransform.rect.width,
            0f
        ).y;

        return preferredHeight <= messageText.rectTransform.rect.height +
            BubblePageHeightTolerance;
    }

    private static string ComposeBubbleMessage(List<List<string>> blocks)
    {
        StringBuilder builder = new();

        foreach (List<string> block in blocks)
        {
            if (block == null || block.Count == 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append("\n\n");
            }

            for (int index = 0; index < block.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append('\n');
                }

                string line = block[index];
                if (index > 0 && IsInlineSectionHeading(line))
                {
                    builder.Append('\n');
                }

                if ((index == 0 && IsSectionHeading(line)) ||
                    IsInlineSectionHeading(line))
                {
                    builder.Append("<b>").Append(line).Append("</b>");
                }
                else
                {
                    builder.Append(line);
                }
            }
        }

        return builder.ToString();
    }

    private static bool IsSectionHeading(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Contains("=>"))
        {
            return false;
        }

        bool hasAsciiLetter = false;
        foreach (char character in line)
        {
            if (character >= 'a' && character <= 'z')
            {
                return false;
            }

            if (character >= 'A' && character <= 'Z')
            {
                hasAsciiLetter = true;
            }
        }

        return hasAsciiLetter;
    }

    private static bool IsInlineSectionHeading(string line)
    {
        return string.Equals(
            line,
            "EXAMPLE",
            StringComparison.OrdinalIgnoreCase
        );
    }

    private void ValidateBubblePageLayout()
    {
        if (messageText == null)
        {
            return;
        }

        float bodyHeight = messageText.rectTransform.rect.height;
        float bodyWidth = messageText.rectTransform.rect.width;

        for (int index = 0; index < briefingPages.Count; index++)
        {
            BriefingPage page = briefingPages[index];
            float preferredHeight = messageText.GetPreferredValues(
                page.Message,
                bodyWidth,
                0f
            ).y;

            if (preferredHeight > bodyHeight + BubblePageHeightTolerance)
            {
                Debug.LogError(
                    "BattleBriefingController: Bubble page does not fit " +
                    $"page={index + 1}/{briefingPages.Count} " +
                    $"preferredHeight={preferredHeight:0.##} " +
                    $"bodyHeight={bodyHeight:0.##}",
                    this
                );
            }
        }
    }

    private static List<List<string>> SplitBubbleParagraphs(string source)
    {
        List<List<string>> paragraphs = new();

        if (string.IsNullOrWhiteSpace(source))
        {
            return paragraphs;
        }

        string[] rawLines = source.Replace("\r\n", "\n").Split('\n');
        List<string> currentParagraph = new();

        foreach (string rawLine in rawLines)
        {
            string line = rawLine.Trim();

            if (string.IsNullOrEmpty(line))
            {
                if (currentParagraph.Count > 0)
                {
                    paragraphs.Add(currentParagraph);
                    currentParagraph = new List<string>();
                }

                continue;
            }

            currentParagraph.Add(line);
        }

        if (currentParagraph.Count > 0)
        {
            paragraphs.Add(currentParagraph);
        }

        return paragraphs;
    }

    private void PresentCurrentPage()
    {
        if (briefingPages.Count == 0 ||
            speakerNameText == null ||
            messageText == null ||
            nextIndicatorText == null ||
            continueButtonText == null)
        {
            return;
        }

        BriefingPage page = briefingPages[currentPageIndex];
        bool isLastPage = currentPageIndex == briefingPages.Count - 1;

        speakerNameText.text = page.Speaker;
        messageText.text = page.Message;
        nextIndicatorText.text = isLastPage
            ? "READY TO EXECUTE"
            : $"NEXT  {currentPageIndex + 1} / {briefingPages.Count}";
        continueButtonText.text = isLastPage
            ? startButtonText
            : "NEXT  >";
    }

    private void AdvanceBriefing()
    {
        if (hasStartedMission ||
            lastAdvanceFrame == Time.frameCount)
        {
            return;
        }

        lastAdvanceFrame = Time.frameCount;

        if (currentPageIndex < briefingPages.Count - 1)
        {
            currentPageIndex++;
            PresentCurrentPage();

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            return;
        }

        StartMission();
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
        isBriefingVisible = false;
        startButton.interactable = false;
        briefingRoot.SetActive(false);

        if (bubbleRoot != null)
        {
            bubbleRoot.SetActive(false);
        }

        if (BriefingFinished != null)
        {
            BriefingFinished.Invoke();
        }
        else
        {
            runtimeConsoleUI.SetEditorInputLocked(false);
        }

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
