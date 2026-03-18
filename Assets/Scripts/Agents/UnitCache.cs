using UnityEngine;

/// <summary>
/// Shared per-frame cache of all UnitData instances in the scene.
/// Replaces the duplicate private static caches that existed in both HexAgent and HexMovement.
/// </summary>
public static class UnitCache
{
    private static UnitData[] cached;
    private static int cachedFrame = -1;

    public static UnitData[] GetAll()
    {
        if (Time.frameCount != cachedFrame)
        {
            cached = Object.FindObjectsByType<UnitData>(FindObjectsSortMode.None);
            cachedFrame = Time.frameCount;
        }
        return cached;
    }
}
