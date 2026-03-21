using System;
using UnityEngine;

/// <summary>
/// Runtime data for a single hex tile: ownership, terrain type, wall HP, base status.
/// Attach alongside HexMeshGenerator on the hex prefab.
/// </summary>
public class HexTileData : MonoBehaviour
{
    [Header("Coordinates")]
    public HexCoord coord;

    [Header("Ownership")]
    [SerializeField] private Team owner = Team.None;
    [SerializeField] private TileType tileType = TileType.Empty;
    [SerializeField] private int wallHP; // 0-3

    [Header("Base")]
    public bool isBase;
    public Team baseTeam;

    /// <summary>Fired whenever owner, tileType, or wallHP changes.</summary>
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

    public int WallHP
    {
        get => wallHP;
        set
        {
            int clamped = Mathf.Clamp(value, 0, 3);
            if (wallHP != clamped) { wallHP = clamped; OnTileChanged?.Invoke(this); }
        }
    }

    /// <summary>Reset tile to neutral state (preserves base status).</summary>
    public void ResetTile()
    {
        owner = Team.None;
        tileType = TileType.Empty;
        wallHP = 0;

        if (isBase)
            owner = baseTeam;

        OnTileChanged?.Invoke(this);
    }
}
