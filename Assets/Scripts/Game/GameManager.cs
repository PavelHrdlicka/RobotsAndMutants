using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

/// <summary>
/// Orchestrates the sequential turn-based game loop.
///
/// Turn order within a round (interleaved teams):
///   Odd  rounds (1, 3, …): R0 → M0 → R1 → M1 → …
///   Even rounds (2, 4, …): M0 → R0 → M1 → R1 → …
///
/// Each unit gets one action per round (move, attack, build, or idle).
/// Only one unit acts per FixedUpdate frame:
///   1. GameManager calls agent.RequestDecision().
///   2. Academy fires OnActionReceived (next frame, script order -50).
///   3. Agent executes action, sets hasPendingTurnResult = true.
///   4. GameManager.FixedUpdate (script order 0) processes the result,
///      then advances to the next unit.
///
/// Partial class split:
///   GameManager.cs         — game loop (this file)
///   GameManager.Episode.cs — episode lifecycle (reset, match history)
///   GameManager.HUD.cs     — OnGUI HUD rendering
/// </summary>
public partial class GameManager : MonoBehaviour
{
    [Header("Config")]
    public int   maxRounds    = 500;
    public float winThreshold = 0.6f;
    public bool  autoRestart  = true;
    public float restartDelay = 2f;

    [Header("Runtime")]
    public int  currentRound;
    public bool gameOver;
    public Team winner = Team.None;

    private HexGrid       grid;
    private UnitFactory   unitFactory;
    private AbilitySystem abilitySystem;

    // MA-POCA agent groups.
    private SimpleMultiAgentGroup robotGroup;
    private SimpleMultiAgentGroup mutantGroup;

    // ── Sequential turn state ──────────────────────────────────────────────
    private readonly List<UnitData> turnOrder = new();
    private int      turnIndex   = -1;
    private UnitData pendingUnit = null;
    private bool     turnStarted = false;
    private Team     lastTeamPlayed = Team.None; // for strict alternation
    private Team     startingTeam   = Team.Robot; // who starts each round

    // Per-game action counters (reset each episode in ResetGame).
    private int robotAttacks, robotBuilds, robotKills;
    private int mutantAttacks, mutantBuilds, mutantKills;

    // Replay logger — writes JSONL files for strategy analysis.
    private readonly GameReplayLogger replayLogger = new();

    // ── Initialisation ─────────────────────────────────────────────────────

    public GameState State => new GameState(
        currentRound, maxRounds,
        grid?.LargestConnectedGroup(Team.Robot)  ?? 0,  grid?.LargestConnectedGroup(Team.Mutant) ?? 0,
        CountAlive(Team.Robot), CountAlive(Team.Mutant),
        gameOver, winner
    );

    /// <summary>
    /// Runs before ANY Awake — ensures SilentTraining flag is set before
    /// HexGrid.Awake creates camera or UnitFactory creates visuals.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitSilentTraining()
    {
#if UNITY_EDITOR
        GameConfig.SilentTraining = UnityEditor.SessionState.GetBool("SilentTraining", false);
#endif
    }

    private IEnumerator Start()
    {

        yield return null;
        yield return null;

        var config = GameConfig.Instance;
        if (config != null)
        {
            maxRounds     = config.maxSteps;
            winThreshold  = config.WinThreshold;

            // Silent training: max speed — no rendering overhead.
            if (GameConfig.SilentTraining)
            {
                Time.timeScale = 20f;
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1; // uncapped
            }
            else
            {
                Time.timeScale = config.TimeScale;
            }
        }

        grid        = FindFirstObjectByType<HexGrid>();
        unitFactory = FindFirstObjectByType<UnitFactory>();

        if (grid == null || unitFactory == null)
        {
            Debug.LogError("[GameManager] Missing HexGrid or UnitFactory!");
            yield break;
        }

        abilitySystem = new AbilitySystem(grid);

        robotGroup  = new SimpleMultiAgentGroup();
        mutantGroup = new SimpleMultiAgentGroup();

        foreach (var unit in unitFactory.robotUnits)
        {
            var agent = unit.GetComponent<HexAgent>();
            if (agent != null) robotGroup.RegisterAgent(agent);
        }
        foreach (var unit in unitFactory.mutantUnits)
        {
            var agent = unit.GetComponent<HexAgent>();
            if (agent != null) mutantGroup.RegisterAgent(agent);
        }

        if (sessionStartTime == 0f)
            sessionStartTime = Time.realtimeSinceStartup;

        Debug.Log($"[GameManager] Ready. {grid.ContestableTileCount} contestable tiles. Max rounds: {maxRounds}.");
    }

    public bool IsReady => grid != null && unitFactory != null && abilitySystem != null;

    // ── Sequential turn loop ───────────────────────────────────────────────

    // Guard against multiple StartNewRound calls in the same frame.
    private int lastRoundStartFrame = -1;

