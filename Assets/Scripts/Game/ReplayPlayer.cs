using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

/// <summary>
/// Plays back a recorded JSONL replay file on the existing hex grid.
/// Disables ML-Agents and GameManager's game loop, then drives unit state
/// and tile ownership from replay data. Existing visual systems (HexVisuals,
/// UnitHealthBar3D, UnitActionIndicator3D) react automatically.
/// </summary>
public class ReplayPlayer : MonoBehaviour
{
    /// <summary>Set by Editor before entering Play mode. Cleared after loading.</summary>
    public static string PendingReplayPath;

    public enum PlaybackState { Stopped, Playing, Paused, Finished }

    [Header("Playback")]
    public PlaybackState state = PlaybackState.Stopped;
    public float turnDelay = 0.3f; // seconds between turns
    public int currentRound;
    public int currentTurnIndex;

    // Parsed replay data.
    private ReplayData.ReplayFile replay;
    private string replayFileName;

    // Unit lookup.
    private Dictionary<string, UnitData> unitMap = new Dictionary<string, UnitData>();
    private Dictionary<string, HexMovement> movementMap = new Dictionary<string, HexMovement>();

    // References.
    private GameManager gm;
    private HexGrid grid;
    private UnitFactory unitFactory;

    private float nextTurnTime;

    // ── Public API ──────────────────────────────────────────────────────

    public ReplayData.ReplayFile Replay => replay;
    public string FileName => replayFileName;
    public int TotalRounds => replay?.maxRound ?? 0;
    public int TotalTurns => replay?.turns.Count ?? 0;

    /// <summary>Current turn description for HUD display.</summary>
    public string CurrentTurnDescription
    {
        get
        {
            if (replay == null || currentTurnIndex >= replay.turns.Count) return "";
            var t = replay.turns[currentTurnIndex];
            return $"{t.unitName}: {t.action} at ({t.q},{t.r})";
        }
    }

    /// <summary>Previous turn description (the last applied turn).</summary>
    public string PreviousTurnDescription
    {
        get
        {
            if (replay == null || currentTurnIndex <= 0) return "";
            var t = replay.turns[currentTurnIndex - 1];
            return $"{t.unitName}: {t.action} at ({t.q},{t.r})";
        }
    }

    // ── Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Recover path from SessionState before domain-reload wipes static fields.
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(PendingReplayPath))
            PendingReplayPath = UnityEditor.SessionState.GetString("ReplayPlayer_PendingPath", "");
        UnityEditor.SessionState.EraseString("ReplayPlayer_PendingPath");
