using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles discrete hex-to-hex movement, combat, and building for a unit.
///
/// Sequential turn model (v2):
///   TryMove(dir)        — moves to an adjacent hex. Blocked by walls, occupied hexes,
///                         and enemy base hexes. Captures non-own hex on entry.
///                         Robot entering enemy slime: mine (costs energy, slime destroyed).
///   TryAttack(dir)      — attacks in priority order: enemy unit > wall (any team's).
///                         Units on their own base are immune to attack.
///   TryBuild(dir)       — Robot: wall on adjacent own empty hex. Mutant: slime under self.
///   TryDestroyWall(dir) — destroys own wall on adjacent hex.
///
/// Visual position works through a Queue of waypoints so each hop animation
/// completes before the next one starts, regardless of tick rate.
/// </summary>
[RequireComponent(typeof(UnitData))]
public class HexMovement : MonoBehaviour
{
    public const float AnimTickFraction = 0.7f;
    public const int MaxQueueDepth = 4;

    private UnitData unitData;
    private HexGrid grid;
    private float baseSpeed;

    private readonly Queue<Vector3> moveQueue = new Queue<Vector3>();

    // ── Initialisation ────────────────────────────────────────────────────

    public void Initialize(HexGrid hexGrid)
    {
        unitData  = GetComponent<UnitData>();
        grid      = hexGrid;
        moveQueue.Clear();

        float hexDist = grid.outerRadius * Mathf.Sqrt(3f);
        baseSpeed = hexDist / (Time.fixedDeltaTime * AnimTickFraction);
    }

    // ── Visual animation (Update) ─────────────────────────────────────────

