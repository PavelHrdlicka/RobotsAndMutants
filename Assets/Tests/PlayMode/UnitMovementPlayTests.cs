using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode integration tests for unit spawning and movement.
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
        // Create minimal hex grid.
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

        // Create a test unit.
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
        Object.Destroy(unitGo);
        Object.Destroy(gridGo);
        yield return null;
    }

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
        // Move to edge first.
        movement.PlaceAt(new HexCoord(2, 0));
        bool moved = movement.TryMove(0); // East — goes off board.
        Assert.IsFalse(moved, "Move off board should fail.");
        Assert.AreEqual(new HexCoord(2, 0), unitData.currentHex, "Position should not change.");
    }

    [UnityTest]
    public IEnumerator Unit_DeadUnit_CannotMove()
    {
        yield return null;
        unitData.Die(30);
        // Re-enable for test (Die disables GO).
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
            // All 6 directions from center of side-3 board should be valid.
            Assert.IsTrue(valid, $"Direction {dir} from center should be valid.");
        }
    }

    [UnityTest]
    public IEnumerator TryMove_SetsMoveTo_AsTarget()
    {
        yield return null;
        var expected = new HexCoord(0, 0).Neighbor(0); // East = (1, 0)
        movement.TryMove(0);
        Assert.AreEqual(expected, unitData.moveTo,
            "moveTo should equal the target hex after a successful move.");
    }

    [UnityTest]
    public IEnumerator TryMove_SetsMoveFrom_AsPreviousPosition()
    {
        yield return null;
        var start = unitData.currentHex; // (0, 0)
        movement.TryMove(0);
        Assert.AreEqual(start, unitData.moveFrom,
            "moveFrom should equal the hex the unit was on before moving.");
    }

    [UnityTest]
    public IEnumerator TryMove_SetsLastAction_ToMove()
    {
        yield return null;
        movement.TryMove(0);
        Assert.AreEqual(UnitAction.Move, unitData.lastAction,
            "lastAction should be Move after a successful move.");
    }

    [UnityTest]
    public IEnumerator TryMove_Fails_DoesNotChangeMoveFromOrMoveTo()
    {
        yield return null;
        // Move to edge, then try to move off-board.
        movement.PlaceAt(new HexCoord(2, 0));
        var beforeFrom = unitData.moveFrom;
        var beforeTo   = unitData.moveTo;

        bool moved = movement.TryMove(0); // East off board.
        Assert.IsFalse(moved);
        Assert.AreEqual(beforeFrom, unitData.moveFrom, "moveFrom should not change on failed move.");
        Assert.AreEqual(beforeTo,   unitData.moveTo,   "moveTo should not change on failed move.");
    }

    // ── Visual animation tests ──────────────────────────────────────────────

    [UnityTest]
    public IEnumerator VisualAnimation_DoesNotTeleport_OnMove()
    {
        yield return null;
        movement.PlaceAt(new HexCoord(0, 0));
        Vector3 startPos = unitGo.transform.position;

        movement.TryMove(0); // Enqueues visual hop, but Update hasn't run yet.

        Assert.AreEqual(startPos, unitGo.transform.position,
            "Visual position must not teleport instantly on TryMove — animation starts in Update.");
    }

    [UnityTest]
    public IEnumerator VisualAnimation_CompletesWithinOneTick()
    {
        yield return null;
        movement.PlaceAt(new HexCoord(0, 0));
        movement.TryMove(0); // East → (1, 0).

        Vector3 targetPos = grid.HexToWorld(new HexCoord(1, 0)) + Vector3.up * 0.3f;

        // AnimTickFraction = 0.7: animation finishes in 70 % of one fixedDeltaTime.
        // Waiting a full fixedDeltaTime is more than enough.
        yield return new WaitForSeconds(Time.fixedDeltaTime);

        float dist = Vector3.Distance(unitGo.transform.position, targetPos);
        Assert.Less(dist, 0.01f,
            $"Visual position must reach target within one tick. Remaining: {dist:F4}");
    }

    [UnityTest]
    public IEnumerator VisualAnimation_QueuedMoves_EachCompleteSeparately()
    {
        // Issue two moves back-to-back (simulating two consecutive ticks firing fast).
        // The queue must ensure both hops are animated in order, each finishing fully.
        yield return null;
        movement.PlaceAt(new HexCoord(0, 0));

        Vector3 firstTarget  = grid.HexToWorld(new HexCoord(1, 0))  + Vector3.up * 0.3f;
        Vector3 secondTarget = grid.HexToWorld(new HexCoord(1, -1)) + Vector3.up * 0.3f;

        movement.TryMove(0); // East   → (1,  0) — enqueued as hop 1.
        movement.TryMove(2); // NW from (1,0) → (1,-1) — enqueued as hop 2.

        Assert.AreEqual(2, movement.QueueDepth,
            "Both hops must be in the queue immediately after issuing.");

        // After one tick the first hop should be done.
        yield return new WaitForSeconds(Time.fixedDeltaTime);

        float distToFirst = Vector3.Distance(unitGo.transform.position, firstTarget);
        Assert.Less(distToFirst, 0.01f,
            $"First hop must complete within one tick. Remaining: {distToFirst:F4}");

        // After a second tick the second hop should also be done.
        yield return new WaitForSeconds(Time.fixedDeltaTime);

        float distToSecond = Vector3.Distance(unitGo.transform.position, secondTarget);
        Assert.Less(distToSecond, 0.01f,
            $"Second hop must complete within one tick after the first. Remaining: {distToSecond:F4}");
    }

    [UnityTest]
    public IEnumerator VisualAnimation_NeverSkipsIntermediateHop()
    {
        // When two hops are queued, the unit must pass THROUGH the intermediate
        // position (first target), not fly directly from start to final destination.
        yield return null;
        movement.PlaceAt(new HexCoord(0, 0));

        Vector3 start       = unitGo.transform.position;
        Vector3 intermediate = grid.HexToWorld(new HexCoord(1, 0)) + Vector3.up * 0.3f;

        movement.TryMove(0); // Hop 1: → (1,  0).
        movement.TryMove(2); // Hop 2: → (1, -1).

        // Halfway through the first hop: unit should be closer to intermediate than to start.
        yield return new WaitForSeconds(Time.fixedDeltaTime * AnimTickFractionProxy * 0.5f);

        float distToStart        = Vector3.Distance(unitGo.transform.position, start);
        float distToIntermediate = Vector3.Distance(unitGo.transform.position, intermediate);

        Assert.Greater(distToStart, 0.01f,
            "Unit should have left its start position during the first hop.");
        Assert.Less(distToIntermediate, distToStart,
            "Unit should be closer to the intermediate target than to the start — no skipping.");
    }

    // Proxy to avoid referencing const across assembly boundary in test.
    private const float AnimTickFractionProxy = HexMovement.AnimTickFraction;
}
