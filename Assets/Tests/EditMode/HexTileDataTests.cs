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
        Assert.AreEqual(0, tile.WallHP);
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
    public void WallHP_ClampedTo0To3()
    {
        tile.WallHP = 5;
        Assert.AreEqual(3, tile.WallHP);

        tile.WallHP = -1;
        Assert.AreEqual(0, tile.WallHP);
    }

    [Test]
    public void WallHP_SetsAndFiresEvent()
    {
        bool fired = false;
        tile.OnTileChanged += _ => fired = true;
        tile.WallHP = 2;
        Assert.IsTrue(fired);
        Assert.AreEqual(2, tile.WallHP);
    }

    [Test]
    public void ResetTile_ClearsToNeutral()
    {
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Slime;
        tile.WallHP = 2;

        tile.ResetTile();

        Assert.AreEqual(Team.None, tile.Owner);
        Assert.AreEqual(TileType.Empty, tile.TileType);
        Assert.AreEqual(0, tile.WallHP);
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

    // ── Structure auto-clear on ownership change ──────────────────────

    [Test]
    public void Slime_ClearedWhenOwnerChangesToRobot()
    {
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Slime;

        tile.Owner = Team.Robot;

        Assert.AreEqual(TileType.Empty, tile.TileType,
            "Slime must be cleared when hex ownership changes from Mutant to Robot.");
    }

    [Test]
    public void Slime_ClearedWhenOwnerChangesToNone()
    {
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Slime;

        tile.Owner = Team.None;

        Assert.AreEqual(TileType.Empty, tile.TileType,
            "Slime must be cleared when hex becomes neutral.");
    }

    [Test]
    public void Slime_StaysWhenOwnerRemainsMutant()
    {
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Slime;

        // Setting same owner should not clear slime.
        tile.Owner = Team.Mutant;

        Assert.AreEqual(TileType.Slime, tile.TileType,
            "Slime should remain if owner stays Mutant.");
    }

    [Test]
    public void Wall_ClearedWhenOwnerChangesToMutant()
    {
        tile.Owner = Team.Robot;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        tile.Owner = Team.Mutant;

        Assert.AreEqual(TileType.Empty, tile.TileType,
            "Wall must be cleared when hex ownership changes from Robot to Mutant.");
        Assert.AreEqual(0, tile.WallHP, "WallHP must be reset to 0.");
    }

    [Test]
    public void Wall_ClearedWhenOwnerChangesToNone()
    {
        tile.Owner = Team.Robot;
        tile.TileType = TileType.Wall;
        tile.WallHP = 2;

        tile.Owner = Team.None;

        Assert.AreEqual(TileType.Empty, tile.TileType,
            "Wall must be cleared when hex becomes neutral.");
        Assert.AreEqual(0, tile.WallHP);
    }

    [Test]
    public void Wall_StaysWhenOwnerRemainsRobot()
    {
        tile.Owner = Team.Robot;
        tile.TileType = TileType.Wall;
        tile.WallHP = 3;

        tile.Owner = Team.Robot;

        Assert.AreEqual(TileType.Wall, tile.TileType,
            "Wall should remain if owner stays Robot.");
        Assert.AreEqual(3, tile.WallHP);
    }

    [Test]
    public void EmptyTile_NotAffectedByOwnerChange()
    {
        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Empty;

        tile.Owner = Team.Robot;

        Assert.AreEqual(TileType.Empty, tile.TileType,
            "Empty tile should stay empty regardless of owner change.");
    }
}