    private void Update()
    {
        // Dead units don't animate — clear any pending visual hops.
        if (unitData != null && !unitData.isAlive)
        {
            if (moveQueue.Count > 0) moveQueue.Clear();
            return;
        }

        if (moveQueue.Count == 0) return;

        float speed = baseSpeed;

        while (moveQueue.Count > MaxQueueDepth)
            transform.position = moveQueue.Dequeue();

        if (moveQueue.Count == 0) return;

        Vector3 target = moveQueue.Peek();
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) < 0.001f)
        {
            transform.position = target;
            moveQueue.Dequeue();
        }
    }

    // ── Movement ─────────────────────────────────────────────────────────

    /// <summary>
    /// Attempt to move one step in the given direction (0-5).
    /// Blocked by: walls (any team), occupied hexes, invalid coords.
    /// Any non-own hex (neutral or enemy) is captured on entry.
    /// Robot entering enemy slime: mine — costs energy, slime destroyed, hex becomes robot's.
    /// Returns true if the move was executed.
    /// </summary>
    public bool TryMove(int direction)
    {
        if (!unitData.isAlive || grid == null) return false;
        if (direction < 0 || direction > 5)    return false;

        return TryMoveTo(unitData.currentHex.Neighbor(direction));
    }

    public bool TryMoveTo(HexCoord target)
    {
        if (!unitData.isAlive || grid == null)                     return false;
        if (!grid.IsValidCoord(target))                            return false;
        if (HexCoord.Distance(unitData.currentHex, target) != 1)  return false;
        if (IsOccupied(target))                                    return false;

        var tile = grid.GetTile(target);
        if (tile == null) return false;

        // Walls block ALL movement (own and enemy).
        if (tile.TileType == TileType.Wall) return false;

        // Enemy base blocks entry — cannot walk onto opponent's base hexes.
        if (tile.isBase && tile.baseTeam != unitData.team) return false;

        Team enemyTeam = unitData.team == Team.Robot ? Team.Mutant : Team.Robot;

        // Robot entering enemy slime: mine — costs energy, slime destroyed.
        if (unitData.team == Team.Robot && tile.Owner == enemyTeam && tile.TileType == TileType.Slime)
        {
            var cfg = GameConfig.Instance;
            int cost = cfg != null ? cfg.slimeEntryCostRobot : 3;
            if (unitData.Energy < cost) return false;
            unitData.Energy -= cost;
            tile.TileType = TileType.Empty;
        }

        // Update logical state.
        unitData.moveFrom   = unitData.currentHex;
        unitData.moveTo     = target;
        unitData.currentHex = target;
        unitData.lastAction = UnitAction.Move;

        // Capture any non-own, non-base hex on entry.
        if (!tile.isBase && tile.Owner != unitData.team)
        {
            tile.Owner = unitData.team;
            unitData.lastAction = UnitAction.Capture;
            unitData.lastCapturedHex = target;
        }

        // Enqueue visual hop.
        moveQueue.Enqueue(grid.HexToWorld(target) + Vector3.up * 0.3f);

        GetComponent<UnitActionIndicator3D>()?.OnMoveStarted(target);
        return true;
    }

    // ── Attack ───────────────────────────────────────────────────────────

    /// <summary>
    /// Attack the adjacent hex in the given direction.
    /// Valid targets: enemy unit, wall (any team's).
    /// Returns true if an attack was executed.
    /// </summary>
    public bool TryAttack(int direction)
    {
        if (!unitData.isAlive || grid == null) return false;
        if (direction < 0 || direction > 5)    return false;

        HexCoord targetCoord = unitData.currentHex.Neighbor(direction);
        if (!grid.IsValidCoord(targetCoord)) return false;

        var cfg = GameConfig.Instance;

        // Track which hex the attack targets (for replay display).
        unitData.lastAttackHex = targetCoord;

        // Priority 1: Attack enemy unit.
        UnitData enemy = FindEnemyAt(targetCoord);
        if (enemy != null)
            return AttackUnit(enemy, cfg);

        // Priority 2: Attack wall (any team's wall).
        var tile = grid.GetTile(targetCoord);
        if (tile != null && tile.TileType == TileType.Wall)
            return AttackWall(tile, cfg);

        return false;
    }

    private bool AttackUnit(UnitData enemy, GameConfig cfg)
    {
        int attackCost = cfg != null ? cfg.attackUnitCost : 3;
        int baseDamage = cfg != null ? cfg.attackUnitDamage : 3;

        if (unitData.Energy < attackCost) return false;

        // Shield Wall (Robot defender): -1 damage per adjacent robot ally.
        int damageReduction = 0;
        if (enemy.team == Team.Robot)
        {
            int maxReduction = cfg != null ? cfg.shieldWallMaxReduction : 3;
            damageReduction = Mathf.Min(CountAdjacentAllies(enemy), maxReduction);
        }

        // Swarm (Mutant attacker): +1 damage per adjacent mutant ally.
        int damageBonus = 0;
        if (unitData.team == Team.Mutant)
        {
            int maxBonus = cfg != null ? cfg.swarmMaxBonus : 3;
            damageBonus = Mathf.Min(CountAdjacentAllies(unitData), maxBonus);
        }

        int finalDamage = Mathf.Max(0, baseDamage + damageBonus - damageReduction);

        unitData.Energy -= attackCost;
        enemy.Energy    -= finalDamage;

        unitData.lastAction = UnitAction.Attack;
        unitData.lastAttackTarget = enemy;
        unitData.lastAttackWallHP = -1; // not a wall attack

        int respawnCD = cfg != null ? cfg.respawnCooldown : 6;

        bool killed = enemy.Energy <= 0;
        if (killed)
            enemy.Die(respawnCD);

        unitData.lastAttackKilled = killed;

        if (unitData.Energy <= 0)
            unitData.Die(respawnCD);

        return true;
    }

    private bool AttackWall(HexTileData tile, GameConfig cfg)
    {
        int cost   = cfg != null ? cfg.attackWallCost : 2;
        int damage = cfg != null ? cfg.attackWallDamage : 1;

        if (unitData.Energy < cost) return false;

        unitData.Energy -= cost;
        tile.WallHP -= damage;

        if (tile.WallHP <= 0)
        {
            tile.TileType = TileType.Empty;
            tile.WallHP = 0;
        }

        unitData.lastAction = UnitAction.Attack;
        unitData.lastAttackTarget = null;
        unitData.lastAttackKilled = false;
        unitData.lastAttackWallHP = tile.WallHP;
        return true;
    }


    // ── Build ────────────────────────────────────────────────────────────

    /// <summary>
    /// Build on the adjacent hex in the given direction.
    /// Target must be own team, non-base, empty tile. Costs energy.
    /// Robot → Wall (wallBuildCost energy, sets WallHP).
    /// Mutant → Slime (slimePlaceCost energy).
    /// Returns true if build succeeded.
    /// </summary>
    public bool TryBuild(int direction)
    {
        if (!unitData.isAlive || grid == null) return false;
        if (direction < 0 || direction > 5)    return false;

        var cfg = GameConfig.Instance;

        // Mutant places slime on own current hex (not adjacent).
        if (unitData.team == Team.Mutant)
        {
            var tile = grid.GetTile(unitData.currentHex);
            if (tile == null || tile.isBase)     return false;
            if (tile.Owner != unitData.team)     return false;
            if (tile.TileType != TileType.Empty) return false;

            int cost = cfg != null ? cfg.slimePlaceCost : 2;
            if (unitData.Energy < cost) return false;
            unitData.Energy -= cost;
            tile.TileType = TileType.Slime;
            unitData.lastAction = UnitAction.PlaceSlime;
            unitData.lastBuildTarget = unitData.currentHex;
            return true;
        }

        // Robot builds wall on adjacent hex.
        HexCoord targetCoord = unitData.currentHex.Neighbor(direction);
        var wallTile = grid.GetTile(targetCoord);
        if (wallTile == null || wallTile.isBase)     return false;
        if (wallTile.Owner != unitData.team)         return false;
        if (wallTile.TileType != TileType.Empty)     return false;
        if (IsOccupied(targetCoord))                 return false;

        int wallCost = cfg != null ? cfg.wallBuildCost : 4;
        if (unitData.Energy < wallCost) return false;
        unitData.Energy -= wallCost;
        wallTile.TileType = TileType.Wall;
        wallTile.WallHP = cfg != null ? cfg.wallMaxHP : 3;
        unitData.lastAction = UnitAction.BuildWall;
        unitData.lastBuildTarget = targetCoord;
        return true;
    }

    /// <summary>Legacy no-arg build (builds on own tile). Kept for test compatibility.</summary>
    public bool TryBuild()
    {
        if (!unitData.isAlive || grid == null) return false;

        // Try all 6 directions, pick first valid one.
        for (int dir = 0; dir < 6; dir++)
        {
            if (IsValidBuild(dir))
                return TryBuild(dir);
        }
        return false;
    }

    // ── Destroy Wall ─────────────────────────────────────────────────────

    /// <summary>
    /// Destroy own wall on the adjacent hex in the given direction.
    /// Costs destroyOwnWallCost energy. Returns true if destroyed.
    /// </summary>
    public bool TryDestroyWall(int direction)
    {
        if (!unitData.isAlive || grid == null) return false;
        if (direction < 0 || direction > 5)    return false;

        HexCoord target = unitData.currentHex.Neighbor(direction);
        var tile = grid.GetTile(target);
        if (tile == null || tile.TileType != TileType.Wall || tile.Owner != unitData.team)
            return false;

        var cfg = GameConfig.Instance;
        int cost = cfg != null ? cfg.destroyOwnWallCost : 1;
        if (unitData.Energy < cost) return false;

        unitData.Energy -= cost;
        tile.TileType = TileType.Empty;
        tile.WallHP = 0;
        unitData.lastAction = UnitAction.DestroyWall;
        unitData.lastBuildTarget = target; // reuse for "target of structure action"

        return true;
    }

    // ── Validity queries (used by HexAgent for action masking) ────────────

    /// <summary>
    /// Returns true if moving in this direction is valid.
    /// Blocked only by: walls, occupied hexes, invalid coords.
    /// Robot entering enemy slime requires enough energy.
    /// </summary>
    public bool IsValidMove(int direction)
    {
        if (!unitData.isAlive || grid == null) return false;
        if (direction < 0 || direction > 5)    return false;

        HexCoord target = unitData.currentHex.Neighbor(direction);
        if (!grid.IsValidCoord(target) || IsOccupied(target)) return false;

        var tile = grid.GetTile(target);
        if (tile == null) return false;
        if (tile.TileType == TileType.Wall) return false;

        // Enemy base blocks entry.
        if (tile.isBase && tile.baseTeam != unitData.team) return false;

        // Robot entering enemy slime requires enough energy.
        Team enemyTeam = unitData.team == Team.Robot ? Team.Mutant : Team.Robot;
        if (unitData.team == Team.Robot && tile.Owner == enemyTeam && tile.TileType == TileType.Slime)
        {
            var cfg = GameConfig.Instance;
            int cost = cfg != null ? cfg.slimeEntryCostRobot : 3;
            return unitData.Energy >= cost;
        }

        return true;
    }

    /// <summary>
    /// Returns true if attacking in this direction is valid.
    /// Valid targets: enemy unit, wall (any team's).
    /// </summary>
    public bool IsValidAttack(int direction)
    {
        if (!unitData.isAlive || grid == null) return false;
        if (direction < 0 || direction > 5)    return false;

        HexCoord target = unitData.currentHex.Neighbor(direction);
        if (!grid.IsValidCoord(target)) return false;

        var cfg = GameConfig.Instance;

        // Enemy unit?
        if (FindEnemyAt(target) != null)
            return unitData.Energy >= (cfg != null ? cfg.attackUnitCost : 3);

        // Wall (any team's)?
        var tile = grid.GetTile(target);
        if (tile != null && tile.TileType == TileType.Wall)
            return unitData.Energy >= (cfg != null ? cfg.attackWallCost : 2);

        return false;
    }

    /// <summary>Returns true if the build action is valid in the given direction.</summary>
    public bool IsValidBuild(int direction)
    {
        if (!unitData.isAlive || grid == null) return false;
        if (direction < 0 || direction > 5)    return false;

        var cfg = GameConfig.Instance;

        // Mutant: build slime on current hex (direction ignored for validation).
        if (unitData.team == Team.Mutant)
        {
            var tile = grid.GetTile(unitData.currentHex);
            if (tile == null || tile.isBase)     return false;
            if (tile.Owner != unitData.team)     return false;
            if (tile.TileType != TileType.Empty) return false;

            int cost = cfg != null ? cfg.slimePlaceCost : 2;
            return unitData.Energy >= cost;
        }

        // Robot: build wall on adjacent hex.
        HexCoord target = unitData.currentHex.Neighbor(direction);
        var tile2 = grid.GetTile(target);
        if (tile2 == null || tile2.isBase)     return false;
        if (tile2.Owner != unitData.team)      return false;
        if (tile2.TileType != TileType.Empty)  return false;
        if (IsOccupied(target))                return false;

        int wallCost = cfg != null ? cfg.wallBuildCost : 4;
        return unitData.Energy >= wallCost;
    }

    /// <summary>Legacy no-arg IsValidBuild. Returns true if any direction is valid.</summary>
    public bool IsValidBuild()
    {
        for (int dir = 0; dir < 6; dir++)
        {
            if (IsValidBuild(dir)) return true;
        }
        return false;
    }

    /// <summary>Returns true if there is an own wall at the adjacent hex in the given direction.</summary>
    public bool IsValidDestroyWall(int direction)
    {
        if (!unitData.isAlive || grid == null) return false;
        if (direction < 0 || direction > 5)    return false;

        HexCoord target = unitData.currentHex.Neighbor(direction);
        var tile = grid.GetTile(target);
        if (tile == null || tile.TileType != TileType.Wall || tile.Owner != unitData.team)
            return false;

        var cfg = GameConfig.Instance;
        int cost = cfg != null ? cfg.destroyOwnWallCost : 1;
        return unitData.Energy >= cost;
    }

    // ── Placement ────────────────────────────────────────────────────────

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

    private bool IsOccupied(HexCoord coord)
    {
        foreach (var unit in UnitCache.GetAll())
        {
            if (unit == unitData) continue;
            // Dead units on base still block the hex (waiting for respawn).
            if (!unit.gameObject.activeInHierarchy) continue;
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

    private UnitData FindEnemyAt(HexCoord coord)
    {
        foreach (var unit in UnitCache.GetAll())
        {
            if (!unit.isAlive)             continue;
            if (unit.team == unitData.team) continue;
            if (unit.currentHex == coord)
            {
                // Units on their own base are immune to attack.
                var tile = grid.GetTile(coord);
                if (tile != null && tile.isBase && tile.baseTeam == unit.team)
                    return null;
                return unit;
            }
        }
        return null;
    }
}
