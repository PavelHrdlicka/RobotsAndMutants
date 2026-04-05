using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Play setup panel: team selection, board size, AI difficulty, start button.
/// </summary>
public class PlaySetupPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MainMenuController menuController;

    [Header("Team Selection")]
    [SerializeField] private Button robotButton;
    [SerializeField] private Button mutantButton;
    [SerializeField] private Image robotHighlight;
    [SerializeField] private Image mutantHighlight;

    [Header("Board Size")]
    [SerializeField] private Text boardSizeLabel;

    [Header("AI Difficulty")]
    [SerializeField] private Text difficultyLabel;

    [Header("Start")]
    [SerializeField] private Button startButton;

    [Header("Faction Info")]
    [SerializeField] private Text factionDescription;

    private Team selectedTeam = Team.Robot;
    private int boardSizeIndex = 1; // 0=Small(3), 1=Medium(4), 2=Large(5)
    private int difficultyIndex = 1; // 0=Easy, 1=Normal, 2=Hard

    private static readonly int[] BoardSizes = { 3, 4, 5 };
    private static readonly string[] BoardSizeNames = { "Small (3)", "Medium (4)", "Large (5)" };
    private static readonly string[] DifficultyNames = { "Easy", "Normal", "Hard" };

    private static readonly string RobotDesc =
        "Build walls to block enemies and control territory.\n" +
        "Shield Wall: allies reduce incoming damage.\n" +
        "Strong defense, strategic positioning.";

    private static readonly string MutantDesc =
        "Spread slime to regenerate and dominate the map.\n" +
        "Swarm: allies boost attack damage.\n" +
        "Aggressive expansion, strength in numbers.";

    private void OnEnable()
    {
        UpdateUI();
    }

    // ── Team selection ──────────────────────────────────────────────────

    public void SelectRobots()
    {
        selectedTeam = Team.Robot;
        UpdateUI();
    }

    public void SelectMutants()
    {
        selectedTeam = Team.Mutant;
        UpdateUI();
    }

    // ── Board size ──────────────────────────────────────────────────────

    public void BoardSizePrev()
    {
        boardSizeIndex = Mathf.Max(0, boardSizeIndex - 1);
        UpdateUI();
    }

    public void BoardSizeNext()
    {
        boardSizeIndex = Mathf.Min(BoardSizes.Length - 1, boardSizeIndex + 1);
        UpdateUI();
    }

    // ── Difficulty ──────────────────────────────────────────────────────

    public void DifficultyPrev()
    {
        difficultyIndex = Mathf.Max(0, difficultyIndex - 1);
        UpdateUI();
    }

    public void DifficultyNext()
    {
        difficultyIndex = Mathf.Min(DifficultyNames.Length - 1, difficultyIndex + 1);
        UpdateUI();
    }

    // ── Start ───────────────────────────────────────────────────────────

    public void OnStartMatch()
    {
        if (menuController != null)
            menuController.StartMatch(selectedTeam, BoardSizes[boardSizeIndex], difficultyIndex);
    }

    public void OnBack()
    {
        if (menuController != null)
            menuController.ShowMain();
    }

    // ── UI update ───────────────────────────────────────────────────────

    private void UpdateUI()
    {
        bool isRobot = selectedTeam == Team.Robot;

        if (robotHighlight != null)
            robotHighlight.color = isRobot ? new Color(0.3f, 0.5f, 1f, 0.4f) : Color.clear;
        if (mutantHighlight != null)
            mutantHighlight.color = !isRobot ? new Color(0.3f, 1f, 0.3f, 0.4f) : Color.clear;

        if (boardSizeLabel != null)
            boardSizeLabel.text = BoardSizeNames[boardSizeIndex];
        if (difficultyLabel != null)
            difficultyLabel.text = DifficultyNames[difficultyIndex];
        if (factionDescription != null)
            factionDescription.text = isRobot ? RobotDesc : MutantDesc;
    }
}
