using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wires all menu button click handlers at runtime.
/// Attached to the MenuCanvas alongside MainMenuController.
/// Finds buttons by name and connects them to the correct methods.
/// </summary>
[RequireComponent(typeof(MainMenuController))]
public class MainMenuButtonWiring : MonoBehaviour
{
    private MainMenuController menu;

    private void Awake()
    {
        menu = GetComponent<MainMenuController>();
        WireAllButtons();
    }

    private void WireAllButtons()
    {
        // ── Main Panel ──────────────────────────────────────────────
        WireBtn("MainPanel/PlayBtn", menu.ShowPlay);
        WireBtn("MainPanel/ReplaysBtn", menu.ShowReplays);
        WireBtn("MainPanel/SettingsBtn", menu.ShowSettings);
        WireBtn("MainPanel/QuitBtn", menu.QuitGame);

        // ── Play Panel ──────────────────────────────────────────────
        var play = GetComponentInChildren<PlaySetupPanel>(true);
        if (play != null)
        {
            WireBtn("PlayPanel/RobotCard", play.SelectRobots);
            WireBtn("PlayPanel/MutantCard", play.SelectMutants);
            WireBtn("PlayPanel/StartBtn", play.OnStartMatch);
            WireBtn("PlayPanel/BoardPrev", play.BoardSizePrev);
            WireBtn("PlayPanel/BoardNext", play.BoardSizeNext);
            WireBtn("PlayPanel/DiffPrev", play.DifficultyPrev);
            WireBtn("PlayPanel/DiffNext", play.DifficultyNext);
            WireBtn("PlayPanel/BackPlay", play.OnBack);
        }

        // ── Replays Panel ───────────────────────────────────────────
        var replays = GetComponentInChildren<ReplaysPanel>(true);
        if (replays != null)
        {
            WireBtn("ReplaysPanel/WatchBtn", replays.OnWatch);
            WireBtn("ReplaysPanel/DeleteBtn", replays.OnDelete);
            WireBtn("ReplaysPanel/BackReplays", replays.OnBack);
        }

        // ── Settings Panel ──────────────────────────────────────────
        var settings = GetComponentInChildren<SettingsPanel>(true);
        if (settings != null)
        {
            WireBtn("SettingsPanel/AISpeedPrev", settings.AiSpeedPrev);
            WireBtn("SettingsPanel/AISpeedNext", settings.AiSpeedNext);
            WireBtn("SettingsPanel/ApplyBtn", settings.OnApply);
            WireBtn("SettingsPanel/BackSettings", settings.OnBack);
        }
    }

    private void WireBtn(string path, UnityEngine.Events.UnityAction action)
    {
        var t = transform.Find(path);
        if (t == null) return;
        var btn = t.GetComponent<Button>();
        if (btn == null) return;
        btn.onClick.AddListener(action);
    }
}
