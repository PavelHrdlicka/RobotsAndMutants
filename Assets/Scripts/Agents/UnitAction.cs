/// <summary>
/// What action a unit performed this step. Used for visual indicators.
/// </summary>
public enum UnitAction
{
    Idle,       // Stayed in place (own turn choice)
    Move,       // Moved to a neighbor hex
    Attack,     // Initiated combat with adjacent enemy (costs a turn)
    Defend,     // Being attacked by an enemy (does NOT cost a turn)
    BuildCrate, // Robot built a crate
    SpreadSlime,// Mutant spread slime
    Capture,    // Neutralized enemy territory by moving onto it
    Dead        // Unit is dead
}
