using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode tests for ReplayPlayer: navigation, reset, build/capture visualization.
/// Creates a minimal grid + units, builds a synthetic ReplayFile, and tests
/// JumpToTurn, JumpToRound, StepBack, ResetToStart behavior.
/// </summary>
public class ReplayPlayerTests
{
    private GameObject gridGo;
    private HexGrid grid;
    private UnitFactory factory;
    private ReplayPlayer player;
    private readonly List<GameObject> objects = new();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name != "Code-based tests runner")
                Object.Destroy(go);
        yield return null;

        if (!LogAssert.ignoreFailingMessages) LogAssert.ignoreFailingMessages = true;
        Time.timeScale = 1f;

        // Create grid.
        var prefab = new GameObject("HexPrefab");
        prefab.AddComponent<MeshFilter>();
        prefab.AddComponent<MeshRenderer>();
        prefab.AddComponent<HexMeshGenerator>();
        prefab.AddComponent<HexTileData>();
        prefab.SetActive(false);

        gridGo = new GameObject("TestGrid");
        grid = gridGo.AddComponent<HexGrid>();
        grid.hexPrefab = prefab;
        grid.boardSide = 3;
        yield return null;
        Object.Destroy(prefab);

        // Create unit factory with skipMLAgents to avoid URP/agent issues.
        var factoryGo = new GameObject("UnitFactory");
        factory = factoryGo.AddComponent<UnitFactory>();
        factory.grid = grid;
        factory.unitsPerTeam = 2;
        factory.skipMLAgents = true;
        factory.SpawnAllUnits();
        objects.Add(factoryGo);

        // Create replay player.
        var playerGo = new GameObject("ReplayPlayer");
        player = playerGo.AddComponent<ReplayPlayer>();
        objects.Add(playerGo);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var go in objects)
            if (go != null) Object.Destroy(go);
        objects.Clear();
        if (gridGo != null) Object.Destroy(gridGo);
        yield return null;
    }

    /// <summary>Build a synthetic replay with known turns.</summary>
    private ReplayData.ReplayFile BuildTestReplay()
    {
        var replay = new ReplayData.ReplayFile();
        replay.header = new ReplayData.Header
        {
            match = 1,
            unitsPerTeam = 2,
            gridSize = 3,
            maxRounds = 10
        };

        // Round 1: Robot_0 moves and captures (0,0)→(1,0).
        replay.turns.Add(new ReplayData.Turn
        {
            round = 1, unitName = "Robot_0", team = "Robot", action = "Capture",
            energy = 15, q = 1, r = 0,
            hasCaptured = true, capturedQ = 1, capturedR = 0,
            rTiles = 5, mTiles = 4, rAlive = 2, mAlive = 2
        });

        // Round 1: Mutant_0 moves and captures (1,-2)→(0,-1).
        replay.turns.Add(new ReplayData.Turn
        {
            round = 1, unitName = "Mutant_0", team = "Mutant", action = "Capture",
            energy = 15, q = 0, r = -1,
            hasCaptured = true, capturedQ = 0, capturedR = -1,
            rTiles = 5, mTiles = 5, rAlive = 2, mAlive = 2
        });

        // Round 1: Robot_1 stays idle.
        replay.turns.Add(new ReplayData.Turn
        {
            round = 1, unitName = "Robot_1", team = "Robot", action = "Idle",
            energy = 15, q = -2, r = 2,
            rTiles = 5, mTiles = 5, rAlive = 2, mAlive = 2
        });

        // Round 1: Mutant_1 stays idle.
        replay.turns.Add(new ReplayData.Turn
        {
            round = 1, unitName = "Mutant_1", team = "Mutant", action = "Idle",
            energy = 15, q = 2, r = -2,
            rTiles = 5, mTiles = 5, rAlive = 2, mAlive = 2
        });

        // Round 2: Robot_0 builds wall at (1,0)→built at (0,0).
        replay.turns.Add(new ReplayData.Turn
        {
            round = 2, unitName = "Robot_0", team = "Robot", action = "BuildWall",
            energy = 12, q = 1, r = 0,
            hasBuilt = true, builtQ = 0, builtR = 0,
            rTiles = 5, mTiles = 5, rAlive = 2, mAlive = 2
        });

        // Round 2: Mutant_0 places slime at (0,-1)→built at (0,-1).
        replay.turns.Add(new ReplayData.Turn
        {
            round = 2, unitName = "Mutant_0", team = "Mutant", action = "PlaceSlime",
            energy = 13, q = 0, r = -1,
            hasBuilt = true, builtQ = 0, builtR = -1,
            rTiles = 5, mTiles = 5, rAlive = 2, mAlive = 2
        });

        // Round 2: Robot_1 idle, Mutant_1 idle.
        replay.turns.Add(new ReplayData.Turn
        {
            round = 2, unitName = "Robot_1", team = "Robot", action = "Idle",
            energy = 15, q = -2, r = 2,
            rTiles = 5, mTiles = 5, rAlive = 2, mAlive = 2
        });
        replay.turns.Add(new ReplayData.Turn
        {
            round = 2, unitName = "Mutant_1", team = "Mutant", action = "Idle",
            energy = 15, q = 2, r = -2,
            rTiles = 5, mTiles = 5, rAlive = 2, mAlive = 2
        });

        replay.maxRound = 2;
        replay.summary = new ReplayData.Summary
        {
            winner = "Robot", rounds = 2, rTiles = 5, mTiles = 5
        };

        return replay;
    }

    // ── Reset to start ─────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ResetToStart_UnitsOnBaseHexes()
    {
        yield return null;

        var replay = BuildTestReplay();
        player.TestInitialize(replay, grid, factory);

        // Advance to round 2 so units move.
        player.JumpToRound(2);
        Assert.AreEqual(2, player.currentRound);

        // Now jump back to start.
        player.JumpToStart();

        // All units should be on base hexes.
        var robotBases = grid.GetBaseTiles(Team.Robot);
        var mutantBases = grid.GetBaseTiles(Team.Mutant);

        for (int i = 0; i < factory.robotUnits.Count; i++)
        {
            var expected = robotBases[i % robotBases.Count].coord;
            Assert.AreEqual(expected, factory.robotUnits[i].currentHex,
                $"Robot_{i} should be on base hex after reset.");
        }
        for (int i = 0; i < factory.mutantUnits.Count; i++)
        {
            var expected = mutantBases[i % mutantBases.Count].coord;
            Assert.AreEqual(expected, factory.mutantUnits[i].currentHex,
                $"Mutant_{i} should be on base hex after reset.");
        }
    }

    [UnityTest]
    public IEnumerator ResetToStart_UnitsAliveFullEnergy()
    {
        yield return null;

        var replay = BuildTestReplay();
        player.TestInitialize(replay, grid, factory);

        player.JumpToRound(2);
        player.JumpToStart();

        foreach (var unit in factory.AllUnits)
        {
            Assert.IsTrue(unit.isAlive, $"{unit.gameObject.name} should be alive after reset.");
            Assert.AreEqual(unit.maxEnergy, unit.Energy,
                $"{unit.gameObject.name} should have full energy after reset.");
        }
    }

    [UnityTest]
    public IEnumerator ResetToStart_TilesResetToNeutral()
    {
        yield return null;

        var replay = BuildTestReplay();
        player.TestInitialize(replay, grid, factory);

        player.JumpToRound(2);
        player.JumpToStart();

        foreach (var kvp in grid.Tiles)
        {
            var tile = kvp.Value;
            if (!tile.isBase)
            {
                Assert.AreEqual(Team.None, tile.Owner,
                    $"Non-base tile {kvp.Key} should be neutral after reset.");
                Assert.AreEqual(TileType.Empty, tile.TileType,
                    $"Non-base tile {kvp.Key} should be empty after reset.");
            }
        }
    }

    [UnityTest]
    public IEnumerator ResetToStart_StateIsPaused()
    {
        yield return null;

        var replay = BuildTestReplay();
        player.TestInitialize(replay, grid, factory);

        player.JumpToRound(2);
        player.JumpToStart();

        Assert.AreEqual(ReplayPlayer.PlaybackState.Paused, player.state);
        Assert.AreEqual(0, player.currentTurnIndex);
        Assert.AreEqual(0, player.currentRound);
    }

    // ── Step forward / backward ────────────────────────────────────────

    [UnityTest]
    public IEnumerator StepOneTurn_AdvancesOneStep()
    {
        yield return null;

        var replay = BuildTestReplay();
        player.TestInitialize(replay, grid, factory);

        player.StepOneTurn();

        Assert.AreEqual(1, player.currentTurnIndex);
        Assert.AreEqual(1, player.currentRound);
    }

    [UnityTest]
    public IEnumerator StepBackOneTurn_GoesBackOneStep()
    {
        yield return null;

        var replay = BuildTestReplay();
        player.TestInitialize(replay, grid, factory);

        player.JumpToTurn(3);
        Assert.AreEqual(3, player.currentTurnIndex);

        player.StepBackOneTurn();
        Assert.AreEqual(2, player.currentTurnIndex);
    }

    [UnityTest]
    public IEnumerator StepBackOneTurn_AtStart_DoesNothing()
    {
        yield return null;

        var replay = BuildTestReplay();
        player.TestInitialize(replay, grid, factory);

        player.StepBackOneTurn();
        Assert.AreEqual(0, player.currentTurnIndex);
    }

    // ── Jump to round ──────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator JumpToRound_AppliesAllTurnsUpToRound()
    {
        yield return null;

        var replay = BuildTestReplay();
        player.TestInitialize(replay, grid, factory);

        player.JumpToRound(1);

        // After round 1: Robot_0 should have captured (1,0).
        var tile = grid.GetTile(new HexCoord(1, 0));
        if (tile != null)
            Assert.AreEqual(Team.Robot, tile.Owner,
                "After round 1, tile (1,0) should be captured by Robot.");
    }

    [UnityTest]
    public IEnumerator JumpToRound_ThenBackToZero_ResetsCorrectly()
    {
        yield return null;

        var replay = BuildTestReplay();
        player.TestInitialize(replay, grid, factory);

        player.JumpToRound(2);
        player.JumpToRound(0);

        // All non-base tiles neutral.
        foreach (var kvp in grid.Tiles)
        {
            if (!kvp.Value.isBase)
                Assert.AreEqual(Team.None, kvp.Value.Owner,
                    $"Tile {kvp.Key} should be neutral after jumping to round 0.");
        }
    }

    // ── Build actions in replay ────────────────────────────────────────

    [UnityTest]
    public IEnumerator BuildWall_AppearsOnCorrectHex()
    {
        yield return null;

        var replay = BuildTestReplay();
        player.TestInitialize(replay, grid, factory);

        // Round 2 has Robot_0 BuildWall with built=[0,0].
        player.JumpToRound(2);

        var wallTile = grid.GetTile(new HexCoord(0, 0));
        if (wallTile != null && !wallTile.isBase)
        {
            Assert.AreEqual(TileType.Wall, wallTile.TileType,
                "Wall should appear at built target (0,0), not unit position (1,0).");
            Assert.AreEqual(Team.Robot, wallTile.Owner);
        }
    }

    [UnityTest]
    public IEnumerator PlaceSlime_AppearsOnCorrectHex()
    {
        yield return null;

        var replay = BuildTestReplay();
        player.TestInitialize(replay, grid, factory);

        player.JumpToRound(2);

        var slimeTile = grid.GetTile(new HexCoord(0, -1));
        if (slimeTile != null && !slimeTile.isBase)
        {
            Assert.AreEqual(TileType.Slime, slimeTile.TileType,
                "Slime should appear at built target (0,-1).");
            Assert.AreEqual(Team.Mutant, slimeTile.Owner);
        }
    }

    [UnityTest]
    public IEnumerator BuildWall_ClearedAfterReset()
    {
        yield return null;

        var replay = BuildTestReplay();
        player.TestInitialize(replay, grid, factory);

        player.JumpToRound(2);
        player.JumpToStart();

        var wallTile = grid.GetTile(new HexCoord(0, 0));
        if (wallTile != null && !wallTile.isBase)
        {
            Assert.AreEqual(TileType.Empty, wallTile.TileType,
                "Wall should be cleared after reset.");
        }
    }

    // ── Capture in replay ──────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Capture_AppearsOnCorrectHex()
    {
        yield return null;

        var replay = BuildTestReplay();
        player.TestInitialize(replay, grid, factory);

        player.JumpToRound(1);

        var capTile = grid.GetTile(new HexCoord(1, 0));
        if (capTile != null && !capTile.isBase)
        {
            Assert.AreEqual(Team.Robot, capTile.Owner,
                "Captured hex (1,0) should belong to Robot after round 1.");
        }
    }

    [UnityTest]
    public IEnumerator Capture_ClearedAfterReset()
    {
        yield return null;

        var replay = BuildTestReplay();
        player.TestInitialize(replay, grid, factory);

        player.JumpToRound(1);
        player.JumpToStart();

        var capTile = grid.GetTile(new HexCoord(1, 0));
        if (capTile != null && !capTile.isBase)
        {
            Assert.AreEqual(Team.None, capTile.Owner,
                "Captured hex should be neutral after reset.");
        }
    }

    // ── JumpToEnd ──────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator JumpToEnd_StateIsFinished()
    {
        yield return null;

        var replay = BuildTestReplay();
        player.TestInitialize(replay, grid, factory);

        player.JumpToEnd();

        Assert.AreEqual(ReplayPlayer.PlaybackState.Finished, player.state);
        Assert.AreEqual(replay.turns.Count, player.currentTurnIndex);
    }

    // ── Scrub back and forth ───────────────────────────────────────────

    [UnityTest]
    public IEnumerator ScrubForwardThenBack_StateConsistent()
    {
        yield return null;

        var replay = BuildTestReplay();
        player.TestInitialize(replay, grid, factory);

        // Forward to round 2.
        player.JumpToRound(2);
        var r2TurnIndex = player.currentTurnIndex;

        // Back to round 1.
        player.JumpToRound(1);
        Assert.Less(player.currentTurnIndex, r2TurnIndex,
            "Going back should decrease turn index.");

        // Forward again to round 2 — should produce same state.
        player.JumpToRound(2);
        Assert.AreEqual(r2TurnIndex, player.currentTurnIndex,
            "Going forward again to same round should produce same turn index.");
    }
}
