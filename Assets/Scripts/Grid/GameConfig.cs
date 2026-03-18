using UnityEngine;

/// <summary>
/// Central game configuration. Singleton ScriptableObject loaded from Resources.
/// All game parameters in one place — editable from ProjectToolsWindow.
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "Hex Territory/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Board")]
    [Tooltip("Size of one side of the hex-shaped board (in small hexes).")]
    [Range(3, 15)]
    public int boardSide = 5;

    [Header("Units")]
    [Tooltip("Number of units per team. Also determines base size.")]
    [Range(1, 10)]
    public int unitsPerTeam = 3;

    [Header("Simulation")]
    [Tooltip("Milliseconds between two ticks. 20 ms = 50 ticks/s (training). 200 ms = 5 ticks/s (watching). 1000 ms = 1 tick/s (debug).")]
    [Range(1, 5000)]
    public int msPerTick = 200;

    /// <summary>Converts msPerTick to Unity timeScale. fixedDeltaTime = 0.02 s = 20 ms.</summary>
    public float TimeScale => 20f / msPerTick;

    [Header("Win Condition")]
    [Tooltip("Percentage of contestable territory needed to win (0-100).")]
    [Range(10, 100)]
    public int winPercent = 60;

    [Tooltip("Maximum number of steps (rounds) per episode.")]
    [Range(100, 10000)]
    public int maxSteps = 2000;

    // --- Derived ---

    /// <summary>Win threshold as 0-1 float.</summary>
    public float WinThreshold => winPercent / 100f;

    /// <summary>Expected tile count for current board size.</summary>
    public int TileCount => 3 * boardSide * boardSide - 3 * boardSide + 1;

    /// <summary>Base tiles per team = unitsPerTeam (corner + neighbors, clamped).</summary>
    public int BaseTilesPerTeam => Mathf.Min(unitsPerTeam + 1, MaxBaseSize);

    private int MaxBaseSize => boardSide >= 3 ? 4 : 1; // Corner + 3 neighbors max

    [Header("Reward Shaping")]
    [Tooltip("Reward for killing an enemy unit.")]
    public float killBonus = 0.5f;

    [Tooltip("Reward for a successful build action (crate / slime spread).")]
    public float buildReward = 0.05f;

    [Tooltip("Reward per tile gained in a single turn (territory capture).")]
    public float captureRewardPerTile = 0.1f;

    [Tooltip("Reward per enemy tile lost in a single turn (opponent lost territory).")]
    public float enemyLossRewardPerTile = 0.1f;

    [Tooltip("Small negative reward each step to encourage speed (should be negative).")]
    public float stepPenalty = -0.001f;

    [Tooltip("Group reward for winning team at end of episode.")]
    public float winReward = 1f;

    [Tooltip("Group reward for losing team at end of episode.")]
    public float loseReward = -1f;

    [Tooltip("Group reward for the leading team when episode ends by timeout.")]
    public float timeoutWinReward = 0.5f;

    [Tooltip("Group reward for the trailing team when episode ends by timeout.")]
    public float timeoutLoseReward = -0.5f;

    // --- Singleton ---

    private static GameConfig _instance;

    public static GameConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<GameConfig>("GameConfig");
            return _instance;
        }
    }
}
