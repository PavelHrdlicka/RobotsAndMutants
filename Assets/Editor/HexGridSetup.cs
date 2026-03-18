using UnityEngine;
using UnityEditor;

/// <summary>
/// One-click scene setup: creates hex prefab + HexGrid + camera.
/// Run from menu: Tools > Hex Grid > Setup Scene.
/// </summary>
public static class HexGridSetup
{
    private const string PrefabPath   = "Assets/Prefabs/HexTile.prefab";
    private const string MaterialPath = "Assets/Prefabs/HexDefault.mat";

    [MenuItem("Tools/Hex Grid/Setup Scene")]
    public static void SetupScene()
    {
        GameObject prefab = CreateOrLoadPrefab();
        CreateGrid(prefab);
        ConfigureCamera();
        Debug.Log("[HexGridSetup] Scene ready. Press Play to see the grid.");
    }

    [MenuItem("Tools/Hex Grid/Reset (Delete Prefab + Material)")]
    public static void Reset()
    {
        AssetDatabase.DeleteAsset(PrefabPath);
        AssetDatabase.DeleteAsset(MaterialPath);
        var oldGrid = GameObject.Find("HexGrid");
        if (oldGrid != null) Object.DestroyImmediate(oldGrid);
        Debug.Log("[HexGridSetup] Reset done. Run Setup Scene again.");
    }

    private static GameObject CreateOrLoadPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null)
        {
            // Ensure prefab has all required components.
            bool needsUpdate = existing.GetComponent<HexTileData>() == null
                            || existing.GetComponent<HexVisuals>() == null;
            if (needsUpdate)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(existing);
                if (instance.GetComponent<HexTileData>() == null)
                    instance.AddComponent<HexTileData>();
                if (instance.GetComponent<HexVisuals>() == null)
                    instance.AddComponent<HexVisuals>();
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
                Object.DestroyImmediate(instance);
                existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Debug.Log("[HexGridSetup] Updated prefab with missing components.");
            }
            return existing;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        Material mat = GetOrCreateMaterial();

        var temp = new GameObject("HexTile");
        temp.AddComponent<HexMeshGenerator>();
        temp.AddComponent<HexTileData>();
        temp.AddComponent<HexVisuals>();
        temp.GetComponent<MeshRenderer>().sharedMaterial = mat;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
        Object.DestroyImmediate(temp);

        Debug.Log("[HexGridSetup] Created prefab at " + PrefabPath);
        return prefab;
    }

    private static void CreateGrid(GameObject prefab)
    {
        var oldGrid = GameObject.Find("HexGrid");
        if (oldGrid != null)
            Object.DestroyImmediate(oldGrid);

        var oldFactory = GameObject.Find("UnitFactory");
        if (oldFactory != null)
            Object.DestroyImmediate(oldFactory);

        var gridGo = new GameObject("HexGrid");
        var grid = gridGo.AddComponent<HexGrid>();
        grid.hexPrefab = prefab;

        var factoryGo = new GameObject("UnitFactory");
        var factory = factoryGo.AddComponent<UnitFactory>();
        factory.grid = grid;

        // Remove old GameManager if present.
        var oldGM = GameObject.Find("GameManager");
        if (oldGM != null) Object.DestroyImmediate(oldGM);

        var gmGo = new GameObject("GameManager");
        gmGo.AddComponent<GameManager>();

        Debug.Log("[HexGridSetup] HexGrid + UnitFactory + GameManager created.");
    }

    private static Material GetOrCreateMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat != null) return mat;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.75f, 0.75f, 0.7f, 1f));
        AssetDatabase.CreateAsset(mat, MaterialPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[HexGridSetup] Created material at " + MaterialPath);
        return mat;
    }

    private static void ConfigureCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            // Create a camera if none exists.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            // Add URP camera data if available.
            var urpCamType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urpCamType != null) camGo.AddComponent(urpCamType);
            Debug.Log("[HexGridSetup] Created MainCamera.");
        }

        cam.orthographic = true;
        cam.transform.position = new Vector3(0f, 50f, 0f);
        cam.transform.rotation = Quaternion.Euler(45f, 45f, 0f);
        cam.transform.position = -cam.transform.forward * 50f;
        cam.orthographicSize = 10f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.18f, 1f);

        Debug.Log("[HexGridSetup] Camera configured (isometric 45°).");
    }
}
