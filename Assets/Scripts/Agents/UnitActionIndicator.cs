using UnityEngine;

/// <summary>
/// Visual indicator showing the current action of a unit.
/// Renders a colored pill + symbol floating above the unit via OnGUI world-to-screen.
/// For Move action, draws a yellow arrow from source hex center to target hex center.
/// The arrow persists for arrowDisplayFrames frames so it is visible despite fast step rate.
/// </summary>
[RequireComponent(typeof(UnitData))]
public class UnitActionIndicator : MonoBehaviour
{
    private UnitData unitData;
    private HexGrid grid;
    private Camera cam;

    // Arrow: show for this many real-time seconds after the last move.
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

        // Draw move arrow if timer is running.
        if (arrowTimeLeft > 0f && grid != null)
            DrawMoveArrow(arrowTo);

        // World-to-screen position (above unit head).
        Vector3 worldPos = transform.position + Vector3.up * 0.6f;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        if (screenPos.z < 0) return;

        float x = screenPos.x;
        float y = Screen.height - screenPos.y;

        GetActionVisuals(unitData.lastAction, out string symbol, out Color color, out string label);

        // Background pill.
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

        // HP bar.
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

    /// <summary>
    /// Draw a yellow arrow from the unit's current VISUAL position to the destination hex center.
    /// The arrow shrinks as the unit animates toward its target, disappearing on arrival.
    /// </summary>
    private void DrawMoveArrow(HexCoord to)
    {
        // Start from the unit's current visual position (shrinks as unit moves).
        Vector3 fromWorld = transform.position + Vector3.up * 0.05f;
        Vector3 toWorld   = grid.HexToWorld(to) + Vector3.up * 0.05f;

        // Don't draw if unit has nearly arrived.
        if (Vector3.Distance(fromWorld, toWorld) < 0.08f) return;

        Vector3 fs = cam.WorldToScreenPoint(fromWorld);
        Vector3 ts = cam.WorldToScreenPoint(toWorld);

        if (fs.z < 0 || ts.z < 0) return;

        Vector2 p0 = new Vector2(fs.x, Screen.height - fs.y);
        Vector2 p1 = new Vector2(ts.x, Screen.height - ts.y);

        // Fade out as timer runs down.
        float alpha = Mathf.Clamp01(arrowTimeLeft / (ArrowDisplaySeconds * 0.3f));
        Color arrowColor = new Color(1f, 0.95f, 0.2f, 0.85f * alpha);

        DrawSegmentedLine(p0, p1, 4f, arrowColor);
        DrawArrowHead(p0, p1, 10f, arrowColor);
    }

    /// <summary>
    /// Draw a solid line as overlapping small squares. No matrix rotation.
    /// </summary>
    internal static void DrawSegmentedLine(Vector2 from, Vector2 to, float thickness, Color color)
    {
        Vector2 diff = to - from;
        float dist = diff.magnitude;
        if (dist < 1f) return;

        // Step size = half thickness so squares overlap and form a solid line.
        float step = Mathf.Max(1f, thickness * 0.5f);
        int count = Mathf.CeilToInt(dist / step);
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

    /// <summary>Draw a V-shaped arrowhead at the 'to' end using two line segments.</summary>
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
            case UnitAction.Move:
                symbol = ">>"; color = new Color(0.9f, 0.9f, 0.9f); label = "move"; break;
            case UnitAction.Attack:
                symbol = "X"; color = new Color(1f, 0.25f, 0.2f); label = "fight"; break;
            case UnitAction.BuildCrate:
                symbol = "#"; color = new Color(1f, 0.7f, 0.2f); label = "crate"; break;
            case UnitAction.SpreadSlime:
                symbol = "~"; color = new Color(0.4f, 1f, 0.3f); label = "slime"; break;
            case UnitAction.Capture:
                symbol = "+"; color = new Color(0.3f, 0.8f, 1f); label = "claim"; break;
            case UnitAction.Dead:
                symbol = ""; color = Color.gray; label = ""; break;
            default:
                symbol = "-"; color = new Color(0.5f, 0.5f, 0.5f); label = "idle"; break;
        }
    }
}
