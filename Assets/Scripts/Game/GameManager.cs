using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

/// <summary>
/// Orchestrates the step-based game loop:
/// 1. Agents submit actions (movement)
/// 2. Resolve combat
/// 3. Resolve territory captures and abilities
/// 4. Tick respawn cooldowns
/// 5. Update visuals
/// 6. Check win condition
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Config")]
    public int maxSteps = 2000;
    public float winThreshold = 0.6f; // 60% of tiles to win
    public bool autoRestart = true;
    public float restartDelay = 2f;

    [Header("Runtime")]
    public int currentStep;
    public bool gameOver;
    public Team winner = Team.None;

    // Match history (persists across resets within one Play session).
    private struct MatchResult
    {
        public Team winner;
        public int steps;
        public int robotTiles;
        public int mutantTiles;
        public int matchNumber;
    }
    private static readonly List<MatchResult> matchHistory = new();
    private static int matchCounter;

    private HexGrid grid;
    private UnitFactory unitFactory;
    private TerritorySystem territorySystem;
    private CombatSystem combatSystem;
    private AbilitySystem abilitySystem;

    // Cached tile count (excluding base tiles) for win condition calculation.
    private int contestableTileCount;

    // MA-POCA agent groups for team rewards.
    private SimpleMultiAgentGroup robotGroup;
    private SimpleMultiAgentGroup mutantGroup;

    /// <summary>Current game state snapshot.</summary>
    public GameState State => new GameState(
        currentStep, maxSteps,
        CountTiles(Team.Robot), CountTiles(Team.Mutant),
        CountAlive(Team.Robot), CountAlive(Team.Mutant),
        gameOver, winner
    );

    private IEnumerator Start()
    {
        // Wait for grid and units to be ready.
        yield return null;
        yield return null;

        // Apply GameConfig.
        var config = GameConfig.Instance;
        if (config != null)
        {
            maxSteps = config.maxSteps;
            winThreshold = config.WinThreshold;
            Time.timeScale = config.TimeScale;
        }

        grid = FindFirstObjectByType<HexGrid>();
        unitFactory = FindFirstObjectByType<UnitFactory>();

        if (grid == null || unitFactory == null)
        {
            Debug.LogError("[GameManager] Missing HexGrid or UnitFactory!");
            yield break;
        }

        territorySystem = new TerritorySystem(grid);
        combatSystem = new CombatSystem();
        abilitySystem = new AbilitySystem(grid);

        // Count contestable tiles (non-base tiles).
        contestableTileCount = 0;
        foreach (var tile in grid.Tiles.Values)
        {
            if (!tile.isBase) contestableTileCount++;
        }

        // Setup MA-POCA agent groups.
        robotGroup = new SimpleMultiAgentGroup();
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

        Debug.Log($"[GameManager] Ready. {contestableTileCount} contestable tiles. Max steps: {maxSteps}.");
    }

    /// <summary>Returns true when all systems are initialized and ready.</summary>
    public bool IsReady => grid != null && unitFactory != null && combatSystem != null;

    private void FixedUpdate()
    {
        // Auto-step each physics frame (synced with ML-Agents Academy).
        Step();
    }

    /// <summary>
    /// Execute one game step. Called automatically via FixedUpdate or manually.
    /// </summary>
    public void Step()
    {
        if (gameOver || !IsReady) return;

        currentStep++;

        var allUnits = unitFactory.AllUnits;

        // 1. Movement is handled by agents calling HexMovement.TryMove() before Step().

        // 2. Combat.
        combatSystem.ResolveCombat(allUnits);

        // 3. Territory capture.
        territorySystem.ProcessCaptures(allUnits);

        // 4. Abilities (shield, speed, adjacency).
        abilitySystem.UpdateAbilities(allUnits);

        // 5. Respawn.
        unitFactory.RespawnReady();

        // 6. Check win condition.
        CheckWinCondition();
    }

    /// <summary>Reset game for a new episode.</summary>
    public void ResetGame()
    {
        currentStep = 0;
        gameOver = false;
        winner = Team.None;

        // Reset all tiles.
        foreach (var tile in grid.Tiles.Values)
            tile.ResetTile();

        // Reset units.
        unitFactory.ClearUnits();
        unitFactory.SpawnAllUnits();

        // Reset systems.
        territorySystem?.Reset();

        Debug.Log("[GameManager] Game reset.");
    }

    private void CheckWinCondition()
    {
        int robotTiles = CountTiles(Team.Robot);
        int mutantTiles = CountTiles(Team.Mutant);

        float robotRatio = (float)robotTiles / contestableTileCount;
        float mutantRatio = (float)mutantTiles / contestableTileCount;

        if (robotRatio >= winThreshold)
        {
            gameOver = true;
            winner = Team.Robot;
            robotGroup?.AddGroupReward(1f);
            mutantGroup?.AddGroupReward(-1f);
            RecordMatch(Team.Robot, currentStep, robotTiles, mutantTiles);
            EndEpisodeForAll();
            Debug.Log($"[GameManager] Robots win! ({robotRatio:P0} territory at step {currentStep})");
        }
        else if (mutantRatio >= winThreshold)
        {
            gameOver = true;
            winner = Team.Mutant;
            mutantGroup?.AddGroupReward(1f);
            robotGroup?.AddGroupReward(-1f);
            RecordMatch(Team.Mutant, currentStep, robotTiles, mutantTiles);
            EndEpisodeForAll();
            Debug.Log($"[GameManager] Mutants win! ({mutantRatio:P0} territory at step {currentStep})");
        }
        else if (currentStep >= maxSteps)
        {
            gameOver = true;
            if (robotTiles > mutantTiles)
            {
                winner = Team.Robot;
                robotGroup?.AddGroupReward(0.5f);
                mutantGroup?.AddGroupReward(-0.5f);
            }
            else if (mutantTiles > robotTiles)
            {
                winner = Team.Mutant;
                mutantGroup?.AddGroupReward(0.5f);
                robotGroup?.AddGroupReward(-0.5f);
            }
            RecordMatch(winner, currentStep, robotTiles, mutantTiles);
            EndEpisodeForAll();
            Debug.Log($"[GameManager] Max steps reached. Winner: {winner} (R:{robotTiles} vs M:{mutantTiles})");
        }
    }

    private void RecordMatch(Team matchWinner, int steps, int rTiles, int mTiles)
    {
        matchCounter++;
        matchHistory.Add(new MatchResult
        {
            winner = matchWinner,
            steps = steps,
            robotTiles = rTiles,
            mutantTiles = mTiles,
            matchNumber = matchCounter
        });

        // Keep only last 20.
        while (matchHistory.Count > 20)
            matchHistory.RemoveAt(0);
    }

    private void EndEpisodeForAll()
    {
        robotGroup?.EndGroupEpisode();
        mutantGroup?.EndGroupEpisode();

        // Disable all agents so they stop moving.
        foreach (var unit in unitFactory.AllUnits)
        {
            var dr = unit.GetComponent<DecisionRequester>();
            if (dr != null) dr.enabled = false;

            var agent = unit.GetComponent<HexAgent>();
            if (agent != null) agent.enabled = false;
        }

        // Auto-restart after delay.
        if (autoRestart)
            StartCoroutine(AutoRestartCoroutine());
    }

    private IEnumerator AutoRestartCoroutine()
    {
        yield return new WaitForSecondsRealtime(restartDelay);
        ResetGame();

        // Re-enable agents and re-register groups.
        robotGroup = new SimpleMultiAgentGroup();
        mutantGroup = new SimpleMultiAgentGroup();

        foreach (var unit in unitFactory.robotUnits)
        {
            var agent = unit.GetComponent<HexAgent>();
            var dr = unit.GetComponent<DecisionRequester>();
            if (agent != null) { agent.enabled = true; robotGroup.RegisterAgent(agent); }
            if (dr != null) dr.enabled = true;
        }
        foreach (var unit in unitFactory.mutantUnits)
        {
            var agent = unit.GetComponent<HexAgent>();
            var dr = unit.GetComponent<DecisionRequester>();
            if (agent != null) { agent.enabled = true; mutantGroup.RegisterAgent(agent); }
            if (dr != null) dr.enabled = true;
        }
    }

    private int CountTiles(Team team)
    {
        int count = 0;
        foreach (var tile in grid.Tiles.Values)
        {
            if (!tile.isBase && tile.Owner == team)
                count++;
        }
        return count;
    }

    private int CountAlive(Team team)
    {
        int count = 0;
        var units = team == Team.Robot ? unitFactory.robotUnits : unitFactory.mutantUnits;
        foreach (var u in units)
        {
            if (u.isAlive) count++;
        }
        return count;
    }

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

        robotBg = MakeTex(1, 1, new Color(0.10f, 0.20f, 0.50f, 0.85f));
        mutantBg = MakeTex(1, 1, new Color(0.10f, 0.35f, 0.10f, 0.85f));
        darkBg = MakeTex(1, 1, new Color(0.05f, 0.05f, 0.10f, 0.80f));

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
        var tex = new Texture2D(w, h);
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

        var state = State;
        float totalTiles = contestableTileCount > 0 ? contestableTileCount : 1;
        float robotPct = state.robotTiles / totalTiles * 100f;
        float mutantPct = state.mutantTiles / totalTiles * 100f;

        const float panelW = 200f;
        const float panelH = 90f;
        const float margin = 10f;

        // --- Robot panel (left) ---
        panelStyle.normal.background = robotBg;
        GUI.Box(new Rect(margin, margin, panelW, panelH), "", panelStyle);
        GUI.Label(new Rect(margin, margin + 4, panelW, 24), "ROBOTS", titleStyle);
        GUI.Label(new Rect(margin + 12, margin + 30, panelW, 20), $"Alive:  {state.robotAlive} / {unitFactory.unitsPerTeam}", valueStyle);
        GUI.Label(new Rect(margin + 12, margin + 52, panelW, 20), $"Tiles:  {state.robotTiles}  ({robotPct:F1}%)", valueStyle);

        // Territory bar (robot).
        DrawBar(new Rect(margin + 12, margin + 74, panelW - 24, 6),
                robotPct / 100f, new Color(0.3f, 0.5f, 0.9f));

        // --- Mutant panel (right) ---
        float rightX = Screen.width - panelW - margin;
        panelStyle.normal.background = mutantBg;
        GUI.Box(new Rect(rightX, margin, panelW, panelH), "", panelStyle);
        GUI.Label(new Rect(rightX, margin + 4, panelW, 24), "MUTANTS", titleStyle);
        GUI.Label(new Rect(rightX + 12, margin + 30, panelW, 20), $"Alive:  {state.mutantAlive} / {unitFactory.unitsPerTeam}", valueStyle);
        GUI.Label(new Rect(rightX + 12, margin + 52, panelW, 20), $"Tiles:  {state.mutantTiles}  ({mutantPct:F1}%)", valueStyle);

        // Territory bar (mutant).
        DrawBar(new Rect(rightX + 12, margin + 74, panelW - 24, 6),
                mutantPct / 100f, new Color(0.3f, 0.8f, 0.2f));

        // --- Step counter (top center) ---
        float centerW = 160f;
        float centerX = (Screen.width - centerW) * 0.5f;
        panelStyle.normal.background = darkBg;
        GUI.Box(new Rect(centerX, margin, centerW, 30), "", panelStyle);
        GUI.Label(new Rect(centerX, margin + 4, centerW, 22),
                  $"Step {state.currentStep} / {state.maxSteps}", stepStyle);

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

        robotRowBg = MakeTex(1, 1, new Color(0.12f, 0.22f, 0.50f, 0.70f));
        mutantRowBg = MakeTex(1, 1, new Color(0.12f, 0.38f, 0.12f, 0.70f));
        drawRowBg = MakeTex(1, 1, new Color(0.30f, 0.30f, 0.15f, 0.70f));
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

        const float rowH = 18f;
        const float panelW = 340f;
        const float titleH = 24f;
        const float headerH = 20f;
        const float pad = 6f;
        const float margin = 10f;
        float panelH = titleH + headerH + matchHistory.Count * rowH + pad * 2 + 4;
        float panelX = margin;
        float panelY = Screen.height - panelH - margin;

        // Background panel.
        GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), historyPanelBg);

        // Title with total count.
        float y = panelY + pad;
        int robotWins = 0, mutantWins = 0, draws = 0;
        foreach (var m in matchHistory)
        {
            if (m.winner == Team.Robot) robotWins++;
            else if (m.winner == Team.Mutant) mutantWins++;
            else draws++;
        }
        GUI.Label(new Rect(panelX + 8, y, panelW - 16, titleH),
            $"Match History  ({matchCounter} total)   R:{robotWins}  M:{mutantWins}  D:{draws}",
            historyHeaderStyle);
        y += titleH;

        // Column headers.
        GUI.Label(new Rect(panelX + 8, y, 30, headerH), "#", historyHeaderStyle);
        GUI.Label(new Rect(panelX + 38, y, 90, headerH), "Winner", historyHeaderStyle);
        GUI.Label(new Rect(panelX + 130, y, 60, headerH), "Steps", historyHeaderStyle);
        GUI.Label(new Rect(panelX + 195, y, 140, headerH), "Score", historyHeaderStyle);
        y += headerH;

        // Separator line.
        GUI.color = new Color(0.5f, 0.5f, 0.4f, 0.5f);
        GUI.DrawTexture(new Rect(panelX + 6, y - 1, panelW - 12, 1), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Rows (newest first).
        for (int i = matchHistory.Count - 1; i >= 0; i--)
        {
            var m = matchHistory[i];
            Rect rowRect = new Rect(panelX + 4, y, panelW - 8, rowH);

            // Row background color by winner.
            Texture2D rowBg = m.winner switch
            {
                Team.Robot => robotRowBg,
                Team.Mutant => mutantRowBg,
                _ => drawRowBg
            };
            GUI.DrawTexture(rowRect, rowBg);

            // Winner indicator dot.
            Color dotColor = m.winner switch
            {
                Team.Robot => new Color(0.3f, 0.55f, 1f),
                Team.Mutant => new Color(0.3f, 0.85f, 0.25f),
                _ => new Color(0.8f, 0.8f, 0.3f)
            };
            GUI.color = dotColor;
            GUI.DrawTexture(new Rect(panelX + 30, y + 6, 6, 6), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string winnerText = m.winner switch
            {
                Team.Robot => "Robots",
                Team.Mutant => "Mutants",
                _ => "Draw"
            };

            GUI.Label(new Rect(panelX + 8, y, 30, rowH), $"{m.matchNumber}", historyRowStyle);
            GUI.Label(new Rect(panelX + 38, y, 90, rowH), winnerText, historyRowStyle);
            GUI.Label(new Rect(panelX + 130, y, 60, rowH), $"{m.steps}", historyRowStyle);
            GUI.Label(new Rect(panelX + 195, y, 140, rowH), $"R:{m.robotTiles}  vs  M:{m.mutantTiles}", historyRowStyle);

            y += rowH;
        }
    }

    private static void DrawBar(Rect rect, float fill, Color color)
    {
        // Background.
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.6f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        // Fill.
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
