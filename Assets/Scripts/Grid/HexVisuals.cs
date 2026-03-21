using UnityEngine;

/// <summary>
/// Maps HexTileData state (owner, tile type, wall HP, base) to hex color and extrusion.
/// Base tiles are extruded (raised platform) with a glowing team-colored border.
/// Wall tiles are extruded higher, brightness scales with wall HP.
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
    private static readonly Color WallColor        = new Color(0.25f, 0.30f, 0.50f);
    private static readonly Color SlimeColor       = new Color(0.55f, 0.90f, 0.05f);
    private static readonly Color SlimeStripeColor = new Color(0.2f, 0.5f, 0.0f);
    private const float SlimeExtrudeHeight = 0.04f;

    // Shared hatched texture for slime overlay (created once).
    private static Texture2D slimeHatchTexture;
    private static Material slimeOverlayMaterial;

    // Per-tile slime overlay child object.
    private GameObject slimeOverlay;

    private static readonly float WallHPBrightness = 0.05f;
    private const float BaseExtrudeHeight = 0.08f;
    private const float WallExtrudeHeight = 0.12f;

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
        // Silent training: disable all visual updates and mesh rendering.
        if (GameManager.SilentTraining)
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            enabled = false;
            return;
        }

        SetupBaseVisuals();
        UpdateColor();
    }

    private void OnTileChanged(HexTileData _)
    {
        UpdateVisuals();
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

    // ── Visual update ──────────────────────────────────────────────────

    private void UpdateVisuals()
    {
        UpdateColor();
        UpdateExtrusion();
        UpdateSlimeOverlay();
    }

    public void UpdateColor()
    {
        if (tileData == null || meshGen == null) return;
        meshGen.SetColor(ResolveColor());
    }

    private void UpdateExtrusion()
    {
        if (tileData == null || meshGen == null) return;
        if (tileData.isBase) return; // base extrusion is set once in SetupBaseVisuals

        if (tileData.TileType == TileType.Wall)
            meshGen.SetExtruded(true, WallExtrudeHeight);
        else if (tileData.TileType == TileType.Slime)
            meshGen.SetExtruded(true, SlimeExtrudeHeight);
        else
            meshGen.SetExtruded(false, 0f);
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

        if (tileData.TileType == TileType.Wall && tileData.Owner == Team.Robot)
        {
            Color c = WallColor;
            if (tileData.WallHP > 0)
                c += Color.white * (tileData.WallHP * WallHPBrightness);
            return c;
        }

        if (tileData.TileType == TileType.Slime && tileData.Owner == Team.Mutant)
            return SlimeColor;

        return tileData.Owner switch
        {
            Team.Robot  => RobotColor,
            Team.Mutant => MutantColor,
            _           => NeutralColor
        };
    }

    // ── Slime hatched overlay ────────────────────────────────────────────

    private void UpdateSlimeOverlay()
    {
        bool needsOverlay = tileData.TileType == TileType.Slime && tileData.Owner == Team.Mutant;

        if (needsOverlay && slimeOverlay == null)
            CreateSlimeOverlay();
        else if (!needsOverlay && slimeOverlay != null)
        {
            slimeOverlay.SetActive(false); // Hide immediately (Destroy is deferred).
            Destroy(slimeOverlay);
            slimeOverlay = null;
        }
    }

    private void CreateSlimeOverlay()
    {
        EnsureSlimeTexture();

        slimeOverlay = new GameObject("SlimeOverlay");
        slimeOverlay.transform.SetParent(transform, false);
        slimeOverlay.transform.localPosition = new Vector3(0f, SlimeExtrudeHeight + 0.005f, 0f);

        var mf = slimeOverlay.AddComponent<MeshFilter>();
        var mr = slimeOverlay.AddComponent<MeshRenderer>();

        // Flat hex mesh matching tile size.
        float r = meshGen.outerRadius * meshGen.gapFactor * 0.95f;
        const int sides = 6;
        var verts = new Vector3[sides + 1];
        var tris = new int[sides * 3];
        var uvs = new Vector2[sides + 1];
        verts[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < sides; i++)
        {
            float angle = Mathf.Deg2Rad * 60f * i;
            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);
            verts[i + 1] = new Vector3(r * x, 0f, r * z);
            uvs[i + 1] = new Vector2(0.5f + 0.5f * x, 0.5f + 0.5f * z);
        }
        for (int i = 0; i < sides; i++)
        {
            int ti = i * 3;
            tris[ti] = 0;
            tris[ti + 1] = 1 + (i + 1) % sides;
            tris[ti + 2] = 1 + i;
        }
        var mesh = new Mesh { name = "SlimeOverlayMesh" };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mf.mesh = mesh;
        mr.material = slimeOverlayMaterial;
    }

    private static void EnsureSlimeTexture()
    {
        if (slimeHatchTexture != null) return;

        // Create a diagonal stripe texture.
        const int size = 64;
        const int stripeWidth = 6;
        slimeHatchTexture = new Texture2D(size, size, TextureFormat.ARGB32, false);
        slimeHatchTexture.filterMode = FilterMode.Bilinear;
        slimeHatchTexture.wrapMode = TextureWrapMode.Repeat;

        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Diagonal stripes: (x + y) mod period
                bool stripe = ((x + y) % (stripeWidth * 2)) < stripeWidth;
                pixels[y * size + x] = stripe
                    ? new Color(0.1f, 0.4f, 0.0f, 0.7f)   // dark green stripe
                    : new Color(0f, 0f, 0f, 0f);             // transparent
            }
        }
        slimeHatchTexture.SetPixels(pixels);
        slimeHatchTexture.Apply();

        // Transparent unlit material.
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        slimeOverlayMaterial = new Material(shader);
        slimeOverlayMaterial.SetTexture("_BaseMap", slimeHatchTexture);
        slimeOverlayMaterial.SetColor("_BaseColor", Color.white);

        // Enable transparency for URP.
        slimeOverlayMaterial.SetFloat("_Surface", 1);
        slimeOverlayMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        slimeOverlayMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        slimeOverlayMaterial.SetInt("_ZWrite", 0);
        slimeOverlayMaterial.renderQueue = 3000;
        slimeOverlayMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    public static Color GetColorForState(Team owner, TileType tileType, bool isBase, Team baseTeam, int wallHP)
    {
        if (isBase)
        {
            return baseTeam switch
            {
                Team.Robot  => RobotBaseColor,
                Team.Mutant => MutantBaseColor,
                _           => NeutralColor
            };
        }

        if (tileType == TileType.Wall && owner == Team.Robot)
        {
            Color c = WallColor;
            if (wallHP > 0)
                c += Color.white * (wallHP * WallHPBrightness);
            return c;
        }

        if (tileType == TileType.Slime && owner == Team.Mutant)
            return SlimeColor;

        return owner switch
        {
            Team.Robot  => RobotColor,
            Team.Mutant => MutantColor,
            _           => NeutralColor
        };
    }
}
