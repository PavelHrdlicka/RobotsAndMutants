using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;
using UnityEngine;

/// <summary>
/// ML-Agents agent for hex territory control — sequential turn model.
///
/// Observations: own state + 6 neighbor tiles + global scores = 63 floats.
///
/// Actions (25 discrete, single branch):
///   0        = idle / stay
///   1  – 6   = move in direction 0-5
///   7  – 12  = attack in direction 0-5
///   13 – 18  = build in direction 0-5
///   19 – 24  = destroy own wall in direction 0-5
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

    // Cached previous territory state for reward shaping.
    private int prevTeamTiles;
    private int prevEnemyTiles;
    private int prevEnemyComponents;

    public override void Initialize()
    {
        unitData = GetComponent<UnitData>();
        movement = GetComponent<HexMovement>();
        grid     = FindFirstObjectByType<HexGrid>();
        movement.Initialize(grid);
    }

    public override void OnEpisodeBegin()
    {
        prevTeamTiles      = 0;
        prevEnemyTiles     = 0;
        prevEnemyComponents = 0;
    }

    /// <summary>
    /// Observation vector (69 floats):
    /// - Own state (5): q_norm, r_norm, energy/maxEnergy, alive, team(+1/-1)
    /// - 6 neighbors × 10 values each = 60:
    ///     owner: neutral(1), own(1), enemy(1)
    ///     has_wall(1), wall_hp_norm(1), has_slime(1)
    ///     has_enemy_unit(1), has_ally_unit(1)
    ///     enemy_energy_norm(1), is_base(1)
    /// - Global (4): own_territory_pct, enemy_territory_pct, step_progress, respawn_cooldown_norm
    /// Total = 69
    /// </summary>
    public override void CollectObservations(VectorSensor sensor)
    {
        if (grid == null || unitData == null)
        {
            for (int i = 0; i < 69; i++) sensor.AddObservation(0f);
            return;
        }

        int   boardMax   = grid.boardSide - 1;
        float boardRange = boardMax > 0 ? boardMax : 1f;

        // Own state (5).
        sensor.AddObservation(unitData.currentHex.q / boardRange);
        sensor.AddObservation(unitData.currentHex.r / boardRange);
        sensor.AddObservation(unitData.Energy / (float)unitData.maxEnergy);
        sensor.AddObservation(unitData.isAlive ? 1f : 0f);
        sensor.AddObservation(unitData.team == Team.Robot ? 1f : -1f);

        // 6 neighbour observations (10 floats each).
        for (int dir = 0; dir < 6; dir++)
        {
            var neighborCoord = unitData.currentHex.Neighbor(dir);
            var neighborTile  = grid.GetTile(neighborCoord);

            if (neighborTile == null)
            {
                for (int j = 0; j < 10; j++) sensor.AddObservation(0f);
                continue;
            }

            bool isOwn   = neighborTile.Owner == unitData.team;
            bool isEnemy = neighborTile.Owner != Team.None && !isOwn;

            // Ownership (3).
            sensor.AddObservation(neighborTile.Owner == Team.None ? 1f : 0f);
            sensor.AddObservation(isOwn   ? 1f : 0f);
            sensor.AddObservation(isEnemy ? 1f : 0f);

            // Structures (3).
            sensor.AddObservation(neighborTile.TileType == TileType.Wall  ? 1f : 0f);
            sensor.AddObservation(neighborTile.WallHP / 3f);
            sensor.AddObservation(neighborTile.TileType == TileType.Slime ? 1f : 0f);

            // Units (2).
            sensor.AddObservation(HasEnemyUnit(neighborCoord) ? 1f : 0f);
            sensor.AddObservation(HasAllyUnit(neighborCoord)  ? 1f : 0f);

            // Enemy energy (1): normalized energy of enemy unit on this hex, 0 if none.
            sensor.AddObservation(GetEnemyEnergyNorm(neighborCoord));

            // Is base (1).
            sensor.AddObservation(neighborTile.isBase ? 1f : 0f);
        }

        // Global state (4).
        int  ownGroup    = grid.LargestConnectedGroup(unitData.team);
        Team enemyTeam   = unitData.team == Team.Robot ? Team.Mutant : Team.Robot;
        int  enemyGroup  = grid.LargestConnectedGroup(enemyTeam);
        float totalF     = grid.ContestableTileCount > 0 ? grid.ContestableTileCount : 1f;

        sensor.AddObservation(ownGroup   / totalF);
        sensor.AddObservation(enemyGroup / totalF);

        var cfg = GameConfig.Instance;
        int maxSteps = cfg != null ? cfg.maxSteps : 800;
        float stepProgress = Mathf.Clamp01(Academy.Instance.StepCount / (float)maxSteps);
        sensor.AddObservation(stepProgress);

        sensor.AddObservation(unitData.respawnCooldown / 6f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // DecisionRequester fires for ALL agents every frame.
        // Only the active turn unit executes its action; others just observe.
        if (!unitData.isMyTurn) return;

        if (unitData.isAlive)
        {
            int action = actions.DiscreteActions[0];

            if (action == 0)
            {
                // Idle
                unitData.lastAction = UnitAction.Idle;
            }
            else if (action >= 1 && action <= 6)
            {
                // Move in direction
                int dir = action - 1;
                movement.TryMove(dir);

                // Capture bonus.
                if (unitData.lastAction == UnitAction.Capture)
                {
                    Team enemy = unitData.team == Team.Robot ? Team.Mutant : Team.Robot;
                    int enemyNeighbors = grid.CountTeamNeighbors(unitData.currentHex, enemy);
                    if (enemyNeighbors > 0)
                        AddReward((GameConfig.Instance?.hexCaptureReward ?? 0.05f) * enemyNeighbors);
                }
            }
            else if (action >= 7 && action <= 12)
            {
                // Attack in direction (targets: enemy unit, wall).
                int dir = action - 7;
                bool didAttack = movement.TryAttack(dir);
                if (didAttack && unitData.lastAttackKilled)
                    AddReward(GameConfig.Instance?.killBonus ?? 0.5f);
            }
            else if (action >= 13 && action <= 18)
            {
                // Build on adjacent hex in direction.
                int dir = action - 13;
                if (movement.TryBuild(dir))
                {
                    var cfgBuild = GameConfig.Instance;
                    AddReward(cfgBuild?.buildReward ?? 0.05f);

                    // Mutant builds on self, Robot builds on adjacent hex.
                    HexCoord buildTarget = unitData.team == Team.Mutant
                        ? unitData.currentHex
                        : unitData.currentHex.Neighbor(dir);
                    int ownNeighbors = grid.CountTeamNeighbors(buildTarget, unitData.team);
                    if (ownNeighbors > 0)
                        AddReward((cfgBuild?.buildAdjacencyBonus ?? 0.03f) * ownNeighbors);

                    // Extra slime reward for Mutants — slime network is their core strategy.
                    if (unitData.team == Team.Mutant)
                    {
                        AddReward(cfgBuild?.slimePlacementReward ?? 0.08f);

                        // Bonus for slime adjacent to existing slime (network).
                        int slimeNeighbors = CountAdjacentSlime(buildTarget);
                        if (slimeNeighbors > 0)
                            AddReward((cfgBuild?.slimeNetworkBonus ?? 0.04f) * slimeNeighbors);
                    }
                }
            }
            else if (action >= 19 && action <= 24)
            {
                // Destroy own wall in direction (Phase 4 implements TryDestroyWall)
                int dir = action - 19;
                movement.TryDestroyWall(dir);
            }

            // Reward shaping — based on territory analysis (connected groups).
            var cfg = GameConfig.Instance;
            Team enemyTeam = unitData.team == Team.Robot ? Team.Mutant : Team.Robot;
            var ownInfo   = grid.GetTerritoryInfo(unitData.team);
            var enemyInfo = grid.GetTerritoryInfo(enemyTeam);

            // Connected group growth/shrink.
            int groupGained = ownInfo.largestGroup - prevTeamTiles;
            if (groupGained > 0) AddReward((cfg?.captureRewardPerTile ?? 0.1f) * groupGained);

            int enemyGroupLost = prevEnemyTiles - enemyInfo.largestGroup;
            if (enemyGroupLost > 0) AddReward((cfg?.enemyLossRewardPerTile ?? 0.1f) * enemyGroupLost);

            // Cohesion bonus.
            if (ownInfo.totalTiles > 0)
            {
                float cohesion = (float)ownInfo.largestGroup / ownInfo.totalTiles;
                AddReward((cfg?.cohesionBonus ?? 0.02f) * cohesion);
            }

            // Group split bonus.
            int enemySplits = enemyInfo.componentCount - prevEnemyComponents;
            if (enemySplits > 0) AddReward((cfg?.groupSplitBonus ?? 0.3f) * enemySplits);

            // Base connection bonus.
            if (ownInfo.largestTouchesBase)
                AddReward(cfg?.baseConnectionBonus ?? 0.005f);

            // Frontline presence.
            if (grid.IsFrontlineTile(unitData.currentHex, unitData.team))
                AddReward(cfg?.frontlineBonus ?? 0.005f);

            AddReward(cfg?.stepPenalty ?? -0.001f);

            prevTeamTiles       = ownInfo.largestGroup;
            prevEnemyTiles      = enemyInfo.largestGroup;
            prevEnemyComponents = enemyInfo.componentCount;
        }

        // Signal to GameManager that this unit's turn is done.
        unitData.isMyTurn = false;
        unitData.hasPendingTurnResult = true;
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (!unitData.isAlive || grid == null)
        {
            // Dead: only idle allowed.
            for (int i = 1; i < 25; i++)
                actionMask.SetActionEnabled(0, i, false);
            return;
        }

        // Move directions 1-6.
        for (int dir = 0; dir < 6; dir++)
        {
            if (!movement.IsValidMove(dir))
                actionMask.SetActionEnabled(0, 1 + dir, false);
        }

        // Attack directions 7-12.
        for (int dir = 0; dir < 6; dir++)
        {
            if (!movement.IsValidAttack(dir))
                actionMask.SetActionEnabled(0, 7 + dir, false);
        }

        // Build directions 13-18.
        for (int dir = 0; dir < 6; dir++)
        {
            if (!movement.IsValidBuild(dir))
                actionMask.SetActionEnabled(0, 13 + dir, false);
        }

        // Destroy wall directions 19-24.
        for (int dir = 0; dir < 6; dir++)
        {
            if (!movement.IsValidDestroyWall(dir))
                actionMask.SetActionEnabled(0, 19 + dir, false);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var da = actionsOut.DiscreteActions;
        da[0] = Random.Range(0, 25);
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private bool HasEnemyUnit(HexCoord coord)
    {
        foreach (var u in UnitCache.GetAll())
        {
            if (!u.isAlive) continue;
            if (u.team != unitData.team && u.currentHex == coord) return true;
        }
        return false;
    }

    private bool HasAllyUnit(HexCoord coord)
    {
        foreach (var u in UnitCache.GetAll())
        {
            if (u == unitData || !u.isAlive) continue;
            if (u.team == unitData.team && u.currentHex == coord) return true;
        }
        return false;
    }

    private float GetEnemyEnergyNorm(HexCoord coord)
    {
        foreach (var u in UnitCache.GetAll())
        {
            if (!u.isAlive) continue;
            if (u.team != unitData.team && u.currentHex == coord)
                return u.maxEnergy > 0 ? u.Energy / (float)u.maxEnergy : 0f;
        }
        return 0f;
    }

    private int CountAdjacentSlime(HexCoord coord)
    {
        int count = 0;
        for (int dir = 0; dir < 6; dir++)
        {
            var tile = grid.GetTile(coord.Neighbor(dir));
            if (tile != null && tile.TileType == TileType.Slime && tile.Owner == unitData.team)
                count++;
        }
        return count;
    }
}
