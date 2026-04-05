using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared per-frame cache of all UnitData instances in the scene.
/// Provides both flat array and spatial lookup by hex coordinate.
/// </summary>
public static class UnitCache
{
    private static UnitData[] cached;
    private static int cachedFrame = -1;

    // Spatial index: alive units by hex coordinate.
    private static readonly Dictionary<HexCoord, UnitData> spatialIndex = new(16);
    private static int spatialFrame = -1;

    public static UnitData[] GetAll()
    {
        if (Time.frameCount != cachedFrame)
        {
            cached = Object.FindObjectsByType<UnitData>(FindObjectsSortMode.None);
            cachedFrame = Time.frameCount;
        }
        return cached;
    }

    /// <summary>
    /// Get alive unit at a specific hex coordinate (O(1) lookup).
    /// Returns null if no alive unit at that position.
    /// </summary>
    public static UnitData GetAliveAt(HexCoord coord)
    {
        RefreshSpatialIndex();
        spatialIndex.TryGetValue(coord, out var unit);
        return unit;
    }

    private static void RefreshSpatialIndex()
    {
        if (Time.frameCount == spatialFrame) return;
        spatialFrame = Time.frameCount;

        spatialIndex.Clear();
        foreach (var u in GetAll())
        {
            if (u != null && u.isAlive)
                spatialIndex[u.currentHex] = u;
        }
    }
}
