using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor utility to create/reset the MainMenu scene with full Canvas UI.
/// Tools > Setup Main Menu Scene
/// </summary>
public static class MainMenuSetup
{
    private static readonly Color DarkBg = new Color(0.08f, 0.08f, 0.12f);
    private static readonly Color PanelBg = new Color(0.12f, 0.12f, 0.18f, 0.95f);
    private static readonly Color ButtonNormal = new Color(0.2f, 0.25f, 0.35f);
    private static readonly Color ButtonHover = new Color(0.25f, 0.35f, 0.5f);
    private static readonly Color RobotBlue = new Color(0.3f, 0.5f, 1f);
    private static readonly Color MutantGreen = new Color(0.3f, 1f, 0.3f);
    private static readonly Color AccentGold = new Color(1f, 0.85f, 0.3f);

    [MenuItem("Tools/Setup Main Menu Scene")]
    public static void SetupMainMenuScene()
    {
        // Create or open MainMenu scene.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera with dark background.
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = DarkBg;
        cam.orthographic = true;
        camGo.tag = "MainCamera";

        // EventSystem.
        var eventGo = new GameObject("EventSystem");
        eventGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // Canvas.
        var canvasGo = new GameObject("MenuCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var menuCtrl = canvasGo.AddComponent<MainMenuController>();
        canvasGo.AddComponent<MainMenuButtonWiring>();

        // ── Main Panel ──────────────────────────────────────────────────
        var mainPanel = CreatePanel(canvasGo.transform, "MainPanel");
        SetField(menuCtrl, "mainPanel", mainPanel);

        // Title.
        CreateText(mainPanel.transform, "Title", "ROBOTS & MUTANTS",
            new Vector2(0, 200), 64, AccentGold, FontStyle.Bold);
        CreateText(mainPanel.transform, "Subtitle", "Hex Territory Control",
            new Vector2(0, 140), 28, Color.gray, FontStyle.Italic);

        // Menu buttons.
        CreateMenuButton(mainPanel.transform, "PlayBtn", "PLAY", new Vector2(0, 40),
            320, 60, RobotBlue, () => { });
        CreateMenuButton(mainPanel.transform, "ReplaysBtn", "REPLAYS", new Vector2(0, -40),
            320, 60, ButtonNormal, () => { });
        CreateMenuButton(mainPanel.transform, "SettingsBtn", "SETTINGS", new Vector2(0, -120),
            320, 60, ButtonNormal, () => { });
        CreateMenuButton(mainPanel.transform, "QuitBtn", "QUIT", new Vector2(0, -200),
            320, 60, new Color(0.5f, 0.15f, 0.15f), () => { });

        // Version.
        CreateText(mainPanel.transform, "Version", "v0.1 alpha",
            new Vector2(0, -350), 18, new Color(0.4f, 0.4f, 0.4f));

        // ── Play Panel ──────────────────────────────────────────────────
        var playPanel = CreatePanel(canvasGo.transform, "PlayPanel");
        SetField(menuCtrl, "playPanel", playPanel);
        var playSetup = playPanel.AddComponent<PlaySetupPanel>();
        SetField(playSetup, "menuController", menuCtrl);

        CreateText(playPanel.transform, "PlayTitle", "CHOOSE YOUR SIDE",
            new Vector2(0, 280), 42, AccentGold, FontStyle.Bold);

        // Team cards.
        var robotCard = CreateTeamCard(playPanel.transform, "RobotCard",
            "ROBOTS", "Walls + Shield", RobotBlue, new Vector2(-200, 120));
        var mutantCard = CreateTeamCard(playPanel.transform, "MutantCard",
            "MUTANTS", "Slime + Swarm", MutantGreen, new Vector2(200, 120));

        // Robot/Mutant highlights.
        var robotHL = CreateHighlight(robotCard.transform, "RobotHL", RobotBlue);
        var mutantHL = CreateHighlight(mutantCard.transform, "MutantHL", MutantGreen);
        SetField(playSetup, "robotHighlight", robotHL.GetComponent<Image>());
        SetField(playSetup, "mutantHighlight", mutantHL.GetComponent<Image>());

        // Team buttons.
        var robotBtn = robotCard.AddComponent<Button>();
        var mutantBtn = mutantCard.AddComponent<Button>();
        SetField(playSetup, "robotButton", robotBtn);
        SetField(playSetup, "mutantButton", mutantBtn);

        // Faction description.
        var factionDesc = CreateText(playPanel.transform, "FactionDesc", "",
            new Vector2(0, -20), 22, Color.white);
        SetField(playSetup, "factionDescription", factionDesc.GetComponent<Text>());

        // Board size selector.
        CreateText(playPanel.transform, "BoardLabel", "Board Size:",
            new Vector2(-120, -100), 24, Color.white);
        var boardVal = CreateText(playPanel.transform, "BoardValue", "Medium (4)",
            new Vector2(80, -100), 24, AccentGold);
        SetField(playSetup, "boardSizeLabel", boardVal.GetComponent<Text>());
        CreateSmallButton(playPanel.transform, "BoardPrev", "<", new Vector2(0, -100));
        CreateSmallButton(playPanel.transform, "BoardNext", ">", new Vector2(190, -100));

        // Difficulty selector.
        CreateText(playPanel.transform, "DiffLabel", "AI Difficulty:",
            new Vector2(-120, -160), 24, Color.white);
        var diffVal = CreateText(playPanel.transform, "DiffValue", "Normal",
            new Vector2(80, -160), 24, AccentGold);
        SetField(playSetup, "difficultyLabel", diffVal.GetComponent<Text>());
        CreateSmallButton(playPanel.transform, "DiffPrev", "<", new Vector2(0, -160));
        CreateSmallButton(playPanel.transform, "DiffNext", ">", new Vector2(190, -160));

        // Start + Back buttons.
        CreateMenuButton(playPanel.transform, "StartBtn", "START MATCH",
            new Vector2(0, -260), 360, 70, RobotBlue, () => { });
        SetField(playSetup, "startButton", playPanel.transform.Find("StartBtn").GetComponent<Button>());

        CreateText(playPanel.transform, "BackPlay", "< Back",
            new Vector2(-400, -350), 24, Color.gray);

        playPanel.SetActive(false);

        // ── Replays Panel ───────────────────────────────────────────────
        var replaysPanel = CreatePanel(canvasGo.transform, "ReplaysPanel");
        SetField(menuCtrl, "replaysPanel", replaysPanel);
        var replaysComp = replaysPanel.AddComponent<ReplaysPanel>();
        SetField(replaysComp, "menuController", menuCtrl);

        CreateText(replaysPanel.transform, "ReplaysTitle", "REPLAYS",
            new Vector2(0, 280), 42, AccentGold, FontStyle.Bold);

        // List header.
        CreateText(replaysPanel.transform, "ListHeader", "#     Date           Result          Time",
            new Vector2(0, 220), 20, Color.gray);

        // Scrollable list area.
        var scrollGo = new GameObject("ReplayScroll");
        scrollGo.transform.SetParent(replaysPanel.transform, false);
        var scrollRT = scrollGo.AddComponent<RectTransform>();
        scrollRT.anchoredPosition = new Vector2(0, 20);
        scrollRT.sizeDelta = new Vector2(700, 350);
        var scrollImg = scrollGo.AddComponent<Image>();
        scrollImg.color = new Color(0.08f, 0.08f, 0.1f, 0.8f);

        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(scrollGo.transform, false);
        var contentRT = contentGo.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0, 0);
        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        SetField(replaysComp, "listContent", contentGo.transform);

        // Replay row prefab.
        var rowPrefab = CreateReplayRowPrefab(replaysPanel.transform);
        SetField(replaysComp, "replayRowPrefab", rowPrefab);
        rowPrefab.SetActive(false);

        // No replays text.
        var noReplays = CreateText(replaysPanel.transform, "NoReplays", "No replays found.",
            new Vector2(0, 20), 24, Color.gray);
        SetField(replaysComp, "noReplaysText", noReplays.GetComponent<Text>());

        // Watch + Delete + Back buttons.
        var watchBtn = CreateMenuButton(replaysPanel.transform, "WatchBtn", "Watch",
            new Vector2(-100, -220), 200, 50, RobotBlue, () => { });
        var deleteBtn = CreateMenuButton(replaysPanel.transform, "DeleteBtn", "Delete",
            new Vector2(100, -220), 200, 50, new Color(0.5f, 0.15f, 0.15f), () => { });
        SetField(replaysComp, "watchButton", watchBtn.GetComponent<Button>());
        SetField(replaysComp, "deleteButton", deleteBtn.GetComponent<Button>());

        CreateText(replaysPanel.transform, "BackReplays", "< Back",
            new Vector2(-400, -350), 24, Color.gray);

        replaysPanel.SetActive(false);

        // ── Settings Panel ──────────────────────────────────────────────
        var settingsPanel = CreatePanel(canvasGo.transform, "SettingsPanel");
        SetField(menuCtrl, "settingsPanel", settingsPanel);
        var settingsComp = settingsPanel.AddComponent<SettingsPanel>();
        SetField(settingsComp, "menuController", menuCtrl);

        CreateText(settingsPanel.transform, "SettingsTitle", "SETTINGS",
            new Vector2(0, 280), 42, AccentGold, FontStyle.Bold);

        // Gameplay section.
        CreateText(settingsPanel.transform, "GameplayHeader", "Gameplay",
            new Vector2(0, 180), 28, Color.white, FontStyle.Bold);

        CreateText(settingsPanel.transform, "AISpeedLabel", "AI Turn Speed:",
            new Vector2(-100, 120), 22, Color.white);
        var aiSpeedVal = CreateText(settingsPanel.transform, "AISpeedValue", "Normal",
            new Vector2(100, 120), 22, AccentGold);
        SetField(settingsComp, "aiSpeedLabel", aiSpeedVal.GetComponent<Text>());
        CreateSmallButton(settingsPanel.transform, "AISpeedPrev", "<", new Vector2(20, 120));
        CreateSmallButton(settingsPanel.transform, "AISpeedNext", ">", new Vector2(210, 120));

        var coordsToggle = CreateToggle(settingsPanel.transform, "ShowCoords", "Show Coordinates",
            new Vector2(0, 60));
        SetField(settingsComp, "showCoordsToggle", coordsToggle);

        // Graphics section.
        CreateText(settingsPanel.transform, "GraphicsHeader", "Graphics",
            new Vector2(0, -20), 28, Color.white, FontStyle.Bold);

        var fsToggle = CreateToggle(settingsPanel.transform, "Fullscreen", "Fullscreen",
            new Vector2(0, -80));
        SetField(settingsComp, "fullscreenToggle", fsToggle);

        var vsyncToggle = CreateToggle(settingsPanel.transform, "VSync", "VSync",
            new Vector2(0, -130));
        SetField(settingsComp, "vsyncToggle", vsyncToggle);

        // Apply + Back.
        CreateMenuButton(settingsPanel.transform, "ApplyBtn", "Apply",
            new Vector2(0, -230), 240, 50, RobotBlue, () => { });
        CreateText(settingsPanel.transform, "BackSettings", "< Back",
            new Vector2(-400, -350), 24, Color.gray);

        settingsPanel.SetActive(false);

        // ── Wire up button events ───────────────────────────────────────
        WireButton(mainPanel, "PlayBtn", playSetup, null, menuCtrl, "ShowPlay");
        WireButton(mainPanel, "ReplaysBtn", null, null, menuCtrl, "ShowReplays");
        WireButton(mainPanel, "SettingsBtn", null, null, menuCtrl, "ShowSettings");
        WireButton(mainPanel, "QuitBtn", null, null, menuCtrl, "QuitGame");

        WireButton(robotCard, robotBtn, playSetup, "SelectRobots");
        WireButton(mutantCard, mutantBtn, playSetup, "SelectMutants");

        WireButtonDirect(playPanel, "StartBtn", playSetup, "OnStartMatch");
        WireButtonDirect(playPanel, "BoardPrev", playSetup, "BoardSizePrev");
        WireButtonDirect(playPanel, "BoardNext", playSetup, "BoardSizeNext");
        WireButtonDirect(playPanel, "DiffPrev", playSetup, "DifficultyPrev");
        WireButtonDirect(playPanel, "DiffNext", playSetup, "DifficultyNext");

        WireButtonDirect(replaysPanel, "WatchBtn", replaysComp, "OnWatch");
        WireButtonDirect(replaysPanel, "DeleteBtn", replaysComp, "OnDelete");

        WireButtonDirect(settingsPanel, "ApplyBtn", settingsComp, "OnApply");

        // Back buttons (text-based, add Button component).
        AddBackButton(playPanel, "BackPlay", playSetup, "OnBack");
        AddBackButton(replaysPanel, "BackReplays", replaysComp, "OnBack");
        AddBackButton(settingsPanel, "BackSettings", settingsComp, "OnBack");

        // ── Save scene ──────────────────────────────────────────────────
        string scenePath = "Assets/Scenes/MainMenu.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.Refresh();

        // Add to build settings if not already there.
        AddSceneToBuildSettings(scenePath);
        AddSceneToBuildSettings("Assets/Scenes/SampleScene.unity");

        Debug.Log("[MainMenuSetup] MainMenu scene created and saved.");
    }

