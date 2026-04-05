using UnityEngine;

/// <summary>
/// 3D world-space action indicators above unit head.
/// Replaces the OnGUI-based UnitActionIndicator.
///
/// Keeps the OnMoveStarted(HexCoord) interface used by HexMovement.
/// </summary>
[RequireComponent(typeof(UnitData))]
public class UnitActionIndicator3D : MonoBehaviour
{
    private UnitData unitData;
    private HexGrid grid;
    private Camera cam;

    // Indicator root (above head).
    private Transform indicatorRoot;
    private const float IndicatorY = 0.48f;

    // Active turn glow (pulsing emission on model renderers).
    private Renderer[] modelRenderers;
    private Color[] originalEmission;
    private MaterialPropertyBlock[] glowBlocks;
    private bool glowActive;

    // Move arrow.
    private Transform moveArrow;
    private float arrowTimeLeft;
    private const float ArrowDisplaySeconds = 1.2f;

    // Idle ring.
    private Transform idleRing;
    private float idleTimeLeft;
    private const float IdleDisplaySeconds = 1.0f;
    private UnitAction prevAction;

    // Attack (crossed swords).
    private Transform attackIcon;

    // Capture.
    private Transform captureIcon;

    // Build.
    private Transform buildIcon;

    private static Material redMat, blueMat, yellowMat, greyMat, cyanMat, orangeMat;

    private void Awake()
    {
        unitData = GetComponent<UnitData>();
        cam = Camera.main;
        InitMaterials();
        BuildIndicators();
    }

    private void Start()
    {
        grid = Object.FindFirstObjectByType<HexGrid>();
        prevAction = unitData != null ? unitData.lastAction : UnitAction.Move;
    }

    private void Update()
    {
        if (grid == null) grid = Object.FindFirstObjectByType<HexGrid>();
        if (unitData == null) return;

        // Timers.
        if (arrowTimeLeft > 0f) arrowTimeLeft -= Time.unscaledDeltaTime;
        if (idleTimeLeft  > 0f) idleTimeLeft  -= Time.unscaledDeltaTime;

        // Detect idle start.
        if (unitData.lastAction == UnitAction.Idle && prevAction != UnitAction.Idle)
            idleTimeLeft = IdleDisplaySeconds;

        // Auto-clear idle.
        if (unitData.lastAction == UnitAction.Idle && idleTimeLeft <= 0f)
            unitData.lastAction = UnitAction.Move;

        prevAction = unitData.lastAction;

        UpdateVisibility();
        UpdateTurnGlow();

        // Rotate idle ring.
        if (idleRing != null && idleRing.gameObject.activeSelf)
            idleRing.Rotate(Vector3.up, 180f * Time.unscaledDeltaTime, Space.Self);

        // Billboard indicator root toward camera.
        if (cam == null) cam = Camera.main;
        if (cam != null && indicatorRoot != null)
            indicatorRoot.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
    }