#endif

        // Immediately block GameManager's game loop so it doesn't start episodes.
        if (!string.IsNullOrEmpty(PendingReplayPath))
        {
            var gm = GetComponent<GameManager>();
            if (gm != null)
            {
                gm.gameOver = true;
                gm.autoRestart = false;
            }
        }
    }

    private IEnumerator Start()
    {
        if (string.IsNullOrEmpty(PendingReplayPath))
        {
            enabled = false;
            yield break;
        }

        string path = PendingReplayPath;
        PendingReplayPath = null;

        // Wait for grid and units to initialize.
        yield return null;
        yield return null;
        yield return null;

        gm = GetComponent<GameManager>();
        grid = Object.FindFirstObjectByType<HexGrid>();
        unitFactory = Object.FindFirstObjectByType<UnitFactory>();

        if (grid == null || unitFactory == null)
        {
            Debug.LogError("[Replay] HexGrid or UnitFactory not found.");
            enabled = false;
            yield break;
        }

        // Parse replay file.
        try
        {
            replay = ReplayData.Parse(path);
            replayFileName = System.IO.Path.GetFileName(path);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Replay] Failed to parse {path}: {ex.Message}");
            enabled = false;
            yield break;
        }

        if (replay.turns.Count == 0)
        {
            Debug.LogWarning("[Replay] Replay file has no turns.");
            enabled = false;
            yield break;
        }

        Debug.Log($"[Replay] Loaded {replayFileName}: match #{replay.header.match}, " +
                  $"{replay.turns.Count} turns, {replay.maxRound} rounds, winner: {replay.summary.winner}");

        // Disable GameManager's game loop and sync maxRounds for HUD.
        if (gm != null)
        {
            gm.gameOver = true;
            gm.autoRestart = false;
            gm.maxRounds = replay.maxRound;
        }

        // Disable all ML-Agents.
        DisableAllAgents();

        // Build unit lookup.
        BuildUnitMap();

        // Reset all tiles to neutral.
        foreach (var tile in grid.Tiles.Values)
        {
            if (!tile.isBase)
            {
                tile.Owner = Team.None;
                tile.TileType = TileType.Empty;
                tile.WallHP = 0;
            }
        }

        // Reset all units to alive at base, full HP.
        foreach (var unit in unitFactory.AllUnits)
            unit.ResetUnit();

        state = PlaybackState.Paused;
        currentTurnIndex = 0;
        currentRound = 0;
        nextTurnTime = Time.time;
    }

    private void Update()
    {
        if (state != PlaybackState.Playing) return;
        if (replay == null || currentTurnIndex >= replay.turns.Count)
        {
            state = PlaybackState.Finished;
            return;
        }

        if (Time.time >= nextTurnTime)
        {
            StepOneTurn();
            nextTurnTime = Time.time + turnDelay;
        }
    }

    // ── Playback controls ───────────────────────────────────────────────

    public void Play()
    {
        if (state == PlaybackState.Finished)
        {
            // Restart from beginning if finished.
            JumpToStart();
        }
        state = PlaybackState.Playing;
        nextTurnTime = Time.time;
    }

    public void Pause() => state = PlaybackState.Paused;

    public void TogglePlayPause()
    {
        if (state == PlaybackState.Playing) Pause();
        else Play();
    }

    public void StepOneTurn()
    {
        if (replay == null || currentTurnIndex >= replay.turns.Count)
        {
            state = PlaybackState.Finished;
            return;
        }

        ApplyTurn(replay.turns[currentTurnIndex]);
        currentTurnIndex++;

        if (currentTurnIndex >= replay.turns.Count)
            state = PlaybackState.Finished;
    }

    public void StepOneRound()
    {
        if (replay == null || currentTurnIndex >= replay.turns.Count) return;

        int targetRound = replay.turns[currentTurnIndex].round + 1;
        while (currentTurnIndex < replay.turns.Count && replay.turns[currentTurnIndex].round < targetRound)
        {
            ApplyTurn(replay.turns[currentTurnIndex]);
            currentTurnIndex++;
        }

        if (currentTurnIndex >= replay.turns.Count)
            state = PlaybackState.Finished;
    }

    public void JumpToRound(int targetRound)
    {
        ResetToStart();

        // Apply all turns up to targetRound.
        while (currentTurnIndex < replay.turns.Count && replay.turns[currentTurnIndex].round <= targetRound)
        {
            ApplyTurn(replay.turns[currentTurnIndex]);
            currentTurnIndex++;
        }

        if (currentTurnIndex >= replay.turns.Count)
            state = PlaybackState.Finished;
        else
            state = PlaybackState.Paused;
    }

    public void JumpToTurn(int targetTurnIndex)
    {
        targetTurnIndex = Mathf.Clamp(targetTurnIndex, 0, replay?.turns.Count ?? 0);
        ResetToStart();

        while (currentTurnIndex < targetTurnIndex)
        {
            ApplyTurn(replay.turns[currentTurnIndex]);
            currentTurnIndex++;
        }

        if (currentTurnIndex >= replay.turns.Count)
            state = PlaybackState.Finished;
        else
            state = PlaybackState.Paused;
    }

    public void StepBackOneTurn()
    {
        if (replay == null || currentTurnIndex <= 0) return;
        JumpToTurn(currentTurnIndex - 1);
    }

    public void StepBackOneRound()
    {
        if (replay == null || currentRound <= 0) return;
        JumpToRound(currentRound - 1);
    }

    public void JumpToStart()
    {
        ResetToStart();
        state = PlaybackState.Paused;
    }

    public void JumpToEnd()
    {
        if (replay == null) return;
        JumpToTurn(replay.turns.Count);
    }

    private void ResetToStart()
    {
        foreach (var tile in grid.Tiles.Values)
        {
            if (!tile.isBase)
            {
                tile.Owner = Team.None;
                tile.TileType = TileType.Empty;
                tile.WallHP = 0;
            }
        }
        foreach (var unit in unitFactory.AllUnits)
            unit.ResetUnit();

        currentTurnIndex = 0;
        currentRound = 0;
    }

    // ── Turn application ────────────────────────────────────────────────

    private void ApplyTurn(ReplayData.Turn turn)
    {
        currentRound = turn.round;

        // Update GameManager's round counter so HUD shows correct value.
        if (gm != null)
            gm.currentRound = turn.round;

        if (!unitMap.TryGetValue(turn.unitName, out var unit)) return;

        // If unit was dead and now appears with hp > 0, respawn it.
        if (!unit.isAlive && turn.energy > 0)
        {
            var worldPos = grid.HexToWorld(new HexCoord(turn.q, turn.r));
            unit.Respawn(new HexCoord(turn.q, turn.r), worldPos);
        }

        // Set HP.
        unit.Energy = turn.energy;

        // Set position.
        var coord = new HexCoord(turn.q, turn.r);
        if (unit.currentHex != coord && movementMap.TryGetValue(turn.unitName, out var movement))
        {
            unit.moveFrom = unit.currentHex;
            unit.moveTo = coord;
            movement.PlaceAt(coord);
        }

        // Set action.
        unit.lastAction = ParseAction(turn.action);

        // Apply attack target info.
        if (turn.hasTarget && !string.IsNullOrEmpty(turn.targetUnit))
        {
            unit.lastAttackTarget = unitMap.TryGetValue(turn.targetUnit, out var target) ? target : null;
            unit.lastAttackKilled = turn.killed;

            // Kill target if needed.
            if (turn.killed && unit.lastAttackTarget != null && unit.lastAttackTarget.isAlive)
                unit.lastAttackTarget.Die(12);
        }
        else
        {
            unit.lastAttackTarget = null;
            unit.lastAttackKilled = false;
        }

        // Die if HP dropped to 0.
        if (turn.energy <= 0 && unit.isAlive)
            unit.Die(12);

        // Apply tile ownership from build actions.
        var tile = grid.GetTile(coord);
        if (tile != null && !tile.isBase)
        {
            Team team = turn.team == "Robot" ? Team.Robot : Team.Mutant;

            if (turn.action == "BuildCrate" || turn.action == "BuildWall")
            {
                tile.Owner = team;
                tile.TileType = TileType.Wall;
            }
            else if (turn.action == "SpreadSlime" || turn.action == "PlaceSlime")
            {
                tile.Owner = team;
                tile.TileType = TileType.Slime;
            }
            else if (turn.action == "Capture")
            {
                tile.Owner = team;
                tile.TileType = TileType.Empty;
            }
        }
    }

    private static UnitAction ParseAction(string action)
    {
        return action switch
        {
            "Move" => UnitAction.Move,
            "Attack" => UnitAction.Attack,
            "Defend" => UnitAction.Defend,
            "Idle" => UnitAction.Idle,
            "BuildCrate" => UnitAction.BuildWall,
            "BuildWall" => UnitAction.BuildWall,
            "SpreadSlime" => UnitAction.PlaceSlime,
            "PlaceSlime" => UnitAction.PlaceSlime,
            "Capture" => UnitAction.Capture,
            "Dead" => UnitAction.Dead,
            _ => UnitAction.Idle,
        };
    }

    // ── Setup helpers ───────────────────────────────────────────────────

    private void BuildUnitMap()
    {
        unitMap.Clear();
        movementMap.Clear();

        foreach (var unit in unitFactory.AllUnits)
        {
            string name = unit.gameObject.name;
            unitMap[name] = unit;
            var movement = unit.GetComponent<HexMovement>();
            if (movement != null)
                movementMap[name] = movement;
        }
    }

    private void DisableAllAgents()
    {
        foreach (var unit in unitFactory.AllUnits)
        {
            var agent = unit.GetComponent<HexAgent>();
            if (agent != null) agent.enabled = false;
            var dr = unit.GetComponent<DecisionRequester>();
            if (dr != null) dr.enabled = false;
            unit.isMyTurn = false;
        }
    }
}
