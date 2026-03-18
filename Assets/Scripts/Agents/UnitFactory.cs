using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine;

/// <summary>
/// Spawns units for both teams at their base hexes.
/// Creates simple primitive meshes (capsule for Robots, cube for Mutants).
/// </summary>
public class UnitFactory : MonoBehaviour
{
    [Header("Config")]
    public int unitsPerTeam = 6;
    public HexGrid grid;

    [Header("Runtime")]
    public List<UnitData> robotUnits = new();
    public List<UnitData> mutantUnits = new();

    private IEnumerator Start()
    {
        // Wait one frame so HexGrid.Start() finishes generating the board.
        yield return null;

        // Apply GameConfig.
        var config = GameConfig.Instance;
        if (config != null)
            unitsPerTeam = config.unitsPerTeam;

        if (grid == null)
            grid = FindFirstObjectByType<HexGrid>();

        if (grid != null && grid.Tiles.Count > 0)
            SpawnAllUnits();
        else
            Debug.LogWarning("[UnitFactory] No grid found or grid is empty.");
    }

    /// <summary>All units from both teams.</summary>
    public List<UnitData> AllUnits
    {
        get
        {
            var all = new List<UnitData>(robotUnits.Count + mutantUnits.Count);
            all.AddRange(robotUnits);
            all.AddRange(mutantUnits);
            return all;
        }
    }

    /// <summary>Spawn all units. Call after HexGrid has generated.</summary>
    public void SpawnAllUnits()
    {
        ClearUnits();

        var robotBases  = grid.GetBaseTiles(Team.Robot);
        var mutantBases = grid.GetBaseTiles(Team.Mutant);

        SpawnTeam(Team.Robot, robotBases, robotUnits);
        SpawnTeam(Team.Mutant, mutantBases, mutantUnits);

        Debug.Log($"[UnitFactory] Spawned {robotUnits.Count} Robots and {mutantUnits.Count} Mutants.");
    }

    private void SpawnTeam(Team team, List<HexTileData> baseTiles, List<UnitData> unitList)
    {
        int count = Mathf.Min(unitsPerTeam, baseTiles.Count);

        Debug.Log($"[UnitFactory] SpawnTeam {team}: count={count}, bases={baseTiles.Count}");
        for (int i = 0; i < count; i++)
        {
            Debug.Log($"[UnitFactory] Creating {team} unit {i}...");
            var tile = baseTiles[i % baseTiles.Count];
            GameObject unitGo = CreateUnitPrimitive(team, i);
            Debug.Log($"[UnitFactory] {team} unit {i} created OK.");

            var unitData = unitGo.GetComponent<UnitData>();
            unitData.team = team;
            unitData.unitIndex = i;

            var movement = unitGo.GetComponent<HexMovement>();
            movement.Initialize(grid);
            movement.PlaceAt(tile.coord);

            unitGo.transform.SetParent(transform);
            unitList.Add(unitData);
        }
    }

    private GameObject CreateUnitPrimitive(Team team, int index)
    {
        string unitName = team == Team.Robot ? $"Robot_{index}" : $"Mutant_{index}";

        var go = new GameObject(unitName);

        // Core unit components.
        go.AddComponent<UnitData>();
        go.AddComponent<HexMovement>();

        // 3D model.
        if (team == Team.Robot)
        {
            var builder = go.AddComponent<RobotModelBuilder>();
            builder.Build();
        }
        else
        {
            var builder = go.AddComponent<MutantModelBuilder>();
            builder.Build();
        }

        // 3D visuals (health bar, action indicators, attack effects).
        go.AddComponent<UnitHealthBar3D>();
        go.AddComponent<UnitActionIndicator3D>();
        go.AddComponent<AttackEffects>();

        // ML-Agents components.
        var bp = go.AddComponent<BehaviorParameters>();
        bp.BehaviorName = team == Team.Robot ? "HexRobot" : "HexMutant";
        bp.BrainParameters.VectorObservationSize = 56;
        bp.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(8);
        bp.TeamId = team == Team.Robot ? 0 : 1;

        go.AddComponent<HexAgent>();

        // DecisionRequester: all agents observe every frame.
        // Only the active turn unit executes its action (checked via isMyTurn).
        var dr = go.AddComponent<DecisionRequester>();
        dr.DecisionPeriod = 1;

        return go;
    }

    /// <summary>Remove all spawned units.</summary>
    public void ClearUnits()
    {
        foreach (var u in robotUnits)  if (u != null) Destroy(u.gameObject);
        foreach (var u in mutantUnits) if (u != null) Destroy(u.gameObject);
        robotUnits.Clear();
        mutantUnits.Clear();
    }

    /// <summary>Respawn all dead units whose cooldown has expired.</summary>
    public void RespawnReady()
    {
        RespawnTeam(robotUnits, Team.Robot);
        RespawnTeam(mutantUnits, Team.Mutant);
    }

    private void RespawnTeam(List<UnitData> units, Team team)
    {
        var baseTiles = grid.GetBaseTiles(team);

        foreach (var unit in units)
        {
            if (unit.isAlive) continue;
            if (!unit.TickCooldown()) continue;

            // Find an unoccupied base tile.
            foreach (var baseTile in baseTiles)
            {
                if (!IsOccupied(baseTile.coord, units))
                {
                    Vector3 worldPos = grid.HexToWorld(baseTile.coord);
                    unit.Respawn(baseTile.coord, worldPos);
                    unit.GetComponent<HexMovement>().Initialize(grid);
                    break;
                }
            }
        }
    }

    private static bool IsOccupied(HexCoord coord, List<UnitData> units)
    {
        foreach (var u in units)
        {
            if (u.isAlive && u.currentHex == coord)
                return true;
        }
        return false;
    }
}
