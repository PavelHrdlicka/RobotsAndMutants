using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Comprehensive PlayMode tests for attack mechanics.
/// Rules:
///   - Attack targets: enemy unit, wall (any team's). Nothing else.
///   - Cannot attack neutral hex, enemy empty hex, slime, base, or own unit.
///   - Attack unit: costs attackUnitCost (3), deals attackUnitDamage (3).
///   - Attack wall: costs attackWallCost (2), deals attackWallDamage (1) to wall HP.
///   - No counter-damage on unit attack.
///   - Shield Wall: robot defender gets -1 damage per adjacent robot ally (max 3).
///   - Swarm: mutant attacker gets +1 damage per adjacent mutant ally (max 3).
/// </summary>
public class AttackMechanicsTests
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

        if (!LogAssert.ignoreFailingMessages) LogAssert.ignoreFailingMessages = true;
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

    // ── Attack enemy unit ───────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Attack_Unit_CostsEnergy_DealsDamage()
    {
        yield return null;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var (mutant, _) = SpawnUnit(Team.Mutant, new HexCoord(1, 0));

        bool attacked = robotMove.TryAttack(0);

        Assert.IsTrue(attacked);
        Assert.AreEqual(12, robot.Energy, "Attacker pays 3 energy.");
        Assert.AreEqual(12, mutant.Energy, "Defender loses 3 energy.");
    }

    [UnityTest]
    public IEnumerator Attack_Unit_NoCounterDamage()
    {
        yield return null;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var (mutant, _) = SpawnUnit(Team.Mutant, new HexCoord(1, 0));

        robotMove.TryAttack(0);

        // Robot only loses attackCost (3), no counter-damage.
        Assert.AreEqual(12, robot.Energy,
            "Attacker should only lose attack cost, no counter-damage.");
    }

    [UnityTest]
    public IEnumerator Attack_Unit_KillsAt0Energy()
    {
        yield return null;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var (mutant, _) = SpawnUnit(Team.Mutant, new HexCoord(1, 0));
        mutant.Energy = 3;

        robotMove.TryAttack(0);

        Assert.IsFalse(mutant.isAlive, "Unit at 3 energy should die from 3 damage.");
        Assert.IsTrue(robot.lastAttackKilled);
    }

    [UnityTest]
    public IEnumerator Attack_Unit_SetsLastAttackTarget()
    {
        yield return null;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var (mutant, _) = SpawnUnit(Team.Mutant, new HexCoord(1, 0));

        robotMove.TryAttack(0);

        Assert.AreEqual(UnitAction.Attack, robot.lastAction);
        Assert.AreEqual(mutant, robot.lastAttackTarget);
    }

    [UnityTest]
    public IEnumerator Attack_Unit_NotEnoughEnergy_Fails()
    {
        yield return null;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var (mutant, _) = SpawnUnit(Team.Mutant, new HexCoord(1, 0));
        robot.Energy = 2;

        bool attacked = robotMove.TryAttack(0);

        Assert.IsFalse(attacked, "Attack should fail with insufficient energy.");
        Assert.AreEqual(2, robot.Energy, "Energy should not change.");
        Assert.AreEqual(15, mutant.Energy, "Target should not take damage.");
    }

    [UnityTest]
    public IEnumerator Attack_Unit_AttackerDiesIfEnergyDropsTo0()
    {
        yield return null;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var (mutant, _) = SpawnUnit(Team.Mutant, new HexCoord(1, 0));
        robot.Energy = 3; // Will drop to 0 after paying attack cost.

        robotMove.TryAttack(0);

        Assert.IsFalse(robot.isAlive,
            "Attacker should die if energy drops to 0 from attack cost.");
    }

    [UnityTest]
    public IEnumerator Attack_Unit_DoesNotMoveAttacker()
    {
        yield return null;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var (_, _) = SpawnUnit(Team.Mutant, new HexCoord(1, 0));

        robotMove.TryAttack(0);

        Assert.AreEqual(new HexCoord(0, 0), robot.currentHex,
            "Attack should not move the attacker.");
    }

    // ── Attack wall ─────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Attack_Wall_ReducesHP()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        bool attacked = robotMove.TryAttack(0);

        Assert.IsTrue(attacked);
        Assert.AreEqual(2, tile.WallHP, "Wall HP should decrease by 1.");
        Assert.AreEqual(13, robot.Energy, "Wall attack costs 2 energy.");
    }

    [UnityTest]
    public IEnumerator Attack_Wall_DestroyedAt0HP()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Wall;
        tile.WallHP = 1;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        robotMove.TryAttack(0);

        Assert.AreEqual(TileType.Empty, tile.TileType,
            "Wall at 1 HP should be destroyed after attack.");
        Assert.AreEqual(0, tile.WallHP);
    }

    [UnityTest]
    public IEnumerator Attack_OwnWall_Succeeds()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        bool attacked = robotMove.TryAttack(0);

        Assert.IsTrue(attacked,
            "Should be able to attack own wall (any team's wall is valid target).");
        Assert.AreEqual(2, tile.WallHP);
    }

    [UnityTest]
    public IEnumerator Attack_Wall_NotEnoughEnergy_Fails()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        robot.Energy = 1;

        bool attacked = robotMove.TryAttack(0);

        Assert.IsFalse(attacked, "Wall attack should fail with insufficient energy.");
        Assert.AreEqual(3, tile.WallHP, "Wall HP should not change.");
    }

    // ── Attack invalid targets (should fail) ────────────────────────────

    [UnityTest]
    public IEnumerator Attack_NeutralHex_Fails()
    {
        yield return null;

        // Hex at (1,0) is neutral by default.
        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        bool attacked = robotMove.TryAttack(0);

        Assert.IsFalse(attacked,
            "Cannot attack neutral hex — capture by moving.");
    }

    [UnityTest]
    public IEnumerator Attack_EnemyEmptyHex_Fails()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        bool attacked = robotMove.TryAttack(0);

        Assert.IsFalse(attacked,
            "Cannot attack enemy empty hex — capture by moving.");
        Assert.AreEqual(Team.Mutant, tile.Owner,
            "Hex ownership should not change.");
    }

    [UnityTest]
    public IEnumerator Attack_EnemySlime_Fails()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Slime;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        bool attacked = robotMove.TryAttack(0);

        Assert.IsFalse(attacked,
            "Cannot attack slime — destroy by moving onto it.");
        Assert.AreEqual(TileType.Slime, tile.TileType,
            "Slime should remain intact.");
    }

    [UnityTest]
    public IEnumerator Attack_OwnUnit_Fails()
    {
        yield return null;

        var (_, _) = SpawnUnit(Team.Robot, new HexCoord(1, 0));
        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        bool attacked = robotMove.TryAttack(0);

        Assert.IsFalse(attacked, "Cannot attack own unit.");
    }

    [UnityTest]
    public IEnumerator Attack_EmptyHex_NoUnit_NoWall_Fails()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot; // own empty hex

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        bool attacked = robotMove.TryAttack(0);

        Assert.IsFalse(attacked,
            "Cannot attack own empty hex — no valid target.");
    }

    // ── Attack priority: unit before wall ───────────────────────────────

    [UnityTest]
    public IEnumerator Attack_Priority_UnitBeforeWall()
    {
        yield return null;

        // Place both enemy unit and wall at same hex.
        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        var (_, _) = SpawnUnit(Team.Mutant, new HexCoord(1, 0));
        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        robotMove.TryAttack(0);

        Assert.AreEqual(UnitAction.Attack, robot.lastAction);
        Assert.IsNotNull(robot.lastAttackTarget,
            "Should attack the unit, not the wall.");
        Assert.AreEqual(3, tile.WallHP,
            "Wall HP should not change — unit was targeted.");
    }

    // ── Shield Wall (Robot defender) ────────────────────────────────────

    [UnityTest]
    public IEnumerator Attack_ShieldWall_ReducesDamage()
    {
        yield return null;

        // Target robot with 2 adjacent robot allies → -2 damage.
        var (target, _) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var (_, _) = SpawnUnit(Team.Robot, new HexCoord(1, 0));   // ally 1
        var (_, _) = SpawnUnit(Team.Robot, new HexCoord(0, -1));  // ally 2

        var (mutant, mutantMove) = SpawnUnit(Team.Mutant, new HexCoord(-1, 0));

        // Mutant attacks the surrounded robot.
        // Direction: find direction from (-1,0) to (0,0).
        int dir = -1;
        for (int d = 0; d < 6; d++)
        {
            if (new HexCoord(-1, 0).Neighbor(d) == new HexCoord(0, 0))
            { dir = d; break; }
        }
        Assert.IsTrue(dir >= 0);

        mutantMove.TryAttack(dir);

        // Base damage 3, reduced by 2 (2 allies) = 1 damage.
        Assert.AreEqual(14, target.Energy,
            "Shield Wall: 2 adjacent robot allies should reduce damage by 2 (3-2=1).");
    }

    // ── Swarm (Mutant attacker) ─────────────────────────────────────────

    [UnityTest]
    public IEnumerator Attack_Swarm_IncreaseDamage()
    {
        yield return null;

        var (target, _) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        // Mutant attacker at (-1,0) with 2 adjacent mutant allies.
        var (mutant, mutantMove) = SpawnUnit(Team.Mutant, new HexCoord(-1, 0));
        var (_, _) = SpawnUnit(Team.Mutant, new HexCoord(-1, 1));   // ally 1
        var (_, _) = SpawnUnit(Team.Mutant, new HexCoord(-2, 1));   // ally 2

        int dir = -1;
        for (int d = 0; d < 6; d++)
        {
            if (new HexCoord(-1, 0).Neighbor(d) == new HexCoord(0, 0))
            { dir = d; break; }
        }
        Assert.IsTrue(dir >= 0);

        mutantMove.TryAttack(dir);

        // Base damage 3 + 2 (2 allies) = 5 damage.
        Assert.AreEqual(10, target.Energy,
            "Swarm: 2 adjacent mutant allies should increase damage by 2 (3+2=5).");
    }

    // ── IsValidAttack consistency ───────────────────────────────────────

    [UnityTest]
    public IEnumerator IsValidAttack_True_ForEnemyUnit()
    {
        yield return null;

        var (_, _) = SpawnUnit(Team.Mutant, new HexCoord(1, 0));
        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        Assert.IsTrue(move.IsValidAttack(0));
    }

    [UnityTest]
    public IEnumerator IsValidAttack_True_ForWall()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        var (_, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        Assert.IsTrue(move.IsValidAttack(0));
    }

    [UnityTest]
    public IEnumerator IsValidAttack_False_ForNeutralHex()
    {
        yield return null;

        var (_, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        Assert.IsFalse(move.IsValidAttack(0),
            "Cannot attack neutral hex.");
    }

    [UnityTest]
    public IEnumerator IsValidAttack_False_ForEnemyEmptyHex()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;

        var (_, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        Assert.IsFalse(move.IsValidAttack(0),
            "Cannot attack enemy empty hex.");
    }

    [UnityTest]
    public IEnumerator IsValidAttack_False_ForSlime()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Slime;

        var (_, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        Assert.IsFalse(move.IsValidAttack(0),
            "Cannot attack slime — must move onto it.");
    }

    [UnityTest]
    public IEnumerator IsValidAttack_False_NotEnoughEnergy_Unit()
    {
        yield return null;

        var (_, _) = SpawnUnit(Team.Mutant, new HexCoord(1, 0));
        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        robot.Energy = 2;

        Assert.IsFalse(move.IsValidAttack(0),
            "IsValidAttack should be false when not enough energy for unit attack.");
    }

    [UnityTest]
    public IEnumerator IsValidAttack_False_NotEnoughEnergy_Wall()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        robot.Energy = 1;

        Assert.IsFalse(move.IsValidAttack(0),
            "IsValidAttack should be false when not enough energy for wall attack.");
    }

    // ── Dead unit cannot attack ─────────────────────────────────────────

    [UnityTest]
    public IEnumerator Attack_DeadUnit_Fails()
    {
        yield return null;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var (_, _) = SpawnUnit(Team.Mutant, new HexCoord(1, 0));
        robot.Die(6);

        bool attacked = robotMove.TryAttack(0);
        Assert.IsFalse(attacked, "Dead unit cannot attack.");
    }

    // ── Attack dead enemy ─────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Attack_DeadEnemy_Fails()
    {
        yield return null;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var (mutant, _) = SpawnUnit(Team.Mutant, new HexCoord(1, 0));
        mutant.Die(6);

        bool attacked = robotMove.TryAttack(0);
        Assert.IsFalse(attacked, "Cannot attack dead enemy unit.");
    }

    // ── Attack base hex ───────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Attack_EnemyBaseHex_NoUnitOrWall_Fails()
    {
        yield return null;

        // Find a mutant base hex adjacent to a non-base hex.
        HexCoord baseCoord = default;
        HexCoord adjacentCoord = default;
        bool found = false;
        foreach (var kvp in grid.Tiles)
        {
            if (kvp.Value.isBase && kvp.Value.Owner == Team.Mutant)
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
            Assert.Inconclusive("No mutant base with non-base neighbor found.");
            yield break;
        }

        var (robot, move) = SpawnUnit(Team.Robot, adjacentCoord);

        int dir = -1;
        for (int d = 0; d < 6; d++)
        {
            if (adjacentCoord.Neighbor(d) == baseCoord) { dir = d; break; }
        }
        Assert.IsTrue(dir >= 0);

        bool attacked = move.TryAttack(dir);
        Assert.IsFalse(attacked,
            "Cannot attack enemy base hex when no unit or wall is there.");
    }
}
