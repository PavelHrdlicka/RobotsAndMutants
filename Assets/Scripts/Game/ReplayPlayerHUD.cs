using UnityEngine;

/// <summary>
/// OnGUI overlay for replay playback controls: play/pause, step, speed, round scrubber.
/// Attaches to the same GameObject as ReplayPlayer.
/// </summary>
[RequireComponent(typeof(ReplayPlayer))]
public class ReplayPlayerHUD : MonoBehaviour
{
    private ReplayPlayer player;
    private GUIStyle labelStyle;
    private GUIStyle headerStyle;
    private GUIStyle buttonStyle;
    private GUIStyle infoStyle;
    private Texture2D barBg;

    private void Awake()
    {
        player = GetComponent<ReplayPlayer>();
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
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.84f, 0f) }
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            fixedHeight = 35
        };

        infoStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
        };
    }

    private void OnGUI()
    {
        if (player == null || player.state == ReplayPlayer.PlaybackState.Stopped) return;
        if (player.Replay == null) return;

        InitStyles();

        float barHeight = 165;
        float barY = Screen.height - barHeight;
        float barWidth = Mathf.Min(Screen.width * 0.7f, 700);
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
        float btnW = 80;
        float btnGap = 5;
        float btnX = barX;

        // Play/Pause.
        string playLabel = player.state == ReplayPlayer.PlaybackState.Playing ? "||" : ">";
        if (GUI.Button(new Rect(btnX, y, 50, 35), playLabel, buttonStyle))
            player.TogglePlayPause();
        btnX += 55;

        // Step Back.
        if (GUI.Button(new Rect(btnX, y, btnW, 35), "Back", buttonStyle))
        {
            player.Pause();
            player.StepBackOneTurn();
        }
        btnX += btnW + btnGap;

        // Step Turn.
        if (GUI.Button(new Rect(btnX, y, btnW, 35), "Step", buttonStyle))
        {
            player.Pause();
            player.StepOneTurn();
        }
        btnX += btnW + btnGap;

        // Step Round.
        if (GUI.Button(new Rect(btnX, y, btnW + 10, 35), "Round+", buttonStyle))
        {
            player.Pause();
            player.StepOneRound();
        }
        btnX += btnW + 15;

        // Speed label + slider.
        GUI.Label(new Rect(btnX, y, 50, 35), $"{1f / Mathf.Max(player.turnDelay, 0.01f):F0}/s", labelStyle);
        btnX += 50;
        float sliderWidth = barX + barWidth - btnX - 10;
        if (sliderWidth > 50)
        {
            // Map turnDelay: 1.0 (slow) to 0.01 (fast). Slider goes 0.01-1.0, inverted for intuition.
            float speed = GUI.HorizontalSlider(new Rect(btnX, y + 12, sliderWidth, 20), 1f / Mathf.Max(player.turnDelay, 0.01f), 1f, 100f);
            player.turnDelay = 1f / Mathf.Max(speed, 1f);
        }
        y += 38;

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
