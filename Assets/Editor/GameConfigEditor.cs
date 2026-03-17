using UnityEngine;
using UnityEditor;

/// <summary>
/// Ensures GameConfig asset exists in Resources folder.
/// </summary>
[InitializeOnLoad]
public static class GameConfigEditor
{
    private const string AssetPath = "Assets/Resources/GameConfig.asset";

    static GameConfigEditor()
    {
        EditorApplication.delayCall += EnsureConfigExists;
    }

    public static GameConfig GetOrCreateConfig()
    {
        var config = AssetDatabase.LoadAssetAtPath<GameConfig>(AssetPath);
        if (config != null) return config;

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        config = ScriptableObject.CreateInstance<GameConfig>();
        AssetDatabase.CreateAsset(config, AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[GameConfig] Created default config at " + AssetPath);
        return config;
    }

    private static void EnsureConfigExists()
    {
        GetOrCreateConfig();
    }
}
