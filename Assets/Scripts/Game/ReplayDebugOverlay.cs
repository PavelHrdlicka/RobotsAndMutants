using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Debug overlay for replay mode: shows unit index numbers above units
/// and axial coordinates (q,r) on each hex tile. Toggled via SHOW DETAIL button.
/// </summary>
public class ReplayDebugOverlay : MonoBehaviour
{
    public bool showDetail;

    private readonly List<GameObject> unitLabels = new();
    private readonly List<GameObject> hexLabels = new();
    private bool built;

    private Camera cam;

    public void Toggle()
    {
        showDetail = !showDetail;

        if (showDetail && !built)
            Build();

        foreach (var label in unitLabels)
            if (label != null) label.SetActive(showDetail);
        foreach (var label in hexLabels)
            if (label != null) label.SetActive(showDetail);
    }

    private void Build()
    {
        built = true;
        cam = Camera.main;

        // Unit labels.
        var factory = FindFirstObjectByType<UnitFactory>();
        if (factory != null)
        {
            foreach (var unit in factory.AllUnits)
            {
                var label = CreateLabel(
                    $"UnitLabel_{unit.gameObject.name}",
                    unit.unitIndex.ToString(),
                    unit.team == Team.Robot ? new Color(0.5f, 0.7f, 1f) : new Color(0.5f, 1f, 0.5f),
                    0.10f);
                label.transform.SetParent(unit.transform, false);
                label.transform.localPosition = new Vector3(0f, 0.65f, 0f);
                unitLabels.Add(label);
            }
        }

        // Hex coordinate labels.
        var grid = FindFirstObjectByType<HexGrid>();
        if (grid != null)
        {
            foreach (var kvp in grid.Tiles)
            {
                var coord = kvp.Key;
                var tile = kvp.Value;
                var label = CreateLabel(
                    $"HexLabel_{coord.q}_{coord.r}",
                    $"{coord.q},{coord.r}",
                    new Color(1f, 1f, 1f, 0.8f),
                    0.03f);
                // Place slightly above tile surface.
                Vector3 pos = grid.HexToWorld(coord);
                pos.y = tile.isBase ? 0.15f : 0.05f;
                label.transform.position = pos;
                hexLabels.Add(label);
            }
        }
    }

    private void LateUpdate()
    {
        if (!showDetail || cam == null) return;

        // Billboard all labels toward camera.
        Quaternion rot = cam.transform.rotation;
        foreach (var label in unitLabels)
            if (label != null && label.activeSelf)
                label.transform.rotation = rot;
        foreach (var label in hexLabels)
            if (label != null && label.activeSelf)
                label.transform.rotation = rot;
    }

    private static GameObject CreateLabel(string name, string text, Color color, float charSize)
    {
        var go = new GameObject(name);
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.characterSize = charSize;
        tm.fontSize = 80;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.fontStyle = FontStyle.Bold;

        // Keep default font material (works in URP). Just disable shadows.
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        go.SetActive(false);
        return go;
    }

    private void OnDestroy()
    {
        foreach (var label in hexLabels)
            if (label != null) Destroy(label);
        hexLabels.Clear();
        // Unit labels are children of units — destroyed with them.
        unitLabels.Clear();
    }
}
