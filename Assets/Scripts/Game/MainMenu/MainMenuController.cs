using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls the main menu UI panels and scene transitions.
/// Attached to the MainMenu Canvas root in the MainMenu scene.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject playPanel;
    [SerializeField] private GameObject replaysPanel;
    [SerializeField] private GameObject settingsPanel;

    private void Start()
    {
        ShowMain();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ── Panel navigation ────────────────────────────────────────────────

    public void ShowMain()
    {
        SetActivePanel(mainPanel);
    }

    public void ShowPlay()
    {
        SetActivePanel(playPanel);
    }

    public void ShowReplays()
    {
        SetActivePanel(replaysPanel);
    }

    public void ShowSettings()
    {
        SetActivePanel(settingsPanel);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Game launch ─────────────────────────────────────────────────────

    public void StartMatch(Team humanTeam, int boardSize, int difficulty)
    {
        GameModeConfig.CurrentMode = GameMode.HumanVsAI;
        GameModeConfig.HumanTeam = humanTeam;
        GameModeConfig.BoardSize = boardSize;
        GameModeConfig.AIDifficulty = difficulty;
        GameModeConfig.LaunchedFromMenu = true;
        GameModeConfig.AITurnDelay = SettingsPanel.GetAITurnDelay();

#if UNITY_EDITOR
        UnityEditor.SessionState.SetString("GameMode", "HumanVsAI");
        UnityEditor.SessionState.SetString("HumanTeam", humanTeam.ToString());
#endif

        SceneManager.LoadScene("SampleScene");
    }

    public void WatchReplay(string replayPath)
    {
        GameModeConfig.CurrentMode = GameMode.Replay;
        ReplayPlayer.PendingReplayPath = replayPath;

#if UNITY_EDITOR
        UnityEditor.SessionState.SetString("GameMode", "Replay");
        UnityEditor.SessionState.SetString("ReplayPlayer_PendingPath", replayPath);
#endif

        SceneManager.LoadScene("SampleScene");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void SetActivePanel(GameObject panel)
    {
        if (mainPanel != null) mainPanel.SetActive(panel == mainPanel);
        if (playPanel != null) playPanel.SetActive(panel == playPanel);
        if (replaysPanel != null) replaysPanel.SetActive(panel == replaysPanel);
        if (settingsPanel != null) settingsPanel.SetActive(panel == settingsPanel);
    }

    /// <summary>Navigate back to main menu from the game scene.</summary>
    public static void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
