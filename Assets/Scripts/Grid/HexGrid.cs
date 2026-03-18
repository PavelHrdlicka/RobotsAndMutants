using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a hex-shaped board (large hexagon with given side length) using axial coordinates.
/// Side N produces 3N²-3N+1 tiles. After generation, centres the camera in top-down ortho view.
/// </summary>
public class HexGrid : MonoBehaviour
{
    [Header("Board")]
    [Tooltip("Number of hexes along each edge of the hex-shaped board.")]
    public int boardSide = 5;

    [Header("Hex")]
    [Tooltip("Prefab with HexMeshGenerator (and optionally HexTileData) attached.")]
    public GameObject hexPrefab;

    [Tooltip("Outer radius of each hex (must match HexMeshGenerator.outerRadius).")]
    public float outerRadius = 0.5f;

    [Header("Camera")]
    [Tooltip("Camera to position. Falls back to Camera.main when null.")]
    public Camera targetCamera;

    private readonly Dictionary<HexCoord, HexTileData> tiles = new();

    /// <summary>Total number of tiles on a hex board with given side.</summary>
    public static int TileCount(int side) => 3 * side * side - 3 * side + 1;

    /// <summary>All tile data indexed by axial coordinate.</summary>
    public IReadOnlyDictionary<HexCoord, HexTileData> Tiles => tiles;

    private void Awake()
    {
        // Skip camera setup in test mode — URP initialization in empty InitTestScene blocks.
        if (!TestModeDetector.IsTestMode())
            EnsureCamera();
    }

    private void Start()
    {
        var config = GameConfig.Instance;
        if (config != null)
            boardSide = config.boardSide;

        if (hexPrefab == null)
        {
            Debug.LogError("[HexGrid] hexPrefab is not assigned! Run Tools > Hex Grid > Reset, then Setup Scene.");
            return;
        }
        GenerateGrid();
        SetupBases();
        if (!TestModeDetector.IsTestMode())
            CenterCamera();
    }


