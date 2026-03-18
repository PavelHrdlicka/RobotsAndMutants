using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode integration tests for the game loop: territory capture via movement,
/// combat (TryAttack model), and build mechanics.
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
        // Destroy all scene objects except the PlayMode test runner controller.
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
    public IEnumerator TerritoryCapture_MoveOntoNeutralTile_ClaimsIt()
    {
        yield return null;

        // Territory capture happens via HexMovement.TryMove — moving onto enemy/neutral
        // tile neutralizes it, moving onto own tile is a no-op.
        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        move.TryMove(0); // East → (1,0)

        var tile = grid.GetTile(new HexCoord(1, 0));
        Assert.AreEqual(Team.None, tile.Owner,
            "Neutral tile stays neutral after move (capture only flips enemy tiles).");
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
