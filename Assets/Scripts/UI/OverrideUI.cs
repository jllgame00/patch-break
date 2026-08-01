using System.Text;
using TMPro;
using UnityEngine;

public sealed class OverrideUI : MonoBehaviour
{
    [SerializeField]
    private HeroOverrideController overrideController;

    [SerializeField]
    private TMP_Text statusText;

    private int lastRemainingCharges = -1;

    private void Awake()
    {
        if (statusText == null)
        {
            statusText = GetComponent<TMP_Text>();
        }
    }

    private void Update()
    {
        if (overrideController == null ||
            statusText == null)
        {
            return;
        }

        if (lastRemainingCharges ==
            overrideController.RemainingCharges)
        {
            return;
        }

        lastRemainingCharges =
            overrideController.RemainingCharges;

        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        StringBuilder builder = new();

        builder.Append("OVERRIDE [Q]: ");

        for (int index = 0;
             index < overrideController.MaxCharges;
             index++)
        {
            bool available =
                index <
                overrideController.RemainingCharges;

            builder.Append(available ? "■" : "□");

            if (index <
                overrideController.MaxCharges - 1)
            {
                builder.Append(' ');
            }
        }

        statusText.text = builder.ToString();
    }
}