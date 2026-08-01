using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed class RuntimeConsoleUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ProgramRuntime runtime;
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private Button compileButton;
    [SerializeField] private TMP_Text outputText;

    [Header("Default Program")]
    [SerializeField, TextArea(3, 8)]
    private string defaultProgram =
        "if enemy.near => slash\n" +
        "if enemy.far => approach";

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

        if (string.IsNullOrWhiteSpace(codeInput.text))
        {
            codeInput.text = defaultProgram;
        }

        outputText.text =
            "> HERO_RUNTIME.EXE\n" +
            "> STATUS: WAITING FOR PROGRAM";

        compileButton.onClick.AddListener(HandleCompileClicked);
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

    private void HandleCompileClicked()
    {
        Debug.Log("COMPILE BUTTON CLICKED");

        codeInput.DeactivateInputField();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        outputText.text = "> COMPILING...";

        bool succeeded = runtime.CompileAndRun(
            codeInput.text
        );

        outputText.text =
            $"> {runtime.LastCompileMessage}";

        Debug.Log(
            succeeded
                ? "COMPILE SUCCEEDED"
                : "COMPILE FAILED"
        );
    }
}