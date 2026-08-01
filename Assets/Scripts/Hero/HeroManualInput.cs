using UnityEngine;

[RequireComponent(typeof(HeroController))]
[RequireComponent(typeof(HeroActionExecutor))]
public sealed class HeroManualInput : MonoBehaviour
{
    private HeroController controller;
    private HeroActionExecutor executor;

    private void Awake()
    {
        controller = GetComponent<HeroController>();
        executor = GetComponent<HeroActionExecutor>();
    }

    private void Update()
    {
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        controller.SetMoveInput(horizontalInput);

        if (Input.GetKeyDown(KeyCode.J))
        {
            executor.TryExecute(HeroActionType.Slash);
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            executor.TryExecute(HeroActionType.DashForward);
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            executor.TryExecute(HeroActionType.DashBack);
        }
    }

    private void OnDisable()
    {
        if (controller != null)
        {
            controller.SetMoveInput(0f);
        }
    }
}