using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Detects interesting strategic moments in replay files.
/// Outputs a compact highlights.json for AI-assisted analysis.
/// Patterns detected: comeback, territory_swing, coordinated_attack,
/// flanking, wipe_event, blitz_win, close_game, stalemate_break, kill_streak.
/// </summary>
public static class HighlightDetector
{
    // ── Data structures ──────────────────────────────────────────────────

    public struct TurnData
    {
        public int round;
        public string unit;
        public string team;
        public string action;
        public int energy;
        public int q, r;
        public int targetQ, targetR;
        public string targetUnit;
        public bool killed;
        public int rTiles, mTiles;
        public int rAlive, mAlive;
    }

    public struct HeaderData
    {
        public int match;
        public int unitsPerTeam;
        public int gridSize;
        public int maxRounds;
        public float winThreshold;
    }

    public struct SummaryData
    {
        public string winner;
        public int rounds;
        public int rTiles, mTiles;
    }

    public class Highlight
    {
        public string type;
        public int roundStart;
        public int roundEnd;
        public string team;
        public string description;
    }

    public class GameHighlights
    {
        public string fileName;
        public int match;
        public string winner;
        public int rounds;
        public int interestingness;
        public List<Highlight> highlights = new List<Highlight>();
    }

    // ── Main API ─────────────────────────────────────────────────────────

