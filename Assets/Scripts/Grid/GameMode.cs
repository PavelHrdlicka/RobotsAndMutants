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
/// Set before entering Play mode, read by GameManager and UnitFactory.
/// </summary>
public static class GameModeConfig
{
    public static GameMode CurrentMode = GameMode.Training;

    /// <summary>Which team the human controls in HumanVsAI mode.</summary>
    public static Team HumanTeam = Team.Robot;
}
