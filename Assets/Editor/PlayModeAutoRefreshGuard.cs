using UnityEditor;
using UnityEngine;

/// <summary>
/// Disables Unity Asset Pipeline auto-refresh during Play mode to prevent
/// multi-second freezes caused by reimporting changed files mid-simulation.
/// Restores the original setting when exiting Play mode.
/// </summary>
[InitializeOnLoad]
public static class PlayModeAutoRefreshGuard
{
    private const string PrefKey = "PlayModeAutoRefreshGuard_PrevSetting";

    static PlayModeAutoRefreshGuard()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.EnteredPlayMode:
                // Save current setting and disable auto-refresh.
                int current = (int)AssetDatabase.DesiredWorkerCount;
                EditorPrefs.SetInt(PrefKey, EditorPrefs.GetInt("kAutoRefreshMode", 1));
                EditorPrefs.SetInt("kAutoRefreshMode", 0);
                AssetDatabase.DisallowAutoRefresh();
                Debug.Log("[AutoRefreshGuard] Auto-refresh disabled for Play mode.");
                break;

            case PlayModeStateChange.ExitingPlayMode:
                // Restore original setting.
                int prev = EditorPrefs.GetInt(PrefKey, 1);
                EditorPrefs.SetInt("kAutoRefreshMode", prev);
                AssetDatabase.AllowAutoRefresh();
                Debug.Log("[AutoRefreshGuard] Auto-refresh restored.");
                break;
        }
    }
}
