using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles discrete hex-to-hex movement, combat, and building for a unit.
///
/// Sequential turn model:
///   TryMove   — moves to an empty adjacent hex (no unit of any team there).
///   TryAttack — fights the enemy on an adjacent hex; attacker stays in place.
///   TryBuild  — builds a Crate (Robot) or spreads Slime (Mutant) on the
///               unit's current tile.
///
/// Visual position works through a Queue of waypoints so each hop animation
/// completes before the next one starts, regardless of tick rate.
/// Speed is auto-computed so each hop finishes in AnimTickFraction of one
/// physics tick.
/// </summary>
[RequireComponent(typeof(UnitData))]
public class HexMovement : MonoBehaviour
{
    /// <summary>
    /// Fraction of one physics tick used for a single hop animation (0 &lt; f ≤ 1).
    /// 0.7 → finishes in 70 % of a tick, leaving headroom before the next move.
    /// </summary>
    public const float AnimTickFraction = 0.7f;

    /// <summary>
    /// Maximum pending hops in the visual queue. Entries beyond this are skipped
    /// (position snapped) so the visual never lags more than this many hops in
    /// fast-simulation / training mode.
    /// </summary>
    public const int MaxQueueDepth = 4;

    private UnitData unitData;
    private HexGrid grid;
    private float baseSpeed;

    // Visual waypoint queue: unit animates through positions in FIFO order.
    private readonly Queue<Vector3> moveQueue = new Queue<Vector3>();

    // ── Initialisation ────────────────────────────────────────────────────

    public void Initialize(HexGrid hexGrid)
    {
        unitData  = GetComponent<UnitData>();
        grid      = hexGrid;
        moveQueue.Clear();

        // Adjacent hex center distance for flat-top layout = outerRadius × √3.
        float hexDist = grid.outerRadius * Mathf.Sqrt(3f);
        baseSpeed = hexDist / (Time.fixedDeltaTime * AnimTickFraction);
    }

    // ── Visual animation (Update) ─────────────────────────────────────────

    private void Update()
    {
        if (moveQueue.Count == 0) return;

        float speed = baseSpeed;

        // Skip stale hops when simulation outruns the renderer (training mode).
        while (moveQueue.Count > MaxQueueDepth)
            transform.position = moveQueue.Dequeue();

        if (moveQueue.Count == 0) return;

        Vector3 target = moveQueue.Peek();
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        // Snap and dequeue when the hop is complete.
        if (Vector3.Distance(transform.position, target) < 0.001f)
        {
            transform.position = target;
            moveQueue.Dequeue();
        }
    }

    // ── Public movement API ───────────────────────────────────────────────

    /// <summary>
    /// Attempt to move one step in the given direction (0-5).
    /// Only succeeds if the target hex is empty (no unit of any team).
    /// Returns true if the move was executed.
    /// </summary>
    public bool TryMove(int direction)
    {
        if (!unitData.isAlive || grid == null) return false;
        if (direction < 0 || direction > 5)    return false;

        return TryMoveTo(unitData.currentHex.Neighbor(direction));
    }

    /// <summary>
    /// Attempt to move to a specific adjacent hex coordinate.
    /// Only succeeds if the target hex is empty (no unit of any team).
    /// Moving onto an enemy tile neutralizes it (Owner → None, TileType → Empty).
    /// Fortified enemy tiles resist: unit must stand adjacent for Fortification+1 turns first.
    /// Returns true if the move was executed.
    /// </summary>
    public bool TryMoveTo(HexCoord target)
    {
        if (!unitData.isAlive || grid == null)                     return false;
        if (!grid.IsValidCoord(target))                            return false;
        if (HexCoord.Distance(unitData.currentHex, target) != 1)  return false;
        if (IsOccupied(target))                                    return false;

        // Update logical state instantly (game logic reads currentHex).
        unitData.moveFrom   = unitData.currentHex;
        unitData.moveTo     = target;
        unitData.currentHex = target;
        unitData.lastAction = UnitAction.Move;

        // Neutralize enemy tile on arrival.
        // Fortified tiles resist: each move chips 1 fortification level.
        // Only neutralized once fortification reaches 0.
        var tile = grid.GetTile(target);
        if (tile != null && !tile.isBase && tile.Owner != Team.None && tile.Owner != unitData.team)
        {
            if (tile.Fortification > 0)
            {
                tile.Fortification--;
                // Tile weakened but stays enemy-owned.
            }
            else
            {
                tile.Owner         = Team.None;
                tile.TileType      = TileType.Empty;
                unitData.lastAction = UnitAction.Capture;
            }
        }

        // Enqueue visual hop — animation plays it out in sequence.
        moveQueue.Enqueue(grid.HexToWorld(target) + Vector3.up * 0.3f);

        // Notify arrow indicator.
        GetComponent<UnitActionIndicator>()?.OnMoveStarted(target);
        return true;
    }

