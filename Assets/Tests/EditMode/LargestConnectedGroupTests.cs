using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests for HexGrid territory analysis: LargestConnectedGroup, TerritoryInfo,
/// CountTeamNeighbors, IsFrontlineTile.
/// Creates lightweight GameObjects with HexTileData, populates HexGrid.tiles via reflection.
/// </summary>
public class LargestConnectedGroupTests
{
    private GameObject gridGo;
    private HexGrid grid;
    private Dictionary<HexCoord, HexTileData> tiles;
    private readonly List<GameObject> tileObjects = new();

    [SetUp]
    public void SetUp()
    {
        gridGo = new GameObject("TestGrid");
        grid = gridGo.AddComponent<HexGrid>();

        // Access private tiles dictionary via reflection.
        var field = typeof(HexGrid).GetField("tiles", BindingFlags.NonPublic | BindingFlags.Instance);
        tiles = (Dictionary<HexCoord, HexTileData>)field.GetValue(grid);

        // Reset frame cache so each test gets fresh results.
        var cacheField = typeof(HexGrid).GetField("_territoryInfoCacheFrame", BindingFlags.NonPublic | BindingFlags.Instance);
        cacheField.SetValue(grid, -1);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in tileObjects) Object.DestroyImmediate(go);
        tileObjects.Clear();
        Object.DestroyImmediate(gridGo);
    }

    private HexTileData AddTile(int q, int r, Team owner = Team.None, bool isBase = false)
    {
        var go = new GameObject($"Tile_{q}_{r}");
        var tile = go.AddComponent<HexTileData>();
        tile.coord = new HexCoord(q, r);
        tile.Owner = owner;
        tile.isBase = isBase;
        if (isBase) tile.baseTeam = owner;
        tiles[tile.coord] = tile;
        tileObjects.Add(go);
        return tile;
    }

    // Force cache invalidation so changes within the same frame are picked up.
    private void InvalidateCache()
    {
        var cacheField = typeof(HexGrid).GetField("_territoryInfoCacheFrame", BindingFlags.NonPublic | BindingFlags.Instance);
        cacheField.SetValue(grid, -1);
    }

    // ── LargestConnectedGroup tests ─────────────────────────────────────────

    [Test]
    public void EmptyBoard_ReturnsZero()
    {
        AddTile(0, 0);
        AddTile(1, 0);
        AddTile(0, 1);

        Assert.AreEqual(0, grid.LargestConnectedGroup(Team.Robot));
        Assert.AreEqual(0, grid.LargestConnectedGroup(Team.Mutant));
    }

    [Test]
    public void SingleTile_ReturnsOne()
    {
        AddTile(0, 0, Team.Robot);
        AddTile(1, 0);

        Assert.AreEqual(1, grid.LargestConnectedGroup(Team.Robot));
    }

    [Test]
    public void TwoAdjacentTiles_ReturnsTwo()
    {
        AddTile(0, 0, Team.Robot);
        AddTile(1, 0, Team.Robot);

        Assert.AreEqual(2, grid.LargestConnectedGroup(Team.Robot));
    }

    [Test]
    public void TwoDisconnectedTiles_ReturnsOne()
    {
        AddTile(0, 0, Team.Robot);
        AddTile(2, 0, Team.Robot);
        AddTile(1, 0); // neutral gap

        Assert.AreEqual(1, grid.LargestConnectedGroup(Team.Robot));
    }

    [Test]
    public void LargerGroupWins()
    {
        AddTile(0, 0, Team.Robot);
        AddTile(1, 0, Team.Robot);
        AddTile(1, -1, Team.Robot);
        AddTile(-3, 0, Team.Robot);
        AddTile(-1, 0);
        AddTile(-2, 0);

        Assert.AreEqual(3, grid.LargestConnectedGroup(Team.Robot));
    }

    [Test]
    public void BaseTilesConnectAndCount()
    {
        AddTile(-1, 0, Team.Robot);
        AddTile(0, 0, Team.Robot, isBase: true);
        AddTile(1, 0, Team.Robot);

        Assert.AreEqual(3, grid.LargestConnectedGroup(Team.Robot));
    }

    [Test]
    public void BaseTileOnly_CountsInTerritory()
    {
        AddTile(0, 0, Team.Robot, isBase: true);
        AddTile(1, 0, Team.Robot, isBase: true);

        Assert.AreEqual(2, grid.LargestConnectedGroup(Team.Robot));
    }

    [Test]
    public void TeamsAreIndependent()
    {
        AddTile(0, 0, Team.Robot);
        AddTile(1, 0, Team.Robot);
        AddTile(0, 1, Team.Robot);
        AddTile(-1, 0, Team.Mutant);
        AddTile(-1, 1, Team.Mutant);

        Assert.AreEqual(3, grid.LargestConnectedGroup(Team.Robot));
        Assert.AreEqual(2, grid.LargestConnectedGroup(Team.Mutant));
    }

    [Test]
    public void EnemyTileBreaksConnection()
    {
        AddTile(0, 0, Team.Robot);
        AddTile(1, 0, Team.Mutant);
        AddTile(2, 0, Team.Robot);

        Assert.AreEqual(1, grid.LargestConnectedGroup(Team.Robot));
    }

    [Test]
    public void ChainOfSixDirections()
    {
        AddTile(0, 0, Team.Mutant);
        AddTile(1, 0, Team.Mutant);
        AddTile(1, -1, Team.Mutant);
        AddTile(0, -1, Team.Mutant);
        AddTile(-1, 0, Team.Mutant);
        AddTile(-1, 1, Team.Mutant);
        AddTile(0, 1, Team.Mutant);

        Assert.AreEqual(7, grid.LargestConnectedGroup(Team.Mutant));
    }

    // ── TerritoryInfo tests ─────────────────────────────────────────────────

    [Test]
    public void TerritoryInfo_ComponentCount_TwoIslands()
    {
        AddTile(0, 0, Team.Robot);
        AddTile(1, 0, Team.Robot);
        AddTile(3, 0, Team.Robot);  // isolated
        AddTile(2, 0);              // neutral gap

        var info = grid.GetTerritoryInfo(Team.Robot);
        Assert.AreEqual(2, info.componentCount);
        Assert.AreEqual(2, info.largestGroup);
        Assert.AreEqual(3, info.totalTiles);
    }

    [Test]
    public void TerritoryInfo_Cohesion_AllConnected()
    {
        AddTile(0, 0, Team.Robot);
        AddTile(1, 0, Team.Robot);
        AddTile(0, 1, Team.Robot);

        var info = grid.GetTerritoryInfo(Team.Robot);
        Assert.AreEqual(1, info.componentCount);
        Assert.AreEqual(3, info.largestGroup);
        Assert.AreEqual(3, info.totalTiles);
        // Cohesion = 3/3 = 1.0
        Assert.AreEqual(1f, (float)info.largestGroup / info.totalTiles, 0.001f);
    }

    [Test]
    public void TerritoryInfo_Cohesion_Fragmented()
    {
        // 3 isolated tiles = cohesion 1/3.
        AddTile(0, 0, Team.Mutant);
        AddTile(2, 0, Team.Mutant);
        AddTile(4, 0, Team.Mutant);
        AddTile(1, 0);
        AddTile(3, 0);

        var info = grid.GetTerritoryInfo(Team.Mutant);
        Assert.AreEqual(3, info.componentCount);
        Assert.AreEqual(1, info.largestGroup);
        Assert.AreEqual(3, info.totalTiles);
        Assert.AreEqual(1f / 3f, (float)info.largestGroup / info.totalTiles, 0.01f);
    }

    [Test]
    public void TerritoryInfo_LargestTouchesBase_True()
    {
        AddTile(0, 0, Team.Robot, isBase: true);
        AddTile(1, 0, Team.Robot);
        AddTile(2, 0, Team.Robot);

        var info = grid.GetTerritoryInfo(Team.Robot);
        Assert.IsTrue(info.largestTouchesBase);
        Assert.AreEqual(3, info.largestGroup); // base counts
    }

    [Test]
    public void TerritoryInfo_LargestTouchesBase_False()
    {
        // Base is in a small island, largest group is elsewhere.
        AddTile(0, 0, Team.Robot, isBase: true);
        AddTile(1, 0, Team.Robot);  // group A: 2 tiles (base + 1)

        AddTile(5, 0, Team.Robot);  // group B: 3 tiles (largest)
        AddTile(5, -1, Team.Robot);
        AddTile(5, 1, Team.Robot);

        // Gaps.
        AddTile(2, 0); AddTile(3, 0); AddTile(4, 0);

        var info = grid.GetTerritoryInfo(Team.Robot);
        Assert.AreEqual(3, info.largestGroup);
        Assert.IsFalse(info.largestTouchesBase);
    }

    [Test]
    public void TerritoryInfo_EmptyTeam()
    {
        AddTile(0, 0);
        AddTile(1, 0);

        var info = grid.GetTerritoryInfo(Team.Robot);
        Assert.AreEqual(0, info.largestGroup);
        Assert.AreEqual(0, info.componentCount);
        Assert.AreEqual(0, info.totalTiles);
        Assert.IsFalse(info.largestTouchesBase);
    }

    // ── CountTeamNeighbors tests ────────────────────────────────────────────

    [Test]
    public void CountTeamNeighbors_NoNeighbors()
    {
        AddTile(0, 0, Team.Robot);
        AddTile(1, 0);  // neutral neighbor

        Assert.AreEqual(0, grid.CountTeamNeighbors(new HexCoord(0, 0), Team.Robot));
    }

    [Test]
    public void CountTeamNeighbors_ThreeNeighbors()
    {
        AddTile(0, 0, Team.Robot);
        AddTile(1, 0, Team.Robot);   // E
        AddTile(0, 1, Team.Robot);   // SE
        AddTile(-1, 0, Team.Robot);  // W
        AddTile(1, -1);              // NE neutral
        AddTile(0, -1, Team.Mutant); // NW enemy

        Assert.AreEqual(3, grid.CountTeamNeighbors(new HexCoord(0, 0), Team.Robot));
    }

    [Test]
    public void CountTeamNeighbors_IgnoresBases()
    {
        AddTile(0, 0, Team.Robot);
        AddTile(1, 0, Team.Robot, isBase: true);  // base neighbor — excluded
        AddTile(0, 1, Team.Robot);                 // normal neighbor

        Assert.AreEqual(1, grid.CountTeamNeighbors(new HexCoord(0, 0), Team.Robot));
    }

    // ── IsFrontlineTile tests ───────────────────────────────────────────────

    [Test]
    public void IsFrontline_OwnTileNextToEnemy()
    {
        AddTile(0, 0, Team.Robot);
        AddTile(1, 0, Team.Mutant);

        Assert.IsTrue(grid.IsFrontlineTile(new HexCoord(0, 0), Team.Robot));
    }

    [Test]
    public void IsFrontline_OwnTileNoEnemy()
    {
        AddTile(0, 0, Team.Robot);
        AddTile(1, 0, Team.Robot);
        AddTile(0, 1);

        Assert.IsFalse(grid.IsFrontlineTile(new HexCoord(0, 0), Team.Robot));
    }

    [Test]
    public void IsFrontline_NeutralTile_ReturnsFalse()
    {
        AddTile(0, 0);
        AddTile(1, 0, Team.Mutant);

        Assert.IsFalse(grid.IsFrontlineTile(new HexCoord(0, 0), Team.Robot));
    }

    [Test]
    public void IsFrontline_EnemyTile_ReturnsFalse()
    {
        AddTile(0, 0, Team.Mutant);
        AddTile(1, 0, Team.Robot);

        Assert.IsFalse(grid.IsFrontlineTile(new HexCoord(0, 0), Team.Robot));
    }
}
