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
        // Hide Canvas children — OnGUI handles rendering.
        for (int i = 0; i < transform.childCount; i++)
            transform.GetChild(i).gameObject.SetActive(false);
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

    // ── OnGUI overlay for team selection (reliable when Canvas refs are broken) ──

    private GUIStyle cardStyle, cardSelectedStyle, cardTitleStyle, cardSubStyle, sectionStyle;
    private Texture2D cardBg, cardSelectedBg;

    private void OnGUI()
    {
        if (!gameObject.activeInHierarchy) return;
        InitCardStyles();

        float panelW = 620f;
        float panelX = (Screen.width - panelW) * 0.5f;
        float cardW = 280f, cardH = 180f;
        float cardY = Screen.height * 0.25f;
        float gap = 20f;

        // Robot card.
        float rX = panelX;
        bool isRobot = selectedTeam == Team.Robot;
        GUI.DrawTexture(new Rect(rX, cardY, cardW, cardH), isRobot ? cardSelectedBg : cardBg);
        if (isRobot)
        {
            GUI.color = new Color(0.3f, 0.5f, 1f);
            DrawOutline(new Rect(rX, cardY, cardW, cardH), 3);
            GUI.color = Color.white;
        }
        // Team color bar.
        GUI.color = new Color(0.3f, 0.5f, 1f);
        GUI.DrawTexture(new Rect(rX, cardY, cardW, 6), Texture2D.whiteTexture);
        GUI.color = Color.white;

        cardTitleStyle.normal.textColor = new Color(0.3f, 0.5f, 1f);
        GUI.Label(new Rect(rX, cardY + 30, cardW, 40), "ROBOTS", cardTitleStyle);
        GUI.Label(new Rect(rX, cardY + 75, cardW, 60), "Build walls, shield allies\nStrategic defense", cardSubStyle);
        if (GUI.Button(new Rect(rX, cardY, cardW, cardH), "", GUIStyle.none))
            SelectRobots();

        // Mutant card.
        float mX = panelX + cardW + gap;
        bool isMutant = selectedTeam == Team.Mutant;
        GUI.DrawTexture(new Rect(mX, cardY, cardW, cardH), isMutant ? cardSelectedBg : cardBg);
        if (isMutant)
        {
            GUI.color = new Color(0.3f, 0.9f, 0.3f);
            DrawOutline(new Rect(mX, cardY, cardW, cardH), 3);
            GUI.color = Color.white;
        }
        GUI.color = new Color(0.3f, 0.9f, 0.3f);
        GUI.DrawTexture(new Rect(mX, cardY, cardW, 6), Texture2D.whiteTexture);
        GUI.color = Color.white;

        cardTitleStyle.normal.textColor = new Color(0.3f, 0.9f, 0.3f);
        GUI.Label(new Rect(mX, cardY + 30, cardW, 40), "MUTANTS", cardTitleStyle);
        GUI.Label(new Rect(mX, cardY + 75, cardW, 60), "Spread slime, swarm enemies\nAggressive expansion", cardSubStyle);
        if (GUI.Button(new Rect(mX, cardY, cardW, cardH), "", GUIStyle.none))
            SelectMutants();

        // Board size + difficulty + start below cards.
        float optY = cardY + cardH + 30;
        GUI.Label(new Rect(panelX, optY, panelW, 24), $"Board: {BoardSizeNames[boardSizeIndex]}     Difficulty: {DifficultyNames[difficultyIndex]}", sectionStyle);

        float btnY = optY + 30;
        float btnW = 100f;
        var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };

        if (GUI.Button(new Rect(panelX, btnY, btnW, 30), "< Board", btnStyle))
            BoardSizePrev();
        if (GUI.Button(new Rect(panelX + btnW + 5, btnY, btnW, 30), "Board >", btnStyle))
            BoardSizeNext();
        if (GUI.Button(new Rect(panelX + (btnW + 5) * 2 + 20, btnY, btnW, 30), "< Diff", btnStyle))
            DifficultyPrev();
        if (GUI.Button(new Rect(panelX + (btnW + 5) * 3 + 20, btnY, btnW, 30), "Diff >", btnStyle))
            DifficultyNext();

        float startY = btnY + 45;
        GUI.backgroundColor = new Color(0.2f, 0.7f, 0.2f);
        var startStyle = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold, fixedHeight = 45 };
        if (GUI.Button(new Rect(panelX + 100, startY, panelW - 200, 45), "START GAME", startStyle))
            OnStartMatch();
        GUI.backgroundColor = Color.white;

        if (GUI.Button(new Rect(panelX + panelW * 0.5f - 60, startY + 55, 120, 30), "< BACK", btnStyle))
            OnBack();
    }

    private void InitCardStyles()
    {
        if (cardStyle != null) return;

        cardBg = MakeTex(new Color(0.12f, 0.12f, 0.18f, 0.9f));
        cardSelectedBg = MakeTex(new Color(0.15f, 0.18f, 0.28f, 0.95f));

        cardStyle = new GUIStyle();
        cardSelectedStyle = new GUIStyle();

        cardTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        cardSubStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14, alignment = TextAnchor.UpperCenter,
            normal = { textColor = new Color(0.75f, 0.75f, 0.75f) },
            wordWrap = true
        };
        sectionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
    }

    private static void DrawOutline(Rect rect, float thickness)
    {
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
    }

    private static Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}
