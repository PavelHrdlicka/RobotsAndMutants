using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Highlights valid action targets for the active human unit.
/// Shows colored overlays on hex tiles: green=move, red=attack, blue=build, yellow=destroy.
/// Pre-allocates a pool of highlight objects to avoid runtime allocation and texture leaks.
/// </summary>
public class HexHighlighter : MonoBehaviour
{
    private HexGrid grid;
    private HumanInputManager inputManager;

    private static readonly Color moveColor    = new(0.2f, 0.9f, 0.2f, 0.35f);
    private static readonly Color attackColor  = new(0.9f, 0.2f, 0.2f, 0.35f);
    private static readonly Color buildColor   = new(0.2f, 0.4f, 0.9f, 0.35f);
    private static readonly Color destroyColor = new(0.9f, 0.8f, 0.2f, 0.35f);

    // Pool of reusable highlight objects.
    private const int PoolSize = 7; // max 6 neighbors + 1 self
    private readonly List<GameObject> pool = new(PoolSize);
    private readonly List<Material> poolMaterials = new(PoolSize);
    private int activeCount;

    // Cached shared material (one per color, created once).
    private readonly Dictionary<Color, Material> materialCache = new();

    private HumanActionMode lastMode = (HumanActionMode)(-1);
    private HexCoord lastUnitHex;
    private bool lastWasActive;

    public void Initialize(HexGrid hexGrid, HumanInputManager input)
    {
        grid = hexGrid;
        inputManager = input;
        BuildPool();
    }

    private void BuildPool()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "HexHighlight";
            go.transform.localScale = new Vector3(0.9f, 0.01f, 0.9f);
            go.transform.SetParent(transform);
            go.SetActive(false);

            // Remove collider so it doesn't block raycasts.
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            pool.Add(go);

            // Create a dedicated material for this instance (not shared).
            var mat = CreateTransparentMaterial(Color.clear);
            go.GetComponent<Renderer>().material = mat;
            poolMaterials.Add(mat);
        }
    }

    private Material CreateTransparentMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader);
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_Blend", 0);
        mat.SetFloat("_AlphaClip", 0);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        mat.color = color;
        return mat;
    }

    private void Update()
    {
        if (grid == null || inputManager == null)
            return;

        // Find the active human unit.
        var activeUnit = FindActiveHumanUnit();
        bool isActive = activeUnit != null && activeUnit.isMyTurn && activeUnit.isAlive;

        // Only refresh highlights when state changes.
        if (isActive == lastWasActive
            && inputManager.ActionMode == lastMode
            && isActive && activeUnit.currentHex == lastUnitHex)
            return;

        lastWasActive = isActive;
        lastMode = inputManager.ActionMode;
        lastUnitHex = isActive ? activeUnit.currentHex : default;

        HideAll();

        if (!isActive) return;

        var movement = activeUnit.GetComponent<HexMovement>();
        if (movement == null) return;

        switch (inputManager.ActionMode)
        {
            case HumanActionMode.Move:
                HighlightValidMoves(activeUnit, movement);
                break;
            case HumanActionMode.Attack:
                HighlightValidAttacks(activeUnit, movement);
                break;
            case HumanActionMode.Build:
                HighlightValidBuilds(activeUnit, movement);
                break;
            case HumanActionMode.DestroyWall:
                HighlightValidDestroys(activeUnit, movement);
                break;
        }
    }

    private void HighlightValidMoves(UnitData unit, HexMovement movement)
    {
        for (int dir = 0; dir < 6; dir++)
            if (movement.IsValidMove(dir))
                ShowHighlight(unit.currentHex.Neighbor(dir), moveColor);
    }

    private void HighlightValidAttacks(UnitData unit, HexMovement movement)
    {
        for (int dir = 0; dir < 6; dir++)
            if (movement.IsValidAttack(dir))
                ShowHighlight(unit.currentHex.Neighbor(dir), attackColor);
    }

    private void HighlightValidBuilds(UnitData unit, HexMovement movement)
    {
        if (unit.team == Team.Mutant)
        {
            if (movement.IsValidBuild(0))
                ShowHighlight(unit.currentHex, buildColor);
        }
        else
        {
            for (int dir = 0; dir < 6; dir++)
                if (movement.IsValidBuild(dir))
                    ShowHighlight(unit.currentHex.Neighbor(dir), buildColor);
        }
    }

    private void HighlightValidDestroys(UnitData unit, HexMovement movement)
    {
        for (int dir = 0; dir < 6; dir++)
            if (movement.IsValidDestroyWall(dir))
                ShowHighlight(unit.currentHex.Neighbor(dir), destroyColor);
    }

    private void ShowHighlight(HexCoord coord, Color color)
    {
        if (activeCount >= pool.Count) return;

        var go = pool[activeCount];
        go.transform.position = grid.HexToWorld(coord) + Vector3.up * 0.15f;
        poolMaterials[activeCount].color = color;
        go.SetActive(true);
        activeCount++;
    }

    private void HideAll()
    {
        for (int i = 0; i < activeCount; i++)
            pool[i].SetActive(false);
        activeCount = 0;
    }

    private UnitData FindActiveHumanUnit()
    {
        var units = FindObjectsByType<HumanTurnController>(FindObjectsSortMode.None);
        foreach (var htc in units)
        {
            var ud = htc.GetComponent<UnitData>();
            if (ud != null && ud.isMyTurn && ud.isAlive)
                return ud;
        }
        return null;
    }

    private void OnDestroy()
    {
        // Clean up materials to prevent leaks.
        foreach (var mat in poolMaterials)
            if (mat != null) Destroy(mat);
        poolMaterials.Clear();

        foreach (var go in pool)
            if (go != null) Destroy(go);
        pool.Clear();
    }
}