    /// <summary>Analyze all replay files and write highlights.json.</summary>
    public static string AnalyzeAndExport(int lastN = 0)
    {
        string replayDir = GameReplayLogger.TrainingReplayDir;
        if (!Directory.Exists(replayDir))
        {
            Debug.LogWarning("[Highlights] No Training replays directory found.");
            return null;
        }

        var files = Directory.GetFiles(replayDir, "game_*.jsonl")
            .OrderByDescending(f => File.GetLastWriteTime(f));

        var fileList = lastN > 0 ? files.Take(lastN).ToList() : files.ToList();

        var allHighlights = new List<GameHighlights>();
        foreach (var file in fileList)
        {
            try
            {
                var lines = File.ReadAllLines(file);
                var gh = Analyze(lines, Path.GetFileName(file));
                if (gh.highlights.Count > 0)
                    allHighlights.Add(gh);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Highlights] Error parsing {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        // Sort by interestingness descending.
        allHighlights.Sort((a, b) => b.interestingness.CompareTo(a.interestingness));

        string outputPath = Path.Combine(replayDir, "highlights.json");
        WriteHighlightsJson(allHighlights, fileList.Count, outputPath);

        Debug.Log($"[Highlights] Found {allHighlights.Count} interesting games out of {fileList.Count}. " +
                  $"Output: {outputPath}");

        return outputPath;
    }

    /// <summary>Analyze a single replay file's lines. Testable without file I/O.</summary>
    public static GameHighlights Analyze(string[] lines, string fileName = "")
    {
        var gh = new GameHighlights { fileName = fileName };

        // Parse data.
        var header = new HeaderData { unitsPerTeam = 3, maxRounds = 2000 };
        var summary = new SummaryData();
        var turns = new List<TurnData>();

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string type = ExtractString(line, "type");
            if (type == "header")
            {
                header.match = ExtractInt(line, "match");
                header.unitsPerTeam = ExtractInt(line, "unitsPerTeam", 3);
                header.gridSize = ExtractInt(line, "gridSize", 5);
                header.maxRounds = ExtractInt(line, "maxRounds", 2000);
                header.winThreshold = ExtractFloat(line, "winThreshold", 0.6f);
            }
            else if (type == "turn")
            {
                var td = new TurnData
                {
                    round = ExtractInt(line, "round"),
                    unit = ExtractString(line, "unit"),
                    team = ExtractString(line, "team"),
                    action = ExtractString(line, "action"),
                    energy = line.Contains("\"energy\":") ? ExtractInt(line, "energy") : ExtractInt(line, "hp"),
                    q = ExtractPosQ(line),
                    r = ExtractPosR(line),
                    killed = line.Contains("\"killed\":true"),
                    rTiles = ExtractInt(line, "rTiles"),
                    mTiles = ExtractInt(line, "mTiles"),
                    rAlive = ExtractInt(line, "rAlive"),
                    mAlive = ExtractInt(line, "mAlive"),
                    targetUnit = ExtractString(line, "targetUnit"),
                };
                ExtractTarget(line, out td.targetQ, out td.targetR);
                turns.Add(td);
            }
            else if (type == "summary")
            {
                summary.winner = ExtractString(line, "winner");
                summary.rounds = ExtractInt(line, "rounds");
                summary.rTiles = ExtractInt(line, "rTiles");
                summary.mTiles = ExtractInt(line, "mTiles");
            }
        }

        gh.match = header.match;
        gh.winner = summary.winner;
        gh.rounds = summary.rounds;

        // Run all detectors.
        DetectComeback(gh, turns, summary);
        DetectTerritorySwing(gh, turns);
        DetectCoordinatedAttack(gh, turns);
        DetectFlanking(gh, turns);
        DetectWipeEvent(gh, turns);
        DetectBlitzWin(gh, summary, header);
        DetectCloseGame(gh, summary, header);
        DetectStalemateBreak(gh, turns);
        DetectKillStreak(gh, turns);

        // Compute interestingness score.
        int score = 0;
        foreach (var h in gh.highlights)
        {
            score += h.type switch
            {
                "comeback" => 3,
                "territory_swing" => 2,
                "coordinated_attack" => 1,
                "flanking" => 2,
                "wipe_event" => 2,
                "blitz_win" => 1,
                "close_game" => 2,
                "stalemate_break" => 2,
                "kill_streak" => 1,
                _ => 1,
            };
        }
        gh.interestingness = Mathf.Clamp(score, 0, 5);

        return gh;
    }

    // ── Detectors ────────────────────────────────────────────────────────

    /// <summary>Winner was losing by >10 tiles at some point.</summary>
    internal static void DetectComeback(GameHighlights gh, List<TurnData> turns, SummaryData summary)
    {
        if (string.IsNullOrEmpty(summary.winner) || summary.winner == "None") return;
        bool winnerIsRobot = summary.winner == "Robot";

        int maxDeficit = 0;
        int worstRound = 0;

        foreach (var t in turns)
        {
            int deficit = winnerIsRobot ? (t.mTiles - t.rTiles) : (t.rTiles - t.mTiles);
            if (deficit > maxDeficit)
            {
                maxDeficit = deficit;
                worstRound = t.round;
            }
        }

        if (maxDeficit > 10)
        {
            gh.highlights.Add(new Highlight
            {
                type = "comeback",
                roundStart = worstRound,
                roundEnd = summary.rounds,
                team = summary.winner,
                description = $"{summary.winner} was losing by {maxDeficit} tiles at round {worstRound}, then came back to win."
            });
        }
    }

    /// <summary>Territory change of >8 tiles within 10 rounds.</summary>
    internal static void DetectTerritorySwing(GameHighlights gh, List<TurnData> turns)
    {
        if (turns.Count == 0) return;

        // Build per-round territory snapshots (last turn data per round).
        var roundData = BuildRoundSnapshots(turns);
        var rounds = roundData.Keys.OrderBy(r => r).ToList();

        for (int i = 0; i < rounds.Count; i++)
        {
            // Look 10 rounds ahead.
            int startRound = rounds[i];
            int endRound = startRound + 10;

            // Find the closest round <= endRound.
            int j = i;
            while (j < rounds.Count - 1 && rounds[j + 1] <= endRound) j++;
            if (j == i) continue;

            var start = roundData[rounds[i]];
            var end = roundData[rounds[j]];

            int rSwing = Math.Abs(end.rTiles - start.rTiles);
            int mSwing = Math.Abs(end.mTiles - start.mTiles);

            if (rSwing > 8 || mSwing > 8)
            {
                string team = rSwing > mSwing ? "Robot" : "Mutant";
                int swing = Math.Max(rSwing, mSwing);
                gh.highlights.Add(new Highlight
                {
                    type = "territory_swing",
                    roundStart = rounds[i],
                    roundEnd = rounds[j],
                    team = team,
                    description = $"{team} territory changed by {swing} tiles between rounds {rounds[i]}-{rounds[j]}."
                });
                // Skip ahead to avoid duplicate detections for same swing.
                i = j;
            }
        }
    }

    /// <summary>2+ units of same team attack in the same round.</summary>
    internal static void DetectCoordinatedAttack(GameHighlights gh, List<TurnData> turns)
    {
        var attacksByRoundTeam = new Dictionary<(int round, string team), List<TurnData>>();

        foreach (var t in turns)
        {
            if (t.action != "Attack") continue;
            var key = (t.round, t.team);
            if (!attacksByRoundTeam.ContainsKey(key))
                attacksByRoundTeam[key] = new List<TurnData>();
            attacksByRoundTeam[key].Add(t);
        }

        foreach (var kvp in attacksByRoundTeam)
        {
            if (kvp.Value.Count >= 2)
            {
                int kills = kvp.Value.Count(t => t.killed);
                gh.highlights.Add(new Highlight
                {
                    type = "coordinated_attack",
                    roundStart = kvp.Key.round,
                    roundEnd = kvp.Key.round,
                    team = kvp.Key.team,
                    description = $"{kvp.Value.Count} {kvp.Key.team} units attacked in round {kvp.Key.round}" +
                                  (kills > 0 ? $", scoring {kills} kill(s)." : ".")
                });
            }
        }
    }

    /// <summary>Unit attacks with 2+ allies on adjacent hexes.</summary>
    internal static void DetectFlanking(GameHighlights gh, List<TurnData> turns)
    {
        // Group turns by round.
        var roundGroups = new Dictionary<int, List<TurnData>>();
        foreach (var t in turns)
        {
            if (!roundGroups.ContainsKey(t.round))
                roundGroups[t.round] = new List<TurnData>();
            roundGroups[t.round].Add(t);
        }

        foreach (var kvp in roundGroups)
        {
            foreach (var t in kvp.Value)
            {
                if (t.action != "Attack") continue;

                // Count allies adjacent to attacker in this round.
                int adjacentAllies = 0;
                foreach (var other in kvp.Value)
                {
                    if (other.unit == t.unit) continue;
                    if (other.team != t.team) continue;
                    if (HexDistance(t.q, t.r, other.q, other.r) == 1)
                        adjacentAllies++;
                }

                if (adjacentAllies >= 2)
                {
                    gh.highlights.Add(new Highlight
                    {
                        type = "flanking",
                        roundStart = kvp.Key,
                        roundEnd = kvp.Key,
                        team = t.team,
                        description = $"{t.unit} attacked with {adjacentAllies} allies adjacent in round {kvp.Key}" +
                                      (t.killed ? " — killed the target!" : ".")
                    });
                }
            }
        }
    }

    /// <summary>All units of one team dead at the same time.</summary>
    internal static void DetectWipeEvent(GameHighlights gh, List<TurnData> turns)
    {
        var reported = new HashSet<int>(); // avoid duplicate reports per round

        foreach (var t in turns)
        {
            if (reported.Contains(t.round)) continue;

            string wipedTeam = null;
            if (t.rAlive == 0) wipedTeam = "Robot";
            else if (t.mAlive == 0) wipedTeam = "Mutant";

            if (wipedTeam != null)
            {
                reported.Add(t.round);
                string otherTeam = wipedTeam == "Robot" ? "Mutant" : "Robot";
                gh.highlights.Add(new Highlight
                {
                    type = "wipe_event",
                    roundStart = t.round,
                    roundEnd = t.round,
                    team = otherTeam,
                    description = $"All {wipedTeam} units dead at round {t.round} — total wipe by {otherTeam}!"
                });
            }
        }
    }

    /// <summary>Game ends in under 30% of max steps.</summary>
    internal static void DetectBlitzWin(GameHighlights gh, SummaryData summary, HeaderData header)
    {
        if (string.IsNullOrEmpty(summary.winner) || summary.winner == "None") return;
        if (header.maxRounds <= 0) return;

        float ratio = (float)summary.rounds / header.maxRounds;
        if (ratio < 0.3f)
        {
            gh.highlights.Add(new Highlight
            {
                type = "blitz_win",
                roundStart = 1,
                roundEnd = summary.rounds,
                team = summary.winner,
                description = $"{summary.winner} won in just {summary.rounds} rounds ({ratio * 100:F0}% of max {header.maxRounds}) — blitz victory!"
            });
        }
    }

    /// <summary>Final tile difference is less than 5% of total contestable tiles.</summary>
    internal static void DetectCloseGame(GameHighlights gh, SummaryData summary, HeaderData header)
    {
        int totalTiles = 3 * header.gridSize * header.gridSize - 3 * header.gridSize + 1;
        if (totalTiles <= 0) return;

        int diff = Math.Abs(summary.rTiles - summary.mTiles);
        float diffPct = (float)diff / totalTiles * 100f;

        if (diffPct < 5f && summary.rounds > 0)
        {
            gh.highlights.Add(new Highlight
            {
                type = "close_game",
                roundStart = 1,
                roundEnd = summary.rounds,
                team = summary.winner ?? "None",
                description = $"Extremely close game — final difference only {diff} tiles ({diffPct:F1}%). " +
                              $"R:{summary.rTiles} vs M:{summary.mTiles}."
            });
        }
    }

    /// <summary>50+ rounds with no tile change, then sudden shift.</summary>
    internal static void DetectStalemateBreak(GameHighlights gh, List<TurnData> turns)
    {
        if (turns.Count == 0) return;

        var roundData = BuildRoundSnapshots(turns);
        var rounds = roundData.Keys.OrderBy(r => r).ToList();
        if (rounds.Count < 2) return;

        int staleStart = rounds[0];
        int prevR = roundData[rounds[0]].rTiles;
        int prevM = roundData[rounds[0]].mTiles;

        for (int i = 1; i < rounds.Count; i++)
        {
            var snap = roundData[rounds[i]];
            bool changed = snap.rTiles != prevR || snap.mTiles != prevM;

            if (changed)
            {
                int staleLen = rounds[i] - staleStart;
                if (staleLen >= 50)
                {
                    int swing = Math.Abs(snap.rTiles - prevR) + Math.Abs(snap.mTiles - prevM);
                    gh.highlights.Add(new Highlight
                    {
                        type = "stalemate_break",
                        roundStart = staleStart,
                        roundEnd = rounds[i],
                        team = snap.rTiles > prevR ? "Robot" : "Mutant",
                        description = $"Stalemate for {staleLen} rounds ({staleStart}-{rounds[i]}), then territory shifted by {swing} tiles."
                    });
                }
                staleStart = rounds[i];
                prevR = snap.rTiles;
                prevM = snap.mTiles;
            }
        }
    }

    /// <summary>A single unit kills 3+ enemies within 20 rounds.</summary>
    internal static void DetectKillStreak(GameHighlights gh, List<TurnData> turns)
    {
        // Collect kill events per unit.
        var killsByUnit = new Dictionary<string, List<int>>();

        foreach (var t in turns)
        {
            if (!t.killed) continue;
            if (!killsByUnit.ContainsKey(t.unit))
                killsByUnit[t.unit] = new List<int>();
            killsByUnit[t.unit].Add(t.round);
        }

        foreach (var kvp in killsByUnit)
        {
            var killRounds = kvp.Value;
            if (killRounds.Count < 3) continue;

            // Sliding window of 20 rounds.
            for (int i = 0; i <= killRounds.Count - 3; i++)
            {
                int windowEnd = killRounds[i] + 20;
                int count = 0;
                int lastRound = killRounds[i];
                for (int j = i; j < killRounds.Count && killRounds[j] <= windowEnd; j++)
                {
                    count++;
                    lastRound = killRounds[j];
                }

                if (count >= 3)
                {
                    string team = turns.First(t => t.unit == kvp.Key).team;
                    gh.highlights.Add(new Highlight
                    {
                        type = "kill_streak",
                        roundStart = killRounds[i],
                        roundEnd = lastRound,
                        team = team,
                        description = $"{kvp.Key} scored {count} kills in rounds {killRounds[i]}-{lastRound} — kill streak!"
                    });
                    break; // One per unit.
                }
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private struct RoundSnapshot
    {
        public int rTiles, mTiles;
    }

    private static Dictionary<int, RoundSnapshot> BuildRoundSnapshots(List<TurnData> turns)
    {
        var result = new Dictionary<int, RoundSnapshot>();
        foreach (var t in turns)
            result[t.round] = new RoundSnapshot { rTiles = t.rTiles, mTiles = t.mTiles };
        return result;
    }

    public static int HexDistance(int q1, int r1, int q2, int r2)
    {
        int dq = q1 - q2;
        int dr = r1 - r2;
        return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
    }

    // ── JSON output ─────────────────────────────────────────────────────

    private static void WriteHighlightsJson(List<GameHighlights> games, int totalAnalyzed, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"generated\": \"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}\",");
        sb.AppendLine($"  \"totalGamesAnalyzed\": {totalAnalyzed},");
        sb.AppendLine($"  \"gamesWithHighlights\": {games.Count},");
        sb.AppendLine("  \"games\": [");

        for (int g = 0; g < games.Count; g++)
        {
            var game = games[g];
            sb.AppendLine("    {");
            sb.AppendLine($"      \"file\": \"{EscapeJson(game.fileName)}\",");
            sb.AppendLine($"      \"match\": {game.match},");
            sb.AppendLine($"      \"winner\": \"{EscapeJson(game.winner ?? "None")}\",");
            sb.AppendLine($"      \"rounds\": {game.rounds},");
            sb.AppendLine($"      \"interestingness\": {game.interestingness},");
            sb.AppendLine("      \"highlights\": [");

            for (int h = 0; h < game.highlights.Count; h++)
            {
                var hl = game.highlights[h];
                sb.AppendLine("        {");
                sb.AppendLine($"          \"type\": \"{EscapeJson(hl.type)}\",");
                sb.AppendLine($"          \"roundStart\": {hl.roundStart},");
                sb.AppendLine($"          \"roundEnd\": {hl.roundEnd},");
                sb.AppendLine($"          \"team\": \"{EscapeJson(hl.team ?? "")}\",");
                sb.AppendLine($"          \"description\": \"{EscapeJson(hl.description)}\"");
                sb.Append("        }");
                if (h < game.highlights.Count - 1) sb.Append(",");
                sb.AppendLine();
            }

            sb.AppendLine("      ]");
            sb.Append("    }");
            if (g < games.Count - 1) sb.Append(",");
            sb.AppendLine();
        }

        sb.AppendLine("  ]");
        sb.AppendLine("}");

        File.WriteAllText(outputPath, sb.ToString());
    }

    private static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }

    // ── JSON field extraction (same pattern as StrategyAnalyzer) ─────────

    internal static string ExtractString(string json, string key)
    {
        string pattern = $"\"{key}\":\"";
        int idx = json.IndexOf(pattern, StringComparison.Ordinal);
        if (idx < 0) return "";
        int start = idx + pattern.Length;
        int end = json.IndexOf('"', start);
        return end > start ? json.Substring(start, end - start) : "";
    }

    internal static int ExtractInt(string json, string key, int defaultVal = 0)
    {
        string pattern = $"\"{key}\":";
        int idx = json.IndexOf(pattern, StringComparison.Ordinal);
        if (idx < 0) return defaultVal;
        int start = idx + pattern.Length;
        int end = start;
        while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
            end++;
        if (end > start && int.TryParse(json.Substring(start, end - start), out int val))
            return val;
        return defaultVal;
    }

    internal static float ExtractFloat(string json, string key, float defaultVal = 0f)
    {
        string pattern = $"\"{key}\":";
        int idx = json.IndexOf(pattern, StringComparison.Ordinal);
        if (idx < 0) return defaultVal;
        int start = idx + pattern.Length;
        int end = start;
        while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.' || json[end] == '-'))
            end++;
        if (end > start && float.TryParse(json.Substring(start, end - start),
                NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
            return val;
        return defaultVal;
    }

    internal static int ExtractPosQ(string json)
    {
        string pattern = "\"pos\":[";
        int idx = json.IndexOf(pattern, StringComparison.Ordinal);
        if (idx < 0) return 0;
        int start = idx + pattern.Length;
        int comma = json.IndexOf(',', start);
        if (comma > start && int.TryParse(json.Substring(start, comma - start), out int val))
            return val;
        return 0;
    }

    internal static int ExtractPosR(string json)
    {
        string pattern = "\"pos\":[";
        int idx = json.IndexOf(pattern, StringComparison.Ordinal);
        if (idx < 0) return 0;
        int comma = json.IndexOf(',', idx + pattern.Length);
        if (comma < 0) return 0;
        int start = comma + 1;
        int end = json.IndexOf(']', start);
        if (end > start && int.TryParse(json.Substring(start, end - start), out int val))
            return val;
        return 0;
    }

    private static void ExtractTarget(string json, out int q, out int r)
    {
        q = 0; r = 0;
        string pattern = "\"target\":[";
        int idx = json.IndexOf(pattern, StringComparison.Ordinal);
        if (idx < 0) return;
        int start = idx + pattern.Length;
        int comma = json.IndexOf(',', start);
        if (comma < 0) return;
        int end = json.IndexOf(']', comma);
        if (end < 0) return;
        int.TryParse(json.Substring(start, comma - start), out q);
        int.TryParse(json.Substring(comma + 1, end - comma - 1), out r);
    }
}
