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
    public int maxSteps = 800;

    // --- Derived ---

    /// <summary>Win threshold as 0-1 float.</summary>
    public float WinThreshold => winPercent / 100f;

    /// <summary>Expected tile count for current board size.</summary>
    public int TileCount => 3 * boardSide * boardSide - 3 * boardSide + 1;

    /// <summary>Base tiles per team = unitsPerTeam (corner + neighbors, clamped).</summary>
    public int BaseTilesPerTeam => Mathf.Min(unitsPerTeam + 1, MaxBaseSize);

    private int MaxBaseSize => boardSide >= 3 ? 4 : 1; // Corner + 3 neighbors max

    [Header("Energy")]
    [Tooltip("Maximum energy per unit. Energy = HP + action currency.")]
    [Range(5, 30)]
    public int unitMaxEnergy = 15;

    [Tooltip("Respawn cooldown in steps after death.")]
    [Range(1, 30)]
    public int respawnCooldown = 6;

    [Header("Structures")]
    [Tooltip("Energy cost to build a wall on adjacent own hex.")]
    [Range(1, 10)]
    public int wallBuildCost = 4;

    [Tooltip("Wall hit points (attacks needed to destroy).")]
    [Range(1, 5)]
    public int wallMaxHP = 3;

    [Tooltip("Energy cost to place slime on adjacent own hex.")]
    [Range(1, 10)]
    public int slimePlaceCost = 2;

    [Tooltip("Energy cost to destroy own wall from adjacent hex.")]
    [Range(0, 5)]
    public int destroyOwnWallCost = 1;

    [Header("Combat")]
    [Tooltip("Energy cost to attack an enemy unit.")]
    [Range(1, 10)]
    public int attackUnitCost = 3;

    [Tooltip("Base damage dealt to enemy unit per attack.")]
    [Range(1, 10)]
    public int attackUnitDamage = 3;

    [Tooltip("Energy cost to attack an enemy wall.")]
    [Range(1, 10)]
    public int attackWallCost = 2;

    [Tooltip("Damage dealt to enemy wall per attack.")]
    [Range(1, 3)]
    public int attackWallDamage = 1;

    [Header("Proximity Bonuses")]
    [Tooltip("Shield Wall (Robots): max damage reduction from adjacent allies.")]
    [Range(0, 5)]
    public int shieldWallMaxReduction = 3;

    [Tooltip("Swarm (Mutants): max bonus damage from adjacent allies.")]
    [Range(0, 5)]
    public int swarmMaxBonus = 3;

    [Header("Regeneration")]
    [Tooltip("Energy regenerated per step when standing on own base hex.")]
    [Range(0, 10)]
    public int baseRegenPerStep = 3;

    [Tooltip("Energy regenerated per step for Mutants standing on own slime.")]
    [Range(0, 5)]
    public int slimeRegenPerStep = 1;

    [Tooltip("Energy cost for Robot entering enemy slime hex (slime destroyed on entry).")]
    [Range(0, 10)]
    public int slimeEntryCostRobot = 3;

    [Header("Replay Logging")]
    [Tooltip("Log every Nth game to a JSONL replay file. 1 = every game, 10 = every 10th game.")]
    [Range(1, 1000)]
    public int replayLogEveryNthGame = 1;

    [Header("Reward Shaping")]
    [Tooltip("Reward for killing an enemy unit.")]
    public float killBonus = 0.5f;

    [Tooltip("Reward for a successful build action (wall / slime).")]
    public float buildReward = 0.05f;

    [Tooltip("Reward per tile gained in a single turn (territory capture).")]
    public float captureRewardPerTile = 0.1f;

    [Tooltip("Reward per enemy tile lost in a single turn (opponent lost territory).")]
    public float enemyLossRewardPerTile = 0.1f;

    [Tooltip("Per-neighbor bonus when building on a tile adjacent to own tiles (encourages clustering).")]
    public float buildAdjacencyBonus = 0.03f;

    [Tooltip("Reward for capturing a hex via attack (neutral or enemy).")]
    public float hexCaptureReward = 0.05f;

    [Tooltip("Reward for placing a wall in a strategic position.")]
    public float wallPlacementReward = 0.03f;

    [Tooltip("Extra reward for Mutants placing slime (on top of buildReward).")]
    public float slimePlacementReward = 0.08f;

    [Tooltip("Per-neighbor bonus when Mutant places slime adjacent to existing slime (network).")]
    public float slimeNetworkBonus = 0.04f;

    [Tooltip("Bonus scaled by cohesion ratio (largest group / total tiles). Penalizes scattered islands.")]
    public float cohesionBonus = 0.02f;

    [Tooltip("One-time bonus when an action splits the enemy into more connected components.")]
    public float groupSplitBonus = 0.3f;

    [Tooltip("Per-step bonus when the largest group is connected to the team's base.")]
    public float baseConnectionBonus = 0.005f;

    [Tooltip("Per-step bonus for a unit standing on a frontline tile (own tile adjacent to enemy).")]
    public float frontlineBonus = 0.005f;

    [Tooltip("Small negative reward each step to encourage speed (should be negative).")]
    public float stepPenalty = -0.001f;

    [Tooltip("Extra penalty when unit chooses Idle (should be negative, stronger than stepPenalty).")]
    public float idlePenalty = -0.01f;

    [Tooltip("Group reward for winning team at end of episode.")]
    public float winReward = 1f;

    [Tooltip("Group reward for losing team at end of episode.")]
    public float loseReward = -1f;

    [Tooltip("Group reward for the leading team when episode ends by timeout.")]
    public float timeoutWinReward = 0.5f;

    [Tooltip("Group reward for the trailing team when episode ends by timeout.")]
    public float timeoutLoseReward = -0.5f;

    // --- Silent Training ---
    /// <summary>When true, skip all visual rendering for maximum training performance.</summary>
    public static bool SilentTraining;

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
