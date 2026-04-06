using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Replays panel: lists saved replays, allows watching or deleting.
/// Uses OnGUI for rendering (Canvas serialized references are unreliable across scene rebuilds).
/// </summary>
public class ReplaysPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MainMenuController menuController;
    [SerializeField] private Transform listContent;
    [SerializeField] private GameObject replayRowPrefab;
    [SerializeField] private Button watchButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Text noReplaysText;

    private readonly List<ReplayEntry> entries = new();
    private int selectedIndex = -1;
    private Vector2 scrollPos;

    private struct ReplayEntry
    {
        public string filePath;
        public string fileName;
        public string date;
        public string result;
        public string duration;
        public int matchNum;
        public bool favorite;
    }

    private bool showFavoritesOnly;

    // GUI styles (lazy init).
    private GUIStyle headerStyle, rowStyle, selectedRowStyle, buttonStyle, hintStyle;
    private Texture2D darkBg, rowBg, selectedBg;
    private bool stylesInit;

    private void OnEnable()
    {
        // Hide all Canvas children — OnGUI handles rendering now.
        for (int i = 0; i < transform.childCount; i++)
            transform.GetChild(i).gameObject.SetActive(false);

        RefreshList();
    }

    public void OnBack()
    {
        if (menuController != null)
            menuController.ShowMain();
    }

    public void OnWatch()
    {
        if (selectedIndex < 0 || selectedIndex >= entries.Count) return;
        if (menuController != null)
            menuController.WatchReplay(entries[selectedIndex].filePath);
    }

    public void OnDelete()
    {
        if (selectedIndex < 0 || selectedIndex >= entries.Count) return;

        string path = entries[selectedIndex].filePath;
        if (File.Exists(path))
        {
            File.Delete(path);
            RefreshList();
        }
    }

    // ── List management ─────────────────────────────────────────────────

    private void RefreshList()
    {
        entries.Clear();
        selectedIndex = -1;

        string replayDir = GameReplayLogger.HumanVsAIReplayDir;
        if (!Directory.Exists(replayDir)) return;

        var files = Directory.GetFiles(replayDir, "game_*.jsonl");
        System.Array.Sort(files, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));

        foreach (string file in files)
        {
            var info = new FileInfo(file);
            if (info.Length < 100) continue;

            var entry = ParseReplayFile(file);
            if (entry.fileName == null) continue;

            entry.favorite = IsFavorite(entry.fileName);
            entries.Add(entry);
        }
    }

    private ReplayEntry ParseReplayFile(string filePath)
    {
        var entry = new ReplayEntry { filePath = filePath, fileName = Path.GetFileName(filePath) };

        try
        {
            using var reader = new StreamReader(filePath);
            string firstLine = reader.ReadLine();
            string summaryLine = null;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Contains("\"type\":\"summary\""))
                    summaryLine = line;
            }

            // Parse header for match number.
            if (firstLine != null && firstLine.Contains("\"match\":"))
            {
                int idx = firstLine.IndexOf("\"match\":") + 8;
                int end = firstLine.IndexOf(',', idx);
                if (end > idx && int.TryParse(firstLine.Substring(idx, end - idx), out int m))
                    entry.matchNum = m;
            }

            // Parse date from filename: game_N_YYYYMMDD_HHMMSS.jsonl
            string name = Path.GetFileNameWithoutExtension(filePath);
            var parts = name.Split('_');
            if (parts.Length >= 4)
                entry.date = $"{parts[2].Substring(6, 2)}.{parts[2].Substring(4, 2)} {parts[3].Substring(0, 2)}:{parts[3].Substring(2, 2)}";

            // Parse humanTeam from header.
            string humanTeam = ExtractJsonString(firstLine, "humanTeam");

            // Parse summary for winner and duration.
            if (summaryLine != null)
            {
                string winner = ExtractJsonString(summaryLine, "winner");
                entry.result = ReplayPlayer.FormatWinner(winner, humanTeam);

                int dIdx = summaryLine.IndexOf("\"duration_sec\":");
                if (dIdx >= 0)
                {
                    dIdx += 15;
                    int dEnd = summaryLine.IndexOf('}', dIdx);
                    if (dEnd < 0) dEnd = summaryLine.Length;
                    string dStr = summaryLine.Substring(dIdx, dEnd - dIdx).Trim().TrimEnd(',', '}');
                    if (float.TryParse(dStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float dur))
                    {
                        int mins = (int)(dur / 60f);
                        int secs = (int)(dur % 60f);
                        entry.duration = $"{mins}:{secs:D2}";
                    }
                }
            }
            else
            {
                entry.result = "Incomplete";
            }
        }
        catch
        {
            entry.result = "Error";
        }

        return entry;
    }

    private static string ExtractJsonString(string json, string key)
    {
        if (string.IsNullOrEmpty(json)) return "";
        string pattern = $"\"{key}\":\"";
        int idx = json.IndexOf(pattern, System.StringComparison.Ordinal);
        if (idx < 0) return "";
        int start = idx + pattern.Length;
        int end = json.IndexOf('"', start);
        return end > start ? json.Substring(start, end - start) : "";
    }

    // ── Favorites persistence (PlayerPrefs) ─────────────────────────────

    private static string FavKey(string fileName) => $"ReplayFav_{fileName}";

    private static bool IsFavorite(string fileName) =>
        PlayerPrefs.GetInt(FavKey(fileName), 0) == 1;

    private static void SetFavorite(string fileName, bool fav)
    {
        PlayerPrefs.SetInt(FavKey(fileName), fav ? 1 : 0);
        PlayerPrefs.Save();
    }

    // ── OnGUI rendering ─────────────────────────────────────────────────

    private void InitStyles()
    {
        if (stylesInit) return;
        stylesInit = true;

        darkBg = MakeTex(new Color(0.08f, 0.08f, 0.12f, 0.95f));
        rowBg = MakeTex(new Color(0.12f, 0.12f, 0.18f, 0.8f));
        selectedBg = MakeTex(new Color(0.2f, 0.35f, 0.7f, 0.6f));

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.9f, 0.85f, 0.5f) }
        };

        rowStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13, alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };

        selectedRowStyle = new GUIStyle(rowStyle)
        {
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.95f, 0.6f) }
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14, fontStyle = FontStyle.Bold, fixedHeight = 36
        };

        hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
        };
    }

    private void OnGUI()
    {
        if (!gameObject.activeInHierarchy) return;

        InitStyles();

        float panelW = 560f;
        float panelH = 500f;
        float panelX = (Screen.width - panelW) * 0.5f;
        float panelY = (Screen.height - panelH) * 0.5f;

        // Background.
        GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), darkBg);

        float y = panelY + 12f;
        float innerX = panelX + 16f;
        float innerW = panelW - 32f;

        // Title.
        var titleStyle = new GUIStyle(headerStyle) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(panelX, y, panelW, 36), "REPLAYS", titleStyle);
        y += 40f;

        // Favorites filter toggle.
        string filterLabel = showFavoritesOnly ? "\u2605 Favorites only" : "\u2606 Show all";
        var filterStyle = new GUIStyle(buttonStyle) { fontSize = 12, fixedHeight = 24 };
        if (GUI.Button(new Rect(innerX + innerW - 140, y, 140, 24), filterLabel, filterStyle))
            showFavoritesOnly = !showFavoritesOnly;
        y += 30f;

        // Count visible entries.
        int visibleCount = 0;
        for (int i = 0; i < entries.Count; i++)
            if (!showFavoritesOnly || entries[i].favorite) visibleCount++;

        if (visibleCount == 0)
        {
            string msg = entries.Count == 0 ? "No replays found." : "No favorite replays.";
            GUI.Label(new Rect(panelX, y + 60, panelW, 30), msg, hintStyle);
        }
        else
        {
            // Column headers: Star, #, Date, Result, Time
            GUI.Label(new Rect(innerX, y, 24, 22), "\u2605", headerStyle);
            GUI.Label(new Rect(innerX + 28, y, 30, 22), "#", headerStyle);
            GUI.Label(new Rect(innerX + 62, y, 100, 22), "Date", headerStyle);
            GUI.Label(new Rect(innerX + 172, y, 140, 22), "Result", headerStyle);
            GUI.Label(new Rect(innerX + 330, y, 60, 22), "Time", headerStyle);
            y += 26f;

            // Separator.
            GUI.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            GUI.DrawTexture(new Rect(innerX, y, innerW, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
            y += 4f;

            // Scrollable list.
            float listH = Mathf.Min(visibleCount * 30f, 280f);
            Rect listRect = new Rect(innerX, y, innerW, listH);
            Rect contentRect = new Rect(0, 0, innerW - 20f, visibleCount * 30f);

            scrollPos = GUI.BeginScrollView(listRect, scrollPos, contentRect);
            int rowNum = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (showFavoritesOnly && !e.favorite) continue;

                float rowY = rowNum * 30f;
                Rect rowRect = new Rect(0, rowY, innerW - 20f, 28f);

                GUI.DrawTexture(rowRect, i == selectedIndex ? selectedBg : rowBg);

                var style = i == selectedIndex ? selectedRowStyle : rowStyle;

                // Favorite star (clickable) — drawn BEFORE row button so it receives clicks.
                string star = e.favorite ? "\u2605" : "\u2606";
                var starStyle = new GUIStyle(GUI.skin.button) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
                starStyle.normal.textColor = e.favorite ? new Color(1f, 0.85f, 0.2f) : new Color(0.4f, 0.4f, 0.4f);
                starStyle.normal.background = null;
                starStyle.hover.textColor = new Color(1f, 0.95f, 0.5f);
                if (GUI.Button(new Rect(2, rowY, 24, 28), star, starStyle))
                {
                    var updated = entries[i];
                    updated.favorite = !updated.favorite;
                    entries[i] = updated;
                    SetFavorite(updated.fileName, updated.favorite);
                }

                // Row selection (area after star column).
                if (GUI.Button(new Rect(26, rowY, innerW - 46, 28), "", GUIStyle.none))
                    selectedIndex = i;

                GUI.Label(new Rect(30, rowY, 28, 28), $"{rowNum + 1}", style);
                GUI.Label(new Rect(64, rowY, 100, 28), e.date ?? "-", style);
                GUI.Label(new Rect(174, rowY, 140, 28), e.result ?? "-", style);
                GUI.Label(new Rect(332, rowY, 60, 28), e.duration ?? "-", style);

                rowNum++;
            }
            GUI.EndScrollView();

            y += listH + 8f;
        }

        // Buttons at bottom.
        float btnW = 140f;
        float btnGap = 16f;
        float btnY = panelY + panelH - 80f;
        float totalBtnW = btnW * 2 + btnGap;
        float btnX = panelX + (panelW - totalBtnW) * 0.5f;

        GUI.enabled = selectedIndex >= 0 && selectedIndex < entries.Count;
        if (GUI.Button(new Rect(btnX, btnY, btnW, 36), "Watch Replay", buttonStyle))
            OnWatch();
        GUI.enabled = true;
        btnX += btnW + btnGap;

        if (GUI.Button(new Rect(btnX, btnY, btnW, 36), "< BACK", buttonStyle))
            OnBack();
    }

    private static Texture2D MakeTex(Color color)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }
}
