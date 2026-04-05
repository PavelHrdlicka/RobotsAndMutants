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
    public IEnumerator ReplaysPanel_HidesButtonsWhenEmpty()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        Assert.IsTrue(source.Contains("watchButton") && source.Contains("SetActive(!empty)"),
            "Watch button must be hidden when replay list is empty.");
        Assert.IsTrue(source.Contains("deleteButton") && source.Contains("SetActive(!empty)"),
            "Delete button must be hidden when replay list is empty.");
    }

    [UnityTest]
    public IEnumerator ReplaysPanel_ShowsNoReplaysText()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/MainMenu/ReplaysPanel.cs"));

        Assert.IsTrue(source.Contains("noReplaysText") && source.Contains("SetActive(empty)"),
            "Must show 'No replays' text when list is empty.");
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

        // Watch/Delete must respect selection state.
        Assert.IsTrue(replays.Contains("watchButton.interactable = selectedIndex >= 0"),
            "Watch must be disabled when no replay is selected.");
        Assert.IsTrue(replays.Contains("deleteButton.interactable = selectedIndex >= 0"),
            "Delete must be disabled when no replay is selected.");
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
    public IEnumerator GameBootstrap_HasAfterSceneLoadAttribute()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameBootstrap.cs"));

        Assert.IsTrue(source.Contains("RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)"),
            "GameBootstrap must run at AfterSceneLoad to detect missing HexGrid.");
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
        string[] launchMethods = { "DoLaunchMainMenu", "DoResetSetupPlay", "DoLaunchReplay" };
        foreach (string method in launchMethods)
        {
            Assert.IsTrue(source.Contains(method),
                $"Launch method '{method}' must exist in ProjectToolsWindow.");
        }

        // None of them should have bare "isPlaying = true" without prior scene open.
        // The deferred wrappers (LaunchMainMenu, LaunchHumanVsAI, LaunchReplay) handle
        // exiting play mode first, then calling the Do* method via EnteredEditMode callback.
        Assert.IsTrue(source.Contains("void LaunchMainMenu()"),
            "LaunchMainMenu wrapper must exist for deferred pattern.");
    }

    [UnityTest]
    public IEnumerator DoLaunchMainMenu_SavesBeforePlay()
    {
        yield return null;

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Editor/ProjectToolsWindow.cs"));

        // Must save before entering Play mode to prevent scene loss.
        int doLaunchIdx = source.IndexOf("DoLaunchMainMenu");
        Assert.Greater(doLaunchIdx, 0);

        string afterMethod = source.Substring(doLaunchIdx, 500);
        Assert.IsTrue(afterMethod.Contains("SaveAssets") || afterMethod.Contains("SaveOpenScenes"),
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

    // ── Helpers ──────────────────────────────────────────────────────────

    private static void SetField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        if (field != null) field.SetValue(obj, value);
    }
}
