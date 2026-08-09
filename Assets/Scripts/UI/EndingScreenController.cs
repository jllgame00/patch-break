using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the Ending intentionally small: a terminal-style sequence that can
/// be advanced with the current Input System, then returns to MainMenu.
/// Presentation objects are assigned by PatchBreakEndingNarrativeSetup so the
/// scene itself never needs hand-edited YAML.
/// </summary>
public sealed class EndingScreenController : MonoBehaviour
{
    private readonly struct NarrativeBeat
    {
        public NarrativeBeat(string text, float holdDuration)
        {
            Text = text;
            HoldDuration = holdDuration;
        }

        public string Text { get; }
        public float HoldDuration { get; }
    }

    private static readonly NarrativeBeat[] Beats =
    {
        new(
            "[ SYSTEM ]\nDEBUGGER PROCESS TERMINATED.",
            2.15f
        ),
        new(
            "패턴은 분석되었다.\n" +
            "행동은 예측되었다.\n" +
            "프로그램은 완벽하게 읽혔다.",
            2.65f
        ),
        new(
            "하지만—\n\n" +
            "정해진 코드만으로는\n" +
            "끝까지 살아남을 수 없었다.",
            2.65f
        ),
        new(
            "필요했던 것은\n" +
            "더 완벽한 명령이 아니라,\n\n" +
            "상황을 보고,\n" +
            "고치고,\n" +
            "다시 선택하는 것이었다.",
            3.25f
        ),
        new(
            "LIVE PATCH COMPLETE.\n\n" +
            "> The program survived.\n" +
            "> Because you changed it.",
            3.25f
        ),
        new("PATCH//BREAK", 0f)
    };

    [Header("Progression")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Narrative Presentation")]
    [SerializeField] private TMP_Text narrativeText;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private CanvasGroup narrativeCanvasGroup;
    [SerializeField, Min(0f)] private float initialDelay = 0.75f;
    [SerializeField, Min(0.005f)] private float characterInterval = 0.022f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.2f;

    private int beatIndex = -1;
    private int visibleCharacters;
    private int characterCount;
    private float phaseElapsed;
    private bool isTyping;
    private bool isHolding;
    private bool sequenceReady;
    private bool transitionRequested;

    private void Awake()
    {
        Time.timeScale = 1f;
    }

    private void Start()
    {
        if (!HasPresentationReferences())
        {
            Debug.LogError(
                "EndingScreenController: narrative presentation references " +
                "are missing. Run Tools > PATCH BREAK > Ending > " +
                "Setup Narrative Ending.",
                this
            );
            return;
        }

        narrativeText.text = string.Empty;
        narrativeText.maxVisibleCharacters = 0;
        promptText.gameObject.SetActive(false);
        narrativeCanvasGroup.alpha = 0f;
        sequenceReady = true;
    }

    private void Update()
    {
        if (!sequenceReady || transitionRequested)
        {
            return;
        }

        bool confirmPressed = WasConfirmPressed();

        if (beatIndex < 0)
        {
            phaseElapsed += Time.unscaledDeltaTime;
            if (phaseElapsed >= initialDelay)
            {
                BeginBeat(0);
            }

            return;
        }

        if (isTyping)
        {
            UpdateTyping();
            if (confirmPressed)
            {
                CompleteCurrentBeat();
            }

            return;
        }

        if (!isHolding)
        {
            return;
        }

        if (beatIndex == Beats.Length - 1)
        {
            if (confirmPressed)
            {
                LoadMainMenu();
            }

            return;
        }

        phaseElapsed += Time.unscaledDeltaTime;
        if (confirmPressed || phaseElapsed >= Beats[beatIndex].HoldDuration)
        {
            BeginBeat(beatIndex + 1);
        }
    }

    /// <summary>
    /// Called only by the editor setup tool. Runtime logic never creates UI,
    /// which keeps the ending scene topology explicit and reviewable.
    /// </summary>
    public void ConfigureNarrativePresentation(
        TMP_Text narrative,
        TMP_Text prompt,
        CanvasGroup canvasGroup
    )
    {
        narrativeText = narrative;
        promptText = prompt;
        narrativeCanvasGroup = canvasGroup;
    }

    public bool HasPresentationReferences()
    {
        return narrativeText != null &&
               promptText != null &&
               narrativeCanvasGroup != null;
    }

    private void BeginBeat(int nextBeatIndex)
    {
        beatIndex = nextBeatIndex;
        phaseElapsed = 0f;
        visibleCharacters = 0;
        isTyping = true;
        isHolding = false;

        NarrativeBeat beat = Beats[beatIndex];
        narrativeText.text = beat.Text;
        narrativeText.maxVisibleCharacters = 0;
        narrativeText.ForceMeshUpdate();
        characterCount = narrativeText.textInfo.characterCount;
        narrativeCanvasGroup.alpha = 0f;
        promptText.gameObject.SetActive(false);
    }

    private void UpdateTyping()
    {
        phaseElapsed += Time.unscaledDeltaTime;
        narrativeCanvasGroup.alpha = Mathf.Clamp01(
            phaseElapsed / fadeDuration
        );

        int nextVisibleCharacters = Mathf.Min(
            characterCount,
            Mathf.FloorToInt(phaseElapsed / characterInterval) + 1
        );
        if (nextVisibleCharacters > visibleCharacters)
        {
            visibleCharacters = nextVisibleCharacters;
            narrativeText.maxVisibleCharacters = visibleCharacters;
        }

        if (visibleCharacters >= characterCount)
        {
            CompleteCurrentBeat();
        }
    }

    private void CompleteCurrentBeat()
    {
        narrativeText.maxVisibleCharacters = characterCount;
        narrativeCanvasGroup.alpha = 1f;
        isTyping = false;
        isHolding = true;
        phaseElapsed = 0f;

        if (beatIndex == Beats.Length - 1)
        {
            promptText.gameObject.SetActive(true);
        }
    }

    private static bool WasConfirmPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            return true;
        }

        Gamepad gamepad = Gamepad.current;
        return gamepad != null &&
               (gamepad.buttonSouth.wasPressedThisFrame ||
                gamepad.startButton.wasPressedThisFrame);
    }

    private void LoadMainMenu()
    {
        if (transitionRequested)
        {
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError(
                "EndingScreenController: Main menu scene '" +
                mainMenuSceneName +
                "' cannot be loaded. Ensure it is included in the Build " +
                "Profile.",
                this
            );
            return;
        }

        transitionRequested = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
