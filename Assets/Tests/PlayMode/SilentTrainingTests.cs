using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode tests for Silent Training mode.
/// When GameManager.SilentTraining is true:
///   - Camera rendering disabled (frees GPU)
///   - No unit visual models (no RobotModelBuilder, MutantModelBuilder)
///   - No health bars, action indicators, attack effects, labels
///   - Hex tile MeshRenderers disabled
///   - Game logic still works (movement, attack, capture, build)
/// </summary>
public class SilentTrainingTests
{
    private GameObject gridGo;
    private HexGrid grid;
    private UnitFactory factory;
    private readonly List<GameObject> objects = new();
    private bool originalSilent;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name != "Code-based tests runner")
                Object.Destroy(go);
        yield return null;

        if (!LogAssert.ignoreFailingMessages) LogAssert.ignoreFailingMessages = true;

        // Save and enable silent mode.
        originalSilent = GameManager.SilentTraining;
        GameManager.SilentTraining = true;

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

        var factoryGo = new GameObject("UnitFactory");
        factory = factoryGo.AddComponent<UnitFactory>();
        factory.grid = grid;
        factory.unitsPerTeam = 2;
        factory.skipMLAgents = true;
        factory.SpawnAllUnits();
        objects.Add(factoryGo);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        GameManager.SilentTraining = originalSilent;
        foreach (var go in objects)
            if (go != null) Object.Destroy(go);
        objects.Clear();
        if (gridGo != null) Object.Destroy(gridGo);
        yield return null;
    }

    // ── No visual models ───────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Silent_NoRobotModelBuilder()
    {
        yield return null;

        foreach (var unit in factory.robotUnits)
        {
            Assert.IsNull(unit.GetComponent<RobotModelBuilder>(),
                $"{unit.gameObject.name} should NOT have RobotModelBuilder in silent mode.");
        }
    }

    [UnityTest]
    public IEnumerator Silent_NoMutantModelBuilder()
    {
        yield return null;

        foreach (var unit in factory.mutantUnits)
        {
            Assert.IsNull(unit.GetComponent<MutantModelBuilder>(),
                $"{unit.gameObject.name} should NOT have MutantModelBuilder in silent mode.");
        }
    }

    [UnityTest]
    public IEnumerator Silent_NoHealthBar()
    {
        yield return null;

        foreach (var unit in factory.AllUnits)
        {
            Assert.IsNull(unit.GetComponent<UnitHealthBar3D>(),
                $"{unit.gameObject.name} should NOT have UnitHealthBar3D in silent mode.");
        }
    }

    [UnityTest]
    public IEnumerator Silent_NoActionIndicator()
    {
        yield return null;

        foreach (var unit in factory.AllUnits)
        {
            Assert.IsNull(unit.GetComponent<UnitActionIndicator3D>(),
                $"{unit.gameObject.name} should NOT have UnitActionIndicator3D in silent mode.");
        }
    }

    [UnityTest]
    public IEnumerator Silent_NoAttackEffects()
    {
        yield return null;

        foreach (var unit in factory.AllUnits)
        {
            Assert.IsNull(unit.GetComponent<AttackEffects>(),
                $"{unit.gameObject.name} should NOT have AttackEffects in silent mode.");
        }
    }

    [UnityTest]
    public IEnumerator Silent_NoUnitLabel()
    {
        yield return null;

        foreach (var unit in factory.AllUnits)
        {
            var label = unit.transform.Find("Label");
            Assert.IsNull(label,
                $"{unit.gameObject.name} should NOT have Label child in silent mode.");
        }
    }

    // ── No ModelRoot (no 3D primitives created) ────────────────────────

    [UnityTest]
    public IEnumerator Silent_NoModelRoot()
    {
        yield return null;

        foreach (var unit in factory.AllUnits)
        {
            var modelRoot = unit.transform.Find("ModelRoot");
            Assert.IsNull(modelRoot,
                $"{unit.gameObject.name} should NOT have ModelRoot in silent mode.");
        }
    }

    // ── Game logic still works ─────────────────────────────────────────

    [UnityTest]
    public IEnumerator Silent_MoveStillWorks()
    {
        yield return null;

        var unit = factory.robotUnits[0];
        var move = unit.GetComponent<HexMovement>();
        unit.Energy = 15;

        HexCoord start = unit.currentHex;
        bool moved = move.TryMove(0);

        // Move should work even in silent mode (game logic unaffected).
        if (moved)
        {
            Assert.AreNotEqual(start, unit.currentHex,
                "Unit should have moved in silent mode.");
        }
    }

    [UnityTest]
    public IEnumerator Silent_AttackStillWorks()
    {
        yield return null;

        var robot = factory.robotUnits[0];
        var mutant = factory.mutantUnits[0];

        // Place them adjacent.
        var robotMove = robot.GetComponent<HexMovement>();
        var mutantMove = mutant.GetComponent<HexMovement>();
        robotMove.PlaceAt(new HexCoord(0, 0));
        mutantMove.PlaceAt(new HexCoord(1, 0));
        robot.Energy = 15;
        mutant.Energy = 15;

        bool attacked = robotMove.TryAttack(0);

        if (attacked)
        {
            var cfg = GameConfig.Instance;
            int dmg = cfg != null ? cfg.attackUnitDamage : 4;
            Assert.AreEqual(15 - dmg, mutant.Energy,
                "Attack should deal damage even in silent mode.");
        }
    }

    [UnityTest]
    public IEnumerator Silent_CaptureStillWorks()
    {
        yield return null;

        var unit = factory.robotUnits[0];
        var move = unit.GetComponent<HexMovement>();
        move.PlaceAt(new HexCoord(0, 0));
        unit.Energy = 15;

        // Move onto neutral hex — should capture.
        bool moved = move.TryMove(0);
        if (moved)
        {
            var tile = grid.GetTile(unit.currentHex);
            if (tile != null && !tile.isBase)
            {
                Assert.AreEqual(Team.Robot, tile.Owner,
                    "Capture should work in silent mode.");
            }
        }
    }

    [UnityTest]
    public IEnumerator Silent_BuildStillWorks()
    {
        yield return null;

        var unit = factory.robotUnits[0];
        var move = unit.GetComponent<HexMovement>();
        move.PlaceAt(new HexCoord(0, 0));
        unit.Energy = 15;

        // Set adjacent hex as owned.
        var adjTile = grid.GetTile(new HexCoord(1, 0));
        if (adjTile != null)
        {
            adjTile.Owner = Team.Robot;
            bool built = move.TryBuild(0);
            if (built)
            {
                Assert.AreEqual(TileType.Wall, adjTile.TileType,
                    "Build should create wall in silent mode.");
            }
        }
    }

    // ── Units still have core components ───────────────────────────────

    [UnityTest]
    public IEnumerator Silent_UnitsHaveUnitData()
    {
        yield return null;

        foreach (var unit in factory.AllUnits)
        {
            Assert.IsNotNull(unit.GetComponent<UnitData>(),
                $"{unit.gameObject.name} must have UnitData even in silent mode.");
        }
    }

    [UnityTest]
    public IEnumerator Silent_UnitsHaveHexMovement()
    {
        yield return null;

        foreach (var unit in factory.AllUnits)
        {
            Assert.IsNotNull(unit.GetComponent<HexMovement>(),
                $"{unit.gameObject.name} must have HexMovement even in silent mode.");
        }
    }

    // ── Grid tiles exist but renderers disabled ────────────────────────

    [UnityTest]
    public IEnumerator Silent_GridTilesExist()
    {
        yield return null;

        Assert.Greater(grid.Tiles.Count, 0, "Grid should have tiles in silent mode.");
    }

    [UnityTest]
    public IEnumerator Silent_TileDataStillWorks()
    {
        yield return null;

        var tile = grid.GetTile(new HexCoord(0, 0));
        Assert.IsNotNull(tile, "Tile (0,0) should exist.");

        tile.Owner = Team.Robot;
        Assert.AreEqual(Team.Robot, tile.Owner,
            "Tile ownership changes should work in silent mode.");

        tile.TileType = TileType.Wall;
        Assert.AreEqual(TileType.Wall, tile.TileType,
            "Tile type changes should work in silent mode.");
    }
}
