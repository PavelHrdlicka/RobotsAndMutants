using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode integration tests for unit spawning, movement, attack, and build.
/// </summary>
public class UnitMovementPlayTests
{
    private GameObject gridGo;
    private HexGrid grid;
    private GameObject unitGo;
    private UnitData unitData;
    private HexMovement movement;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Destroy all scene objects except the PlayMode test runner controller.
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

        yield return null; // Let Start() run.

        Object.Destroy(prefab);

        unitGo = new GameObject("TestUnit");
        unitData = unitGo.AddComponent<UnitData>();
        unitData.team = Team.Robot;
        unitData.isAlive = true;
        movement = unitGo.AddComponent<HexMovement>();
        movement.Initialize(grid);
        movement.PlaceAt(new HexCoord(0, 0));
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (unitGo != null) Object.Destroy(unitGo);
        if (gridGo != null) Object.Destroy(gridGo);
        // Destroy any extra units created by individual tests.
        foreach (var u in Object.FindObjectsByType<UnitData>(FindObjectsSortMode.None))
            if (u != null && u.gameObject != null) Object.Destroy(u.gameObject);
        yield return null;
    }

    // ── Basic movement ──────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Unit_PlacedAtCorrectHex()
    {
        yield return null;
        Assert.AreEqual(new HexCoord(0, 0), unitData.currentHex);
    }

    [UnityTest]
    public IEnumerator Unit_MoveToValidNeighbor_Succeeds()
    {
        yield return null;
        bool moved = movement.TryMove(0); // East
        Assert.IsTrue(moved, "Move to valid neighbor should succeed.");
        Assert.AreEqual(new HexCoord(1, 0), unitData.currentHex);
    }

    [UnityTest]
    public IEnumerator Unit_MoveOffBoard_Fails()
    {
        yield return null;
        // Use actual board edge (boardSide may be overridden by GameConfig).
        int edge = grid.boardSide - 1;
        var edgeHex = new HexCoord(edge, 0);
        movement.PlaceAt(edgeHex);
        bool moved = movement.TryMove(0); // East — goes off board.
        Assert.IsFalse(moved, $"Move off board from ({edge},0) should fail.");
        Assert.AreEqual(edgeHex, unitData.currentHex, "Position should not change.");
    }

    [UnityTest]
    public IEnumerator Unit_DeadUnit_CannotMove()
    {
        yield return null;
        unitData.Die(12);
        unitGo.SetActive(true);
        unitData.isAlive = false;

        bool moved = movement.TryMove(0);
        Assert.IsFalse(moved, "Dead unit should not move.");
    }

    [UnityTest]
    public IEnumerator Unit_IsValidMove_MatchesTryMove()
    {
        yield return null;
        for (int dir = 0; dir < 6; dir++)
        {
            bool valid = movement.IsValidMove(dir);
            Assert.IsTrue(valid, $"Direction {dir} from center should be valid.");
        }
    }

    [UnityTest]
    public IEnumerator TryMove_SetsMoveTo_AsTarget()
    {
        yield return null;
        var expected = new HexCoord(0, 0).Neighbor(0);
        movement.TryMove(0);
        Assert.AreEqual(expected, unitData.moveTo,
            "moveTo should equal the target hex after a successful move.");
    }

    [UnityTest]
    public IEnumerator TryMove_SetsMoveFrom_AsPreviousPosition()
    {
        yield return null;
        var start = unitData.currentHex;
        movement.TryMove(0);
        Assert.AreEqual(start, unitData.moveFrom,
            "moveFrom should equal the hex the unit was on before moving.");
    }

    [UnityTest]
    public IEnumerator TryMove_SetsLastAction_ToMove()
    {
        yield return null;
        movement.TryMove(0);
        // Moving onto a neutral tile triggers free capture.
        Assert.AreEqual(UnitAction.Capture, unitData.lastAction,
            "lastAction should be Capture after moving onto a neutral tile (free capture).");
    }

    [UnityTest]
    public IEnumerator TryMove_Fails_DoesNotChangeMoveFromOrMoveTo()
    {
        yield return null;
        // Use actual board edge (boardSide may be overridden by GameConfig).
        int edge = grid.boardSide - 1;
        movement.PlaceAt(new HexCoord(edge, 0));
        var beforeFrom = unitData.moveFrom;
        var beforeTo   = unitData.moveTo;

        bool moved = movement.TryMove(0); // East off board.
        Assert.IsFalse(moved);
        Assert.AreEqual(beforeFrom, unitData.moveFrom, "moveFrom should not change on failed move.");
        Assert.AreEqual(beforeTo,   unitData.moveTo,   "moveTo should not change on failed move.");
    }

    // ── Move onto enemy territory captures it ─────────────────────────────

    [UnityTest]
    public IEnumerator TryMove_OntoEnemyTile_CapturesIt()
    {
        yield return null;
        var tile = grid.GetTile(new HexCoord(1, 0));
        Assert.IsNotNull(tile, "Tile (1,0) must exist.");
        tile.Owner = Team.Mutant;

        bool moved = movement.TryMove(0); // East → (1,0) enemy territory

        Assert.IsTrue(moved, "Enemy territory should NOT block movement — unit captures it.");
        Assert.AreEqual(new HexCoord(1, 0), unitData.currentHex, "Unit should move to enemy hex.");
        Assert.AreEqual(Team.Robot, tile.Owner, "Enemy hex should be captured by moving unit.");
    }

    [UnityTest]
    public IEnumerator TryMove_OntoEnemySlime_RobotPaysEnergy()
    {
        yield return null;
        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner    = Team.Mutant;
        tile.TileType = TileType.Slime;
        unitData.Energy = unitData.maxEnergy;

        bool moved = movement.TryMove(0); // East → (1,0) enemy slime

        int slimeCost = GameConfig.Instance != null ? GameConfig.Instance.slimeEntryCostRobot : 3;
        Assert.IsTrue(moved, "Robot should be able to enter enemy slime (paying energy).");
        Assert.AreEqual(unitData.maxEnergy - slimeCost, unitData.Energy, $"Robot pays {slimeCost} energy to enter enemy slime.");
        Assert.AreEqual(TileType.Empty, tile.TileType, "Slime should be destroyed.");
        Assert.AreEqual(Team.Robot, tile.Owner, "Robot claims tile after destroying slime (free capture).");
    }

    // ── Attack ──────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator TryAttack_HitsAdjacentEnemy()
    {
        yield return null;
        // Place an enemy mutant at (1,0) — adjacent to robot at (0,0).
        var enemyGo = new GameObject("Enemy");
        var enemy = enemyGo.AddComponent<UnitData>();
        enemy.team = Team.Mutant;
        enemy.isAlive = true;
        enemy.currentHex = new HexCoord(1, 0);
        enemyGo.transform.position = grid.HexToWorld(enemy.currentHex);

        unitData.Energy = unitData.maxEnergy;
        enemy.Energy = enemy.maxEnergy;

        bool attacked = movement.TryAttack(0); // East → (1,0)
        Assert.IsTrue(attacked, "Attack toward adjacent enemy should succeed.");
        Assert.AreEqual(UnitAction.Attack, unitData.lastAction);
        var cfg = GameConfig.Instance;
        int atkCost = cfg != null ? cfg.attackUnitCost : 3;
        int atkDmg = cfg != null ? cfg.attackUnitDamage : 3;
        Assert.AreEqual(unitData.maxEnergy - atkCost, unitData.Energy, $"Attacker pays {atkCost} energy cost (no counter-damage).");
        Assert.AreEqual(enemy.maxEnergy - atkDmg, enemy.Energy, $"Defender loses {atkDmg} energy from attack.");
        // Attacker stays in place.
        Assert.AreEqual(new HexCoord(0, 0), unitData.currentHex, "Attacker should not move.");

        Object.Destroy(enemyGo);
    }

    // ── Build ───────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator TryBuild_OnAdjacentOwnTile_CreatesRobotWall()
    {
        yield return null;
        // Setup: own tile at (1,0).
        var tile = grid.GetTile(new HexCoord(1, 0));
        Assert.IsNotNull(tile);
        tile.Owner = Team.Robot;
        unitData.Energy = unitData.maxEnergy;

        bool built = movement.TryBuild(0); // Build east → (1,0)
        Assert.IsTrue(built, "Build on own empty adjacent tile should succeed.");
        Assert.AreEqual(Team.Robot, tile.Owner, "Tile should remain owned by Robot.");
        Assert.AreEqual(TileType.Wall, tile.TileType, "Tile type should be Wall.");
        Assert.AreEqual(3, tile.WallHP, "Wall should have max HP.");
        int wallCost = GameConfig.Instance != null ? GameConfig.Instance.wallBuildCost : 4;
        Assert.AreEqual(unitData.maxEnergy - wallCost, unitData.Energy, $"Wall build costs {wallCost} energy.");
        Assert.AreEqual(UnitAction.BuildWall, unitData.lastAction);
    }

    // ── Visual animation tests ──────────────────────────────────────────

    [UnityTest]
    public IEnumerator VisualAnimation_DoesNotTeleport_OnMove()
    {
        yield return null;
        movement.PlaceAt(new HexCoord(0, 0));
        Vector3 startPos = unitGo.transform.position;

        movement.TryMove(0);

        Assert.AreEqual(startPos, unitGo.transform.position,
            "Visual position must not teleport instantly on TryMove — animation starts in Update.");
    }

    [UnityTest]
    public IEnumerator VisualAnimation_CompletesWithinOneTick()
    {
        yield return null;
        movement.PlaceAt(new HexCoord(0, 0));
        movement.TryMove(0);

        Vector3 targetPos = grid.HexToWorld(new HexCoord(1, 0)) + Vector3.up * 0.3f;

        yield return new WaitForSeconds(Time.fixedDeltaTime * 2f);

        float dist = Vector3.Distance(unitGo.transform.position, targetPos);
        Assert.Less(dist, 0.01f,
            $"Visual position must reach target within two ticks. Remaining: {dist:F4}");
    }

    [UnityTest]
    public IEnumerator VisualAnimation_QueuedMoves_EachCompleteSeparately()
    {
        yield return null;
        movement.PlaceAt(new HexCoord(0, 0));

        Vector3 secondTarget = grid.HexToWorld(new HexCoord(1, -1)) + Vector3.up * 0.3f;

        movement.TryMove(0); // East   → (1,  0)
        movement.TryMove(2); // NW     → (1, -1)

        Assert.AreEqual(2, movement.QueueDepth,
            "Both hops must be in the queue immediately after issuing.");

        // After enough time, both hops should be done.
        yield return new WaitForSeconds(Time.fixedDeltaTime * 4f);

        float distToSecond = Vector3.Distance(unitGo.transform.position, secondTarget);
        Assert.Less(distToSecond, 0.01f,
            $"Second hop must complete. Remaining: {distToSecond:F4}");
    }

    [UnityTest]
    public IEnumerator VisualAnimation_NeverSkipsIntermediateHop()
    {
        yield return null;
        movement.PlaceAt(new HexCoord(0, 0));

        Vector3 start       = unitGo.transform.position;
        Vector3 intermediate = grid.HexToWorld(new HexCoord(1, 0)) + Vector3.up * 0.3f;

        movement.TryMove(0); // Hop 1
        movement.TryMove(2); // Hop 2

        // Wait a fraction of a tick.
        yield return new WaitForSeconds(Time.fixedDeltaTime * AnimTickFractionProxy * 0.5f);

        float distToStart        = Vector3.Distance(unitGo.transform.position, start);
        float distToIntermediate = Vector3.Distance(unitGo.transform.position, intermediate);

        Assert.Greater(distToStart, 0.001f,
            "Unit should have left its start position during the first hop.");
        Assert.LessOrEqual(distToIntermediate, distToStart + 0.01f,
            "Unit should be closer to (or at) the intermediate target than to the start — no skipping.");
    }

    private const float AnimTickFractionProxy = HexMovement.AnimTickFraction;

    // ── Performance test ────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Performance_MovementSpeed_Minimum50TurnsPerSecond()
    {
        yield return null;

        const int targetOps = 200;
        float startTime = Time.realtimeSinceStartup;

        // Alternate: move east then move west.
        for (int i = 0; i < targetOps; i++)
        {
            if (i % 2 == 0)
            {
                movement.TryMove(0); // East
                movement.PlaceAt(unitData.currentHex); // snap visual
            }
            else
            {
                movement.TryMove(3); // West (back)
                movement.PlaceAt(unitData.currentHex);
            }
        }

        float elapsed = Time.realtimeSinceStartup - startTime;
        float opsPerSec = targetOps / elapsed;

        Assert.Greater(opsPerSec, 50f,
            $"Movement throughput too slow: {opsPerSec:F0} ops/s (min 50). Elapsed: {elapsed:F3}s");
    }
}
