using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests for game mechanics: energy, unit death/respawn, game state.
/// Uses minimal GameObjects without full scene setup.
/// </summary>
public class GameMechanicsTests
{
    [Test]
    public void UnitData_Die_SetsInactive()
    {
        var go = new GameObject("TestUnit");
        var unit = go.AddComponent<UnitData>();
        unit.isAlive = true;
        unit.Energy = 10;

        unit.Die(6);

        Assert.IsFalse(unit.isAlive);
        Assert.AreEqual(0, unit.Energy);
        Assert.AreEqual(6, unit.respawnCooldown);
        Assert.IsFalse(go.activeSelf);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void UnitData_TickCooldown_CountsDown()
    {
        var go = new GameObject("TestUnit");
        var unit = go.AddComponent<UnitData>();
        unit.Die(3);

        Assert.IsFalse(unit.TickCooldown()); // 2 remaining
        Assert.IsFalse(unit.TickCooldown()); // 1 remaining
        Assert.IsTrue(unit.TickCooldown());  // 0 — ready to respawn

        Object.DestroyImmediate(go);
    }

    [Test]
    public void UnitData_Respawn_RestoresState()
    {
        var go = new GameObject("TestUnit");
        var unit = go.AddComponent<UnitData>();
        unit.Die(6);

        var hex = new HexCoord(1, -1);
        unit.Respawn(hex, Vector3.zero);

        Assert.IsTrue(unit.isAlive);
        Assert.AreEqual(15, unit.Energy); // maxEnergy = 15
        Assert.AreEqual(0, unit.respawnCooldown);
        Assert.AreEqual(hex, unit.currentHex);
        Assert.IsTrue(go.activeSelf);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void UnitData_ResetUnit_FullRestore()
    {
        var go = new GameObject("TestUnit");
        var unit = go.AddComponent<UnitData>();
        unit.Energy = 1;

        unit.ResetUnit();

        Assert.AreEqual(15, unit.Energy); // maxEnergy = 15

        Object.DestroyImmediate(go);
    }

    [Test]
    public void GameState_RoundProgress_IsCorrect()
    {
        var state = new GameState(500, 2000, 10, 5, 6, 6, false, Team.None);
        Assert.AreEqual(0.25f, state.RoundProgress, 0.001f);
    }

    [Test]
    public void GameState_TotalContested_SumsBothTeams()
    {
        var state = new GameState(0, 2000, 30, 20, 6, 6, false, Team.None);
        Assert.AreEqual(50, state.TotalContested);
    }
}
