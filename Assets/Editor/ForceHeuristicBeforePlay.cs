using UnityEditor;
using UnityEngine;
using Unity.MLAgents.Policies;

/// <summary>
/// Before entering Play mode, force all BehaviorParameters in the scene
/// to HeuristicOnly. This ensures the backup scene (which Unity restores
/// in Play mode and during PlayMode tests) does not contain agents that
/// try to connect to a Python trainer via gRPC — which blocks and freezes Unity.
/// </summary>
[InitializeOnLoad]
public static class ForceHeuristicBeforePlay
{
    static ForceHeuristicBeforePlay()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;

        int count = 0;
        foreach (var bp in Object.FindObjectsByType<BehaviorParameters>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (bp.BehaviorType != BehaviorType.HeuristicOnly)
            {
                bp.BehaviorType = BehaviorType.HeuristicOnly;
                count++;
            }
        }

        if (count > 0)
            Debug.Log($"[ForceHeuristic] Set {count} agents to HeuristicOnly before Play.");
    }
}
