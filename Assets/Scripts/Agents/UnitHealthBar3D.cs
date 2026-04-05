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
    private TextMesh energyText;
    private Camera cam;

    // Model greying.
    private Renderer[] modelRenderers;
    private Color[] originalColors;
    private MaterialPropertyBlock[] greyBlocks;
    private float prevFrac = -1f;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private MaterialPropertyBlock fillBlock;
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
        fillBlock = new MaterialPropertyBlock();
        barFill = fillGo.transform;

        // Energy number text.
        var textGo = new GameObject("EnergyText");
        textGo.transform.SetParent(barRoot, false);
        textGo.transform.localPosition = new Vector3(BarWidth * 0.5f + 0.02f, 0f, 0f);
        energyText = textGo.AddComponent<TextMesh>();
        energyText.fontSize = 40;
        energyText.characterSize = 0.025f;
        energyText.anchor = TextAnchor.MiddleLeft;
        energyText.alignment = TextAlignment.Left;
        energyText.color = Color.white;
        energyText.fontStyle = FontStyle.Bold;
        var tr = textGo.GetComponent<MeshRenderer>();
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void Start()
    {
        // Cache model part renderers for greying effect.
        // ModelRoot is the child containing all visual parts.
        var modelRoot = transform.Find("ModelRoot");
        if (modelRoot != null)
        {
            modelRenderers = modelRoot.GetComponentsInChildren<Renderer>();
            originalColors = new Color[modelRenderers.Length];
            greyBlocks = new MaterialPropertyBlock[modelRenderers.Length];
            for (int i = 0; i < modelRenderers.Length; i++)
            {
                greyBlocks[i] = new MaterialPropertyBlock();
                var mat = modelRenderers[i].sharedMaterial;
                originalColors[i] = mat.HasProperty("_BaseColor")
                    ? mat.GetColor("_BaseColor")
                    : mat.color;
            }
        }
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

        fillBlock.SetColor(BaseColorId, activeColor);
        fillRenderer.SetPropertyBlock(fillBlock);

        // Scale fill bar horizontally, anchor left.
        float fillWidth = BarWidth * frac;
        barFill.localScale = new Vector3(fillWidth, BarHeight, BarDepth + 0.001f);
        barFill.localPosition = new Vector3((fillWidth - BarWidth) * 0.5f, 0f, 0f);

        // Energy number or respawn cooldown.
        if (energyText != null)
        {
            if (!unitData.isAlive && unitData.respawnCooldown > 0)
                energyText.text = $"\u23F3{unitData.respawnCooldown}"; // ⏳ + cooldown
            else
                energyText.text = unitData.Energy.ToString();
        }

        // Grey out model parts from bottom to top based on lost energy.
        if (modelRenderers != null && Mathf.Abs(frac - prevFrac) > 0.01f)
        {
            prevFrac = frac;
            UpdateModelGreying(frac);
        }
    }

    private static readonly Color GreyColor = new Color(0.25f, 0.25f, 0.25f);
    private static readonly Color DeadColor = new Color(0.08f, 0.08f, 0.08f);

    private void UpdateModelGreying(float energyFrac)
    {
        if (modelRenderers == null || greyBlocks == null) return;

        // Dead unit → fully black.
        bool isDead = unitData != null && !unitData.isAlive;
        if (isDead)
        {
            for (int i = 0; i < modelRenderers.Length; i++)
            {
                if (modelRenderers[i] == null) continue;
                greyBlocks[i].SetColor(BaseColorId, DeadColor);
                modelRenderers[i].SetPropertyBlock(greyBlocks[i]);
            }
            return;
        }

        int total = modelRenderers.Length;
        float greyFrac = 1f - energyFrac;
        float greyParts = greyFrac * total;

        for (int i = 0; i < total; i++)
        {
            if (modelRenderers[i] == null) continue;

            float partProgress = total - 1 - i;
            float t;

            if (partProgress < greyParts - 1f)
                t = 1f;
            else if (partProgress < greyParts)
                t = greyParts - partProgress;
            else
                t = 0f;

            Color c = Color.Lerp(originalColors[i], GreyColor, t);
            greyBlocks[i].SetColor(BaseColorId, c);
            modelRenderers[i].SetPropertyBlock(greyBlocks[i]);
        }
    }

    public static Material[] GetStaticMaterials()
    {
        var mats = new[] { barMaterial };
        barMaterial = null;
        return mats;
    }

    private static void EnsureMaterial()
    {
        if (barMaterial != null) return;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        barMaterial = new Material(shader);
    }
}
