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
        // On a neutral tile, lastAction stays Move (no neutralization).
        Assert.AreEqual(UnitAction.Move, unitData.lastAction,
            "lastAction should be Move after a successful move to a neutral tile.");
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

    // ── Move onto enemy tile neutralizes it ──────────────────────────────

    [UnityTest]
    public IEnumerator TryMove_OntoEnemyTile_NeutralizesIt()
    {
        yield return null;
        // Paint (1,0) as mutant slime.
        var tile = grid.GetTile(new HexCoord(1, 0));
        Assert.IsNotNull(tile, "Tile (1,0) must exist.");
        tile.Owner    = Team.Mutant;
        tile.TileType = TileType.Slime;

        movement.TryMove(0); // East → (1,0)

        Assert.AreEqual(Team.None, tile.Owner, "Enemy tile should be neutralized.");
        Assert.AreEqual(TileType.Empty, tile.TileType, "Tile type should be Empty after neutralization.");
        Assert.AreEqual(UnitAction.Capture, unitData.lastAction, "lastAction should be Capture.");
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

        int robotHpBefore = unitData.Health;
        int enemyHpBefore = enemy.Health;

        bool attacked = movement.TryAttack(0); // East → (1,0)
        Assert.IsTrue(attacked, "Attack toward adjacent enemy should succeed.");
        Assert.AreEqual(UnitAction.Attack, unitData.lastAction);
        Assert.AreEqual(UnitAction.Defend, enemy.lastAction);
        Assert.AreEqual(robotHpBefore - 1, unitData.Health, "Attacker takes 1 damage.");
        Assert.AreEqual(enemyHpBefore - 2, enemy.Health, "Defender takes 2 damage.");
        // Attacker stays in place.
        Assert.AreEqual(new HexCoord(0, 0), unitData.currentHex, "Attacker should not move.");

        Object.Destroy(enemyGo);
    }

    // ── Build ───────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator TryBuild_OnNeutralTile_CreatesRobotCrate()
    {
        yield return null;
        // Move to (1,0) first so we are on a neutral tile.
        movement.TryMove(0);
        var tile = grid.GetTile(new HexCoord(1, 0));
        Assert.IsNotNull(tile);
        Assert.AreEqual(Team.None, tile.Owner, "Tile should be neutral before build.");

        bool built = movement.TryBuild();
        Assert.IsTrue(built, "Build on neutral tile should succeed.");
        Assert.AreEqual(Team.Robot, tile.Owner, "Tile should be owned by Robot after build.");
        Assert.AreEqual(TileType.Crate, tile.TileType, "Tile type should be Crate.");
        Assert.AreEqual(UnitAction.BuildCrate, unitData.lastAction);
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
}
