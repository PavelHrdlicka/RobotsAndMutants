using UnityEngine;

/// <summary>
/// OnGUI HUD: team panels, round counter, session stats, match history.
/// All pixel-art icon generators live here.
/// </summary>
public partial class GameManager
{
    // ── GUI styles ─────────────────────────────────────────────────────────

    private GUIStyle panelStyle;
    private GUIStyle teamTitleStyle;
    private GUIStyle statStyle;
    private GUIStyle statNumStyle;
    private GUIStyle roundStyle;
    private GUIStyle roundSubStyle;
    private GUIStyle gameOverStyle;
    private Texture2D robotBg, mutantBg, darkBg;

    // Pixel-art icons (16×16).
    private Texture2D iconRobot, iconMutant, iconSwords, iconSkull, iconHammer, iconTiles;
    private bool stylesInitialized;

    private GUIStyle historyHeaderStyle;
    private GUIStyle historyRowStyle;
    private Texture2D robotRowBg;
    private Texture2D mutantRowBg;
    private Texture2D drawRowBg;
    private Texture2D historyPanelBg;
    private bool historyStylesInit;

    private Texture2D statsBg;

    // ── HUD data cache ─────────────────────────────────────────────────────

    private float hudCacheTime;
    private const float HudCacheInterval = 0.15f;
    private GameState cachedState;
    private float cachedRobotPct, cachedMutantPct;
    private int cachedRobotAlive, cachedMutantAlive;
    private int cachedRobotTotal, cachedMutantTotal;

    // Cached stats strings (rebuilt every HudCacheInterval).
    private string statsLine1 = "", statsLine2 = "", statsLine3 = "", statsLine4 = "";

    // ── Style initialisation ───────────────────────────────────────────────

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

