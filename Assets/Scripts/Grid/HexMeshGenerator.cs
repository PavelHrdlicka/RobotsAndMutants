using UnityEngine;

/// <summary>
/// Procedurally generates a flat-top hexagon mesh and allows runtime color changes.
/// Attach to a GameObject — MeshFilter and MeshRenderer are added automatically.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HexMeshGenerator : MonoBehaviour
{
    [Tooltip("Outer radius (center to vertex) of the hexagon.")]
    public float outerRadius = 0.5f;

    private static readonly Color DefaultColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        propertyBlock = new MaterialPropertyBlock();

        GenerateMesh();

        // Only create a fallback material if none was assigned (e.g. via prefab).
        if (meshRenderer.sharedMaterial == null)
            InitMaterial();

        SetColor(DefaultColor);
    }

    /// <summary>
    /// Generate the hex mesh. Called automatically in Awake, but can also be called
    /// manually (e.g. from EditMode tests where Awake does not run).
    /// </summary>
    public void GenerateMesh()
    {
        // Flat-top hex: first vertex points right (0°), then every 60°.
        const int sides = 6;
        var vertices = new Vector3[sides + 1]; // center + 6 corners
        var triangles = new int[sides * 3];

        vertices[0] = Vector3.zero; // center

        for (int i = 0; i < sides; i++)
        {
            float angleDeg = 60f * i;
            float angleRad = Mathf.Deg2Rad * angleDeg;
            vertices[i + 1] = new Vector3(
                outerRadius * Mathf.Cos(angleRad),
                0f,
                outerRadius * Mathf.Sin(angleRad)
            );
        }

        for (int i = 0; i < sides; i++)
        {
            int ti = i * 3;
            // Clockwise winding when viewed from above (+Y) so front faces point up.
            triangles[ti]     = 0;
            triangles[ti + 1] = (i < sides - 1) ? i + 2 : 1;
            triangles[ti + 2] = i + 1;
        }

        var mesh = new Mesh { name = "HexMesh" };
        mesh.vertices  = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
    }

    private void InitMaterial()
    {
        // Fallback: try to find the URP Lit shader at runtime.
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard"); // last resort fallback
        if (shader != null)
            meshRenderer.material = new Material(shader);
    }

    /// <summary>
    /// Change the hex color at runtime using a MaterialPropertyBlock (no material clone).
    /// </summary>
    public void SetColor(Color color)
    {
        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", color);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }
}
