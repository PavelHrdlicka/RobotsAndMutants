/// <summary>
/// Terrain type on a hex tile, determines team-specific bonuses.
/// </summary>
public enum TileType
{
    Empty,
    Wall,   // Built by Robots — blocks movement, has HP (1-3)
    Slime   // Placed by Mutants — regen for Mutants, costs Robots energy to enter
}
