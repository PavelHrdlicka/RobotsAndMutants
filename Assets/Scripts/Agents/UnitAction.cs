/// <summary>
/// What action a unit performed this step. Used for visual indicators.
/// </summary>
public enum UnitAction
{
    Idle,       // Stayed in place
    Move,       // Moved to a neighbor hex
    Attack,     // Engaged in combat
    BuildCrate, // Robot built a crate
    SpreadSlime,// Mutant spread slime
    Capture,    // Captured neutral/enemy territory
    Dead        // Unit is dead
}
