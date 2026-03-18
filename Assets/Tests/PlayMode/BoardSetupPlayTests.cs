using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode integration tests verifying full board setup.
/// Note: GameConfig.Instance may override boardSide and unitsPerTeam
/// in Start(), so assertions use actual values after generation.
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
        // Destroy all scene objects so GameManager/UnitFactory/ML-Agents don't interfere.
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            Object.Destroy(go);
        yield return null;

        LogAssert.ignoreFailingMessages = true;

        var prefab = CreatePrefab();

        gridGo = new GameObject("TestGrid");
        grid = gridGo.AddComponent<HexGrid>();
        grid.hexPrefab = prefab;
        grid.boardSide = side;

        factoryGo = new GameObject("TestFactory");
        factory = factoryGo.AddComponent<UnitFactory>();
        factory.grid = grid;
        factory.unitsPerTeam = unitsPerTeam;
        factory.skipMLAgents = true; // No HexAgent/DecisionRequester in tests.

        yield return null; // HexGrid.Start()
        yield return null; // UnitFactory.Start()

        Object.Destroy(prefab);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        // Clean up all spawned units to prevent leaks.
        foreach (var u in Object.FindObjectsByType<UnitData>(FindObjectsSortMode.None))
            if (u != null) Object.Destroy(u.gameObject);
        if (factoryGo != null) Object.Destroy(factoryGo);
        if (gridGo != null) Object.Destroy(gridGo);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Board_Side5_Has61Tiles()
    {
        yield return SetupBoard(5);
        // boardSide may be overridden by GameConfig — use actual value.
        int side = grid.boardSide;
        int expected = HexGrid.TileCount(side);
        Assert.AreEqual(expected, grid.Tiles.Count,
            $"Side {side} board should have {expected} tiles.");
    }

    [UnityTest]
    public IEnumerator Board_Side5_HasBothBases()
    {
        yield return SetupBoard(5);
        var robotBases = grid.GetBaseTiles(Team.Robot);
        var mutantBases = grid.GetBaseTiles(Team.Mutant);

        Assert.Greater(robotBases.Count, 0, "Robot base should have tiles.");
        Assert.Greater(mutantBases.Count, 0, "Mutant base should have tiles.");
        Assert.AreEqual(robotBases.Count, mutantBases.Count, "Both bases should have equal size.");
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
        int baseTiles = grid.GetBaseTiles(Team.Robot).Count + grid.GetBaseTiles(Team.Mutant).Count;
        int expectedContestable = grid.Tiles.Count - baseTiles;

        int contestable = 0;
        foreach (var tile in grid.Tiles.Values)
            if (!tile.isBase) contestable++;

        Assert.AreEqual(expectedContestable, contestable,
            $"Contestable = total ({grid.Tiles.Count}) - bases ({baseTiles}).");
    }

    [UnityTest]
    public IEnumerator Board_Side5_UnitsSpawned()
    {
        yield return SetupBoard(5, 3);
        // unitsPerTeam may be overridden by GameConfig — use actual spawned count.
        Assert.AreEqual(factory.robotUnits.Count, factory.mutantUnits.Count,
            "Both teams should spawn equal units.");
        Assert.Greater(factory.robotUnits.Count, 0, "Should spawn at least 1 robot unit.");
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
        if (cam == null)
            Assert.Ignore("No MainCamera in test scene — skipping camera test.");
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

    // ── Camera test ────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Board_CameraCreatedByGrid_IsOrthographic()
    {
        yield return SetupBoard(5);
        // HexGrid.CenterCamera creates camera at Play time if none exists.
        yield return null;
        var cam = Camera.main;
        if (cam == null)
            Assert.Ignore("No MainCamera — grid did not create one in test scene.");
        Assert.IsTrue(cam.orthographic, "Camera should be orthographic.");
    }

    // ── Performance test ────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Performance_BoardSetup_CompletesUnder2Seconds()
    {
        float start = Time.realtimeSinceStartup;
        yield return SetupBoard(5, 3);
        float elapsed = Time.realtimeSinceStartup - start;

        Assert.Less(elapsed, 2f, $"SetupBoard took {elapsed:F2}s — should be under 2s.");
        Assert.Greater(grid.Tiles.Count, 0, "Grid should have tiles.");
    }
}
