using System.Collections.Generic;

/// <summary>
/// Applies per-team passive abilities each round (called at round start):
///
/// Robot  — Shield: takes 0 incoming damage while standing on own territory.
/// Mutant — Regeneration: heals 1 energy when standing on own slime.
/// Both   — Adjacency fortification: allied neighbours strengthen owned tiles (0-3).
/// </summary>
public class AbilitySystem
{
    private readonly HexGrid grid;

    public AbilitySystem(HexGrid grid)
    {
        this.grid = grid;
    }

    /// <summary>
    /// Recalculate all unit buffs and tile fortification. Call once per round.
    /// </summary>
    public void UpdateAbilities(List<UnitData> allUnits)
    {
        foreach (var unit in allUnits)
        {
            if (!unit.isAlive) continue;

            var tile = grid.GetTile(unit.currentHex);
            if (tile == null) continue;

            // Robot shield: active when standing on own territory.
            unit.hasShield = (unit.team == Team.Robot && tile.Owner == Team.Robot);

            // Mutant regeneration: heal 1 energy when standing on own slime.
            if (unit.team == Team.Mutant && tile.Owner == Team.Mutant
                && tile.TileType == TileType.Slime && unit.Health < unit.maxHealth)
            {
                unit.Health += 1;
            }

            // Adjacency fortification: count allied neighbours.
            if (tile.Owner == unit.team && !tile.isBase)
            {
                int allyNeighborCount = CountAllyNeighbors(unit, allUnits);
                tile.Fortification = allyNeighborCount; // Clamped 0-3 by property setter.
            }
        }
    }

    private int CountAllyNeighbors(UnitData unit, List<UnitData> allUnits)
    {
        int count = 0;
        var neighbors = grid.GetNeighbors(unit.currentHex);

        foreach (var neighborTile in neighbors)
        {
            foreach (var other in allUnits)
            {
                if (other == unit || !other.isAlive) continue;
                if (other.team == unit.team && other.currentHex == neighborTile.coord)
                {
                    count++;
                    break;
                }
            }
        }

        return count;
    }
}
