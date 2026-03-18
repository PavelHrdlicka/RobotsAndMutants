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

    // Cumulative stats (persist across resets within one Play session).
    private static long totalTurns;
    private static float sessionStartTime;

    private HexGrid         grid;
    private UnitFactory     unitFactory;
    private AbilitySystem   abilitySystem;

    // MA-POCA agent groups.
    private SimpleMultiAgentGroup robotGroup;
    private SimpleMultiAgentGroup mutantGroup;

    // ── Sequential turn state ──────────────────────────────────────────────
    private readonly List<UnitData> turnOrder = new();
    private int      turnIndex   = -1;
    private UnitData pendingUnit = null;
    private bool     turnStarted = false;

    // Per-game action counters (reset each episode).
    private int robotAttacks, robotBuilds, robotKills;
    private int mutantAttacks, mutantBuilds, mutantKills;

    // ── Initialisation ─────────────────────────────────────────────────────

    /// <summary>Current game state snapshot.</summary>
    public GameState State => new GameState(
        currentRound, maxRounds,
        grid?.CountTiles(Team.Robot) ?? 0,  grid?.CountTiles(Team.Mutant) ?? 0,
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

        abilitySystem = new AbilitySystem(grid);

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

        if (sessionStartTime == 0f)
            sessionStartTime = Time.realtimeSinceStartup;

        Debug.Log($"[GameManager] Ready. {grid.ContestableTileCount} contestable tiles. Max rounds: {maxRounds}.");
    }

    public bool IsReady => grid != null && unitFactory != null;

    // ── Sequential turn loop ───────────────────────────────────────────────

    // Guard against multiple StartNewRound calls in the same frame.
    private int lastRoundStartFrame = -1;

    // Safety: cap how many turns can be processed in a single FixedUpdate.
    private const int MaxTurnsPerFixedUpdate = 1;

    private void FixedUpdate()
    {
        if (gameOver || !IsReady) return;

        // Bootstrap / new round — at most once per frame to prevent freeze.
        if (!turnStarted)
        {
            if (Time.frameCount == lastRoundStartFrame) return;
            lastRoundStartFrame = Time.frameCount;
            StartNewRound();
            return;
        }

        // Check whether the pending unit has completed its turn.
        // Process at most one turn per FixedUpdate to prevent frame starvation.
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

        // Safety: force game-over if max rounds exceeded (even if no one acted).
        if (currentRound > maxRounds)
        {
            int rTiles = grid.CountTiles(Team.Robot);
            int mTiles = grid.CountTiles(Team.Mutant);
            if      (rTiles > mTiles) EndGame(Team.Robot,  0.5f, -0.5f, rTiles, mTiles, "Max rounds.");
            else if (mTiles > rTiles) EndGame(Team.Mutant, 0.5f, -0.5f, rTiles, mTiles, "Max rounds.");
            else                      EndGame(Team.None,   0f,    0f,   rTiles, mTiles, "Max rounds. Draw.");
            return;
        }

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
            // Round complete — tick respawn cooldowns.
            unitFactory.RespawnReady();
            // Let FixedUpdate start the next round (one per frame max).
            turnStarted = false;
            return;
        }

        pendingUnit = turnOrder[turnIndex];

        // Safety: if the pending unit's GO is inactive (died mid-round but somehow
        // still in list), skip it.
        if (pendingUnit == null || !pendingUnit.gameObject.activeInHierarchy)
        {
            AdvanceTurn(); // skip to next (safe: list is finite)
            return;
        }

        pendingUnit.isMyTurn = true;
    }

    private void PostTurnProcessing(UnitData unit)
    {
        totalTurns++;

        // Track per-team action stats.
        if (unit.isAlive)
        {
            bool isRobot = unit.team == Team.Robot;
            switch (unit.lastAction)
            {
                case UnitAction.Attack:
                    if (isRobot) robotAttacks++; else mutantAttacks++;
                    // Check if the defender died → count as kill (scan enemy team only).
                    var enemies = isRobot ? unitFactory.mutantUnits : unitFactory.robotUnits;
                    foreach (var u in enemies)
                    {
                        if (!u.isAlive && u.lastAction == UnitAction.Dead
                            && HexCoord.Distance(unit.currentHex, u.currentHex) <= 1)
                        {
                            if (isRobot) robotKills++; else mutantKills++;
                            break;
                        }
                    }
                    break;
                case UnitAction.BuildCrate:
                case UnitAction.SpreadSlime:
                    if (isRobot) robotBuilds++; else mutantBuilds++;
                    break;
            }
        }

        CheckWinCondition();
    }

    // ── Win condition ──────────────────────────────────────────────────────

    private void CheckWinCondition()
    {
        int   robotTiles  = grid.CountTiles(Team.Robot);
        int   mutantTiles = grid.CountTiles(Team.Mutant);
        float total       = grid.ContestableTileCount > 0 ? grid.ContestableTileCount : 1f;
        float robotRatio  = robotTiles  / total;
        float mutantRatio = mutantTiles / total;

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
        currentRound  = 0;
        gameOver      = false;
        winner        = Team.None;
        turnStarted   = false;
        pendingUnit   = null;
        turnIndex     = -1;
        robotAttacks  = 0; robotBuilds  = 0; robotKills  = 0;
        mutantAttacks = 0; mutantBuilds = 0; mutantKills = 0;
        turnOrder.Clear();

        foreach (var tile in grid.Tiles.Values)
            tile.ResetTile();

        unitFactory.ClearUnits();
        unitFactory.SpawnAllUnits();

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
        // Persist running total to PlayerPrefs every game.
        PlayerPrefs.SetInt("TotalGames", PlayerPrefs.GetInt("TotalGames", 0) + 1);
        long prev = (long)PlayerPrefs.GetInt("TotalTurnsHi", 0) << 32
                  | (uint)PlayerPrefs.GetInt("TotalTurnsLo", 0);
        // Save round count for this episode as turns delta.
        long updated = prev + currentRound;
        PlayerPrefs.SetInt("TotalTurnsHi", (int)(updated >> 32));
        PlayerPrefs.SetInt("TotalTurnsLo", (int)(updated & 0xFFFFFFFF));

        robotGroup?.EndGroupEpisode();
        mutantGroup?.EndGroupEpisode();

        foreach (var unit in unitFactory.AllUnits)
        {
            var agent = unit.GetComponent<HexAgent>();
            if (agent != null) agent.enabled = false;
            var dr = unit.GetComponent<DecisionRequester>();
            if (dr != null) dr.enabled = false;
            unit.isMyTurn = false;
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

    // ── Helpers ────────────────────────────────────────────────────────────

    private int CountAlive(Team team)
    {
        var units = team == Team.Robot ? unitFactory.robotUnits : unitFactory.mutantUnits;
        int count = 0;
        foreach (var u in units) if (u.isAlive) count++;
        return count;
    }

    private string cachedModelInfo;
    private float modelInfoCacheTime;

    private string GetModelInfo()
    {
        // Cache for 2 seconds to avoid per-frame lookups.
        if (cachedModelInfo != null && Time.unscaledTime - modelInfoCacheTime < 2f)
            return cachedModelInfo;
        modelInfoCacheTime = Time.unscaledTime;

        if (unitFactory == null || unitFactory.robotUnits.Count == 0)
        {
            cachedModelInfo = "Model: none (random)";
            return cachedModelInfo;
        }

        var bp = unitFactory.robotUnits[0].GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
        if (bp != null && bp.Model != null)
            cachedModelInfo = $"Model: {bp.Model.name} (trained)";
        else
            cachedModelInfo = "Model: none (heuristic/random)";

        return cachedModelInfo;
    }

    // ── GUI ────────────────────────────────────────────────────────────────

    // ── GUI styles & icons ──────────────────────────────────────────────
    private GUIStyle panelStyle;
    private GUIStyle teamTitleStyle;
    private GUIStyle statStyle;
    private GUIStyle statNumStyle;
    private GUIStyle roundStyle;
    private GUIStyle roundSubStyle;
    private GUIStyle gameOverStyle;
    private Texture2D robotBg, mutantBg, darkBg;

    // Pixel-art icons (16x16).
    private Texture2D iconRobot, iconMutant, iconSwords, iconSkull, iconHammer, iconTiles;
    private bool stylesInitialized;

    private void InitStyles()
    {
        if (stylesInitialized) return;
        stylesInitialized = true;

        robotBg  = MakeTex(1, 1, new Color(0.08f, 0.15f, 0.40f, 0.90f));
        mutantBg = MakeTex(1, 1, new Color(0.08f, 0.28f, 0.08f, 0.90f));
        darkBg   = MakeTex(1, 1, new Color(0.04f, 0.04f, 0.08f, 0.85f));

        panelStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(8, 8, 6, 6) };

        teamTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleCenter
        };

        statStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = new Color(0.75f, 0.75f, 0.7f) },
            alignment = TextAnchor.MiddleLeft
        };

        statNumStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14, fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 1f, 0.95f) },
            alignment = TextAnchor.MiddleLeft
        };

        roundStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleCenter
        };

        roundSubStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            normal = { textColor = new Color(0.65f, 0.65f, 0.55f) },
            alignment = TextAnchor.MiddleCenter
        };

        gameOverStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24, fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.85f, 0.2f) },
            alignment = TextAnchor.MiddleCenter
        };

        // Generate pixel-art icons.
        iconRobot  = MakeIconRobot();
        iconMutant = MakeIconMutant();
        iconSwords = MakeIconSwords();
        iconSkull  = MakeIconSkull();
        iconHammer = MakeIconHammer();
        iconTiles  = MakeIconTiles();
    }

    // ── Pixel-art icon generators (16x16) ─────────────────────────────

    private static Texture2D MakeIcon(int size, System.Action<Color[], int> draw)
    {
        var tex = new Texture2D(size, size) { filterMode = FilterMode.Point };
        var px = new Color[size * size];
        draw(px, size);
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static void Set(Color[] px, int s, int x, int y, Color c)
    {
        if (x >= 0 && x < s && y >= 0 && y < s) px[y * s + x] = c;
    }

    private static Texture2D MakeIconRobot()
    {
        return MakeIcon(16, (px, s) =>
        {
            Color b = new Color(0.2f, 0.45f, 1f), d = new Color(0.12f, 0.25f, 0.6f),
                  e = new Color(1f, 0.95f, 0.3f);
            // Head (6x4 at top center)
            for (int x = 5; x <= 10; x++) for (int y = 11; y <= 14; y++) Set(px, s, x, y, b);
            // Eyes
            Set(px, s, 6, 13, e); Set(px, s, 9, 13, e);
            // Body (8x5)
            for (int x = 4; x <= 11; x++) for (int y = 5; y <= 10; y++) Set(px, s, x, y, d);
            // Legs
            for (int y = 1; y <= 4; y++) { Set(px, s, 5, y, b); Set(px, s, 6, y, b); Set(px, s, 9, y, b); Set(px, s, 10, y, b); }
            // Arms
            for (int y = 6; y <= 9; y++) { Set(px, s, 3, y, b); Set(px, s, 12, y, b); }
        });
    }

    private static Texture2D MakeIconMutant()
    {
        return MakeIcon(16, (px, s) =>
        {
            Color g = new Color(0.3f, 0.85f, 0.2f), d = new Color(0.2f, 0.6f, 0.12f),
                  e = new Color(1f, 0.2f, 0.1f);
            // Body blob (circle-ish)
            for (int x = 3; x <= 12; x++) for (int y = 2; y <= 9; y++)
            {
                float dx = x - 7.5f, dy = y - 5.5f;
                if (dx * dx + dy * dy < 22) Set(px, s, x, y, g);
            }
            // Head
            for (int x = 5; x <= 10; x++) for (int y = 10; y <= 14; y++)
            {
                float dx = x - 7.5f, dy = y - 12f;
                if (dx * dx + dy * dy < 10) Set(px, s, x, y, d);
            }
            // Eyes
            Set(px, s, 6, 13, e); Set(px, s, 9, 13, e);
            // Tentacles
            for (int y = 3; y <= 6; y++) { Set(px, s, 1, y, g); Set(px, s, 2, y, g); Set(px, s, 13, y, g); Set(px, s, 14, y, g); }
        });
    }

    private static Texture2D MakeIconSwords()
    {
        return MakeIcon(16, (px, s) =>
        {
            Color r = new Color(1f, 0.3f, 0.2f), h = new Color(0.6f, 0.4f, 0.2f);
            // Two diagonals crossing
            for (int i = 2; i <= 13; i++)
            {
                Set(px, s, i, i, r); Set(px, s, i + 1, i, r);
                Set(px, s, 15 - i, i, r); Set(px, s, 14 - i, i, r);
            }
            // Handles
            Set(px, s, 2, 2, h); Set(px, s, 3, 3, h); Set(px, s, 13, 2, h); Set(px, s, 12, 3, h);
        });
    }

    private static Texture2D MakeIconSkull()
    {
        return MakeIcon(16, (px, s) =>
        {
            Color w = new Color(0.9f, 0.9f, 0.85f), d = new Color(0.15f, 0.15f, 0.15f);
            // Cranium (circle)
            for (int x = 3; x <= 12; x++) for (int y = 6; y <= 14; y++)
            {
                float dx = x - 7.5f, dy = y - 10f;
                if (dx * dx + dy * dy < 20) Set(px, s, x, y, w);
            }
            // Eye sockets
            for (int x = 5; x <= 6; x++) for (int y = 10; y <= 11; y++) Set(px, s, x, y, d);
            for (int x = 9; x <= 10; x++) for (int y = 10; y <= 11; y++) Set(px, s, x, y, d);
            // Nose
            Set(px, s, 7, 9, d); Set(px, s, 8, 9, d);
            // Jaw
            for (int x = 5; x <= 10; x++) for (int y = 5; y <= 6; y++) Set(px, s, x, y, w);
            for (int x = 5; x <= 10; x++) Set(px, s, x, 4, d); // teeth line
        });
    }

    private static Texture2D MakeIconHammer()
    {
        return MakeIcon(16, (px, s) =>
        {
            Color o = new Color(1f, 0.65f, 0.15f), h = new Color(0.55f, 0.35f, 0.15f);
            // Handle (vertical)
            for (int y = 1; y <= 10; y++) { Set(px, s, 7, y, h); Set(px, s, 8, y, h); }
            // Head (horizontal block)
            for (int x = 3; x <= 12; x++) for (int y = 11; y <= 14; y++) Set(px, s, x, y, o);
        });
    }

    private static Texture2D MakeIconTiles()
    {
        return MakeIcon(16, (px, s) =>
        {
            Color c = new Color(0.3f, 0.8f, 1f), d = new Color(0.15f, 0.4f, 0.5f);
            // 2x2 grid of small squares
            for (int x = 2; x <= 6; x++) for (int y = 9; y <= 13; y++) Set(px, s, x, y, c);
            for (int x = 9; x <= 13; x++) for (int y = 9; y <= 13; y++) Set(px, s, x, y, d);
            for (int x = 2; x <= 6; x++) for (int y = 2; y <= 6; y++) Set(px, s, x, y, d);
            for (int x = 9; x <= 13; x++) for (int y = 2; y <= 6; y++) Set(px, s, x, y, c);
        });
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

    // ── HUD cache (avoid recomputing every OnGUI call) ──────────────────

    private float hudCacheTime;
    private const float HudCacheInterval = 0.15f;
    private GameState cachedState;
    private float cachedRobotPct, cachedMutantPct;
    private int cachedRobotAlive, cachedMutantAlive;
    private int cachedRobotTotal, cachedMutantTotal;

    private void RefreshHudCache()
    {
        if (Time.unscaledTime - hudCacheTime < HudCacheInterval) return;
        hudCacheTime = Time.unscaledTime;

        cachedState = State;
        float total = grid != null && grid.ContestableTileCount > 0 ? grid.ContestableTileCount : 1f;
        cachedRobotPct  = cachedState.robotTiles  / total * 100f;
        cachedMutantPct = cachedState.mutantTiles / total * 100f;

        // Cache alive counts to avoid iterating units in OnGUI.
        cachedRobotAlive  = cachedState.robotAlive;
        cachedMutantAlive = cachedState.mutantAlive;
        cachedRobotTotal  = unitFactory != null ? unitFactory.robotUnits.Count  : 0;
        cachedMutantTotal = unitFactory != null ? unitFactory.mutantUnits.Count : 0;

        RefreshStatsStrings();
    }

    // ── Main HUD ──────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (grid == null) return;

        InitStyles();
        RefreshHudCache();

        var state = cachedState;
        float robotPct  = cachedRobotPct;
        float mutantPct = cachedMutantPct;

        const float panelW = 210f;
        const float panelH = 120f;
        const float centerW = 240f;
        const float centerH = 52f;
        const float margin = 8f;

        float centerX = (Screen.width - centerW) * 0.5f;

        // --- Round counter (top center) ---
        panelStyle.normal.background = darkBg;
        GUI.Box(new Rect(centerX, margin, centerW, centerH), "", panelStyle);
        GUI.Label(new Rect(centerX, margin + 4, centerW, 26),
                  $"Round {state.currentRound} / {state.maxRounds}", roundStyle);
        GUI.Label(new Rect(centerX, margin + 30, centerW, 16), GetModelInfo(), roundSubStyle);

        // --- Robot panel (left of center) ---
        float robotX = centerX - panelW - margin;
        DrawTeamPanel(robotX, margin, panelW, panelH,
            "ROBOTS", robotBg, iconRobot,
            new Color(0.3f, 0.5f, 0.95f),
            cachedRobotTotal, cachedRobotAlive, state.robotTiles, robotPct,
            robotAttacks, robotKills, robotBuilds);

        // --- Mutant panel (right of center) ---
        float mutantX = centerX + centerW + margin;
        DrawTeamPanel(mutantX, margin, panelW, panelH,
            "MUTANTS", mutantBg, iconMutant,
            new Color(0.35f, 0.85f, 0.25f),
            cachedMutantTotal, cachedMutantAlive, state.mutantTiles, mutantPct,
            mutantAttacks, mutantKills, mutantBuilds);

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

        // --- Session Stats + Match History (bottom-left) ---
        DrawSessionStats();
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

    private Texture2D statsBg;

    // Cached stats strings (rebuilt in RefreshHudCache).
    private string statsLine1 = "", statsLine2 = "", statsLine3 = "", statsLine4 = "";

    private void RefreshStatsStrings()
    {
        float elapsed = Time.realtimeSinceStartup - sessionStartTime;
        string timeStr = elapsed < 60f ? $"{elapsed:F0}s"
                       : elapsed < 3600 ? $"{elapsed / 60f:F1}m"
                       : $"{elapsed / 3600f:F1}h";
        float tps = elapsed > 1f ? totalTurns / elapsed : 0f;

        statsLine1 = $"Session  |  {matchCounter} games   {totalTurns:N0} turns   {timeStr}";
        statsLine2 = $"Speed: {tps:F0} turns/s   Avg: {(matchCounter > 0 ? totalTurns / matchCounter : 0):N0} turns/game";

        long allGames  = PlayerPrefs.GetInt("TotalGames", 0);
        long allRounds = (long)PlayerPrefs.GetInt("TotalTurnsHi", 0) << 32
                       | (uint)PlayerPrefs.GetInt("TotalTurnsLo", 0);
        statsLine3 = $"ALL TIME: {allGames:N0} games   {allRounds:N0} rounds";

        // Training stats for current model.
        string trainRunId  = PlayerPrefs.GetString("TrainedRunId", "");
        int trainGames     = PlayerPrefs.GetInt("TrainedOnGames", 0);
        int trainRounds    = PlayerPrefs.GetInt("TrainedOnRounds", 0);
        int trainSteps     = PlayerPrefs.GetInt("TrainedSteps", 0);
        if (!string.IsNullOrEmpty(trainRunId))
            statsLine4 = $"MODEL ({trainRunId}): {trainSteps:N0} steps  |  {trainGames:N0} games  {trainRounds:N0} rounds";
        else
            statsLine4 = "";
    }

    private void DrawSessionStats()
    {
        InitHistoryStyles();

        if (statsBg == null)
            statsBg = MakeTex(1, 1, new Color(0.06f, 0.06f, 0.14f, 0.85f));

        const float panelW = 340f;
        const float margin = 10f;
        bool hasModelLine = !string.IsNullOrEmpty(statsLine4);
        float panelH = hasModelLine ? 94f : 74f;

        float historyH = matchHistory.Count > 0
            ? 24f + 20f + matchHistory.Count * 18f + 12f + 4 : 0f;
        float panelX = margin;
        float panelY = Screen.height - historyH - panelH - margin - 4f;

        GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), statsBg);

        float y = panelY + 4f;
        GUI.Label(new Rect(panelX + 8, y, panelW - 16, 18), statsLine1, historyHeaderStyle);
        y += 20f;
        GUI.Label(new Rect(panelX + 8, y, panelW - 16, 18), statsLine2, historyRowStyle);
        y += 20f;
        GUI.Label(new Rect(panelX + 8, y, panelW - 16, 18), statsLine3, historyHeaderStyle);
        if (hasModelLine)
        {
            y += 20f;
            GUI.Label(new Rect(panelX + 8, y, panelW - 16, 18), statsLine4, historyRowStyle);
        }
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

    private void DrawTeamPanel(float px, float py, float pw, float ph,
        string title, Texture2D bg, Texture2D unitIcon, Color teamColor,
        int unitTotal, int aliveCount, int tiles, float tilePct,
        int attacks, int kills, int builds)
    {
        const float iconS = 16f;
        const float gap = 3f;

        panelStyle.normal.background = bg;
        GUI.Box(new Rect(px, py, pw, ph), "", panelStyle);

        // Team title.
        GUI.Label(new Rect(px, py + 2, pw, 24), title, teamTitleStyle);

        // Alive row: bright icons (left = alive), dim (right = dead).
        float rowX = px + 10;
        float rowY = py + 28;
        for (int i = 0; i < unitTotal; i++)
        {
            GUI.color = i < aliveCount ? Color.white : new Color(1, 1, 1, 0.2f);
            GUI.DrawTexture(new Rect(rowX + i * (iconS + gap), rowY, iconS, iconS), unitIcon);
        }
        GUI.color = Color.white;

        // Stats row 1: Swords + attacks,  Skull + kills
        float sy1 = py + 50;
        GUI.DrawTexture(new Rect(px + 10, sy1, iconS, iconS), iconSwords);
        GUI.Label(new Rect(px + 30, sy1, 40, iconS), $"{attacks}", statNumStyle);
        GUI.DrawTexture(new Rect(px + 75, sy1, iconS, iconS), iconSkull);
        GUI.Label(new Rect(px + 95, sy1, 40, iconS), $"{kills}", statNumStyle);

        // Stats row 2: Hammer + builds,  Tiles + count
        float sy2 = py + 72;
        GUI.DrawTexture(new Rect(px + 10, sy2, iconS, iconS), iconHammer);
        GUI.Label(new Rect(px + 30, sy2, 40, iconS), $"{builds}", statNumStyle);
        GUI.DrawTexture(new Rect(px + 75, sy2, iconS, iconS), iconTiles);
        GUI.Label(new Rect(px + 95, sy2, 100, iconS), $"{tiles} ({tilePct:F0}%)", statNumStyle);

        // Territory bar.
        DrawBar(new Rect(px + 10, py + 96, pw - 20, 6), tilePct / 100f, teamColor);
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
