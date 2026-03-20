using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.MLAgents.Policies;
using Debug = UnityEngine.Debug;

/// <summary>
/// Dockable editor window with quick-access buttons for all project tools.
/// Layout follows the ML training workflow: Configure → Train → Observe → Analyze → Test.
/// Open via: Tools > Project Tools Window
/// </summary>
public class ProjectToolsWindow : EditorWindow
{
    private Vector2 scrollPos;

    // Replay analysis state.
    private int analyzeCount = 10;
    private string lastAnalysisResult;

    // Foldout states (persisted via EditorPrefs).
    private const string k_AdvancedFoldoutKey = "ProjectTools_AdvancedFoldout";
    private const string k_SceneSetupFoldoutKey = "ProjectTools_SceneSetupFoldout";

    // Cached per Layout event — must NOT change between Layout and Repaint events
    // or GUILayout will throw "control N's position in group with only N controls".
    private bool cachedTrainingRunning;
    private bool cachedTbRunning;
    private bool cachedHasCheckpoint;
    private bool cachedHasModel;
    private string cachedPrevRunId;

    // Python processes.
    private static Process trainingProcess;
    private static Process tensorboardProcess;
    private static string runId = "run1";

    // SessionState keys — survive domain reload within the same Unity session.
    private const string k_TrainingPidKey       = "ProjectTools_TrainingPID";
    private const string k_PythonReadyKey       = "ProjectTools_PythonReady";
    private const string k_AutoPlayPending      = "ProjectTools_AutoPlayPending";
    // Set just before EditorApplication.isPlaying=true. Prevents a second isPlaying=true
    // call from TryResumeAutoPlay() during the domain reload that Play mode entry triggers.
    // Cleared only when playModeStateChanged fires (EnteredPlayMode or EnteredEditMode).
    private const string k_IsEnteringPlayMode   = "ProjectTools_IsEnteringPlayMode";
    // Set before Reset/SetupScene so a domain reload mid-DoStartTrainingAndPlay
    // can be detected in OnEnable and the training start can be resumed.
    private const string k_StartTrainingPending = "ProjectTools_StartTrainingPending";
    private const string k_StartTrainingRunId   = "ProjectTools_StartTrainingRunId";

    // Set to true when Python outputs "Listening on port 5004" — triggers auto Play.
    private static volatile bool pythonReady;

    private static int RunCounter
    {
        get => EditorPrefs.GetInt("MLTrain_RunCounter", DetectNextRunId());
        set => EditorPrefs.SetInt("MLTrain_RunCounter", value);
    }

    private static int DetectNextRunId()
    {
        string resultsDir = System.IO.Path.GetFullPath("results");
        int max = 0;
        if (System.IO.Directory.Exists(resultsDir))
        {
            foreach (var dir in System.IO.Directory.GetDirectories(resultsDir))
            {
                string name = System.IO.Path.GetFileName(dir);
                if (name.StartsWith("run") && int.TryParse(name.Substring(3), out int num))
                    if (num > max) max = num;
            }
        }
        return max + 1;
    }

    // Loaded model info (persisted in EditorPrefs).
    private const string ModelRunIdKey    = "MLTrain_LoadedModelRunId";
    private const string ModelStepsKey    = "MLTrain_LoadedModelSteps";

    // Training session counters — snapshot at start, delta saved to PlayerPrefs at stop.
    private static int trainStartGames;
    private static int trainStartRounds;

    private const string PythonExe = @"C:\Users\mail\miniconda3\envs\mlagents2\python.exe";
    private static string ConfigPath => System.IO.Path.GetFullPath("Assets/ML-Agents/config/hex_territory.yaml");

    [MenuItem("Tools/Project Tools Window")]
    public static void ShowWindow()
    {
        // Dock to the right side next to Inspector.
        var inspectorType = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        var window = GetWindow<ProjectToolsWindow>("Project Tools", false, inspectorType);
        window.minSize = new Vector2(250, 500);
    }

    /// <summary>Auto-open on editor launch + keep running in background.</summary>
    [InitializeOnLoadMethod]
    private static void AutoOpen()
    {
        // Register playModeStateChanged here (InitializeOnLoadMethod) so it survives
        // every domain reload — including the one triggered by entering Play mode.
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        EditorApplication.delayCall += () =>
        {
            if (!HasOpenInstances<ProjectToolsWindow>())
                ShowWindow();

            // Run simulation even when Unity editor is not focused.
            Application.runInBackground = true;
        };
    }

