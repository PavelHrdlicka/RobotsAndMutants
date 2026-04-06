using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Draws an animated dashed hex outline around tiles occupied by units that have
/// at least one adjacent ally. Dashes flow around the hex perimeter via Y-axis rotation.
///
/// Implementation:
///   - Geometry-baked dashes: 8 separate quad segments along the hex perimeter with gaps.
///   - Opaque URP/Unlit material (no transparent shader runtime issues).
///   - Animation: rotate the entire ring object around Y axis.
///   - Team-colored via MaterialPropertyBlock._BaseColor.
/// </summary>
[RequireComponent(typeof(UnitData))]
public class AdjacencyAura : MonoBehaviour
{
    private const int DashCount = 12;        // Number of dashes around the perimeter.
    private const float DashFillRatio = 0.6f; // Fraction of arc length used by dash (rest is gap).
    private const float RotateSpeed = 25f;   // Degrees per second.

    private UnitData unitData;
    private HexGrid grid;
    private GameObject outlineObj;
    private MeshRenderer outlineRenderer;
    private MaterialPropertyBlock mpb;
    private HexCoord lastCoord;
    private float outerRadius;

    private static readonly int ColorID = Shader.PropertyToID("_BaseColor");
    private static Material dashMaterial;

    private void Awake()
    {
        unitData = GetComponent<UnitData>();
        mpb = new MaterialPropertyBlock();
    }

    private void Start()
    {
        if (GameConfig.SilentTraining) { enabled = false; return; }
    }

    private void LateUpdate()
    {
        // Lazy-init grid (may not exist at Start time in replay/bootstrap scenarios).
        if (grid == null)
        {
            grid = Object.FindFirstObjectByType<HexGrid>();
            if (grid == null) return;
            var meshGen = Object.FindFirstObjectByType<HexMeshGenerator>();
            outerRadius = meshGen != null ? meshGen.outerRadius : 0.5f;
        }

        if (unitData == null || !unitData.isAlive)
        {
            if (outlineObj != null) outlineObj.SetActive(false);
            return;
        }

        var coord = unitData.currentHex;
        bool posChanged = coord != lastCoord;

        // Recompute adjacency periodically.
        if (posChanged || Time.frameCount % 30 == 0)
        {
            lastCoord = coord;
            bool hasAllies = HasAdjacentAlly(coord, unitData.team);

            if (hasAllies)
            {
                if (outlineObj == null)
                    CreateOutline();

                var worldPos = grid.HexToWorld(coord);
                outlineObj.transform.position = new Vector3(worldPos.x, 0.04f, worldPos.z);
                outlineObj.SetActive(true);

                Color teamColor = unitData.team == Team.Robot
                    ? new Color(0.4f, 0.7f, 1f)
                    : new Color(0.3f, 1f, 0.3f);
                mpb.SetColor(ColorID, teamColor);
                outlineRenderer.SetPropertyBlock(mpb);
            }
            else
            {
                if (outlineObj != null) outlineObj.SetActive(false);
            }
        }

        // Animate: rotate ring around Y axis so dashes appear to flow around the hex.
        if (outlineObj != null && outlineObj.activeSelf)
            outlineObj.transform.Rotate(Vector3.up, RotateSpeed * Time.deltaTime, Space.World);
    }

    private bool HasAdjacentAlly(HexCoord coord, Team team)
    {
        for (int dir = 0; dir < 6; dir++)
        {
            var neighbor = coord.Neighbor(dir);
            var ally = UnitCache.GetAliveAt(neighbor);
            if (ally != null && ally.team == team && ally != unitData)
                return true;
        }
        return false;
    }

    // ── Outline creation ───────────────────────────────────────────────

    private void CreateOutline()
    {
        EnsureDashMaterial();

        outlineObj = new GameObject("AdjacencyOutline");
        outlineObj.SetActive(false);

        var mf = outlineObj.AddComponent<MeshFilter>();
        mf.mesh = CreateDashedRingMesh(outerRadius * 1.04f, outerRadius * 0.94f);

        outlineRenderer = outlineObj.AddComponent<MeshRenderer>();
        outlineRenderer.sharedMaterial = dashMaterial;
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
    }

    /// <summary>
    /// Creates a ring-shaped mesh composed of DashCount separate arc segments
    /// (with gaps between them) following the circular path around the hex.
    /// Each segment is a small quad strip approximating an arc.
    /// </summary>
    private static Mesh CreateDashedRingMesh(float outer, float inner)
    {
        const int segmentResolution = 4; // sub-quads per dash for smooth arc
        var verts = new List<Vector3>();
        var tris = new List<int>();

        float arcPerDash = (Mathf.PI * 2f) / DashCount;
        float dashArc = arcPerDash * DashFillRatio;

        for (int d = 0; d < DashCount; d++)
        {
            float startAngle = d * arcPerDash;
            float endAngle = startAngle + dashArc;

            int baseIdx = verts.Count;

            for (int s = 0; s <= segmentResolution; s++)
            {
                float t = (float)s / segmentResolution;
                float angle = Mathf.Lerp(startAngle, endAngle, t);
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                verts.Add(new Vector3(outer * cos, 0f, outer * sin));
                verts.Add(new Vector3(inner * cos, 0f, inner * sin));
            }

            // Build quads: (outer_i, outer_i+1, inner_i+1) and (outer_i, inner_i+1, inner_i)
            for (int s = 0; s < segmentResolution; s++)
            {
                int o0 = baseIdx + s * 2;
                int i0 = baseIdx + s * 2 + 1;
                int o1 = baseIdx + (s + 1) * 2;
                int i1 = baseIdx + (s + 1) * 2 + 1;

                tris.Add(o0); tris.Add(o1); tris.Add(i0);
                tris.Add(o1); tris.Add(i1); tris.Add(i0);
            }
        }

        var mesh = new Mesh { name = "DashedHexRing" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ── Material ────────────────────────────────────────────────────────

    private static void EnsureDashMaterial()
    {
        if (dashMaterial != null) return;

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        dashMaterial = new Material(shader);
        dashMaterial.SetColor("_BaseColor", Color.white);
        // Opaque — no bloom, no transparent runtime issues.
    }

    // ── Cleanup ────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        if (outlineObj != null)
            Destroy(outlineObj);
    }

    public static Material[] GetStaticMaterials()
    {
        var mats = new[] { dashMaterial };
        dashMaterial = null;
        return mats;
    }
}
