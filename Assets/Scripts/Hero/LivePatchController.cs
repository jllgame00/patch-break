using UnityEngine;

[RequireComponent(typeof(ProgramRuntime))]
public sealed class LivePatchController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ProgramRuntime runtime;
    [SerializeField] private RuntimeConsoleUI consoleUI;

    [Header("Live Patch")]
    [SerializeField] private KeyCode patchKey = KeyCode.Space;

    [SerializeField, Min(1)]
    private int maxUses = 1;

    [SerializeField, Range(0.01f, 0.5f)]
    private float slowMotionScale = 0.08f;

    public bool IsPatching { get; private set; }
    public int RemainingUses { get; private set; }
    public int MaxUses => maxUses;

    private float previousTimeScale = 1f;
    private float previousFixedDeltaTime = 0.02f;

    private void Awake()
    {
        if (runtime == null)
        {
            runtime = GetComponent<ProgramRuntime>();
        }
    }

    private void Start()
    {
        RemainingUses = maxUses;
    }

    private void Update()
    {
        if (IsPatching)
            return;

        if (RemainingUses <= 0)
            return;

        if (runtime == null || !runtime.IsRunning)
            return;

        if (Input.GetKeyDown(patchKey))
        {
            BeginLivePatch();
        }
    }

    public void HandleCompileResult(bool succeeded)
    {
        if (!IsPatching)
            return;

        // 문법 오류가 있으면 슬로모션 상태에서
        // 계속 코드를 수정할 수 있게 유지한다.
        if (!succeeded)
            return;

        RemainingUses--;
        EndLivePatch();

        Debug.Log(
            $"LIVE PATCH APPLIED " +
            $"({RemainingUses}/{maxUses} remaining)"
        );
    }

    private void BeginLivePatch()
    {
        if (consoleUI == null)
        {
            Debug.LogError(
                "LivePatchController: Console UI is not assigned."
            );

            return;
        }

        IsPatching = true;

        previousTimeScale = Time.timeScale;
        previousFixedDeltaTime = Time.fixedDeltaTime;

        Time.timeScale = slowMotionScale;
        Time.fixedDeltaTime =
            previousFixedDeltaTime * slowMotionScale;

        consoleUI.EnterLivePatchMode();

        Debug.Log("LIVE PATCH MODE ENTERED");
    }

    private void EndLivePatch()
    {
        RestoreTime();

        IsPatching = false;

        if (consoleUI != null)
        {
            consoleUI.ExitLivePatchMode();
        }
    }

    private void RestoreTime()
    {
        Time.timeScale = previousTimeScale;
        Time.fixedDeltaTime = previousFixedDeltaTime;
    }
    
    public void CancelForBattleEnd()
    {
        if (!IsPatching)
            return;

        RestoreTime();
        IsPatching = false;

        if (consoleUI != null)
        {
            consoleUI.ExitLivePatchMode();
        }

        Debug.Log("LIVE PATCH CANCELLED: BATTLE ENDED");
    }

    private void OnDisable()
    {
        if (!IsPatching)
            return;

        RestoreTime();
        IsPatching = false;
    }
}