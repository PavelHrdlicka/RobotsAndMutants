using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;
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
    /// Observation vector (63 floats):
    /// - Own position (2), health (1), alive (1), team (1) = 5
    /// - 6 neighbors × 9 values each = 54
    ///     neutral(1), own(1), enemy(1), empty(1), crate(1), slime(1),
    ///     hasEnemy(1), hasAlly(1), fortification(1)
    /// - Global: own tiles (1), enemy tiles (1), step progress (1), respawn cooldown (1) = 4
    /// Total = 63
    /// </summary>
    public override void CollectObservations(VectorSensor sensor)
    {
        if (grid == null || unitData == null)
        {
            for (int i = 0; i < 63; i++) sensor.AddObservation(0f);
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

        // 6 neighbour observations (9 floats each).
        for (int dir = 0; dir < 6; dir++)
        {
            var neighborCoord = unitData.currentHex.Neighbor(dir);
            var neighborTile  = grid.GetTile(neighborCoord);

            if (neighborTile == null)
            {
                for (int j = 0; j < 9; j++) sensor.AddObservation(0f);
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
            sensor.AddObservation(neighborTile.Fortification / 3f);
        }

        // Global state — largest connected group (win condition metric).
        int  ownGroup    = grid.LargestConnectedGroup(unitData.team);
        Team enemyTeam   = unitData.team == Team.Robot ? Team.Mutant : Team.Robot;
        int  enemyGroup  = grid.LargestConnectedGroup(enemyTeam);
        float totalF     = grid.ContestableTileCount > 0 ? grid.ContestableTileCount : 1f;

        sensor.AddObservation(ownGroup   / totalF);
        sensor.AddObservation(enemyGroup / totalF);

        float stepProgress = Mathf.Clamp01(Academy.Instance.StepCount / 6000f);
        sensor.AddObservation(stepProgress);

        // Respawn cooldown: 0 when alive, counts down from 30 max.
        sensor.AddObservation(unitData.respawnCooldown / 30f);
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
                unitData.lastAction = UnitAction.Idle;
            }
            else if (action >= 1 && action <= 6)
            {
                int dir = action - 1;
                bool didAttack = movement.TryAttack(dir);
                if (didAttack)
                {
                    // Kill bonus: check if the enemy at that hex just died.
                    HexCoord targetCoord = unitData.currentHex.Neighbor(dir);
                    foreach (var u in UnitCache.GetAll())
                    {
                        if (u.team != unitData.team && u.currentHex == targetCoord && !u.isAlive)
                        {
                            AddReward(GameConfig.Instance?.killBonus ?? 0.5f);
                            break;
                        }
                    }
                }
                else
                {
                    movement.TryMove(dir);

                    // Disruption bonus: capturing a tile surrounded by enemy tiles.
                    if (unitData.lastAction == UnitAction.Capture)
                    {
                        Team enemy = unitData.team == Team.Robot ? Team.Mutant : Team.Robot;
                        int enemyNeighbors = grid.CountTeamNeighbors(unitData.currentHex, enemy);
                        if (enemyNeighbors > 0)
                            AddReward((GameConfig.Instance?.captureDisruptionBonus ?? 0.05f) * enemyNeighbors);
                    }
                }
            }
            else if (action == 7)
            {
                if (movement.TryBuild())
                {
                    AddReward(GameConfig.Instance?.buildReward ?? 0.05f);

                    // Adjacency bonus: more own neighbors → higher reward for clustering.
                    int ownNeighbors = grid.CountTeamNeighbors(unitData.currentHex, unitData.team);
                    if (ownNeighbors > 0)
                        AddReward((GameConfig.Instance?.buildAdjacencyBonus ?? 0.03f) * ownNeighbors);
                }
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

            // 1) Cohesion bonus: reward keeping territory in one big group.
            if (ownInfo.totalTiles > 0)
            {
                float cohesion = (float)ownInfo.largestGroup / ownInfo.totalTiles;
                AddReward((cfg?.cohesionBonus ?? 0.02f) * cohesion);
            }

            // 2) Group split bonus: extra reward when enemy gets fragmented.
            int enemySplits = enemyInfo.componentCount - prevEnemyComponents;
            if (enemySplits > 0) AddReward((cfg?.groupSplitBonus ?? 0.3f) * enemySplits);

            // 3) Base connection bonus: reward keeping largest group linked to base.
            if (ownInfo.largestTouchesBase)
                AddReward(cfg?.baseConnectionBonus ?? 0.005f);

            // 4) Frontline presence: reward units holding the border.
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
        var da = actionsOut.DiscreteActions;
        da[0] = Random.Range(0, 8);
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
}
