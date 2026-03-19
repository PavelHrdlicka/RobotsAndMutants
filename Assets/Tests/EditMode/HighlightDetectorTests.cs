using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// EditMode tests for HighlightDetector — each detector tested independently
/// with crafted JSONL data.
/// </summary>
public class HighlightDetectorTests
{
    // ── Helpers to build JSONL lines ─────────────────────────────────────

    private static string Header(int match = 1, int units = 3, int grid = 5, int maxRounds = 2000)
        => $"{{\"type\":\"header\",\"match\":{match},\"unitsPerTeam\":{units},\"gridSize\":{grid},\"maxRounds\":{maxRounds},\"winThreshold\":0.60}}";

    private static string Turn(int round, string unit, string team, string action,
        int hp, int q, int r, int rTiles, int mTiles, int rAlive = 3, int mAlive = 3,
        string targetUnit = null, int tq = 0, int tr = 0, bool killed = false)
    {
        string target = targetUnit != null
            ? $",\"target\":[{tq},{tr}],\"targetUnit\":\"{targetUnit}\",\"killed\":{(killed ? "true" : "false")}"
            : ",\"target\":null,\"targetUnit\":null,\"killed\":false";
        return $"{{\"type\":\"turn\",\"round\":{round},\"unit\":\"{unit}\",\"team\":\"{team}\"," +
               $"\"action\":\"{action}\",\"hp\":{hp},\"pos\":[{q},{r}]" +
               $"{target},\"rTiles\":{rTiles},\"mTiles\":{mTiles},\"rAlive\":{rAlive},\"mAlive\":{mAlive}}}";
    }

    private static string Summary(string winner, int rounds, int rTiles, int mTiles)
        => $"{{\"type\":\"summary\",\"winner\":\"{winner}\",\"rounds\":{rounds},\"rTiles\":{rTiles},\"mTiles\":{mTiles}," +
           $"\"rAttacks\":0,\"mAttacks\":0,\"rDeaths\":0,\"mDeaths\":0,\"rBuilds\":0,\"mBuilds\":0,\"duration_sec\":10.0}}";

    // ── Comeback ─────────────────────────────────────────────────────────

