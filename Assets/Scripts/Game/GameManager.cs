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

    // Per-game action counters (reset each episode in ResetGame).
    private int robotAttacks, robotBuilds, robotKills;
    private int mutantAttacks, mutantBuilds, mutantKills;

    // ── Initialisation ─────────────────────────────────────────────────────

    public GameState State => new GameState(
        currentRound, maxRounds,
        grid?.CountTiles(Team.Robot)  ?? 0,  grid?.CountTiles(Team.Mutant) ?? 0,
        CountAlive(Team.Robot), CountAlive(Team.Mutant),
        gameOver, winner
    );

    private IEnumerator Start()
    {
        yield return null;
        yield return null;

        var config = GameConfig.Instance;
        if (config != null)
        {
            maxRounds     = config.maxSteps;
            winThreshold  = config.WinThreshold;
            Time.timeScale = config.TimeScale;
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
            int rTiles = grid.CountTiles(Team.Robot);
            int mTiles = grid.CountTiles(Team.Mutant);
            if      (rTiles > mTiles) EndGame(Team.Robot,  tw, tl, rTiles, mTiles, "Max rounds.");
            else if (mTiles > rTiles) EndGame(Team.Mutant, tw, tl, rTiles, mTiles, "Max rounds.");
            else                      EndGame(Team.None,   0f, 0f, rTiles, mTiles, "Max rounds. Draw.");
            return;
        }

        abilitySystem.UpdateAbilities(unitFactory.AllUnits);
        BuildTurnOrder();
        turnIndex   = -1;
        turnStarted = true;
        AdvanceTurn();
    }

    /// <summary>
    /// Interleaved turn order — odd rounds robots first, even rounds mutants first.
    /// Dead units are included so their slot can be skipped cheaply in AdvanceTurn.
    /// </summary>
    private void BuildTurnOrder()
    {
        turnOrder.Clear();
        bool robotsFirst = (currentRound % 2 == 1);
        var  first  = robotsFirst ? unitFactory.robotUnits  : unitFactory.mutantUnits;
        var  second = robotsFirst ? unitFactory.mutantUnits : unitFactory.robotUnits;

        int max = Mathf.Max(first.Count, second.Count);
        for (int i = 0; i < max; i++)
        {
            if (i < first.Count)  turnOrder.Add(first[i]);
            if (i < second.Count) turnOrder.Add(second[i]);
        }
    }

    private void AdvanceTurn()
    {
        turnIndex++;

        while (turnIndex < turnOrder.Count && !turnOrder[turnIndex].isAlive)
            turnIndex++;

        if (turnIndex >= turnOrder.Count)
        {
            unitFactory.RespawnReady();
            turnStarted = false;
            return;
        }

        pendingUnit = turnOrder[turnIndex];

        if (pendingUnit == null || !pendingUnit.gameObject.activeInHierarchy)
        {
            AdvanceTurn();
            return;
        }

        pendingUnit.isMyTurn = true;
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
                case UnitAction.BuildCrate:
                case UnitAction.SpreadSlime:
                    if (isRobot) robotBuilds++; else mutantBuilds++;
                    break;
            }
        }

        CheckWinCondition();
    }

    // ── Win condition ──────────────────────────────────────────────────────

    private void CheckWinCondition()
    {
        int   robotTiles  = grid.CountTiles(Team.Robot);
        int   mutantTiles = grid.CountTiles(Team.Mutant);
        float total       = grid.ContestableTileCount > 0 ? grid.ContestableTileCount : 1f;
        float robotRatio  = robotTiles  / total;
        float mutantRatio = mutantTiles / total;

        var cfg = GameConfig.Instance;
        float wr = cfg?.winReward         ??  1f;
        float lr = cfg?.loseReward        ?? -1f;
        float tw = cfg?.timeoutWinReward  ??  0.5f;
        float tl = cfg?.timeoutLoseReward ?? -0.5f;

        if (robotRatio >= winThreshold)
        {
            EndGame(Team.Robot, wr, lr, robotTiles, mutantTiles,
                    $"Robots win! ({robotRatio:P0} territory at round {currentRound})");
        }
        else if (mutantRatio >= winThreshold)
        {
            EndGame(Team.Mutant, wr, lr, robotTiles, mutantTiles,
                    $"Mutants win! ({mutantRatio:P0} territory at round {currentRound})");
        }
        else if (currentRound >= maxRounds && turnIndex >= turnOrder.Count - 1)
        {
            if (robotTiles > mutantTiles)
                EndGame(Team.Robot,  tw, tl, robotTiles, mutantTiles, "Max rounds. Robots lead.");
            else if (mutantTiles > robotTiles)
                EndGame(Team.Mutant, tw, tl, robotTiles, mutantTiles, "Max rounds. Mutants lead.");
            else
                EndGame(Team.None,   0f, 0f, robotTiles, mutantTiles, "Max rounds. Draw.");
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
