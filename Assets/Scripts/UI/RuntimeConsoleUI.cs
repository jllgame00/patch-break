using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class RuntimeConsoleUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ProgramRuntime runtime;
    [SerializeField] private LivePatchController livePatchController;
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private Button compileButton;
    [SerializeField] private TMP_Text outputText;

    [Header("Default Program")]
    [SerializeField, TextArea(3, 8)]
    private string defaultProgram =
        "if enemy.near => slash\n" +
        "if enemy.far => approach";

    private TMP_Text compileButtonLabel;
    private Coroutine focusRoutine;

    private void Awake()
    {
        if (runtime == null ||
            codeInput == null ||
            compileButton == null ||
            outputText == null)
        {
            Debug.LogError(
                "RuntimeConsoleUI: Required reference is missing."
            );

            enabled = false;
            return;
        }

        compileButtonLabel =
            compileButton.GetComponentInChildren<TMP_Text>();

        if (string.IsNullOrWhiteSpace(codeInput.text))
        {
            codeInput.text = defaultProgram;
        }

        outputText.text =
            "> HERO_RUNTIME.EXE\n" +
            "> STATUS: WAITING FOR PROGRAM";

        SetEditorEnabled(true);
        SetButtonLabel("COMPILE & RUN");

        compileButton.onClick.AddListener(
            HandleCompileClicked
        );
    }

    private void OnDestroy()
    {
        if (compileButton != null)
        {
            compileButton.onClick.RemoveListener(
                HandleCompileClicked
            );
        }
    }

    public void EnterLivePatchMode()
    {
        SetEditorEnabled(true);
        SetButtonLabel("APPLY PATCH");

        outputText.text =
            "> LIVE PATCH MODE\n" +
            "> EDIT PROGRAM WHILE TIME IS SLOWED\n" +
            "> APPLY PATCH TO RESUME";

        StartInputFocus();
    }

    public void ExitLivePatchMode()
    {
        StopInputFocus();

        codeInput.DeactivateInputField();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        SetEditorEnabled(false);
        SetButtonLabel("PATCH APPLIED");
    }

    public void AppendSystemMessage(string message)
    {
        if (outputText == null ||
            string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        string formattedMessage = $"> {message}";

        outputText.text =
            string.IsNullOrWhiteSpace(outputText.text)
                ? formattedMessage
                : $"{outputText.text}\n{formattedMessage}";
    }

    private void HandleCompileClicked()
    {
        bool isLivePatch =
            livePatchController != null &&
            livePatchController.IsPatching;

        string sourceCode = codeInput.text;

        codeInput.DeactivateInputField();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        outputText.text = "> COMPILING...";

        bool succeeded =
            runtime.CompileAndRun(sourceCode);

        outputText.text =
            $"> {runtime.LastCompileMessage}";

        if (isLivePatch)
        {
            livePatchController.HandleCompileResult(
                succeeded
            );

            if (!succeeded)
            {
                SetEditorEnabled(true);
                SetButtonLabel("FIX & APPLY");
                StartInputFocus();
            }

            return;
        }

        if (succeeded)
        {
            SetEditorEnabled(false);
            SetButtonLabel("PROGRAM RUNNING");
        }
        else
        {
            SetEditorEnabled(true);
            SetButtonLabel("COMPILE & RUN");
            StartInputFocus();
        }
    }

    private void SetEditorEnabled(bool enabledValue)
    {
        codeInput.interactable = enabledValue;
        compileButton.interactable = enabledValue;
    }

    private void SetButtonLabel(string value)
    {
        if (compileButtonLabel != null)
        {
            compileButtonLabel.text = value;
        }
    }

    private void StartInputFocus()
    {
        StopInputFocus();

        focusRoutine =
            StartCoroutine(FocusInputNextFrame());
    }

    private void StopInputFocus()
    {
        if (focusRoutine == null)
            return;

        StopCoroutine(focusRoutine);
        focusRoutine = null;
    }

    private IEnumerator FocusInputNextFrame()
    {
        // Space 입력이 코드창에 그대로 찍히는 것을 막기 위해
        // 다음 프레임에 입력창을 활성화한다.
        yield return null;

        codeInput.ActivateInputField();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(
                codeInput.gameObject
            );
        }

        codeInput.caretPosition =
            codeInput.text.Length;

        focusRoutine = null;
    }
    
    public void SetBattleEnded(bool victory)
    {
        StopInputFocus();

        codeInput.DeactivateInputField();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        SetEditorEnabled(false);

        SetButtonLabel(
            victory
                ? "BATTLE COMPLETE"
                : "SYSTEM HALTED"
        );

        outputText.text =
            victory
                ? "> TARGET PROCESS TERMINATED\n" +
                  "> BUILD STATUS: PASSED"
                : "> HERO_RUNTIME TERMINATED\n" +
                  "> BUILD STATUS: FAILED";
    }
}
