using UnityEngine;

/// <summary>
/// Procedurally generates a flat-top hexagon mesh and allows runtime color changes.
/// Supports a gap factor (visual separation between tiles) and extruded mode
/// for base tiles (raised platform with side walls).
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HexMeshGenerator : MonoBehaviour
{
    [Tooltip("Outer radius (center to vertex) of the hexagon.")]
    public float outerRadius = 0.5f;

    [Tooltip("Shrink factor for visible gaps between hexes. 1 = no gap, 0.90 = 10% gap.")]
    public float gapFactor = 0.92f;

    private static readonly Color DefaultColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        propertyBlock = new MaterialPropertyBlock();

        GenerateMesh();

        if (meshRenderer.sharedMaterial == null)
            InitMaterial();

        SetColor(DefaultColor);
    }

    // ── Flat hex mesh (normal tiles) ───────────────────────────────────

    /// <summary>
    /// Generate a flat hex mesh with gap factor applied.
    /// </summary>
    public void GenerateMesh()
    {
        float r = outerRadius * gapFactor;
        const int sides = 6;
        var vertices  = new Vector3[sides + 1];
        var triangles = new int[sides * 3];

        vertices[0] = Vector3.zero;

        for (int i = 0; i < sides; i++)
        {
            float angle = Mathf.Deg2Rad * 60f * i;
            vertices[i + 1] = new Vector3(r * Mathf.Cos(angle), 0f, r * Mathf.Sin(angle));
        }

        for (int i = 0; i < sides; i++)
        {
            int ti = i * 3;
            triangles[ti]     = 0;
            triangles[ti + 1] = (i < sides - 1) ? i + 2 : 1;
            triangles[ti + 2] = i + 1;
        }

        ApplyMesh(vertices, triangles);
    }

    // ── Extruded hex mesh (base tiles) ─────────────────────────────────

    /// <summary>
    /// Switch between flat and extruded (raised platform with side walls).
    /// </summary>
    public void SetExtruded(bool extruded, float height = 0.08f)
    {
        if (extruded)
            GenerateExtrudedMesh(height);
        else
            GenerateMesh();
    }

    /// <summary>
    /// Generate a raised hex with top face, side walls. Bottom is open (not visible).
    /// </summary>
    public void GenerateExtrudedMesh(float height = 0.08f)
    {
        float r = outerRadius * gapFactor;
        const int sides = 6;

        // Vertices: 1 top center + 6 top rim + 6 bottom rim = 13
        var vertices = new Vector3[1 + sides + sides];
        vertices[0] = new Vector3(0f, height, 0f); // top center

        for (int i = 0; i < sides; i++)
        {
            float angle = Mathf.Deg2Rad * 60f * i;
            float x = r * Mathf.Cos(angle);
            float z = r * Mathf.Sin(angle);
            vertices[1 + i]         = new Vector3(x, height, z); // top rim
            vertices[1 + sides + i] = new Vector3(x, 0f, z);     // bottom rim
        }

        // Triangles: 6 top face + 6*2 side quads = 18 triangles = 54 indices
        var triangles = new int[18 * 3];
        int ti = 0;

        // Top face (same winding as flat hex).
        for (int i = 0; i < sides; i++)
        {
            triangles[ti++] = 0;
            triangles[ti++] = 1 + ((i + 1) % sides);
            triangles[ti++] = 1 + i;
        }

        // Side walls: for each edge, a quad from top rim to bottom rim.
        for (int i = 0; i < sides; i++)
        {
            int topA = 1 + i;
            int topB = 1 + (i + 1) % sides;
            int botA = 1 + sides + i;
            int botB = 1 + sides + (i + 1) % sides;

            // Quad = 2 triangles (outward-facing normals).
            triangles[ti++] = topA;
            triangles[ti++] = topB;
            triangles[ti++] = botA;

            triangles[ti++] = botA;
            triangles[ti++] = topB;
            triangles[ti++] = botB;
        }

        ApplyMesh(vertices, triangles);
    }

    // ── Material & color ───────────────────────────────────────────────

    private void InitMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader != null)
            meshRenderer.material = new Material(shader);
    }

    /// <summary>
    /// Change the hex color at runtime using a MaterialPropertyBlock (no material clone).
    /// </summary>
    private Color currentColor;

    /// <summary>Current hex color (set via SetColor).</summary>
    public Color CurrentColor => currentColor;

    public void SetColor(Color color)
    {
        currentColor = color;
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", color);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private void ApplyMesh(Vector3[] vertices, int[] triangles)
    {
        var mesh = new Mesh { name = "HexMesh" };
        mesh.vertices  = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().mesh = mesh;
    }
}
