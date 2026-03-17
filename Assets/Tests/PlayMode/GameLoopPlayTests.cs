using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode integration tests for the game loop: territory capture, combat, game over.
/// </summary>
public class GameLoopPlayTests
{
    private GameObject gridGo;
    private HexGrid grid;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
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
        Object.Destroy(gridGo);
        yield return null;
    }

    [UnityTest]
    public IEnumerator TerritoryCapture_UnitOnNeutralTile_ClaimsIt()
    {
        yield return null;

        var unitGo = new GameObject("CaptureUnit");
        var unit = unitGo.AddComponent<UnitData>();
        unit.team = Team.Robot;
        unit.isAlive = true;
        unit.currentHex = new HexCoord(1, 0);

        var territorySystem = new TerritorySystem(grid);
        territorySystem.ProcessCaptures(new System.Collections.Generic.List<UnitData> { unit });

        var tile = grid.GetTile(new HexCoord(1, 0));
        Assert.AreEqual(Team.Robot, tile.Owner, "Unit should capture neutral tile.");

        Object.Destroy(unitGo);
    }

    [UnityTest]
    public IEnumerator Combat_TwoEnemiesOnSameHex_BothTakeDamage()
    {
        yield return null;

        var go1 = new GameObject("Robot");
        var robot = go1.AddComponent<UnitData>();
        robot.team = Team.Robot;
        robot.isAlive = true;
        robot.Health = 3;
        robot.currentHex = new HexCoord(0, 0);

        var go2 = new GameObject("Mutant");
        var mutant = go2.AddComponent<UnitData>();
        mutant.team = Team.Mutant;
        mutant.isAlive = true;
        mutant.Health = 3;
        mutant.currentHex = new HexCoord(0, 0);

        var combatSystem = new CombatSystem();
        combatSystem.ResolveCombat(new System.Collections.Generic.List<UnitData> { robot, mutant });

        Assert.AreEqual(2, robot.Health, "Robot should take 1 damage.");
        Assert.AreEqual(2, mutant.Health, "Mutant should take 1 damage.");

        Object.Destroy(go1);
        Object.Destroy(go2);
    }

    [UnityTest]
    public IEnumerator Combat_ShieldedRobot_TakesNoDamage()
    {
        yield return null;

        var go1 = new GameObject("ShieldedRobot");
        var robot = go1.AddComponent<UnitData>();
        robot.team = Team.Robot;
        robot.isAlive = true;
        robot.Health = 3;
        robot.hasShield = true;
        robot.currentHex = new HexCoord(0, 0);

        var go2 = new GameObject("Mutant");
        var mutant = go2.AddComponent<UnitData>();
        mutant.team = Team.Mutant;
        mutant.isAlive = true;
        mutant.Health = 3;
        mutant.currentHex = new HexCoord(0, 0);

        var combatSystem = new CombatSystem();
        combatSystem.ResolveCombat(new System.Collections.Generic.List<UnitData> { robot, mutant });

        Assert.AreEqual(3, robot.Health, "Shielded Robot should take no damage.");
        Assert.AreEqual(2, mutant.Health, "Mutant should take 1 damage.");

        Object.Destroy(go1);
        Object.Destroy(go2);
    }

    [UnityTest]
    public IEnumerator Combat_UnitDies_At0HP()
    {
        yield return null;

        var go1 = new GameObject("Robot");
        var robot = go1.AddComponent<UnitData>();
        robot.team = Team.Robot;
        robot.isAlive = true;
        robot.Health = 1;
        robot.currentHex = new HexCoord(0, 0);

        var go2 = new GameObject("Mutant");
        var mutant = go2.AddComponent<UnitData>();
        mutant.team = Team.Mutant;
        mutant.isAlive = true;
        mutant.Health = 3;
        mutant.currentHex = new HexCoord(0, 0);

        var combatSystem = new CombatSystem();
        combatSystem.ResolveCombat(new System.Collections.Generic.List<UnitData> { robot, mutant });

        Assert.IsFalse(robot.isAlive, "Robot at 1HP should die after taking 1 damage.");
        Assert.IsTrue(mutant.isAlive, "Mutant at 3HP should survive.");

        Object.Destroy(go1);
        Object.Destroy(go2);
    }
}