    // ── UI factory helpers ──────────────────────────────────────────────

    private static GameObject CreatePanel(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    private static GameObject CreateText(Transform parent, string name, string text,
        Vector2 pos, int fontSize, Color color, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(800, fontSize + 20);
        var t = go.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return go;
    }

    private static GameObject CreateMenuButton(Transform parent, string name, string label,
        Vector2 pos, float width, float height, Color bgColor, System.Action onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, height);

        var img = go.AddComponent<Image>();
        img.color = bgColor;

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = bgColor * 1.3f;
        colors.pressedColor = bgColor * 0.8f;
        btn.colors = colors;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRT = textGo.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        var t = textGo.AddComponent<Text>();
        t.text = label;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = (int)(height * 0.45f);
        t.color = Color.white;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;

        return go;
    }

    private static void CreateSmallButton(Transform parent, string name, string label, Vector2 pos)
    {
        CreateMenuButton(parent, name, label, pos, 40, 36, ButtonNormal, () => { });
    }

    private static GameObject CreateTeamCard(Transform parent, string name,
        string title, string subtitle, Color color, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(280, 200);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

        // Color bar at top.
        var bar = new GameObject("ColorBar");
        bar.transform.SetParent(go.transform, false);
        var barRT = bar.AddComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 1);
        barRT.anchorMax = new Vector2(1, 1);
        barRT.pivot = new Vector2(0.5f, 1);
        barRT.sizeDelta = new Vector2(0, 8);
        barRT.anchoredPosition = Vector2.zero;
        var barImg = bar.AddComponent<Image>();
        barImg.color = color;

