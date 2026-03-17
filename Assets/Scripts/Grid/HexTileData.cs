using System;
using UnityEngine;

/// <summary>
/// Runtime data for a single hex tile: ownership, terrain type, fortification, base status.
/// Attach alongside HexMeshGenerator on the hex prefab.
/// </summary>
public class HexTileData : MonoBehaviour
{
    [Header("Coordinates")]
    public HexCoord coord;

    [Header("Ownership")]
    [SerializeField] private Team owner = Team.None;
    [SerializeField] private TileType tileType = TileType.Empty;
    [SerializeField] private int fortification; // 0-3

    [Header("Base")]
    public bool isBase;
    public Team baseTeam;

    /// <summary>Fired whenever owner, tileType, or fortification changes.</summary>
    public event Action<HexTileData> OnTileChanged;

    public Team Owner
    {
        get => owner;
        set { if (owner != value) { owner = value; OnTileChanged?.Invoke(this); } }
    }

    public TileType TileType
    {
        get => tileType;
        set { if (tileType != value) { tileType = value; OnTileChanged?.Invoke(this); } }
    }

    public int Fortification
    {
        get => fortification;
        set
        {
            int clamped = Mathf.Clamp(value, 0, 3);
            if (fortification != clamped) { fortification = clamped; OnTileChanged?.Invoke(this); }
        }
    }

    /// <summary>Reset tile to neutral state (preserves base status).</summary>
    public void ResetTile()
    {
        owner = Team.None;
        tileType = TileType.Empty;
        fortification = 0;

        if (isBase)
            owner = baseTeam;

        OnTileChanged?.Invoke(this);
    }
}