        iconRobot  = MakeIconRobot();
        iconMutant = MakeIconMutant();
        iconSwords = MakeIconSwords();
        iconSkull  = MakeIconSkull();
        iconHammer = MakeIconHammer();
        iconTiles  = MakeIconTiles();
    }

    private void InitHistoryStyles()
    {
        if (historyStylesInit) return;
        historyStylesInit = true;

        robotRowBg     = MakeTex(1, 1, new Color(0.12f, 0.22f, 0.50f, 0.70f));
        mutantRowBg    = MakeTex(1, 1, new Color(0.12f, 0.38f, 0.12f, 0.70f));
        drawRowBg      = MakeTex(1, 1, new Color(0.30f, 0.30f, 0.15f, 0.70f));
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

    // ── HUD cache refresh ──────────────────────────────────────────────────

    private void RefreshHudCache()
    {
        if (Time.unscaledTime - hudCacheTime < HudCacheInterval) return;
        hudCacheTime = Time.unscaledTime;

        cachedState = State;
        float total = grid != null && grid.ContestableTileCount > 0 ? grid.ContestableTileCount : 1f;
        cachedRobotPct  = cachedState.robotTiles  / total * 100f;
        cachedMutantPct = cachedState.mutantTiles / total * 100f;

        cachedRobotAlive  = cachedState.robotAlive;
        cachedMutantAlive = cachedState.mutantAlive;
        cachedRobotTotal  = unitFactory != null ? unitFactory.robotUnits.Count  : 0;
        cachedMutantTotal = unitFactory != null ? unitFactory.mutantUnits.Count : 0;

        RefreshStatsStrings();
    }

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

        string trainRunId = PlayerPrefs.GetString("TrainedRunId", "");
        int trainGames    = PlayerPrefs.GetInt("TrainedOnGames", 0);
        int trainRounds   = PlayerPrefs.GetInt("TrainedOnRounds", 0);
        int trainSteps    = PlayerPrefs.GetInt("TrainedSteps", 0);
        statsLine4 = !string.IsNullOrEmpty(trainRunId)
            ? $"MODEL ({trainRunId}): {trainSteps:N0} steps  |  {trainGames:N0} games  {trainRounds:N0} rounds"
            : "";
    }

    // ── Main HUD ───────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (grid == null) return;

        InitStyles();
        RefreshHudCache();

        // Silent training: only show match history + session stats, no game visuals.
        if (GameConfig.SilentTraining)
        {
            DrawSessionStats();
            DrawMatchHistory();
            return;
        }

        var state = cachedState;
        float robotPct  = cachedRobotPct;
        float mutantPct = cachedMutantPct;

        const float panelW  = 210f;
        const float panelH  = 120f;
        const float centerW = 240f;
        const float centerH = 52f;
        const float margin  = 8f;

        float centerX = (Screen.width - centerW) * 0.5f;

        // Round counter (top center).
        panelStyle.normal.background = darkBg;
        GUI.Box(new Rect(centerX, margin, centerW, centerH), "", panelStyle);
        GUI.Label(new Rect(centerX, margin + 4, centerW, 26),
                  $"Round {state.currentRound} / {state.maxRounds}", roundStyle);
        GUI.Label(new Rect(centerX, margin + 30, centerW, 16), GetModelInfo(), roundSubStyle);

        // Robot panel (left of center).
        float robotX = centerX - panelW - margin;
        DrawTeamPanel(robotX, margin, panelW, panelH,
            "ROBOTS", robotBg, iconRobot,
            new Color(0.3f, 0.5f, 0.95f),
            cachedRobotTotal, cachedRobotAlive, state.robotTiles, robotPct,
            robotAttacks, robotKills, robotBuilds);

        // Mutant panel (right of center).
        float mutantX = centerX + centerW + margin;
        DrawTeamPanel(mutantX, margin, panelW, panelH,
            "MUTANTS", mutantBg, iconMutant,
            new Color(0.35f, 0.85f, 0.25f),
            cachedMutantTotal, cachedMutantAlive, state.mutantTiles, mutantPct,
            mutantAttacks, mutantKills, mutantBuilds);

        // Game over banner — suppress during replay playback (gameOver is used to block game loop).
        var replayPlayer = GetComponent<ReplayPlayer>();
        bool isReplayActive = replayPlayer != null && replayPlayer.enabled && replayPlayer.Replay != null;
        bool showGameOver = state.gameOver && (!isReplayActive || replayPlayer.state == ReplayPlayer.PlaybackState.Finished);

        if (showGameOver)
        {
            float bannerW = 400f;
            float bannerX = (Screen.width - bannerW) * 0.5f;
            panelStyle.normal.background = darkBg;
            GUI.Box(new Rect(bannerX, Screen.height * 0.4f, bannerW, 50), "", panelStyle);

            string winText;
            if (isReplayActive)
            {
                string replayWinner = replayPlayer.Replay?.summary.winner ?? "";
                winText = string.IsNullOrEmpty(replayWinner) || replayWinner == "Draw"
                    ? "DRAW!"
                    : $"{replayWinner.ToUpper()}S WIN!";
            }
            else
            {
                winText = state.winner == Team.None
                    ? "DRAW!"
                    : $"{state.winner.ToString().ToUpper()}S WIN!";
            }
            GUI.Label(new Rect(bannerX, Screen.height * 0.4f + 8, bannerW, 34), winText, gameOverStyle);

            // Post-match buttons (only in HumanVsAI or menu-launched games).
            if (GameModeConfig.CurrentMode == GameMode.HumanVsAI || GameModeConfig.LaunchedFromMenu)
            {
                float btnW = 130f;
                float btnH = 40f;
                float btnY = Screen.height * 0.4f + 65f;
                float totalW = btnW * 3 + 20;
                float startX = bannerX + (bannerW - totalW) * 0.5f;

                if (GUI.Button(new Rect(startX, btnY, btnW, btnH), "Back to Menu"))
                    MainMenuController.ReturnToMainMenu();

                if (GUI.Button(new Rect(startX + btnW + 10, btnY, btnW, btnH), "Watch Replay"))
                {
                    string replayPath = replayLogger.LastCompletedReplayPath;
                    if (!string.IsNullOrEmpty(replayPath) && System.IO.File.Exists(replayPath))
                    {
                        GameModeConfig.CurrentMode = GameMode.Replay;
                        ReplayPlayer.PendingReplayPath = replayPath;
                        UnityEngine.SceneManagement.SceneManager.LoadScene(
                            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                    }
                }

                if (GUI.Button(new Rect(startX + (btnW + 10) * 2, btnY, btnW, btnH), "Rematch"))
                    RematchRequested = true;
            }
        }

        DrawSessionStats();
        DrawMatchHistory();

        if (GameModeConfig.CurrentMode == GameMode.HumanVsAI)
        {
            DrawHumanPlayerHUD();
            DrawTurnLog();
        }
    }

    // ── Human player HUD ──────────────────────────────────────────────────

    private GUIStyle humanHudStyle;
    private GUIStyle actionButtonStyle;
    private GUIStyle activeActionButtonStyle;

    private void DrawHumanPlayerHUD()
    {
        if (humanHudStyle == null)
        {
            humanHudStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            humanHudStyle.normal.textColor = Color.white;
        }
        if (actionButtonStyle == null)
        {
            actionButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 13 };
        }
        if (activeActionButtonStyle == null)
        {
            activeActionButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            activeActionButtonStyle.normal.textColor = Color.yellow;
        }

        var inputMgr = FindFirstObjectByType<HumanInputManager>();
        if (inputMgr == null) return;

        // Check which unit is active (human-controlled).
        bool isHumanTurn = pendingUnit != null && pendingUnit.isMyTurn
                           && pendingUnit.GetComponent<HumanTurnController>() != null;
        HexMovement activeMovement = isHumanTurn ? pendingUnit.GetComponent<HexMovement>() : null;

        // Check which action modes have at least one valid target.
        bool canMove = false, canAttack = false, canBuild = false, canDestroy = false;
        if (activeMovement != null)
        {
            for (int dir = 0; dir < 6; dir++)
            {
                if (activeMovement.IsValidMove(dir))    canMove = true;
                if (activeMovement.IsValidAttack(dir))  canAttack = true;
                if (activeMovement.IsValidBuild(dir))   canBuild = true;
                if (activeMovement.IsValidDestroyWall(dir)) canDestroy = true;
            }
        }

        const float barW = 580f;
        const float barH = 40f;
        float barX = (Screen.width - barW) * 0.5f;
        float barY = Screen.height - barH - 60f;

        // Background.
        GUI.DrawTexture(new Rect(barX, barY, barW, barH),
            MakeTex(1, 1, new Color(0.1f, 0.1f, 0.2f, 0.85f)));

        // Action buttons — disabled if no valid targets.
        float btnW = 100f;
        float btnH = 30f;
        float btnY = barY + 5f;
        float startX = barX + 10f;

        var modes = new[] {
            (HumanActionMode.Move,        "[1] Move",    canMove),
            (HumanActionMode.Attack,      "[2] Attack",  canAttack),
            (HumanActionMode.Build,       "[3] Build",   canBuild),
            (HumanActionMode.DestroyWall, "[4] Destroy", canDestroy)
        };

        for (int i = 0; i < modes.Length; i++)
        {
            bool enabled = isHumanTurn && modes[i].Item3;
            GUI.enabled = enabled;

            var style = inputMgr.ActionMode == modes[i].Item1 && enabled
                ? activeActionButtonStyle : actionButtonStyle;
            if (GUI.Button(new Rect(startX + i * (btnW + 5f), btnY, btnW, btnH),
                modes[i].Item2, style))
            {
                inputMgr.ActionMode = modes[i].Item1;
            }
        }

        // Idle button — always available during human turn.
        GUI.enabled = isHumanTurn;
        if (GUI.Button(new Rect(startX + 4 * (btnW + 5f), btnY, 80f, btnH),
            "[Space] Idle", actionButtonStyle))
        {
            inputMgr.IdleRequested = true;
        }
        GUI.enabled = true;

        // Active unit info above the toolbar.
        if (isHumanTurn)
        {
            float thinking = Time.realtimeSinceStartup - turnStartTime;
            string info = $"Your turn: {pendingUnit.DisplayName}  |  Energy: {pendingUnit.Energy}/{pendingUnit.maxEnergy}  |  {thinking:F1}s";
            GUI.Label(new Rect(barX, barY - 28f, barW, 24f), info, humanHudStyle);

            // Average thinking time.
            if (humanTurnCount > 0)
            {
                string stats = $"Avg think: {avgHumanThinkTime:F1}s  |  Turns: {humanTurnCount}";
                GUI.Label(new Rect(barX, barY - 48f, barW, 20f), stats, humanHudStyle);
            }
        }
        else if (pendingUnit != null && pendingUnit.isMyTurn)
        {
            string info = $"AI thinking: {pendingUnit.DisplayName}";
            GUI.Label(new Rect(barX, barY - 28f, barW, 24f), info, humanHudStyle);
        }
    }

    // ── Turn log (last 10 turns) ─────────────────────────────────────────

    private GUIStyle turnLogStyle;
    private GUIStyle turnLogHeaderStyle;
    private GUIStyle turnLogNumStyle;
    private Texture2D turnLogBg;

    /// <summary>Format unit name for display: "Robot_1" → "Robot 1".</summary>
    private static string FormatUnitName(string name)
    {
        return name != null ? name.Replace("_", " ") : "";
    }

    private void DrawTurnLog()
    {
        if (turnLog.Count == 0) return;

        if (turnLogStyle == null)
        {
            turnLogStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };
            turnLogStyle.normal.textColor = Color.white;
        }
        if (turnLogHeaderStyle == null)
        {
            turnLogHeaderStyle = new GUIStyle(turnLogStyle)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };
        }
        if (turnLogNumStyle == null)
        {
            turnLogNumStyle = new GUIStyle(turnLogStyle)
            {
                alignment = TextAnchor.MiddleRight
            };
            turnLogNumStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        }

        if (turnLogBg == null)
            turnLogBg = MakeTex(1, 1, new Color(0.06f, 0.06f, 0.14f, 0.80f));

        // Column layout:  #(24)  Unit(90)  Action(70)
        const float colNum    = 24f;
        const float colUnit   = 90f;
        const float colAction = 70f;
        const float pad       = 8f;
        const float logW      = pad + colNum + colUnit + colAction + pad;
        const float rowH      = 17f;
        const float headerH   = 22f;
        const float subHeaderH = 16f;
        float logH = headerH + subHeaderH + turnLog.Count * rowH + pad;
        float logX = Screen.width - logW - 10f;
        float logY = Screen.height - logH - 120f;

        GUI.DrawTexture(new Rect(logX, logY, logW, logH), turnLogBg);

        float y = logY + 3f;

        // Title.
        GUI.Label(new Rect(logX + pad, y, logW - pad * 2, headerH), "Last Turns", turnLogHeaderStyle);
        y += headerH;

        // Column headers.
        turnLogNumStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        GUI.Label(new Rect(logX + pad, y, colNum, subHeaderH), "#", turnLogNumStyle);
        turnLogStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        GUI.Label(new Rect(logX + pad + colNum, y, colUnit, subHeaderH), "Unit", turnLogStyle);
        GUI.Label(new Rect(logX + pad + colNum + colUnit, y, colAction, subHeaderH), "Action", turnLogStyle);
        y += subHeaderH;

        // Separator line.
        GUI.color = new Color(0.4f, 0.4f, 0.4f, 0.4f);
        GUI.DrawTexture(new Rect(logX + pad, y - 1, logW - pad * 2, 1), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Rows (newest first).
        for (int i = turnLog.Count - 1; i >= 0; i--)
        {
            var entry = turnLog[i];
            int displayNum = turnLog.Count - i; // 1 = newest

            Color rowColor = entry.team == Team.Robot
                ? new Color(0.5f, 0.7f, 1f)
                : new Color(0.5f, 1f, 0.5f);

            // Row number (grey).
            turnLogNumStyle.normal.textColor = new Color(0.45f, 0.45f, 0.45f);
            GUI.Label(new Rect(logX + pad, y, colNum, rowH), $"{displayNum}", turnLogNumStyle);

            // Unit name (team color, no underscore).
            turnLogStyle.normal.textColor = rowColor;
            GUI.Label(new Rect(logX + pad + colNum, y, colUnit, rowH), FormatUnitName(entry.unitName), turnLogStyle);

            // Action (team color, aligned).
            GUI.Label(new Rect(logX + pad + colNum + colUnit, y, colAction, rowH), entry.action.ToString(), turnLogStyle);

            y += rowH;
        }

        turnLogStyle.normal.textColor = Color.white;
    }

    // ── Team panel ─────────────────────────────────────────────────────────

    private void DrawTeamPanel(float px, float py, float pw, float ph,
        string title, Texture2D bg, Texture2D unitIcon, Color teamColor,
        int unitTotal, int aliveCount, int tiles, float tilePct,
        int attacks, int kills, int builds)
    {
        const float iconS = 16f;
        const float gap   = 3f;

        panelStyle.normal.background = bg;
        GUI.Box(new Rect(px, py, pw, ph), "", panelStyle);

        GUI.Label(new Rect(px, py + 2, pw, 24), title, teamTitleStyle);

        float rowX = px + 10;
        float rowY = py + 28;
        for (int i = 0; i < unitTotal; i++)
        {
            GUI.color = i < aliveCount ? Color.white : new Color(1, 1, 1, 0.2f);
            GUI.DrawTexture(new Rect(rowX + i * (iconS + gap), rowY, iconS, iconS), unitIcon);
        }
        GUI.color = Color.white;

        float sy1 = py + 50;
        GUI.DrawTexture(new Rect(px + 10, sy1, iconS, iconS), iconSwords);
        GUI.Label(new Rect(px + 30, sy1, 40, iconS), $"{attacks}", statNumStyle);
        GUI.DrawTexture(new Rect(px + 75, sy1, iconS, iconS), iconSkull);
        GUI.Label(new Rect(px + 95, sy1, 40, iconS), $"{kills}", statNumStyle);

        float sy2 = py + 72;
        GUI.DrawTexture(new Rect(px + 10, sy2, iconS, iconS), iconHammer);
        GUI.Label(new Rect(px + 30, sy2, 40, iconS), $"{builds}", statNumStyle);
        GUI.DrawTexture(new Rect(px + 75, sy2, iconS, iconS), iconTiles);
        GUI.Label(new Rect(px + 95, sy2, 100, iconS), $"{tiles} ({tilePct:F0}%)", statNumStyle);

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

    // ── Session stats ──────────────────────────────────────────────────────

    private void DrawSessionStats()
    {
        InitHistoryStyles();

        if (statsBg == null)
            statsBg = MakeTex(1, 1, new Color(0.06f, 0.06f, 0.14f, 0.85f));

        const float panelW = 360f;
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

    // ── Match history ──────────────────────────────────────────────────────

    private void DrawMatchHistory()
    {
        if (matchHistory.Count == 0) return;
        InitHistoryStyles();

        // Column layout (x offsets from panelX):
        //  8  : # (20)
        // 28  : Winner (58) + dot at 84
        // 88  : Rounds (38)
        // 128 : Score% (46)
        // 176 : Atk-R (28)  204 : Atk-M (28)
        // 234 : Die-R (28)  262 : Die-M (28)
        // 292 : Bld-R (28)  320 : Bld-M (28)
        bool showTime = GameModeConfig.CurrentMode == GameMode.HumanVsAI;
        float panelW  = showTime ? 410f : 360f;
        const float rowH    = 18f;
        const float titleH  = 24f;
        const float headerH = 36f;   // two-line header (group + sub)
        const float pad     = 6f;
        const float margin  = 10f;
        float panelH  = titleH + headerH + matchHistory.Count * rowH + pad * 2 + 4;
        float panelX  = margin;
        float panelY  = Screen.height - panelH - margin;

        GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), historyPanelBg);

        // Title row.
        float y       = panelY + pad;
        int robotWins = 0, mutantWins = 0, draws = 0;
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

        // Group header row (top line).
        GUI.Label(new Rect(panelX +   8, y, 174, 16), "",            historyHeaderStyle);  // spacer
        GUI.Label(new Rect(panelX + 176, y,  56, 16), "Atk/rnd",    historyHeaderStyle);
        GUI.Label(new Rect(panelX + 234, y,  56, 16), "Dth/rnd",    historyHeaderStyle);
        GUI.Label(new Rect(panelX + 292, y,  56, 16), "Bld/rnd",    historyHeaderStyle);
        if (showTime) GUI.Label(new Rect(panelX + 350, y, 50, 16), "Time", historyHeaderStyle);
        y += 16f;

        // Sub-header row (bottom line).
        GUI.Label(new Rect(panelX +   8, y,  20, 18), "#",       historyHeaderStyle);
        GUI.Label(new Rect(panelX +  28, y,  58, 18), "Winner",  historyHeaderStyle);
        GUI.Label(new Rect(panelX +  88, y,  38, 18), "Rounds",  historyHeaderStyle);
        GUI.Label(new Rect(panelX + 128, y,  46, 18), "Score%",  historyHeaderStyle);
        GUI.Label(new Rect(panelX + 176, y,  26, 18), "R",       historyHeaderStyle);
        GUI.Label(new Rect(panelX + 204, y,  26, 18), "M",       historyHeaderStyle);
        GUI.Label(new Rect(panelX + 234, y,  26, 18), "R",       historyHeaderStyle);
        GUI.Label(new Rect(panelX + 262, y,  26, 18), "M",       historyHeaderStyle);
        GUI.Label(new Rect(panelX + 292, y,  26, 18), "R",       historyHeaderStyle);
        GUI.Label(new Rect(panelX + 320, y,  26, 18), "M",       historyHeaderStyle);
        y += 20f;

        GUI.color = new Color(0.5f, 0.5f, 0.4f, 0.5f);
        GUI.DrawTexture(new Rect(panelX + 6, y - 1, panelW - 12, 1), Texture2D.whiteTexture);
        GUI.color = Color.white;

        for (int i = matchHistory.Count - 1; i >= 0; i--)
        {
            var m        = matchHistory[i];
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
            GUI.DrawTexture(new Rect(panelX + 78, y + 6, 6, 6), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string winnerText = m.winner switch
            {
                Team.Robot  => "Robots",
                Team.Mutant => "Mutants",
                _           => "Draw"
            };
            string scoreText = m.winner == Team.None ? "-" : $"{m.winnerPct:F0}%";

            float r = Mathf.Max(m.rounds, 1);
            GUI.Label(new Rect(panelX +   8, y,  20, rowH), $"{m.matchNumber}",                historyRowStyle);
            GUI.Label(new Rect(panelX +  28, y,  58, rowH), winnerText,                        historyRowStyle);
            GUI.Label(new Rect(panelX +  88, y,  38, rowH), $"{m.rounds}",                     historyRowStyle);
            GUI.Label(new Rect(panelX + 128, y,  46, rowH), scoreText,                         historyRowStyle);
            GUI.Label(new Rect(panelX + 176, y,  26, rowH), $"{m.robotAttacks  / r:F1}",       historyRowStyle);
            GUI.Label(new Rect(panelX + 204, y,  26, rowH), $"{m.mutantAttacks / r:F1}",       historyRowStyle);
            GUI.Label(new Rect(panelX + 234, y,  26, rowH), $"{m.robotDeaths   / r:F1}",       historyRowStyle);
            GUI.Label(new Rect(panelX + 262, y,  26, rowH), $"{m.mutantDeaths  / r:F1}",       historyRowStyle);
            GUI.Label(new Rect(panelX + 292, y,  26, rowH), $"{m.robotBuilds   / r:F1}",       historyRowStyle);
            GUI.Label(new Rect(panelX + 320, y,  26, rowH), $"{m.mutantBuilds  / r:F1}",       historyRowStyle);

            if (showTime)
            {
                int mins = (int)(m.durationSeconds / 60f);
                int secs = (int)(m.durationSeconds % 60f);
                GUI.Label(new Rect(panelX + 350, y, 50, rowH), $"{mins}:{secs:D2}", historyRowStyle);
            }

            y += rowH;
        }
    }

    // ── Pixel-art icon generators (16×16) ──────────────────────────────────

    private static Texture2D MakeIcon(int size, System.Action<Color[], int> draw)
    {
        var tex = new Texture2D(size, size) { filterMode = FilterMode.Point };
        var px  = new Color[size * size];
        draw(px, size);
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static void Set(Color[] px, int s, int x, int y, Color c)
    {
        if (x >= 0 && x < s && y >= 0 && y < s) px[y * s + x] = c;
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

    private static Texture2D MakeIconRobot()
    {
        return MakeIcon(16, (px, s) =>
        {
            Color b = new Color(0.2f, 0.45f, 1f), d = new Color(0.12f, 0.25f, 0.6f),
                  e = new Color(1f, 0.95f, 0.3f);
            for (int x = 5; x <= 10; x++) for (int y = 11; y <= 14; y++) Set(px, s, x, y, b);
            Set(px, s, 6, 13, e); Set(px, s, 9, 13, e);
            for (int x = 4; x <= 11; x++) for (int y = 5; y <= 10; y++) Set(px, s, x, y, d);
            for (int y = 1; y <= 4; y++) { Set(px, s, 5, y, b); Set(px, s, 6, y, b); Set(px, s, 9, y, b); Set(px, s, 10, y, b); }
            for (int y = 6; y <= 9; y++) { Set(px, s, 3, y, b); Set(px, s, 12, y, b); }
        });
    }

    private static Texture2D MakeIconMutant()
    {
        return MakeIcon(16, (px, s) =>
        {
            Color g = new Color(0.3f, 0.85f, 0.2f), d = new Color(0.2f, 0.6f, 0.12f),
                  e = new Color(1f, 0.2f, 0.1f);
            for (int x = 3; x <= 12; x++) for (int y = 2; y <= 9; y++)
            {
                float dx = x - 7.5f, dy = y - 5.5f;
                if (dx * dx + dy * dy < 22) Set(px, s, x, y, g);
            }
            for (int x = 5; x <= 10; x++) for (int y = 10; y <= 14; y++)
            {
                float dx = x - 7.5f, dy = y - 12f;
                if (dx * dx + dy * dy < 10) Set(px, s, x, y, d);
            }
            Set(px, s, 6, 13, e); Set(px, s, 9, 13, e);
            for (int y = 3; y <= 6; y++) { Set(px, s, 1, y, g); Set(px, s, 2, y, g); Set(px, s, 13, y, g); Set(px, s, 14, y, g); }
        });
    }

    private static Texture2D MakeIconSwords()
    {
        return MakeIcon(16, (px, s) =>
        {
            Color r = new Color(1f, 0.3f, 0.2f), h = new Color(0.6f, 0.4f, 0.2f);
            for (int i = 2; i <= 13; i++)
            {
                Set(px, s, i, i, r); Set(px, s, i + 1, i, r);
                Set(px, s, 15 - i, i, r); Set(px, s, 14 - i, i, r);
            }
            Set(px, s, 2, 2, h); Set(px, s, 3, 3, h); Set(px, s, 13, 2, h); Set(px, s, 12, 3, h);
        });
    }

    private static Texture2D MakeIconSkull()
    {
        return MakeIcon(16, (px, s) =>
        {
            Color w = new Color(0.9f, 0.9f, 0.85f), d = new Color(0.15f, 0.15f, 0.15f);
            for (int x = 3; x <= 12; x++) for (int y = 6; y <= 14; y++)
            {
                float dx = x - 7.5f, dy = y - 10f;
                if (dx * dx + dy * dy < 20) Set(px, s, x, y, w);
            }
            for (int x = 5; x <= 6; x++) for (int y = 10; y <= 11; y++) Set(px, s, x, y, d);
            for (int x = 9; x <= 10; x++) for (int y = 10; y <= 11; y++) Set(px, s, x, y, d);
            Set(px, s, 7, 9, d); Set(px, s, 8, 9, d);
            for (int x = 5; x <= 10; x++) for (int y = 5; y <= 6; y++) Set(px, s, x, y, w);
            for (int x = 5; x <= 10; x++) Set(px, s, x, 4, d);
        });
    }

    private static Texture2D MakeIconHammer()
    {
        return MakeIcon(16, (px, s) =>
        {
            Color o = new Color(1f, 0.65f, 0.15f), h = new Color(0.55f, 0.35f, 0.15f);
            for (int y = 1; y <= 10; y++) { Set(px, s, 7, y, h); Set(px, s, 8, y, h); }
            for (int x = 3; x <= 12; x++) for (int y = 11; y <= 14; y++) Set(px, s, x, y, o);
        });
    }

    private static Texture2D MakeIconTiles()
    {
        return MakeIcon(16, (px, s) =>
        {
            Color c = new Color(0.3f, 0.8f, 1f), d = new Color(0.15f, 0.4f, 0.5f);
            for (int x = 2; x <= 6; x++) for (int y = 9; y <= 13; y++) Set(px, s, x, y, c);
            for (int x = 9; x <= 13; x++) for (int y = 9; y <= 13; y++) Set(px, s, x, y, d);
            for (int x = 2; x <= 6; x++) for (int y = 2; y <= 6; y++) Set(px, s, x, y, d);
            for (int x = 9; x <= 13; x++) for (int y = 2; y <= 6; y++) Set(px, s, x, y, c);
        });
    }
}
