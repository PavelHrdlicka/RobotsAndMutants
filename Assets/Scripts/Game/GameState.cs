/// <summary>
/// Read-only snapshot of current game state. Used by agents for observations and by UI.
/// </summary>
public readonly struct GameState
{
    public readonly int currentRound;
    public readonly int maxRounds;
    public readonly int robotTiles;
    public readonly int mutantTiles;
    public readonly int robotAlive;
    public readonly int mutantAlive;
    public readonly bool gameOver;
    public readonly Team winner;

    public GameState(int currentRound, int maxRounds, int robotTiles, int mutantTiles,
                     int robotAlive, int mutantAlive, bool gameOver, Team winner)
    {
        this.currentRound = currentRound;
        this.maxRounds    = maxRounds;
        this.robotTiles   = robotTiles;
        this.mutantTiles  = mutantTiles;
        this.robotAlive   = robotAlive;
        this.mutantAlive  = mutantAlive;
        this.gameOver     = gameOver;
        this.winner       = winner;
    }

    public float RoundProgress => maxRounds > 0 ? (float)currentRound / maxRounds : 0f;
    public int TotalContested  => robotTiles + mutantTiles;
}
