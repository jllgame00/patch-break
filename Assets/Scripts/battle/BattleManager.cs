using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class BattleManager : MonoBehaviour
{
    [Header("Combat")]
    [SerializeField] private Health heroHealth;
    [SerializeField] private Health enemyHealth;
    [SerializeField] private ProgramRuntime runtime;
    [SerializeField] private LivePatchController livePatchController;
    [SerializeField] private RuntimeConsoleUI consoleUI;

    [Header("Result UI")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultTitle;
    [SerializeField] private TMP_Text resultBody;
    [SerializeField] private Button restartButton;
    
    [Header("Encounter")]
    [SerializeField]
    private string enemyDisplayName = "TARGET_PROCESS";

    [Header("Progression")]
    [SerializeField] private string nextSceneName;

    private bool battleEnded;
    private bool resultActionTriggered;
    private bool shouldLoadNextScene;
    private float defaultFixedDeltaTime;
    private TMP_Text restartButtonLabel;

    private void Awake()
    {
        defaultFixedDeltaTime = Time.fixedDeltaTime;

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        if (restartButton != null)
        {
            restartButtonLabel =
                restartButton.GetComponentInChildren<TMP_Text>(true);

            restartButton.onClick.AddListener(
                HandleResultAction
            );
        }
    }

    private void Start()
    {
        if (heroHealth == null ||
            enemyHealth == null)
        {
            Debug.LogError(
                "BattleManager: Health reference is missing."
            );

            enabled = false;
            return;
        }

        heroHealth.Died += HandleHeroDied;
        enemyHealth.Died += HandleEnemyDied;
    }

    private void OnDestroy()
    {
        if (heroHealth != null)
        {
            heroHealth.Died -= HandleHeroDied;
        }

        if (enemyHealth != null)
        {
            enemyHealth.Died -= HandleEnemyDied;
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(
                HandleResultAction
            );
        }
    }

    private void HandleHeroDied(Health health)
    {
        FinishBattle(victory: false);
    }

    private void HandleEnemyDied(Health health)
    {
        FinishBattle(victory: true);
    }

    private void FinishBattle(bool victory)
    {
        if (battleEnded)
            return;

        battleEnded = true;

        if (runtime != null)
        {
            runtime.StopProgram();
        }

        if (livePatchController != null)
        {
            livePatchController.CancelForBattleEnd();
        }

        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;

        if (consoleUI != null)
        {
            consoleUI.SetBattleEnded(victory);
        }

        if (resultTitle != null)
        {
            resultTitle.text =
                victory
                    ? "BUILD PASSED"
                    : "RUNTIME FAILURE";
        }

        if (resultBody != null)
        {
            resultBody.text =
                victory
                    ? "TARGET PROCESS TERMINATED\n" +
                      $"{enemyDisplayName} CLEARED"
                    : "HERO_RUNTIME.EXE HAS STOPPED\n" +
                      "REVISE YOUR PROGRAM";
        }

        ConfigureResultAction(victory);

        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        if (EventSystem.current != null &&
            restartButton != null)
        {
            EventSystem.current.SetSelectedGameObject(
                restartButton.gameObject
            );
        }

        Debug.Log(
            victory
                ? "BATTLE RESULT: VICTORY"
                : "BATTLE RESULT: DEFEAT"
        );
    }

    private void ConfigureResultAction(bool victory)
    {
        shouldLoadNextScene =
            victory &&
            TryGetLoadableNextScene(out _);

        SetRestartButtonLabel(
            !victory
                ? "RESTART PROGRAM"
                : shouldLoadNextScene
                    ? "NEXT PROCESS"
                    : "RUN AGAIN"
        );
    }

    private void SetRestartButtonLabel(string value)
    {
        if (restartButtonLabel != null)
        {
            restartButtonLabel.text = value;
        }
    }

    private bool TryGetLoadableNextScene(out string sceneName)
    {
        sceneName = (nextSceneName ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(sceneName))
            return false;

        if (Application.CanStreamedLevelBeLoaded(sceneName))
            return true;

        Debug.LogError(
            $"BattleManager: Next scene '{sceneName}' cannot be " +
            "loaded. Ensure it is included in the Build Profile. " +
            "The result action will restart the current scene."
        );

        sceneName = string.Empty;
        return false;
    }

    private void HandleResultAction()
    {
        if (!battleEnded || resultActionTriggered)
            return;

        resultActionTriggered = true;

        if (restartButton != null)
        {
            restartButton.interactable = false;
        }

        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;

        if (shouldLoadNextScene &&
            TryGetLoadableNextScene(out string sceneName))
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        RestartBattle();
    }

    private void RestartBattle()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;

        Scene currentScene =
            SceneManager.GetActiveScene();

        SceneManager.LoadScene(
            currentScene.buildIndex
        );
    }
}
