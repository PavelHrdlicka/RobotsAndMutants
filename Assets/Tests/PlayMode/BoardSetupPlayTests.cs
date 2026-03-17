using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode integration tests verifying full board setup at various sizes.
/// Tests grid generation, base placement, camera, and unit spawning.
/// </summary>
public class BoardSetupPlayTests
{
    private GameObject gridGo;
    private HexGrid grid;
    private GameObject factoryGo;
    private UnitFactory factory;

    private GameObject CreatePrefab()
    {
        var prefab = new GameObject("HexPrefab");
        prefab.AddComponent<MeshFilter>();
        prefab.AddComponent<MeshRenderer>();
        prefab.AddComponent<HexMeshGenerator>();
        prefab.AddComponent<HexTileData>();
        prefab.AddComponent<HexVisuals>();
        prefab.SetActive(false);
        return prefab;
    }

    private IEnumerator SetupBoard(int side, int unitsPerTeam = 3)
    {
        var prefab = CreatePrefab();

        gridGo = new GameObject("TestGrid");
        grid = gridGo.AddComponent<HexGrid>();
        grid.hexPrefab = prefab;
        grid.boardSide = side;

        factoryGo = new GameObject("TestFactory");
        factory = factoryGo.AddComponent<UnitFactory>();
        factory.grid = grid;
        factory.unitsPerTeam = unitsPerTeam;

        yield return null; // HexGrid.Start()
        yield return null; // UnitFactory.Start() (coroutine waits 1 frame)

        Object.Destroy(prefab);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (factoryGo != null) Object.Destroy(factoryGo);
        if (gridGo != null) Object.Destroy(gridGo);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Board_Side5_Has61Tiles()
    {
        yield return SetupBoard(5);
        Assert.AreEqual(61, grid.Tiles.Count, "Side 5 board should have 61 tiles.");
    }

    [UnityTest]
    public IEnumerator Board_Side5_HasBothBases()
    {
        yield return SetupBoard(5);
        var robotBases = grid.GetBaseTiles(Team.Robot);
        var mutantBases = grid.GetBaseTiles(Team.Mutant);

        Assert.AreEqual(4, robotBases.Count, "Robot base should have 4 tiles.");
        Assert.AreEqual(4, mutantBases.Count, "Mutant base should have 4 tiles.");
    }

    [UnityTest]
    public IEnumerator Board_Side5_BasesOwnedByTeam()
    {
        yield return SetupBoard(5);
        foreach (var tile in grid.GetBaseTiles(Team.Robot))
            Assert.AreEqual(Team.Robot, tile.Owner, "Robot base tile should be owned by Robot.");
        foreach (var tile in grid.GetBaseTiles(Team.Mutant))
            Assert.AreEqual(Team.Mutant, tile.Owner, "Mutant base tile should be owned by Mutant.");
    }

    [UnityTest]
    public IEnumerator Board_Side5_ContestableTilesCount()
    {
        yield return SetupBoard(5);
        int contestable = 0;
        foreach (var tile in grid.Tiles.Values)
        {
            if (!tile.isBase) contestable++;
        }
        // 61 total - 4 robot base - 4 mutant base = 53
        Assert.AreEqual(53, contestable, "Should have 53 contestable tiles on side-5 board.");
    }

    [UnityTest]
    public IEnumerator Board_Side5_UnitsSpawned()
    {
        yield return SetupBoard(5, 3);
        Assert.AreEqual(3, factory.robotUnits.Count, "Should spawn 3 robot units.");
        Assert.AreEqual(3, factory.mutantUnits.Count, "Should spawn 3 mutant units.");
    }

    [UnityTest]
    public IEnumerator Board_Side5_UnitsOnBaseTiles()
    {
        yield return SetupBoard(5, 3);
        var robotBases = grid.GetBaseTiles(Team.Robot);
        foreach (var unit in factory.robotUnits)
        {
            bool onBase = false;
            foreach (var baseTile in robotBases)
            {
                if (unit.currentHex == baseTile.coord) { onBase = true; break; }
            }
            Assert.IsTrue(onBase, $"Robot unit should spawn on a Robot base tile, got {unit.currentHex}.");
        }
    }

    [UnityTest]
    public IEnumerator Board_Side5_CameraIsOrthographic()
    {
        yield return SetupBoard(5);
        var cam = Camera.main;
        Assert.IsNotNull(cam, "Main camera should exist.");
        Assert.IsTrue(cam.orthographic, "Camera should be orthographic.");
    }

    [UnityTest]
    public IEnumerator Board_AllTilesHaveVisuals()
    {
        yield return SetupBoard(5);
        foreach (var tile in grid.Tiles.Values)
        {
            var visuals = tile.GetComponent<HexVisuals>();
            Assert.IsNotNull(visuals, $"Tile {tile.coord} should have HexVisuals component.");
        }
    }
}
