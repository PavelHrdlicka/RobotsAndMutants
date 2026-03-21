using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode integration tests for the game loop: territory capture via movement,
/// combat (TryAttack model), build mechanics, and wall destruction.
/// </summary>
public class GameLoopPlayTests
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

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        move.TryMove(0); // East → (1,0)

        var tile = grid.GetTile(new HexCoord(1, 0));
        Assert.AreEqual(Team.Robot, tile.Owner,
            "Neutral tile should be claimed by moving unit (free capture).");
    }

    [UnityTest]
    public IEnumerator Move_CapturesEnemyTerritory()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        bool moved = move.TryMove(0); // East → (1,0) enemy territory

        Assert.IsTrue(moved, "Robot should be able to move into Mutant territory.");
        Assert.AreEqual(new HexCoord(1, 0), unit.currentHex);
        Assert.AreEqual(Team.Robot, tile.Owner, "Enemy hex should be captured on entry.");
    }

    [UnityTest]
    public IEnumerator Move_BlockedByWall()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        bool moved = move.TryMove(0);

        Assert.IsFalse(moved, "Wall should block movement even for own team.");
    }

    // ── Combat (unit attacks) ─────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Combat_AttackUnit_CostsEnergy_NoCounterDamage()
    {
        yield return null;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var (mutant, _)        = SpawnUnit(Team.Mutant, new HexCoord(1, 0));

        robot.Energy  = 15;
        mutant.Energy = 15;

        bool attacked = robotMove.TryAttack(0); // East → hits mutant at (1,0)

        var cfg = GameConfig.Instance;
        int atkCost = cfg != null ? cfg.attackUnitCost : 3;
        int atkDmg = cfg != null ? cfg.attackUnitDamage : 3;
        Assert.IsTrue(attacked, "Attack should succeed against adjacent enemy.");
        Assert.AreEqual(15 - atkCost, robot.Energy, $"Attacker should pay {atkCost} energy (no counter-damage).");
        Assert.AreEqual(15 - atkDmg, mutant.Energy, $"Defender should lose {atkDmg} energy from attack.");
    }

    [UnityTest]
    public IEnumerator Combat_AttackUnit_NotEnoughEnergy_Fails()
    {
        yield return null;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var (mutant, _)        = SpawnUnit(Team.Mutant, new HexCoord(1, 0));

        robot.Energy  = 2; // Not enough (attack costs 3)
        mutant.Energy = 15;

        bool attacked = robotMove.TryAttack(0);

        Assert.IsFalse(attacked, "Attack should fail when not enough energy.");
        Assert.AreEqual(2, robot.Energy, "Energy should not change on failed attack.");
    }

    [UnityTest]
    public IEnumerator Combat_UnitDies_At0Energy()
    {
        yield return null;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var (mutant, _)        = SpawnUnit(Team.Mutant, new HexCoord(1, 0));

        robot.Energy  = 15;
        var cfg2 = GameConfig.Instance;
        int dmg = cfg2 != null ? cfg2.attackUnitDamage : 3;
        int cost = cfg2 != null ? cfg2.attackUnitCost : 3;
        mutant.Energy = dmg - 1; // will die from damage

        robotMove.TryAttack(0);

        Assert.IsFalse(mutant.isAlive, $"Mutant at {dmg - 1} energy should die after taking {dmg} damage.");
        Assert.IsTrue(robot.isAlive,   "Robot should survive (no counter-damage).");
        Assert.AreEqual(15 - cost, robot.Energy);
    }

    // ── Combat (wall attacks) ─────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Combat_AttackWall_ReducesHP()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        robot.Energy = 15;

        bool attacked = robotMove.TryAttack(0);

        Assert.IsTrue(attacked, "Attack on wall should succeed.");
        Assert.AreEqual(2, tile.WallHP, "Wall HP should decrease by 1.");
        int wCost = GameConfig.Instance != null ? GameConfig.Instance.attackWallCost : 2;
        Assert.AreEqual(15 - wCost, robot.Energy, $"Wall attack costs {wCost} energy.");
    }

    [UnityTest]
    public IEnumerator Combat_AttackWall_DestroyedAt0HP()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Wall;
        tile.WallHP = 1;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        robot.Energy = 15;

        robotMove.TryAttack(0);

        Assert.AreEqual(TileType.Empty, tile.TileType, "Wall at 1 HP should be destroyed.");
        Assert.AreEqual(0, tile.WallHP);
    }

    // ── Build ─────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Build_Wall_Adjacent_Costs4()
    {
        yield return null;

        // Setup: own tile adjacent.
        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        robot.Energy = 15;

        bool built = robotMove.TryBuild(0); // East → (1,0)

        Assert.IsTrue(built);
        Assert.AreEqual(TileType.Wall, tile.TileType);
        Assert.AreEqual(3, tile.WallHP);
        int wallCost = GameConfig.Instance != null ? GameConfig.Instance.wallBuildCost : 4;
        Assert.AreEqual(15 - wallCost, robot.Energy, $"Wall build costs {wallCost} energy.");
    }

    [UnityTest]
    public IEnumerator Build_Slime_OnSelf_Costs2()
    {
        yield return null;

        // Mutant stands on own hex — builds slime under itself.
        var tile = grid.GetTile(new HexCoord(0, 0));
        tile.Owner = Team.Mutant;

        var (mutant, mutantMove) = SpawnUnit(Team.Mutant, new HexCoord(0, 0));
        mutant.Energy = 15;

        bool built = mutantMove.TryBuild(0); // direction ignored for mutant

        Assert.IsTrue(built);
        Assert.AreEqual(TileType.Slime, tile.TileType, "Slime should be placed on mutant's current hex.");
        Assert.AreEqual(13, mutant.Energy, "Slime place costs 2 energy.");
    }

    [UnityTest]
    public IEnumerator Build_OnEnemyHex_Fails()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        robot.Energy = 15;

        bool built = robotMove.TryBuild(0);
        Assert.IsFalse(built, "Cannot build on enemy hex.");
    }

    // ── Destroy Wall ──────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator DestroyWall_Own_Costs1()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        robot.Energy = 15;

        bool destroyed = robotMove.TryDestroyWall(0);

        Assert.IsTrue(destroyed);
        Assert.AreEqual(TileType.Empty, tile.TileType);
        Assert.AreEqual(14, robot.Energy, "Destroy own wall costs 1 energy.");
        Assert.AreEqual(Team.Robot, tile.Owner, "Tile keeps team ownership.");
    }

    [UnityTest]
    public IEnumerator DestroyWall_Enemy_Fails()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        robot.Energy = 15;

        bool destroyed = robotMove.TryDestroyWall(0);
        Assert.IsFalse(destroyed, "Cannot destroy enemy wall with TryDestroyWall.");
    }

    // ── Single-action-per-turn guarantee ─────────────────────────────────

    [UnityTest]
    public IEnumerator SingleAction_MoveIsExactlyOneHex()
    {
        yield return null;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        unit.Energy = 15;

        bool moved = move.TryMove(0); // East → (1,0)

        Assert.IsTrue(moved);
        Assert.AreEqual(new HexCoord(1, 0), unit.currentHex,
            "TryMove should move exactly one hex, not more.");
        Assert.AreEqual(1, HexCoord.Distance(new HexCoord(0, 0), unit.currentHex),
            "Distance from origin should be exactly 1 after one TryMove.");
    }

    [UnityTest]
    public IEnumerator SingleAction_SecondMoveInSameTurn_MovesAgain()
    {
        yield return null;

        // HexMovement itself does NOT enforce single-action — that's GameManager's job.
        // Calling TryMove twice at the code level moves twice. This proves
        // the enforcement lives in the turn system, not in HexMovement.
        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        unit.Energy = 15;

        move.TryMove(0); // East → (1,0)
        Assert.AreEqual(new HexCoord(1, 0), unit.currentHex);

        move.TryMove(0); // East again → (2,0)
        Assert.AreEqual(new HexCoord(2, 0), unit.currentHex,
            "HexMovement allows multiple moves — turn enforcement is in GameManager/HexAgent.");
    }

    [UnityTest]
    public IEnumerator SingleAction_IsMyTurn_FalseByDefault()
    {
        yield return null;

        var (unit, _) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        Assert.IsFalse(unit.isMyTurn,
            "isMyTurn should be false by default — only GameManager sets it true.");
    }

    [UnityTest]
    public IEnumerator SingleAction_HasPendingTurnResult_FalseByDefault()
    {
        yield return null;

        var (unit, _) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        Assert.IsFalse(unit.hasPendingTurnResult,
            "hasPendingTurnResult should be false by default.");
    }

    [UnityTest]
    public IEnumerator SingleAction_MoveDoesNotChangeIsMyTurn()
    {
        yield return null;

        // HexMovement.TryMove does NOT touch isMyTurn — only HexAgent does.
        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        unit.Energy = 15;
        unit.isMyTurn = true;

        move.TryMove(0);

        Assert.IsTrue(unit.isMyTurn,
            "HexMovement should not change isMyTurn — that's HexAgent's responsibility.");
        Assert.IsFalse(unit.hasPendingTurnResult,
            "HexMovement should not set hasPendingTurnResult — that's HexAgent's responsibility.");
    }

    [UnityTest]
    public IEnumerator SingleAction_AttackDoesNotChangeIsMyTurn()
    {
        yield return null;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var (mutant, _) = SpawnUnit(Team.Mutant, new HexCoord(1, 0));
        robot.Energy = 15;
        mutant.Energy = 15;
        robot.isMyTurn = true;

        robotMove.TryAttack(0);

        Assert.IsTrue(robot.isMyTurn,
            "HexMovement.TryAttack should not change isMyTurn.");
        Assert.IsFalse(robot.hasPendingTurnResult,
            "HexMovement.TryAttack should not set hasPendingTurnResult.");
    }

    [UnityTest]
    public IEnumerator SingleAction_BuildDoesNotChangeIsMyTurn()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;

        var (robot, robotMove) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        robot.Energy = 15;
        robot.isMyTurn = true;

        robotMove.TryBuild(0);

        Assert.IsTrue(robot.isMyTurn,
            "HexMovement.TryBuild should not change isMyTurn.");
        Assert.IsFalse(robot.hasPendingTurnResult,
            "HexMovement.TryBuild should not set hasPendingTurnResult.");
    }

    [UnityTest]
    public IEnumerator SingleAction_OnlyOneUnitActsPerAdvance()
    {
        yield return null;

        // Simulate the turn handshake manually:
        // 1. GameManager sets isMyTurn = true for unit A
        // 2. Unit A acts (TryMove), then HexAgent would set isMyTurn=false, hasPendingTurnResult=true
        // 3. GameManager sees hasPendingTurnResult, advances to unit B
        // 4. Unit B gets isMyTurn = true
        // This test verifies the flag transitions.

        var (unitA, moveA) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var (unitB, moveB) = SpawnUnit(Team.Mutant, new HexCoord(-1, 0));
        unitA.Energy = 15;
        unitB.Energy = 15;

        // Step 1: Unit A gets its turn.
        unitA.isMyTurn = true;
        Assert.IsFalse(unitB.isMyTurn, "Unit B should not have a turn yet.");

        // Step 2: Unit A acts and signals completion (simulating HexAgent).
        moveA.TryMove(0);
        unitA.isMyTurn = false;
        unitA.hasPendingTurnResult = true;

        Assert.IsFalse(unitA.isMyTurn, "Unit A's turn should be over.");
        Assert.IsTrue(unitA.hasPendingTurnResult, "Unit A should signal turn completion.");

        // Step 3: GameManager processes result and advances.
        unitA.hasPendingTurnResult = false;
        unitB.isMyTurn = true;

        Assert.IsFalse(unitA.isMyTurn, "Unit A should stay inactive.");
        Assert.IsTrue(unitB.isMyTurn, "Unit B should now be active.");

        // Step 4: Unit B acts.
        moveB.TryMove(0);
        unitB.isMyTurn = false;
        unitB.hasPendingTurnResult = true;

        Assert.IsFalse(unitB.isMyTurn);
        Assert.IsTrue(unitB.hasPendingTurnResult);
    }

    [UnityTest]
    public IEnumerator SingleAction_DeadUnit_CannotAct()
    {
        yield return null;

        var (unit, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        unit.Energy = 15;
        unit.Die(6);

        bool moved = move.TryMove(0);
        Assert.IsFalse(moved, "Dead unit should not be able to move.");

        // Set up adjacent enemy for attack test.
        var (enemy, _) = SpawnUnit(Team.Mutant, new HexCoord(1, 0));
        enemy.Energy = 15;

        bool attacked = move.TryAttack(0);
        Assert.IsFalse(attacked, "Dead unit should not be able to attack.");

        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;
        bool built = move.TryBuild(0);
        Assert.IsFalse(built, "Dead unit should not be able to build.");
    }

    // ── Invalid actions fall through to Idle ──────────────────────────

    [UnityTest]
    public IEnumerator InvalidAttack_ShowsIdle_NotStaleAction()
    {
        yield return null;

        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        robot.Energy = 15;

        // No enemy or wall at (1,0) — attack should be invalid.
        robot.lastAction = UnitAction.Idle; // simulate HexAgent reset
        bool attacked = move.TryAttack(0);

        Assert.IsFalse(attacked, "Attack on empty hex must fail.");
        Assert.AreEqual(UnitAction.Idle, robot.lastAction,
            "After failed attack, lastAction should remain Idle (not stale).");
    }

    [UnityTest]
    public IEnumerator InvalidDestroyWall_ShowsIdle()
    {
        yield return null;

        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        // No wall at (1,0) — destroy should be invalid.
        robot.lastAction = UnitAction.Idle;
        bool destroyed = move.TryDestroyWall(0);

        Assert.IsFalse(destroyed, "DestroyWall on empty hex must fail.");
        Assert.AreEqual(UnitAction.Idle, robot.lastAction,
            "After failed DestroyWall, lastAction should remain Idle.");
    }

    [UnityTest]
    public IEnumerator InvalidBuild_ShowsIdle()
    {
        yield return null;

        // Hex at (1,0) is neutral — robot can't build on neutral hex.
        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        robot.lastAction = UnitAction.Idle;
        bool built = move.TryBuild(0);

        Assert.IsFalse(built, "Build on neutral hex must fail.");
        Assert.AreEqual(UnitAction.Idle, robot.lastAction,
            "After failed Build, lastAction should remain Idle.");
    }

    [UnityTest]
    public IEnumerator InvalidMove_ShowsIdle()
    {
        yield return null;

        // Wall at (1,0) — move should be blocked.
        var tile = grid.GetTile(new HexCoord(1, 0));
        tile.Owner = Team.Robot;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        var (robot, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        robot.lastAction = UnitAction.Idle;
        bool moved = move.TryMove(0);

        Assert.IsFalse(moved, "Move onto wall must fail.");
        Assert.AreEqual(UnitAction.Idle, robot.lastAction,
            "After failed Move, lastAction should remain Idle.");
    }
}
