using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles territory capture: when a unit stands on a hex, it claims it.
/// Robots build Crates, Mutants spread Slime to adjacent neutral tiles.
/// Fortified tiles require multiple steps to capture.
/// </summary>
public class TerritorySystem
{
    private readonly HexGrid grid;

    // Tracks how many consecutive steps an enemy unit has been on a fortified tile.
    private readonly Dictionary<HexCoord, int> captureProgress = new();

    public TerritorySystem(HexGrid grid)
    {
        this.grid = grid;
    }

    /// <summary>
    /// Process territory capture for all alive units. Call once per step.
    /// </summary>
    public void ProcessCaptures(List<UnitData> allUnits)
    {
        foreach (var unit in allUnits)
        {
            if (!unit.isAlive) continue;

            var tile = grid.GetTile(unit.currentHex);
            if (tile == null || tile.isBase) continue;

            if (tile.Owner == unit.team)
            {
                // Already own it — optionally build/spread.
                ApplyTeamAbility(unit, tile);
            }
            else
            {
                // Neutral or enemy tile — attempt capture.
                TryCapture(unit, tile);
            }
        }
    }

    private void TryCapture(UnitData unit, HexTileData tile)
    {
        // Fortified enemy tiles need multiple steps to flip.
        if (tile.Owner != Team.None && tile.Fortification > 0)
        {
            captureProgress.TryGetValue(tile.coord, out int progress);
            progress++;

            if (progress <= tile.Fortification)
            {
                captureProgress[tile.coord] = progress;
                return; // Not enough steps yet.
            }

            // Capture succeeds — reset progress.
            captureProgress.Remove(tile.coord);
        }

        // Flip ownership.
        tile.Owner = unit.team;
        tile.TileType = TileType.Empty;
        tile.Fortification = 0;

        // Mark action (only if not already attacking — attack takes priority).
        if (unit.lastAction != UnitAction.Attack)
            unit.lastAction = UnitAction.Capture;
    }

    private void ApplyTeamAbility(UnitData unit, HexTileData tile)
    {
        if (unit.team == Team.Robot && tile.TileType == TileType.Empty)
        {
            // Robots build a Crate on their own empty territory (30% chance per step).
            if (Random.value < 0.3f)
            {
                tile.TileType = TileType.Crate;
                if (unit.lastAction != UnitAction.Attack)
                    unit.lastAction = UnitAction.BuildCrate;
            }
        }
        else if (unit.team == Team.Mutant && tile.TileType == TileType.Empty)
        {
            // Mutants spread Slime on their own tile.
            tile.TileType = TileType.Slime;
            if (unit.lastAction != UnitAction.Attack)
                unit.lastAction = UnitAction.SpreadSlime;

            // Also spread to adjacent neutral tiles.
            SpreadSlime(tile.coord);
        }
    }

    private void SpreadSlime(HexCoord center)
    {
        var neighbors = grid.GetNeighbors(center);
        foreach (var neighbor in neighbors)
        {
            if (neighbor.isBase) continue;
            if (neighbor.Owner == Team.None && neighbor.TileType == TileType.Empty)
            {
                // 20% chance to spread slime to each neutral neighbor.
                if (Random.value < 0.2f)
                {
                    neighbor.Owner = Team.Mutant;
                    neighbor.TileType = TileType.Slime;
                }
            }
        }
    }

    /// <summary>Reset capture progress (call on episode reset).</summary>
    public void Reset()
    {
        captureProgress.Clear();
    }
}
