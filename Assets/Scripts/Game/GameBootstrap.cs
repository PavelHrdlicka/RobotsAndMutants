using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime bootstrap for the game scene. Creates HexGrid, GameManager, and all
/// required components when the scene is loaded from MainMenu (or standalone build).
/// In Editor with ProjectTools, HexGridSetup handles setup before Play mode.
///
/// Uses sceneLoaded event so it fires on EVERY scene load, not just app start.
/// </summary>
public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Only bootstrap game scenes, not MainMenu.
        if (scene.name == "MainMenu") return;

        // If HexGrid already exists (set up by Editor tools), skip.
        if (Object.FindFirstObjectByType<HexGrid>() != null) return;

        Debug.Log("[GameBootstrap] No HexGrid found — running runtime setup.");
        SetupScene();
    }

    private static void SetupScene()
    {
        // Load hex prefab.
        var prefab = LoadHexPrefab();
        if (prefab == null)
        {
            Debug.LogError("[GameBootstrap] Could not load HexTile prefab!");
            return;
        }

        // Create HexGrid.
        var gridGo = new GameObject("HexGrid");
        var grid = gridGo.AddComponent<HexGrid>();
        grid.hexPrefab = prefab;

        // Apply board size from menu config.
        var config = GameConfig.Instance;
        if (config != null)
        {
            if (GameModeConfig.BoardSize > 0)
                config.boardSide = GameModeConfig.BoardSize;
            grid.boardSide = config.boardSide;
        }
        else
        {
            grid.boardSide = GameModeConfig.BoardSize > 0 ? GameModeConfig.BoardSize : 4;
        }

        // Create GameManager.
        var gmGo = new GameObject("GameManager");
        gmGo.AddComponent<GameManager>();

        // Create UnitFactory.
        var ufGo = new GameObject("UnitFactory");
        ufGo.AddComponent<UnitFactory>();

        // Configure camera.
        ConfigureCamera(grid.boardSide);
    }

    private static GameObject LoadHexPrefab()
    {
#if UNITY_EDITOR
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HexTile.prefab");
        if (prefab != null) return prefab;
#endif

        var loaded = Resources.Load<GameObject>("HexTile");
        if (loaded != null) return loaded;

        // Fallback: create minimal hex prefab at runtime.
        Debug.LogWarning("[GameBootstrap] Creating fallback hex prefab.");
        var go = new GameObject("HexPrefab");
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        go.AddComponent<HexMeshGenerator>();
        go.AddComponent<HexTileData>();
        go.SetActive(false);
        return go;
    }

    private static void ConfigureCamera(int boardSide)
    {
        var cam = Camera.main;
        if (cam == null) return;

        cam.orthographic = true;

        float height = boardSide * 1.2f;
        cam.transform.position = new Vector3(0, height, -height * 0.7f);
        cam.transform.rotation = Quaternion.Euler(45, 0, 0);
        cam.orthographicSize = boardSide * 0.9f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
    }
}
