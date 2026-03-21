using UnityEngine;

/// <summary>
/// 3D energy bar floating above the unit.
/// Single continuous bar with color gradient based on energy fraction.
/// Billboards toward the camera every frame.
/// </summary>
[RequireComponent(typeof(UnitData))]
public class UnitHealthBar3D : MonoBehaviour
{
    private UnitData unitData;
    private Transform barRoot;
    private Transform barFill;
    private Transform barBg;
    private Renderer fillRenderer;
    private Renderer bgRenderer;
    private Camera cam;

    private static Material barMaterial;

    private const float BarWidth  = 0.14f;
    private const float BarHeight = 0.018f;
    private const float BarDepth  = 0.005f;
    private const float BarY      = 0.40f;

    private static readonly Color FullColor  = new Color(0.2f, 0.95f, 0.2f);
    private static readonly Color MidColor   = new Color(0.95f, 0.95f, 0.2f);
    private static readonly Color LowColor   = new Color(0.95f, 0.2f, 0.2f);
    private static readonly Color BgColor    = new Color(0.15f, 0.15f, 0.15f, 0.5f);

    private void Awake()
    {
        unitData = GetComponent<UnitData>();
        cam = Camera.main;
        BuildBar();
    }

    private void BuildBar()
    {
        EnsureMaterial();

        barRoot = new GameObject("EnergyBar").transform;
        barRoot.SetParent(transform, false);
        barRoot.localPosition = new Vector3(0f, BarY, 0f);

        // Background (full width, dark).
        var bgGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bgGo.name = "BarBg";
        bgGo.transform.SetParent(barRoot, false);
        bgGo.transform.localPosition = Vector3.zero;
        bgGo.transform.localScale = new Vector3(BarWidth, BarHeight, BarDepth);
        var col = bgGo.GetComponent<Collider>();
        if (col != null) Destroy(col);
        bgRenderer = bgGo.GetComponent<Renderer>();
        bgRenderer.material = new Material(barMaterial);
        bgRenderer.material.SetColor("_BaseColor", BgColor);
        barBg = bgGo.transform;

        // Fill (scales with energy fraction).
        var fillGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fillGo.name = "BarFill";
        fillGo.transform.SetParent(barRoot, false);
        fillGo.transform.localScale = new Vector3(BarWidth, BarHeight, BarDepth + 0.001f);
        col = fillGo.GetComponent<Collider>();
        if (col != null) Destroy(col);
        fillRenderer = fillGo.GetComponent<Renderer>();
        fillRenderer.material = new Material(barMaterial);
        barFill = fillGo.transform;
    }

    private void LateUpdate()
    {
        if (barRoot == null || unitData == null) return;

        // Billboard: face camera.
        if (cam == null) cam = Camera.main;
        if (cam != null)
            barRoot.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);

        // Update fill width and color.
        float frac = unitData.maxEnergy > 0 ? unitData.Energy / (float)unitData.maxEnergy : 0f;
        frac = Mathf.Clamp01(frac);

        Color activeColor = frac > 0.6f ? FullColor :
                            frac > 0.3f ? MidColor  : LowColor;

        fillRenderer.material.SetColor("_BaseColor", activeColor);

        // Scale fill bar horizontally, anchor left.
        float fillWidth = BarWidth * frac;
        barFill.localScale = new Vector3(fillWidth, BarHeight, BarDepth + 0.001f);
        barFill.localPosition = new Vector3((fillWidth - BarWidth) * 0.5f, 0f, 0f);
    }

    private static void EnsureMaterial()
    {
        if (barMaterial != null) return;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        barMaterial = new Material(shader);
    }
}
