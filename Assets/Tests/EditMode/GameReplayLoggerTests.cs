using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests for GameReplayLogger: file path safety, JSONL format, throttling.
/// </summary>
public class GameReplayLoggerTests
{
    private string tempDir;

    [SetUp]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "ReplayTest_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);
    }

    // ── RULE: replay path must NEVER be under Assets/ ───────────────────

    [Test]
    public void ReplayDir_IsNotUnderAssets()
    {
        var logger = new GameReplayLogger();
        string replayDir = logger.TestGetReplayDir();
        string assetsDir = Path.GetFullPath("Assets");

        Assert.IsFalse(
            replayDir.StartsWith(assetsDir, System.StringComparison.OrdinalIgnoreCase),
            $"Replay directory '{replayDir}' must NOT be under Assets/. " +
            "Writing files to Assets/ triggers Unity Asset Pipeline imports and kills training performance.");
    }

    // ── Throttling ──────────────────────────────────────────────────────

    [Test]
    public void StartGame_SkipsNonNthGame()
    {
        var logger = new TestReplayLogger(tempDir) { logEveryNthGame = 10 };

        logger.StartGame(5, null, (HexGrid)null);
        logger.EndGame(Team.Robot, 10, 5, 3, 1, 1, 0, 0, 0, 0, (HexGrid)null);

        Assert.AreEqual(0, Directory.GetFiles(tempDir, "game_5_*").Length,
            "Non-Nth game should not create a replay file.");
    }

    [Test]
    public void StartGame_WritesNthGame()
    {
        var logger = new TestReplayLogger(tempDir) { logEveryNthGame = 10 };

        logger.StartGame(10, null, (HexGrid)null);
        logger.EndGame(Team.Robot, 10, 5, 3, 1, 1, 0, 0, 0, 0, (HexGrid)null);

        Assert.AreEqual(1, Directory.GetFiles(tempDir, "game_10_*").Length,
            "Nth game should create a replay file.");
    }

    // ── JSONL format ────────────────────────────────────────────────────

    [Test]
    public void WritesValidJsonl_HeaderTurnSummary()
    {
        var logger = new TestReplayLogger(tempDir) { logEveryNthGame = 1 };

        var go = new GameObject("Robot_0");
        var unit = go.AddComponent<UnitData>();
        unit.team = Team.Robot;
        unit.unitIndex = 0;
        unit.Health = 5;
        unit.currentHex = new HexCoord(2, -1);
        unit.lastAction = UnitAction.Move;

        logger.StartGame(1, null, (HexGrid)null);
        logger.LogTurn(1, unit, 10, 8, 6, 6);
        logger.EndGame(Team.Robot, 50, 15, 5, 10, 8, 2, 1, 5, 3, (HexGrid)null);

        Object.DestroyImmediate(go);

        string[] files = Directory.GetFiles(tempDir, "game_*.jsonl");
        Assert.AreEqual(1, files.Length);

        string[] lines = File.ReadAllLines(files[0]);
        Assert.GreaterOrEqual(lines.Length, 3, "Need at least header + turn + summary.");

        // Header line.
        Assert.IsTrue(lines[0].Contains("\"type\":\"header\""));
        Assert.IsTrue(lines[0].Contains("\"match\":1"));
        Assert.IsTrue(lines[0].Contains("\"unitsPerTeam\":"));
        Assert.IsTrue(lines[0].Contains("\"gridSize\":"));

        // Turn line.
        Assert.IsTrue(lines[1].Contains("\"type\":\"turn\""));
        Assert.IsTrue(lines[1].Contains("\"unit\":\"Robot_0\""));
        Assert.IsTrue(lines[1].Contains("\"team\":\"Robot\""));
        Assert.IsTrue(lines[1].Contains("\"pos\":[2,-1]"));
        Assert.IsTrue(lines[1].Contains("\"rTiles\":10"));
        Assert.IsTrue(lines[1].Contains("\"mTiles\":8"));

        // Summary line.
        string last = lines[lines.Length - 1];
        Assert.IsTrue(last.Contains("\"type\":\"summary\""));
        Assert.IsTrue(last.Contains("\"winner\":\"Robot\""));
        Assert.IsTrue(last.Contains("\"rounds\":50"));
        Assert.IsTrue(last.Contains("\"rAttacks\":10"));
        Assert.IsTrue(last.Contains("\"mAttacks\":8"));
    }

    [Test]
    public void AttackTarget_RecordedInTurnLine()
    {
        var logger = new TestReplayLogger(tempDir) { logEveryNthGame = 1 };

        var attackerGo = new GameObject("Robot_0");
        var attacker = attackerGo.AddComponent<UnitData>();
        attacker.team = Team.Robot;
        attacker.Health = 5;
        attacker.currentHex = new HexCoord(1, 0);
        attacker.lastAction = UnitAction.Attack;

        var targetGo = new GameObject("Mutant_0");
        var target = targetGo.AddComponent<UnitData>();
        target.team = Team.Mutant;
        target.currentHex = new HexCoord(2, 0);

        attacker.lastAttackTarget = target;
        attacker.lastAttackKilled = true;

        logger.StartGame(1, null, (HexGrid)null);
        logger.LogTurn(1, attacker, 10, 8, 6, 5);
        logger.EndGame(Team.Robot, 10, 10, 8, 1, 0, 0, 1, 0, 0, (HexGrid)null);

        Object.DestroyImmediate(attackerGo);
        Object.DestroyImmediate(targetGo);

        string[] files = Directory.GetFiles(tempDir, "game_*.jsonl");
        string[] lines = File.ReadAllLines(files[0]);

        Assert.IsTrue(lines[1].Contains("\"targetUnit\":\"Mutant_0\""));
        Assert.IsTrue(lines[1].Contains("\"killed\":true"));
        Assert.IsTrue(lines[1].Contains("\"target\":[2,0]"));
    }

    [Test]
    public void NoAttackTarget_WritesNull()
    {
        var logger = new TestReplayLogger(tempDir) { logEveryNthGame = 1 };

        var go = new GameObject("Robot_0");
        var unit = go.AddComponent<UnitData>();
        unit.team = Team.Robot;
        unit.Health = 5;
        unit.currentHex = new HexCoord(0, 0);
        unit.lastAction = UnitAction.Move;
        unit.lastAttackTarget = null;

        logger.StartGame(1, null, (HexGrid)null);
        logger.LogTurn(1, unit, 5, 5, 6, 6);
        logger.EndGame(Team.None, 10, 5, 5, 0, 0, 0, 0, 0, 0, (HexGrid)null);

        Object.DestroyImmediate(go);

        string[] files = Directory.GetFiles(tempDir, "game_*.jsonl");
        string[] lines = File.ReadAllLines(files[0]);

        Assert.IsTrue(lines[1].Contains("\"target\":null"));
        Assert.IsTrue(lines[1].Contains("\"targetUnit\":null"));
        Assert.IsTrue(lines[1].Contains("\"killed\":false"));
    }

    // ── Test helper ─────────────────────────────────────────────────────

    private class TestReplayLogger : GameReplayLogger
    {
        private readonly string dir;
        public TestReplayLogger(string testDir) { dir = testDir; }
        protected override string GetReplayDir() => dir;
    }
}

/// <summary>
/// Extension to expose GetReplayDir for path safety tests without subclassing.
/// </summary>
public static class GameReplayLoggerTestExtensions
{
    public static string TestGetReplayDir(this GameReplayLogger logger)
    {
        // Call the virtual method to get the actual directory that would be used.
        var method = typeof(GameReplayLogger).GetMethod("GetReplayDir",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (string)method.Invoke(logger, (object[])null);
    }
}
