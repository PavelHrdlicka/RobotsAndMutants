using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Reads JSONL replay files and computes per-team strategy metrics.
/// Editor-only analysis tool.
/// </summary>
public static class StrategyAnalyzer
{
    public struct GameMetrics
    {
        public string fileName;
        public int match;
        public string winner;
        public int rounds;
        public int unitsPerTeam;

        // Per-team totals from summary.
        public int rAttacks, mAttacks;
        public int rDeaths, mDeaths;
        public int rBuilds, mBuilds;
        public int rTilesFinal, mTilesFinal;

        // Computed per-round-per-unit rates.
        public float rAggressionRate, mAggressionRate;
        public float rBuildRate, mBuildRate;
        public float rKillEfficiency, mKillEfficiency;

        // Action distribution (from turn lines).
        public Dictionary<string, int> rActionDist, mActionDist;

        // Cluster index: avg inter-unit distance per team (from turn snapshots).
        public float rClusterIndex, mClusterIndex;

        // Strategy labels.
        public List<string> rStrategies, mStrategies;
    }

    private static readonly string ReplayDir =
        Path.GetFullPath("Replays");

    /// <summary>Analyze the last N replay files.</summary>
    public static List<GameMetrics> AnalyzeLast(int count)
    {
        if (!Directory.Exists(ReplayDir))
        {
            Debug.LogWarning("[StrategyAnalyzer] No Replays directory found.");
            return new List<GameMetrics>();
        }

        var files = Directory.GetFiles(ReplayDir, "game_*.jsonl")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .Take(count)
            .Reverse()
            .ToList();

        return AnalyzeFiles(files);
    }

    /// <summary>Analyze all replay files.</summary>
    public static List<GameMetrics> AnalyzeAll()
    {
        if (!Directory.Exists(ReplayDir))
        {
            Debug.LogWarning("[StrategyAnalyzer] No Replays directory found.");
            return new List<GameMetrics>();
        }

        var files = Directory.GetFiles(ReplayDir, "game_*.jsonl")
            .OrderBy(f => File.GetLastWriteTime(f))
            .ToList();

        return AnalyzeFiles(files);
    }

