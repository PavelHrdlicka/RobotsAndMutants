using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles discrete hex-to-hex movement for a unit.
///
/// Logical position (currentHex) updates instantly every tick.
/// Visual position (transform.position) works through a Queue of waypoints,
/// completing each hop before starting the next — guaranteeing smooth animation
/// regardless of tick rate or frame rate.
///
/// Speed is auto-computed so each hop completes in AnimTickFraction of one physics
/// tick: speed = hexDist / (fixedDeltaTime × AnimTickFraction).
/// This ratio holds at any timeScale, so the animation is always done before the
/// next logical move arrives.
///
/// When the simulation runs faster than the renderer can display (training mode),
/// entries older than MaxQueueDepth are skipped so the visual never falls far behind.
/// </summary>
[RequireComponent(typeof(UnitData))]
public class HexMovement : MonoBehaviour
{
    /// <summary>
    /// Fraction of one physics tick used for a single hop animation (0 < f ≤ 1).
    /// 0.7 → finishes in 70 % of a tick, leaving headroom before the next move.
    /// </summary>
    public const float AnimTickFraction = 0.7f;

    /// <summary>
    /// Maximum pending hops in the visual queue. Entries beyond this are skipped
    /// (position snapped) so the visual never lags more than this many hops in
    /// fast-simulation / training mode.
    /// </summary>
    public const int MaxQueueDepth = 4;

    private UnitData unitData;
    private HexGrid grid;
    private float baseSpeed;

    // Visual waypoint queue: unit animates through positions in FIFO order.
    private readonly Queue<Vector3> moveQueue = new Queue<Vector3>();

    // ── Initialisation ────────────────────────────────────────────────────

    public void Initialize(HexGrid hexGrid)
    {
        unitData  = GetComponent<UnitData>();
        grid      = hexGrid;
        moveQueue.Clear();

        // Adjacent hex center distance for flat-top layout = outerRadius × √3.
        float hexDist = grid.outerRadius * Mathf.Sqrt(3f);
        baseSpeed = hexDist / (Time.fixedDeltaTime * AnimTickFraction);
    }

    // ── Visual animation (Update) ─────────────────────────────────────────

    private void Update()
    {
        if (moveQueue.Count == 0) return;

        float speed = baseSpeed * (unitData != null ? unitData.speedMultiplier : 1f);

        // Skip stale hops when simulation outruns the renderer (training mode).
        while (moveQueue.Count > MaxQueueDepth)
            transform.position = moveQueue.Dequeue();

        if (moveQueue.Count == 0) return;

        Vector3 target = moveQueue.Peek();
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        // Snap and dequeue when the hop is complete.
        if (Vector3.Distance(transform.position, target) < 0.001f)
        {
            transform.position = target;
            moveQueue.Dequeue();
        }
    }

    // ── Public movement API ───────────────────────────────────────────────

    /// <summary>
    /// Attempt to move one step in the given direction (0-5).
    /// Returns true if the move was executed.
    /// </summary>
    public bool TryMove(int direction)
    {
        if (!unitData.isAlive || grid == null) return false;
        if (direction < 0 || direction > 5)    return false;

        return TryMoveTo(unitData.currentHex.Neighbor(direction));
    }

    /// <summary>
    /// Attempt to move to a specific adjacent hex coordinate.
    /// Returns true if the move was executed.
    /// </summary>
    public bool TryMoveTo(HexCoord target)
    {
        if (!unitData.isAlive || grid == null)                     return false;
        if (!grid.IsValidCoord(target))                            return false;
        if (HexCoord.Distance(unitData.currentHex, target) != 1)  return false;
        if (IsOccupiedByAlly(target))                              return false;

        // Update logical state instantly (game logic reads currentHex).
        unitData.moveFrom   = unitData.currentHex;
        unitData.moveTo     = target;
        unitData.currentHex = target;
        unitData.lastAction = UnitAction.Move;

        // Enqueue visual hop — animation plays it out in sequence.
        moveQueue.Enqueue(grid.HexToWorld(target) + Vector3.up * 0.3f);

        // Notify arrow indicator before territory system overwrites lastAction.
        GetComponent<UnitActionIndicator>()?.OnMoveStarted(target);
        return true;
    }

    /// <summary>Returns true if the given direction is a valid move from the current hex.</summary>
    public bool IsValidMove(int direction)
    {
        if (!unitData.isAlive || grid == null) return false;
        if (direction < 0 || direction > 5)    return false;

        HexCoord target = unitData.currentHex.Neighbor(direction);
        return grid.IsValidCoord(target) && !IsOccupiedByAlly(target);
    }

    /// <summary>
    /// Place the unit at a hex instantly with no animation (spawn, respawn, editor).
    /// Clears any pending visual queue.
    /// </summary>
    public void PlaceAt(HexCoord coord)
    {
        if (unitData != null) unitData.currentHex = coord;
        Vector3 worldPos = grid.HexToWorld(coord) + Vector3.up * 0.3f;
        transform.position = worldPos;
        moveQueue.Clear();
    }

    /// <summary>How many hops are currently pending in the visual queue.</summary>
    public int QueueDepth => moveQueue.Count;

    // ── Private helpers ───────────────────────────────────────────────────

    private bool IsOccupiedByAlly(HexCoord coord)
    {
        var allUnits = FindObjectsByType<UnitData>(FindObjectsSortMode.None);
        foreach (var unit in allUnits)
        {
            if (unit == unitData) continue;
            if (!unit.isAlive)    continue;
            if (unit.team == unitData.team && unit.currentHex == coord)
                return true;
        }
        return false;
    }
}
