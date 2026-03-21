using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles discrete hex-to-hex movement, combat, and building for a unit.
///
/// Sequential turn model (v2):
///   TryMove(dir)        — moves to an adjacent hex. Blocked by enemy territory,
///                         walls, and occupied hexes. Free capture of neutral hexes.
///   TryAttack(dir)      — attacks in priority order: unit > wall > enemy hex > neutral hex.
///   TryBuild(dir)       — builds Wall (Robot) or places Slime (Mutant) on adjacent own empty hex.
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
    /// Blocked by: enemy territory, walls (any team), occupied hexes, invalid coords.
    /// Neutral hex: free capture on entry.
    /// Robot entering enemy slime: costs slimeEntryCostRobot, slime destroyed.
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

        Team enemyTeam = unitData.team == Team.Robot ? Team.Mutant : Team.Robot;

        // Enemy territory blocks movement (except slime entry for robots handled below).
        if (!tile.isBase && tile.Owner == enemyTeam && tile.TileType != TileType.Slime)
            return false;

        // Robot entering enemy slime: costs energy and destroys slime.
        if (unitData.team == Team.Robot && tile.Owner == enemyTeam && tile.TileType == TileType.Slime)
        {
            var cfg = GameConfig.Instance;
            int cost = cfg != null ? cfg.slimeEntryCostRobot : 3;
            if (unitData.Energy < cost) return false;
            unitData.Energy -= cost;
            tile.TileType = TileType.Empty;
            tile.Owner = Team.None;
        }

        // Update logical state.
        unitData.moveFrom   = unitData.currentHex;
        unitData.moveTo     = target;
        unitData.currentHex = target;
        unitData.lastAction = UnitAction.Move;

        // Free capture of neutral hex on entry.
        if (!tile.isBase && tile.Owner == Team.None)
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
    /// Priority: enemy unit > wall > enemy hex > neutral hex.
    /// Returns true if an attack was executed.
    /// </summary>
    public bool TryAttack(int direction)
    {
        if (!unitData.isAlive || grid == null) return false;
        if (direction < 0 || direction > 5)    return false;

        HexCoord targetCoord = unitData.currentHex.Neighbor(direction);
        if (!grid.IsValidCoord(targetCoord)) return false;

        var cfg = GameConfig.Instance;

        // Priority 1: Attack enemy unit.
        UnitData enemy = FindEnemyAt(targetCoord);
        if (enemy != null)
            return AttackUnit(enemy, cfg);

        // Priority 2: Attack wall (any team's wall).
        var tile = grid.GetTile(targetCoord);
        if (tile != null && tile.TileType == TileType.Wall)
            return AttackWall(tile, cfg);

        // Priority 3: Attack enemy hex (flip ownership).
        if (tile != null && !tile.isBase && tile.Owner != Team.None && tile.Owner != unitData.team)
            return AttackEnemyHex(tile, cfg);

        // Priority 4: Attack neutral hex (claim it).
        if (tile != null && !tile.isBase && tile.Owner == Team.None)
            return AttackNeutralHex(tile, cfg);

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
        return true;
    }

    private bool AttackEnemyHex(HexTileData tile, GameConfig cfg)
    {
        int cost = cfg != null ? cfg.attackEnemyHexCost : 2;
        if (unitData.Energy < cost) return false;

        unitData.Energy -= cost;
        tile.Owner = unitData.team;
        tile.TileType = TileType.Empty;
        tile.WallHP = 0;

        unitData.lastAction = UnitAction.Capture;
        unitData.lastCapturedHex = tile.coord;
        unitData.lastAttackTarget = null;
        unitData.lastAttackKilled = false;
        return true;
    }

    private bool AttackNeutralHex(HexTileData tile, GameConfig cfg)
    {
        int cost = cfg != null ? cfg.attackNeutralCost : 1;
        if (unitData.Energy < cost) return false;

        unitData.Energy -= cost;
        tile.Owner = unitData.team;

        unitData.lastAction = UnitAction.Capture;
        unitData.lastCapturedHex = tile.coord;
        unitData.lastAttackTarget = null;
        unitData.lastAttackKilled = false;
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

        return true;
    }

    // ── Validity queries (used by HexAgent for action masking) ────────────

    /// <summary>Returns true if moving in this direction is valid.</summary>
    public bool IsValidMove(int direction)
    {
        if (!unitData.isAlive || grid == null) return false;
        if (direction < 0 || direction > 5)    return false;

        HexCoord target = unitData.currentHex.Neighbor(direction);
        if (!grid.IsValidCoord(target) || IsOccupied(target)) return false;

        var tile = grid.GetTile(target);
        if (tile == null) return false;
        if (tile.TileType == TileType.Wall) return false;

        Team enemyTeam = unitData.team == Team.Robot ? Team.Mutant : Team.Robot;

        // Enemy territory blocks (except slime for robots if enough energy).
        if (!tile.isBase && tile.Owner == enemyTeam)
        {
            if (tile.TileType == TileType.Slime && unitData.team == Team.Robot)
            {
                var cfg = GameConfig.Instance;
                int cost = cfg != null ? cfg.slimeEntryCostRobot : 3;
                return unitData.Energy >= cost;
            }
            return false;
        }

        return true;
    }

    /// <summary>Returns true if attacking in this direction is valid (any attackable target).</summary>
    public bool IsValidAttack(int direction)
    {
        if (!unitData.isAlive || grid == null) return false;
        if (direction < 0 || direction > 5)    return false;

        HexCoord target = unitData.currentHex.Neighbor(direction);
        if (!grid.IsValidCoord(target)) return false;

        // Enemy unit present?
        if (FindEnemyAt(target) != null)
        {
            var cfg = GameConfig.Instance;
            return unitData.Energy >= (cfg != null ? cfg.attackUnitCost : 3);
        }

        var tile = grid.GetTile(target);
        if (tile == null) return false;

        // Wall?
        if (tile.TileType == TileType.Wall)
        {
            var cfg = GameConfig.Instance;
            return unitData.Energy >= (cfg != null ? cfg.attackWallCost : 2);
        }

        // Enemy hex?
        if (!tile.isBase && tile.Owner != Team.None && tile.Owner != unitData.team)
        {
            var cfg = GameConfig.Instance;
            return unitData.Energy >= (cfg != null ? cfg.attackEnemyHexCost : 2);
        }

        // Neutral hex?
        if (!tile.isBase && tile.Owner == Team.None)
        {
            var cfg = GameConfig.Instance;
            return unitData.Energy >= (cfg != null ? cfg.attackNeutralCost : 1);
        }

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
