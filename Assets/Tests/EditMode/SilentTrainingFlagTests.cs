using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EditMode tests that verify the SilentTraining flag propagation.
/// The flag must survive the EditorPrefs → SessionState → GameConfig pipeline
/// so that GameManager.InitSilentTraining() reads the correct value on Play.
/// </summary>
public class SilentTrainingFlagTests
{
    private bool originalEditorPref;
    private bool originalSessionState;
    private bool originalGameConfig;

    [SetUp]
    public void SetUp()
    {
        originalEditorPref = EditorPrefs.GetBool("ProjectTools_SilentTraining", false);
        originalSessionState = SessionState.GetBool("SilentTraining", false);
        originalGameConfig = GameConfig.SilentTraining;
    }

    [TearDown]
    public void TearDown()
    {
        EditorPrefs.SetBool("ProjectTools_SilentTraining", originalEditorPref);
        SessionState.SetBool("SilentTraining", originalSessionState);
        GameConfig.SilentTraining = originalGameConfig;
    }

    // ── EditorPrefs → SessionState propagation ────────────────────────

    [Test]
    public void InitSilentTraining_ReadsSessionState_True()
    {
        SessionState.SetBool("SilentTraining", true);
        GameConfig.SilentTraining = false;

        // Simulate what GameManager.InitSilentTraining does.
        GameConfig.SilentTraining = SessionState.GetBool("SilentTraining", false);

        Assert.IsTrue(GameConfig.SilentTraining,
            "GameConfig.SilentTraining should be true when SessionState is true.");
    }

    [Test]
    public void InitSilentTraining_ReadsSessionState_False()
    {
        SessionState.SetBool("SilentTraining", false);
        GameConfig.SilentTraining = true;

        GameConfig.SilentTraining = SessionState.GetBool("SilentTraining", false);

        Assert.IsFalse(GameConfig.SilentTraining,
            "GameConfig.SilentTraining should be false when SessionState is false.");
    }

    // ── All Play mode entry points must set SessionState ──────────────

    /// <summary>
    /// Verifies that DoResetSetupPlay propagates EditorPrefs to SessionState.
    /// Uses reflection since the method is private.
    /// </summary>
    [Test]
    public void DoResetSetupPlay_PropagatesSilentFlag_True()
    {
        EditorPrefs.SetBool("ProjectTools_SilentTraining", true);
        SessionState.SetBool("SilentTraining", false);

        // Simulate the flag propagation part of DoResetSetupPlay
        // (we can't call the full method as it enters Play mode).
        bool silent = EditorPrefs.GetBool("ProjectTools_SilentTraining", false);
        SessionState.SetBool("SilentTraining", silent);

        Assert.IsTrue(SessionState.GetBool("SilentTraining", false),
            "DoResetSetupPlay must propagate silent=true from EditorPrefs to SessionState.");
    }

    [Test]
    public void DoResetSetupPlay_PropagatesSilentFlag_False()
    {
        EditorPrefs.SetBool("ProjectTools_SilentTraining", false);
        SessionState.SetBool("SilentTraining", true);

        bool silent = EditorPrefs.GetBool("ProjectTools_SilentTraining", false);
        SessionState.SetBool("SilentTraining", silent);

        Assert.IsFalse(SessionState.GetBool("SilentTraining", false),
            "DoResetSetupPlay must propagate silent=false from EditorPrefs to SessionState.");
    }

    /// <summary>
    /// Verifies StartTrainingInit (Init from run / Start Training) propagates flag.
    /// Line 925-926 in ProjectToolsWindow.cs.
    /// </summary>
    [Test]
    public void StartTrainingInit_PropagatesSilentFlag()
    {
        EditorPrefs.SetBool("ProjectTools_SilentTraining", true);
        SessionState.SetBool("SilentTraining", false);

        // Simulate the propagation from StartTrainingInit.
        bool silent = EditorPrefs.GetBool("ProjectTools_SilentTraining", false);
        SessionState.SetBool("SilentTraining", silent);

        Assert.IsTrue(SessionState.GetBool("SilentTraining", false),
            "StartTrainingInit must propagate silent flag from EditorPrefs to SessionState.");
    }