        CreateText(go.transform, "CardTitle", title,
            new Vector2(0, 40), 36, color, FontStyle.Bold);
        CreateText(go.transform, "CardSub", subtitle,
            new Vector2(0, -20), 22, Color.white);

        return go;
    }

    private static GameObject CreateHighlight(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-4, -4);
        rt.offsetMax = new Vector2(4, 4);
        var img = go.AddComponent<Image>();
        img.color = Color.clear;
        img.raycastTarget = false;
        return go;
    }

    private static Toggle CreateToggle(Transform parent, string name, string label, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(400, 40);

        var toggle = go.AddComponent<Toggle>();

        // Background.
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(go.transform, false);
        var bgRT = bgGo.AddComponent<RectTransform>();
        bgRT.anchoredPosition = new Vector2(-170, 0);
        bgRT.sizeDelta = new Vector2(30, 30);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.25f, 0.25f, 0.3f);

        // Checkmark.
        var checkGo = new GameObject("Checkmark");
        checkGo.transform.SetParent(bgGo.transform, false);
        var checkRT = checkGo.AddComponent<RectTransform>();
        checkRT.anchorMin = Vector2.zero;
        checkRT.anchorMax = Vector2.one;
        checkRT.offsetMin = new Vector2(4, 4);
        checkRT.offsetMax = new Vector2(-4, -4);
        var checkImg = checkGo.AddComponent<Image>();
        checkImg.color = RobotBlue;

        toggle.targetGraphic = bgImg;
        toggle.graphic = checkImg;

        // Label.
        CreateText(go.transform, "Label", label, new Vector2(30, 0), 22, Color.white);

        return toggle;
    }

    private static GameObject CreateReplayRowPrefab(Transform parent)
    {
        var go = new GameObject("ReplayRowPrefab");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 36);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f, 0.5f);
        go.AddComponent<Button>();

        var le = go.AddComponent<HorizontalLayoutGroup>();
        le.spacing = 20;
        le.padding = new RectOffset(20, 20, 4, 4);
        le.childAlignment = TextAnchor.MiddleLeft;
        le.childForceExpandWidth = true;
        le.childForceExpandHeight = true;

        // 4 text columns.
        string[] cols = { "#", "Date", "Result", "Time" };
        float[] widths = { 50, 140, 160, 80 };
        for (int i = 0; i < cols.Length; i++)
        {
            var col = new GameObject($"Col{i}");
            col.transform.SetParent(go.transform, false);
            var colRT = col.AddComponent<RectTransform>();
            colRT.sizeDelta = new Vector2(widths[i], 0);
            var t = col.AddComponent<Text>();
            t.text = cols[i];
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 18;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            var lel = col.AddComponent<LayoutElement>();
            lel.preferredWidth = widths[i];
        }

        return go;
    }

    // ── Button wiring helpers ───────────────────────────────────────────

    private static void WireButton(GameObject panel, string btnName,
        object target1, object target2, object menuCtrl, string methodName)
    {
        var btnTransform = panel.transform.Find(btnName);
        if (btnTransform == null) return;
        var btn = btnTransform.GetComponent<Button>();
        if (btn == null) return;

        var unityEvent = new Button.ButtonClickedEvent();
        var action = new UnityEngine.Events.UnityAction(
            () => ((MainMenuController)menuCtrl).SendMessage(methodName));
        btn.onClick = unityEvent;
        // We can't easily wire persistent listeners from code, rely on runtime wiring instead.
    }

    private static void WireButton(GameObject cardGo, Button btn, object target, string method)
    {
        // Runtime wiring handled by component Start methods.
    }

    private static void WireButtonDirect(GameObject panel, string btnName, object target, string method)
    {
        // Runtime wiring handled by component Start methods.
    }

    private static void AddBackButton(GameObject panel, string textName, object target, string method)
    {
        var textTransform = panel.transform.Find(textName);
        if (textTransform == null) return;
        var btn = textTransform.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.clear;
        colors.highlightedColor = new Color(1, 1, 1, 0.1f);
        btn.colors = colors;
    }

    // ── Reflection field setter ─────────────────────────────────────────

    private static void SetField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        if (field != null)
            field.SetValue(obj, value);
        else
            Debug.LogWarning($"[MainMenuSetup] Field '{fieldName}' not found on {obj.GetType().Name}");
    }

    // ── Build settings ──────────────────────────────────────────────────

    private static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);

        foreach (var s in scenes)
            if (s.path == scenePath) return; // already present

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[MainMenuSetup] Added '{scenePath}' to Build Settings.");
    }
}
