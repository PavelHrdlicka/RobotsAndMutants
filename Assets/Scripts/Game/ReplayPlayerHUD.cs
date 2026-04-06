using UnityEngine;

/// <summary>
/// OnGUI overlay for replay playback controls: play/pause, step, speed, round scrubber.
/// Attaches to the same GameObject as ReplayPlayer.
/// </summary>
[RequireComponent(typeof(ReplayPlayer))]
public class ReplayPlayerHUD : MonoBehaviour
{
    private ReplayPlayer player;
    private ReplayDebugOverlay debugOverlay;
    private GUIStyle labelStyle;
    private GUIStyle headerStyle;
    private GUIStyle buttonStyle;
    private GUIStyle infoStyle;
    private GUIStyle toggleButtonOnStyle;
    private Texture2D barBg;

    private void Awake()
    {
        player = GetComponent<ReplayPlayer>();
        debugOverlay = GetComponent<ReplayDebugOverlay>();
        if (debugOverlay == null)
            debugOverlay = gameObject.AddComponent<ReplayDebugOverlay>();
    }

    private void InitStyles()
    {
        if (labelStyle != null) return;

        barBg = new Texture2D(1, 1);
        barBg.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.15f, 0.85f));
        barBg.Apply();

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.84f, 0f) }
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            fixedHeight = 28
        };

        infoStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
        };

        toggleButtonOnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            fixedHeight = 28,
            normal = { textColor = new Color(1f, 0.84f, 0f) }
        };
    }

    private void OnGUI()
    {
        if (player == null || player.state == ReplayPlayer.PlaybackState.Stopped) return;
        if (player.Replay == null) return;

        // Keyboard controls via Event.current (works with any input backend).
        if (Event.current.type == EventType.KeyDown)
        {
            switch (Event.current.keyCode)
            {
                case KeyCode.Space:
                    if (player.state != ReplayPlayer.PlaybackState.Finished)
                        player.TogglePlayPause();
                    Event.current.Use();
                    break;
                case KeyCode.R:
                    player.Restart();
                    Event.current.Use();
                    break;
                case KeyCode.RightArrow:
                    player.Pause();
                    player.StepOneTurn();
                    Event.current.Use();
                    break;
                case KeyCode.LeftArrow:
                    player.Pause();
                    player.StepBackOneTurn();
                    Event.current.Use();
                    break;
                case KeyCode.UpArrow:
                    player.Pause();
                    player.StepOneRound();
                    Event.current.Use();
                    break;
                case KeyCode.DownArrow:
                    player.Pause();
                    player.StepBackOneRound();
                    Event.current.Use();
                    break;
            }
        }

        InitStyles();

        float barHeight = 100;
        float barY = Screen.height - barHeight;
        float barWidth = Mathf.Min(Screen.width * 0.55f, 520);
        float barX = (Screen.width - barWidth) / 2f;

        // Background panel.
        GUI.DrawTexture(new Rect(barX - 10, barY - 10, barWidth + 20, barHeight + 10), barBg);

        float y = barY;

        // Back to Menu button (top-right of bar).
        float menuBtnW = 100;
        if (GUI.Button(new Rect(barX + barWidth - menuBtnW, y, menuBtnW, 24), "Back to Menu", buttonStyle))
            MainMenuController.ReturnToMainMenu();

        // Header: human-readable game info (no JSON filename).
        GUI.Label(new Rect(barX, y, barWidth - menuBtnW - 5, 25), $"REPLAY — {player.DisplayTitle}", headerStyle);
        y += 25;

        // Round info + state.
        string stateLabel = player.state switch
        {
            ReplayPlayer.PlaybackState.Playing => "Playing",
            ReplayPlayer.PlaybackState.Paused => "Paused",
            ReplayPlayer.PlaybackState.Finished => "Finished",
            _ => ""
        };
        GUI.Label(new Rect(barX, y, barWidth, 20),
            $"Round {player.currentRound}/{player.TotalRounds}  |  Turn {player.currentTurnIndex}/{player.TotalTurns}  |  {stateLabel}",
            labelStyle);
        y += 24;

        // Transport buttons.
        float btnH = 28;
        float btnW = 60;
        float btnGap = 3;
        float btnX = barX;

        // Play/Pause or Restart when finished.
        if (player.state == ReplayPlayer.PlaybackState.Finished)
        {
            if (GUI.Button(new Rect(btnX, y, 70, btnH), "Restart", buttonStyle))
                player.Restart();
            btnX += 73;
        }
        else
        {
            string playLabel = player.state == ReplayPlayer.PlaybackState.Playing ? "||" : ">";
            if (GUI.Button(new Rect(btnX, y, 36, btnH), playLabel, buttonStyle))
                player.TogglePlayPause();
            btnX += 39;
        }

        // Step Back.
        if (GUI.Button(new Rect(btnX, y, btnW, btnH), "Back", buttonStyle))
        {
            player.Pause();
            player.StepBackOneTurn();
        }
        btnX += btnW + btnGap;

        // Step Turn.
        if (GUI.Button(new Rect(btnX, y, btnW, btnH), "Step", buttonStyle))
        {
            player.Pause();
            player.StepOneTurn();
        }
        btnX += btnW + btnGap;

        // Step Round.
        if (GUI.Button(new Rect(btnX, y, 70, btnH), "Round+", buttonStyle))
        {
            player.Pause();
            player.StepOneRound();
        }
        btnX += 73;

        // Speed label + slider.
        GUI.Label(new Rect(btnX, y, 35, btnH), $"{1f / Mathf.Max(player.turnDelay, 0.01f):F0}/s", labelStyle);
        btnX += 35;

        // Show Detail toggle.
        float detailBtnW = 100;
        float detailBtnX = barX + barWidth - detailBtnW;
        bool isDetailOn = debugOverlay != null && debugOverlay.showDetail;
        var detailStyle = isDetailOn ? toggleButtonOnStyle : buttonStyle;
        string detailLabel = isDetailOn ? "HIDE" : "DETAIL";
        if (GUI.Button(new Rect(detailBtnX, y, detailBtnW, btnH), detailLabel, detailStyle))
            debugOverlay?.Toggle();

        float sliderWidth = detailBtnX - btnX - 10;
        if (sliderWidth > 30)
        {
            float speed = GUI.HorizontalSlider(new Rect(btnX, y + 10, sliderWidth, 20), 1f / Mathf.Max(player.turnDelay, 0.01f), 1f, 100f);
            player.turnDelay = 1f / Mathf.Max(speed, 1f);
        }
        y += 32;

        // Round scrubber.
        if (player.TotalRounds > 0)
        {
            GUI.Label(new Rect(barX, y, 50, 20), "Rnd:", labelStyle);
            float newRound = GUI.HorizontalSlider(new Rect(barX + 50, y + 5, barWidth - 60, 20), player.currentRound, 0, player.TotalRounds);
            int targetRound = Mathf.RoundToInt(newRound);
            if (targetRound != player.currentRound && Mathf.Abs(newRound - player.currentRound) > 0.5f)
            {
                player.Pause();
                player.JumpToRound(targetRound);
            }
        }

        // Keyboard hints.
        var hintStyle = new GUIStyle(infoStyle) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(barX, barY - 22, barWidth, 16), "Space: Play/Pause | Arrows: Step | R: Restart", hintStyle);

        // Turn log (last 10 applied turns).
        DrawReplayTurnLog();
    }

    // ── Replay Turn Log ─────────────────────────────────────────────────

    private GUIStyle turnLogStyle;
    private GUIStyle turnLogHeaderStyle;
    private GUIStyle turnLogNumStyle;
    private GUIStyle turnLogRowBtnStyle;
    private Texture2D turnLogBg;
    private Texture2D turnLogHoverBg;

    private void DrawReplayTurnLog()
    {
        if (player.Replay == null || player.currentTurnIndex <= 0) return;

        if (turnLogStyle == null)
        {
            turnLogStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, alignment = TextAnchor.MiddleLeft
            };
            turnLogStyle.normal.textColor = Color.white;
        }
        if (turnLogHeaderStyle == null)
        {
            turnLogHeaderStyle = new GUIStyle(turnLogStyle)
            {
                fontStyle = FontStyle.Bold, fontSize = 12
            };
        }
        if (turnLogNumStyle == null)
        {
            turnLogNumStyle = new GUIStyle(turnLogStyle) { alignment = TextAnchor.MiddleRight };
            turnLogNumStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        }
        if (turnLogBg == null)
        {
            turnLogBg = new Texture2D(1, 1);
            turnLogBg.SetPixel(0, 0, new Color(0.06f, 0.06f, 0.14f, 0.80f));
            turnLogBg.Apply();
        }
        if (turnLogHoverBg == null)
        {
            turnLogHoverBg = new Texture2D(1, 1);
            turnLogHoverBg.SetPixel(0, 0, new Color(0.25f, 0.25f, 0.4f, 0.6f));
            turnLogHoverBg.Apply();
        }
        if (turnLogRowBtnStyle == null)
        {
            turnLogRowBtnStyle = new GUIStyle(GUIStyle.none);
            turnLogRowBtnStyle.hover.background = turnLogHoverBg;
            turnLogRowBtnStyle.active.background = turnLogHoverBg;
        }

        var turns = player.Replay.turns;
        int endIdx = player.currentTurnIndex; // last applied turn
        int startIdx = Mathf.Max(0, endIdx - 10);
        int count = endIdx - startIdx;

        const float colNum = 22f;
        const float colRnd = 30f;
        const float colUnit = 90f;
        const float colAction = 80f;
        const float pad = 8f;
        const float logW = pad + colNum + colRnd + colUnit + colAction + pad;
        const float rowH = 17f;
        const float headerH = 22f;
        const int maxVisibleRows = 10;
        float logH = headerH + maxVisibleRows * rowH + pad;
        float logX = Screen.width - logW - 10f;
        float logY = Screen.height - logH - 160f;

        GUI.DrawTexture(new Rect(logX, logY, logW, logH), turnLogBg);

        float ly = logY + 3f;
        GUI.Label(new Rect(logX + pad, ly, logW - pad * 2, headerH), "Last Turns", turnLogHeaderStyle);
        ly += headerH;

        // Rows (newest first, numbered 1-10). Clickable — jumps to that turn.
        int rowNum = 1;
        for (int i = endIdx - 1; i >= startIdx; i--)
        {
            var t = turns[i];
            Color rowColor = t.team == "Robot"
                ? new Color(0.5f, 0.7f, 1f)
                : new Color(0.5f, 1f, 0.5f);

            // Clickable row background (invisible button with hover highlight).
            if (GUI.Button(new Rect(logX, ly, logW, rowH), GUIContent.none, turnLogRowBtnStyle))
            {
                player.Pause();
                player.JumpToTurn(i + 1); // +1 because JumpToTurn applies turns [0..target)
            }

            float cx = logX + pad;

            // Row number.
            turnLogNumStyle.normal.textColor = new Color(0.45f, 0.45f, 0.45f);
            GUI.Label(new Rect(cx, ly, colNum, rowH), $"{rowNum}", turnLogNumStyle);
            cx += colNum;

            // Round.
            GUI.Label(new Rect(cx, ly, colRnd, rowH), $"R{t.round}", turnLogNumStyle);
            cx += colRnd;

            // Unit name.
            turnLogStyle.normal.textColor = rowColor;
            string name = t.unitName != null ? t.unitName.Replace("_", " ") : "";
            GUI.Label(new Rect(cx, ly, colUnit, rowH), name, turnLogStyle);
            cx += colUnit;

            // Action (human-readable).
            string action = t.energy <= 0 ? "\u23F3 Dead" : GameManager.FormatAction(t.action);
            GUI.Label(new Rect(cx, ly, colAction, rowH), action, turnLogStyle);

            ly += rowH;
            rowNum++;
        }

        turnLogStyle.normal.textColor = Color.white;
    }
}
