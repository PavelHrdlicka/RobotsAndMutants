using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// ML-Agents agent for hex territory control — sequential turn model.
///
/// Observations: own state + 6 neighbor tiles + global scores = 56 floats.
///
/// Actions (8 discrete):
///   0       = idle / stay
///   1 – 6   = direction 0-5:
///               • if enemy adjacent → attack (stay in place, deal damage)
///               • if hex empty      → move there
///   7       = build (Robot: crate; Mutant: slime + spread)
///
/// After OnActionReceived the agent sets hasPendingTurnResult = true so
/// GameManager knows this unit's turn is complete and can advance.
/// </summary>
[RequireComponent(typeof(UnitData), typeof(HexMovement))]
public class HexAgent : Agent
{
    private UnitData unitData;
    private HexMovement movement;
    private HexGrid grid;

    // Cached previous tile counts for reward shaping.
    private int prevTeamTiles;
    private int prevEnemyTiles;

    public override void Initialize()
    {
        unitData = GetComponent<UnitData>();
        movement = GetComponent<HexMovement>();
        grid     = FindFirstObjectByType<HexGrid>();
        movement.Initialize(grid);
    }

    public override void OnEpisodeBegin()
    {
        prevTeamTiles  = 0;
        prevEnemyTiles = 0;
    }

    /// <summary>
    /// Observation vector (56 floats):
    /// - Own position (2), health (1), alive (1), team (1) = 5
    /// - 6 neighbors × 8 values each = 48
    /// - Global: own tiles (1), enemy tiles (1), step progress (1) = 3
    /// Total = 56
    /// </summary>
    public override void CollectObservations(VectorSensor sensor)
    {
        if (grid == null || unitData == null)
        {
            for (int i = 0; i < 56; i++) sensor.AddObservation(0f);
            return;
        }

        int   boardMax   = grid.boardSide - 1;
        float boardRange = boardMax > 0 ? boardMax : 1f;

        // Own state.
        sensor.AddObservation(unitData.currentHex.q / boardRange);
        sensor.AddObservation(unitData.currentHex.r / boardRange);
        sensor.AddObservation(unitData.Health / (float)unitData.maxHealth);
        sensor.AddObservation(unitData.isAlive ? 1f : 0f);
        sensor.AddObservation(unitData.team == Team.Robot ? 1f : -1f);

        // 6 neighbour observations.
        for (int dir = 0; dir < 6; dir++)
        {
            var neighborCoord = unitData.currentHex.Neighbor(dir);
            var neighborTile  = grid.GetTile(neighborCoord);

            if (neighborTile == null)
            {
                for (int j = 0; j < 8; j++) sensor.AddObservation(0f);
                continue;
            }

            bool isOwn   = neighborTile.Owner == unitData.team;
            bool isEnemy = neighborTile.Owner != Team.None && !isOwn;
            sensor.AddObservation(neighborTile.Owner == Team.None ? 1f : 0f);
            sensor.AddObservation(isOwn   ? 1f : 0f);
            sensor.AddObservation(isEnemy ? 1f : 0f);

            sensor.AddObservation(neighborTile.TileType == TileType.Empty ? 1f : 0f);
            sensor.AddObservation(neighborTile.TileType == TileType.Crate ? 1f : 0f);
            sensor.AddObservation(neighborTile.TileType == TileType.Slime ? 1f : 0f);

            sensor.AddObservation(HasEnemyUnit(neighborCoord) ? 1f : 0f);
            sensor.AddObservation(HasAllyUnit(neighborCoord)  ? 1f : 0f);
        }

        // Global state.
        int  ownTiles          = CountTiles(unitData.team);
        Team enemyTeam         = unitData.team == Team.Robot ? Team.Mutant : Team.Robot;
        int  enemyTiles        = CountTiles(enemyTeam);
        int  totalContestable  = GetContestableTileCount();
        float totalF           = totalContestable > 0 ? totalContestable : 1f;

        sensor.AddObservation(ownTiles   / totalF);
        sensor.AddObservation(enemyTiles / totalF);

        float stepProgress = Mathf.Clamp01(Academy.Instance.StepCount / 2000f);
        sensor.AddObservation(stepProgress);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Always signal turn complete (even if dead — GameManager must advance).
        unitData.lastAction = UnitAction.Idle;

        if (unitData.isAlive)
        {
            int action = actions.DiscreteActions[0];

            if (action >= 1 && action <= 6)
            {
                int dir = action - 1;
                // Attack takes priority; fall back to move if no enemy there.
                if (!movement.TryAttack(dir))
                    movement.TryMove(dir);
            }
            else if (action == 7)
            {
                movement.TryBuild();
            }

            // Reward shaping.
            int ownTiles   = CountTiles(unitData.team);
            Team enemyTeam = unitData.team == Team.Robot ? Team.Mutant : Team.Robot;
            int enemyTiles = CountTiles(enemyTeam);

            int tilesGained = ownTiles - prevTeamTiles;
            if (tilesGained > 0) AddReward(0.1f * tilesGained);

            int enemyLost = prevEnemyTiles - enemyTiles;
            if (enemyLost > 0) AddReward(0.1f * enemyLost);

            AddReward(-0.001f);

            prevTeamTiles  = ownTiles;
            prevEnemyTiles = enemyTiles;
        }

        // Signal to GameManager that this unit's turn is done.
        unitData.hasPendingTurnResult = true;
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (!unitData.isAlive || grid == null)
        {
            for (int i = 1; i <= 7; i++)
                actionMask.SetActionEnabled(0, i, false);
            return;
        }

        // Directions 1-6: valid if can move OR can attack in that direction.
        for (int dir = 0; dir < 6; dir++)
        {
            bool valid = movement.IsValidMove(dir) || movement.IsValidAttack(dir);
            if (!valid)
                actionMask.SetActionEnabled(0, dir + 1, false);
        }

        // Action 7 (build): valid only if on own empty non-base tile.
        if (!movement.IsValidBuild())
            actionMask.SetActionEnabled(0, 7, false);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        actionsOut.DiscreteActions.Array[0] = Random.Range(0, 8);
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private bool HasEnemyUnit(HexCoord coord)
    {
        var allUnits = FindObjectsByType<UnitData>(FindObjectsSortMode.None);
        foreach (var u in allUnits)
        {
            if (!u.isAlive) continue;
            if (u.team != unitData.team && u.currentHex == coord) return true;
        }
        return false;
    }

    private bool HasAllyUnit(HexCoord coord)
    {
        var allUnits = FindObjectsByType<UnitData>(FindObjectsSortMode.None);
        foreach (var u in allUnits)
        {
            if (u == unitData || !u.isAlive) continue;
            if (u.team == unitData.team && u.currentHex == coord) return true;
        }
        return false;
    }

    private int CountTiles(Team team)
    {
        int count = 0;
        foreach (var tile in grid.Tiles.Values)
        {
            if (!tile.isBase && tile.Owner == team) count++;
        }
        return count;
    }

    private int GetContestableTileCount()
    {
        int count = 0;
        foreach (var tile in grid.Tiles.Values)
        {
            if (!tile.isBase) count++;
        }
        return count;
    }
}
