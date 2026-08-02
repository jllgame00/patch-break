using UnityEngine;

[RequireComponent(typeof(HeroActionExecutor))]
public sealed class HeroOverrideController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ProgramRuntime runtime;
    [SerializeField] private HeroActionExecutor executor;

    [Header("Override")]
    [SerializeField, Min(1)]
    private int maxCharges = 3;

    [SerializeField]
    private KeyCode dashBackKey = KeyCode.Q;

    public int RemainingCharges { get; private set; }
    public int MaxCharges => maxCharges;

    private void Awake()
    {
        if (executor == null)
        {
            executor = GetComponent<HeroActionExecutor>();
        }

        if (runtime == null)
        {
            runtime = GetComponent<ProgramRuntime>();
        }
    }

    private void Start()
    {
        ResetCharges();
    }

    private void Update()
    {
        if (runtime == null || !runtime.IsRunning)
        {
            return;
        }

        if (RemainingCharges <= 0)
        {
            return;
        }

        if (Input.GetKeyDown(dashBackKey))
        {
            TryUseDashBackOverride();
        }
    }

    public void ResetCharges()
    {
        RemainingCharges = maxCharges;
    }

    private void TryUseDashBackOverride()
    {
        Transform target =
            runtime == null
                ? null
                : runtime.Target;

        bool executed = executor.ForceExecute(
            HeroActionType.DashBack,
            target
        );

        if (!executed)
        {
            return;
        }

        RemainingCharges--;

        Debug.Log(
            $"OVERRIDE EXECUTED: DASH_BACK " +
            $"({RemainingCharges}/{maxCharges} remaining)"
        );
    }
}
