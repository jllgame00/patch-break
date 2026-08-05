using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class EndingScreenController : MonoBehaviour
{
    [Header("Progression")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Copy")]
    [SerializeField] private string title = "PATCH COMPLETE";
    [SerializeField] private string subtitle = "SYSTEM STABILIZED";

    private bool transitionRequested;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle buttonStyle;

    private void Awake()
    {
        Time.timeScale = 1f;
    }

    private void OnGUI()
    {
        EnsureStyles();

        float width = Mathf.Min(640f, Screen.width - 40f);
        float left = (Screen.width - width) * 0.5f;
        float centerY = Screen.height * 0.5f;

        GUI.Label(
            new Rect(left, centerY - 100f, width, 48f),
            title,
            titleStyle
        );

        GUI.Label(
            new Rect(left, centerY - 35f, width, 32f),
            subtitle,
            subtitleStyle
        );

        if (GUI.Button(
                new Rect(left + (width - 220f) * 0.5f, centerY + 35f, 220f, 46f),
                "RETURN TO MAIN MENU",
                buttonStyle))
        {
            LoadMainMenu();
        }
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
            return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 32,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = Color.white;

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18
        };
        subtitleStyle.normal.textColor = new Color(0.6f, 0.85f, 1f);

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
    }

    private void LoadMainMenu()
    {
        if (transitionRequested)
            return;

        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError(
                $"EndingScreenController: Main menu scene " +
                $"'{mainMenuSceneName}' " +
                "cannot be loaded. Ensure it is included in the Build Profile."
            );
            return;
        }

        transitionRequested = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
