using UnityEngine;

/// <summary>
/// Maps HexTileData state (owner, tile type, fortification, base) to hex color.
/// Subscribes to OnTileChanged and updates automatically.
/// </summary>
[RequireComponent(typeof(HexTileData), typeof(HexMeshGenerator))]
public class HexVisuals : MonoBehaviour
{
    private HexTileData tileData;
    private HexMeshGenerator meshGen;

    // --- Color palette ---
    private static readonly Color NeutralColor     = new Color(0.55f, 0.55f, 0.50f);
    private static readonly Color RobotColor       = new Color(0.30f, 0.50f, 0.85f);
    private static readonly Color MutantColor      = new Color(0.45f, 0.75f, 0.30f);
    private static readonly Color RobotBaseColor   = new Color(0.15f, 0.30f, 0.70f);
    private static readonly Color MutantBaseColor  = new Color(0.25f, 0.55f, 0.15f);
    private static readonly Color CrateColor       = new Color(0.20f, 0.35f, 0.65f);
    private static readonly Color SlimeColor       = new Color(0.35f, 0.85f, 0.20f);

    private static readonly float FortificationBrightness = 0.08f;

    private void Awake()
    {
        tileData = GetComponent<HexTileData>();
        meshGen = GetComponent<HexMeshGenerator>();
    }

    private void OnEnable()
    {
        if (tileData != null)
            tileData.OnTileChanged += OnTileChanged;
    }

    private void OnDisable()
    {
        if (tileData != null)
            tileData.OnTileChanged -= OnTileChanged;
    }

    private void Start()
    {
        UpdateColor();
    }

    private void OnTileChanged(HexTileData _)
    {
        UpdateColor();
    }

    /// <summary>
    /// Resolve the correct color from current tile state and apply it.
    /// </summary>
    public void UpdateColor()
    {
        if (tileData == null || meshGen == null) return;

        Color color = ResolveColor();

        // Fortification brightens the tile.
        if (tileData.Fortification > 0)
            color += Color.white * (tileData.Fortification * FortificationBrightness);

        meshGen.SetColor(color);
    }

    private Color ResolveColor()
    {
        // Base tiles get saturated team color.
        if (tileData.isBase)
        {
            return tileData.baseTeam switch
            {
                Team.Robot  => RobotBaseColor,
                Team.Mutant => MutantBaseColor,
                _           => NeutralColor
            };
        }

        // Tile type overrides (only on owned tiles).
        if (tileData.TileType == TileType.Crate && tileData.Owner == Team.Robot)
            return CrateColor;
        if (tileData.TileType == TileType.Slime && tileData.Owner == Team.Mutant)
            return SlimeColor;

        // Ownership color.
        return tileData.Owner switch
        {
            Team.Robot  => RobotColor,
            Team.Mutant => MutantColor,
            _           => NeutralColor
        };
    }

    /// <summary>
    /// Get the expected color for a given state (used by tests).
    /// </summary>
    public static Color GetColorForState(Team owner, TileType tileType, bool isBase, Team baseTeam, int fortification)
    {
        Color color;

        if (isBase)
        {
            color = baseTeam switch
            {
                Team.Robot  => RobotBaseColor,
                Team.Mutant => MutantBaseColor,
                _           => NeutralColor
            };
        }
        else if (tileType == TileType.Crate && owner == Team.Robot)
            color = CrateColor;
        else if (tileType == TileType.Slime && owner == Team.Mutant)
            color = SlimeColor;
        else
        {
            color = owner switch
            {
                Team.Robot  => RobotColor,
                Team.Mutant => MutantColor,
                _           => NeutralColor
            };
        }

        if (fortification > 0)
            color += Color.white * (fortification * FortificationBrightness);

        return color;
    }
}
