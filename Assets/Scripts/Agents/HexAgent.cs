using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// ML-Agents agent for hex territory control.
/// Observations: own state + 6 neighbor tiles + global scores.
/// Actions: 7 discrete (stay, move direction 0-5).
/// </summary>
[RequireComponent(typeof(UnitData), typeof(HexMovement))]
public class HexAgent : Agent
{
    private UnitData unitData;
    private HexMovement movement;
    private HexGrid grid;

    // Cached previous state for reward shaping.
    private int prevTeamTiles;
    private int prevEnemyTiles;

    public override void Initialize()
    {
        unitData = GetComponent<UnitData>();
        movement = GetComponent<HexMovement>();
        grid = FindFirstObjectByType<HexGrid>();
        movement.Initialize(grid);
    }

    public override void OnEpisodeBegin()
    {
        prevTeamTiles = 0;
        prevEnemyTiles = 0;
    }

    /// <summary>
    /// Observation vector (~55 floats):
    /// - Own position (2), health (1), alive (1), team (1) = 5
    /// - 6 neighbors × 8 values each = 48
    /// - Global: own tiles (1), enemy tiles (1), step progress (1) = 3
    /// Total = 56
    /// </summary>
    public override void CollectObservations(VectorSensor sensor)
    {
        if (grid == null || unitData == null)
        {
            // Pad with zeros if not ready.
            for (int i = 0; i < 56; i++) sensor.AddObservation(0f);
            return;
        }

        int boardMax = grid.boardSide - 1;
        float boardRange = boardMax > 0 ? boardMax : 1f;

        // Own state.
        sensor.AddObservation(unitData.currentHex.q / boardRange);   // normalized q
        sensor.AddObservation(unitData.currentHex.r / boardRange);   // normalized r
        sensor.AddObservation(unitData.Health / (float)unitData.maxHealth);
        sensor.AddObservation(unitData.isAlive ? 1f : 0f);
        sensor.AddObservation(unitData.team == Team.Robot ? 1f : -1f);

        // 6 neighbor observations.
        for (int dir = 0; dir < 6; dir++)
        {
            var neighborCoord = unitData.currentHex.Neighbor(dir);
            var neighborTile = grid.GetTile(neighborCoord);

            if (neighborTile == null)
            {
                // Off-board: 8 zeros.
                for (int j = 0; j < 8; j++) sensor.AddObservation(0f);
                continue;
            }

            // Ownership one-hot (3): None, Same team, Enemy team.
            bool isOwn = neighborTile.Owner == unitData.team;
            bool isEnemy = neighborTile.Owner != Team.None && !isOwn;
            sensor.AddObservation(neighborTile.Owner == Team.None ? 1f : 0f);
            sensor.AddObservation(isOwn ? 1f : 0f);
            sensor.AddObservation(isEnemy ? 1f : 0f);

            // Tile type one-hot (3): Empty, Crate, Slime.
            sensor.AddObservation(neighborTile.TileType == TileType.Empty ? 1f : 0f);
            sensor.AddObservation(neighborTile.TileType == TileType.Crate ? 1f : 0f);
            sensor.AddObservation(neighborTile.TileType == TileType.Slime ? 1f : 0f);

            // Has enemy unit on this tile (1).
            sensor.AddObservation(HasEnemyUnit(neighborCoord) ? 1f : 0f);

            // Has allied unit on this tile (1).
            sensor.AddObservation(HasAllyUnit(neighborCoord) ? 1f : 0f);
        }

        // Global state.
        int ownTiles = CountTiles(unitData.team);
        Team enemyTeam = unitData.team == Team.Robot ? Team.Mutant : Team.Robot;
        int enemyTiles = CountTiles(enemyTeam);
        int totalContestable = GetContestableTileCount();
        float totalF = totalContestable > 0 ? totalContestable : 1f;

        sensor.AddObservation(ownTiles / totalF);
        sensor.AddObservation(enemyTiles / totalF);

        // Step progress: use Academy step count as proxy (avoids Game assembly dependency).
        float stepProgress = Mathf.Clamp01(Academy.Instance.StepCount / 2000f);
        sensor.AddObservation(stepProgress);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!unitData.isAlive) return;

        int action = actions.DiscreteActions[0];

        // 0 = stay, 1-6 = move direction 0-5.
        unitData.lastAction = UnitAction.Idle;
        if (action > 0)
            movement.TryMove(action - 1);

        // Reward shaping.
        int ownTiles = CountTiles(unitData.team);
        Team enemyTeam = unitData.team == Team.Robot ? Team.Mutant : Team.Robot;
        int enemyTiles = CountTiles(enemyTeam);

        // Reward for gaining territory.
        int tilesGained = ownTiles - prevTeamTiles;
        if (tilesGained > 0)
            AddReward(0.1f * tilesGained);

        // Reward for taking enemy territory.
        int enemyLost = prevEnemyTiles - enemyTiles;
        if (enemyLost > 0)
            AddReward(0.1f * enemyLost);

        // Small step penalty to encourage action.
        AddReward(-0.001f);

        prevTeamTiles = ownTiles;
        prevEnemyTiles = enemyTiles;
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (!unitData.isAlive || grid == null)
        {
            // Mask everything except stay.
            for (int i = 1; i <= 6; i++)
                actionMask.SetActionEnabled(0, i, false);
            return;
        }

        for (int dir = 0; dir < 6; dir++)
        {
            if (!movement.IsValidMove(dir))
                actionMask.SetActionEnabled(0, dir + 1, false);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var da = actionsOut.DiscreteActions;
        // Random valid action for testing.
        da[0] = Random.Range(0, 7);
    }

    private bool HasEnemyUnit(HexCoord coord)
    {
        var allUnits = FindObjectsByType<UnitData>(FindObjectsSortMode.None);
        foreach (var u in allUnits)
        {
            if (!u.isAlive) continue;
            if (u.team != unitData.team && u.currentHex == coord)
                return true;
        }
        return false;
    }

    private bool HasAllyUnit(HexCoord coord)
    {
        var allUnits = FindObjectsByType<UnitData>(FindObjectsSortMode.None);
        foreach (var u in allUnits)
        {
            if (u == unitData || !u.isAlive) continue;
            if (u.team == unitData.team && u.currentHex == coord)
                return true;
        }
        return false;
    }

    private int CountTiles(Team team)
    {
        int count = 0;
        foreach (var tile in grid.Tiles.Values)
        {
            if (!tile.isBase && tile.Owner == team)
                count++;
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
