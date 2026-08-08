using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-local visual listener for confirmed Health damage. The manager keeps
/// Health gameplay generic by subscribing to its existing Damaged event and
/// renders only pooled, world-space sprite sequences at the damaged actor.
/// </summary>
public sealed class HitVfxManager : MonoBehaviour
{
    private struct PendingHit
    {
        public Health Target;
        public Vector3 FallbackPosition;
    }

    [Header("Confirmed Damage Targets")]
    [SerializeField] private Health[] observedHealth = System.Array.Empty<Health>();

    [Header("Visual Sequences")]
    [SerializeField] private Sprite[] normalFrames = System.Array.Empty<Sprite>();
    [SerializeField] private Sprite[] strongFrames = System.Array.Empty<Sprite>();

    [SerializeField, Min(0.01f)] private float normalFramesPerSecond = 15f;
    [SerializeField, Min(0.01f)] private float strongFramesPerSecond = 15f;

    [Header("Reusable Slots")]
    [SerializeField] private HitVfxSlot[] slots = System.Array.Empty<HitVfxSlot>();

    private static readonly List<HitVfxManager> ActiveManagers = new();
    private readonly List<Health> subscribedHealth = new();
    private readonly List<PendingHit> pendingHits = new();
    private int nextSlotIndex;

    public Health[] ObservedHealth => observedHealth;
    public Sprite[] NormalFrames => normalFrames;
    public Sprite[] StrongFrames => strongFrames;
    public float NormalFramesPerSecond => normalFramesPerSecond;
    public float StrongFramesPerSecond => strongFramesPerSecond;
    public HitVfxSlot[] Slots => slots;

    public void Configure(
        Health[] targets,
        Sprite[] normal,
        Sprite[] strong,
        float normalFps,
        float strongFps,
        HitVfxSlot[] configuredSlots)
    {
        UnsubscribeFromDamage();

        observedHealth = targets ?? System.Array.Empty<Health>();
        normalFrames = normal ?? System.Array.Empty<Sprite>();
        strongFrames = strong ?? System.Array.Empty<Sprite>();
        normalFramesPerSecond = Mathf.Max(0.01f, normalFps);
        strongFramesPerSecond = Mathf.Max(0.01f, strongFps);
        slots = configuredSlots ?? System.Array.Empty<HitVfxSlot>();
        nextSlotIndex = 0;

        if (Application.isPlaying && isActiveAndEnabled)
        {
            SubscribeToDamage();
        }
    }

    /// <summary>
    /// Plays the already-configured strong sequence for an explicit gameplay
    /// category. It is intentionally never inferred from raw damage values.
    /// </summary>
    public void PlayStrongAt(Vector3 worldPosition)
    {
        Play(strongFrames, strongFramesPerSecond, worldPosition);
    }

    /// <summary>
    /// Reports the exact contact supplied by an already-confirmed damage path.
    /// If a scene has no configured manager for the target, this is a no-op.
    /// </summary>
    public static void ReportConfirmedHit(
        Health damagedHealth,
        Vector3 worldPosition)
    {
        ReportConfirmed(
            damagedHealth,
            worldPosition,
            strong: false
        );
    }

    /// <summary>
    /// Exact-contact variant used only by an existing, named special attack.
    /// It does not create a new damage category or change Health resolution.
    /// </summary>
    public static void ReportConfirmedStrongHit(
        Health damagedHealth,
        Vector3 worldPosition)
    {
        ReportConfirmed(
            damagedHealth,
            worldPosition,
            strong: true
        );
    }

    private static void ReportConfirmed(
        Health damagedHealth,
        Vector3 worldPosition,
        bool strong)
    {
        for (int index = ActiveManagers.Count - 1; index >= 0; index--)
        {
            HitVfxManager manager = ActiveManagers[index];
            if (manager == null)
            {
                ActiveManagers.RemoveAt(index);
                continue;
            }

            manager.TryPlayReportedHit(
                damagedHealth,
                worldPosition,
                strong
            );
        }
    }

    private void OnEnable()
    {
        if (!ActiveManagers.Contains(this))
        {
            ActiveManagers.Add(this);
        }

        SubscribeToDamage();
    }