    private static List<GameMetrics> AnalyzeFiles(List<string> files)
    {
        var results = new List<GameMetrics>();
        foreach (var file in files)
        {
            try
            {
                var metrics = AnalyzeFile(file);
                results.Add(metrics);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StrategyAnalyzer] Error parsing {Path.GetFileName(file)}: {ex.Message}");
            }
        }
        return results;
    }

    private static GameMetrics AnalyzeFile(string filePath)
    {
        var m = new GameMetrics
        {
            fileName = Path.GetFileName(filePath),
            rActionDist = new Dictionary<string, int>(),
            mActionDist = new Dictionary<string, int>(),
            rStrategies = new List<string>(),
            mStrategies = new List<string>(),
        };

        // Collect unit positions per round for cluster analysis.
        // Key: round, Value: list of (team, q, r).
        var robotPositions = new List<(int q, int r)>();
        var mutantPositions = new List<(int q, int r)>();
        int positionSamples = 0;
        float rDistSum = 0, mDistSum = 0;

        string[] lines = File.ReadAllLines(filePath);
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string type = ExtractString(line, "type");

            if (type == "header")
            {
                m.match = ExtractInt(line, "match");
                m.unitsPerTeam = ExtractInt(line, "unitsPerTeam", 3);
            }
            else if (type == "turn")
            {
                string team = ExtractString(line, "team");
                string action = ExtractString(line, "action");
                int q = ExtractPosQ(line);
                int r = ExtractPosR(line);

                var dist = team == "Robot" ? m.rActionDist : m.mActionDist;
                if (!dist.ContainsKey(action)) dist[action] = 0;
                dist[action]++;

                // Sample positions for cluster index (every 10th turn line to limit computation).
                if (team == "Robot") robotPositions.Add((q, r));
                else mutantPositions.Add((q, r));
            }
            else if (type == "summary")
            {
                m.winner = ExtractString(line, "winner");
                m.rounds = ExtractInt(line, "rounds");
                m.rAttacks = ExtractInt(line, "rAttacks");
                m.mAttacks = ExtractInt(line, "mAttacks");
                m.rDeaths = ExtractInt(line, "rDeaths");
                m.mDeaths = ExtractInt(line, "mDeaths");
                m.rBuilds = ExtractInt(line, "rBuilds");
                m.mBuilds = ExtractInt(line, "mBuilds");
                m.rTilesFinal = ExtractInt(line, "rTiles");
                m.mTilesFinal = ExtractInt(line, "mTiles");
            }
        }

        // Compute rates.
        float rounds = Mathf.Max(m.rounds, 1);
        float units = Mathf.Max(m.unitsPerTeam, 1);

        m.rAggressionRate = m.rAttacks / rounds / units;
        m.mAggressionRate = m.mAttacks / rounds / units;
        m.rBuildRate = m.rBuilds / rounds / units;
        m.mBuildRate = m.mBuilds / rounds / units;
        m.rKillEfficiency = m.rAttacks > 0 ? (float)m.mDeaths / m.rAttacks : 0f;
        m.mKillEfficiency = m.mAttacks > 0 ? (float)m.rDeaths / m.mAttacks : 0f;

        // Compute cluster index from sampled positions.
        m.rClusterIndex = ComputeClusterIndex(robotPositions);
        m.mClusterIndex = ComputeClusterIndex(mutantPositions);

        // Classify strategies.
        m.rStrategies = ClassifyStrategy(m.rAggressionRate, m.rBuildRate, m.rKillEfficiency, m.rClusterIndex);
        m.mStrategies = ClassifyStrategy(m.mAggressionRate, m.mBuildRate, m.mKillEfficiency, m.mClusterIndex);

        return m;
    }

    private static float ComputeClusterIndex(List<(int q, int r)> positions)
    {
        if (positions.Count < 2) return 0f;

        // Sample every 10th position to avoid O(n^2) on large games.
        var sampled = new List<(int q, int r)>();
        for (int i = 0; i < positions.Count; i += 10)
            sampled.Add(positions[i]);

        if (sampled.Count < 2) return 0f;

        float totalDist = 0;
        int pairs = 0;
        for (int i = 0; i < sampled.Count; i++)
        {
            for (int j = i + 1; j < sampled.Count; j++)
            {
                totalDist += HexDistance(sampled[i].q, sampled[i].r, sampled[j].q, sampled[j].r);
                pairs++;
            }
        }
        return pairs > 0 ? totalDist / pairs : 0f;
    }

    private static float HexDistance(int q1, int r1, int q2, int r2)
    {
        int dq = q1 - q2;
        int dr = r1 - r2;
        return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2f;
    }

    private static List<string> ClassifyStrategy(float aggression, float buildRate, float killEff, float cluster)
    {
        var tags = new List<string>();

        if (aggression > 0.3f)
            tags.Add("Aggressive");
        if (buildRate > 0.2f && aggression < 0.15f)
            tags.Add("Builder");
        if (cluster < 2.0f && buildRate > 0.1f)
            tags.Add("Turtle");
        if (aggression > 0.2f && killEff > 0.3f)
            tags.Add("Efficient Hunter");

        if (tags.Count == 0)
            tags.Add("Balanced");

        return tags;
    }

    /// <summary>Log analysis results to console and export CSV.</summary>
    public static void LogResults(List<GameMetrics> results)
    {
        if (results.Count == 0)
        {
            Debug.Log("[StrategyAnalyzer] No games to analyze.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("[StrategyAnalyzer] Analysis Results:");
        sb.AppendLine("─────────────────────────────────────────");

        foreach (var m in results)
        {
            sb.AppendLine($"Game #{m.match} ({m.fileName}) — Winner: {m.winner}, Rounds: {m.rounds}");
            sb.AppendLine($"  Robots:  Aggr={m.rAggressionRate:F2}/rnd/unit  Build={m.rBuildRate:F2}/rnd/unit  KillEff={m.rKillEfficiency:F2}  Cluster={m.rClusterIndex:F1}  → [{string.Join(", ", m.rStrategies)}]");
            sb.AppendLine($"  Mutants: Aggr={m.mAggressionRate:F2}/rnd/unit  Build={m.mBuildRate:F2}/rnd/unit  KillEff={m.mKillEfficiency:F2}  Cluster={m.mClusterIndex:F1}  → [{string.Join(", ", m.mStrategies)}]");
        }

        Debug.Log(sb.ToString());

        // Export CSV.
        ExportCsv(results);
    }

    private static void ExportCsv(List<GameMetrics> results)
    {
        try
        {
            string csvPath = Path.Combine(ReplayDir, "analysis.csv");
            var sb = new StringBuilder();
            sb.AppendLine("match,winner,rounds,rTiles,mTiles,rAttacks,mAttacks,rDeaths,mDeaths,rBuilds,mBuilds,rAggression,mAggression,rBuildRate,mBuildRate,rKillEff,mKillEff,rCluster,mCluster,rStrategy,mStrategy");

            foreach (var m in results)
            {
                sb.AppendLine(string.Join(",",
                    m.match, m.winner, m.rounds,
                    m.rTilesFinal, m.mTilesFinal,
                    m.rAttacks, m.mAttacks, m.rDeaths, m.mDeaths, m.rBuilds, m.mBuilds,
                    m.rAggressionRate.ToString("F3", CultureInfo.InvariantCulture),
                    m.mAggressionRate.ToString("F3", CultureInfo.InvariantCulture),
                    m.rBuildRate.ToString("F3", CultureInfo.InvariantCulture),
                    m.mBuildRate.ToString("F3", CultureInfo.InvariantCulture),
                    m.rKillEfficiency.ToString("F3", CultureInfo.InvariantCulture),
                    m.mKillEfficiency.ToString("F3", CultureInfo.InvariantCulture),
                    m.rClusterIndex.ToString("F1", CultureInfo.InvariantCulture),
                    m.mClusterIndex.ToString("F1", CultureInfo.InvariantCulture),
                    $"\"{string.Join(";", m.rStrategies)}\"",
                    $"\"{string.Join(";", m.mStrategies)}\""));
            }

            File.WriteAllText(csvPath, sb.ToString());
            Debug.Log($"[StrategyAnalyzer] CSV exported to: {csvPath}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StrategyAnalyzer] CSV export failed: {ex.Message}");
        }
    }

    // ── Simple JSON field extraction (no dependency on external JSON library) ──

    private static string ExtractString(string json, string key)
    {
        string pattern = $"\"{key}\":\"";
        int idx = json.IndexOf(pattern, StringComparison.Ordinal);
        if (idx < 0) return "";
        int start = idx + pattern.Length;
        int end = json.IndexOf('"', start);
        return end > start ? json.Substring(start, end - start) : "";
    }

    private static int ExtractInt(string json, string key, int defaultVal = 0)
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

    private static int ExtractPosQ(string json)
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

    private static int ExtractPosR(string json)
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
}
