using UnityEngine;

/// <summary>
/// Maps HexTileData state (owner, tile type, fortification, base) to hex color.
/// Base tiles are extruded (raised platform) with a glowing team-colored border.
/// Subscribes to OnTileChanged and updates automatically.
/// </summary>
[RequireComponent(typeof(HexTileData), typeof(HexMeshGenerator))]
public class HexVisuals : MonoBehaviour
{
    private HexTileData tileData;
    private HexMeshGenerator meshGen;
    private bool baseSetupDone;

    // --- Color palette ---
    private static readonly Color NeutralColor     = new Color(0.55f, 0.55f, 0.50f);
    private static readonly Color RobotColor       = new Color(0.30f, 0.50f, 0.85f);
    private static readonly Color MutantColor      = new Color(0.45f, 0.75f, 0.30f);
    private static readonly Color RobotBaseColor   = new Color(0.15f, 0.30f, 0.70f);
    private static readonly Color MutantBaseColor  = new Color(0.25f, 0.55f, 0.15f);
    private static readonly Color CrateColor       = new Color(0.20f, 0.35f, 0.65f);
    private static readonly Color SlimeColor       = new Color(0.35f, 0.85f, 0.20f);

    private static readonly float FortificationBrightness = 0.08f;
    private const float BaseExtrudeHeight = 0.08f;

    private void Awake()
    {
        tileData = GetComponent<HexTileData>();
        meshGen  = GetComponent<HexMeshGenerator>();
    }

    private void OnEnable()
    {
        if (tileData != null)
            tileData.OnTileChanged += OnTileChanged;
    }

    private void OnDisable()
    {
        if (tileData != null)
            tileData.OnTileChanged -= OnTileChanged;
    }

    private void Start()
    {
        SetupBaseVisuals();
        UpdateColor();
    }

    private void OnTileChanged(HexTileData _)
    {
        UpdateColor();
    }

    // ── Base tile extrusion + glow border ────────────────────────────────

    private void SetupBaseVisuals()
    {
        if (baseSetupDone || tileData == null || meshGen == null) return;
        if (!tileData.isBase) return;
        baseSetupDone = true;

        // Raise the tile mesh into a platform with side walls.
        meshGen.SetExtruded(true, BaseExtrudeHeight);

        // Create a glowing border ring underneath the platform.
        CreateBaseBorder();
    }

    private void CreateBaseBorder()
    {
        var borderGo = new GameObject("BaseBorder");
        borderGo.transform.SetParent(transform, false);
        borderGo.transform.localPosition = new Vector3(0f, 0.005f, 0f);

        // Slightly larger flat hex underneath the raised tile.
        var mf = borderGo.AddComponent<MeshFilter>();
        var mr = borderGo.AddComponent<MeshRenderer>();

        // Generate a flat hex mesh at slightly larger radius.
        float borderRadius = meshGen.outerRadius * 1.02f;
        const int sides = 6;
        var verts = new Vector3[sides + 1];
        var tris  = new int[sides * 3];
        verts[0] = Vector3.zero;
        for (int i = 0; i < sides; i++)
        {
            float angle = Mathf.Deg2Rad * 60f * i;
            verts[i + 1] = new Vector3(borderRadius * Mathf.Cos(angle), 0f, borderRadius * Mathf.Sin(angle));
        }
        for (int i = 0; i < sides; i++)
        {
            int ti = i * 3;
            tris[ti]     = 0;
            tris[ti + 1] = 1 + (i + 1) % sides;
            tris[ti + 2] = 1 + i;
        }
        var mesh = new Mesh { name = "BaseBorderMesh" };
        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mf.mesh = mesh;

        // Emissive material for the glow.
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var mat = new Material(shader);

        Color glowColor = tileData.baseTeam == Team.Robot
            ? new Color(0.3f, 0.5f, 1f, 1f)
            : new Color(0.3f, 1f, 0.3f, 1f);
        mat.SetColor("_BaseColor", glowColor);
        mr.material = mat;
    }

    // ── Color resolution ─────────────────────────────────────────────────

    public void UpdateColor()
    {
        if (tileData == null || meshGen == null) return;

        Color color = ResolveColor();

        if (tileData.Fortification > 0)
            color += Color.white * (tileData.Fortification * FortificationBrightness);

        meshGen.SetColor(color);
    }

    private Color ResolveColor()
    {
        if (tileData.isBase)
        {
            return tileData.baseTeam switch
            {
                Team.Robot  => RobotBaseColor,
                Team.Mutant => MutantBaseColor,
                _           => NeutralColor
            };
        }

        if (tileData.TileType == TileType.Crate && tileData.Owner == Team.Robot)
            return CrateColor;
        if (tileData.TileType == TileType.Slime && tileData.Owner == Team.Mutant)
            return SlimeColor;

        return tileData.Owner switch
        {
            Team.Robot  => RobotColor,
            Team.Mutant => MutantColor,
            _           => NeutralColor
        };
    }

    public static Color GetColorForState(Team owner, TileType tileType, bool isBase, Team baseTeam, int fortification)
    {
        Color color;

        if (isBase)
        {
            color = baseTeam switch
            {
                Team.Robot  => RobotBaseColor,
                Team.Mutant => MutantBaseColor,
                _           => NeutralColor
            };
        }
        else if (tileType == TileType.Crate && owner == Team.Robot)
            color = CrateColor;
        else if (tileType == TileType.Slime && owner == Team.Mutant)
            color = SlimeColor;
        else
        {
            color = owner switch
            {
                Team.Robot  => RobotColor,
                Team.Mutant => MutantColor,
                _           => NeutralColor
            };
        }

        if (fortification > 0)
            color += Color.white * (fortification * FortificationBrightness);

        return color;
    }
}
