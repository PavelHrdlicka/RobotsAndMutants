using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Writes per-game JSONL replay files for strategy analysis.
/// Throttled: only logs every Nth game to minimize disk I/O during training.
/// Format: header line, turn lines, summary line.
/// </summary>
public class GameReplayLogger
{
    /// <summary>Only log every Nth game. Driven by GameConfig.replayLogEveryNthGame.</summary>
    public int logEveryNthGame = 1;

    private StreamWriter writer;
    private int turnCounter;
    private float gameStartTime;
    private bool isLogging;

    // Write OUTSIDE Assets/ to avoid triggering Unity Asset Pipeline imports.
    // Path.GetFullPath("Replays") resolves to project root (working dir).
    private static readonly string DefaultReplayDir = Path.GetFullPath("Replays");

    /// <summary>Override in tests to redirect output to a temp directory.</summary>
    protected virtual string GetReplayDir() => DefaultReplayDir;

    /// <summary>Start a new replay file. Call at game start (ResetGame).</summary>
    public void StartGame(int matchNum, GameConfig config, HexGrid grid)
    {
        Close();
        isLogging = false;

        if (matchNum % logEveryNthGame != 0) return;

        try
        {
            string replayDir = GetReplayDir();
            if (!Directory.Exists(replayDir))
                Directory.CreateDirectory(replayDir);

            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"game_{matchNum}_{timestamp}.jsonl";
            string filePath = Path.Combine(replayDir, fileName);

            writer = new StreamWriter(filePath, false, Encoding.UTF8) { AutoFlush = false };
            isLogging = true;
            turnCounter = 0;
            gameStartTime = Time.realtimeSinceStartup;

            // Header line.
            int boardSide = config != null ? config.boardSide : 5;
            int units = config != null ? config.unitsPerTeam : 3;
            int maxRounds = config != null ? config.maxSteps : 2000;
            float winThreshold = config != null ? config.WinThreshold : 0.6f;

            var sb = new StringBuilder(256);
            sb.Append("{\"type\":\"header\"");
            sb.Append($",\"match\":{matchNum}");
            sb.Append($",\"timestamp\":\"{System.DateTime.Now:yyyy-MM-ddTHH:mm:ss}\"");
            sb.Append($",\"unitsPerTeam\":{units}");
            sb.Append($",\"gridSize\":{boardSide}");
            sb.Append($",\"maxRounds\":{maxRounds}");
            sb.Append($",\"winThreshold\":{winThreshold:F2}");
            sb.Append("}");
            writer.WriteLine(sb.ToString());
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Replay] Failed to create replay file: {ex.Message}");
            Close();
        }
    }

    /// <summary>Log one unit's turn action. Call from PostTurnProcessing.</summary>
    public void LogTurn(int round, UnitData unit, int rTiles, int mTiles, int rAlive, int mAlive)
    {
        if (!isLogging || writer == null || unit == null) return;

        try
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"type\":\"turn\"");
            sb.Append($",\"round\":{round}");
            sb.Append($",\"unit\":\"{unit.gameObject.name}\"");
            sb.Append($",\"team\":\"{unit.team}\"");
            sb.Append($",\"action\":\"{unit.lastAction}\"");
            sb.Append($",\"energy\":{unit.Energy}");
            sb.Append($",\"pos\":[{unit.currentHex.q},{unit.currentHex.r}]");

            // Attack target info.
            if (unit.lastAction == UnitAction.Attack)
            {
                var ah = unit.lastAttackHex;
                sb.Append($",\"attackHex\":[{ah.q},{ah.r}]");
            }
            if (unit.lastAttackTarget != null)
            {
                var t = unit.lastAttackTarget;
                sb.Append($",\"target\":[{t.currentHex.q},{t.currentHex.r}]");
                sb.Append($",\"targetUnit\":\"{t.gameObject.name}\"");
                sb.Append($",\"killed\":{(unit.lastAttackKilled ? "true" : "false")}");
            }
            else
            {
                sb.Append(",\"target\":null,\"targetUnit\":null,\"killed\":false");
            }

            // Captured hex info (for Capture actions — the actual hex that was captured).
            if (unit.lastAction == UnitAction.Capture)
            {
                var c = unit.lastCapturedHex;
                sb.Append($",\"captured\":[{c.q},{c.r}]");
            }

            // Build target info (where wall/slime was placed).
            if (unit.lastAction == UnitAction.BuildWall || unit.lastAction == UnitAction.PlaceSlime)
            {
                var b = unit.lastBuildTarget;
                sb.Append($",\"built\":[{b.q},{b.r}]");
            }

            sb.Append($",\"rTiles\":{rTiles}");
            sb.Append($",\"mTiles\":{mTiles}");
            sb.Append($",\"rAlive\":{rAlive}");
            sb.Append($",\"mAlive\":{mAlive}");
            sb.Append("}");

            writer.WriteLine(sb.ToString());

            turnCounter++;
            if (turnCounter % 50 == 0)
                writer.Flush();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Replay] Write error: {ex.Message}");
            Close();
        }
    }

    /// <summary>Write summary line with winning territory pattern and close file.</summary>
    public void EndGame(Team winner, int rounds, int rTiles, int mTiles,
                        int rAtk, int mAtk, int rDeaths, int mDeaths,
                        int rBuilds, int mBuilds, HexGrid grid)
    {
        if (!isLogging || writer == null) return;

        try
        {
            float duration = Time.realtimeSinceStartup - gameStartTime;

            var sb = new StringBuilder(512);
            sb.Append("{\"type\":\"summary\"");
            sb.Append($",\"winner\":\"{winner}\"");
            sb.Append($",\"rounds\":{rounds}");
            sb.Append($",\"rTiles\":{rTiles}");
            sb.Append($",\"mTiles\":{mTiles}");
            sb.Append($",\"rAttacks\":{rAtk}");
            sb.Append($",\"mAttacks\":{mAtk}");
            sb.Append($",\"rDeaths\":{rDeaths}");
            sb.Append($",\"mDeaths\":{mDeaths}");
            sb.Append($",\"rBuilds\":{rBuilds}");
            sb.Append($",\"mBuilds\":{mBuilds}");
            sb.Append($",\"duration_sec\":{duration:F1}");
            sb.Append("}");
            writer.WriteLine(sb.ToString());

            // Territory snapshot: all owned tiles for each team + winning connected group.
            WriteTerritorySnapshot(grid, winner);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Replay] Summary write error: {ex.Message}");
        }

        Close();
    }

    /// <summary>
    /// Writes a territory_snapshot line with full board state and the winning connected group.
    /// Format: { type: "territory", robot_tiles: [[q,r],...], mutant_tiles: [...],
    ///           winning_group: [[q,r],...], winning_group_size: N }
    /// </summary>
    private void WriteTerritorySnapshot(HexGrid grid, Team winner)
    {
        if (grid == null) return;

        var sb = new StringBuilder(1024);
        sb.Append("{\"type\":\"territory\"");

        // All owned tiles per team (non-base).
        sb.Append(",\"robot_tiles\":[");
        AppendTeamCoords(sb, grid, Team.Robot);
        sb.Append("]");

        sb.Append(",\"mutant_tiles\":[");
        AppendTeamCoords(sb, grid, Team.Mutant);
        sb.Append("]");

        // Winning connected group — the exact hexes that counted for the win.
        if (winner != Team.None)
        {
            var winningGroup = FindLargestConnectedGroupCoords(grid, winner);
            sb.Append($",\"winning_team\":\"{winner}\"");
            sb.Append($",\"winning_group_size\":{winningGroup.Count}");
            sb.Append(",\"winning_group\":[");
            for (int i = 0; i < winningGroup.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append($"[{winningGroup[i].q},{winningGroup[i].r}]");
            }
            sb.Append("]");
        }
        else
        {
            sb.Append(",\"winning_team\":\"None\"");
            sb.Append(",\"winning_group_size\":0");
            sb.Append(",\"winning_group\":[]");
        }

        sb.Append("}");
        writer.WriteLine(sb.ToString());
    }

    private static void AppendTeamCoords(StringBuilder sb, HexGrid grid, Team team)
    {
        bool first = true;
        foreach (var kvp in grid.Tiles)
        {
            if (kvp.Value.Owner != team) continue;
            if (!first) sb.Append(',');
            sb.Append($"[{kvp.Key.q},{kvp.Key.r}]");
            first = false;
        }
    }

    /// <summary>BFS to find the coords of the largest connected group (non-base tiles).</summary>
    private static System.Collections.Generic.List<HexCoord> FindLargestConnectedGroupCoords(HexGrid grid, Team team)
    {
        var visited = new System.Collections.Generic.HashSet<HexCoord>();
        System.Collections.Generic.List<HexCoord> bestGroup = null;

        foreach (var kvp in grid.Tiles)
        {
            if (kvp.Value.Owner != team) continue;
            if (visited.Contains(kvp.Key)) continue;

            var group = new System.Collections.Generic.List<HexCoord>();
            var queue = new System.Collections.Generic.Queue<HexCoord>();
            queue.Enqueue(kvp.Key);
            visited.Add(kvp.Key);

            while (queue.Count > 0)
            {
                var coord = queue.Dequeue();
                var tile = grid.GetTile(coord);
                if (tile != null)
                    group.Add(coord);

                for (int i = 0; i < 6; i++)
                {
                    var neighbor = coord.Neighbor(i);
                    if (visited.Contains(neighbor)) continue;
                    var nTile = grid.GetTile(neighbor);
                    if (nTile == null || nTile.Owner != team) continue;
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            if (bestGroup == null || group.Count > bestGroup.Count)
                bestGroup = group;
        }

        return bestGroup ?? new System.Collections.Generic.List<HexCoord>();
    }

    /// <summary>Flush and close the current file.</summary>
    public void Close()
    {
        if (writer != null)
        {
            try { writer.Flush(); writer.Close(); writer.Dispose(); }
            catch { /* ignore */ }
            writer = null;
        }
        isLogging = false;
    }
}
