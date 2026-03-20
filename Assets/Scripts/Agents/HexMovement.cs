using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles discrete hex-to-hex movement, combat, and building for a unit.
///
/// Sequential turn model:
///   TryMove   — moves to an empty adjacent hex (no unit of any team there).
///   TryAttack — fights the enemy on an adjacent hex; attacker stays in place.
///   TryBuild  — builds a Crate (Robot) or places Slime (Mutant) on the
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
        GetComponent<UnitActionIndicator3D>()?.OnMoveStarted(target);
        return true;
    }

    /// <summary>
    /// Attack the enemy unit on the adjacent hex in the given direction.
    /// Attacker stays in place; both units take damage.
    /// Robot Flanking: each adjacent ally gives robotFlankingChancePerAlly chance of double damage (max 3 allies).
    /// Mutant Swarm Cover: each adjacent ally of defender gives mutantDodgeChancePerAlly chance to dodge (max 3 allies).
    /// Returns true if an attack was executed.
    /// </summary>
    public bool TryAttack(int direction)
    {
        if (!unitData.isAlive || grid == null) return false;
        if (direction < 0 || direction > 5)    return false;

        HexCoord targetCoord = unitData.currentHex.Neighbor(direction);
        UnitData enemy = FindEnemyAt(targetCoord);
        if (enemy == null) return false;

        var cfg = GameConfig.Instance;

        // Base damage: attacker deals 2, defender hits back for 1. Shield negates.
        int damageToSelf  = unitData.hasShield ? 0 : 1;
        int damageToEnemy = enemy.hasShield    ? 0 : 2;

        // Robot Flanking: adjacent allies give chance of double damage.
        if (damageToEnemy > 0 && unitData.team == Team.Robot && cfg != null)
        {
            int allies = CountAdjacentAllies(unitData);
            float flankChance = allies * cfg.robotFlankingChancePerAlly;
            if (Random.value < flankChance)
                damageToEnemy *= 2;
        }

        // Mutant Swarm Cover: defender's adjacent allies give chance to dodge.
        if (damageToEnemy > 0 && enemy.team == Team.Mutant && cfg != null)
        {
            int defenderAllies = CountAdjacentAllies(enemy);
            float dodgeChance = defenderAllies * cfg.mutantDodgeChancePerAlly;
            if (Random.value < dodgeChance)
                damageToEnemy = 0;
        }

        unitData.Health -= damageToSelf;
        enemy.Health    -= damageToEnemy;

        unitData.lastAction = UnitAction.Attack;
        unitData.lastAttackTarget = enemy;
        if (enemy.isAlive) enemy.lastAction = UnitAction.Defend;

        bool killed = enemy.Health <= 0;
        if (killed)
            enemy.Die(12);

        unitData.lastAttackKilled = killed;

        if (unitData.Health <= 0)
            unitData.Die(12);

        return true;
    }

    /// <summary>
    /// Build on the unit's current tile (neutral or own, non-base, empty).
    /// Cannot build on an enemy-owned tile.
    /// Robot → builds a Crate and claims the tile.
    /// Mutant → converts tile to Slime and claims it.
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
        foreach (var unit in UnitCache.GetAll())
        {
            if (unit == unitData) continue;
            if (!unit.isAlive)    continue;
            if (unit.currentHex == coord) return true;
        }
        return false;
    }

    /// <summary>Count alive allies adjacent to unit (same team, max 3).</summary>
    private int CountAdjacentAllies(UnitData unit)
    {
        int count = 0;
        for (int i = 0; i < 6; i++)
        {
            HexCoord neighbor = unit.currentHex.Neighbor(i);
            foreach (var other in UnitCache.GetAll())
            {
                if (other == unit || !other.isAlive) continue;
                if (other.team != unit.team) continue;
                if (other.currentHex == neighbor)
                {
                    count++;
                    if (count >= 3) return 3;
                    break;
                }
            }
        }
        return count;
    }

    /// <summary>Returns the enemy unit at the given coord, or null if none.</summary>
    private UnitData FindEnemyAt(HexCoord coord)
    {
        foreach (var unit in UnitCache.GetAll())
        {
            if (!unit.isAlive)             continue;
            if (unit.team == unitData.team) continue;
            if (unit.currentHex == coord)  return unit;
        }
        return null;
    }
}