    [Test]
    public void DetectComeback_WinnerWasLosing()
    {
        var lines = new[]
        {
            Header(),
            Turn(10, "Robot_0", "Robot", "Move", 7, 0, 0, 5, 20, 3, 3),   // Robot losing by 15
            Turn(50, "Robot_0", "Robot", "Move", 7, 1, 0, 18, 15, 3, 3),  // Robot catching up
            Summary("Robot", 100, 25, 10),
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        var comebacks = gh.highlights.Where(h => h.type == "comeback").ToList();
        Assert.AreEqual(1, comebacks.Count);
        Assert.AreEqual("Robot", comebacks[0].team);
        Assert.IsTrue(comebacks[0].description.Contains("15 tiles"));
    }

    [Test]
    public void DetectComeback_NoComeback_WhenDeficitSmall()
    {
        var lines = new[]
        {
            Header(),
            Turn(10, "Robot_0", "Robot", "Move", 7, 0, 0, 10, 15, 3, 3),  // Only 5 behind
            Summary("Robot", 100, 20, 10),
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        Assert.AreEqual(0, gh.highlights.Count(h => h.type == "comeback"));
    }

    [Test]
    public void DetectComeback_IgnoresDrawOrNone()
    {
        var lines = new[]
        {
            Header(),
            Turn(10, "Robot_0", "Robot", "Move", 7, 0, 0, 5, 20, 3, 3),
            Summary("None", 100, 15, 15),
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        Assert.AreEqual(0, gh.highlights.Count(h => h.type == "comeback"));
    }

    // ── Territory Swing ──────────────────────────────────────────────────

    [Test]
    public void DetectTerritorySwing_LargeChange()
    {
        var lines = new[]
        {
            Header(),
            Turn(10, "Robot_0", "Robot", "Move", 7, 0, 0, 10, 10, 3, 3),
            Turn(18, "Robot_0", "Robot", "Move", 7, 1, 0, 20, 10, 3, 3),  // +10 in 8 rounds
            Summary("Robot", 50, 25, 10),
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        var swings = gh.highlights.Where(h => h.type == "territory_swing").ToList();
        Assert.AreEqual(1, swings.Count);
        Assert.AreEqual("Robot", swings[0].team);
    }

    [Test]
    public void DetectTerritorySwing_SmallChange_NotDetected()
    {
        var lines = new[]
        {
            Header(),
            Turn(10, "Robot_0", "Robot", "Move", 7, 0, 0, 10, 10, 3, 3),
            Turn(20, "Robot_0", "Robot", "Move", 7, 1, 0, 14, 10, 3, 3),  // Only +4
            Summary("Robot", 50, 20, 10),
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        Assert.AreEqual(0, gh.highlights.Count(h => h.type == "territory_swing"));
    }

    // ── Coordinated Attack ───────────────────────────────────────────────

    [Test]
    public void DetectCoordinatedAttack_TwoUnitsAttackSameRound()
    {
        var lines = new[]
        {
            Header(),
            Turn(15, "Robot_0", "Robot", "Attack", 7, 0, 0, 10, 10, 3, 3, "Mutant_0", 1, 0),
            Turn(15, "Robot_1", "Robot", "Attack", 7, 2, 0, 10, 10, 3, 3, "Mutant_1", 3, 0),
            Summary("Robot", 50, 20, 10),
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        var attacks = gh.highlights.Where(h => h.type == "coordinated_attack").ToList();
        Assert.AreEqual(1, attacks.Count);
        Assert.AreEqual(15, attacks[0].roundStart);
        Assert.IsTrue(attacks[0].description.Contains("2"));
    }

    [Test]
    public void DetectCoordinatedAttack_DifferentTeams_NotDetected()
    {
        var lines = new[]
        {
            Header(),
            Turn(15, "Robot_0", "Robot", "Attack", 7, 0, 0, 10, 10, 3, 3, "Mutant_0", 1, 0),
            Turn(15, "Mutant_0", "Mutant", "Attack", 7, 1, 0, 10, 10, 3, 3, "Robot_0", 0, 0),
            Summary("Robot", 50, 20, 10),
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        Assert.AreEqual(0, gh.highlights.Count(h => h.type == "coordinated_attack"));
    }

    // ── Flanking ─────────────────────────────────────────────────────────

    [Test]
    public void DetectFlanking_AttackerWith2Allies()
    {
        // Robot_0 attacks at (0,0), Robot_1 at (1,0) = adjacent, Robot_2 at (0,1) = adjacent.
        var lines = new[]
        {
            Header(),
            Turn(10, "Robot_0", "Robot", "Attack", 7, 0, 0, 10, 10, 3, 3, "Mutant_0", -1, 0, true),
            Turn(10, "Robot_1", "Robot", "Move", 7, 1, 0, 10, 10, 3, 3),
            Turn(10, "Robot_2", "Robot", "Move", 7, 0, 1, 10, 10, 3, 3),
            Summary("Robot", 50, 20, 10),
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        var flanks = gh.highlights.Where(h => h.type == "flanking").ToList();
        Assert.AreEqual(1, flanks.Count);
        Assert.IsTrue(flanks[0].description.Contains("2 allies"));
        Assert.IsTrue(flanks[0].description.Contains("killed"));
    }

    [Test]
    public void DetectFlanking_OnlyOneAlly_NotDetected()
    {
        var lines = new[]
        {
            Header(),
            Turn(10, "Robot_0", "Robot", "Attack", 7, 0, 0, 10, 10, 3, 3, "Mutant_0", -1, 0),
            Turn(10, "Robot_1", "Robot", "Move", 7, 1, 0, 10, 10, 3, 3),
            Turn(10, "Robot_2", "Robot", "Move", 7, 5, 5, 10, 10, 3, 3), // Far away
            Summary("Robot", 50, 20, 10),
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        Assert.AreEqual(0, gh.highlights.Count(h => h.type == "flanking"));
    }

    // ── Wipe Event ───────────────────────────────────────────────────────

    [Test]
    public void DetectWipeEvent_AllEnemiesDead()
    {
        var lines = new[]
        {
            Header(),
            Turn(20, "Robot_0", "Robot", "Attack", 7, 0, 0, 10, 10, 3, 0, "Mutant_2", 1, 0, true),
            Summary("Robot", 50, 20, 10),
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        var wipes = gh.highlights.Where(h => h.type == "wipe_event").ToList();
        Assert.AreEqual(1, wipes.Count);
        Assert.AreEqual("Robot", wipes[0].team); // Robot wiped the Mutants
        Assert.IsTrue(wipes[0].description.Contains("Mutant"));
    }

    [Test]
    public void DetectWipeEvent_NoWipe()
    {
        var lines = new[]
        {
            Header(),
            Turn(20, "Robot_0", "Robot", "Attack", 7, 0, 0, 10, 10, 3, 1),
            Summary("Robot", 50, 20, 10),
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        Assert.AreEqual(0, gh.highlights.Count(h => h.type == "wipe_event"));
    }

    // ── Blitz Win ────────────────────────────────────────────────────────

    [Test]
    public void DetectBlitzWin_FastVictory()
    {
        var lines = new[]
        {
            Header(maxRounds: 2000),
            Turn(1, "Robot_0", "Robot", "Move", 7, 0, 0, 10, 5, 3, 3),
            Summary("Robot", 400, 25, 5),  // 400/2000 = 20% < 30%
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        var blitz = gh.highlights.Where(h => h.type == "blitz_win").ToList();
        Assert.AreEqual(1, blitz.Count);
        Assert.IsTrue(blitz[0].description.Contains("400 rounds"));
    }

    [Test]
    public void DetectBlitzWin_SlowGame_NotDetected()
    {
        var lines = new[]
        {
            Header(maxRounds: 2000),
            Summary("Robot", 1500, 25, 5),  // 75% > 30%
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        Assert.AreEqual(0, gh.highlights.Count(h => h.type == "blitz_win"));
    }

    // ── Close Game ───────────────────────────────────────────────────────

    [Test]
    public void DetectCloseGame_TightFinish()
    {
        // gridSize=5 → 61 tiles. diff=2 → 3.3% < 5%
        var lines = new[]
        {
            Header(grid: 5),
            Summary("Robot", 500, 20, 18),
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        var close = gh.highlights.Where(h => h.type == "close_game").ToList();
        Assert.AreEqual(1, close.Count);
        Assert.IsTrue(close[0].description.Contains("2 tiles"));
    }

    [Test]
    public void DetectCloseGame_Blowout_NotDetected()
    {
        var lines = new[]
        {
            Header(grid: 5),
            Summary("Robot", 500, 30, 5),  // 25 tile diff = 41%
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        Assert.AreEqual(0, gh.highlights.Count(h => h.type == "close_game"));
    }

    // ── Stalemate Break ──────────────────────────────────────────────────

    [Test]
    public void DetectStalemateBreak_LongStaleFollowedByShift()
    {
        var lines = new List<string> { Header() };

        // 60 rounds of no change.
        for (int i = 1; i <= 60; i++)
            lines.Add(Turn(i, "Robot_0", "Robot", "Idle", 7, 0, 0, 10, 10, 3, 3));

        // Then territory shifts.
        lines.Add(Turn(61, "Robot_0", "Robot", "Capture", 7, 1, 0, 15, 10, 3, 3));
        lines.Add(Summary("Robot", 100, 20, 10));

        var gh = HighlightDetector.Analyze(lines.ToArray(), "test.jsonl");

        var stale = gh.highlights.Where(h => h.type == "stalemate_break").ToList();
        Assert.AreEqual(1, stale.Count);
        Assert.IsTrue(stale[0].description.Contains("60 rounds"));
    }

    [Test]
    public void DetectStalemateBreak_ShortStale_NotDetected()
    {
        var lines = new List<string> { Header() };

        // Only 20 rounds stale.
        for (int i = 1; i <= 20; i++)
            lines.Add(Turn(i, "Robot_0", "Robot", "Idle", 7, 0, 0, 10, 10, 3, 3));

        lines.Add(Turn(21, "Robot_0", "Robot", "Capture", 7, 1, 0, 15, 10, 3, 3));
        lines.Add(Summary("Robot", 50, 20, 10));

        var gh = HighlightDetector.Analyze(lines.ToArray(), "test.jsonl");

        Assert.AreEqual(0, gh.highlights.Count(h => h.type == "stalemate_break"));
    }

    // ── Kill Streak ──────────────────────────────────────────────────────

    [Test]
    public void DetectKillStreak_ThreeKillsInWindow()
    {
        var lines = new[]
        {
            Header(),
            Turn(10, "Robot_0", "Robot", "Attack", 7, 0, 0, 10, 10, 3, 3, "Mutant_0", 1, 0, true),
            Turn(15, "Robot_0", "Robot", "Attack", 7, 0, 0, 10, 10, 3, 2, "Mutant_1", -1, 0, true),
            Turn(25, "Robot_0", "Robot", "Attack", 7, 0, 0, 10, 10, 3, 1, "Mutant_2", 0, -1, true),
            Summary("Robot", 50, 20, 5),
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        var streaks = gh.highlights.Where(h => h.type == "kill_streak").ToList();
        Assert.AreEqual(1, streaks.Count);
        Assert.IsTrue(streaks[0].description.Contains("Robot_0"));
        Assert.IsTrue(streaks[0].description.Contains("3 kills"));
    }

    [Test]
    public void DetectKillStreak_SpreadOut_NotDetected()
    {
        var lines = new[]
        {
            Header(),
            Turn(10, "Robot_0", "Robot", "Attack", 7, 0, 0, 10, 10, 3, 3, "Mutant_0", 1, 0, true),
            Turn(50, "Robot_0", "Robot", "Attack", 7, 0, 0, 10, 10, 3, 2, "Mutant_1", -1, 0, true),
            Turn(90, "Robot_0", "Robot", "Attack", 7, 0, 0, 10, 10, 3, 1, "Mutant_2", 0, -1, true),
            Summary("Robot", 100, 20, 5),
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        Assert.AreEqual(0, gh.highlights.Count(h => h.type == "kill_streak"));
    }

    // ── Interestingness scoring ──────────────────────────────────────────

    [Test]
    public void Interestingness_CappedAt5()
    {
        // Game with comeback (3) + wipe (2) + blitz (1) = 6 → capped at 5.
        var lines = new[]
        {
            Header(maxRounds: 2000),
            Turn(10, "Robot_0", "Robot", "Move", 7, 0, 0, 2, 20, 3, 3),
            Turn(50, "Robot_0", "Robot", "Attack", 7, 0, 0, 15, 5, 3, 0, "Mutant_2", 1, 0, true),
            Summary("Robot", 100, 25, 5),
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        Assert.LessOrEqual(gh.interestingness, 5);
    }

    [Test]
    public void Interestingness_ZeroForBoringGame()
    {
        var lines = new[]
        {
            Header(),
            Turn(1, "Robot_0", "Robot", "Move", 7, 0, 0, 10, 10, 3, 3),
            Summary("Robot", 1000, 20, 15),
        };

        var gh = HighlightDetector.Analyze(lines, "test.jsonl");

        Assert.AreEqual(0, gh.interestingness);
    }

    // ── Edge cases ───────────────────────────────────────────────────────

    [Test]
    public void EmptyFile_ReturnsEmptyHighlights()
    {
        var gh = HighlightDetector.Analyze(new string[0], "empty.jsonl");

        Assert.AreEqual(0, gh.highlights.Count);
        Assert.AreEqual(0, gh.interestingness);
    }

    [Test]
    public void HeaderOnly_ReturnsEmptyHighlights()
    {
        var lines = new[] { Header() };

        var gh = HighlightDetector.Analyze(lines, "header_only.jsonl");

        Assert.AreEqual(0, gh.highlights.Count);
    }

    // ── HexDistance ──────────────────────────────────────────────────────

    [Test]
    public void HexDistance_Adjacent_Returns1()
    {
        Assert.AreEqual(1, HighlightDetector.HexDistance(0, 0, 1, 0));
        Assert.AreEqual(1, HighlightDetector.HexDistance(0, 0, 0, 1));
        Assert.AreEqual(1, HighlightDetector.HexDistance(0, 0, 1, -1));
    }

    [Test]
    public void HexDistance_Same_Returns0()
    {
        Assert.AreEqual(0, HighlightDetector.HexDistance(3, -2, 3, -2));
    }

    [Test]
    public void HexDistance_FarApart()
    {
        Assert.AreEqual(4, HighlightDetector.HexDistance(0, 0, 4, 0));
        Assert.AreEqual(3, HighlightDetector.HexDistance(0, 0, 2, 1));
    }
}
