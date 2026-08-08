using UnityEngine;

/// <summary>
/// Plays once when an existing briefing sequence first becomes visible. Page
/// changes leave visibility true and deliberately do not replay the cue.
/// </summary>
public sealed class BriefingAppearAudio : MonoBehaviour
{
    [SerializeField] private BattleBriefingController briefingController;

    private bool wasVisible;

    public BattleBriefingController BriefingController => briefingController;

    public void Configure(BattleBriefingController configuredController)
    {
        briefingController = configuredController;
    }

    private void Awake()
    {
        if (briefingController == null)
        {
            briefingController = GetComponent<BattleBriefingController>();
        }

        // Always begin false so a briefing opened during another component's
        // Awake still receives its one first-visible cue in Update.
        wasVisible = false;
    }

    private void Update()
    {
        bool isVisible = briefingController != null &&
                         briefingController.IsBriefingVisible;
        if (isVisible && !wasVisible)
        {
            PersistentAudioManager.PlayBriefingAppear();
        }

        wasVisible = isVisible;
    }
}
