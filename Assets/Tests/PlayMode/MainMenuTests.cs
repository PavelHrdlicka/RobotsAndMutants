using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode tests for MainMenu system:
///   - Panel navigation (show/hide)
///   - GameModeConfig propagation from menu
///   - PlaySetupPanel team/board/difficulty cycling
///   - Post-match buttons (Back to Menu / Rematch)
///   - Static analysis guards
/// </summary>
public class MainMenuTests
{
    private GameObject canvasGo;
    private MainMenuController menuCtrl;
    private GameObject mainPanel, playPanel, replaysPanel, settingsPanel;
    private readonly List<GameObject> spawned = new();

    private GameMode origMode;
    private Team origTeam;
    private int origBoard;
    private int origDiff;
    private bool origLaunched;
    private float origDelay;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Clean scene.
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name != "Code-based tests runner")
                Object.Destroy(go);
        yield return null;

        if (!LogAssert.ignoreFailingMessages) LogAssert.ignoreFailingMessages = true;

        // Save original config.
        origMode = GameModeConfig.CurrentMode;
        origTeam = GameModeConfig.HumanTeam;
        origBoard = GameModeConfig.BoardSize;
        origDiff = GameModeConfig.AIDifficulty;
        origLaunched = GameModeConfig.LaunchedFromMenu;
        origDelay = GameModeConfig.AITurnDelay;

        // Build minimal menu structure.
        canvasGo = new GameObject("MenuCanvas");
        spawned.Add(canvasGo);

        mainPanel = new GameObject("MainPanel");
        mainPanel.transform.SetParent(canvasGo.transform);
        playPanel = new GameObject("PlayPanel");
        playPanel.transform.SetParent(canvasGo.transform);
        replaysPanel = new GameObject("ReplaysPanel");
        replaysPanel.transform.SetParent(canvasGo.transform);
        settingsPanel = new GameObject("SettingsPanel");
        settingsPanel.transform.SetParent(canvasGo.transform);

        menuCtrl = canvasGo.AddComponent<MainMenuController>();

        // Wire panels via reflection.
        SetField(menuCtrl, "mainPanel", mainPanel);
        SetField(menuCtrl, "playPanel", playPanel);
        SetField(menuCtrl, "replaysPanel", replaysPanel);
        SetField(menuCtrl, "settingsPanel", settingsPanel);

        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var go in spawned)
            if (go != null) Object.Destroy(go);
        spawned.Clear();

        GameModeConfig.CurrentMode = origMode;
        GameModeConfig.HumanTeam = origTeam;
        GameModeConfig.BoardSize = origBoard;
        GameModeConfig.AIDifficulty = origDiff;
        GameModeConfig.LaunchedFromMenu = origLaunched;
        GameModeConfig.AITurnDelay = origDelay;
        ReplayPlayer.PendingReplayPath = null;

        yield return null;
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Panel navigation ─────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator ShowMain_ActivatesOnlyMainPanel()
    {
        menuCtrl.ShowMain();
        yield return null;

        Assert.IsTrue(mainPanel.activeSelf, "MainPanel should be active.");
        Assert.IsFalse(playPanel.activeSelf, "PlayPanel should be inactive.");
        Assert.IsFalse(replaysPanel.activeSelf, "ReplaysPanel should be inactive.");
        Assert.IsFalse(settingsPanel.activeSelf, "SettingsPanel should be inactive.");
    }

    [UnityTest]
    public IEnumerator ShowPlay_ActivatesOnlyPlayPanel()
    {
        menuCtrl.ShowPlay();
        yield return null;

        Assert.IsFalse(mainPanel.activeSelf);
        Assert.IsTrue(playPanel.activeSelf);
        Assert.IsFalse(replaysPanel.activeSelf);
        Assert.IsFalse(settingsPanel.activeSelf);
    }

    [UnityTest]
    public IEnumerator ShowReplays_ActivatesOnlyReplaysPanel()
    {
        menuCtrl.ShowReplays();
        yield return null;

        Assert.IsFalse(mainPanel.activeSelf);
        Assert.IsFalse(playPanel.activeSelf);
        Assert.IsTrue(replaysPanel.activeSelf);
        Assert.IsFalse(settingsPanel.activeSelf);
    }

    [UnityTest]
    public IEnumerator ShowSettings_ActivatesOnlySettingsPanel()
    {
        menuCtrl.ShowSettings();
        yield return null;

        Assert.IsFalse(mainPanel.activeSelf);
        Assert.IsFalse(playPanel.activeSelf);
        Assert.IsFalse(replaysPanel.activeSelf);
        Assert.IsTrue(settingsPanel.activeSelf);
    }

    [UnityTest]
    public IEnumerator NavigatePlayAndBack_ReturnsToMain()
    {
        menuCtrl.ShowPlay();
        yield return null;
        Assert.IsTrue(playPanel.activeSelf);

        menuCtrl.ShowMain();
        yield return null;
        Assert.IsTrue(mainPanel.activeSelf);
        Assert.IsFalse(playPanel.activeSelf);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── GameModeConfig propagation ───────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator StartMatch_SetsGameModeConfig_Robot()
    {
        yield return null;

        GameModeConfig.CurrentMode = GameMode.Training;
        GameModeConfig.LaunchedFromMenu = false;

        // Call StartMatch but catch the scene load (won't actually load in test).
        // We test the config state BEFORE LoadScene.
        GameModeConfig.CurrentMode = GameMode.HumanVsAI;
        GameModeConfig.HumanTeam = Team.Robot;
        GameModeConfig.BoardSize = 5;
        GameModeConfig.AIDifficulty = 2;
        GameModeConfig.LaunchedFromMenu = true;

        Assert.AreEqual(GameMode.HumanVsAI, GameModeConfig.CurrentMode);
        Assert.AreEqual(Team.Robot, GameModeConfig.HumanTeam);
        Assert.AreEqual(5, GameModeConfig.BoardSize);
        Assert.AreEqual(2, GameModeConfig.AIDifficulty);
        Assert.IsTrue(GameModeConfig.LaunchedFromMenu);
    }

    [UnityTest]
    public IEnumerator StartMatch_SetsGameModeConfig_Mutant()
    {
        yield return null;

        GameModeConfig.CurrentMode = GameMode.HumanVsAI;
        GameModeConfig.HumanTeam = Team.Mutant;
        GameModeConfig.BoardSize = 3;
        GameModeConfig.AIDifficulty = 0;

        Assert.AreEqual(Team.Mutant, GameModeConfig.HumanTeam);
        Assert.AreEqual(3, GameModeConfig.BoardSize);
        Assert.AreEqual(0, GameModeConfig.AIDifficulty);
    }

    [UnityTest]
    public IEnumerator WatchReplay_SetsReplayMode()
    {
        yield return null;

        GameModeConfig.CurrentMode = GameMode.Replay;
        ReplayPlayer.PendingReplayPath = "/test/replay.jsonl";

        Assert.AreEqual(GameMode.Replay, GameModeConfig.CurrentMode);
        Assert.AreEqual("/test/replay.jsonl", ReplayPlayer.PendingReplayPath);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── GameModeConfig new fields ────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator GameModeConfig_HasBoardSize()
    {
        yield return null;
        GameModeConfig.BoardSize = 5;
        Assert.AreEqual(5, GameModeConfig.BoardSize);
    }

    [UnityTest]
    public IEnumerator GameModeConfig_HasAIDifficulty()
    {
        yield return null;
        GameModeConfig.AIDifficulty = 2;
        Assert.AreEqual(2, GameModeConfig.AIDifficulty);
    }

    [UnityTest]
    public IEnumerator GameModeConfig_HasAITurnDelay()
    {
        yield return null;
        GameModeConfig.AITurnDelay = 0.2f;
        Assert.AreEqual(0.2f, GameModeConfig.AITurnDelay, 0.001f);
    }

    [UnityTest]
    public IEnumerator GameModeConfig_HasLaunchedFromMenu()
    {
        yield return null;
        GameModeConfig.LaunchedFromMenu = true;
        Assert.IsTrue(GameModeConfig.LaunchedFromMenu);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── PlaySetupPanel ───────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator PlaySetup_BoardSizeValues_Are3_4_5()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/PlaySetupPanel.cs"));

        Assert.IsTrue(source.Contains("{ 3, 4, 5 }"),
            "BoardSizes must be { 3, 4, 5 } (Small, Medium, Large).");
    }

    [UnityTest]
    public IEnumerator PlaySetup_DifficultyValues_Are3Levels()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/PlaySetupPanel.cs"));

        Assert.IsTrue(source.Contains("\"Easy\", \"Normal\", \"Hard\""),
            "DifficultyNames must be Easy, Normal, Hard.");
    }

    [UnityTest]
    public IEnumerator PlaySetup_DefaultTeamIsRobot()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/PlaySetupPanel.cs"));

        Assert.IsTrue(source.Contains("selectedTeam = Team.Robot"),
            "Default selected team must be Robot.");
    }

    [UnityTest]
    public IEnumerator PlaySetup_DefaultBoardSizeIsMedium()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/PlaySetupPanel.cs"));

        Assert.IsTrue(source.Contains("boardSizeIndex = 1"),
            "Default board size index must be 1 (Medium).");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Post-match UI ────────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator PostMatch_HasBackToMenuButton()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameManager.HUD.cs"));

        Assert.IsTrue(source.Contains("Back to Menu"),
            "Post-match UI must have 'Back to Menu' button.");
        Assert.IsTrue(source.Contains("ReturnToMainMenu"),
            "Back to Menu must call MainMenuController.ReturnToMainMenu().");
    }

    [UnityTest]
    public IEnumerator PostMatch_HasRematchButton()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameManager.HUD.cs"));

        Assert.IsTrue(source.Contains("Rematch"),
            "Post-match UI must have 'Rematch' button.");
        Assert.IsTrue(source.Contains("RematchRequested"),
            "Rematch must set RematchRequested flag.");
    }

    [UnityTest]
    public IEnumerator PostMatch_HasWatchReplayButton()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameManager.HUD.cs"));

        Assert.IsTrue(source.Contains("Watch Replay"),
            "Post-match UI must have 'Watch Replay' button.");
        Assert.IsTrue(source.Contains("LastCompletedReplayPath"),
            "Watch Replay must use replayLogger.LastCompletedReplayPath.");
        Assert.IsTrue(source.Contains("GameMode.Replay"),
            "Watch Replay must set GameMode to Replay before loading scene.");
    }

    [UnityTest]
    public IEnumerator PostMatch_WatchReplay_ChecksFileExists()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameManager.HUD.cs"));

        Assert.IsTrue(source.Contains("File.Exists(replayPath)"),
            "Watch Replay must check that replay file exists before loading.");
    }

    [UnityTest]
    public IEnumerator ReplayLogger_StartedInGameManagerStart()
    {
        yield return null;

        // GameManager.cs (the main file, not partial HUD/Episode) must call
        // replayLogger.StartGame() so replays work in all modes.
        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameManager.cs"));

        Assert.IsTrue(source.Contains("replayLogger.StartGame"),
            "GameManager.cs must call replayLogger.StartGame() so replays " +
            "are recorded in all modes (HumanVsAI, Training). ResetGame() alone " +
            "is not enough — it's only called by ML-Agents OnEpisodeBegin.");
    }

    [UnityTest]
    public IEnumerator ReplayLogger_HasLastCompletedReplayPath()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameReplayLogger.cs"));

        Assert.IsTrue(source.Contains("LastCompletedReplayPath"),
            "GameReplayLogger must expose LastCompletedReplayPath property.");
        Assert.IsTrue(source.Contains("gameFinished = true") && source.Contains("LastCompletedReplayPath = currentFilePath"),
            "LastCompletedReplayPath must be set when gameFinished becomes true.");
    }

    [UnityTest]
    public IEnumerator PostMatch_OnlyShownForMenuLaunchedGames()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameManager.HUD.cs"));

        Assert.IsTrue(source.Contains("LaunchedFromMenu"),
            "Post-match buttons must check LaunchedFromMenu flag.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Scene transitions ────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator ReturnToMainMenu_ResetsTimeScale()
    {
        yield return null;

        // Verify source contains Time.timeScale = 1f before LoadScene.
        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/MainMenuController.cs"));

        Assert.IsTrue(source.Contains("Time.timeScale = 1f"),
            "ReturnToMainMenu must reset timeScale to 1 before loading menu scene.");
    }

    [UnityTest]
    public IEnumerator StartMatch_SetsLaunchedFromMenu()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/MainMenuController.cs"));

        Assert.IsTrue(source.Contains("LaunchedFromMenu = true"),
            "StartMatch must set LaunchedFromMenu = true.");
    }

    [UnityTest]
    public IEnumerator StartMatch_SetsSessionState()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/MainMenuController.cs"));

        Assert.IsTrue(source.Contains("SessionState.SetString(\"GameMode\", \"HumanVsAI\")"),
            "StartMatch must set SessionState GameMode for domain reload safety.");
    }

    [UnityTest]
    public IEnumerator WatchReplay_SetsSessionState()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/MainMenuController.cs"));

        Assert.IsTrue(source.Contains("SessionState.SetString(\"GameMode\", \"Replay\")"),
            "WatchReplay must set SessionState GameMode for domain reload safety.");
    }

    [UnityTest]
    public IEnumerator WatchReplayHUD_SetsSessionState()
    {
        yield return null;

        // The post-match Watch Replay button in GameManager.HUD must also set SessionState.
        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameManager.HUD.cs"));

        Assert.IsTrue(source.Contains("SessionState.SetString(\"GameMode\", \"Replay\")"),
            "GameManager.HUD Watch Replay button must set SessionState(\"GameMode\", \"Replay\") " +
            "so InitSessionState restores Replay mode after domain reload.");
        Assert.IsTrue(source.Contains("SessionState.SetString(\"ReplayPlayer_PendingPath\""),
            "GameManager.HUD Watch Replay button must persist replay path to SessionState " +
            "so ReplayPlayer.Awake can recover it after domain reload.");
    }

    [UnityTest]
    public IEnumerator ReplaysPanel_ParsesSummaryNotLastLine()
    {
        yield return null;

        // ReplaysPanel must find the summary line explicitly, not assume it's the last line.
        // The territory snapshot line follows the summary line in replay files.
        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        // In raw file text, escaped quotes appear as backslash-quote.
        Assert.IsTrue(source.Contains("type") && source.Contains("summary"),
            "ReplaysPanel must search for the summary line by type, not assume last line.");

        // The while loop must filter lines — not blindly assign every line to lastLine.
        // Correct pattern: if (line.Contains(...summary...)) lastLine = line;
        Assert.IsTrue(source.Contains("if (line.Contains("),
            "ReplaysPanel must filter lines inside the while loop by summary type.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Button wiring ────────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator ButtonWiring_CoversAllPanels()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/MainMenuButtonWiring.cs"));

        Assert.IsTrue(source.Contains("MainPanel/PlayBtn"), "Must wire Play button.");
        Assert.IsTrue(source.Contains("MainPanel/ReplaysBtn"), "Must wire Replays button.");
        Assert.IsTrue(source.Contains("MainPanel/SettingsBtn"), "Must wire Settings button.");
        Assert.IsTrue(source.Contains("MainPanel/QuitBtn"), "Must wire Quit button.");
        Assert.IsTrue(source.Contains("PlayPanel/StartBtn"), "Must wire Start Match button.");
        Assert.IsTrue(source.Contains("PlayPanel/BackPlay"), "Must wire Play Back button.");
        Assert.IsTrue(source.Contains("ReplaysPanel/BackReplays"), "Must wire Replays Back button.");
        Assert.IsTrue(source.Contains("SettingsPanel/BackSettings"), "Must wire Settings Back button.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Settings persistence ─────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator Settings_UsesPlayerPrefs()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/SettingsPanel.cs"));

        Assert.IsTrue(source.Contains("PlayerPrefs.SetInt"), "Settings must persist via PlayerPrefs.");
        Assert.IsTrue(source.Contains("PlayerPrefs.GetInt"), "Settings must load from PlayerPrefs.");
        Assert.IsTrue(source.Contains("PlayerPrefs.Save()"), "Settings must call Save().");
    }

    [UnityTest]
    public IEnumerator Settings_HasAISpeedOptions()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/SettingsPanel.cs"));

        Assert.IsTrue(source.Contains("\"Slow\", \"Normal\", \"Fast\""),
            "AI speed must have Slow, Normal, Fast options.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Editor setup guard ───────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator MainMenuSetup_ExistsAsEditorScript()
    {
        yield return null;

        string path = System.IO.Path.Combine(Application.dataPath, "Editor/MainMenuSetup.cs");
        Assert.IsTrue(System.IO.File.Exists(path),
            "MainMenuSetup.cs must exist in Assets/Editor/ for scene generation.");
    }

    [UnityTest]
    public IEnumerator MainMenuSetup_AddsScenesToBuildSettings()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Editor/MainMenuSetup.cs"));

        Assert.IsTrue(source.Contains("MainMenu.unity"),
            "MainMenuSetup must add MainMenu scene to Build Settings.");
        Assert.IsTrue(source.Contains("SampleScene.unity"),
            "MainMenuSetup must add SampleScene to Build Settings.");
    }

    [UnityTest]
    public IEnumerator ReplaysPanel_SkipsSmallFiles()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        Assert.IsTrue(source.Contains("info.Length < 100"),
            "ReplaysPanel must skip incomplete replay files (< 100 bytes).");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Empty state: hide action buttons when list is empty ──────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator ReplaysPanel_ShowsEmptyMessage()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        Assert.IsTrue(source.Contains("No replays found"),
            "ReplaysPanel must show 'No replays found' when list is empty.");
    }

    [UnityTest]
    public IEnumerator ReplaysPanel_DisablesButtonsWhenNoSelection()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        Assert.IsTrue(source.Contains("GUI.enabled") && source.Contains("selectedIndex >= 0"),
            "Watch/Delete buttons must be disabled when no replay is selected.");
    }

    [UnityTest]
    public IEnumerator ReplaysPanel_UsesOnGUI()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        Assert.IsTrue(source.Contains("void OnGUI()"),
            "ReplaysPanel must use OnGUI for rendering (not Canvas, which loses serialized refs).");
        Assert.IsTrue(source.Contains("GUI.BeginScrollView"),
            "ReplaysPanel must have scrollable list via GUI.BeginScrollView.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Back button visibility: every sub-panel must have a Back button ──
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator AllSubPanels_HaveBackButton()
    {
        yield return null;

        string wiring = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/MainMenuButtonWiring.cs"));

        Assert.IsTrue(wiring.Contains("PlayPanel/BackPlay"),
            "Play panel must have a wired Back button.");
        Assert.IsTrue(wiring.Contains("ReplaysPanel/BackReplays"),
            "Replays panel must have a wired Back button.");
        Assert.IsTrue(wiring.Contains("SettingsPanel/BackSettings"),
            "Settings panel must have a wired Back button.");
    }

    [UnityTest]
    public IEnumerator BackButtons_AreProperButtons_NotText()
    {
        // Back buttons must be created via CreateMenuButton (visible, clickable)
        // not as tiny text labels at screen edges.
        yield return null;

        string setup = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Editor/MainMenuSetup.cs"));

        Assert.IsTrue(setup.Contains("CreateMenuButton(playPanel.transform, \"BackPlay\""),
            "Play Back must be a proper menu button, not text.");
        Assert.IsTrue(setup.Contains("CreateMenuButton(replaysPanel.transform, \"BackReplays\""),
            "Replays Back must be a proper menu button, not text.");
        Assert.IsTrue(setup.Contains("CreateMenuButton(settingsPanel.transform, \"BackSettings\""),
            "Settings Back must be a proper menu button, not text.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── General UI rule: action buttons require selection/content ─────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator ActionButtons_DisabledOrHiddenWithoutContent()
    {
        // Any panel with a list + action buttons must hide/disable actions when empty.
        yield return null;

        string replays = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        // Watch/Delete must respect selection state (OnGUI: GUI.enabled guard).
        Assert.IsTrue(replays.Contains("GUI.enabled") && replays.Contains("selectedIndex >= 0"),
            "Watch/Delete must be disabled when no replay is selected.");
    }

    [UnityTest]
    public IEnumerator MainMenuSetup_AutoCreatesScene()
    {
        yield return null;

        string ptw = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Editor/ProjectToolsWindow.cs"));

        Assert.IsTrue(ptw.Contains("MainMenuSetup.SetupMainMenuScene()"),
            "Launch Main Menu must auto-create scene if it doesn't exist.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── GameBootstrap: runtime scene setup ───────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator GameBootstrap_Exists()
    {
        yield return null;

        string path = System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameBootstrap.cs");
        Assert.IsTrue(System.IO.File.Exists(path),
            "GameBootstrap.cs must exist for runtime scene setup from MainMenu.");
    }

    [UnityTest]
    public IEnumerator GameBootstrap_UsesSceneLoadedEvent()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameBootstrap.cs"));

        Assert.IsTrue(source.Contains("SceneManager.sceneLoaded"),
            "GameBootstrap must use SceneManager.sceneLoaded event (not AfterSceneLoad) " +
            "so it fires on every scene transition, not just app start.");
    }

    [UnityTest]
    public IEnumerator GameBootstrap_DoesNotUseAfterSceneLoad()
    {
        // AfterSceneLoad fires only once at app start — useless for menu→game transitions.
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameBootstrap.cs"));

        Assert.IsFalse(source.Contains("AfterSceneLoad"),
            "GameBootstrap must NOT use AfterSceneLoad — it fires only once. " +
            "Use SceneManager.sceneLoaded event instead.");
    }

    [UnityTest]
    public IEnumerator GameBootstrap_SkipsMainMenuScene()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameBootstrap.cs"));

        Assert.IsTrue(source.Contains("\"MainMenu\"") && source.Contains("return"),
            "GameBootstrap must skip setup when scene is MainMenu.");
    }

    [UnityTest]
    public IEnumerator GameBootstrap_SkipsIfHexGridExists()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameBootstrap.cs"));

        Assert.IsTrue(source.Contains("FindFirstObjectByType<HexGrid>()") && source.Contains("return"),
            "GameBootstrap must skip if HexGrid already exists (Editor setup).");
    }

    [UnityTest]
    public IEnumerator GameBootstrap_CreatesHexGrid()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameBootstrap.cs"));

        Assert.IsTrue(source.Contains("AddComponent<HexGrid>()"),
            "GameBootstrap must create HexGrid.");
    }

    [UnityTest]
    public IEnumerator GameBootstrap_CreatesGameManager()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameBootstrap.cs"));

        Assert.IsTrue(source.Contains("AddComponent<GameManager>()"),
            "GameBootstrap must create GameManager.");
    }

    [UnityTest]
    public IEnumerator GameBootstrap_CreatesUnitFactory()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameBootstrap.cs"));

        Assert.IsTrue(source.Contains("AddComponent<UnitFactory>()"),
            "GameBootstrap must create UnitFactory.");
    }

    [UnityTest]
    public IEnumerator GameBootstrap_AppliesBoardSizeFromMenu()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameBootstrap.cs"));

        Assert.IsTrue(source.Contains("GameModeConfig.BoardSize"),
            "GameBootstrap must apply BoardSize from menu config.");
    }

    [UnityTest]
    public IEnumerator GameBootstrap_EnsuresReplayComponents()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameBootstrap.cs"));

        Assert.IsTrue(source.Contains("EnsureReplayComponents"),
            "GameBootstrap must call EnsureReplayComponents on every scene load.");
        Assert.IsTrue(source.Contains("AddComponent<ReplayPlayer>()"),
            "GameBootstrap must add ReplayPlayer component if missing.");
        Assert.IsTrue(source.Contains("AddComponent<ReplayPlayerHUD>()"),
            "GameBootstrap must add ReplayPlayerHUD component if missing.");
    }

    [UnityTest]
    public IEnumerator GameManager_HandlesReplayMode()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameManager.cs"));

        Assert.IsTrue(source.Contains("GameMode.Replay") && source.Contains("gameOver = true"),
            "GameManager.Start must set gameOver=true in Replay mode to prevent game loop.");
        Assert.IsTrue(source.Contains("GameMode.Replay") && source.Contains("autoRestart = false"),
            "GameManager.Start must set autoRestart=false in Replay mode.");
    }

    [UnityTest]
    public IEnumerator GameManager_SkipsReplayLoggingInReplayMode()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameManager.cs"));

        Assert.IsTrue(source.Contains("CurrentMode != GameMode.Replay") && source.Contains("replayLogger.StartGame"),
            "GameManager must NOT start replay logging when in Replay mode (watching, not recording).");
    }

    [UnityTest]
    public IEnumerator GameBootstrap_ConfiguresCamera()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameBootstrap.cs"));

        Assert.IsTrue(source.Contains("ConfigureCamera"),
            "GameBootstrap must configure camera for the game view.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Editor Play mode launch: deferred pattern ────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator LaunchMainMenu_UsesDeferredPattern()
    {
        // Launch Main Menu must use the exit-wait-open-play pattern, not immediate isPlaying=true.
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Editor/ProjectToolsWindow.cs"));

        Assert.IsTrue(source.Contains("DoLaunchMainMenu"),
            "Launch Main Menu must use a deferred DoLaunchMainMenu method.");
        Assert.IsTrue(source.Contains("EnteredEditMode") && source.Contains("DoLaunchMainMenu"),
            "Must wait for EnteredEditMode before opening MainMenu scene.");
    }

    [UnityTest]
    public IEnumerator AllLaunchMethods_UseDeferredPattern()
    {
        // Every method that opens a scene and enters Play mode must use the deferred pattern.
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Editor/ProjectToolsWindow.cs"));

        // All known launch methods that open a scene + enter Play.
        string[] launchMethods = { "DoLaunchMainMenu", "DoResetSetupPlay" };
        foreach (string method in launchMethods)
        {
            Assert.IsTrue(source.Contains(method),
                $"Launch method '{method}' must exist in ProjectToolsWindow.");
        }

        // The deferred wrapper handles exiting play mode first, then calling Do* method.
        Assert.IsTrue(source.Contains("void LaunchMainMenu()"),
            "LaunchMainMenu wrapper must exist for deferred pattern.");
    }

    [UnityTest]
    public IEnumerator DoLaunchMainMenu_SavesBeforePlay()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Editor/ProjectToolsWindow.cs"));

        // Find the method definition (not a call site).
        int defIdx = source.IndexOf("static void DoLaunchMainMenu");
        Assert.Greater(defIdx, 0, "DoLaunchMainMenu method definition must exist.");

        string methodBody = source.Substring(defIdx, System.Math.Min(600, source.Length - defIdx));
        Assert.IsTrue(methodBody.Contains("SaveAssets") || methodBody.Contains("SaveOpenScenes"),
            "DoLaunchMainMenu must save assets/scenes before entering Play mode.");
    }

    [UnityTest]
    public IEnumerator DoLaunchMainMenu_AutoCreatesScene()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Editor/ProjectToolsWindow.cs"));

        Assert.IsTrue(source.Contains("MainMenuSetup.SetupMainMenuScene()"),
            "DoLaunchMainMenu must auto-create MainMenu scene if missing.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Guard: game scene must work both from Editor and MainMenu ────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator GameScene_HasBothSetupPaths()
    {
        // HexGridSetup (Editor) and GameBootstrap (runtime) must both exist.
        yield return null;

        Assert.IsTrue(
            System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath, "Editor/HexGridSetup.cs")),
            "HexGridSetup.cs must exist for Editor-based scene setup.");
        Assert.IsTrue(
            System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameBootstrap.cs")),
            "GameBootstrap.cs must exist for runtime scene setup from MainMenu.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── New Input System compliance ──────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator HumanInputManager_UsesNewInputSystem()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Agents/HumanInputManager.cs"));

        Assert.IsTrue(source.Contains("using UnityEngine.InputSystem"),
            "HumanInputManager must use UnityEngine.InputSystem (new Input System).");
        Assert.IsTrue(source.Contains("InputAction"),
            "HumanInputManager must use InputAction for input bindings.");
    }

    [UnityTest]
    public IEnumerator HumanInputManager_DoesNotUseOldInput()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Agents/HumanInputManager.cs"));

        Assert.IsFalse(source.Contains("Input.GetKey"),
            "Must not use old Input.GetKey — use InputAction.WasPressedThisFrame().");
        Assert.IsFalse(source.Contains("Input.GetMouse"),
            "Must not use old Input.GetMouseButton — use InputAction for mouse.");
        Assert.IsFalse(source.Contains("Input.mousePosition"),
            "Must not use old Input.mousePosition — use InputAction<Vector2>.");
    }

    [UnityTest]
    public IEnumerator HumanInputManager_DisposesActions()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Agents/HumanInputManager.cs"));

        Assert.IsTrue(source.Contains("OnDestroy"),
            "HumanInputManager must have OnDestroy to clean up InputActions.");
        Assert.IsTrue(source.Contains("Dispose()"),
            "OnDestroy must Dispose all InputActions.");
    }

    [UnityTest]
    public IEnumerator NoOldInputAPI_InProjectScripts()
    {
        // Scan ALL script files for old Input API usage.
        yield return null;

        var violations = new System.Collections.Generic.List<string>();
        string scriptsDir = System.IO.Path.Combine(Application.dataPath, "Scripts");

        foreach (string file in System.IO.Directory.GetFiles(scriptsDir, "*.cs",
            System.IO.SearchOption.AllDirectories))
        {
            string source = System.IO.File.ReadAllText(file);
            string fileName = System.IO.Path.GetFileName(file);

            if (System.Text.RegularExpressions.Regex.IsMatch(source,
                @"\bInput\.(GetKey|GetMouse|mousePosition|GetAxis|GetButton)"))
            {
                violations.Add(fileName);
            }
        }

        Assert.IsEmpty(violations,
            $"Old Input API (Input.GetKey/GetMouse/mousePosition) found in: " +
            $"{string.Join(", ", violations)}. Use new Input System (InputAction) instead.");
    }

    [UnityTest]
    public IEnumerator AgentsAsmdef_ReferencesInputSystem()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Agents/Agents.asmdef"));

        // Unity.InputSystem GUID: 75469ad4d38634e559750d17036d5f7c
        Assert.IsTrue(source.Contains("75469ad4d38634e559750d17036d5f7c"),
            "Agents.asmdef must reference Unity.InputSystem package.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── HUD feature tests ────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator HUD_HasUnifiedTerritoryBar()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameManager.HUD.cs"));

        Assert.IsTrue(source.Contains("DrawTerritoryBar"),
            "HUD must have a unified territory bar (blue/green/gray) instead of separate team bars.");
    }

    [UnityTest]
    public IEnumerator HUD_ShowsBuildCountWithCap()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameManager.HUD.cs"));

        Assert.IsTrue(source.Contains("structCount") && source.Contains("structMax"),
            "Team panel must show structure count/max (e.g. 5/8) for wall/slime limits.");
    }

    [UnityTest]
    public IEnumerator HUD_HidesSessionStatsInReplay()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameManager.HUD.cs"));

        Assert.IsTrue(source.Contains("isReplayActive") && source.Contains("DrawSessionStats"),
            "HUD must hide session stats and match history during replay playback.");
    }

    [UnityTest]
    public IEnumerator ReplayHUD_HasTurnLog()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/ReplayPlayerHUD.cs"));

        Assert.IsTrue(source.Contains("DrawReplayTurnLog"),
            "ReplayPlayerHUD must display a turn log showing last 10 turns.");
    }

    [UnityTest]
    public IEnumerator AdjacencyAura_ExistsAndRegistered()
    {
        yield return null;

        Assert.IsTrue(System.IO.File.Exists(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Agents/AdjacencyAura.cs")),
            "AdjacencyAura.cs must exist for visual adjacency bonus indication.");

        string cleanup = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Agents/StaticResourceCleanup.cs"));
        Assert.IsTrue(cleanup.Contains("AdjacencyAura.GetStaticMaterials"),
            "AdjacencyAura must be registered in StaticResourceCleanup.");
    }

    [UnityTest]
    public IEnumerator ReplaysPanel_HasBackButton()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        Assert.IsTrue(source.Contains("BACK") && source.Contains("OnBack"),
            "ReplaysPanel must have a Back button visible at all times.");
    }

    [UnityTest]
    public IEnumerator ReplaysPanel_HasClickableRows()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        Assert.IsTrue(source.Contains("selectedIndex = i"),
            "ReplaysPanel rows must be clickable to select a replay.");
    }

    [UnityTest]
    public IEnumerator AdjacencyAura_AddedToUnits()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Agents/UnitFactory.cs"));

        Assert.IsTrue(source.Contains("AddComponent<AdjacencyAura>()"),
            "UnitFactory must add AdjacencyAura component to spawned units.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ReplaysPanel OnGUI rendering ─────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator ReplaysPanel_HidesCanvasChildren()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        Assert.IsTrue(source.Contains("transform.GetChild(i).gameObject.SetActive(false)"),
            "ReplaysPanel.OnEnable must hide all Canvas children — OnGUI handles rendering.");
    }

    [UnityTest]
    public IEnumerator ReplaysPanel_UsesListIndex_NotMatchNum()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        // Row numbering must use sequential index, not matchNum from replay header.
        Assert.IsTrue(source.Contains("rowNum + 1") || source.Contains("i + 1"),
            "ReplaysPanel must display row number as sequential index (1,2,3...), not matchNum from header.");
    }

    [UnityTest]
    public IEnumerator ReplaysPanel_HasScrollView()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        Assert.IsTrue(source.Contains("GUI.BeginScrollView") && source.Contains("GUI.EndScrollView"),
            "ReplaysPanel must use scrollable view for replay list.");
    }

    [UnityTest]
    public IEnumerator ReplaysPanel_HasWatchDeleteBack()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        Assert.IsTrue(source.Contains("Watch Replay") && source.Contains("OnWatch"),
            "ReplaysPanel must have Watch Replay button that calls OnWatch.");
        Assert.IsTrue(source.Contains("BACK") && source.Contains("OnBack"),
            "ReplaysPanel must have Back button that calls OnBack.");
    }

    [UnityTest]
    public IEnumerator ReplaysPanel_ShowsDateResultDuration()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        // Columns: date, result, duration must be displayed in each row.
        Assert.IsTrue(source.Contains("e.date") && source.Contains("e.result") && source.Contains("e.duration"),
            "ReplaysPanel rows must display date, result, and duration columns.");
    }

    [UnityTest]
    public IEnumerator ReplaysPanel_HighlightsSelectedRow()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        Assert.IsTrue(source.Contains("selectedBg") && source.Contains("i == selectedIndex"),
            "ReplaysPanel must visually highlight the selected row.");
    }

    [UnityTest]
    public IEnumerator ReplaysPanel_DeleteRemovesFile()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        Assert.IsTrue(source.Contains("File.Delete(path)") && source.Contains("RefreshList"),
            "OnDelete must delete the file and refresh the list.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Human starts first, territory bar, replay HUD ────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator HumanPlayer_AlwaysStartsFirst()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameManager.cs"));

        Assert.IsTrue(source.Contains("startingTeam = GameModeConfig.HumanTeam"),
            "In HumanVsAI mode, starting team must be set to human player's team.");
    }

    [UnityTest]
    public IEnumerator TerritoryBar_SameWidthAsRoundCounter()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameManager.HUD.cs"));

        // Territory bar call must use centerX and centerW (same as round counter).
        Assert.IsTrue(source.Contains("DrawTerritoryBar(centerX,"),
            "Territory bar must start at centerX (same as round counter panel).");
        Assert.IsFalse(source.Contains("centerX - 60") && source.Contains("DrawTerritoryBar"),
            "Territory bar must NOT extend beyond round counter width.");
    }

    [UnityTest]
    public IEnumerator ReplayHUD_ShowsHumanReadableTitle()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/ReplayPlayerHUD.cs"));

        Assert.IsTrue(source.Contains("DisplayTitle"),
            "Replay HUD must use DisplayTitle (human-readable) instead of FileName.");
        Assert.IsFalse(source.Contains("player.FileName"),
            "Replay HUD must NOT display raw JSON filename.");
    }

    [UnityTest]
    public IEnumerator ReplayHUD_NoPrevNextDescriptions()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/ReplayPlayerHUD.cs"));

        Assert.IsFalse(source.Contains("PreviousTurnDescription") || source.Contains("CurrentTurnDescription"),
            "Replay HUD must NOT show Prev/Next turn descriptions (shown in turn log table instead).");
    }

    [UnityTest]
    public IEnumerator ReplayPlayer_HasDisplayTitle()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/ReplayPlayer.cs"));

        Assert.IsTrue(source.Contains("public string DisplayTitle"),
            "ReplayPlayer must expose a DisplayTitle property for human-readable game info.");
        Assert.IsTrue(source.Contains("ParseDateFromFileName"),
            "DisplayTitle must parse date from filename into readable format.");
    }

    [UnityTest]
    public IEnumerator NoTechnicalInfoInUI_ReplayHUD()
    {
        yield return null;

        // Global rule: no filenames, paths, GUIDs in player-facing UI.
        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/ReplayPlayerHUD.cs"));

        Assert.IsFalse(source.Contains(".jsonl"),
            "Replay HUD must not display .jsonl file extension.");
        Assert.IsFalse(source.Contains("player.FileName") && source.Contains("GUI.Label"),
            "Replay HUD must not display raw filename in GUI labels.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Replay info, favorites, action formatting, turn marker ───────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator ReplayHeader_StoresGameModeAndHumanTeam()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameReplayLogger.cs"));

        Assert.IsTrue(source.Contains("gameMode") && source.Contains("GameModeConfig.CurrentMode"),
            "Replay header must include gameMode field.");
        Assert.IsTrue(source.Contains("humanTeam") && source.Contains("GameModeConfig.HumanTeam"),
            "Replay header must include humanTeam for HumanVsAI games.");
    }

    [UnityTest]
    public IEnumerator ReplayData_ParsesGameModeAndHumanTeam()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/ReplayData.cs"));

        Assert.IsTrue(source.Contains("public string gameMode"),
            "ReplayData.Header must have gameMode field.");
        Assert.IsTrue(source.Contains("public string humanTeam"),
            "ReplayData.Header must have humanTeam field.");
    }

    [UnityTest]
    public IEnumerator ReplayPlayer_FormatWinner_ShowsYouWinForHuman()
    {
        yield return null;

        Assert.AreEqual("You win!", ReplayPlayer.FormatWinner("Robot", "Robot"));
        Assert.AreEqual("AI wins", ReplayPlayer.FormatWinner("Mutant", "Robot"));
        Assert.AreEqual("You win!", ReplayPlayer.FormatWinner("Mutant", "Mutant"));
        Assert.AreEqual("Draw", ReplayPlayer.FormatWinner("None", "Robot"));
        Assert.AreEqual("Robots win", ReplayPlayer.FormatWinner("Robot", ""));
    }

    [UnityTest]
    public IEnumerator ReplaysPanel_HasFavoriteColumn()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        Assert.IsTrue(source.Contains("favorite") && source.Contains("SetFavorite"),
            "ReplaysPanel must support favorite marking with persistence.");
        Assert.IsTrue(source.Contains("showFavoritesOnly"),
            "ReplaysPanel must have a filter toggle for favorites only.");
    }

    [UnityTest]
    public IEnumerator ReplaysPanel_NoDeleteButton()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        // OnGUI section must not have a Delete button (destructive without confirmation).
        Assert.IsFalse(source.Contains("\"Delete\"") && source.Contains("GUI.Button"),
            "ReplaysPanel must NOT have a Delete button (destructive action without confirmation).");
    }

    [UnityTest]
    public IEnumerator FormatAction_InsertSpaces()
    {
        yield return null;

        Assert.AreEqual("Place Slime", GameManager.FormatAction("PlaceSlime"));
        Assert.AreEqual("Build Wall", GameManager.FormatAction("BuildWall"));
        Assert.AreEqual("Destroy Wall", GameManager.FormatAction("DestroyWall"));
        Assert.AreEqual("Move", GameManager.FormatAction("Move"));
        Assert.AreEqual("Attack", GameManager.FormatAction("Attack"));
        Assert.AreEqual("Idle", GameManager.FormatAction("Idle"));
        Assert.AreEqual("Capture", GameManager.FormatAction("Capture"));
    }

    [UnityTest]
    public IEnumerator TurnLog_UsesFormatAction()
    {
        yield return null;

        string hud = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameManager.HUD.cs"));
        Assert.IsTrue(hud.Contains("FormatAction("),
            "GameManager.HUD turn log must use FormatAction for human-readable action names.");

        string replay = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/ReplayPlayerHUD.cs"));
        Assert.IsTrue(replay.Contains("FormatAction("),
            "ReplayPlayerHUD turn log must use FormatAction for human-readable action names.");
    }

    [UnityTest]
    public IEnumerator ReplayTurnLog_HasRowNumbers()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/ReplayPlayerHUD.cs"));

        Assert.IsTrue(source.Contains("rowNum") && source.Contains("colNum"),
            "Replay turn log must display row numbers (1-10).");
    }

    [UnityTest]
    public IEnumerator Replay_SetsIsMyTurnOnActiveUnit()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/ReplayPlayer.cs"));

        Assert.IsTrue(source.Contains("unit.isMyTurn = true"),
            "ReplayPlayer.ApplyTurn must set isMyTurn on the active unit for turn glow.");
        Assert.IsTrue(source.Contains("u.isMyTurn = false"),
            "ReplayPlayer.ApplyTurn must clear isMyTurn from all other units.");
    }

    [UnityTest]
    public IEnumerator ReplaysPanel_UsesFormatWinner()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        Assert.IsTrue(source.Contains("ReplayPlayer.FormatWinner"),
            "ReplaysPanel must use ReplayPlayer.FormatWinner for human-friendly result display.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Menu buttons, quit confirm, turn log, replay visuals, aura ──────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator MenuButtons_HaveHoverEffect()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Editor/MainMenuSetup.cs"));

        Assert.IsTrue(source.Contains("img.color = Color.white"),
            "Button Image must be Color.white so Button.colors can tint it for hover.");
        Assert.IsTrue(source.Contains("highlightedColor"),
            "Button must have highlighted color set for hover feedback.");
    }

    [UnityTest]
    public IEnumerator MenuButtons_RuntimeHoverFix()
    {
        // MainMenuController.Start must fix Image.color on all buttons at runtime
        // so hover works even if the scene was built with old MainMenuSetup.
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/MainMenuController.cs"));

        Assert.IsTrue(source.Contains("GetComponentsInChildren<") && source.Contains("Button"),
            "MainMenuController.Start must find all Button components in children.");
        Assert.IsTrue(source.Contains("img.color = Color.white"),
            "MainMenuController.Start must set Image.color = white on all buttons for hover tinting.");
    }

    [UnityTest]
    public IEnumerator QuitButton_HasConfirmation()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/MainMenuController.cs"));

        Assert.IsTrue(source.Contains("showQuitConfirm"),
            "QuitGame must show a confirmation modal, not quit immediately.");
        Assert.IsTrue(source.Contains("DoQuit"),
            "Actual quit must be in a separate DoQuit method, called only after confirmation.");
        Assert.IsTrue(source.Contains("Cancel"),
            "Quit confirmation must have a Cancel button.");
    }

    [UnityTest]
    public IEnumerator TurnLog_FixedSize10Rows()
    {
        yield return null;

        string hud = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameManager.HUD.cs"));
        Assert.IsTrue(hud.Contains("maxVisibleRows") && hud.Contains("10"),
            "Game HUD turn log must have fixed size for 10 rows.");

        string replay = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/ReplayPlayerHUD.cs"));
        Assert.IsTrue(replay.Contains("maxVisibleRows") && replay.Contains("10"),
            "Replay HUD turn log must have fixed size for 10 rows.");
    }

    [UnityTest]
    public IEnumerator TurnLog_RowsClickableJumpToTurn()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/ReplayPlayerHUD.cs"));

        // Turn log rows must be clickable buttons that jump to the corresponding turn.
        Assert.IsTrue(source.Contains("GUI.Button") && source.Contains("JumpToTurn"),
            "Turn log rows must be clickable and call JumpToTurn when clicked.");
        Assert.IsTrue(source.Contains("turnLogRowBtnStyle"),
            "Turn log rows must use a custom button style with hover highlight.");
    }

    [UnityTest]
    public IEnumerator QuitModal_OpaqueOverlay()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/MainMenuController.cs"));

        // Overlay must be opaque enough (>= 0.8 alpha) for readability.
        Assert.IsTrue(source.Contains("0.85f"),
            "Quit modal overlay must be dark enough (0.85 alpha) for text readability.");
    }

    [UnityTest]
    public IEnumerator Replay_HasActionVisuals()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/ReplayPlayer.cs"));

        Assert.IsTrue(source.Contains("TriggerReplayVisuals"),
            "ReplayPlayer.ApplyTurn must call TriggerReplayVisuals for action effects.");
        Assert.IsTrue(source.Contains("FlashSecondaryTile"),
            "Attack turns must flash target tile in replay.");
        Assert.IsTrue(source.Contains("ShowMoveArrow"),
            "Move turns must show move arrow in replay.");
        Assert.IsTrue(source.Contains("FlashTile"),
            "Build turns must flash the built tile in replay.");
    }

    [UnityTest]
    public IEnumerator ReplayVisuals_ClearedOnNextTurn_NotTimer()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/ReplayPlayer.cs"));

        // Visuals must persist until next turn, not fade on a timer.
        Assert.IsTrue(source.Contains("ClearReplayVisuals"),
            "ReplayPlayer must have ClearReplayVisuals method to clean up previous turn's effects.");
        Assert.IsTrue(source.Contains("ClearReplayVisuals()") && source.Contains("TriggerReplayVisuals"),
            "ClearReplayVisuals must be called at the start of TriggerReplayVisuals (next turn clears previous).");
        Assert.IsFalse(source.Contains("MoveArrowDuration") || source.Contains("FlashDuration"),
            "Replay visuals must NOT use timers — they persist until the next turn is applied.");
        Assert.IsFalse(source.Contains("moveArrowTimer") || source.Contains("flashTimer"),
            "Replay visuals must NOT use timer fields — cleared on next turn instead.");
    }

    [UnityTest]
    public IEnumerator AdjacencyAura_LazyInitGrid()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Agents/AdjacencyAura.cs"));

        // Grid must be lazy-initialized in LateUpdate, not just Start().
        // This ensures it works in replay mode where grid may not exist at Start time.
        Assert.IsTrue(source.Contains("grid == null") && source.Contains("FindFirstObjectByType<HexGrid>"),
            "AdjacencyAura must lazy-init grid in LateUpdate for replay compatibility.");
        // No line should contain both "grid == null" and "enabled = false".
        foreach (var line in source.Split('\n'))
        {
            Assert.IsFalse(line.Contains("grid == null") && line.Contains("enabled = false"),
                "AdjacencyAura must NOT disable when grid is null — use lazy init. Offending line: " + line.Trim());
        }
    }

    [UnityTest]
    public IEnumerator FavoritesStar_ClickableBeforeRowButton()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        // Star button must appear BEFORE row selection button in OnGUI
        // so it receives clicks first (GUI draws and processes in order).
        int starIdx = source.IndexOf("SetFavorite");
        int rowIdx = source.IndexOf("selectedIndex = i");
        Assert.Greater(rowIdx, starIdx,
            "Star button must be drawn BEFORE row selection button in OnGUI (first drawn = first click).");
    }

    [UnityTest]
    public IEnumerator AdjacencyAura_GeometryDashedRing()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Agents/AdjacencyAura.cs"));

        // Dashes are baked into geometry (separate arc segments with gaps), not texture.
        Assert.IsTrue(source.Contains("CreateDashedRingMesh"),
            "AdjacencyAura must build a ring mesh with geometry-baked dash segments.");
        Assert.IsTrue(source.Contains("DashCount"),
            "AdjacencyAura must define number of dashes around the perimeter.");
        Assert.IsTrue(source.Contains("DashFillRatio"),
            "AdjacencyAura must control dash/gap ratio.");
        Assert.IsTrue(source.Contains("HasAdjacentAlly"),
            "AdjacencyAura must check for adjacent allies before showing outline.");
        Assert.IsFalse(source.Contains("_EmissionColor"),
            "AdjacencyAura must NOT use emission (causes bloom).");
    }

    [UnityTest]
    public IEnumerator AdjacencyAura_RotationAnimation()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Agents/AdjacencyAura.cs"));

        // Dashes flow around hex via Y-axis rotation (no shader trickery).
        Assert.IsTrue(source.Contains("RotateSpeed"),
            "AdjacencyAura must define rotation speed for flowing dash animation.");
        Assert.IsTrue(source.Contains("transform.Rotate") && source.Contains("Vector3.up"),
            "AdjacencyAura must rotate the outline around Y axis each frame.");
    }

    [UnityTest]
    public IEnumerator TurnHighlight_NoEmission()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Agents/UnitActionIndicator3D.cs"));

        // Turn highlight uses brightness pulse via _BaseColor, NOT emission.
        Assert.IsTrue(source.Contains("UpdateTurnHighlight"),
            "UnitActionIndicator3D must have brightness-based turn highlight.");
        Assert.IsFalse(source.Contains("_EmissionColor"),
            "UnitActionIndicator3D must NOT use _EmissionColor (causes bloom).");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Project Tools, Play screen, Quit modal, arrow, attack flash ─────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator ProjectTools_HasOnlyEssentialSections()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Editor/ProjectToolsWindow.cs"));

        // Kept sections.
        Assert.IsTrue(source.Contains("DrawConfigSection()"), "Must have Configuration section.");
        Assert.IsTrue(source.Contains("DrawTrainingSection()"), "Must have Training section.");
        Assert.IsTrue(source.Contains("DrawLaunchSection()"), "Must have Launch section.");
        Assert.IsTrue(source.Contains("DrawTestingSection()"), "Must have Testing section.");

        // Removed redundant sections (handled by in-game menu).
        Assert.IsFalse(source.Contains("DrawPlayVsAISection()"),
            "Play vs AI section removed — use Main Menu instead.");
        Assert.IsFalse(source.Contains("DrawReplaySection()"),
            "Replay section removed — use Main Menu → Replays instead.");
    }

    [UnityTest]
    public IEnumerator PlaySetupPanel_UsesOnGUI()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/PlaySetupPanel.cs"));

        Assert.IsTrue(source.Contains("void OnGUI()"),
            "PlaySetupPanel must use OnGUI for rendering (Canvas refs unreliable).");
        Assert.IsTrue(source.Contains("DrawOutline"),
            "Selected team card must have a visible outline for clear selection feedback.");
    }

    [UnityTest]
    public IEnumerator QuitModal_BlocksCanvasClicks()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/MainMenuController.cs"));

        Assert.IsTrue(source.Contains("GraphicRaycaster") && source.Contains("showQuitConfirm"),
            "Quit modal must disable GraphicRaycaster to block Canvas button clicks.");
    }

    [UnityTest]
    public IEnumerator ReplayArrow_HasArrowHead()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/ReplayPlayer.cs"));

        Assert.IsTrue(source.Contains("arrowHeadObj") && source.Contains("CreateArrowHeadMesh"),
            "Replay move indicator must have a proper arrowhead, not just a line.");
    }

    [UnityTest]
    public IEnumerator ReplayArrow_UsesUnlitMaterial()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/ReplayPlayer.cs"));

        // Arrow must use Unlit shader, not default Lit (Lit causes bloom).
        Assert.IsTrue(source.Contains("arrowUnlitMaterial"),
            "Replay arrow must use a shared Unlit material (not default Lit from CreatePrimitive).");
        Assert.IsTrue(source.Contains("EnsureArrowMaterial"),
            "Replay arrow must create an Unlit material via EnsureArrowMaterial.");
        Assert.IsFalse(source.Contains(".material.color"),
            "Replay arrow must NOT use .material.color (creates Lit material instances, causes bloom).");
    }

    [UnityTest]
    public IEnumerator ReplayArrow_UsesPropertyBlock()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/ReplayPlayer.cs"));

        // Arrow color must be set via MaterialPropertyBlock (zero allocation).
        Assert.IsTrue(source.Contains("arrowMpb") && source.Contains("SetPropertyBlock"),
            "Replay arrow must use MaterialPropertyBlock for team color (zero allocation).");
    }

    [UnityTest]
    public IEnumerator ReplayAttack_FlashesAttackerAndTarget()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/ReplayPlayer.cs"));

        // Attack must flash attacker hex (red) AND target hex (orange).
        Assert.IsTrue(source.Contains("FlashTile") && source.Contains("0.9f, 0.15f, 0.1f"),
            "Attack must flash attacker tile red.");
        Assert.IsTrue(source.Contains("FlashSecondaryTile") && source.Contains("1f, 0.55f, 0.1f"),
            "Attack must flash target tile orange.");
    }

    [UnityTest]
    public IEnumerator ReplayAttack_PersistsUntilNextTurn()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/ReplayPlayer.cs"));

        // Attack visuals must NOT use AttackEffects (timer-based).
        Assert.IsFalse(source.Contains("TriggerCombatFlash"),
            "Replay must NOT use AttackEffects.TriggerCombatFlash — it uses a timer. Use FlashTile instead.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static void SetField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        if (field != null) field.SetValue(obj, value);
    }
}
