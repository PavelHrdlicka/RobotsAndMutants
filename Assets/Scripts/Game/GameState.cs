/// <summary>
/// Read-only snapshot of current game state. Used by agents for observations and by UI.
/// </summary>
public readonly struct GameState
{
    public readonly int currentStep;
    public readonly int maxSteps;
    public readonly int robotTiles;
    public readonly int mutantTiles;
    public readonly int robotAlive;
    public readonly int mutantAlive;
    public readonly bool gameOver;
    public readonly Team winner;

    public GameState(int currentStep, int maxSteps, int robotTiles, int mutantTiles,
                     int robotAlive, int mutantAlive, bool gameOver, Team winner)
    {
        this.currentStep = currentStep;
        this.maxSteps = maxSteps;
        this.robotTiles = robotTiles;
        this.mutantTiles = mutantTiles;
        this.robotAlive = robotAlive;
        this.mutantAlive = mutantAlive;
        this.gameOver = gameOver;
        this.winner = winner;
    }

    public float StepProgress => maxSteps > 0 ? (float)currentStep / maxSteps : 0f;
    public int TotalContested => robotTiles + mutantTiles;
}