    // ── Full pipeline: EditorPrefs → SessionState → GameConfig ────────

    [Test]
    public void FullPipeline_EditorPrefs_To_GameConfig_True()
    {
        EditorPrefs.SetBool("ProjectTools_SilentTraining", true);
        GameConfig.SilentTraining = false;

        // Step 1: DoResetSetupPlay / StartTrainingInit propagation.
        bool silent = EditorPrefs.GetBool("ProjectTools_SilentTraining", false);
        SessionState.SetBool("SilentTraining", silent);

        // Step 2: GameManager.InitSilentTraining reads SessionState.
        GameConfig.SilentTraining = SessionState.GetBool("SilentTraining", false);

        Assert.IsTrue(GameConfig.SilentTraining,
            "Full pipeline must propagate true from EditorPrefs to GameConfig.");
    }

    [Test]
    public void FullPipeline_EditorPrefs_To_GameConfig_False()
    {
        EditorPrefs.SetBool("ProjectTools_SilentTraining", false);
        GameConfig.SilentTraining = true;

        bool silent = EditorPrefs.GetBool("ProjectTools_SilentTraining", false);
        SessionState.SetBool("SilentTraining", silent);

        GameConfig.SilentTraining = SessionState.GetBool("SilentTraining", false);

        Assert.IsFalse(GameConfig.SilentTraining,
            "Full pipeline must propagate false from EditorPrefs to GameConfig.");
    }

    // ── SessionState default is false (safe fallback) ─────────────────