    private void FixedUpdate()
    {
        if (gameOver || !IsReady) return;

        if (!turnStarted)
        {
            if (Time.frameCount == lastRoundStartFrame) return;
            lastRoundStartFrame = Time.frameCount;
            StartNewRound();
            return;
        }

        if (pendingUnit != null && pendingUnit.hasPendingTurnResult)
        {
            pendingUnit.hasPendingTurnResult = false;
            PostTurnProcessing(pendingUnit);
            if (gameOver) return;
            AdvanceTurn();
        }
    }

    private void StartNewRound()
    {
        currentRound++;

        if (currentRound > maxRounds)
        {
            var cfg = GameConfig.Instance;
            float tw = cfg?.timeoutWinReward  ??  0.5f;
            float tl = cfg?.timeoutLoseReward ?? -0.5f;
            int rGroup = grid.LargestConnectedGroup(Team.Robot);
            int mGroup = grid.LargestConnectedGroup(Team.Mutant);
            if      (rGroup > mGroup) EndGame(Team.Robot,  tw, tl, rGroup, mGroup, "Max rounds.");
            else if (mGroup > rGroup) EndGame(Team.Mutant, tw, tl, rGroup, mGroup, "Max rounds.");
            else                      EndGame(Team.None,   0f, 0f, rGroup, mGroup, "Max rounds. Draw.");
            return;
        }

        abilitySystem.UpdateAbilities(unitFactory.AllUnits);
        BuildTurnOrder();
        turnIndex   = -1;
        turnStarted = true;
        AdvanceTurn();
    }

    /// <summary>
    /// Build strictly alternating turn order: R, M, R, M, ...
    /// ALL units included (alive + dead waiting for respawn).
    /// Starting team alternates each round.
    /// </summary>
    private void BuildTurnOrder()
    {
        turnOrder.Clear();

        bool robotNext = (startingTeam == Team.Robot);
        int ri = 0, mi = 0;
        var robots  = unitFactory.robotUnits;
        var mutants = unitFactory.mutantUnits;

        while (ri < robots.Count || mi < mutants.Count)
        {
            if (robotNext && ri < robots.Count)
            {
                turnOrder.Add(robots[ri++]);
                robotNext = false;
            }
            else if (!robotNext && mi < mutants.Count)
            {
                turnOrder.Add(mutants[mi++]);
                robotNext = true;
            }
            else
            {
                robotNext = !robotNext;
            }
        }

        lastTeamPlayed = Team.None;
    }

    private void AdvanceTurn()
    {
        turnIndex++;

        if (turnIndex >= turnOrder.Count)
        {
            turnStarted = false;
            startingTeam = (startingTeam == Team.Robot) ? Team.Mutant : Team.Robot;
            return;
        }

        pendingUnit = turnOrder[turnIndex];
        if (pendingUnit == null)
        {
            AdvanceTurn();
            return;
        }

        // Dead unit: tick cooldown. If ready → respawn. Either way, turn is "used".
        if (!pendingUnit.isAlive)
        {
            if (pendingUnit.TickCooldown())
            {
                // Respawn at current base hex with full energy.
                var worldPos = grid.HexToWorld(pendingUnit.currentHex);
                pendingUnit.Respawn(pendingUnit.currentHex, worldPos);
                var movement = pendingUnit.GetComponent<HexMovement>();
                if (movement != null) movement.Initialize(grid);
            }
            pendingUnit.lastAction = UnitAction.Dead;
            // Signal turn done (same as HexAgent would).
            pendingUnit.isMyTurn = false;
            pendingUnit.hasPendingTurnResult = true;
            return;
        }

        pendingUnit.isMyTurn = true;
        lastTeamPlayed = pendingUnit.team;
    }

    private void PostTurnProcessing(UnitData unit)
    {
        totalTurns++;

        if (unit.isAlive)
        {
            bool isRobot = unit.team == Team.Robot;
            switch (unit.lastAction)
            {
                case UnitAction.Attack:
                    if (isRobot) robotAttacks++; else mutantAttacks++;
                    var enemies = isRobot ? unitFactory.mutantUnits : unitFactory.robotUnits;
                    foreach (var u in enemies)
                    {
                        if (!u.isAlive && u.lastAction == UnitAction.Dead
                            && HexCoord.Distance(unit.currentHex, u.currentHex) <= 1)
                        {
                            if (isRobot) robotKills++; else mutantKills++;
                            break;
                        }
                    }
                    break;
                case UnitAction.BuildWall:
                case UnitAction.PlaceSlime:
                    if (isRobot) robotBuilds++; else mutantBuilds++;
                    break;
            }
        }

        // Log turn to replay file.
        replayLogger.LogTurn(currentRound, unit,
            grid.LargestConnectedGroup(Team.Robot), grid.LargestConnectedGroup(Team.Mutant),
            CountAlive(Team.Robot), CountAlive(Team.Mutant),
            unitFactory.AllUnits);

        // Clear per-turn attack tracking.
        unit.lastAttackTarget = null;
        unit.lastAttackKilled = false;

        // Teleport any newly dead units to base immediately.
        TeleportDeadToBase();

        CheckWinCondition();
    }

