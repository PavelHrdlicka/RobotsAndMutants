using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Comprehensive PlayMode tests for build mechanics.
/// Rules:
///   - Robot builds wall on adjacent friendly empty hex (not base). Costs wallBuildCost (4).
///   - Mutant places slime under itself on own empty hex (not base). Costs slimePlaceCost (2).
///   - Cannot build on base, enemy hex, occupied hex, or hex with existing structure.
///   - DestroyWall: destroys own adjacent wall, costs destroyOwnWallCost (1).
/// </summary>
public class BuildMechanicsTests
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

    // ── Robot wall building ─────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Build_Robot_Wall_OnAdjacentOwnHex()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;

        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        bool built = move.TryBuild(0); // direction 0 = East → (1,0)

        Assert.IsTrue(built);
        Assert.AreEqual(TileType.Wall, tile.TileType);
        Assert.AreEqual(3, tile.WallHP, "Wall should start with 3 HP.");
        Assert.AreEqual(11, robot.Energy, "Wall build costs 4 energy.");
        Assert.AreEqual(UnitAction.BuildWall, robot.lastAction);
    }

    [UnityTest]
    public IEnumerator Build_Robot_Wall_FailsOnEnemyHex()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;

        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        bool built = move.TryBuild(0);

        Assert.IsFalse(built, "Cannot build wall on enemy hex.");
    }

    [UnityTest]
    public IEnumerator Build_Robot_Wall_FailsOnNeutralHex()
    {
        yield return null;

        // Hex at (1,0) is neutral by default.
        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        bool built = move.TryBuild(0);

        Assert.IsFalse(built, "Cannot build wall on neutral hex.");
    }

    [UnityTest]
    public IEnumerator Build_Robot_Wall_FailsOnExistingWall()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        bool built = move.TryBuild(0);

        Assert.IsFalse(built, "Cannot build wall on hex that already has a wall.");
    }

    [UnityTest]
    public IEnumerator Build_Robot_Wall_FailsOnExistingSlime()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;
        tile.TileType = TileType.Slime;

        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        bool built = move.TryBuild(0);

        Assert.IsFalse(built, "Cannot build wall on hex with slime.");
    }

    [UnityTest]
    public IEnumerator Build_Robot_Wall_FailsOnOccupiedHex()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;

        var (_, _) = SpawnUnit(Team.Robot, new HexCoord(1, 0)); // occupant
        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        bool built = move.TryBuild(0);

        Assert.IsFalse(built, "Cannot build wall on occupied hex.");
    }

    [UnityTest]
    public IEnumerator Build_Robot_Wall_FailsOnBase()
    {
        yield return null;

        // Find a base hex and an adjacent non-base hex.
        HexCoord baseCoord = default;
        HexCoord adjacentCoord = default;
        bool found = false;
        foreach (var kvp in grid.Tiles)
        {
            if (kvp.Value.isBase)
            {
                baseCoord = kvp.Key;
                for (int d = 0; d < 6; d++)
                {
                    var n = baseCoord.Neighbor(d);
                    var nTile = grid.GetTile(n);
                    if (nTile != null && !nTile.isBase)
                    {
                        adjacentCoord = n;
                        found = true;
                        break;
                    }
                }
                if (found) break;
            }
        }

        if (!found)
        {
            Assert.Inconclusive("No base hex with non-base neighbor found.");
            yield break;
        }

        var (robot, move) = SpawnUnit(grid.GetTile(baseCoord).Owner, adjacentCoord);

        int dir = -1;
        for (int d = 0; d < 6; d++)
        {
            if (adjacentCoord.Neighbor(d) == baseCoord) { dir = d; break; }
        }
        Assert.IsTrue(dir >= 0);

        bool built = move.TryBuild(dir);

        Assert.IsFalse(built, "Cannot build wall on base hex.");
    }

    [UnityTest]
    public IEnumerator Build_Robot_Wall_FailsNotEnoughEnergy()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;

        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        robot.Energy = 3; // Not enough (costs 4)

        bool built = move.TryBuild(0);

        Assert.IsFalse(built, "Cannot build wall without enough energy.");
        Assert.AreEqual(TileType.Empty, tile.TileType);
    }

    // ── Mutant slime placement ──────────────────────────────────────────

    [UnityTest]
    public IEnumerator Build_Mutant_Slime_UnderSelf()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(0, 0));
        tile.Owner = Team.Mutant;

        var (mutant, move) = SpawnUnit(Team.Mutant, new HexCoord(0, 0));

        bool built = move.TryBuild(0); // direction ignored for mutant

        Assert.IsTrue(built);
        Assert.AreEqual(TileType.Slime, tile.TileType,
            "Slime should be placed on mutant's current hex.");
        Assert.AreEqual(13, mutant.Energy, "Slime placement costs 2 energy.");
        Assert.AreEqual(UnitAction.PlaceSlime, mutant.lastAction);
    }

    [UnityTest]
    public IEnumerator Build_Mutant_Slime_FailsOnEnemyHex()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(0, 0));
        tile.Owner = Team.Robot;

        var (mutant, move) = SpawnUnit(Team.Mutant, new HexCoord(0, 0));

        bool built = move.TryBuild(0);

        Assert.IsFalse(built, "Cannot place slime on enemy hex.");
    }

    [UnityTest]
    public IEnumerator Build_Mutant_Slime_FailsOnNeutralHex()
    {
        yield return null;

        // Hex at (0,0) is neutral by default.
        var (mutant, move) = SpawnUnit(Team.Mutant, new HexCoord(0, 0));

        bool built = move.TryBuild(0);

        Assert.IsFalse(built, "Cannot place slime on neutral hex.");
    }

    [UnityTest]
    public IEnumerator Build_Mutant_Slime_FailsOnExistingSlime()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(0, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Slime;

        var (mutant, move) = SpawnUnit(Team.Mutant, new HexCoord(0, 0));

        bool built = move.TryBuild(0);

        Assert.IsFalse(built, "Cannot place slime on hex already with slime.");
    }

    [UnityTest]
    public IEnumerator Build_Mutant_Slime_FailsOnBase()
    {
        yield return null;

        // Find a mutant base hex.
        HexCoord baseCoord = default;
        bool found = false;
        foreach (var kvp in grid.Tiles)
        {
            if (kvp.Value.isBase && kvp.Value.Owner == Team.Mutant)
            {
                baseCoord = kvp.Key;
                found = true;
                break;
            }
        }

        if (!found)
        {
            Assert.Inconclusive("No mutant base hex found.");
            yield break;
        }

        var (mutant, move) = SpawnUnit(Team.Mutant, baseCoord);

        bool built = move.TryBuild(0);

        Assert.IsFalse(built, "Cannot place slime on base hex.");
    }

    [UnityTest]
    public IEnumerator Build_Mutant_Slime_FailsNotEnoughEnergy()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(0, 0));
        tile.Owner = Team.Mutant;

        var (mutant, move) = SpawnUnit(Team.Mutant, new HexCoord(0, 0));
        mutant.Energy = 1; // Not enough (costs 2)

        bool built = move.TryBuild(0);

        Assert.IsFalse(built, "Cannot place slime without enough energy.");
    }

    // ── Destroy own wall ────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator DestroyWall_Own_Succeeds()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        bool destroyed = move.TryDestroyWall(0);

        Assert.IsTrue(destroyed);
        Assert.AreEqual(TileType.Empty, tile.TileType, "Wall should be destroyed.");
        Assert.AreEqual(0, tile.WallHP);
        Assert.AreEqual(14, robot.Energy, "Destroy own wall costs 1 energy.");
        Assert.AreEqual(Team.Robot, tile.Owner, "Ownership should remain.");
    }

    [UnityTest]
    public IEnumerator DestroyWall_Enemy_Fails()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        bool destroyed = move.TryDestroyWall(0);

        Assert.IsFalse(destroyed, "Cannot destroy enemy wall with TryDestroyWall.");
        Assert.AreEqual(TileType.Wall, tile.TileType);
    }

    [UnityTest]
    public IEnumerator DestroyWall_NoWall_Fails()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;
        // No wall — empty tile.

        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        bool destroyed = move.TryDestroyWall(0);

        Assert.IsFalse(destroyed, "Cannot destroy wall where there is none.");
    }

    [UnityTest]
    public IEnumerator DestroyWall_NotEnoughEnergy_Fails()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        robot.Energy = 0;

        bool destroyed = move.TryDestroyWall(0);

        Assert.IsFalse(destroyed, "Cannot destroy wall with 0 energy.");
    }

    // ── IsValidBuild consistency ────────────────────────────────────────

    [UnityTest]
    public IEnumerator IsValidBuild_Robot_True_ForOwnEmptyHex()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;

        var (_, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        Assert.IsTrue(move.IsValidBuild(0));
    }

    [UnityTest]
    public IEnumerator IsValidBuild_Robot_False_ForEnemyHex()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;

        var (_, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        Assert.IsFalse(move.IsValidBuild(0));
    }

    [UnityTest]
    public IEnumerator IsValidBuild_Mutant_True_ForOwnEmptyHex()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(0, 0));
        tile.Owner = Team.Mutant;

        var (_, move) = SpawnUnit(Team.Mutant, new HexCoord(0, 0));

        Assert.IsTrue(move.IsValidBuild(0));
    }

    [UnityTest]
    public IEnumerator IsValidBuild_Mutant_False_ForNeutralHex()
    {
        yield return null;

        var (_, move) = SpawnUnit(Team.Mutant, new HexCoord(0, 0));

        Assert.IsFalse(move.IsValidBuild(0));
    }

    // ── Dead unit cannot build ──────────────────────────────────────────

    [UnityTest]
    public IEnumerator Build_DeadUnit_Fails()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;

        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        robot.Die(6);

        bool built = move.TryBuild(0);
        Assert.IsFalse(built, "Dead unit cannot build.");
    }
}