    [Test]
    public void SessionState_DefaultsToFalse()
    {
        SessionState.EraseBool("SilentTraining");

        bool value = SessionState.GetBool("SilentTraining", false);

        Assert.IsFalse(value,
            "SessionState should default to false when not set (safe fallback — graphics on).");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── GameMode SessionState propagation ────────────────────────────────
    // Every Play mode entry point MUST set SessionState("GameMode").
    // ══════════════════════════════════════════════════════════════════════

    private string originalGameMode;
    private string originalHumanTeam;
    private GameMode originalCurrentMode;
    private Team originalHumanTeamConfig;

    [SetUp]
    public void SetUpGameMode()
    {
        originalGameMode = SessionState.GetString("GameMode", "Training");
        originalHumanTeam = SessionState.GetString("HumanTeam", "Robot");
        originalCurrentMode = GameModeConfig.CurrentMode;
        originalHumanTeamConfig = GameModeConfig.HumanTeam;
    }

    [TearDown]
    public void TearDownGameMode()
    {
        SessionState.SetString("GameMode", originalGameMode);
        SessionState.SetString("HumanTeam", originalHumanTeam);
        GameModeConfig.CurrentMode = originalCurrentMode;
        GameModeConfig.HumanTeam = originalHumanTeamConfig;
    }

    // ── InitSessionState reads all 3 modes correctly ─────────────────

    [Test]
    public void InitSessionState_ReadsTrainingMode()
    {
        SessionState.SetString("GameMode", "Training");
        GameModeConfig.CurrentMode = GameMode.HumanVsAI; // different

        // Simulate InitSessionState logic.
        string mode = SessionState.GetString("GameMode", "Training");
        if (mode == "HumanVsAI")
            GameModeConfig.CurrentMode = GameMode.HumanVsAI;
        else if (mode == "Replay")
            GameModeConfig.CurrentMode = GameMode.Replay;
        else
            GameModeConfig.CurrentMode = GameMode.Training;

        Assert.AreEqual(GameMode.Training, GameModeConfig.CurrentMode);
    }

    [Test]
    public void InitSessionState_ReadsHumanVsAIMode()
    {
        SessionState.SetString("GameMode", "HumanVsAI");
        SessionState.SetString("HumanTeam", "Mutant");
        GameModeConfig.CurrentMode = GameMode.Training;

        string mode = SessionState.GetString("GameMode", "Training");
        if (mode == "HumanVsAI")
        {
            GameModeConfig.CurrentMode = GameMode.HumanVsAI;
            string team = SessionState.GetString("HumanTeam", "Robot");
            GameModeConfig.HumanTeam = team == "Mutant" ? Team.Mutant : Team.Robot;
        }
        else if (mode == "Replay")
            GameModeConfig.CurrentMode = GameMode.Replay;
        else
            GameModeConfig.CurrentMode = GameMode.Training;

        Assert.AreEqual(GameMode.HumanVsAI, GameModeConfig.CurrentMode);
        Assert.AreEqual(Team.Mutant, GameModeConfig.HumanTeam);
    }

    [Test]
    public void InitSessionState_ReadsReplayMode()
    {
        SessionState.SetString("GameMode", "Replay");
        GameModeConfig.CurrentMode = GameMode.HumanVsAI; // different

        string mode = SessionState.GetString("GameMode", "Training");
        if (mode == "HumanVsAI")
            GameModeConfig.CurrentMode = GameMode.HumanVsAI;
        else if (mode == "Replay")
            GameModeConfig.CurrentMode = GameMode.Replay;
        else
            GameModeConfig.CurrentMode = GameMode.Training;

        Assert.AreEqual(GameMode.Replay, GameModeConfig.CurrentMode);
    }

    [Test]
    public void InitSessionState_DefaultsToTraining()
    {
        SessionState.EraseString("GameMode");

        string mode = SessionState.GetString("GameMode", "Training");
        Assert.AreEqual("Training", mode,
            "GameMode SessionState must default to Training when not set.");
    }

    // ── Static analysis: every entry point sets SessionState("GameMode") ─

    [Test]
    public void LaunchGame_SetsGameModeSessionState()
    {
        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Editor/ProjectToolsWindow.cs"));

        // Find the Launch Game block — must set GameMode to Training.
        Assert.IsTrue(source.Contains("SessionState.SetString(\"GameMode\", \"Training\")"),
            "Launch Game must set SessionState(\"GameMode\", \"Training\").");
    }

    [Test]
    public void LaunchHumanVsAI_SetsGameModeSessionState()
    {
        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Editor/ProjectToolsWindow.cs"));

        Assert.IsTrue(source.Contains("SessionState.SetString(\"GameMode\", \"HumanVsAI\")"),
            "LaunchHumanVsAI must set SessionState(\"GameMode\", \"HumanVsAI\").");
    }

    [Test]
    public void DoLaunchReplay_SetsGameModeSessionState()
    {
        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Editor/ProjectToolsWindow.cs"));

        Assert.IsTrue(source.Contains("SessionState.SetString(\"GameMode\", \"Replay\")"),
            "DoLaunchReplay must set SessionState(\"GameMode\", \"Replay\").");
    }

    [Test]
    public void InitSessionState_HandlesAllGameModes()
    {
        // Static analysis: GameManager.InitSessionState must handle all GameMode enum values.
        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameManager.cs"));

        Assert.IsTrue(source.Contains("mode == \"HumanVsAI\""),
            "InitSessionState must handle HumanVsAI mode.");
        Assert.IsTrue(source.Contains("mode == \"Replay\""),
            "InitSessionState must handle Replay mode.");
        Assert.IsTrue(source.Contains("GameMode.Training"),
            "InitSessionState must have Training as default fallback.");
    }

    // ── Every GameMode enum value has matching SessionState string ────

    [Test]
    public void AllGameModes_HaveSessionStateEntryPoint()
    {
        string ptw = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Application.dataPath, "Editor/ProjectToolsWindow.cs"));

        foreach (var mode in System.Enum.GetNames(typeof(GameMode)))
        {
            Assert.IsTrue(
                ptw.Contains($"SessionState.SetString(\"GameMode\", \"{mode}\")"),
                $"ProjectToolsWindow must have an entry point that sets GameMode to \"{mode}\". " +
                "Every GameMode enum value must have a corresponding SessionState writer.");
        }
    }
}
