using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Replays panel: lists saved replays, allows watching or deleting.
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

    private struct ReplayEntry
    {
        public string filePath;
        public string fileName;
        public string date;
        public string result;
        public string duration;
        public int matchNum;
    }

    private void OnEnable()
    {
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

    public void SelectEntry(int index)
    {
        selectedIndex = index;
        UpdateSelection();
    }

    // ── List management ─────────────────────────────────────────────────

    private void RefreshList()
    {
        entries.Clear();
        selectedIndex = -1;

        // Clear existing UI rows.
        if (listContent != null)
        {
            for (int i = listContent.childCount - 1; i >= 0; i--)
                Destroy(listContent.GetChild(i).gameObject);
        }

        string replayDir = Path.GetFullPath("Replays");
        if (!Directory.Exists(replayDir))
        {
            UpdateEmptyState();
            return;
        }

        var files = Directory.GetFiles(replayDir, "game_*.jsonl");
        System.Array.Sort(files, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));

        foreach (string file in files)
        {
            var info = new FileInfo(file);
            if (info.Length < 100) continue; // skip incomplete

            var entry = ParseReplayFile(file);
            if (entry.fileName == null) continue;

            entries.Add(entry);
            CreateRow(entries.Count - 1, entry);
        }

        UpdateEmptyState();
        UpdateSelection();
    }

    private ReplayEntry ParseReplayFile(string filePath)
    {
        var entry = new ReplayEntry { filePath = filePath, fileName = Path.GetFileName(filePath) };

        try
        {
            using var reader = new StreamReader(filePath);
            string firstLine = reader.ReadLine();
            string lastLine = null;
            string line;
            while ((line = reader.ReadLine()) != null)
                lastLine = line;

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

            // Parse summary for winner and duration.
            if (lastLine != null && lastLine.Contains("\"type\":\"summary\""))
            {
                if (lastLine.Contains("\"winner\":\"Robot\"")) entry.result = "Robots win";
                else if (lastLine.Contains("\"winner\":\"Mutant\"")) entry.result = "Mutants win";
                else entry.result = "Draw";

                // Duration.
                int dIdx = lastLine.IndexOf("\"duration_sec\":");
                if (dIdx >= 0)
                {
                    dIdx += 15;
                    int dEnd = lastLine.IndexOf('}', dIdx);
                    if (dEnd < 0) dEnd = lastLine.Length;
                    string dStr = lastLine.Substring(dIdx, dEnd - dIdx).Trim().TrimEnd(',', '}');
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

    private void CreateRow(int index, ReplayEntry entry)
    {
        if (listContent == null || replayRowPrefab == null) return;

        var row = Instantiate(replayRowPrefab, listContent);
        row.SetActive(true);

        var texts = row.GetComponentsInChildren<Text>();
        if (texts.Length >= 4)
        {
            texts[0].text = $"#{entry.matchNum}";
            texts[1].text = entry.date ?? "-";
            texts[2].text = entry.result ?? "-";
            texts[3].text = entry.duration ?? "-";
        }

        var btn = row.GetComponent<Button>();
        if (btn != null)
        {
            int captured = index;
            btn.onClick.AddListener(() => SelectEntry(captured));
        }
    }

    private void UpdateEmptyState()
    {
        bool empty = entries.Count == 0;
        if (noReplaysText != null)
            noReplaysText.gameObject.SetActive(empty);
        if (watchButton != null)
            watchButton.gameObject.SetActive(!empty);
        if (deleteButton != null)
            deleteButton.gameObject.SetActive(!empty);
    }

    private void UpdateSelection()
    {
        if (watchButton != null) watchButton.interactable = selectedIndex >= 0;
        if (deleteButton != null) deleteButton.interactable = selectedIndex >= 0;

        // Highlight selected row.
        if (listContent != null)
        {
            for (int i = 0; i < listContent.childCount; i++)
            {
                var img = listContent.GetChild(i).GetComponent<Image>();
                if (img != null)
                    img.color = i == selectedIndex
                        ? new Color(0.3f, 0.5f, 1f, 0.3f)
                        : new Color(0.15f, 0.15f, 0.15f, 0.5f);
            }
        }
    }
}
