using Unity.MLAgents.Policies;
using UnityEngine;

/// <summary>
/// Prevents ML-Agents from trying to connect to a Python trainer on scene load.
/// Runs before any Agent.Awake() fires, setting all serialized BehaviorParameters
/// to HeuristicOnly. When mlagents-learn is running, it overrides this via the
/// communicator channel.
///
/// This fixes the freeze that occurs when a backup scene has agents with
/// BehaviorType=Default but no Python trainer is listening.
/// </summary>
public static class ForceHeuristicOnLoad
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SetAllAgentsHeuristic()
    {
        // Find all BehaviorParameters already in memory (from serialized scene).
        foreach (var bp in Object.FindObjectsByType<BehaviorParameters>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (bp.BehaviorType == BehaviorType.Default)
                bp.BehaviorType = BehaviorType.HeuristicOnly;
        }
    }
}