    /// <summary>
    /// Clears Play mode entry flags once Unity has actually entered or exited Play mode.
    /// This is the authoritative signal used by TryResumeAutoPlay to avoid double-entry.
    /// </summary>
    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.EraseBool(k_AutoPlayPending);
            SessionState.EraseBool(k_IsEnteringPlayMode);
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            // Play mode exit (normal exit or failed entry).
            SessionState.EraseBool(k_IsEnteringPlayMode);
        }
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
        runId = $"run{RunCounter}";
        TryRestoreTrainingProcess();
        TryResumeTrainingStart();
        TryResumeAutoPlay();
    }

    /// <summary>
    /// If a domain reload happened inside DoStartTrainingAndPlay() — between
    /// the Reset/Setup and the StartTraining() call — resume starting Python now.
    /// This can happen when SaveAssets() flushes a pending compilation.
    /// </summary>
    private static void TryResumeTrainingStart()
    {
        if (!SessionState.GetBool(k_StartTrainingPending, false)) return;

        string savedRunId = SessionState.GetString(k_StartTrainingRunId, "");
        if (string.IsNullOrEmpty(savedRunId))
        {
            SessionState.EraseBool(k_StartTrainingPending);
            return;
        }

        runId = savedRunId;
        SessionState.EraseBool(k_StartTrainingPending);

        // If process was already restored from PID (domain reload after StartTraining
        // succeeded but before the flag was cleared) just register the wait loop.
        bool alreadyRunning = trainingProcess != null && !trainingProcess.HasExited;
        if (!alreadyRunning)
        {
            Debug.Log($"[ML-Train] Resuming training start after domain reload (run-id: {runId}).");
            StartTraining();
        }

        RegisterWaitForPython();
    }

    /// <summary>
    /// If a domain reload happened while waiting for auto-Play (between
    /// "Python ready" and actual Play mode entry), re-enter Play mode now.
    /// </summary>
    private static void TryResumeAutoPlay()
    {
        if (!SessionState.GetBool(k_AutoPlayPending, false)) return;

        if (SessionState.GetBool(k_IsEnteringPlayMode, false))
        {
            Debug.Log("[ML-Train] Domain reload is part of Play mode entry — waiting for play mode.");
            return;
        }

        bool trainingAlive = trainingProcess != null && !trainingProcess.HasExited;
        if (trainingAlive)
        {
            Debug.Log("[ML-Train] Resuming auto-Play after script-compile domain reload.");
            SessionState.SetBool(k_IsEnteringPlayMode, true);
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }
        else
        {
            SessionState.EraseBool(k_AutoPlayPending);
        }
    }

    /// <summary>
    /// After a domain reload the static trainingProcess reference is lost.
    /// Restore it from the saved PID so the status indicator keeps working.
    /// </summary>
    private static void TryRestoreTrainingProcess()
    {
        if (trainingProcess != null && !trainingProcess.HasExited) return;

        int savedPid = SessionState.GetInt(k_TrainingPidKey, -1);
        if (savedPid < 0) return;

        try
        {
            var proc = Process.GetProcessById(savedPid);
            if (!proc.HasExited)
            {
                trainingProcess = proc;
                trainingExitLogged = false;
                Debug.Log($"[ML-Train] Reattached to training process PID {savedPid} after domain reload.");
            }
            else
            {
                SessionState.EraseInt(k_TrainingPidKey);
            }
        }
        catch
        {
            // Process no longer exists.
            SessionState.EraseInt(k_TrainingPidKey);
        }
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    // Track whether we already logged the training exit.
    private static bool trainingExitLogged;

    private void OnEditorUpdate()
    {
        // Once Play mode is confirmed running, clear all pending flags.
        if (EditorApplication.isPlaying)
        {
            if (SessionState.GetBool(k_AutoPlayPending, false))
                SessionState.EraseBool(k_AutoPlayPending);
            if (SessionState.GetBool(k_IsEnteringPlayMode, false))
                SessionState.EraseBool(k_IsEnteringPlayMode);
        }

        // Monitor training process — log when it exits and auto-load models.
        if (trainingProcess != null && trainingProcess.HasExited && !trainingExitLogged)
        {
            trainingExitLogged = true;
            int exitCode = trainingProcess.ExitCode;
            if (exitCode == 0)
            {
                Debug.Log($"[ML-Train] Training finished successfully (exit code 0). Loading models from results/{runId}/...");
                LoadBestModels(runId);
            }
            else
            {
                Debug.LogWarning($"[ML-Train] Training process exited with code {exitCode}. Check Console for errors.");
            }
            Repaint();
        }
    }

    // ─── OnGUI ────────────────────────────────────────────

    private void OnGUI()
    {
        // Snapshot all process-state booleans once per Layout event.
        // This guarantees the same control count for the subsequent Repaint event.
        if (Event.current.type == EventType.Layout)
        {
            cachedTrainingRunning = trainingProcess != null && !trainingProcess.HasExited;
            cachedTbRunning       = tensorboardProcess != null && !tensorboardProcess.HasExited;
            cachedPrevRunId       = EditorPrefs.GetString(ModelRunIdKey, "");
            cachedHasModel        = !string.IsNullOrEmpty(cachedPrevRunId);
            cachedHasCheckpoint   = Directory.Exists(Path.GetFullPath($"results/{runId}"));
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawConfigSection();
        DrawTrainingSection();
        DrawObserveSection();
        DrawReplaySection();
        DrawAnalysisSection();
        DrawTestingSection();
        DrawSceneSetupSection();

        EditorGUILayout.EndScrollView();
    }

    // ─── 1. Configuration ─────────────────────────────────

    private void DrawConfigSection()
    {
        DrawSection("Configuration", () =>
        {
            var config = GameConfigEditor.GetOrCreateConfig();
            if (config == null)
            {
                EditorGUILayout.HelpBox("GameConfig asset not found!", MessageType.Error);
                return;
            }

            EditorGUI.BeginChangeCheck();

            config.boardSide = EditorGUILayout.IntSlider("Board Side", config.boardSide, 3, 15);
            int tileCount = 3 * config.boardSide * config.boardSide - 3 * config.boardSide + 1;
            EditorGUILayout.LabelField($"  Total tiles: {tileCount}", EditorStyles.miniLabel);

            config.unitsPerTeam = EditorGUILayout.IntSlider("Units per Team", config.unitsPerTeam, 1, 10);
            EditorGUILayout.LabelField($"  Base size: {Mathf.Min(config.unitsPerTeam, 4)} tiles/team", EditorStyles.miniLabel);

            config.msPerTick = EditorGUILayout.IntSlider("ms / tick", config.msPerTick, 1, 5000);
            float ticksPerSec = 1000f / config.msPerTick;
            EditorGUILayout.LabelField($"  {ticksPerSec:F1} ticks/s  (timeScale = {config.TimeScale:F2})", EditorStyles.miniLabel);

            config.winPercent = EditorGUILayout.IntSlider("Win %", config.winPercent, 10, 100);
            config.maxSteps = EditorGUILayout.IntSlider("Max Steps", config.maxSteps, 100, 10000);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Combat", EditorStyles.miniBoldLabel);
            config.unitMaxHealth = EditorGUILayout.IntSlider("Unit Max Health", config.unitMaxHealth, 3, 20);
            config.robotFlankingChancePerAlly = EditorGUILayout.Slider("Robot Flank %/ally", config.robotFlankingChancePerAlly, 0f, 0.5f);
            EditorGUILayout.LabelField($"  {config.robotFlankingChancePerAlly * 100:F0}% per adjacent ally → double damage (max 3)", EditorStyles.miniLabel);
            config.mutantDodgeChancePerAlly = EditorGUILayout.Slider("Mutant Dodge %/ally", config.mutantDodgeChancePerAlly, 0f, 0.5f);
            EditorGUILayout.LabelField($"  {config.mutantDodgeChancePerAlly * 100:F0}% per adjacent ally → dodge attack (max 3)", EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            config.replayLogEveryNthGame = EditorGUILayout.IntSlider("Replay: log every Nth game", config.replayLogEveryNthGame, 1, 1000);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(config);

                // Apply live during play.
                if (EditorApplication.isPlaying)
                    Time.timeScale = config.TimeScale;
            }
        });
    }

    // ─── 2. Training ──────────────────────────────────────

    private void DrawTrainingSection()
    {
        DrawSection("Training", () =>
        {
            // --- Hero button: one-click training ---
            GUI.enabled = !cachedTrainingRunning;
            GUI.backgroundColor = !cachedTrainingRunning ? new Color(1f, 0.84f, 0f) : Color.gray;
            if (GUILayout.Button("Start Training", GUILayout.Height(45)))
                StartTrainingAndPlay();
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            // --- Run ID + Status on one line ---
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Run ID:", GUILayout.Width(45));
            runId = EditorGUILayout.TextField(runId, GUILayout.Width(70));
            GUILayout.FlexibleSpace();

            // Status indicator.
            string statusText;
            Color statusColor;
            bool processExited = trainingProcess != null && trainingProcess.HasExited;

            if (cachedTrainingRunning)
            {
                bool blink = ((int)(EditorApplication.timeSinceStartup * 2)) % 2 == 0;
                string pid = "";
                try { pid = $" PID {trainingProcess.Id}"; } catch { }
                statusText = $"Training{pid} {(blink ? "●" : "○")}";
                statusColor = new Color(0.3f, 1f, 0.3f);
                Repaint();
            }
            else if (processExited)
            {
                int exitCode = trainingProcess.ExitCode;
                statusText = exitCode == 0 ? "Finished OK" : $"Error (code {exitCode})";
                statusColor = exitCode == 0 ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.4f, 0.3f);
            }
            else
            {
                statusText = "Idle";
                statusColor = Color.gray;
            }

            var statusStyle = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = statusColor }, fontStyle = FontStyle.Bold };
            EditorGUILayout.LabelField(statusText, statusStyle);
            EditorGUILayout.EndHorizontal();

            // --- Stop button (only when running) ---
            if (cachedTrainingRunning)
            {
                GUI.backgroundColor = new Color(1f, 0.4f, 0.3f);
                if (GUILayout.Button("Stop Training", GUILayout.Height(30)))
                    StopTraining();
                GUI.backgroundColor = Color.white;
            }

            // --- Advanced start options (foldout) ---
            EditorGUILayout.Space(4);
            bool advancedOpen = EditorPrefs.GetBool(k_AdvancedFoldoutKey, false);
            bool newAdvancedOpen = EditorGUILayout.Foldout(advancedOpen, "Advanced Start Options", true);
            if (newAdvancedOpen != advancedOpen)
                EditorPrefs.SetBool(k_AdvancedFoldoutKey, newAdvancedOpen);

            if (newAdvancedOpen)
            {
                GUI.enabled = !cachedTrainingRunning;

                // Start Training (new) — manual, does NOT auto-Play.
                GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
                if (GUILayout.Button("Start Training (manual, no auto-Play)", GUILayout.Height(28)))
                    StartTraining();
                GUI.backgroundColor = Color.white;

                // Resume.
                GUI.enabled = !cachedTrainingRunning && cachedHasCheckpoint;
                string resumeLabel = cachedHasCheckpoint ? $"Resume ({runId})" : $"Resume ({runId}) — no checkpoint";
                if (GUILayout.Button(resumeLabel, GUILayout.Height(25)))
                    StartTraining(TrainingMode.Resume);

                // Init from previous.
                GUI.enabled = !cachedTrainingRunning && cachedHasModel;
                string initLabel = cachedHasModel ? $"Init from {cachedPrevRunId}" : "Init from previous — no model yet";
                if (GUILayout.Button(initLabel, GUILayout.Height(25)))
                {
                    int next = RunCounter;
                    RunCounter = next + 1;
                    runId = $"run{next}";
                    StartTraining(TrainingMode.InitFrom, cachedPrevRunId);
                }

                GUI.enabled = true;
            }

            // --- TensorBoard ---
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            if (!cachedTbRunning)
            {
                if (GUILayout.Button("Open TensorBoard"))
                    StartTensorBoard();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Open in Browser"))
                    Application.OpenURL("http://localhost:6006");
                if (GUILayout.Button("Stop TensorBoard"))
                    StopTensorBoard();
                EditorGUILayout.EndHorizontal();
            }
        });
    }

    // ─── 3. Observe ───────────────────────────────────────

    private void DrawObserveSection()
    {
        DrawSection("Observe (no training)", () =>
        {
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("Launch Game", GUILayout.Height(35)))
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = false;
                    void OnExit(PlayModeStateChange s)
                    {
                        if (s == PlayModeStateChange.EnteredEditMode)
                        {
                            EditorApplication.playModeStateChanged -= OnExit;
                            DoResetSetupPlay();
                        }
                    }
                    EditorApplication.playModeStateChanged += OnExit;
                }
                else
                {
                    DoResetSetupPlay();
                }
            }
            GUI.backgroundColor = Color.white;

            // Speed info (read-only, config is edited in Configuration section).
            var config = GameConfigEditor.GetOrCreateConfig();
            if (config != null)
            {
                float ticksPerSec = 1000f / config.msPerTick;
                EditorGUILayout.LabelField($"Speed: {config.msPerTick}ms/tick ({ticksPerSec:F0} ticks/s)", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Reset Game"))
                ResetGame();
        });
    }

    // ─── 3b. Replay ─────────────────────────────────────────

    private string selectedReplayPath;

    private void DrawReplaySection()
    {
        DrawSection("Replay", () =>
        {
            // File picker.
            EditorGUILayout.BeginHorizontal();
            string displayName = string.IsNullOrEmpty(selectedReplayPath)
                ? "(no file selected)"
                : System.IO.Path.GetFileName(selectedReplayPath);
            EditorGUILayout.LabelField(displayName, EditorStyles.miniLabel);

            if (GUILayout.Button("Browse...", GUILayout.Width(70)))
            {
                string dir = System.IO.Path.GetFullPath("Replays");
                if (!System.IO.Directory.Exists(dir)) dir = System.IO.Path.GetFullPath(".");
                string path = EditorUtility.OpenFilePanel("Select Replay File", dir, "jsonl");
                if (!string.IsNullOrEmpty(path))
                    selectedReplayPath = path;
            }
            EditorGUILayout.EndHorizontal();

            // Quick pick: latest replay.
            if (GUILayout.Button("Select Latest Replay"))
            {
                string dir = System.IO.Path.GetFullPath("Replays");
                if (System.IO.Directory.Exists(dir))
                {
                    var files = System.IO.Directory.GetFiles(dir, "game_*.jsonl");
                    if (files.Length > 0)
                    {
                        string latest = files[0];
                        var latestTime = System.IO.File.GetLastWriteTime(latest);
                        foreach (var f in files)
                        {
                            var t = System.IO.File.GetLastWriteTime(f);
                            if (t > latestTime) { latest = f; latestTime = t; }
                        }
                        selectedReplayPath = latest;
                    }
                    else
                    {
                        Debug.LogWarning("[Replay] No replay files found in Replays/");
                    }
                }
            }

            // Launch replay button.
            EditorGUILayout.Space(4);
            GUI.enabled = !string.IsNullOrEmpty(selectedReplayPath);
            GUI.backgroundColor = new Color(1f, 0.84f, 0f);
            if (GUILayout.Button("Play Replay", GUILayout.Height(35)))
                LaunchReplay(selectedReplayPath);
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        });
    }

    private static void LaunchReplay(string replayPath)
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            void OnExit(PlayModeStateChange s)
            {
                if (s == PlayModeStateChange.EnteredEditMode)
                {
                    EditorApplication.playModeStateChanged -= OnExit;
                    DoLaunchReplay(replayPath);
                }
            };
            EditorApplication.playModeStateChanged += OnExit;
        }
        else
        {
            DoLaunchReplay(replayPath);
        }
    }

    private static void DoLaunchReplay(string replayPath)
    {
        ReplayPlayer.PendingReplayPath = replayPath;

        var scenePath = "Assets/Scenes/SampleScene.unity";
        if (System.IO.File.Exists(scenePath))
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        HexGridSetup.Reset();
        HexGridSetup.SetupScene();
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        EditorApplication.isPlaying = true;
    }

    // ─── 4. Analysis ──────────────────────────────────────

    private void DrawAnalysisSection()
    {
        DrawSection("Analysis", () =>
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Last N games:", GUILayout.Width(90));
            analyzeCount = EditorGUILayout.IntField(analyzeCount, GUILayout.Width(50));
            if (GUILayout.Button("Analyze"))
            {
                var results = StrategyAnalyzer.AnalyzeLast(analyzeCount);
                StrategyAnalyzer.LogResults(results);
                lastAnalysisResult = results.Count > 0
                    ? $"Analyzed {results.Count} games. See Console."
                    : "No replay files found.";
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Analyze All Replays"))
            {
                var results = StrategyAnalyzer.AnalyzeAll();
                StrategyAnalyzer.LogResults(results);
                lastAnalysisResult = results.Count > 0
                    ? $"Analyzed {results.Count} games. CSV exported."
                    : "No replay files found.";
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.LabelField("AI Highlights", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Detect Highlights (Last N)"))
            {
                string path = HighlightDetector.AnalyzeAndExport(analyzeCount);
                lastAnalysisResult = path != null
                    ? $"Highlights exported to {path}. Open in Claude Code for AI interpretation."
                    : "No replay files found.";
            }
            if (GUILayout.Button("Detect All"))
            {
                string path = HighlightDetector.AnalyzeAndExport();
                lastAnalysisResult = path != null
                    ? $"Highlights exported to {path}. Open in Claude Code for AI interpretation."
                    : "No replay files found.";
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Open Replays Folder"))
            {
                string dir = System.IO.Path.GetFullPath("Replays");
                if (System.IO.Directory.Exists(dir))
                    EditorUtility.RevealInFinder(dir);
                else
                    Debug.LogWarning("[ProjectTools] Replays folder not found yet. Play some games first.");
            }

            if (!string.IsNullOrEmpty(lastAnalysisResult))
                EditorGUILayout.HelpBox(lastAnalysisResult, MessageType.Info);
        });
    }

    // ─── 5. Testing ───────────────────────────────────────

    private void DrawTestingSection()
    {
        DrawSection("Testing", () =>
        {
            if (GUILayout.Button("Run EditMode Tests", GUILayout.Height(28)))
                AutoTestRunner.RunEditModeTests();

            if (GUILayout.Button(
                    EditorApplication.isPlaying ? "Stop Game + Open Test Runner" : "Open Test Runner",
                    GUILayout.Height(25)))
            {
                RunPlayModeTestsSafely();
            }

            EditorGUILayout.Space(4);
            bool autoTest = AutoTestRunner.Enabled;
            bool newAutoTest = EditorGUILayout.Toggle("Auto-run after compile", autoTest);
            if (newAutoTest != autoTest)
                AutoTestRunner.Enabled = newAutoTest;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUI.backgroundColor = new Color(1f, 0.4f, 0.3f);
            if (GUILayout.Button("Reset All History & Models", GUILayout.Height(28)))
                ResetAllHistory();
            GUI.backgroundColor = Color.white;
        });
    }

    // ─── 6. Scene Setup (collapsed foldout) ───────────────

    private void DrawSceneSetupSection()
    {
        EditorGUILayout.Space(8);
        bool open = EditorPrefs.GetBool(k_SceneSetupFoldoutKey, false);
        bool newOpen = EditorGUILayout.Foldout(open, "Scene Setup", true, EditorStyles.boldLabel);
        if (newOpen != open)
            EditorPrefs.SetBool(k_SceneSetupFoldoutKey, newOpen);

        if (newOpen)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Setup Scene", GUILayout.Height(28)))
                HexGridSetup.SetupScene();
            if (GUILayout.Button("Reset Scene", GUILayout.Height(28)))
                HexGridSetup.Reset();
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Randomize Tile Ownership"))
                RandomizeTileOwnership();

            EditorGUILayout.EndVertical();
        }
    }

    // ─── ML Training Logic ────────────────────────────────

    private enum TrainingMode { Force, Resume, InitFrom }

    private static void StartTraining(TrainingMode mode = TrainingMode.Force, string initFromRunId = null)
    {
        StopTraining();

        if (!File.Exists(PythonExe))
        {
            Debug.LogError($"[ML-Train] Python not found at: {PythonExe}");
            return;
        }
        if (!File.Exists(ConfigPath))
        {
            Debug.LogError($"[ML-Train] Config not found at: {ConfigPath}");
            return;
        }

        // Guard: validate prerequisites before launching Python.
        if (mode == TrainingMode.Resume && !Directory.Exists(Path.GetFullPath($"results/{runId}")))
        {
            Debug.LogError($"[ML-Train] Cannot resume: results/{runId}/ not found. Use 'Start Training' first.");
            return;
        }
        if (mode == TrainingMode.InitFrom)
        {
            if (string.IsNullOrEmpty(initFromRunId))
            {
                Debug.LogError("[ML-Train] Cannot init-from: no source run ID specified.");
                return;
            }
            string onnxPath = Path.GetFullPath($"results/{initFromRunId}/HexRobot.onnx");
            if (!File.Exists(onnxPath))
            {
                Debug.LogError($"[ML-Train] Cannot init-from: results/{initFromRunId}/HexRobot.onnx not found.");
                return;
            }
        }

        string modeFlag = mode switch
        {
            TrainingMode.Resume  => "--resume",
            TrainingMode.InitFrom => $"--initialize-from={initFromRunId}",
            _                    => "--force",
        };

        var psi = new ProcessStartInfo
        {
            FileName = PythonExe,
            Arguments = $"-m mlagents.trainers.learn \"{ConfigPath}\" --run-id={runId} {modeFlag}",
            WorkingDirectory = System.IO.Path.GetFullPath("."),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            EnvironmentVariables = { ["PYTHONIOENCODING"] = "utf-8" }
        };

        pythonReady = false;
        trainingProcess = new Process { StartInfo = psi };
        trainingProcess.OutputDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                UnityEngine.Debug.Log($"[ML-Train] {e.Data}");
                if (e.Data.Contains("Listening on port 5004"))
                    pythonReady = true;
            }
        };
        trainingProcess.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                // Filter deprecation warnings.
                if (e.Data.Contains("UserWarning") || e.Data.Contains("pkg_resources"))
                    return;
                UnityEngine.Debug.Log($"[ML-Train] {e.Data}");
                if (e.Data.Contains("Listening on port 5004"))
                    pythonReady = true;
            }
        };

        // Snapshot current counters so we can compute the training delta at stop.
        trainStartGames  = PlayerPrefs.GetInt("TotalGames", 0);
        trainStartRounds = PlayerPrefs.GetInt("TotalRoundsLo", 0);

        try
        {
            trainingExitLogged = false;
            trainingProcess.Start();
            trainingProcess.BeginOutputReadLine();
            trainingProcess.BeginErrorReadLine();
            SessionState.SetInt(k_TrainingPidKey, trainingProcess.Id);
            Debug.Log($"[ML-Train] Started training (run-id: {runId}, PID: {trainingProcess.Id}). Python is loading — wait for 'Listening on port 5004'...");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ML-Train] Failed to start process: {ex.Message}");
            trainingProcess = null;
            return;
        }

        // Show Console so user can see training output.
        EditorApplication.ExecuteMenuItem("Window/General/Console");

        // Force window repaint to update button state.
        if (HasOpenInstances<ProjectToolsWindow>())
            GetWindow<ProjectToolsWindow>().Repaint();
    }

    private static void StartTrainingAndPlay()
    {
        if (EditorApplication.isPlaying)
        {
            // Must wait for Play mode to fully exit before saving scene.
            EditorApplication.isPlaying = false;
            void OnPlayModeChanged(PlayModeStateChange state)
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                    DoStartTrainingAndPlay();
                }
            }
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
        else
        {
            DoStartTrainingAndPlay();
        }
    }

    private static void DoStartTrainingAndPlay()
    {
        // Guard: if scripts are compiling, SaveAssets() will flush the compilation
        // and trigger a domain reload that interrupts this method before StartTraining().
        if (EditorApplication.isCompiling)
        {
            Debug.LogWarning("[ML-Train] Scripts are compiling — please wait and try again.");
            return;
        }

        // Clear any stale AutoPlayPending from a previous failed attempt.
        SessionState.EraseBool(k_AutoPlayPending);

        // Increment run counter for unique ID.
        int next = RunCounter;
        RunCounter = next + 1;
        runId = $"run{next}";

        // Persist runId BEFORE Reset/SetupScene so that if SaveAssets() triggers a
        // domain reload mid-function, TryResumeTrainingStart() in OnEnable can pick up.
        SessionState.SetBool(k_StartTrainingPending, true);
        SessionState.SetString(k_StartTrainingRunId, runId);

        // Reset and setup scene, then persist so prefab refs survive Play mode.
        HexGridSetup.Reset();
        HexGridSetup.SetupScene();
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();

        // Start Python training.
        StartTraining();

        // Reached here — no domain reload happened. Clear the pending flag.
        SessionState.EraseBool(k_StartTrainingPending);

        // Wait for Python "Listening on port 5004" before entering Play mode.
        RegisterWaitForPython();
    }

    /// <summary>
    /// Registers an EditorApplication.update callback that waits for Python
    /// to print "Listening on port 5004", then enters Play mode automatically.
    /// Fallback: enters Play after 90 s even if the message never arrives.
    /// </summary>
    private static void RegisterWaitForPython()
    {
        double startTime = EditorApplication.timeSinceStartup;
        const double maxWaitSeconds = 90.0;
        EditorApplication.CallbackFunction waitForPython = null;
        waitForPython = () =>
        {
            double elapsed = EditorApplication.timeSinceStartup - startTime;
            bool timedOut = elapsed > maxWaitSeconds;

            if (pythonReady || timedOut)
            {
                EditorApplication.update -= waitForPython;
                if (timedOut && !pythonReady)
                    Debug.LogWarning("[ML-Train] Timed out waiting for Python. Starting Play anyway.");
                else
                    Debug.Log("[ML-Train] Python ready — auto-starting Play mode.");
                SessionState.SetBool(k_AutoPlayPending, true);
                SessionState.SetBool(k_IsEnteringPlayMode, true);
                EditorApplication.isPlaying = true;
            }
        };
        EditorApplication.update += waitForPython;
    }

    private static void StopTraining()
    {
        if (trainingProcess != null && !trainingProcess.HasExited)
        {
            trainingProcess.Kill();
            trainingProcess.Dispose();
            Debug.Log("[ML-Train] Training stopped.");
        }
        trainingProcess = null;
        SessionState.EraseInt(k_TrainingPidKey);
        SessionState.EraseBool(k_AutoPlayPending);
        SessionState.EraseBool(k_IsEnteringPlayMode);
        SessionState.EraseBool(k_StartTrainingPending);

        KillOrphanedTrainers();
        LoadBestModels(runId);

        if (HasOpenInstances<ProjectToolsWindow>())
            GetWindow<ProjectToolsWindow>().Repaint();
    }

    /// <summary>
    /// Find the latest .onnx models from a training run and copy them into
    /// Assets/Resources/ so they can be assigned to agents at runtime.
    /// </summary>
    private static void LoadBestModels(string fromRunId)
    {
        string resultsDir = Path.GetFullPath($"results/{fromRunId}");
        if (!Directory.Exists(resultsDir))
        {
            Debug.LogWarning($"[ML-Train] No results folder for {fromRunId}.");
            return;
        }

        string robotOnnx  = Path.Combine(resultsDir, "HexRobot.onnx");
        string mutantOnnx = Path.Combine(resultsDir, "HexMutant.onnx");

        if (!File.Exists(robotOnnx) || !File.Exists(mutantOnnx))
        {
            Debug.LogWarning($"[ML-Train] ONNX models not found in {resultsDir}.");
            return;
        }

        string destDir = "Assets/Resources";
        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        File.Copy(robotOnnx, Path.Combine(destDir, "HexRobot.onnx"), true);
        File.Copy(mutantOnnx, Path.Combine(destDir, "HexMutant.onnx"), true);
        AssetDatabase.Refresh();

        // Read training steps from status JSON.
        long totalSteps = 0;
        string statusPath = Path.Combine(resultsDir, "run_logs", "training_status.json");
        if (File.Exists(statusPath))
        {
            string json = File.ReadAllText(statusPath);
            foreach (string line in json.Split('\n'))
            {
                string trimmed = line.Trim().Trim(',');
                if (trimmed.StartsWith("\"steps\""))
                {
                    string val = trimmed.Split(':')[1].Trim().Trim(',');
                    if (long.TryParse(val, out long s) && s > totalSteps)
                        totalSteps = s;
                }
            }
        }

        EditorPrefs.SetString(ModelRunIdKey, fromRunId);
        EditorPrefs.SetInt(ModelStepsKey, (int)totalSteps);

        int nowGames  = PlayerPrefs.GetInt("TotalGames", 0);
        int nowRounds = PlayerPrefs.GetInt("TotalRoundsLo", 0);
        int trainGames  = Mathf.Max(0, nowGames  - trainStartGames);
        int trainRounds = Mathf.Max(0, nowRounds - trainStartRounds);
        PlayerPrefs.SetInt("TrainedOnGames",  trainGames);
        PlayerPrefs.SetInt("TrainedOnRounds", trainRounds);
        PlayerPrefs.SetString("TrainedRunId", fromRunId);
        PlayerPrefs.SetInt("TrainedSteps", (int)totalSteps);
        PlayerPrefs.Save();

        Debug.Log($"[ML-Train] Loaded models from {fromRunId} ({totalSteps:N0} steps, {trainGames} games, {trainRounds} rounds). Models at {destDir}/");

        if (EditorApplication.isPlaying)
            AssignModelsToAgents();
    }

    private static void AssignModelsToAgents()
    {
        var robotModel  = AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>("Assets/Resources/HexRobot.onnx");
        var mutantModel = AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>("Assets/Resources/HexMutant.onnx");

        int assigned = 0;
        foreach (var bp in Object.FindObjectsByType<BehaviorParameters>(FindObjectsSortMode.None))
        {
            if (bp.BehaviorName == "HexRobot" && robotModel != null)
            {
                bp.Model = robotModel;
                bp.BehaviorType = BehaviorType.InferenceOnly;
                assigned++;
            }
            else if (bp.BehaviorName == "HexMutant" && mutantModel != null)
            {
                bp.Model = mutantModel;
                bp.BehaviorType = BehaviorType.InferenceOnly;
                assigned++;
            }
        }
        Debug.Log($"[ML-Train] Models assigned to {assigned} agents (InferenceOnly).");
    }

    /// <summary>Kill any orphaned mlagents Python processes holding port 5004.</summary>
    private static void KillOrphanedTrainers()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("python"))
            {
                try
                {
                    if (proc.MainModule != null && proc.MainModule.FileName == PythonExe)
                    {
                        Debug.Log($"[ML-Train] Killing orphaned Python process (PID {proc.Id}).");
                        proc.Kill();
                    }
                }
                catch { /* access denied or already exited — ignore */ }
            }
        }
        catch { /* GetProcessesByName failed — ignore */ }
    }

    private static void DoResetSetupPlay()
    {
        var scenePath = "Assets/Scenes/SampleScene.unity";
        if (System.IO.File.Exists(scenePath))
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        HexGridSetup.Reset();
        HexGridSetup.SetupScene();
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        EditorApplication.isPlaying = true;
    }

    private static void StartTensorBoard()
    {
        StopTensorBoard();

        string resultsDir = System.IO.Path.GetFullPath("results");

        var psi = new ProcessStartInfo
        {
            FileName = PythonExe,
            Arguments = $"-m tensorboard.main --logdir \"{resultsDir}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        tensorboardProcess = new Process { StartInfo = psi };
        tensorboardProcess.Start();

        UnityEngine.Debug.Log("[TensorBoard] Started at http://localhost:6006");
        Application.OpenURL("http://localhost:6006");
    }

    private static void StopTensorBoard()
    {
        if (tensorboardProcess != null && !tensorboardProcess.HasExited)
        {
            tensorboardProcess.Kill();
            tensorboardProcess.Dispose();
            UnityEngine.Debug.Log("[TensorBoard] Stopped.");
        }
        tensorboardProcess = null;
    }

    // ─── Utility ──────────────────────────────────────────

    private static void DrawSection(string title, System.Action content)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        try
        {
            content();
        }
        catch (System.Exception ex) when (Event.current?.type != EventType.Layout)
        {
            Debug.LogException(ex);
        }
        EditorGUILayout.EndVertical();
    }

    private static void RandomizeTileOwnership()
    {
        var grid = Object.FindFirstObjectByType<HexGrid>();
        if (grid == null || grid.Tiles == null || grid.Tiles.Count == 0)
        {
            Debug.LogWarning("[ProjectTools] No HexGrid found. Run Setup Scene and enter Play mode first.");
            return;
        }

        var teams = new[] { Team.None, Team.Robot, Team.Mutant };
        foreach (var tile in grid.Tiles.Values)
        {
            Team team = teams[Random.Range(0, teams.Length)];
            tile.Owner = team;

            if (team == Team.Robot && Random.value < 0.3f)
                tile.TileType = TileType.Crate;
            else if (team == Team.Mutant && Random.value < 0.3f)
                tile.TileType = TileType.Slime;
            else
                tile.TileType = TileType.Empty;

            tile.Fortification = Random.value < 0.2f ? Random.Range(1, 4) : 0;
        }
        Debug.Log("[ProjectTools] Randomized tile ownership, types, and fortification.");
    }

    private static void ResetGame()
    {
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            Debug.LogWarning("[ProjectTools] No GameManager found. Enter Play mode first.");
            return;
        }
        gm.ResetGame();
    }

    private static void ResetAllHistory()
    {
        if (!EditorUtility.DisplayDialog("Reset All History",
                "This will delete:\n" +
                "• All PlayerPrefs (game counters, match history)\n" +
                "• Trained ONNX models in Assets/Resources/\n" +
                "• All replay files in Replays/\n" +
                "• Training run counter\n\n" +
                "results/ folder is kept (gitignored). Continue?",
                "Reset Everything", "Cancel"))
            return;

        // 1. PlayerPrefs — game counters.
        PlayerPrefs.DeleteKey("TotalGames");
        PlayerPrefs.DeleteKey("TotalTurnsHi");
        PlayerPrefs.DeleteKey("TotalTurnsLo");
        PlayerPrefs.DeleteKey("TrainedRunId");
        PlayerPrefs.DeleteKey("TrainedOnGames");
        PlayerPrefs.DeleteKey("TrainedOnRounds");
        PlayerPrefs.DeleteKey("TrainedSteps");
        PlayerPrefs.Save();

        // 2. EditorPrefs — training model info + run counter.
        EditorPrefs.DeleteKey(ModelRunIdKey);
        EditorPrefs.DeleteKey(ModelStepsKey);
        EditorPrefs.DeleteKey("MLTrain_RunCounter");

        // 3. ONNX models in Assets/Resources/.
        string[] modelFiles = { "Assets/Resources/HexRobot.onnx", "Assets/Resources/HexMutant.onnx" };
        foreach (var f in modelFiles)
        {
            if (File.Exists(f)) File.Delete(f);
            string meta = f + ".meta";
            if (File.Exists(meta)) File.Delete(meta);
        }

        // 4. Replay files.
        string replayDir = Path.GetFullPath("Replays");
        if (Directory.Exists(replayDir))
        {
            Directory.Delete(replayDir, true);
            Debug.Log($"[Reset] Deleted {replayDir}");
        }

        // 5. Reset run counter to 1.
        RunCounter = 1;
        runId = "run1";

        AssetDatabase.Refresh();
        Debug.Log("[Reset] All history, models, and replays cleared. Fresh start.");
    }

    private static void RunPlayModeTestsSafely()
    {
        EditorApplication.ExecuteMenuItem("Window/General/Test Runner");

        if (EditorApplication.isPlaying)
        {
            Debug.Log("[ProjectTools] Stopping game — click 'Run All' in Test Runner once Unity exits Play mode.");
            EditorApplication.isPlaying = false;
        }
    }
}
