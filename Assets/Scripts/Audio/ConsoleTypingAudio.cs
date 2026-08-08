using TMPro;
using UnityEngine;

/// <summary>
/// Adds quiet typing feedback only for focused, physical edits in the existing
/// command TMP_InputField. Programmatic text assignments have no key input in
/// the current frame and therefore remain silent.
/// </summary>
public sealed class ConsoleTypingAudio : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;

    private string lastValue;

    public TMP_InputField InputField => inputField;

    public void Configure(TMP_InputField configuredInputField)
    {
        inputField = configuredInputField;
    }

    private void Awake()
    {
        if (inputField == null)
        {
            inputField = GetComponentInChildren<TMP_InputField>(true);
        }

        lastValue = inputField != null ? inputField.text : string.Empty;
    }

    private void OnEnable()
    {
        if (inputField != null)
        {
            inputField.onValueChanged.AddListener(HandleValueChanged);
        }
    }

    private void OnDisable()
    {
        if (inputField != null)
        {
            inputField.onValueChanged.RemoveListener(HandleValueChanged);
        }
    }

    private void HandleValueChanged(string value)
    {
        bool changed = value != lastValue;
        lastValue = value;

        if (!changed || inputField == null || !inputField.isFocused ||
            !WasPhysicalTextEditThisFrame())
        {
            return;
        }

        // A paste raises one TMP value change, so it produces at most one
        // cue instead of one sound per pasted character.
        PersistentAudioManager.PlayTyping();
    }

    private static bool WasPhysicalTextEditThisFrame()
    {
        if (!string.IsNullOrEmpty(Input.inputString))
        {
            return true;
        }

        if (Input.GetKeyDown(KeyCode.Backspace) ||
            Input.GetKeyDown(KeyCode.Delete))
        {
            return true;
        }

        bool modifier = Input.GetKey(KeyCode.LeftControl) ||
                        Input.GetKey(KeyCode.RightControl) ||
                        Input.GetKey(KeyCode.LeftCommand) ||
                        Input.GetKey(KeyCode.RightCommand);
        return modifier && Input.GetKeyDown(KeyCode.V);
    }
}
