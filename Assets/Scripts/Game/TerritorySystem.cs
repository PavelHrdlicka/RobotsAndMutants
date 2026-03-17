using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles territory capture: when a unit moves onto a hex it claims it.
/// Fortified tiles require multiple consecutive steps to flip.
///
/// Build actions (crates / slime) are handled directly by HexMovement.TryBuild()
/// so this system only deals with movement-based capture.
/// </summary>
public class TerritorySystem
{
    private readonly HexGrid grid;

    // Tracks how many consecutive turns an enemy unit has been on a fortified tile.
    private readonly Dictionary<HexCoord, int> captureProgress = new();

    public TerritorySystem(HexGrid grid)
    {
        this.grid = grid;
    }

    /// <summary>
    /// Process territory capture for a single unit after it has moved.
    /// Called by GameManager once per turn when the unit's lastAction == Move.
    /// </summary>
    public void ProcessCaptureForUnit(UnitData unit)
    {
        if (!unit.isAlive) return;

        var tile = grid.GetTile(unit.currentHex);
        if (tile == null || tile.isBase) return;

        if (tile.Owner != unit.team)
            TryCapture(unit, tile);
        // Own tile: no passive ability here — builds are handled by TryBuild().
    }

    /// <summary>
    /// Process territory capture for all alive units. Legacy batch method.
    /// </summary>
    public void ProcessCaptures(List<UnitData> allUnits)
    {
        foreach (var unit in allUnits)
            ProcessCaptureForUnit(unit);
    }

    private void TryCapture(UnitData unit, HexTileData tile)
    {
        // Fortified enemy tiles need multiple turns to flip.
        if (tile.Owner != Team.None && tile.Fortification > 0)
        {
            captureProgress.TryGetValue(tile.coord, out int progress);
            progress++;

            if (progress <= tile.Fortification)
            {
                captureProgress[tile.coord] = progress;
                return; // Not enough turns yet.
            }

            // Capture succeeds — reset progress.
            captureProgress.Remove(tile.coord);
        }

        // Flip ownership.
        tile.Owner         = unit.team;
        tile.TileType      = TileType.Empty;
        tile.Fortification = 0;

        if (unit.lastAction != UnitAction.Attack)
            unit.lastAction = UnitAction.Capture;
    }

    /// <summary>Reset capture progress (call on episode reset).</summary>
    public void Reset()
    {
        captureProgress.Clear();
    }
}
