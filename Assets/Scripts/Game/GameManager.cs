using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

/// <summary>
/// Orchestrates the sequential turn-based game loop.
///
/// Turn order within a round (interleaved teams):
///   Odd  rounds (1, 3, …): R0 → M0 → R1 → M1 → …
///   Even rounds (2, 4, …): M0 → R0 → M1 → R1 → …
///
/// Each unit gets one action per round (move, attack, build, or idle).
/// Only one unit acts per FixedUpdate frame:
///   1. GameManager calls agent.RequestDecision().
///   2. Academy fires OnActionReceived (next frame, script order -50).
///   3. Agent executes action, sets hasPendingTurnResult = true.
///   4. GameManager.FixedUpdate (script order 0) processes the result,
///      then advances to the next unit.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Config")]
    public int   maxRounds     = 500;
    public float winThreshold  = 0.6f;
    public bool  autoRestart   = true;
    public float restartDelay  = 2f;

    [Header("Runtime")]
    public int  currentRound;
    public bool gameOver;
    public Team winner = Team.None;

    // Match history (persists across resets within one Play session).
    private struct MatchResult
    {
        public Team winner;
        public int  rounds;
        public int  robotTiles;
        public int  mutantTiles;
        public int  matchNumber;
    }
    private static readonly List<MatchResult> matchHistory = new();
    private static int matchCounter;

    private HexGrid         grid;
    private UnitFactory     unitFactory;
    private TerritorySystem territorySystem;
    private AbilitySystem   abilitySystem;

    // Contestable tile count (non-base) for win-condition calculation.
    private int contestableTileCount;

    // MA-POCA agent groups.
    private SimpleMultiAgentGroup robotGroup;
    private SimpleMultiAgentGroup mutantGroup;

    // ── Sequential turn state ──────────────────────────────────────────────
    private readonly List<UnitData> turnOrder = new();
    private int      turnIndex   = -1;
    private UnitData pendingUnit = null;
    private bool     turnStarted = false;

    // ── Initialisation ─────────────────────────────────────────────────────

    /// <summary>Current game state snapshot.</summary>
    public GameState State => new GameState(
        currentRound, maxRounds,
        CountTiles(Team.Robot),  CountTiles(Team.Mutant),
        CountAlive(Team.Robot),  CountAlive(Team.Mutant),
        gameOver, winner
    );

    private IEnumerator Start()
    {
        yield return null;
        yield return null;

        var config = GameConfig.Instance;
        if (config != null)
        {
            maxRounds     = config.maxSteps;
            winThreshold  = config.WinThreshold;
            Time.timeScale = config.TimeScale;
        }

        grid        = FindFirstObjectByType<HexGrid>();
        unitFactory = FindFirstObjectByType<UnitFactory>();

        if (grid == null || unitFactory == null)
        {
            Debug.LogError("[GameManager] Missing HexGrid or UnitFactory!");
            yield break;
        }

        territorySystem = new TerritorySystem(grid);
        abilitySystem   = new AbilitySystem(grid);

        contestableTileCount = 0;
        foreach (var tile in grid.Tiles.Values)
            if (!tile.isBase) contestableTileCount++;

        robotGroup  = new SimpleMultiAgentGroup();
        mutantGroup = new SimpleMultiAgentGroup();

        foreach (var unit in unitFactory.robotUnits)
        {
            var agent = unit.GetComponent<HexAgent>();
            if (agent != null) robotGroup.RegisterAgent(agent);
        }
        foreach (var unit in unitFactory.mutantUnits)
        {
            var agent = unit.GetComponent<HexAgent>();
            if (agent != null) mutantGroup.RegisterAgent(agent);
        }

        Debug.Log($"[GameManager] Ready. {contestableTileCount} contestable tiles. Max rounds: {maxRounds}.");
    }

    public bool IsReady => grid != null && unitFactory != null && territorySystem != null;

    // ── Sequential turn loop ───────────────────────────────────────────────

    private void FixedUpdate()
    {
        if (gameOver || !IsReady) return;

        // Bootstrap: start first round once systems are ready.
        if (!turnStarted)
        {
            StartNewRound();
            return;
        }

        // Check whether the pending unit has completed its turn.
        if (pendingUnit != null && pendingUnit.hasPendingTurnResult)
        {
            pendingUnit.hasPendingTurnResult = false;
            PostTurnProcessing(pendingUnit);
            if (gameOver) return;
            AdvanceTurn();
        }
    }

    private void StartNewRound()
    {
        currentRound++;
        abilitySystem.UpdateAbilities(unitFactory.AllUnits);
        BuildTurnOrder();
        turnIndex   = -1;
        turnStarted = true;
        AdvanceTurn();
    }

    /// <summary>
    /// Interleaved turn order — odd rounds robots first, even rounds mutants first.
    /// Dead units are included so their slot can be skipped cheaply in AdvanceTurn.
    /// </summary>
    private void BuildTurnOrder()
    {
        turnOrder.Clear();
        bool robotsFirst = (currentRound % 2 == 1);
        var  first  = robotsFirst ? unitFactory.robotUnits  : unitFactory.mutantUnits;
        var  second = robotsFirst ? unitFactory.mutantUnits : unitFactory.robotUnits;

        int max = Mathf.Max(first.Count, second.Count);
        for (int i = 0; i < max; i++)
        {
            if (i < first.Count)  turnOrder.Add(first[i]);
            if (i < second.Count) turnOrder.Add(second[i]);
        }
    }

    private void AdvanceTurn()
    {
        turnIndex++;

        // Skip dead units without consuming an Academy step.
        while (turnIndex < turnOrder.Count && !turnOrder[turnIndex].isAlive)
            turnIndex++;

        if (turnIndex >= turnOrder.Count)
        {
            // Round complete.
            unitFactory.RespawnReady();
            if (!gameOver)
                StartNewRound();
            return;
        }

        pendingUnit = turnOrder[turnIndex];

        var agent = pendingUnit.GetComponent<HexAgent>();
        if (agent != null && agent.enabled)
        {
            agent.RequestDecision();
        }
        else
        {
            // No active agent — auto-skip this unit's turn.
            pendingUnit.hasPendingTurnResult = true;
        }
    }

    private void PostTurnProcessing(UnitData unit)
    {
        // Territory is claimed only through explicit BUILD actions (TryBuild in HexMovement).
        // Moving does NOT colour / claim a tile — no territory processing on Move.

        CheckWinCondition();
    }

    // ── Win condition ──────────────────────────────────────────────────────

    private void CheckWinCondition()
    {
        int   robotTiles  = CountTiles(Team.Robot);
        int   mutantTiles = CountTiles(Team.Mutant);
        float robotRatio  = (float)robotTiles  / contestableTileCount;
        float mutantRatio = (float)mutantTiles / contestableTileCount;

        if (robotRatio >= winThreshold)
        {
            EndGame(Team.Robot, 1f, -1f, robotTiles, mutantTiles,
                    $"Robots win! ({robotRatio:P0} territory at round {currentRound})");
        }
        else if (mutantRatio >= winThreshold)
        {
            EndGame(Team.Mutant, 1f, -1f, robotTiles, mutantTiles,
                    $"Mutants win! ({mutantRatio:P0} territory at round {currentRound})");
        }
        else if (currentRound >= maxRounds && turnIndex >= turnOrder.Count - 1)
        {
            if (robotTiles > mutantTiles)
                EndGame(Team.Robot,  0.5f, -0.5f, robotTiles, mutantTiles, $"Max rounds. Robots lead.");
            else if (mutantTiles > robotTiles)
                EndGame(Team.Mutant, 0.5f, -0.5f, robotTiles, mutantTiles, $"Max rounds. Mutants lead.");
            else
                EndGame(Team.None,   0f,    0f,   robotTiles, mutantTiles, $"Max rounds. Draw.");
        }
    }

    private void EndGame(Team win, float winnerReward, float loserReward,
                         int rTiles, int mTiles, string logMsg)
    {
        gameOver = true;
        winner   = win;

        var winGroup  = (win == Team.Robot) ? robotGroup  : mutantGroup;
        var loseGroup = (win == Team.Robot) ? mutantGroup : robotGroup;

        if (win == Team.None)
        {
            robotGroup?.AddGroupReward(0f);
            mutantGroup?.AddGroupReward(0f);
        }
        else
        {
            winGroup?.AddGroupReward(winnerReward);
            loseGroup?.AddGroupReward(loserReward);
        }

        RecordMatch(win, currentRound, rTiles, mTiles);
        EndEpisodeForAll();
        Debug.Log($"[GameManager] {logMsg}");
    }

    // ── Episode management ─────────────────────────────────────────────────

    public void ResetGame()
    {
        currentRound = 0;
        gameOver     = false;
        winner       = Team.None;
        turnStarted  = false;
        pendingUnit  = null;
        turnIndex    = -1;
        turnOrder.Clear();

        foreach (var tile in grid.Tiles.Values)
            tile.ResetTile();

        unitFactory.ClearUnits();
        unitFactory.SpawnAllUnits();
        territorySystem?.Reset();

        Debug.Log("[GameManager] Game reset.");
    }

    private void RecordMatch(Team matchWinner, int rounds, int rTiles, int mTiles)
    {
        matchCounter++;
        matchHistory.Add(new MatchResult
        {
            winner      = matchWinner,
            rounds      = rounds,
            robotTiles  = rTiles,
            mutantTiles = mTiles,
            matchNumber = matchCounter
        });
        while (matchHistory.Count > 20) matchHistory.RemoveAt(0);
    }

    private void EndEpisodeForAll()
    {
        robotGroup?.EndGroupEpisode();
        mutantGroup?.EndGroupEpisode();

        foreach (var unit in unitFactory.AllUnits)
        {
            var agent = unit.GetComponent<HexAgent>();
            if (agent != null) agent.enabled = false;
        }

        if (autoRestart)
            StartCoroutine(AutoRestartCoroutine());
    }

    private IEnumerator AutoRestartCoroutine()
    {
        yield return new WaitForSecondsRealtime(restartDelay);
        ResetGame();

        robotGroup  = new SimpleMultiAgentGroup();
        mutantGroup = new SimpleMultiAgentGroup();

        foreach (var unit in unitFactory.robotUnits)
        {
            var agent = unit.GetComponent<HexAgent>();
            if (agent != null) { agent.enabled = true; robotGroup.RegisterAgent(agent); }
        }
        foreach (var unit in unitFactory.mutantUnits)
        {
            var agent = unit.GetComponent<HexAgent>();
            if (agent != null) { agent.enabled = true; mutantGroup.RegisterAgent(agent); }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private int CountTiles(Team team)
    {
        int count = 0;
        foreach (var tile in grid.Tiles.Values)
            if (!tile.isBase && tile.Owner == team) count++;
        return count;
    }

    private int CountAlive(Team team)
    {
        var units = team == Team.Robot ? unitFactory.robotUnits : unitFactory.mutantUnits;
        int count = 0;
        foreach (var u in units) if (u.isAlive) count++;
        return count;
    }

    // ── GUI ────────────────────────────────────────────────────────────────

    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle valueStyle;
    private GUIStyle stepStyle;
    private GUIStyle gameOverStyle;
    private Texture2D robotBg;
    private Texture2D mutantBg;
    private Texture2D darkBg;
    private bool stylesInitialized;

    private void InitStyles()
    {
        if (stylesInitialized) return;
        stylesInitialized = true;

        robotBg  = MakeTex(1, 1, new Color(0.10f, 0.20f, 0.50f, 0.85f));
        mutantBg = MakeTex(1, 1, new Color(0.10f, 0.35f, 0.10f, 0.85f));
        darkBg   = MakeTex(1, 1, new Color(0.05f, 0.05f, 0.10f, 0.80f));

        panelStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 8, 8) };

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleCenter
        };

        valueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f) },
            alignment = TextAnchor.MiddleLeft
        };

        stepStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleCenter
        };

        gameOverStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22, fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.85f, 0.2f) },
            alignment = TextAnchor.MiddleCenter
        };
    }

    private static Texture2D MakeTex(int w, int h, Color col)
    {
        var tex    = new Texture2D(w, h);
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void OnGUI()
    {
        if (grid == null) return;
        InitStyles();

        var   state     = State;
        float totalTiles = contestableTileCount > 0 ? contestableTileCount : 1;
        float robotPct  = state.robotTiles  / totalTiles * 100f;
        float mutantPct = state.mutantTiles / totalTiles * 100f;

        const float panelW = 200f;
        const float panelH = 90f;
        const float margin = 10f;

        // --- Robot panel (left) ---
        panelStyle.normal.background = robotBg;
        GUI.Box(new Rect(margin, margin, panelW, panelH), "", panelStyle);
        GUI.Label(new Rect(margin, margin + 4, panelW, 24), "ROBOTS", titleStyle);
        GUI.Label(new Rect(margin + 12, margin + 30, panelW, 20),
                  $"Alive:  {state.robotAlive} / {unitFactory.unitsPerTeam}", valueStyle);
        GUI.Label(new Rect(margin + 12, margin + 52, panelW, 20),
                  $"Tiles:  {state.robotTiles}  ({robotPct:F1}%)", valueStyle);
        DrawBar(new Rect(margin + 12, margin + 74, panelW - 24, 6),
                robotPct / 100f, new Color(0.3f, 0.5f, 0.9f));

        // --- Mutant panel (right) ---
        float rightX = Screen.width - panelW - margin;
        panelStyle.normal.background = mutantBg;
        GUI.Box(new Rect(rightX, margin, panelW, panelH), "", panelStyle);
        GUI.Label(new Rect(rightX, margin + 4, panelW, 24), "MUTANTS", titleStyle);
        GUI.Label(new Rect(rightX + 12, margin + 30, panelW, 20),
                  $"Alive:  {state.mutantAlive} / {unitFactory.unitsPerTeam}", valueStyle);
        GUI.Label(new Rect(rightX + 12, margin + 52, panelW, 20),
                  $"Tiles:  {state.mutantTiles}  ({mutantPct:F1}%)", valueStyle);
        DrawBar(new Rect(rightX + 12, margin + 74, panelW - 24, 6),
                mutantPct / 100f, new Color(0.3f, 0.8f, 0.2f));

        // --- Round counter (top center) ---
        float centerW = 180f;
        float centerX = (Screen.width - centerW) * 0.5f;
        panelStyle.normal.background = darkBg;
        GUI.Box(new Rect(centerX, margin, centerW, 30), "", panelStyle);
        GUI.Label(new Rect(centerX, margin + 4, centerW, 22),
                  $"Round {state.currentRound} / {state.maxRounds}", stepStyle);

        // --- Game over banner ---
        if (state.gameOver)
        {
            float bannerW = 400f;
            float bannerX = (Screen.width - bannerW) * 0.5f;
            panelStyle.normal.background = darkBg;
            GUI.Box(new Rect(bannerX, Screen.height * 0.4f, bannerW, 50), "", panelStyle);

            string winText = state.winner == Team.None
                ? "DRAW!"
                : $"{state.winner.ToString().ToUpper()}S WIN!";
            GUI.Label(new Rect(bannerX, Screen.height * 0.4f + 8, bannerW, 34), winText, gameOverStyle);
        }

        // --- Match History (bottom) ---
        DrawMatchHistory();
    }

    private GUIStyle historyHeaderStyle;
    private GUIStyle historyRowStyle;
    private Texture2D robotRowBg;
    private Texture2D mutantRowBg;
    private Texture2D drawRowBg;
    private Texture2D historyPanelBg;
    private bool historyStylesInit;

    private void InitHistoryStyles()
    {
        if (historyStylesInit) return;
        historyStylesInit = true;

        robotRowBg    = MakeTex(1, 1, new Color(0.12f, 0.22f, 0.50f, 0.70f));
        mutantRowBg   = MakeTex(1, 1, new Color(0.12f, 0.38f, 0.12f, 0.70f));
        drawRowBg     = MakeTex(1, 1, new Color(0.30f, 0.30f, 0.15f, 0.70f));
        historyPanelBg = MakeTex(1, 1, new Color(0.04f, 0.04f, 0.08f, 0.85f));

        historyHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11, fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.8f, 0.8f, 0.6f) },
            alignment = TextAnchor.MiddleLeft
        };

        historyRowStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal = { textColor = new Color(0.95f, 0.95f, 0.95f) },
            alignment = TextAnchor.MiddleLeft
        };
    }

    private void DrawMatchHistory()
    {
        if (matchHistory.Count == 0) return;
        InitHistoryStyles();

        const float rowH    = 18f;
        const float panelW  = 340f;
        const float titleH  = 24f;
        const float headerH = 20f;
        const float pad     = 6f;
        const float margin  = 10f;
        float panelH  = titleH + headerH + matchHistory.Count * rowH + pad * 2 + 4;
        float panelX  = margin;
        float panelY  = Screen.height - panelH - margin;

        GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), historyPanelBg);

        float y         = panelY + pad;
        int robotWins   = 0, mutantWins = 0, draws = 0;
        foreach (var m in matchHistory)
        {
            if      (m.winner == Team.Robot)  robotWins++;
            else if (m.winner == Team.Mutant) mutantWins++;
            else                              draws++;
        }
        GUI.Label(new Rect(panelX + 8, y, panelW - 16, titleH),
            $"Match History  ({matchCounter} total)   R:{robotWins}  M:{mutantWins}  D:{draws}",
            historyHeaderStyle);
        y += titleH;

        GUI.Label(new Rect(panelX +   8, y,  30, headerH), "#",      historyHeaderStyle);
        GUI.Label(new Rect(panelX +  38, y,  90, headerH), "Winner", historyHeaderStyle);
        GUI.Label(new Rect(panelX + 130, y,  60, headerH), "Rounds", historyHeaderStyle);
        GUI.Label(new Rect(panelX + 195, y, 140, headerH), "Score",  historyHeaderStyle);
        y += headerH;

        GUI.color = new Color(0.5f, 0.5f, 0.4f, 0.5f);
        GUI.DrawTexture(new Rect(panelX + 6, y - 1, panelW - 12, 1), Texture2D.whiteTexture);
        GUI.color = Color.white;

        for (int i = matchHistory.Count - 1; i >= 0; i--)
        {
            var m       = matchHistory[i];
            Rect rowRect = new Rect(panelX + 4, y, panelW - 8, rowH);

            Texture2D rowBg = m.winner switch
            {
                Team.Robot  => robotRowBg,
                Team.Mutant => mutantRowBg,
                _           => drawRowBg
            };
            GUI.DrawTexture(rowRect, rowBg);

            Color dotColor = m.winner switch
            {
                Team.Robot  => new Color(0.3f, 0.55f, 1f),
                Team.Mutant => new Color(0.3f, 0.85f, 0.25f),
                _           => new Color(0.8f, 0.8f, 0.3f)
            };
            GUI.color = dotColor;
            GUI.DrawTexture(new Rect(panelX + 30, y + 6, 6, 6), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string winnerText = m.winner switch
            {
                Team.Robot  => "Robots",
                Team.Mutant => "Mutants",
                _           => "Draw"
            };

            GUI.Label(new Rect(panelX +   8, y,  30, rowH), $"{m.matchNumber}", historyRowStyle);
            GUI.Label(new Rect(panelX +  38, y,  90, rowH), winnerText,          historyRowStyle);
            GUI.Label(new Rect(panelX + 130, y,  60, rowH), $"{m.rounds}",       historyRowStyle);
            GUI.Label(new Rect(panelX + 195, y, 140, rowH), $"R:{m.robotTiles}  vs  M:{m.mutantTiles}", historyRowStyle);

            y += rowH;
        }
    }

    private static void DrawBar(Rect rect, float fill, Color color)
    {
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.6f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height),
                        Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
