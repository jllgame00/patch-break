using UnityEngine;

/// <summary>
/// Presentation-only walk audio. It observes CharacterPoseController's live
/// Walk visual state and never reads or changes transform, Rigidbody, or
/// gameplay state.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class CharacterFootstepAudio : MonoBehaviour
{
    [SerializeField] private CharacterPoseController poseController;
    [SerializeField] private AudioClip footstepClip;

    [Header("Cadence")]
    [SerializeField, Min(0.01f)] private float stepIntervalMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float minimumInterval = 0.01f;
    [SerializeField, Range(0f, 1f)]
    private float clipLengthMinimumIntervalMultiplier = 0.5f;
    [SerializeField, Min(0f)] private float initialDelay = 0.05f;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.35f;
    [SerializeField, Range(0.5f, 2f)] private float minimumPitch = 0.97f;
    [SerializeField, Range(0.5f, 2f)] private float maximumPitch = 1.03f;

    private bool wasWalking;
    private float nextStepTime;

    public CharacterPoseController PoseController => poseController;
    public AudioClip FootstepClip => footstepClip;
    public float StepIntervalMultiplier => stepIntervalMultiplier;
    public float MinimumInterval => minimumInterval;
    public float ClipLengthMinimumIntervalMultiplier =>
        clipLengthMinimumIntervalMultiplier;
    public float InitialDelay => initialDelay;
    public float Volume => volume;
    public float MinimumPitch => minimumPitch;
    public float MaximumPitch => maximumPitch;

    /// <summary>
    /// The current configured cadence. Two steps per animation cycle are the
    /// baseline, bounded by the clip-overlap policy for long supplied clips.
    /// </summary>
    public float StepInterval
    {
        get
        {
            float cycleDuration = GetWalkCycleDuration();
            float nominalInterval = cycleDuration * 0.5f *
                                    Mathf.Max(0.01f, stepIntervalMultiplier);
            float clipBound = footstepClip == null
                ? 0f
                : footstepClip.length * Mathf.Clamp01(
                    clipLengthMinimumIntervalMultiplier
                );
            return Mathf.Max(minimumInterval, nominalInterval, clipBound);
        }
    }

    public void Configure(
        CharacterPoseController configuredPoseController,
        AudioClip configuredFootstepClip,
        float configuredStepIntervalMultiplier,
        float configuredMinimumInterval,
        float configuredClipLengthMinimumIntervalMultiplier,
        float configuredInitialDelay,
        float configuredVolume,
        float configuredMinimumPitch,
        float configuredMaximumPitch)
    {
        poseController = configuredPoseController;
        footstepClip = configuredFootstepClip;
        stepIntervalMultiplier = Mathf.Max(
            0.01f,
            configuredStepIntervalMultiplier
        );
        minimumInterval = Mathf.Max(0.01f, configuredMinimumInterval);
        clipLengthMinimumIntervalMultiplier = Mathf.Clamp01(
            configuredClipLengthMinimumIntervalMultiplier
        );
        initialDelay = Mathf.Max(0f, configuredInitialDelay);
        volume = Mathf.Clamp01(configuredVolume);
        minimumPitch = Mathf.Clamp(configuredMinimumPitch, 0.5f, 2f);
        maximumPitch = Mathf.Max(
            minimumPitch,
            Mathf.Clamp(configuredMaximumPitch, 0.5f, 2f)
        );
        ResetCadence();
    }

    private void OnEnable()
    {
        ResetCadence();
    }

    private void OnDisable()
    {
        ResetCadence();
    }

    private void Update()
    {
        bool isWalking = footstepClip != null &&
                         poseController != null &&
                         poseController.IsWalking;
        if (!isWalking)
        {
            if (wasWalking)
            {
                ResetCadence();
            }

            return;
        }

        if (!wasWalking)
        {
            wasWalking = true;
            nextStepTime = Time.time + initialDelay;
        }

        if (Time.time < nextStepTime)
        {
            return;
        }

        PersistentAudioManager.PlayFootstep(
            this,
            footstepClip,
            volume,
            minimumPitch,
            maximumPitch
        );
        nextStepTime = Time.time + StepInterval;
    }

    private void ResetCadence()
    {
        wasWalking = false;
        nextStepTime = float.PositiveInfinity;
        PersistentAudioManager.StopFootsteps(this);
    }

    private float GetWalkCycleDuration()
    {
        if (poseController == null ||
            poseController.WalkFrames == null ||
            poseController.WalkFrames.Length == 0)
        {
            return minimumInterval * 2f;
        }

        return poseController.WalkFrames.Length /
               Mathf.Max(0.01f, poseController.WalkFramesPerSecond);
    }
}
