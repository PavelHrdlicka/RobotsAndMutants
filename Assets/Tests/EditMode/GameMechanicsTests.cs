using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests for game mechanics: combat damage, unit death/respawn, game state.
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
        unit.Health = 5;

        unit.Die(12);

        Assert.IsFalse(unit.isAlive);
        Assert.AreEqual(0, unit.Health);
        Assert.AreEqual(12, unit.respawnCooldown);
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
        unit.Die(12);

        var hex = new HexCoord(1, -1);
        unit.Respawn(hex, Vector3.zero);

        Assert.IsTrue(unit.isAlive);
        Assert.AreEqual(5, unit.Health);
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
        unit.Health = 1;
        unit.hasShield = true;

        unit.ResetUnit();

        Assert.AreEqual(5, unit.Health);
        Assert.IsFalse(unit.hasShield);

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
