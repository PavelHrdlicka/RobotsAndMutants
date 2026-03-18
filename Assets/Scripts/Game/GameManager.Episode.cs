using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

/// <summary>
/// Episode lifecycle: reset, match recording, agent group management.
/// </summary>
public partial class GameManager
{
    // ── Match history ──────────────────────────────────────────────────────

    private struct MatchResult
    {
        public Team winner;
        public int  rounds;
        public int  robotTiles;
        public int  mutantTiles;
        public int  matchNumber;
    }

    private static readonly List<MatchResult> matchHistory = new();
    private static int   matchCounter;
    private static long  totalTurns;
    private static float sessionStartTime;

    // ── ResetGame ──────────────────────────────────────────────────────────

    public void ResetGame()
    {
        currentRound  = 0;
        gameOver      = false;
        winner        = Team.None;
        turnStarted   = false;
        pendingUnit   = null;
        turnIndex     = -1;
        robotAttacks  = 0; robotBuilds  = 0; robotKills  = 0;
        mutantAttacks = 0; mutantBuilds = 0; mutantKills = 0;
        turnOrder.Clear();

        foreach (var tile in grid.Tiles.Values)
            tile.ResetTile();

        unitFactory.ClearUnits();
        unitFactory.SpawnAllUnits();

        Debug.Log("[GameManager] Game reset.");
    }

    private void RecordMatch(Team matchWinner, int rounds, int rTiles, int mTiles)
    {
        matchCounter++;
        matchHistory.Add(new MatchResult
        {
            winner      = matchWinner,
            rounds      = rounds,
            robotTiles  = rTiles,
            mutantTiles = mTiles,
            matchNumber = matchCounter
        });
        while (matchHistory.Count > 20) matchHistory.RemoveAt(0);
    }

    private void EndEpisodeForAll()
    {
        // Persist running total to PlayerPrefs every game.
        PlayerPrefs.SetInt("TotalGames", PlayerPrefs.GetInt("TotalGames", 0) + 1);
        long prev = (long)PlayerPrefs.GetInt("TotalTurnsHi", 0) << 32
                  | (uint)PlayerPrefs.GetInt("TotalTurnsLo", 0);
        long updated = prev + currentRound;
        PlayerPrefs.SetInt("TotalTurnsHi", (int)(updated >> 32));
        PlayerPrefs.SetInt("TotalTurnsLo", (int)(updated & 0xFFFFFFFF));

        robotGroup?.EndGroupEpisode();
        mutantGroup?.EndGroupEpisode();

        foreach (var unit in unitFactory.AllUnits)
        {
            var agent = unit.GetComponent<HexAgent>();
            if (agent != null) agent.enabled = false;
            var dr = unit.GetComponent<DecisionRequester>();
            if (dr != null) dr.enabled = false;
            unit.isMyTurn = false;
        }

        if (autoRestart)
            StartCoroutine(AutoRestartCoroutine());
    }

    private IEnumerator AutoRestartCoroutine()
    {
        yield return new WaitForSecondsRealtime(restartDelay);
        ResetGame();

        robotGroup  = new SimpleMultiAgentGroup();
        mutantGroup = new SimpleMultiAgentGroup();

        foreach (var unit in unitFactory.robotUnits)
        {
            var agent = unit.GetComponent<HexAgent>();
            var dr    = unit.GetComponent<DecisionRequester>();
            if (agent != null) { agent.enabled = true; robotGroup.RegisterAgent(agent); }
            if (dr    != null) dr.enabled = true;
        }
        foreach (var unit in unitFactory.mutantUnits)
        {
            var agent = unit.GetComponent<HexAgent>();
            var dr    = unit.GetComponent<DecisionRequester>();
            if (agent != null) { agent.enabled = true; mutantGroup.RegisterAgent(agent); }
            if (dr    != null) dr.enabled = true;
        }
    }
}
