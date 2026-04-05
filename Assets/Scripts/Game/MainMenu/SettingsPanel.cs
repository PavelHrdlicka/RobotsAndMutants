using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings panel: gameplay options persisted to PlayerPrefs.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MainMenuController menuController;

    [Header("Gameplay")]
    [SerializeField] private Text aiSpeedLabel;
    [SerializeField] private Toggle showCoordsToggle;

    [Header("Graphics")]
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Toggle vsyncToggle;

    private int aiSpeedIndex = 1; // 0=Slow, 1=Normal, 2=Fast
    private static readonly string[] AiSpeedNames = { "Slow", "Normal", "Fast" };
    private static readonly float[] AiSpeedValues = { 1.0f, 0.5f, 0.2f };

    private const string PrefAISpeed = "Settings_AISpeed";
    private const string PrefShowCoords = "Settings_ShowCoords";
    private const string PrefFullscreen = "Settings_Fullscreen";
    private const string PrefVSync = "Settings_VSync";

    private void OnEnable()
    {
        LoadSettings();
        UpdateUI();
    }

    public void OnBack()
    {
        SaveSettings();
        if (menuController != null)
            menuController.ShowMain();
    }

    public void OnApply()
    {
        SaveSettings();
        ApplySettings();
    }

    // ── AI Speed ────────────────────────────────────────────────────────

    public void AiSpeedPrev()
    {
        aiSpeedIndex = Mathf.Max(0, aiSpeedIndex - 1);
        UpdateUI();
    }

    public void AiSpeedNext()
    {
        aiSpeedIndex = Mathf.Min(AiSpeedNames.Length - 1, aiSpeedIndex + 1);
        UpdateUI();
    }

    // ── Persistence ─────────────────────────────────────────────────────

    private void LoadSettings()
    {
        aiSpeedIndex = PlayerPrefs.GetInt(PrefAISpeed, 1);
        if (showCoordsToggle != null)
            showCoordsToggle.isOn = PlayerPrefs.GetInt(PrefShowCoords, 0) == 1;
        if (fullscreenToggle != null)
            fullscreenToggle.isOn = PlayerPrefs.GetInt(PrefFullscreen, Screen.fullScreen ? 1 : 0) == 1;
        if (vsyncToggle != null)
            vsyncToggle.isOn = PlayerPrefs.GetInt(PrefVSync, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetInt(PrefAISpeed, aiSpeedIndex);
        if (showCoordsToggle != null)
            PlayerPrefs.SetInt(PrefShowCoords, showCoordsToggle.isOn ? 1 : 0);
        if (fullscreenToggle != null)
            PlayerPrefs.SetInt(PrefFullscreen, fullscreenToggle.isOn ? 1 : 0);
        if (vsyncToggle != null)
            PlayerPrefs.SetInt(PrefVSync, vsyncToggle.isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplySettings()
    {
        if (fullscreenToggle != null)
            Screen.fullScreen = fullscreenToggle.isOn;
        if (vsyncToggle != null)
            QualitySettings.vSyncCount = vsyncToggle.isOn ? 1 : 0;

        // Store AI speed so GameManager can read it.
        GameModeConfig.AITurnDelay = AiSpeedValues[aiSpeedIndex];
    }

    private void UpdateUI()
    {
        if (aiSpeedLabel != null)
            aiSpeedLabel.text = AiSpeedNames[aiSpeedIndex];
    }

    /// <summary>Read persisted AI speed for use in GameManager.</summary>
    public static float GetAITurnDelay()
    {
        int idx = Mathf.Clamp(PlayerPrefs.GetInt(PrefAISpeed, 1), 0, AiSpeedValues.Length - 1);
        return AiSpeedValues[idx];
    }

    /// <summary>Read persisted show-coordinates flag.</summary>
    public static bool GetShowCoords()
    {
        return PlayerPrefs.GetInt(PrefShowCoords, 0) == 1;
    }
}
