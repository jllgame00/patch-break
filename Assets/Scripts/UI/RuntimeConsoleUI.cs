using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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

    [Header("Diagnostics")]
    [SerializeField]
    private bool enableLivePatchInputDiagnostics;

    [SerializeField, Min(1)]
    private int inputDiagnosticFrameWindow = 900;

    private bool battleEnded;
    private bool editorInputLockedExternally;
    private bool hasLoggedEditorStateWrite;
    private bool lastInputInteractable;
    private bool lastInputReadOnly;
    private bool lastButtonInteractable;
    private bool codeInputDiagnosticEventsRegistered;
    private int inputDiagnosticFramesRemaining;
    private string lastDiagnosticInputText;

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

        lastDiagnosticInputText = codeInput.text;
        RegisterCodeInputDiagnosticEvents();

        outputText.text =
            "> HERO_RUNTIME.EXE\n" +
            "> STATUS: WAITING FOR PROGRAM";

        RefreshEditorInteractivity("AWAKE");
        SetButtonLabel("COMPILE & RUN");

        compileButton.onClick.AddListener(
            HandleCompileClicked
        );
    }

    private void OnDestroy()
    {
        UnregisterCodeInputDiagnosticEvents();

        if (compileButton != null)
        {
            compileButton.onClick.RemoveListener(
                HandleCompileClicked
            );
        }
    }

    private void Update()
    {
        if (!enableLivePatchInputDiagnostics ||
            inputDiagnosticFramesRemaining <= 0)
        {
            return;
        }

        inputDiagnosticFramesRemaining--;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null ||
            !keyboard.anyKey.wasPressedThisFrame)
        {
            return;
        }

        LogKeyboardProbe("LIVE_PATCH_FAILURE_KEY");
        LogRuntimeInputState("LIVE_PATCH_FAILURE_KEY");
        LogRaycastAtCurrentPointer("LIVE_PATCH_FAILURE_KEY");
    }

    public void EnterLivePatchMode()
    {
        RefreshEditorInteractivity("LIVE_PATCH_ENTER");
        SetButtonLabel("APPLY PATCH");

        outputText.text =
            "> LIVE PATCH MODE\n" +
            "> EDIT PROGRAM WHILE TIME IS SLOWED\n" +
            "> APPLY PATCH TO RESUME";

        StartInputFocus();
        LogRuntimeInputState("LIVE_PATCH_ENTER_REQUESTED");

        if (enableLivePatchInputDiagnostics)
        {
            StartCoroutine(LogLivePatchEntryStateNextFrame());
        }
    }

    public void ExitLivePatchMode()
    {
        StopInputFocus();

        codeInput.DeactivateInputField();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        RefreshEditorInteractivity("LIVE_PATCH_EXIT");
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

    public void SetEditorInputLocked(bool locked)
    {
        editorInputLockedExternally = locked;

        if (locked)
        {
            StopInputFocus();

            if (codeInput != null)
            {
                codeInput.DeactivateInputField();
            }

            if (EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject ==
                    codeInput?.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        if (codeInput != null && compileButton != null)
        {
            RefreshEditorInteractivity(
                locked
                    ? "EXTERNAL_EDITOR_LOCK"
                    : "EXTERNAL_EDITOR_UNLOCK"
            );
        }
    }

    private void HandleCompileClicked()
    {
        bool isLivePatch =
            livePatchController != null &&
            livePatchController.IsPatching;

        string sourceCode = codeInput.text;

        LogEditorState("COMPILE_CLICKED_START");
        LogRuntimeInputState("COMPILE_CLICKED_START");
        LogRaycastAtCurrentPointer("COMPILE_CLICKED_START");

        if (!isLivePatch)
        {
            codeInput.DeactivateInputField();

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        outputText.text = "> COMPILING...";

        bool succeeded =
            runtime.CompileAndRun(sourceCode);

        outputText.text =
            $"> {runtime.LastCompileMessage}";

        if (!succeeded)
        {
            LogEditorState("COMPILE_FAILURE_RETURNED");
            LogRuntimeInputState("COMPILE_FAILURE_RETURNED");
        }

        if (isLivePatch)
        {
            livePatchController.HandleCompileResult(
                succeeded
            );

            if (!succeeded)
            {
                LogEditorState("LIVE_PATCH_FAILURE_EVENT_COMPLETE");
                LogRuntimeInputState(
                    "LIVE_PATCH_FAILURE_EVENT_COMPLETE"
                );
            }

            if (!succeeded)
            {
                RestoreEditorAfterCompileFailure(
                    "FIX & APPLY"
                );
                LogEditorState("RESTORE_IMMEDIATE");
                LogLivePatchEditorFinal(
                    "LIVE PATCH EDITOR FINAL IMMEDIATE"
                );
                BeginInputFailureDiagnostics(
                    "LIVE_PATCH_FAILURE_RESTORE_COMPLETE"
                );
                StartCoroutine(
                    LogEditorStateAfterCompileFailure()
                );
            }

            return;
        }

        if (succeeded)
        {
            RefreshEditorInteractivity("INITIAL_COMPILE_SUCCESS");
            SetButtonLabel("PROGRAM RUNNING");
        }
        else
        {
            RestoreEditorAfterCompileFailure(
                "COMPILE & RUN"
            );
            LogEditorState("RESTORE_IMMEDIATE");
            BeginInputFailureDiagnostics(
                "INITIAL_COMPILE_FAILURE_RESTORE_COMPLETE"
            );
            StartCoroutine(
                LogEditorStateAfterCompileFailure()
            );
        }
    }

    private void RestoreEditorAfterCompileFailure(
        string buttonLabel)
    {
        RefreshEditorInteractivity("COMPILE_FAILURE");
        SetButtonLabel(buttonLabel);

        codeInput.Select();
        codeInput.ActivateInputField();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(
                codeInput.gameObject
            );
        }

        int textEnd = codeInput.text.Length;
        codeInput.caretPosition = textEnd;
        codeInput.stringPosition = textEnd;
    }

    private IEnumerator LogEditorStateAfterCompileFailure()
    {
        yield return null;
        LogEditorState("RESTORE_1_FRAME");
        LogLivePatchEditorFinal("LIVE PATCH EDITOR FINAL 1 FRAME");
        LogRuntimeInputState("RESTORE_1_FRAME");

        yield return null;
        yield return null;
        LogEditorState("RESTORE_3_FRAMES");
        LogLivePatchEditorFinal("LIVE PATCH EDITOR FINAL 3 FRAMES");
        LogRuntimeInputState("RESTORE_3_FRAMES");
    }

    private IEnumerator LogLivePatchEntryStateNextFrame()
    {
        yield return null;
        LogRuntimeInputState("LIVE_PATCH_ENTRY_READY");
        LogRaycastAtCurrentPointer("LIVE_PATCH_ENTRY_READY");
    }

    private void BeginInputFailureDiagnostics(string phase)
    {
        if (!enableLivePatchInputDiagnostics)
        {
            return;
        }

        inputDiagnosticFramesRemaining = inputDiagnosticFrameWindow;

        Debug.Log(
            "LIVE PATCH INPUT DIAGNOSTIC WINDOW START\n" +
            $"phase={phase}\n" +
            $"frames={inputDiagnosticFramesRemaining}"
        );

        LogRuntimeInputState(phase);
        LogRaycastAtCurrentPointer(phase);
    }

    private void RegisterCodeInputDiagnosticEvents()
    {
        if (!enableLivePatchInputDiagnostics ||
            codeInput == null ||
            codeInputDiagnosticEventsRegistered)
        {
            return;
        }

        codeInput.onSelect.AddListener(HandleCodeInputSelected);
        codeInput.onDeselect.AddListener(HandleCodeInputDeselected);
        codeInput.onValueChanged.AddListener(
            HandleCodeInputValueChanged
        );
        codeInput.onEndEdit.AddListener(HandleCodeInputEndEdit);
        codeInput.onSubmit.AddListener(HandleCodeInputSubmitted);
        codeInputDiagnosticEventsRegistered = true;
    }

    private void UnregisterCodeInputDiagnosticEvents()
    {
        if (codeInput == null ||
            !codeInputDiagnosticEventsRegistered)
        {
            return;
        }

        codeInput.onSelect.RemoveListener(HandleCodeInputSelected);
        codeInput.onDeselect.RemoveListener(
            HandleCodeInputDeselected
        );
        codeInput.onValueChanged.RemoveListener(
            HandleCodeInputValueChanged
        );
        codeInput.onEndEdit.RemoveListener(HandleCodeInputEndEdit);
        codeInput.onSubmit.RemoveListener(HandleCodeInputSubmitted);
        codeInputDiagnosticEventsRegistered = false;
    }

    private void HandleCodeInputSelected(string value)
    {
        LogCodeInputEvent("SELECT", value);
        LogRaycastAtCurrentPointer("CODE_INPUT_SELECT");
    }

    private void HandleCodeInputDeselected(string value)
    {
        LogCodeInputEvent("DESELECT", value);
    }

    private void HandleCodeInputValueChanged(string value)
    {
        if (!enableLivePatchInputDiagnostics)
        {
            return;
        }

        string previousValue = lastDiagnosticInputText;
        lastDiagnosticInputText = value;

        Debug.Log(
            "CODE INPUT EVENT: VALUE_CHANGED\n" +
            $"old={EscapeDiagnosticText(previousValue)}\n" +
            $"new={EscapeDiagnosticText(value)}"
        );

        LogRuntimeInputState("CODE_INPUT_VALUE_CHANGED");
    }

    private void HandleCodeInputEndEdit(string value)
    {
        LogCodeInputEvent("END_EDIT", value);
    }

    private void HandleCodeInputSubmitted(string value)
    {
        LogCodeInputEvent("SUBMIT", value);
    }

    private void LogCodeInputEvent(string eventName, string value)
    {
        if (!enableLivePatchInputDiagnostics)
        {
            return;
        }

        Debug.Log(
            $"CODE INPUT EVENT: {eventName}\n" +
            $"input={DescribeGameObject(codeInput.gameObject)}\n" +
            $"text={EscapeDiagnosticText(value)}"
        );

        LogRuntimeInputState($"CODE_INPUT_{eventName}");
    }

    private void LogKeyboardProbe(string phase)
    {
        if (!enableLivePatchInputDiagnostics ||
            codeInput == null)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        Debug.Log(
            $"INPUT PROBE phase={phase}\n" +
            $"frame={Time.frameCount}\n" +
            $"keyboardExists={keyboard != null}\n" +
            $"backspacePressed={keyboard != null && keyboard.backspaceKey.wasPressedThisFrame}\n" +
            $"deletePressed={keyboard != null && keyboard.deleteKey.wasPressedThisFrame}\n" +
            $"ctrlPressed={keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)}\n" +
            $"aPressed={keyboard != null && keyboard.aKey.wasPressedThisFrame}\n" +
            $"vPressed={keyboard != null && keyboard.vKey.wasPressedThisFrame}\n" +
            $"selected={DescribeGameObject(EventSystem.current?.currentSelectedGameObject)}\n" +
            $"inputFocused={codeInput.isFocused}\n" +
            $"inputText={EscapeDiagnosticText(codeInput.text)}"
        );
    }

    private void LogRuntimeInputState(string phase)
    {
        if (!enableLivePatchInputDiagnostics ||
            codeInput == null)
        {
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        InputSystemUIInputModule inputModule =
            eventSystem != null
                ? eventSystem.GetComponent<InputSystemUIInputModule>()
                : null;
        Keyboard keyboard = Keyboard.current;

        Debug.Log(
            $"LIVE PATCH INPUT STATE phase={phase}\n" +
            $"frame={Time.frameCount}\n" +
            $"codeInput={DescribeGameObject(codeInput.gameObject)}\n" +
            $"runtimeConsole={DescribeComponent(this)}\n" +
            $"inputActive={codeInput.gameObject.activeInHierarchy}\n" +
            $"inputEnabled={codeInput.enabled}\n" +
            $"inputInteractable={codeInput.interactable}\n" +
            $"inputReadOnly={codeInput.readOnly}\n" +
            $"inputFocused={codeInput.isFocused}\n" +
            $"inputText={EscapeDiagnosticText(codeInput.text)}\n" +
            $"caret={codeInput.caretPosition}\n" +
            $"selectionAnchor={codeInput.selectionAnchorPosition}\n" +
            $"selectionFocus={codeInput.selectionFocusPosition}\n" +
            $"eventSystemEnabled={eventSystem != null && eventSystem.enabled}\n" +
            $"eventSystem={DescribeComponent(eventSystem)}\n" +
            $"selected={DescribeGameObject(eventSystem?.currentSelectedGameObject)}\n" +
            $"inputModuleEnabled={inputModule != null && inputModule.enabled}\n" +
            $"inputModuleActions={inputModule?.actionsAsset?.name ?? "<null>"}\n" +
            $"inputModulePoint={DescribeInputAction(inputModule?.point)}\n" +
            $"inputModuleLeftClick={DescribeInputAction(inputModule?.leftClick)}\n" +
            $"inputModuleSubmit={DescribeInputAction(inputModule?.submit)}\n" +
            $"inputModuleCancel={DescribeInputAction(inputModule?.cancel)}\n" +
            $"keyboardExists={keyboard != null}\n" +
            $"keyboardAnyKeyPressed={keyboard != null && keyboard.anyKey.isPressed}\n" +
            $"keyboardBackspacePressed={keyboard != null && keyboard.backspaceKey.wasPressedThisFrame}\n" +
            $"keyboardVPressed={keyboard != null && keyboard.vKey.wasPressedThisFrame}\n" +
            $"timeScale={Time.timeScale}\n" +
            $"livePatchActive={livePatchController != null && livePatchController.IsLivePatchModeActive}\n" +
            $"programRunning={runtime != null && runtime.IsRunning}\n" +
            $"canvasGroups={GetCanvasGroupSummary()}\n" +
            $"runtimeConsoleInstances={GetRuntimeConsoleInstanceSummary()}\n" +
            $"tmpInputInstances={GetInputFieldInstanceSummary()}\n" +
            $"eventSystemInstances={GetEventSystemInstanceSummary()}"
        );
    }

    private void LogRaycastAtCurrentPointer(string phase)
    {
        if (!enableLivePatchInputDiagnostics)
        {
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        Mouse mouse = Mouse.current;

        if (eventSystem == null || mouse == null)
        {
            Debug.Log(
                $"UI RAYCAST phase={phase} unavailable " +
                $"eventSystem={eventSystem != null} mouse={mouse != null}"
            );
            return;
        }

        PointerEventData pointerEventData =
            new(eventSystem)
            {
                position = mouse.position.ReadValue()
            };
        List<RaycastResult> results = new();
        eventSystem.RaycastAll(pointerEventData, results);

        StringBuilder resultSummary = new();

        if (results.Count == 0)
        {
            resultSummary.Append("<none>");
        }
        else
        {
            for (int index = 0; index < results.Count; index++)
            {
                RaycastResult result = results[index];

                if (index > 0)
                {
                    resultSummary.Append(" | ");
                }

                resultSummary.Append(
                    $"{index}:{DescribeGameObject(result.gameObject)} " +
                    $"depth={result.depth} sortingOrder={result.sortingOrder}"
                );
            }
        }

        Debug.Log(
            $"UI RAYCAST phase={phase}\n" +
            $"position={pointerEventData.position}\n" +
            $"hits={resultSummary}"
        );
    }

    private string GetCanvasGroupSummary()
    {
        CanvasGroup[] canvasGroups =
            codeInput.GetComponentsInParent<CanvasGroup>(true);

        if (canvasGroups.Length == 0)
        {
            return "none";
        }

        StringBuilder summary = new();

        for (int index = 0; index < canvasGroups.Length; index++)
        {
            CanvasGroup group = canvasGroups[index];

            if (index > 0)
            {
                summary.Append(", ");
            }

            summary.Append(
                $"{DescribeComponent(group)} " +
                $"interactable={group.interactable} " +
                $"blocksRaycasts={group.blocksRaycasts} " +
                $"alpha={group.alpha:F2}"
            );
        }

        return summary.ToString();
    }

    private static string DescribeInputAction(
        InputActionReference actionReference)
    {
        InputAction action = actionReference?.action;

        return action == null
            ? "<null>"
            : $"{action.actionMap?.name}/{action.name} " +
              $"enabled={action.enabled}";
    }

    private static string DescribeGameObject(GameObject gameObject)
    {
        return gameObject == null
            ? "<none>"
            : $"{gameObject.name}#{gameObject.GetInstanceID()} " +
              $"path={GetHierarchyPath(gameObject.transform)}";
    }

    private static string DescribeComponent(Component component)
    {
        return component == null
            ? "<none>"
            : $"{component.GetType().Name}#{component.GetInstanceID()} " +
              $"path={GetHierarchyPath(component.transform)}";
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return "<none>";
        }

        StringBuilder path = new(target.name);

        while (target.parent != null)
        {
            target = target.parent;
            path.Insert(0, $"{target.name}/");
        }

        return path.ToString();
    }

    private string GetRuntimeConsoleInstanceSummary()
    {
        RuntimeConsoleUI[] consoles = UnityEngine.Object
            .FindObjectsByType<RuntimeConsoleUI>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        StringBuilder summary = new();

        for (int index = 0; index < consoles.Length; index++)
        {
            if (index > 0)
            {
                summary.Append(" | ");
            }

            summary.Append(DescribeComponent(consoles[index]));
        }

        return summary.ToString();
    }

    private static string GetInputFieldInstanceSummary()
    {
        TMP_InputField[] inputFields = UnityEngine.Object
            .FindObjectsByType<TMP_InputField>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        StringBuilder summary = new();

        for (int index = 0; index < inputFields.Length; index++)
        {
            if (index > 0)
            {
                summary.Append(" | ");
            }

            summary.Append(DescribeComponent(inputFields[index]));
        }

        return summary.ToString();
    }

    private static string GetEventSystemInstanceSummary()
    {
        EventSystem[] eventSystems = UnityEngine.Object
            .FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        StringBuilder summary = new();

        for (int index = 0; index < eventSystems.Length; index++)
        {
            if (index > 0)
            {
                summary.Append(" | ");
            }

            summary.Append(DescribeComponent(eventSystems[index]));
        }

        return summary.ToString();
    }

    private static string EscapeDiagnosticText(string value)
    {
        return string.IsNullOrEmpty(value)
            ? "<empty>"
            : value.Replace("\r", "\\r")
                .Replace("\n", "\\n");
    }

    private void LogEditorState(string phase)
    {
        if (!enableLivePatchInputDiagnostics ||
            codeInput == null ||
            compileButton == null)
        {
            return;
        }

        CanvasGroup[] canvasGroups =
            codeInput.GetComponentsInParent<CanvasGroup>(true);

        StringBuilder canvasGroupState = new();

        if (canvasGroups.Length == 0)
        {
            canvasGroupState.Append("none");
        }
        else
        {
            for (int index = 0;
                 index < canvasGroups.Length;
                 index++)
            {
                CanvasGroup group = canvasGroups[index];

                if (index > 0)
                {
                    canvasGroupState.Append(", ");
                }

                canvasGroupState.Append(
                    $"{group.name}(interactable={group.interactable}, " +
                    $"blocksRaycasts={group.blocksRaycasts}, " +
                    $"alpha={group.alpha:F2})"
                );
            }
        }

        string selected = EventSystem.current == null ||
                          EventSystem.current.currentSelectedGameObject ==
                          null
            ? "none"
            : EventSystem.current.currentSelectedGameObject.name;

        Debug.Log(
            $"EDITOR STATE phase={phase}\n" +
            $"inputActive={codeInput.gameObject.activeInHierarchy}\n" +
            $"inputEnabled={codeInput.enabled}\n" +
            $"inputInteractable={codeInput.interactable}\n" +
            $"inputReadOnly={codeInput.readOnly}\n" +
            $"inputFocused={codeInput.isFocused}\n" +
            $"buttonActive={compileButton.gameObject.activeInHierarchy}\n" +
            $"buttonInteractable={compileButton.interactable}\n" +
            $"selected={selected}\n" +
            $"canvasGroups={canvasGroupState}"
        );
    }

    private bool ShouldEditorBeEditable()
    {
        if (editorInputLockedExternally)
        {
            return false;
        }

        if (battleEnded)
        {
            return false;
        }

        if (livePatchController != null &&
            livePatchController.IsLivePatchModeActive)
        {
            return true;
        }

        return runtime == null || !runtime.IsRunning;
    }

    private void RefreshEditorInteractivity(string writer)
    {
        bool editable = ShouldEditorBeEditable();

        codeInput.gameObject.SetActive(true);
        codeInput.enabled = true;
        codeInput.interactable = editable;
        codeInput.readOnly = !editable;

        compileButton.gameObject.SetActive(true);
        compileButton.interactable = editable;

        LogEditorWrite(
            writer,
            codeInput.interactable,
            codeInput.readOnly,
            compileButton.interactable);
    }

    private void LogEditorWrite(
        string writer,
        bool inputInteractable,
        bool inputReadOnly,
        bool buttonInteractable)
    {
        if (!enableLivePatchInputDiagnostics ||
            runtime == null ||
            livePatchController == null)
        {
            return;
        }

        bool stateChanged = !hasLoggedEditorStateWrite ||
            lastInputInteractable != inputInteractable ||
            lastInputReadOnly != inputReadOnly ||
            lastButtonInteractable != buttonInteractable;

        if (!stateChanged)
        {
            return;
        }

        hasLoggedEditorStateWrite = true;
        lastInputInteractable = inputInteractable;
        lastInputReadOnly = inputReadOnly;
        lastButtonInteractable = buttonInteractable;

        Debug.Log(
            $"EDITOR STATE WRITE writer={writer}\n" +
            $"frame={Time.frameCount}\n" +
            $"programRunning={runtime.IsRunning}\n" +
            $"livePatchActive={livePatchController.IsLivePatchModeActive}\n" +
            $"inputInteractable={inputInteractable}\n" +
            $"inputReadOnly={inputReadOnly}\n" +
            $"buttonInteractable={buttonInteractable}");
    }

    private void LogLivePatchEditorFinal(string phase)
    {
        if (!enableLivePatchInputDiagnostics ||
            codeInput == null ||
            compileButton == null)
        {
            return;
        }

        string selectedObjectName =
            EventSystem.current?.currentSelectedGameObject?.name ??
            "<none>";

        Debug.Log(
            $"{phase}\n" +
            $"frame={Time.frameCount}\n" +
            $"programRunning={runtime != null && runtime.IsRunning}\n" +
            $"livePatchActive={livePatchController != null && livePatchController.IsLivePatchModeActive}\n" +
            $"active={codeInput.gameObject.activeInHierarchy}\n" +
            $"enabled={codeInput.enabled}\n" +
            $"interactable={codeInput.interactable}\n" +
            $"readOnly={codeInput.readOnly}\n" +
            $"focused={codeInput.isFocused}\n" +
            $"selected={selectedObjectName}");
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
        battleEnded = true;
        StopInputFocus();

        codeInput.DeactivateInputField();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        RefreshEditorInteractivity("BATTLE_END");

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