    private void OnDisable()
    {
        ActiveManagers.Remove(this);
        UnsubscribeFromDamage();
        pendingHits.Clear();
    }

    private void HandleDamaged(Health damagedHealth, int appliedDamage)
    {
        if (damagedHealth == null || appliedDamage <= 0)
        {
            return;
        }

        // This is the same confirmed Health reduction that drives the VFX.
        // Misses, invulnerability, and complete guards never reach here.
        PersistentAudioManager.PlayHit();

        SpriteRenderer victimRenderer =
            damagedHealth.GetComponent<SpriteRenderer>();
        if (victimRenderer == null)
        {
            Debug.LogWarning(
                "PATCH//BREAK Hit VFX skipped: damaged target '" +
                damagedHealth.name + "' has no root SpriteRenderer."
            );
            return;
        }

        // Damage does not expose a contact point in the current gameplay API.
        // The current visual bounds center is the stable victim-centered
        // fallback and stays correct while CharacterPose swaps sprites.
        Vector3 fallbackPosition = victimRenderer.bounds.center;
        fallbackPosition.z = transform.position.z;
        pendingHits.Add(
            new PendingHit
            {
                Target = damagedHealth,
                FallbackPosition = fallbackPosition
            }
        );
    }

    private void Play(
        Sprite[] frames,
        float framesPerSecond,
        Vector3 worldPosition)
    {
        if (frames == null || frames.Length == 0 ||
            slots == null || slots.Length == 0)
        {
            return;
        }

        HitVfxSlot slot = FindAvailableSlot();
        if (slot == null)
        {
            Debug.LogWarning(
                "PATCH//BREAK Hit VFX skipped: pool has no valid slot."
            );
            return;
        }

        slot.Play(frames, framesPerSecond, worldPosition);
    }

    private HitVfxSlot FindAvailableSlot()
    {
        for (int index = 0; index < slots.Length; index++)
        {
            int candidateIndex = (nextSlotIndex + index) % slots.Length;
            HitVfxSlot candidate = slots[candidateIndex];
            if (candidate != null && !candidate.IsPlaying)
            {
                nextSlotIndex = (candidateIndex + 1) % slots.Length;
                return candidate;
            }
        }

        // Four simultaneous hits are beyond the normal battle cadence. Reuse
        // the oldest round-robin slot rather than instantiate a new object.
        HitVfxSlot fallback = slots[nextSlotIndex];
        nextSlotIndex = (nextSlotIndex + 1) % slots.Length;
        return fallback;
    }

    private bool TryPlayReportedHit(
        Health damagedHealth,
        Vector3 worldPosition,
        bool strong)
    {
        if (damagedHealth == null)
        {
            return false;
        }

        for (int index = pendingHits.Count - 1; index >= 0; index--)
        {
            if (pendingHits[index].Target != damagedHealth)
            {
                continue;
            }

            pendingHits.RemoveAt(index);
            worldPosition.z = transform.position.z;
            Play(
                strong ? strongFrames : normalFrames,
                strong ? strongFramesPerSecond : normalFramesPerSecond,
                worldPosition
            );
            return true;
        }

        return false;
    }

    private void LateUpdate()
    {
        // A direct damage source reports an exact contact during the same
        // call stack. Any other future Health user still receives a safe,
        // victim-centered fallback once for its confirmed damage event.
        foreach (PendingHit pending in pendingHits)
        {
            Play(
                normalFrames,
                normalFramesPerSecond,
                pending.FallbackPosition
            );
        }

        pendingHits.Clear();
    }

    private void SubscribeToDamage()
    {
        UnsubscribeFromDamage();

        if (observedHealth == null)
        {
            return;
        }

        foreach (Health health in observedHealth)
        {
            if (health == null || subscribedHealth.Contains(health))
            {
                continue;
            }

            health.Damaged += HandleDamaged;
            subscribedHealth.Add(health);
        }
    }

    private void UnsubscribeFromDamage()
    {
        foreach (Health health in subscribedHealth)
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
            }
        }

        subscribedHealth.Clear();
    }
}