    /// <summary>Called by HexMovement when a move starts.</summary>
    public void OnMoveStarted(HexCoord to)
    {
        arrowTimeLeft = ArrowDisplaySeconds;

        // Point the arrow toward the destination.
        if (moveArrow != null && grid != null)
        {
            Vector3 from = transform.position;
            Vector3 dest = grid.HexToWorld(to) + Vector3.up * 0.3f;
            Vector3 dir  = (dest - from);
            dir.y = 0;
            if (dir.sqrMagnitude > 0.001f)
                moveArrow.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }

    private void UpdateVisibility()
    {
        var action = unitData.lastAction;

        bool showArrow   = arrowTimeLeft > 0f;
        bool showIdle    = action == UnitAction.Idle && idleTimeLeft > 0f;
        bool showAttack  = action == UnitAction.Attack;
        bool showCapture = action == UnitAction.Capture;
        bool showBuild   = action == UnitAction.BuildWall || action == UnitAction.PlaceSlime
                          || action == UnitAction.DestroyWall;

        if (moveArrow  != null) moveArrow.gameObject.SetActive(showArrow);
        if (idleRing   != null) idleRing.gameObject.SetActive(showIdle);
        if (attackIcon != null) attackIcon.gameObject.SetActive(showAttack);
        if (captureIcon!= null) captureIcon.gameObject.SetActive(showCapture);
        if (buildIcon  != null) buildIcon.gameObject.SetActive(showBuild);

        // Fade arrow via scale.
        if (showArrow && moveArrow != null)
        {
            float alpha = Mathf.Clamp01(arrowTimeLeft / (ArrowDisplaySeconds * 0.3f));
            moveArrow.localScale = Vector3.one * alpha;
        }
    }

    // ── Active turn glow ────────────────────────────────────────────

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void UpdateTurnGlow()
    {
        if (modelRenderers == null || modelRenderers.Length == 0) return;

        bool shouldGlow = unitData.isMyTurn && unitData.isAlive;

        if (shouldGlow)
        {
            if (!glowActive)
            {
                // Enable emission keyword on shared materials (one-time, not per-frame).
                foreach (var rend in modelRenderers)
                {
                    if (rend == null) continue;
                    rend.sharedMaterial.EnableKeyword("_EMISSION");
                }
                glowActive = true;
            }

            // Pulsing emission intensity via MaterialPropertyBlock (zero allocation).
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            Color glowColor = unitData.team == Team.Robot
                ? new Color(0.3f, 0.5f, 1f) * (1.5f + pulse * 1.5f)
                : new Color(0.3f, 1f, 0.3f) * (1.5f + pulse * 1.5f);

            for (int i = 0; i < modelRenderers.Length; i++)
            {
                if (modelRenderers[i] == null) continue;
                glowBlocks[i].SetColor(EmissionColorId, glowColor);
                modelRenderers[i].SetPropertyBlock(glowBlocks[i]);
            }
        }
        else if (glowActive)
        {
            // Turn off emission via PropertyBlock.
            for (int i = 0; i < modelRenderers.Length; i++)
            {
                if (modelRenderers[i] == null) continue;
                glowBlocks[i].SetColor(EmissionColorId, Color.black);
                modelRenderers[i].SetPropertyBlock(glowBlocks[i]);
                modelRenderers[i].sharedMaterial.DisableKeyword("_EMISSION");
            }
            glowActive = false;
        }
    }

    private void CacheModelRenderers()
    {
        var modelRoot = transform.Find("ModelRoot");
        if (modelRoot != null)
        {
            modelRenderers = modelRoot.GetComponentsInChildren<Renderer>();
            originalEmission = new Color[modelRenderers.Length];
            glowBlocks = new MaterialPropertyBlock[modelRenderers.Length];
            for (int i = 0; i < modelRenderers.Length; i++)
            {
                glowBlocks[i] = new MaterialPropertyBlock();
                var mat = modelRenderers[i].sharedMaterial;
                originalEmission[i] = mat.HasProperty("_EmissionColor")
                    ? mat.GetColor("_EmissionColor")
                    : Color.black;
            }
        }
        else
        {
            modelRenderers = System.Array.Empty<Renderer>();
            originalEmission = System.Array.Empty<Color>();
            glowBlocks = System.Array.Empty<MaterialPropertyBlock>();
        }
    }

    // ── Build indicator objects ──────────────────────────────────────────

    private void BuildIndicators()
    {
        indicatorRoot = new GameObject("Indicators").transform;
        indicatorRoot.SetParent(transform, false);
        indicatorRoot.localPosition = new Vector3(0f, IndicatorY, 0f);

        CacheModelRenderers();
        moveArrow   = BuildArrow();
        idleRing    = BuildIdleRing();
        attackIcon  = BuildCrossedSwords();
        captureIcon = BuildPlusSign();
        buildIcon   = BuildBuildIcon();

        // All start hidden.
        moveArrow.gameObject.SetActive(false);
        idleRing.gameObject.SetActive(false);
        attackIcon.gameObject.SetActive(false);
        captureIcon.gameObject.SetActive(false);
        buildIcon.gameObject.SetActive(false);
    }

    private Transform BuildArrow()
    {
        var root = new GameObject("MoveArrow").transform;
        root.SetParent(indicatorRoot, false);

        // Shaft (elongated cube).
        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "Shaft";
        shaft.transform.SetParent(root, false);
        shaft.transform.localScale = new Vector3(0.008f, 0.008f, 0.06f);
        shaft.transform.localPosition = new Vector3(0, 0, 0.02f);
        shaft.GetComponent<Renderer>().material = yellowMat;
        DestroyCol(shaft);

        // Head (small cube rotated 45°).
        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        head.transform.SetParent(root, false);
        head.transform.localScale = new Vector3(0.02f, 0.008f, 0.02f);
        head.transform.localPosition = new Vector3(0, 0, 0.06f);
        head.transform.localRotation = Quaternion.Euler(0, 45, 0);
        head.GetComponent<Renderer>().material = yellowMat;
        DestroyCol(head);

        return root;
    }

    private Transform BuildIdleRing()
    {
        var root = new GameObject("IdleRing").transform;
        root.SetParent(indicatorRoot, false);

        // Approximate torus with small spheres.
        const int count = 10;
        float radius = 0.035f;
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i * Mathf.Deg2Rad;
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = $"Dot{i}";
            dot.transform.SetParent(root, false);
            dot.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            dot.transform.localScale = Vector3.one * 0.01f;
            dot.GetComponent<Renderer>().material = greyMat;
            DestroyCol(dot);
        }

        return root;
    }

