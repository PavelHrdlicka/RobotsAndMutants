using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

/// <summary>
/// Data model and JSONL parser for replay files.
/// Parses header, turn, and summary lines from GameReplayLogger output.
/// </summary>
public static class ReplayData
{
    public struct Header
    {
        public int match;
        public int unitsPerTeam;
        public int gridSize;
        public int maxRounds;
        public float winThreshold;
    }

    public struct Turn
    {
        public int round;
        public string unitName;
        public string team;
        public string action;
        public int energy;
        public int q, r;
        public bool hasTarget;
        public int targetQ, targetR;
        public string targetUnit;
        public bool killed;
        public int rTiles, mTiles;
        public int rAlive, mAlive;
        public bool hasCaptured;
        public int capturedQ, capturedR;
        public bool hasBuilt;
        public int builtQ, builtR;
        public bool hasAttackHex;
        public int attackHexQ, attackHexR;
    }

    public struct Summary
    {
        public string winner;
        public int rounds;
        public int rTiles, mTiles;
    }

    public class ReplayFile
    {
        public Header header;
        public List<Turn> turns = new List<Turn>();
        public Summary summary;
        public int maxRound;
    }

    /// <summary>Parse a JSONL replay file from disk.</summary>
    public static ReplayFile Parse(string filePath)
    {
        string[] lines = File.ReadAllLines(filePath);
        return ParseLines(lines);
    }

    /// <summary>Parse JSONL lines (testable without file I/O).</summary>
    public static ReplayFile ParseLines(string[] lines)
    {
        var replay = new ReplayFile();

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string type = ExtractString(line, "type");

            if (type == "header")
            {
                replay.header = new Header
                {
                    match = ExtractInt(line, "match"),
                    unitsPerTeam = ExtractInt(line, "unitsPerTeam", 3),
                    gridSize = ExtractInt(line, "gridSize", 5),
                    maxRounds = ExtractInt(line, "maxRounds", 2000),
                    winThreshold = ExtractFloat(line, "winThreshold", 0.6f),
                };
            }
            else if (type == "turn")
            {
                var turn = new Turn
                {
                    round = ExtractInt(line, "round"),
                    unitName = ExtractString(line, "unit"),
                    team = ExtractString(line, "team"),
                    action = ExtractString(line, "action"),
                    energy = line.Contains("\"energy\":") ? ExtractInt(line, "energy") : ExtractInt(line, "hp"),
                    q = ExtractPosQ(line, "pos"),
                    r = ExtractPosR(line, "pos"),
                    killed = line.Contains("\"killed\":true"),
                    rTiles = ExtractInt(line, "rTiles"),
                    mTiles = ExtractInt(line, "mTiles"),
                    rAlive = ExtractInt(line, "rAlive"),
                    mAlive = ExtractInt(line, "mAlive"),
                    targetUnit = ExtractString(line, "targetUnit"),
                };

                // Parse target position — null check.
                turn.hasTarget = !line.Contains("\"target\":null");
                if (turn.hasTarget)
                {
                    turn.targetQ = ExtractPosQ(line, "target");
                    turn.targetR = ExtractPosR(line, "target");
                }

                // Parse captured hex position.
                turn.hasCaptured = line.Contains("\"captured\":[");
                if (turn.hasCaptured)
                {
                    turn.capturedQ = ExtractPosQ(line, "captured");
                    turn.capturedR = ExtractPosR(line, "captured");
                }

                turn.hasBuilt = line.Contains("\"built\":[");
                if (turn.hasBuilt)
                {
                    turn.builtQ = ExtractPosQ(line, "built");
                    turn.builtR = ExtractPosR(line, "built");
                }

                turn.hasAttackHex = line.Contains("\"attackHex\":[");
                if (turn.hasAttackHex)
                {
                    turn.attackHexQ = ExtractPosQ(line, "attackHex");
                    turn.attackHexR = ExtractPosR(line, "attackHex");
                }

                replay.turns.Add(turn);

                if (turn.round > replay.maxRound)
                    replay.maxRound = turn.round;
            }
            else if (type == "summary")
            {
                replay.summary = new Summary
                {
                    winner = ExtractString(line, "winner"),
                    rounds = ExtractInt(line, "rounds"),
                    rTiles = ExtractInt(line, "rTiles"),
                    mTiles = ExtractInt(line, "mTiles"),
                };
                if (replay.summary.rounds > replay.maxRound)
                    replay.maxRound = replay.summary.rounds;
            }
        }

        return replay;
    }

    // ── JSON field extraction ────────────────────────────────────────────

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

    private static float ExtractFloat(string json, string key, float defaultVal = 0f)
    {
        string pattern = $"\"{key}\":";
        int idx = json.IndexOf(pattern, StringComparison.Ordinal);
        if (idx < 0) return defaultVal;
        int start = idx + pattern.Length;
        int end = start;
        while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.' || json[end] == ',' || json[end] == '-'))
            end++;
        string s = json.Substring(start, end - start).Replace(',', '.');
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
            return val;
        return defaultVal;
    }

    private static int ExtractPosQ(string json, string key)
    {
        string pattern = $"\"{key}\":[";
        int idx = json.IndexOf(pattern, StringComparison.Ordinal);
        if (idx < 0) return 0;
        int start = idx + pattern.Length;
        int comma = json.IndexOf(',', start);
        if (comma > start && int.TryParse(json.Substring(start, comma - start), out int val))
            return val;
        return 0;
    }

    private static int ExtractPosR(string json, string key)
    {
        string pattern = $"\"{key}\":[";
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
