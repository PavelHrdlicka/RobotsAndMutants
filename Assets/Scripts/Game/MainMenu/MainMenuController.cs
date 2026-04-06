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

        // Fix button hover: Image.color must be white, Button.colors handles tinting.
        foreach (var btn in GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            var img = btn.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
                img.color = Color.white;
        }
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

    private bool showQuitConfirm;

    public void QuitGame()
    {
        showQuitConfirm = true;
    }

    private void DoQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Update()
    {
        // Block Canvas raycasts while quit modal is visible.
        var canvas = GetComponentInChildren<UnityEngine.UI.GraphicRaycaster>();
        if (canvas != null)
            canvas.enabled = !showQuitConfirm;
    }

    private int quitModalId = 12345;

    private void OnGUI()
    {
        if (!showQuitConfirm) return;

        // Dark overlay behind modal.
        GUI.color = new Color(0, 0, 0, 0.85f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = 320f, h = 140f;
        float x = (Screen.width - w) * 0.5f;
        float y = (Screen.height - h) * 0.5f;

        // ModalWindow blocks all OnGUI input outside itself.
        GUI.ModalWindow(quitModalId, new Rect(x, y, w, h), DrawQuitModal, GUIContent.none);
    }

    private void DrawQuitModal(int id)
    {
        var labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        GUI.Label(new Rect(0, 15, 320, 30), "Quit Game?", labelStyle);

        var subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
        };
        GUI.Label(new Rect(0, 45, 320, 24), "Are you sure you want to quit?", subStyle);

        var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
        float btnW = 120f, btnH = 36f;
        float btnY = 140f - btnH - 16f;

        if (GUI.Button(new Rect(320f * 0.5f - btnW - 8, btnY, btnW, btnH), "Quit", btnStyle))
            DoQuit();
        if (GUI.Button(new Rect(320f * 0.5f + 8, btnY, btnW, btnH), "Cancel", btnStyle))
            showQuitConfirm = false;
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
