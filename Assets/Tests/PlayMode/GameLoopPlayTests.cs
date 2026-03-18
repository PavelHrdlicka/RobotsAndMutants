using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode integration tests for the game loop: territory, combat (new TryAttack model),
/// build, and legacy CombatSystem.
/// </summary>
public class GameLoopPlayTests
{
    private GameObject gridGo;
    private HexGrid grid;
    private readonly List<GameObject> spawnedObjects = new();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Ignore background errors from scene objects (GameManager, ML-Agents Academy,
        // ProjectToolsWindow) that run during Play Mode tests but aren't part of the test.
        // Destroy all scene objects so GameManager/UnitFactory/ML-Agents don't interfere.
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
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
        // Destroy all spawned test objects — prevents leaking between tests.
        foreach (var go in spawnedObjects)
            if (go != null) Object.Destroy(go);
        spawnedObjects.Clear();
        if (gridGo != null) Object.Destroy(gridGo);
        yield return null;
    }

    /// <summary>Helper: create a unit with HexMovement, properly initialized.</summary>
    private (UnitData data, HexMovement move) SpawnUnit(Team team, HexCoord hex)
    {
        var go = new GameObject($"{team}_{hex}");
        var data = go.AddComponent<UnitData>();
        data.team    = team;
        data.isAlive = true;
        data.currentHex = hex;

        var move = go.AddComponent<HexMovement>();
        move.Initialize(grid);
        move.PlaceAt(hex);

        spawnedObjects.Add(go);
        return (data, move);
    }

    // ── Territory ────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator TerritoryCapture_UnitOnNeutralTile_ClaimsIt()
    {
        yield return null;

        // ProcessCaptures still sets Owner on neutral tiles (legacy API).
        var (unit, _) = SpawnUnit(Team.Robot, new HexCoord(1, 0));
        var territorySystem = new TerritorySystem(grid);
        territorySystem.ProcessCaptures(new List<UnitData> { unit });

        var tile = grid.GetTile(new HexCoord(1, 0));
        Assert.AreEqual(Team.Robot, tile.Owner, "Unit should capture neutral tile.");
    }

    // ── Combat (new TryAttack model) ─────────────────────────────────────

    [UnityTest]
    public IEnumerator Combat_TryAttack_BothTakeDamage()
    {
        yield return null;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var (mutant, _)        = SpawnUnit(Team.Mutant, new HexCoord(1, 0));

        robot.Health  = 5;
        mutant.Health = 5;

        bool attacked = robotMove.TryAttack(0); // East → hits mutant at (1,0)

        Assert.IsTrue(attacked, "Attack should succeed against adjacent enemy.");
        Assert.AreEqual(4, robot.Health,  "Attacker should take 1 damage.");
        Assert.AreEqual(3, mutant.Health, "Defender should take 2 damage.");
    }

    [UnityTest]
    public IEnumerator Combat_ShieldedAttacker_TakesNoDamage()
    {
        yield return null;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var (mutant, _)        = SpawnUnit(Team.Mutant, new HexCoord(1, 0));

        robot.Health   = 5;
        robot.hasShield = true;
        mutant.Health  = 5;

        robotMove.TryAttack(0);

        Assert.AreEqual(5, robot.Health,  "Shielded attacker should take no damage.");
        Assert.AreEqual(3, mutant.Health, "Defender should still take 2 damage.");
    }

    [UnityTest]
    public IEnumerator Combat_UnitDies_At0HP()
    {
        yield return null;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var (mutant, _)        = SpawnUnit(Team.Mutant, new HexCoord(1, 0));

        robot.Health  = 5;
        mutant.Health = 1; // will die from 2-damage attack

        robotMove.TryAttack(0);

        Assert.IsFalse(mutant.isAlive, "Mutant at 1HP should die after taking 2 damage.");
        Assert.IsTrue(robot.isAlive,   "Robot at 5HP should survive taking 1 damage.");
        Assert.AreEqual(4, robot.Health);
    }
}
