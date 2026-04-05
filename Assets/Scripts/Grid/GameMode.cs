/// <summary>
/// Game mode selection. Determines how units are controlled.
/// </summary>
public enum GameMode
{
    /// <summary>Both teams controlled by ML-Agents (training or inference).</summary>
    Training,

    /// <summary>Replay playback from JSONL file.</summary>
    Replay,

    /// <summary>Human player controls one team, AI controls the other.</summary>
    HumanVsAI
}

/// <summary>
/// Static config for the current game session mode.
/// Set before entering Play mode or from MainMenu, read by GameManager and UnitFactory.
/// </summary>
public static class GameModeConfig
{
    public static GameMode CurrentMode = GameMode.Training;

    /// <summary>Which team the human controls in HumanVsAI mode.</summary>
    public static Team HumanTeam = Team.Robot;

    /// <summary>Board size override from menu (0 = use GameConfig default).</summary>
    public static int BoardSize = 0;

    /// <summary>AI difficulty level (0=Easy, 1=Normal, 2=Hard).</summary>
    public static int AIDifficulty = 1;

    /// <summary>AI turn delay in seconds (set from Settings).</summary>
    public static float AITurnDelay = 0.5f;

    /// <summary>Whether the game was launched from MainMenu (vs Editor tools).</summary>
    public static bool LaunchedFromMenu = false;
}
