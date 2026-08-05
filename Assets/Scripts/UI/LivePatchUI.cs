using TMPro;
using UnityEngine;

public sealed class LivePatchUI : MonoBehaviour
{
    [SerializeField]
    private LivePatchController controller;

    [SerializeField]
    private TMP_Text statusText;

    private int lastRemainingUses = -1;
    private bool lastPatchingState;
    private string adaptiveHintMessage;

    private void Awake()
    {
        if (statusText == null)
        {
            statusText = GetComponent<TMP_Text>();
        }
    }

    private void Update()
    {
        if (controller == null ||
            statusText == null)
        {
            return;
        }

        if (lastRemainingUses ==
            controller.RemainingUses &&
            lastPatchingState ==
            controller.IsPatching)
        {
            return;
        }

        lastRemainingUses =
            controller.RemainingUses;

        lastPatchingState =
            controller.IsPatching;

        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (!string.IsNullOrWhiteSpace(adaptiveHintMessage))
        {
            statusText.text = adaptiveHintMessage;
            return;
        }

        if (controller.IsPatching)
        {
            statusText.text =
                "LIVE PATCH: EDITING...";
            return;
        }

        statusText.text =
            controller.RemainingUses > 0
                ? "LIVE PATCH [SPACE]: READY"
                : "LIVE PATCH: USED";
    }

    public void ShowAdaptivePatchHint()
    {
        ShowAdaptivePatchHint(
            "PRESS [SPACE] — LIVE PATCH REQUIRED"
        );
    }

    public void ShowAdaptivePatchHint(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        adaptiveHintMessage = message;
        RefreshDisplay();
    }

    public void HideAdaptivePatchHint()
    {
        if (string.IsNullOrEmpty(adaptiveHintMessage))
            return;

        adaptiveHintMessage = null;
        RefreshDisplay();
    }
}
