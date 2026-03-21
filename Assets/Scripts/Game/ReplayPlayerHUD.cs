using UnityEngine;
using UnityEngine.InputSystem;

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

    private void Update()
    {
        if (player == null || player.Replay == null) return;
        if (player.state == ReplayPlayer.PlaybackState.Stopped) return;

        // Keyboard controls (new Input System).
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.spaceKey.wasPressedThisFrame)
            player.TogglePlayPause();

        if (kb.rightArrowKey.wasPressedThisFrame)
        {
            player.Pause();
            player.StepOneTurn();
        }

        if (kb.leftArrowKey.wasPressedThisFrame)
        {
            player.Pause();
            player.StepBackOneTurn();
        }

        if (kb.upArrowKey.wasPressedThisFrame)
        {
            player.Pause();
            player.StepOneRound();
        }

        if (kb.downArrowKey.wasPressedThisFrame)
        {
            player.Pause();
            player.StepBackOneRound();
        }
    }

    private void OnGUI()
    {
        if (player == null || player.state == ReplayPlayer.PlaybackState.Stopped) return;
        if (player.Replay == null) return;

        InitStyles();

        float barHeight = 130;
        float barY = Screen.height - barHeight;
        float barWidth = Mathf.Min(Screen.width * 0.55f, 520);
        float barX = (Screen.width - barWidth) / 2f;

        // Background panel.
        GUI.DrawTexture(new Rect(barX - 10, barY - 10, barWidth + 20, barHeight + 10), barBg);

        float y = barY;

        // Header: REPLAY badge + file info.
        GUI.Label(new Rect(barX, y, barWidth, 25), $"REPLAY — {player.FileName} (match #{player.Replay.header.match})", headerStyle);
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
        y += 20;

        // Previous + current turn descriptions.
        string prevDesc = player.PreviousTurnDescription;
        string curDesc = player.CurrentTurnDescription;
        if (!string.IsNullOrEmpty(prevDesc))
        {
            var dimStyle = new GUIStyle(infoStyle) { normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(barX, y, barWidth, 16), $"Prev: {prevDesc}", dimStyle);
            y += 16;
        }
        if (!string.IsNullOrEmpty(curDesc))
        {
            var nextStyle = new GUIStyle(infoStyle) { normal = { textColor = new Color(0.9f, 0.9f, 0.7f) }, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(barX, y, barWidth, 16), $"Next: {curDesc}", nextStyle);
            y += 16;
        }
        y += 4;

        // Transport buttons.
        float btnH = 28;
        float btnW = 60;
        float btnGap = 3;
        float btnX = barX;

        // Play/Pause.
        string playLabel = player.state == ReplayPlayer.PlaybackState.Playing ? "||" : ">";
        if (GUI.Button(new Rect(btnX, y, 36, btnH), playLabel, buttonStyle))
            player.TogglePlayPause();
        btnX += 39;

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
    }
}