    private Transform BuildCrossedSwords()
    {
        var root = new GameObject("AttackIcon").transform;
        root.SetParent(indicatorRoot, false);

        // Two crossed bars.
        var bar1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar1.name = "Sword1";
        bar1.transform.SetParent(root, false);
        bar1.transform.localScale = new Vector3(0.06f, 0.008f, 0.008f);
        bar1.transform.localRotation = Quaternion.Euler(0, 0, 45);
        bar1.GetComponent<Renderer>().material = redMat;
        DestroyCol(bar1);

        var bar2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar2.name = "Sword2";
        bar2.transform.SetParent(root, false);
        bar2.transform.localScale = new Vector3(0.06f, 0.008f, 0.008f);
        bar2.transform.localRotation = Quaternion.Euler(0, 0, -45);
        bar2.GetComponent<Renderer>().material = redMat;
        DestroyCol(bar2);

        return root;
    }

    private Transform BuildPlusSign()
    {
        var root = new GameObject("CaptureIcon").transform;
        root.SetParent(indicatorRoot, false);

        var h = GameObject.CreatePrimitive(PrimitiveType.Cube);
        h.name = "H";
        h.transform.SetParent(root, false);
        h.transform.localScale = new Vector3(0.04f, 0.008f, 0.008f);
        h.GetComponent<Renderer>().material = cyanMat;
        DestroyCol(h);

        var v = GameObject.CreatePrimitive(PrimitiveType.Cube);
        v.name = "V";
        v.transform.SetParent(root, false);
        v.transform.localScale = new Vector3(0.008f, 0.04f, 0.008f);
        v.GetComponent<Renderer>().material = cyanMat;
        DestroyCol(v);

        return root;
    }

    private Transform BuildBuildIcon()
    {
        var root = new GameObject("BuildIcon").transform;
        root.SetParent(indicatorRoot, false);

        // Small upward-pointing triangle from cubes.
        var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = "Wrench";
        bar.transform.SetParent(root, false);
        bar.transform.localScale = new Vector3(0.035f, 0.035f, 0.008f);
        bar.transform.localRotation = Quaternion.Euler(0, 0, 45);
        bar.GetComponent<Renderer>().material = orangeMat;
        DestroyCol(bar);

        return root;
    }

    // ── Material helpers ────────────────────────────────────────────────

    private static void InitMaterials()
    {
        if (redMat != null) return;
        redMat    = MakeUnlit(new Color(1f, 0.25f, 0.2f));
        blueMat   = MakeUnlit(new Color(0.3f, 0.6f, 1f));
        yellowMat = MakeUnlit(new Color(1f, 0.95f, 0.2f));
        greyMat   = MakeUnlit(new Color(0.55f, 0.55f, 0.55f));
        cyanMat   = MakeUnlit(new Color(0.3f, 0.9f, 1f));
        orangeMat = MakeUnlit(new Color(1f, 0.7f, 0.2f));
    }

    private static Material MakeUnlit(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        return mat;
    }

    private static void DestroyCol(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }
}
