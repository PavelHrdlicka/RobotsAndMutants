using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode integration tests for HexGrid generation and tile data.
/// Tests run in a live scene with MonoBehaviour lifecycle.
/// </summary>
public class HexGridPlayTests
{
    private GameObject gridGo;
    private HexGrid grid;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Create hex prefab.
        var prefab = new GameObject("HexPrefab");
        prefab.AddComponent<MeshFilter>();
        prefab.AddComponent<MeshRenderer>();
        prefab.AddComponent<HexMeshGenerator>();
        prefab.AddComponent<HexTileData>();
        prefab.AddComponent<HexVisuals>();
        prefab.SetActive(false);

        gridGo = new GameObject("TestGrid");
        grid = gridGo.AddComponent<HexGrid>();
        grid.hexPrefab = prefab;
        grid.boardSide = 3; // Small board for fast tests (19 tiles).

        // Wait for Start() to run.
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
    public IEnumerator Grid_GeneratesCorrectTileCount()
    {
        yield return null;
        Assert.AreEqual(HexGrid.TileCount(3), grid.Tiles.Count,
            $"Board side 3 should have {HexGrid.TileCount(3)} tiles.");
    }

    [UnityTest]
    public IEnumerator Grid_CenterTileExists()
    {
        yield return null;
        var center = grid.GetTile(new HexCoord(0, 0));
        Assert.IsNotNull(center, "Center tile (0,0) should exist.");
    }

    [UnityTest]
    public IEnumerator Grid_CenterHas6Neighbors()
    {
        yield return null;
        var neighbors = grid.GetNeighbors(new HexCoord(0, 0));
        Assert.AreEqual(6, neighbors.Count, "Center tile should have 6 neighbors.");
    }

    [UnityTest]
    public IEnumerator Grid_CornerHas3Neighbors()
    {
        yield return null;
        int max = grid.boardSide - 1;
        var neighbors = grid.GetNeighbors(new HexCoord(max, -max));
        Assert.AreEqual(3, neighbors.Count, "Corner tile should have 3 neighbors.");
    }

    [UnityTest]
    public IEnumerator Grid_BasesAreSetup()
    {
        yield return null;
        var robotBases = grid.GetBaseTiles(Team.Robot);
        var mutantBases = grid.GetBaseTiles(Team.Mutant);

        Assert.Greater(robotBases.Count, 0, "Should have Robot base tiles.");
        Assert.Greater(mutantBases.Count, 0, "Should have Mutant base tiles.");
    }

    [UnityTest]
    public IEnumerator Grid_HexToWorldRoundTrips()
    {
        yield return null;
        var coord = new HexCoord(1, -1);
        Vector3 world = grid.HexToWorld(coord);
        HexCoord back = grid.WorldToHex(world);
        Assert.AreEqual(coord, back, "HexToWorld -> WorldToHex should round-trip.");
    }
}
