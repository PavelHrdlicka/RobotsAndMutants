using UnityEngine;

/// <summary>
/// Visual indicator showing the current action of a unit.
///
/// Move:  no pill/symbol — the smooth visual animation + yellow directional arrow is enough.
/// Idle:  a grey circular arrow orbiting above the unit's head (animated, spinning).
/// Other: coloured pill with symbol + label (Attack, BuildCrate, SpreadSlime, Capture).
///
/// HP bar always drawn below the indicator.
/// </summary>
[RequireComponent(typeof(UnitData))]
public class UnitActionIndicator : MonoBehaviour
{
    private UnitData unitData;
    private HexGrid grid;
    private Camera cam;

    // Move arrow: timer + destination.
    private float arrowTimeLeft;
    private HexCoord arrowTo;
    private const float ArrowDisplaySeconds = 1.2f;

    private static GUIStyle symbolStyle;
    private static GUIStyle labelStyle;
    private static Texture2D bgTex;
    private static bool stylesReady;

    private void Awake()
    {
        unitData = GetComponent<UnitData>();
    }

    private void Start()
    {
        cam = Camera.main;
        grid = Object.FindFirstObjectByType<HexGrid>();
    }

    private void Update()
    {
        if (grid == null)
            grid = Object.FindFirstObjectByType<HexGrid>();

        if (arrowTimeLeft > 0f)
            arrowTimeLeft -= Time.unscaledDeltaTime;
    }

    /// <summary>
    /// Called by HexMovement immediately when a move is executed,
    /// before territory/combat systems can overwrite lastAction.
    /// </summary>
    public void OnMoveStarted(HexCoord to)
    {
        arrowTo       = to;
        arrowTimeLeft = ArrowDisplaySeconds;
    }

    private static void InitStyles()
    {
        if (stylesReady) return;
        stylesReady = true;

        bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, new Color(0, 0, 0, 0.6f));
        bgTex.Apply();

        symbolStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 9,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
        };
    }

    private void OnGUI()
    {
        if (unitData == null || !unitData.isAlive || cam == null) return;
        InitStyles();

        // Move arrow (yellow, directional, fading).
        if (arrowTimeLeft > 0f && grid != null)
            DrawMoveArrow(arrowTo);

        // Screen position above unit head.
        Vector3 worldPos  = transform.position + Vector3.up * 0.6f;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
        if (screenPos.z < 0) return;

        float x = screenPos.x;
        float y = Screen.height - screenPos.y;

        // ── Action-specific visuals ──────────────────────────────────
        var action = unitData.lastAction;

        if (action == UnitAction.Idle)
        {
            // Spinning circular arrow (no pill).
            DrawIdleOrbit(x, y);
        }
        else if (action != UnitAction.Move && action != UnitAction.Dead)
        {
            // Pill + symbol for Attack, BuildCrate, SpreadSlime, Capture.
            GetActionVisuals(action, out string symbol, out Color color, out string label);
            DrawPill(x, y, symbol, color, label);
        }
        // Move & Dead: no pill drawn (move has the directional arrow, dead is hidden).

        // ── HP bar (always) ──────────────────────────────────────────
        DrawHpBar(x, y);
    }

    // ─── Drawing helpers ─────────────────────────────────────────────

    private void DrawPill(float x, float y, string symbol, Color color, string label)
    {
        float pillW = 28f;
        float pillH = 30f;
        Rect pillRect = new Rect(x - pillW * 0.5f, y - pillH - 4f, pillW, pillH);

        GUI.color = new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.7f);
        GUI.DrawTexture(pillRect, bgTex);

        GUI.color = color;
        GUI.DrawTexture(new Rect(pillRect.x, pillRect.y, pillRect.width, 3f), Texture2D.whiteTexture);

        GUI.color = color;
        GUI.Label(new Rect(pillRect.x, pillRect.y + 2f, pillRect.width, 18f), symbol, symbolStyle);

        GUI.color = Color.white;
        GUI.Label(new Rect(pillRect.x - 6f, pillRect.y + 17f, pillRect.width + 12f, 12f), label, labelStyle);
    }

    private void DrawHpBar(float x, float y)
    {
        float hpFrac = unitData.Health / (float)unitData.maxHealth;
        float barW = 22f;
        float barH = 3f;
        float barX = x - barW * 0.5f;
        float barY = y - 2f;

        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.6f);
        GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);

        Color hpColor = hpFrac > 0.6f ? new Color(0.2f, 0.9f, 0.2f) :
                        hpFrac > 0.3f ? new Color(0.9f, 0.9f, 0.2f) :
                                         new Color(0.9f, 0.2f, 0.2f);
        GUI.color = hpColor;
        GUI.DrawTexture(new Rect(barX, barY, barW * hpFrac, barH), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    // ─── Idle: spinning circular arrow ───────────────────────────────

    /// <summary>
    /// Draw a circular arrow orbiting above the unit (same visual style as the move
    /// arrow — overlapping squares — but arranged in a circle, with an arrowhead at
    /// the leading end). The whole thing rotates continuously via Time.unscaledTime.
    /// </summary>
    private void DrawIdleOrbit(float cx, float cy)
    {
        float radius    = 12f;
        float thickness = 3f;
        float centerY   = cy - 22f; // above HP bar

        Color idleColor = new Color(0.55f, 0.55f, 0.55f, 0.65f);

        // Spin: one full revolution per 2 real-seconds.
        float spin = Time.unscaledTime * Mathf.PI; // rad/s — full circle in 2 s

        // Draw an arc of ~270 degrees (leave a gap for the arrowhead to stand out).
        const float arcDeg   = 270f;
        const int   segments = 24;
        float arcRad  = arcDeg * Mathf.Deg2Rad;
        float step    = arcRad / segments;
        float half    = thickness * 0.5f;

        GUI.color = idleColor;
        Vector2 prev = Vector2.zero;
        Vector2 first = Vector2.zero;

        for (int i = 0; i <= segments; i++)
        {
            float angle = spin + i * step;
            Vector2 p = new Vector2(
                cx + Mathf.Cos(angle) * radius,
                centerY + Mathf.Sin(angle) * radius);

            if (i == 0) { first = p; prev = p; continue; }

            // Draw small square at each sample point along the arc.
            GUI.DrawTexture(new Rect(p.x - half, p.y - half, thickness, thickness),
                            Texture2D.whiteTexture);
            prev = p;
        }

        // Arrowhead at the leading end (last point of the arc).
        float headAngle = spin + arcRad;
        Vector2 tip = new Vector2(
            cx + Mathf.Cos(headAngle) * radius,
            centerY + Mathf.Sin(headAngle) * radius);

        // Tangent direction at the tip (perpendicular to radius, pointing along arc).
        Vector2 tangent = new Vector2(
            -Mathf.Sin(headAngle),
             Mathf.Cos(headAngle));
        Vector2 normal = new Vector2(tangent.y, -tangent.x);

        float headSize = 7f;
        Vector2 wing1 = tip - tangent * headSize + normal * (headSize * 0.45f);
        Vector2 wing2 = tip - tangent * headSize - normal * (headSize * 0.45f);

        DrawSegmentedLine(tip, wing1, 2.5f, idleColor);
        DrawSegmentedLine(tip, wing2, 2.5f, idleColor);

        GUI.color = Color.white;
    }

    // ─── Move: directional arrow ─────────────────────────────────────

    private void DrawMoveArrow(HexCoord to)
    {
        Vector3 fromWorld = transform.position + Vector3.up * 0.05f;
        Vector3 toWorld   = grid.HexToWorld(to) + Vector3.up * 0.05f;

        if (Vector3.Distance(fromWorld, toWorld) < 0.08f) return;

        Vector3 fs = cam.WorldToScreenPoint(fromWorld);
        Vector3 ts = cam.WorldToScreenPoint(toWorld);
        if (fs.z < 0 || ts.z < 0) return;

        Vector2 p0 = new Vector2(fs.x, Screen.height - fs.y);
        Vector2 p1 = new Vector2(ts.x, Screen.height - ts.y);

        float alpha = Mathf.Clamp01(arrowTimeLeft / (ArrowDisplaySeconds * 0.3f));
        Color arrowColor = new Color(1f, 0.95f, 0.2f, 0.85f * alpha);

        DrawSegmentedLine(p0, p1, 4f, arrowColor);
        DrawArrowHead(p0, p1, 10f, arrowColor);
    }

    // ─── Shared line / arrowhead primitives ──────────────────────────

    internal static void DrawSegmentedLine(Vector2 from, Vector2 to, float thickness, Color color)
    {
        Vector2 diff = to - from;
        float dist = diff.magnitude;
        if (dist < 1f) return;

        float step = Mathf.Max(1f, thickness * 0.5f);
        int count  = Mathf.CeilToInt(dist / step);
        float half = thickness * 0.5f;

        GUI.color = color;
        for (int i = 0; i <= count; i++)
        {
            float t = i / (float)count;
            Vector2 p = Vector2.Lerp(from, to, t);
            GUI.DrawTexture(new Rect(p.x - half, p.y - half, thickness, thickness),
                            Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }

    private static void DrawArrowHead(Vector2 from, Vector2 to, float size, Color color)
    {
        Vector2 dir   = (to - from).normalized;
        Vector2 right = new Vector2(-dir.y, dir.x);

        Vector2 base1 = to - dir * size + right * (size * 0.5f);
        Vector2 base2 = to - dir * size - right * (size * 0.5f);

        DrawSegmentedLine(to, base1, 3f, color);
        DrawSegmentedLine(to, base2, 3f, color);
    }

    private static void GetActionVisuals(UnitAction action, out string symbol, out Color color, out string label)
    {
        switch (action)
        {
            case UnitAction.Attack:
                symbol = "X"; color = new Color(1f, 0.25f, 0.2f); label = "fight"; break;
            case UnitAction.BuildCrate:
                symbol = "#"; color = new Color(1f, 0.7f, 0.2f); label = "crate"; break;
            case UnitAction.SpreadSlime:
                symbol = "~"; color = new Color(0.4f, 1f, 0.3f); label = "slime"; break;
            case UnitAction.Capture:
                symbol = "+"; color = new Color(0.3f, 0.8f, 1f); label = "claim"; break;
            default:
                symbol = ""; color = Color.gray; label = ""; break;
        }
    }
}
