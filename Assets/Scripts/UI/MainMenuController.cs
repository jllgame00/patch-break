using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    [Serializable]
    private class ProloguePage
    {
        public string header;

        [TextArea(3, 8)]
        public string body;

        public string buttonLabel = "NEXT";
    }

    [Header("Roots")]
    [SerializeField] private GameObject mainMenuRoot;
    [SerializeField] private GameObject prologueRoot;
    [SerializeField] private GameObject howToPlayPanel;
    [SerializeField] private GameObject creditsPanel;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button gameStartButton;
    [SerializeField] private Button howToPlayButton;
    [SerializeField] private Button creditsButton;

    [Header("Prologue")]
    [SerializeField] private TMP_Text prologueHeader;
    [SerializeField] private TMP_Text prologueBody;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text nextButtonLabel;
    [SerializeField] private ProloguePage[] prologuePages;

    [Header("Localization")]
    [SerializeField] private TMP_Text[] koreanTextTargets;
    [SerializeField] private string koreanFontResourceName = "NotoSansKRMenu";

    [Header("Panel Buttons")]
    [SerializeField] private Button howToPlayBackButton;
    [SerializeField] private Button creditsBackButton;

    [Header("Progression")]
    [SerializeField] private string firstBattleSceneName = "Battle";

    [SerializeField, Min(0f)] private float pageAdvanceCooldown = 0.15f;

    private int currentProloguePage = -1;
    private float nextPageAllowedAt;
    private bool sceneLoadRequested;

    private void Awake()
    {
        Time.timeScale = 1f;

        if (!HasRequiredReferences())
        {
            Debug.LogError(
                "MainMenuController: Required menu UI reference is missing."
            );
            enabled = false;
            return;
        }

        if (!ApplyKoreanFont())
        {
            enabled = false;
            return;
        }

        gameStartButton.onClick.AddListener(HandleGameStart);
        howToPlayButton.onClick.AddListener(ShowHowToPlay);
        creditsButton.onClick.AddListener(ShowCredits);
        nextButton.onClick.AddListener(HandleNextPage);
        howToPlayBackButton.onClick.AddListener(ShowMainMenu);
        creditsBackButton.onClick.AddListener(ShowMainMenu);

        ShowMainMenu();
    }

    private void Start()
    {
        SelectButton(gameStartButton);
    }

    private void OnDestroy()
    {
        if (gameStartButton != null)
            gameStartButton.onClick.RemoveListener(HandleGameStart);

        if (howToPlayButton != null)
            howToPlayButton.onClick.RemoveListener(ShowHowToPlay);

        if (creditsButton != null)
            creditsButton.onClick.RemoveListener(ShowCredits);

        if (nextButton != null)
            nextButton.onClick.RemoveListener(HandleNextPage);

        if (howToPlayBackButton != null)
            howToPlayBackButton.onClick.RemoveListener(ShowMainMenu);

        if (creditsBackButton != null)
            creditsBackButton.onClick.RemoveListener(ShowMainMenu);
    }

    private bool HasRequiredReferences()
    {
        return mainMenuRoot != null &&
               prologueRoot != null &&
               howToPlayPanel != null &&
               creditsPanel != null &&
               gameStartButton != null &&
               howToPlayButton != null &&
               creditsButton != null &&
               prologueHeader != null &&
               prologueBody != null &&
               nextButton != null &&
               nextButtonLabel != null &&
               howToPlayBackButton != null &&
               creditsBackButton != null &&
               prologuePages != null &&
               prologuePages.Length > 0 &&
               HasKoreanTextTargets() &&
               !string.IsNullOrWhiteSpace(firstBattleSceneName);
    }

    private bool HasKoreanTextTargets()
    {
        if (koreanTextTargets == null ||
            koreanTextTargets.Length == 0 ||
            string.IsNullOrWhiteSpace(koreanFontResourceName))
        {
            return false;
        }

        foreach (TMP_Text target in koreanTextTargets)
        {
            if (target == null)
                return false;
        }

        return true;
    }

    private bool ApplyKoreanFont()
    {
        Font sourceFont = Resources.Load<Font>(koreanFontResourceName);

        if (sourceFont == null)
        {
            Debug.LogError(
                $"MainMenuController: Korean font resource " +
                $"'{koreanFontResourceName}' is missing."
            );
            return false;
        }

        TMP_FontAsset koreanFont =
            TMP_FontAsset.CreateFontAsset(sourceFont);

        if (koreanFont == null)
        {
            Debug.LogError(
                "MainMenuController: Failed to create the Korean TMP font."
            );
            return false;
        }

        foreach (TMP_Text target in koreanTextTargets)
        {
            target.font = koreanFont;
            target.SetAllDirty();
        }

        return true;
    }

    private void HandleGameStart()
    {
        if (sceneLoadRequested ||
            !mainMenuRoot.activeSelf)
        {
            return;
        }

        currentProloguePage = 0;
        nextPageAllowedAt = Time.unscaledTime + pageAdvanceCooldown;

        mainMenuRoot.SetActive(false);
        howToPlayPanel.SetActive(false);
        creditsPanel.SetActive(false);
        prologueRoot.SetActive(true);

        RefreshProloguePage();
        SelectButton(nextButton);
    }

    private void HandleNextPage()
    {
        if (sceneLoadRequested ||
            !prologueRoot.activeSelf ||
            Time.unscaledTime < nextPageAllowedAt)
        {
            return;
        }

        nextPageAllowedAt = Time.unscaledTime + pageAdvanceCooldown;

        if (currentProloguePage >= prologuePages.Length - 1)
        {
            LoadFirstBattle();
            return;
        }

        currentProloguePage++;
        RefreshProloguePage();
    }

    private void RefreshProloguePage()
    {
        if (currentProloguePage < 0 ||
            currentProloguePage >= prologuePages.Length)
        {
            Debug.LogError(
                "MainMenuController: Prologue page index is out of range."
            );
            return;
        }

        ProloguePage page = prologuePages[currentProloguePage];
        prologueHeader.text = page.header;
        prologueBody.text = page.body;
        nextButtonLabel.text = page.buttonLabel;
    }

    private void ShowHowToPlay()
    {
        if (sceneLoadRequested)
            return;

        mainMenuRoot.SetActive(false);
        prologueRoot.SetActive(false);
        creditsPanel.SetActive(false);
        howToPlayPanel.SetActive(true);
        SelectButton(howToPlayBackButton);
    }

    private void ShowCredits()
    {
        if (sceneLoadRequested)
            return;

        mainMenuRoot.SetActive(false);
        prologueRoot.SetActive(false);
        howToPlayPanel.SetActive(false);
        creditsPanel.SetActive(true);
        SelectButton(creditsBackButton);
    }

    private void ShowMainMenu()
    {
        if (sceneLoadRequested)
            return;

        currentProloguePage = -1;
        mainMenuRoot.SetActive(true);
        prologueRoot.SetActive(false);
        howToPlayPanel.SetActive(false);
        creditsPanel.SetActive(false);
        SelectButton(gameStartButton);
    }

    private void LoadFirstBattle()
    {
        if (sceneLoadRequested)
            return;

        if (!Application.CanStreamedLevelBeLoaded(firstBattleSceneName))
        {
            Debug.LogError(
                $"MainMenuController: First battle scene " +
                $"'{firstBattleSceneName}' cannot be loaded. Ensure it " +
                "is included in the Build Profile."
            );
            return;
        }

        sceneLoadRequested = true;
        nextButton.interactable = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(firstBattleSceneName);
    }

    private static void SelectButton(Button button)
    {
        if (EventSystem.current != null && button != null)
        {
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
    }
}
