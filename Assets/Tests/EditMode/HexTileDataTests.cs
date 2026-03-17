using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests for HexTileData: initial state, property change events, reset behavior.
/// </summary>
public class HexTileDataTests
{
    private GameObject go;
    private HexTileData tile;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("TestTile");
        tile = go.AddComponent<HexTileData>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(go);
    }

    [Test]
    public void InitialState_IsNeutralAndEmpty()
    {
        Assert.AreEqual(Team.None, tile.Owner);
        Assert.AreEqual(TileType.Empty, tile.TileType);
        Assert.AreEqual(0, tile.Fortification);
        Assert.IsFalse(tile.isBase);
    }

    [Test]
    public void OwnerChange_FiresEvent()
    {
        bool fired = false;
        tile.OnTileChanged += _ => fired = true;
        tile.Owner = Team.Robot;
        Assert.IsTrue(fired);
    }

    [Test]
    public void OwnerChange_SameValue_DoesNotFireEvent()
    {
        tile.Owner = Team.None; // already None
        bool fired = false;
        tile.OnTileChanged += _ => fired = true;
        tile.Owner = Team.None;
        Assert.IsFalse(fired);
    }

    [Test]
    public void TileTypeChange_FiresEvent()
    {
        bool fired = false;
        tile.OnTileChanged += _ => fired = true;
        tile.TileType = TileType.Slime;
        Assert.IsTrue(fired);
    }

    [Test]
    public void Fortification_ClampedTo0To3()
    {
        tile.Fortification = 5;
        Assert.AreEqual(3, tile.Fortification);

        tile.Fortification = -1;
        Assert.AreEqual(0, tile.Fortification);
    }

    [Test]
    public void ResetTile_ClearsToNeutral()
    {
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Slime;
        tile.Fortification = 2;

        tile.ResetTile();

        Assert.AreEqual(Team.None, tile.Owner);
        Assert.AreEqual(TileType.Empty, tile.TileType);
        Assert.AreEqual(0, tile.Fortification);
    }

    [Test]
    public void ResetTile_PreservesBaseOwnership()
    {
        tile.isBase = true;
        tile.baseTeam = Team.Robot;
        tile.Owner = Team.Mutant;

        tile.ResetTile();

        Assert.AreEqual(Team.Robot, tile.Owner, "Base tile should reset to its base team.");
    }
}
