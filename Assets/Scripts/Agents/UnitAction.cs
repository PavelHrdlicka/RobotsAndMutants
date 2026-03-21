/// <summary>
/// What action a unit performed this step. Used for visual indicators.
/// </summary>
public enum UnitAction
{
    Idle,       // Stayed in place (own turn choice)
    Move,       // Moved to a neighbor hex
    Attack,     // Initiated combat with adjacent enemy
    Defend,     // Being attacked by an enemy (legacy, kept for replay compat)
    BuildWall,  // Robot built a wall
    PlaceSlime, // Mutant placed slime
    Capture,    // Captured territory
    Dead        // Unit is dead
}
