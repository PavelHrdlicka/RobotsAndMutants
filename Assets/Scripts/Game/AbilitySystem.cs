using System.Collections.Generic;

/// <summary>
/// Applies per-round passive abilities:
/// - Base regeneration: +baseRegenPerStep for units on own base hex.
/// - Mutant slime regeneration: +slimeRegenPerStep for Mutants on own slime.
/// </summary>
public class AbilitySystem
{
    private readonly HexGrid grid;

    public AbilitySystem(HexGrid grid)
    {
        this.grid = grid;
    }

    /// <summary>
    /// Recalculate all unit regeneration. Call once per round.
    /// </summary>
    public void UpdateAbilities(List<UnitData> allUnits)
    {
        var cfg = GameConfig.Instance;

        foreach (var unit in allUnits)
        {
            if (!unit.isAlive) continue;

            var tile = grid.GetTile(unit.currentHex);
            if (tile == null) continue;

            // Base regeneration: +baseRegenPerStep on own base hex.
            if (tile.isBase && tile.baseTeam == unit.team && unit.Energy < unit.maxEnergy)
            {
                int regen = cfg != null ? cfg.baseRegenPerStep : 3;
                unit.Energy += regen;
            }

            // Mutant slime regeneration: +slimeRegenPerStep on own slime.
            if (unit.team == Team.Mutant && tile.Owner == Team.Mutant
                && tile.TileType == TileType.Slime && unit.Energy < unit.maxEnergy)
            {
                int regen = cfg != null ? cfg.slimeRegenPerStep : 1;
                unit.Energy += regen;
            }
        }
    }
}
