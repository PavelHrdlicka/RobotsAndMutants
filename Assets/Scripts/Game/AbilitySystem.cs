using System.Collections.Generic;

/// <summary>
/// Applies per-team passive abilities each step:
/// - Robot shield: +1 defense on own territory
/// - Mutant speed: can move 2 hexes on slime (speedMultiplier = 2)
/// - Adjacency bonus: allied neighbors grant fortification (up to 3)
/// </summary>
public class AbilitySystem
{
    private readonly HexGrid grid;

    public AbilitySystem(HexGrid grid)
    {
        this.grid = grid;
    }

    /// <summary>
    /// Recalculate all unit buffs and tile fortification. Call once per step.
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

            // Mutant speed: doubled when standing on slime.
            unit.speedMultiplier = (unit.team == Team.Mutant && tile.TileType == TileType.Slime) ? 2f : 1f;

            // Adjacency fortification: count allied neighbors.
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
