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

        // Fix stale counter from previous abnormal exit (crash, force stop).
        // If we're in Edit mode but the flag says we disabled refresh, restore it.
        if (!EditorApplication.isPlayingOrWillChangePlaymode
            && SessionState.GetBool(k_ActiveKey, false))
        {
            AssetDatabase.AllowAutoRefresh();
            SessionState.EraseBool(k_ActiveKey);
        }
    }

    private const string k_ActiveKey = "AutoRefreshGuard_Active";

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.EnteredPlayMode:
                SessionState.SetBool(k_ActiveKey, true);
                EditorPrefs.SetInt(PrefKey, EditorPrefs.GetInt("kAutoRefreshMode", 1));
                EditorPrefs.SetInt("kAutoRefreshMode", 0);
                AssetDatabase.DisallowAutoRefresh();
                Debug.Log("[AutoRefreshGuard] Auto-refresh disabled for Play mode.");
                break;

            case PlayModeStateChange.ExitingPlayMode:
                SessionState.EraseBool(k_ActiveKey);
                int prev = EditorPrefs.GetInt(PrefKey, 1);
                EditorPrefs.SetInt("kAutoRefreshMode", prev);
                AssetDatabase.AllowAutoRefresh();
                break;
        }
    }
}
