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

    private bool battleEnded;
    private float defaultFixedDeltaTime;

    private void Awake()
    {
        defaultFixedDeltaTime = Time.fixedDeltaTime;

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(
                RestartBattle
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
                RestartBattle
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
                      "GOLEM ENCOUNTER CLEARED"
                    : "HERO_RUNTIME.EXE HAS STOPPED\n" +
                      "REVISE YOUR PROGRAM";
        }

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