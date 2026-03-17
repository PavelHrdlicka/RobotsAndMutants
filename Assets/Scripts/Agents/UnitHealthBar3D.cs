using UnityEngine;

/// <summary>
/// 3D segmented health bar floating above the unit.
/// 5 individual cubes — each represents one energy point.
/// Billboards toward the camera every frame.
/// </summary>
[RequireComponent(typeof(UnitData))]
public class UnitHealthBar3D : MonoBehaviour
{
    private UnitData unitData;
    private Transform barRoot;
    private Transform[] segments;
    private Renderer[] segRenderers;
    private Camera cam;

    private static Material segMaterial;

    private const int MaxSegments = 5;
    private const float SegWidth  = 0.025f;
    private const float SegHeight = 0.018f;
    private const float SegGap    = 0.004f;
    private const float BarY      = 0.40f; // above unit root

    private static readonly Color FullColor  = new Color(0.2f, 0.95f, 0.2f);
    private static readonly Color MidColor   = new Color(0.95f, 0.95f, 0.2f);
    private static readonly Color LowColor   = new Color(0.95f, 0.2f, 0.2f);
    private static readonly Color EmptyColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);

    private void Awake()
    {
        unitData = GetComponent<UnitData>();
        cam = Camera.main;
        BuildBar();
    }

    private void BuildBar()
    {
        EnsureMaterial();

        barRoot = new GameObject("HealthBar").transform;
        barRoot.SetParent(transform, false);
        barRoot.localPosition = new Vector3(0f, BarY, 0f);

        segments    = new Transform[MaxSegments];
        segRenderers = new Renderer[MaxSegments];

        float totalWidth = MaxSegments * SegWidth + (MaxSegments - 1) * SegGap;
        float startX = -totalWidth * 0.5f + SegWidth * 0.5f;

        for (int i = 0; i < MaxSegments; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Seg{i}";
            go.transform.SetParent(barRoot, false);
            go.transform.localPosition = new Vector3(startX + i * (SegWidth + SegGap), 0f, 0f);
            go.transform.localScale = new Vector3(SegWidth, SegHeight, 0.005f);

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = go.GetComponent<Renderer>();
            rend.material = new Material(segMaterial);
            segments[i]    = go.transform;
            segRenderers[i] = rend;
        }
    }

    private void LateUpdate()
    {
        if (barRoot == null || unitData == null) return;

        // Billboard: face camera.
        if (cam == null) cam = Camera.main;
        if (cam != null)
            barRoot.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);

        // Update segment colors.
        int hp = unitData.Health;
        float hpFrac = hp / (float)unitData.maxHealth;

        Color activeColor = hpFrac > 0.6f ? FullColor :
                            hpFrac > 0.3f ? MidColor  : LowColor;

        for (int i = 0; i < MaxSegments; i++)
        {
            if (segRenderers[i] == null) continue;
            Color c = (i < hp) ? activeColor : EmptyColor;
            segRenderers[i].material.SetColor("_BaseColor", c);
        }
    }

    private static void EnsureMaterial()
    {
        if (segMaterial != null) return;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        segMaterial = new Material(shader);
    }
}