    /// <summary>
    /// Attack the enemy unit on the adjacent hex in the given direction.
    /// Attacker stays in place; both units take 1 damage.
    /// Returns true if an attack was executed.
    /// </summary>
    public bool TryAttack(int direction)
    {
        if (!unitData.isAlive || grid == null) return false;
        if (direction < 0 || direction > 5)    return false;

        HexCoord targetCoord = unitData.currentHex.Neighbor(direction);
        UnitData enemy = FindEnemyAt(targetCoord);
        if (enemy == null) return false;

        // Attacker deals 2 damage, defender deals 1. Shield negates incoming damage to its holder.
        int damageToSelf  = unitData.hasShield ? 0 : 1;   // defender hits back for 1
        int damageToEnemy = enemy.hasShield    ? 0 : 2;    // attacker hits for 2

        unitData.Health -= damageToSelf;
        enemy.Health    -= damageToEnemy;

        unitData.lastAction = UnitAction.Attack;
        if (enemy.isAlive) enemy.lastAction = UnitAction.Attack;

        if (enemy.Health <= 0)
            enemy.Die(12);

        if (unitData.Health <= 0)
            unitData.Die(12);

        return true;
    }

    /// <summary>
    /// Build on the unit's current tile (neutral or own, non-base, empty).
    /// Cannot build on an enemy-owned tile.
    /// Robot → builds a Crate and claims the tile.
    /// Mutant → converts tile to Slime, claims it, and randomly spreads to neutral neighbors.
    /// Returns true if build succeeded.
    /// </summary>
    public bool TryBuild()
    {
        if (!unitData.isAlive || grid == null) return false;

        var tile  = grid.GetTile(unitData.currentHex);
        Team enemy = unitData.team == Team.Robot ? Team.Mutant : Team.Robot;
        if (tile == null || tile.isBase)   return false;
        if (tile.Owner    == enemy)        return false;  // can't build on enemy tile
        if (tile.TileType != TileType.Empty) return false;

        // Claim the tile for this team when building.
        tile.Owner = unitData.team;

        if (unitData.team == Team.Robot)
        {
            tile.TileType       = TileType.Crate;
            unitData.lastAction = UnitAction.BuildCrate;
        }
        else
        {
            tile.TileType       = TileType.Slime;
            unitData.lastAction = UnitAction.SpreadSlime;

            // Spread slime to adjacent neutral empty tiles.
            var neighbors = grid.GetNeighbors(unitData.currentHex);
            foreach (var neighbor in neighbors)
            {
                if (neighbor.isBase) continue;
                if (neighbor.Owner == Team.None && neighbor.TileType == TileType.Empty)
                {
                    if (Random.value < 0.12f)
                    {
                        neighbor.Owner    = Team.Mutant;
                        neighbor.TileType = TileType.Slime;
                    }
                }
            }
        }

        return true;
    }

    // ── Validity queries (used by HexAgent for action masking) ────────────

    /// <summary>Returns true if moving in this direction is valid (target hex is empty).</summary>
    public bool IsValidMove(int direction)
    {
        if (!unitData.isAlive || grid == null) return false;
        if (direction < 0 || direction > 5)    return false;

        HexCoord target = unitData.currentHex.Neighbor(direction);
        return grid.IsValidCoord(target) && !IsOccupied(target);
    }

    /// <summary>Returns true if attacking in this direction is valid (adjacent enemy present).</summary>
    public bool IsValidAttack(int direction)
    {
        if (!unitData.isAlive || grid == null) return false;
        if (direction < 0 || direction > 5)    return false;

        HexCoord target = unitData.currentHex.Neighbor(direction);
        return grid.IsValidCoord(target) && FindEnemyAt(target) != null;
    }

    /// <summary>Returns true if the build action is valid (neutral or own non-base empty tile).</summary>
    public bool IsValidBuild()
    {
        if (!unitData.isAlive || grid == null) return false;

        var  tile  = grid.GetTile(unitData.currentHex);
        Team enemy = unitData.team == Team.Robot ? Team.Mutant : Team.Robot;
        return tile != null && !tile.isBase
            && tile.Owner    != enemy           // neutral or own — not enemy
            && tile.TileType == TileType.Empty;
    }

    /// <summary>
    /// Place the unit at a hex instantly with no animation (spawn, respawn, editor).
    /// Clears any pending visual queue.
    /// </summary>
    public void PlaceAt(HexCoord coord)
    {
        if (unitData != null) unitData.currentHex = coord;
        Vector3 worldPos = grid.HexToWorld(coord) + Vector3.up * 0.3f;
        transform.position = worldPos;
        moveQueue.Clear();
    }

    /// <summary>How many hops are currently pending in the visual queue.</summary>
    public int QueueDepth => moveQueue.Count;

    // ── Private helpers ───────────────────────────────────────────────────

    /// <summary>Returns true if any alive unit (ally or enemy) occupies this hex.</summary>
    private bool IsOccupied(HexCoord coord)
    {
        var allUnits = FindObjectsByType<UnitData>(FindObjectsSortMode.None);
        foreach (var unit in allUnits)
        {
            if (unit == unitData) continue;
            if (!unit.isAlive)    continue;
            if (unit.currentHex == coord) return true;
        }
        return false;
    }

    /// <summary>Returns the enemy unit at the given coord, or null if none.</summary>
    private UnitData FindEnemyAt(HexCoord coord)
    {
        var allUnits = FindObjectsByType<UnitData>(FindObjectsSortMode.None);
        foreach (var unit in allUnits)
        {
            if (!unit.isAlive)             continue;
            if (unit.team == unitData.team) continue;
            if (unit.currentHex == coord)  return unit;
        }
        return null;
    }
}
