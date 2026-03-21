using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Comprehensive PlayMode tests for movement mechanics.
/// Rules:
///   - Only walls and occupied hexes block movement.
///   - Moving onto neutral or enemy hex captures it (free).
///   - Robot entering enemy slime: mine (costs energy, slime destroyed, hex → robot's).
///   - Own territory and base hexes: free movement, no capture.
/// </summary>
public class MovementMechanicsTests
{
    private GameObject gridGo;
    private HexGrid grid;
    private readonly List<GameObject> spawnedObjects = new();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name != "Code-based tests runner")
                Object.Destroy(go);
        yield return null;

        LogAssert.ignoreFailingMessages = true;
        Time.timeScale = 1f;

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
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var go in spawnedObjects)
            if (go != null) Object.Destroy(go);
        spawnedObjects.Clear();
        if (gridGo != null) Object.Destroy(gridGo);
        yield return null;
    }

    private (UnitData data, HexMovement move) SpawnUnit(Team team, HexCoord hex)
    {
        var go = new GameObject($"{team}_{hex}");
        var data = go.AddComponent<UnitData>();
        data.team = team;
        data.isAlive = true;
        data.currentHex = hex;
        data.Energy = 15;

        var move = go.AddComponent<HexMovement>();
        move.Initialize(grid);
        move.PlaceAt(hex);

        spawnedObjects.Add(go);
        return (data, move);
    }

    // ── Neutral hex capture ─────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Move_NeutralHex_CapturesIt()
    {
        yield return null;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        bool moved = move.TryMove(0); // East → (1,0)

        Assert.IsTrue(moved);
        Assert.AreEqual(new HexCoord(1, 0), unit.currentHex);
        Assert.AreEqual(Team.Robot, grid.GetTile(new HexCoord(1, 0)).Owner,
            "Neutral hex should be captured on entry.");
        Assert.AreEqual(UnitAction.Capture, unit.lastAction,
            "Action should be Capture when taking neutral hex.");
    }

    [UnityTest]
    public IEnumerator Move_NeutralHex_NoEnergyCost()
    {
        yield return null;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        unit.Energy = 15;
        move.TryMove(0);

        Assert.AreEqual(15, unit.Energy,
            "Capturing neutral hex by moving should cost no energy.");
    }

    [UnityTest]
    public IEnumerator Move_NeutralHex_Mutant_CapturesIt()
    {
        yield return null;

        var (unit, move) = SpawnUnit(Team.Mutant, new HexCoord(0, 0));
        bool moved = move.TryMove(0);

        Assert.IsTrue(moved);
        Assert.AreEqual(Team.Mutant, grid.GetTile(new HexCoord(1, 0)).Owner,
            "Mutant should also capture neutral hex on entry.");
    }

    // ── Enemy hex capture ───────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Move_EnemyEmptyHex_CapturesIt()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        bool moved = move.TryMove(0);

        Assert.IsTrue(moved, "Movement onto enemy empty hex should succeed.");
        Assert.AreEqual(new HexCoord(1, 0), unit.currentHex);
        Assert.AreEqual(Team.Robot, tile.Owner,
            "Enemy hex should flip to robot's team on entry.");
    }

    [UnityTest]
    public IEnumerator Move_EnemyEmptyHex_NoEnergyCost()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        unit.Energy = 15;
        move.TryMove(0);

        Assert.AreEqual(15, unit.Energy,
            "Capturing enemy empty hex should cost no energy.");
    }

    [UnityTest]
    public IEnumerator Move_EnemyEmptyHex_ActionIsCapture()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        move.TryMove(0);

        Assert.AreEqual(UnitAction.Capture, unit.lastAction);
        Assert.AreEqual(new HexCoord(1, 0), unit.lastCapturedHex);
    }

    [UnityTest]
    public IEnumerator Move_EnemyEmptyHex_Mutant_CapturesRobotHex()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;

        var (unit, move) = SpawnUnit(Team.Mutant, new HexCoord(0, 0));
        bool moved = move.TryMove(0);

        Assert.IsTrue(moved);
        Assert.AreEqual(Team.Mutant, tile.Owner,
            "Mutant should capture robot's hex on entry.");
    }

    // ── Own territory ───────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Move_OwnHex_NoCapture()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        bool moved = move.TryMove(0);

        Assert.IsTrue(moved, "Should move freely on own territory.");
        Assert.AreEqual(UnitAction.Move, unit.lastAction,
            "Moving on own hex should be Move, not Capture.");
        Assert.AreEqual(Team.Robot, tile.Owner,
            "Own hex should remain under same ownership.");
    }

    // ── Wall blocking ───────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Move_BlockedByOwnWall()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        bool moved = move.TryMove(0);

        Assert.IsFalse(moved, "Own wall should block movement.");
        Assert.AreEqual(new HexCoord(0, 0), unit.currentHex);
    }

    [UnityTest]
    public IEnumerator Move_BlockedByEnemyWall()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        bool moved = move.TryMove(0);

        Assert.IsFalse(moved, "Enemy wall should block movement.");
        Assert.AreEqual(new HexCoord(0, 0), unit.currentHex);
    }

    // ── Occupied hex blocking ───────────────────────────────────────────

    [UnityTest]
    public IEnumerator Move_BlockedByAllyUnit()
    {
        yield return null;

        var (_, _) = SpawnUnit(Team.Robot, new HexCoord(1, 0));
        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        bool moved = move.TryMove(0);

        Assert.IsFalse(moved, "Hex occupied by ally should block movement.");
    }

    [UnityTest]
    public IEnumerator Move_BlockedByEnemyUnit()
    {
        yield return null;

        var (_, _) = SpawnUnit(Team.Mutant, new HexCoord(1, 0));
        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        bool moved = move.TryMove(0);

        Assert.IsFalse(moved, "Hex occupied by enemy should block movement.");
    }

    // ── Robot vs enemy slime (mine) ─────────────────────────────────────

    [UnityTest]
    public IEnumerator Move_RobotOnEnemySlime_CostsEnergy()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Slime;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        unit.Energy = 15;

        bool moved = move.TryMove(0);

        Assert.IsTrue(moved, "Robot should be able to enter enemy slime.");
        Assert.AreEqual(12, unit.Energy,
            "Entering enemy slime should cost 3 energy (slimeEntryCostRobot).");
    }

    [UnityTest]
    public IEnumerator Move_RobotOnEnemySlime_DestroysSlime()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Slime;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        unit.Energy = 15;

        move.TryMove(0);

        Assert.AreEqual(TileType.Empty, tile.TileType,
            "Slime should be destroyed when robot enters.");
    }

    [UnityTest]
    public IEnumerator Move_RobotOnEnemySlime_HexBecomesRobots()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Slime;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        unit.Energy = 15;

        move.TryMove(0);

        Assert.AreEqual(Team.Robot, tile.Owner,
            "Hex should become robot's after destroying slime.");
    }

    [UnityTest]
    public IEnumerator Move_RobotOnEnemySlime_NotEnoughEnergy_Blocked()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Slime;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        unit.Energy = 2; // Not enough (costs 3)

        bool moved = move.TryMove(0);

        Assert.IsFalse(moved,
            "Robot without enough energy should not enter enemy slime.");
        Assert.AreEqual(TileType.Slime, tile.TileType,
            "Slime should remain intact.");
        Assert.AreEqual(Team.Mutant, tile.Owner,
            "Ownership should not change.");
    }

    [UnityTest]
    public IEnumerator Move_RobotOnEnemySlime_ActionIsCapture()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Slime;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        unit.Energy = 15;

        move.TryMove(0);

        Assert.AreEqual(UnitAction.Capture, unit.lastAction,
            "Entering enemy slime should count as Capture.");
    }

    // ── Mutant on own slime ─────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Move_MutantOnOwnSlime_Free()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Slime;

        var (unit, move) = SpawnUnit(Team.Mutant, new HexCoord(0, 0));
        unit.Energy = 15;

        bool moved = move.TryMove(0);

        Assert.IsTrue(moved, "Mutant should move freely on own slime.");
        Assert.AreEqual(15, unit.Energy, "Moving on own slime costs no energy.");
        Assert.AreEqual(TileType.Slime, tile.TileType,
            "Own slime should remain intact.");
    }

    // ── Base hex ────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Move_OntoBaseHex_NoCapture()
    {
        yield return null;

        // Find a base hex.
        HexTileData baseTile = null;
        HexCoord baseCoord = default;
        HexCoord adjacentCoord = default;
        foreach (var kvp in grid.Tiles)
        {
            if (kvp.Value.isBase)
            {
                baseTile = kvp.Value;
                baseCoord = kvp.Key;
                // Find an adjacent non-base coord.
                for (int d = 0; d < 6; d++)
                {
                    var n = baseCoord.Neighbor(d);
                    if (grid.IsValidCoord(n))
                    {
                        adjacentCoord = n;
                        break;
                    }
                }
                break;
            }
        }

        if (baseTile == null)
        {
            Assert.Inconclusive("No base hex found in test grid.");
            yield break;
        }

        Team baseTeam = baseTile.Owner;
        var (unit, move) = SpawnUnit(baseTeam, adjacentCoord);

        int dir = -1;
        for (int d = 0; d < 6; d++)
        {
            if (adjacentCoord.Neighbor(d) == baseCoord) { dir = d; break; }
        }
        Assert.IsTrue(dir >= 0, "Should find direction to base hex.");

        bool moved = move.TryMove(dir);

        Assert.IsTrue(moved, "Should be able to move onto own base hex.");
        Assert.AreEqual(UnitAction.Move, unit.lastAction,
            "Moving onto base hex should not be Capture — it's already owned.");
    }

    // ── Dead unit ───────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Move_DeadUnit_Fails()
    {
        yield return null;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        unit.Die(6);

        bool moved = move.TryMove(0);
        Assert.IsFalse(moved, "Dead unit cannot move.");
    }

    // ── Invalid direction ───────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Move_InvalidDirection_Fails()
    {
        yield return null;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        Assert.IsFalse(move.TryMove(-1), "Direction -1 should fail.");
        Assert.IsFalse(move.TryMove(6),  "Direction 6 should fail.");
        Assert.IsFalse(move.TryMove(99), "Direction 99 should fail.");
    }

    // ── IsValidMove consistency ─────────────────────────────────────────

    [UnityTest]
    public IEnumerator IsValidMove_MatchesTryMove_NeutralHex()
    {
        yield return null;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        Assert.IsTrue(move.IsValidMove(0),
            "IsValidMove should return true for neutral adjacent hex.");
    }

    [UnityTest]
    public IEnumerator IsValidMove_MatchesTryMove_EnemyHex()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        Assert.IsTrue(move.IsValidMove(0),
            "IsValidMove should return true for enemy empty hex.");
    }

    [UnityTest]
    public IEnumerator IsValidMove_False_ForWall()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        Assert.IsFalse(move.IsValidMove(0),
            "IsValidMove should return false for wall.");
    }

    [UnityTest]
    public IEnumerator IsValidMove_False_ForOccupiedHex()
    {
        yield return null;

        var (_, _) = SpawnUnit(Team.Mutant, new HexCoord(1, 0));
        var (_, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        Assert.IsFalse(move.IsValidMove(0),
            "IsValidMove should return false for occupied hex.");
    }

    [UnityTest]
    public IEnumerator IsValidMove_EnemySlime_TrueIfEnoughEnergy()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Slime;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        unit.Energy = 15;
        Assert.IsTrue(move.IsValidMove(0),
            "IsValidMove should be true for enemy slime with enough energy.");
    }

    [UnityTest]
    public IEnumerator IsValidMove_EnemySlime_FalseIfNotEnoughEnergy()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Slime;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        unit.Energy = 2;
        Assert.IsFalse(move.IsValidMove(0),
            "IsValidMove should be false for enemy slime without enough energy.");
    }
}