    /// <summary>Create a camera if none exists. Called in Awake so URP sees it early.</summary>
    private void EnsureCamera()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cam.targetDisplay = 0;
            cam.enabled = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.18f, 1f);
            Debug.Log("[HexGrid] Created camera in Awake.");
        }
        else if (cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() == null)
        {
            cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            Debug.Log("[HexGrid] Added URP data to existing camera.");
        }
    }

    private void GenerateGrid()
    {
        int max = boardSide - 1;

        for (int q = -max; q <= max; q++)
        {
            int rMin = Mathf.Max(-max, -q - max);
            int rMax = Mathf.Min(max, -q + max);

            for (int r = rMin; r <= rMax; r++)
            {
                var coord = new HexCoord(q, r);
                Vector3 pos = HexToWorld(coord);

                GameObject hex = Instantiate(hexPrefab, pos, Quaternion.identity, transform);
                hex.name = $"Hex_{q}_{r}";

                var tileData = hex.GetComponent<HexTileData>();
                if (tileData == null)
                    tileData = hex.AddComponent<HexTileData>();
                tileData.coord = coord;

                tiles[coord] = tileData;
            }
        }
    }

    /// <summary>Convert axial coordinate to world position (flat-top hex layout).</summary>
    public Vector3 HexToWorld(HexCoord coord)
    {
        float x = outerRadius * 1.5f * coord.q;
        float z = outerRadius * (Mathf.Sqrt(3f) * 0.5f * coord.q + Mathf.Sqrt(3f) * coord.r);
        return new Vector3(x, 0f, z);
    }

    /// <summary>Convert world position to nearest axial coordinate.</summary>
    public HexCoord WorldToHex(Vector3 worldPos)
    {
        float q = worldPos.x / (outerRadius * 1.5f);
        float r = (worldPos.z - outerRadius * Mathf.Sqrt(3f) * 0.5f * q) / (outerRadius * Mathf.Sqrt(3f));

        return RoundToHex(q, r);
    }

    /// <summary>Get tile data at the given coordinate, or null if out of bounds.</summary>
    public HexTileData GetTile(HexCoord coord)
    {
        tiles.TryGetValue(coord, out var tile);
        return tile;
    }

    /// <summary>Get all valid neighbor tiles for a coordinate (up to 6).</summary>
    public List<HexTileData> GetNeighbors(HexCoord coord)
    {
        var result = new List<HexTileData>(6);
        for (int i = 0; i < 6; i++)
        {
            var neighbor = coord.Neighbor(i);
            if (tiles.TryGetValue(neighbor, out var tile))
                result.Add(tile);
        }
        return result;
    }

    /// <summary>Check if a coordinate is valid on the current board.</summary>
    public bool IsValidCoord(HexCoord coord) => tiles.ContainsKey(coord);

    /// <summary>
    /// Robot base: bottom-left cluster (q negative, r positive).
    /// Mutant base: top-right cluster (q positive, r negative).
    /// Each base is the corner hex plus its neighbors (up to 7 tiles).
    /// </summary>
    private void SetupBases()
    {
        int max = boardSide - 1;

        // Robot base: corner at (-max, max) — bottom-left of the hex board.
        SetupBaseCluster(new HexCoord(-max, max), Team.Robot);

        // Mutant base: corner at (max, -max) — top-right of the hex board.
        SetupBaseCluster(new HexCoord(max, -max), Team.Mutant);
    }

    private void SetupBaseCluster(HexCoord center, Team team)
    {
        // Base size = unitsPerTeam from config (need at least that many spawn tiles).
        var config = GameConfig.Instance;
        int targetSize = config != null ? config.unitsPerTeam : 4;

        // BFS outward from center to fill base up to targetSize tiles.
        var baseCoords = new System.Collections.Generic.List<HexCoord>();
        var queue = new System.Collections.Generic.Queue<HexCoord>();
        var visited = new System.Collections.Generic.HashSet<HexCoord>();

        queue.Enqueue(center);
        visited.Add(center);

        while (queue.Count > 0 && baseCoords.Count < targetSize)
        {
            var coord = queue.Dequeue();
            if (!tiles.ContainsKey(coord)) continue;

            baseCoords.Add(coord);

            for (int i = 0; i < 6; i++)
            {
                var neighbor = coord.Neighbor(i);
                if (!visited.Contains(neighbor) && tiles.ContainsKey(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        foreach (var coord in baseCoords)
        {
            var tile = GetTile(coord);
            if (tile != null)
                MarkAsBase(tile, team);
        }
    }

    private static void MarkAsBase(HexTileData tile, Team team)
    {
        tile.isBase = true;
        tile.baseTeam = team;
        tile.Owner = team;
    }

    /// <summary>Count non-base tiles owned by the given team.</summary>
    public int CountTiles(Team team)
    {
        int count = 0;
        foreach (var tile in tiles.Values)
            if (!tile.isBase && tile.Owner == team) count++;
        return count;
    }

    private int _contestableTileCount = -1;

    /// <summary>
    /// Total number of non-base (contestable) tiles. Computed once after grid generation.
    /// </summary>
    public int ContestableTileCount
    {
        get
        {
            if (_contestableTileCount < 0)
            {
                _contestableTileCount = 0;
                foreach (var tile in tiles.Values)
                    if (!tile.isBase) _contestableTileCount++;
            }
            return _contestableTileCount;
        }
    }

    /// <summary>Get all base tiles for a given team.</summary>
    public List<HexTileData> GetBaseTiles(Team team)
    {
        var result = new List<HexTileData>();
        foreach (var tile in tiles.Values)
        {
            if (tile.isBase && tile.baseTeam == team)
                result.Add(tile);
        }
        return result;
    }

    private static HexCoord RoundToHex(float q, float r)
    {
        float s = -q - r;
        int rq = Mathf.RoundToInt(q);
        int rr = Mathf.RoundToInt(r);
        int rs = Mathf.RoundToInt(s);

        float dq = Mathf.Abs(rq - q);
        float dr = Mathf.Abs(rr - r);
        float ds = Mathf.Abs(rs - s);

        if (dq > dr && dq > ds)
            rq = -rr - rs;
        else if (dr > ds)
            rr = -rq - rs;

        return new HexCoord(rq, rr);
    }

    private void CenterCamera()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;

        // Create camera if none exists (e.g. fresh scene after Reset).
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();

            // URP requires this component for the camera to render.
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        }
        else if (cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() == null)
        {
            cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        }

        // Force camera to render to Display 1.
        cam.targetDisplay = 0;
        cam.enabled = true;
        cam.depth = 0;

        float boardRadius = outerRadius * Mathf.Sqrt(3f) * (boardSide - 1);
        float padding = outerRadius * 3f;

        // Isometric view: 45° down, 45° rotated — shows hex board in 3D perspective.
        cam.orthographic = true;
        cam.transform.rotation = Quaternion.Euler(45f, 45f, 0f);

        // Position camera along the view direction, centred on the board.
        cam.transform.position = -cam.transform.forward * 50f;

        // Ortho size must compensate for the angled view (board takes less screen height).
        float isoScale = 1.35f;
        float aspect = cam.aspect;
        float sizeForHeight = (boardRadius + padding) * isoScale;
        float sizeForWidth  = (boardRadius + padding) * isoScale / aspect;
        cam.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);

        cam.nearClipPlane = 0.1f;
        cam.farClipPlane  = 200f;
        cam.clearFlags    = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.18f, 1f);
    }
}
