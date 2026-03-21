using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for AbilitySystem passive abilities:
///   - Base regen: +baseRegenPerStep for units on own base hex.
///   - Slime regen: +slimeRegenPerStep for Mutants on own slime.
///   - Energy capped at maxEnergy.
///   - Dead units get no regen.
///   - Wrong team on base/slime gets no regen.
/// </summary>
public class AbilitySystemTests
{
    private GameObject gridGo;
    private HexGrid grid;
    private AbilitySystem abilitySystem;
    private Dictionary<HexCoord, HexTileData> tiles;
    private readonly List<GameObject> objects = new();

    [SetUp]
    public void SetUp()
    {
        gridGo = new GameObject("TestGrid");
        grid = gridGo.AddComponent<HexGrid>();

        var field = typeof(HexGrid).GetField("tiles", BindingFlags.NonPublic | BindingFlags.Instance);
        tiles = (Dictionary<HexCoord, HexTileData>)field.GetValue(grid);

        abilitySystem = new AbilitySystem(grid);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in objects) Object.DestroyImmediate(go);
        objects.Clear();
        Object.DestroyImmediate(gridGo);
    }

    private HexTileData AddTile(int q, int r, Team owner = Team.None, bool isBase = false,
        TileType tileType = TileType.Empty)
    {
        var go = new GameObject($"Tile_{q}_{r}");
        var tile = go.AddComponent<HexTileData>();
        tile.coord = new HexCoord(q, r);
        tile.Owner = owner;
        tile.isBase = isBase;
        if (isBase) tile.baseTeam = owner;
        tile.TileType = tileType;
        tiles[tile.coord] = tile;
        objects.Add(go);
        return tile;
    }

    private UnitData AddUnit(Team team, HexCoord hex, int energy = 10)
    {
        var go = new GameObject($"{team}_{hex}");
        var unit = go.AddComponent<UnitData>();
        unit.team = team;
        unit.isAlive = true;
        unit.currentHex = hex;
        unit.Energy = energy;
        objects.Add(go);
        return unit;
    }

    // ── Base regeneration ─────────────────────────────────────────────────

    [Test]
    public void BaseRegen_RobotOnOwnBase_GainsEnergy()
    {
        AddTile(0, 0, Team.Robot, isBase: true);
        var unit = AddUnit(Team.Robot, new HexCoord(0, 0), energy: 10);

        abilitySystem.UpdateAbilities(new List<UnitData> { unit });

        int regen = GameConfig.Instance != null ? GameConfig.Instance.baseRegenPerStep : 3;
        Assert.AreEqual(10 + regen, unit.Energy,
            $"Robot on own base should gain {regen} energy (baseRegenPerStep).");
    }

    [Test]
    public void BaseRegen_MutantOnOwnBase_GainsEnergy()
    {
        AddTile(0, 0, Team.Mutant, isBase: true);
        var unit = AddUnit(Team.Mutant, new HexCoord(0, 0), energy: 10);

        abilitySystem.UpdateAbilities(new List<UnitData> { unit });

        int regen = GameConfig.Instance != null ? GameConfig.Instance.baseRegenPerStep : 3;
        Assert.AreEqual(10 + regen, unit.Energy,
            $"Mutant on own base should gain {regen} energy (baseRegenPerStep).");
    }

    [Test]
    public void BaseRegen_RobotOnEnemyBase_NoRegen()
    {
        var tile = AddTile(0, 0, Team.Mutant, isBase: true);
        var unit = AddUnit(Team.Robot, new HexCoord(0, 0), energy: 10);

        abilitySystem.UpdateAbilities(new List<UnitData> { unit });

        Assert.AreEqual(10, unit.Energy, "Robot on enemy base should NOT gain energy.");
    }

    [Test]
    public void BaseRegen_CappedAtMaxEnergy()
    {
        AddTile(0, 0, Team.Robot, isBase: true);
        var unit = AddUnit(Team.Robot, new HexCoord(0, 0), energy: 14);

        abilitySystem.UpdateAbilities(new List<UnitData> { unit });

        Assert.LessOrEqual(unit.Energy, unit.maxEnergy,
            "Base regen should not exceed maxEnergy.");
    }

    [Test]
    public void BaseRegen_AtMaxEnergy_NoRegen()
    {
        AddTile(0, 0, Team.Robot, isBase: true);
        var unit = AddUnit(Team.Robot, new HexCoord(0, 0), energy: 15);

        abilitySystem.UpdateAbilities(new List<UnitData> { unit });

        Assert.AreEqual(unit.maxEnergy, unit.Energy, "Already at max energy — no regen.");
    }

    // ── Slime regeneration ────────────────────────────────────────────────

    [Test]
    public void SlimeRegen_MutantOnOwnSlime_GainsEnergy()
    {
        AddTile(0, 0, Team.Mutant, tileType: TileType.Slime);
        var unit = AddUnit(Team.Mutant, new HexCoord(0, 0), energy: 10);

        abilitySystem.UpdateAbilities(new List<UnitData> { unit });

        Assert.AreEqual(11, unit.Energy, "Mutant on own slime should gain 1 energy.");
    }

    [Test]
    public void SlimeRegen_RobotOnEnemySlime_NoRegen()
    {
        AddTile(0, 0, Team.Mutant, tileType: TileType.Slime);
        var unit = AddUnit(Team.Robot, new HexCoord(0, 0), energy: 10);

        abilitySystem.UpdateAbilities(new List<UnitData> { unit });

        Assert.AreEqual(10, unit.Energy, "Robot on enemy slime should NOT gain energy.");
    }

    [Test]
    public void SlimeRegen_MutantOnEmptyOwnTile_NoRegen()
    {
        AddTile(0, 0, Team.Mutant);
        var unit = AddUnit(Team.Mutant, new HexCoord(0, 0), energy: 10);

        abilitySystem.UpdateAbilities(new List<UnitData> { unit });

        Assert.AreEqual(10, unit.Energy, "Mutant on own empty tile (no slime) gets no slime regen.");
    }

    [Test]
    public void SlimeRegen_CappedAtMaxEnergy()
    {
        AddTile(0, 0, Team.Mutant, tileType: TileType.Slime);
        var unit = AddUnit(Team.Mutant, new HexCoord(0, 0), energy: 15);

        abilitySystem.UpdateAbilities(new List<UnitData> { unit });

        Assert.AreEqual(unit.maxEnergy, unit.Energy, "Slime regen should not exceed maxEnergy.");
    }

    // ── Combined regen ────────────────────────────────────────────────────

    [Test]
    public void BothRegens_MutantOnOwnBaseWithSlime_GetsBoth()
    {
        var tile = AddTile(0, 0, Team.Mutant, isBase: true, tileType: TileType.Slime);
        var unit = AddUnit(Team.Mutant, new HexCoord(0, 0), energy: 5);

        abilitySystem.UpdateAbilities(new List<UnitData> { unit });

        int baseRegen = GameConfig.Instance != null ? GameConfig.Instance.baseRegenPerStep : 3;
        int slimeRegen = GameConfig.Instance != null ? GameConfig.Instance.slimeRegenPerStep : 1;
        Assert.AreEqual(5 + baseRegen + slimeRegen, unit.Energy,
            $"Mutant on own base with slime should get both regens (+{baseRegen} base + +{slimeRegen} slime).");
    }

    // ── Dead unit ─────────────────────────────────────────────────────────

    [Test]
    public void DeadUnit_NoRegen()
    {
        AddTile(0, 0, Team.Robot, isBase: true);
        var unit = AddUnit(Team.Robot, new HexCoord(0, 0), energy: 0);
        unit.isAlive = false;

        abilitySystem.UpdateAbilities(new List<UnitData> { unit });

        Assert.AreEqual(0, unit.Energy, "Dead unit should not receive any regen.");
    }

    // ── Normal tile ───────────────────────────────────────────────────────

    [Test]
    public void NormalTile_NoRegen()
    {
        AddTile(0, 0, Team.Robot);
        var unit = AddUnit(Team.Robot, new HexCoord(0, 0), energy: 10);

        abilitySystem.UpdateAbilities(new List<UnitData> { unit });

        Assert.AreEqual(10, unit.Energy, "Unit on normal own tile should not gain energy.");
    }
}