    /// <summary>
    /// Find any dead units not yet on a base hex and teleport them to a free base hex.
    /// Called after each turn so deaths from combat are handled immediately.
    /// </summary>
    private void TeleportDeadToBase()
    {
        foreach (var unit in unitFactory.AllUnits)
        {
            if (unit.isAlive) continue;

            // Already on a base hex of own team? Skip.
            var currentTile = grid.GetTile(unit.currentHex);
            if (currentTile != null && currentTile.isBase && currentTile.baseTeam == unit.team)
                continue;

            // Find a free base hex.
            var baseTiles = grid.GetBaseTiles(unit.team);
            HexCoord freeBase = unit.currentHex; // fallback: stay in place
            bool found = false;
            foreach (var bt in baseTiles)
            {
                if (!IsHexOccupied(bt.coord))
                {
                    freeBase = bt.coord;
                    found = true;
                    break;
                }
            }

            if (found)
            {
                unit.currentHex = freeBase;
                var movement = unit.GetComponent<HexMovement>();
                if (movement != null) movement.PlaceAt(freeBase);
            }
        }
    }

    private bool IsHexOccupied(HexCoord coord)
    {
        foreach (var u in unitFactory.AllUnits)
        {
            if (u.currentHex == coord && u.gameObject.activeInHierarchy)
                return true;
        }
        return false;
    }

    // ── Win condition ──────────────────────────────────────────────────────

    private void CheckWinCondition()
    {
        // Win condition: largest connected group of territory (not total tiles).
        int   robotGroup  = grid.LargestConnectedGroup(Team.Robot);
        int   mutantGroup = grid.LargestConnectedGroup(Team.Mutant);
        float total       = grid.ContestableTileCount > 0 ? grid.ContestableTileCount : 1f;
        float robotRatio  = robotGroup  / total;
        float mutantRatio = mutantGroup / total;

        var cfg = GameConfig.Instance;
        float wr = cfg?.winReward         ??  1f;
        float lr = cfg?.loseReward        ?? -1f;
        float tw = cfg?.timeoutWinReward  ??  0.5f;
        float tl = cfg?.timeoutLoseReward ?? -0.5f;

        if (robotRatio >= winThreshold)
        {
            EndGame(Team.Robot, wr, lr, robotGroup, mutantGroup,
                    $"Robots win! ({robotRatio:P0} connected territory at round {currentRound})");
        }
        else if (mutantRatio >= winThreshold)
        {
            EndGame(Team.Mutant, wr, lr, robotGroup, mutantGroup,
                    $"Mutants win! ({mutantRatio:P0} connected territory at round {currentRound})");
        }
        else if (currentRound >= maxRounds && turnIndex >= turnOrder.Count - 1)
        {
            if (robotGroup > mutantGroup)
                EndGame(Team.Robot,  tw, tl, robotGroup, mutantGroup, "Max rounds. Robots lead.");
            else if (mutantGroup > robotGroup)
                EndGame(Team.Mutant, tw, tl, robotGroup, mutantGroup, "Max rounds. Mutants lead.");
            else
                EndGame(Team.None,   0f, 0f, robotGroup, mutantGroup, "Max rounds. Draw.");
        }
    }

    private void EndGame(Team win, float winnerReward, float loserReward,
                         int rTiles, int mTiles, string logMsg)
    {
        gameOver = true;
        winner   = win;

        var winGroup  = (win == Team.Robot) ? robotGroup  : mutantGroup;
        var loseGroup = (win == Team.Robot) ? mutantGroup : robotGroup;

        if (win == Team.None)
        {
            robotGroup?.AddGroupReward(0f);
            mutantGroup?.AddGroupReward(0f);
        }
        else
        {
            winGroup?.AddGroupReward(winnerReward);
            loseGroup?.AddGroupReward(loserReward);
        }

        RecordMatch(win, currentRound, rTiles, mTiles);

        // Close replay file with summary + territory snapshot.
        replayLogger.EndGame(win, currentRound, rTiles, mTiles,
            robotAttacks, mutantAttacks, mutantKills, robotKills,
            robotBuilds, mutantBuilds, grid);

        EndEpisodeForAll();
        if (PlayerPrefs.GetInt("TotalGames", 0) % 50 == 0)
            Debug.Log($"[GameManager] {logMsg}");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private int CountAlive(Team team)
    {
        var units = team == Team.Robot ? unitFactory.robotUnits : unitFactory.mutantUnits;
        int count = 0;
        foreach (var u in units) if (u.isAlive) count++;
        return count;
    }

    private string cachedModelInfo;
    private float  modelInfoCacheTime;

    private string GetModelInfo()
    {
        if (cachedModelInfo != null && Time.unscaledTime - modelInfoCacheTime < 2f)
            return cachedModelInfo;
        modelInfoCacheTime = Time.unscaledTime;

        if (unitFactory == null || unitFactory.robotUnits.Count == 0)
        {
            cachedModelInfo = "Model: none (random)";
            return cachedModelInfo;
        }

        var bp = unitFactory.robotUnits[0].GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
        cachedModelInfo = bp != null && bp.Model != null
            ? $"Model: {bp.Model.name} (trained)"
            : "Model: none (heuristic/random)";

        return cachedModelInfo;
    }
}
