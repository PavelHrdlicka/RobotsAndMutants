/// <summary>
/// Terrain type on a hex tile, determines team-specific bonuses.
/// </summary>
public enum TileType
{
    Empty,
    Crate,  // Built by Robots — provides defensive bonus
    Slime   // Spread by Mutants — provides speed bonus
}
