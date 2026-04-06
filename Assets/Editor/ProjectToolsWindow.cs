using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    private string lastAnalysisResult;

    // Foldout states (persisted via EditorPrefs).
    private const string k_AdvancedFoldoutKey = "ProjectTools_AdvancedFoldout";
    private const string k_ConfigDetailFoldoutKey = "ProjectTools_ConfigDetailFoldout";

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
        DrawLaunchSection();
        DrawTestingSection();

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

            // ── Always visible: frequently adjusted settings ──
            config.maxSteps = EditorGUILayout.IntSlider("Max Steps", config.maxSteps, 100, 10000);

            config.msPerTick = EditorGUILayout.IntSlider("ms / tick", config.msPerTick, 1, 5000);
            float ticksPerSec = 1000f / config.msPerTick;
            EditorGUILayout.LabelField($"  {ticksPerSec:F1} ticks/s  (timeScale = {config.TimeScale:F2})", EditorStyles.miniLabel);

            // ── All other settings in a collapsible foldout ──
            EditorGUILayout.Space(4);
            bool detailOpen = EditorPrefs.GetBool(k_ConfigDetailFoldoutKey, false);
            bool newDetailOpen = EditorGUILayout.Foldout(detailOpen, "All Parameters", true);
            if (newDetailOpen != detailOpen)
                EditorPrefs.SetBool(k_ConfigDetailFoldoutKey, newDetailOpen);

            if (newDetailOpen)
            {
                EditorGUILayout.LabelField("Board", EditorStyles.miniBoldLabel);
                config.boardSide = EditorGUILayout.IntSlider("Board Side", config.boardSide, 3, 15);
                int tileCount = 3 * config.boardSide * config.boardSide - 3 * config.boardSide + 1;
                EditorGUILayout.LabelField($"  Total tiles: {tileCount}", EditorStyles.miniLabel);

                config.unitsPerTeam = EditorGUILayout.IntSlider("Units per Team", config.unitsPerTeam, 1, 10);
                EditorGUILayout.LabelField($"  Base size: {Mathf.Min(config.unitsPerTeam, 4)} tiles/team", EditorStyles.miniLabel);

                config.winPercent = EditorGUILayout.IntSlider("Win %", config.winPercent, 10, 100);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Energy", EditorStyles.miniBoldLabel);
                config.unitMaxEnergy = EditorGUILayout.IntSlider("Max Energy", config.unitMaxEnergy, 5, 30);
                config.respawnCooldown = EditorGUILayout.IntSlider("Respawn Cooldown", config.respawnCooldown, 1, 30);
                config.baseRegenPerStep = EditorGUILayout.IntSlider("Base Regen/step", config.baseRegenPerStep, 0, 10);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Structures", EditorStyles.miniBoldLabel);
                config.wallBuildCost = EditorGUILayout.IntSlider("Wall Build Cost", config.wallBuildCost, 1, 10);
                config.wallMaxHP = EditorGUILayout.IntSlider("Wall Max HP", config.wallMaxHP, 1, 5);
                config.slimePlaceCost = EditorGUILayout.IntSlider("Slime Place Cost", config.slimePlaceCost, 1, 10);
                config.destroyOwnWallCost = EditorGUILayout.IntSlider("Destroy Own Wall Cost", config.destroyOwnWallCost, 0, 5);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Combat", EditorStyles.miniBoldLabel);
                config.attackUnitCost = EditorGUILayout.IntSlider("Attack Unit Cost", config.attackUnitCost, 1, 10);
                config.attackUnitDamage = EditorGUILayout.IntSlider("Attack Unit Damage", config.attackUnitDamage, 1, 10);
                config.attackWallCost = EditorGUILayout.IntSlider("Attack Wall Cost", config.attackWallCost, 1, 10);
                config.slimeEntryCostRobot = EditorGUILayout.IntSlider("Slime Entry Cost (Robot)", config.slimeEntryCostRobot, 0, 10);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Proximity Bonuses", EditorStyles.miniBoldLabel);
                config.shieldWallMaxReduction = EditorGUILayout.IntSlider("Shield Wall Max Reduction", config.shieldWallMaxReduction, 0, 5);
                config.swarmMaxBonus = EditorGUILayout.IntSlider("Swarm Max Bonus", config.swarmMaxBonus, 0, 5);

                EditorGUILayout.Space(4);
                config.replayLogEveryNthGame = EditorGUILayout.IntSlider("Replay: log every Nth game", config.replayLogEveryNthGame, 1, 1000);
            }

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
            // --- Silent training toggle ---
            bool silent = EditorPrefs.GetBool("ProjectTools_SilentTraining", false);
            bool newSilent = EditorGUILayout.Toggle("Train Silent (no graphics)", silent);
            if (newSilent != silent)
                EditorPrefs.SetBool("ProjectTools_SilentTraining", newSilent);

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

    private void DrawLaunchSection()
    {
        DrawSection("Launch", () =>
        {
            GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
            if (GUILayout.Button("Launch Main Menu", GUILayout.Height(35)))
                LaunchMainMenu();
            GUI.backgroundColor = Color.white;

            GUILayout.Space(5);

            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("Launch AI vs AI (observe)", GUILayout.Height(30)))
            {
                GameModeConfig.CurrentMode = GameMode.Training;
                SessionState.SetString("GameMode", "Training");

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

            EditorGUILayout.LabelField("Play vs AI, Replays, Settings → use Main Menu", EditorStyles.miniLabel);
        });
    }

    private void LaunchMainMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            void OnExit(PlayModeStateChange s)
            {
                if (s == PlayModeStateChange.EnteredEditMode)
                {
                    EditorApplication.playModeStateChanged -= OnExit;
                    DoLaunchMainMenu();
                }
            };
            EditorApplication.playModeStateChanged += OnExit;
        }
        else
        {
            DoLaunchMainMenu();
        }
    }

    private static void DoLaunchMainMenu()
    {
        // Auto-create MainMenu scene if it doesn't exist yet.
        if (!System.IO.File.Exists("Assets/Scenes/MainMenu.unity"))
            MainMenuSetup.SetupMainMenuScene();
        else
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        EditorApplication.isPlaying = true;
    }

    // ─── 4. Testing ───────────────────────────────────────

    private void DrawTestingSection()
    {
        DrawSection("Testing & Analysis", () =>
        {
            GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
            if (GUILayout.Button("Run All Tests", GUILayout.Height(35)))
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = false;
                    EditorApplication.delayCall += () => AutoTestRunner.RunAllTests();
                }
                else
                {
                    AutoTestRunner.RunAllTests();
                }
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Open Test Runner (expanded)"))
                OpenTestRunnerExpanded();

            EditorGUILayout.Space(4);
            bool autoTest = AutoTestRunner.Enabled;
            bool newAutoTest = EditorGUILayout.Toggle("Auto-run after compile", autoTest);
            if (newAutoTest != autoTest)
                AutoTestRunner.Enabled = newAutoTest;

            // ── Replay analysis ──
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.LabelField("Replay Analysis", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Analyze All Replays"))
            {
                var results = StrategyAnalyzer.AnalyzeAll();
                StrategyAnalyzer.LogResults(results);
                lastAnalysisResult = results.Count > 0
                    ? $"Analyzed {results.Count} games. CSV exported."
                    : "No replay files found.";
            }
            if (GUILayout.Button("Open Replays Folder"))
            {
                string dir = GameReplayLogger.ReplayRootDir;
                if (System.IO.Directory.Exists(dir))
                    EditorUtility.RevealInFinder(dir);
                else
                    Debug.LogWarning("[ProjectTools] Replays folder not found yet.");
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(lastAnalysisResult))
                EditorGUILayout.HelpBox(lastAnalysisResult, MessageType.Info);

            // ── Danger zone ──
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUI.backgroundColor = new Color(1f, 0.4f, 0.3f);
            if (GUILayout.Button("Reset All History & Models", GUILayout.Height(28)))
                ResetAllHistory();
            GUI.backgroundColor = Color.white;
        });
    }


    private static void OpenTestRunnerExpanded()
    {
        EditorApplication.ExecuteMenuItem("Window/General/Test Runner");

        EditorApplication.delayCall += () =>
        {
            try
            {
                var asm = System.Reflection.Assembly.Load("UnityEditor.TestRunner");
                if (asm == null) return;

                var windowType = asm.GetType("UnityEditor.TestTools.TestRunner.TestRunnerWindow");
                if (windowType == null) return;

                var window = EditorWindow.GetWindow(windowType);
                if (window == null) return;

                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public
                          | System.Reflection.BindingFlags.Instance;

                // Keep whichever tab the user had selected (expand both anyway).

                // Expand all trees in both EditMode and PlayMode GUIs.
                var guisField = windowType.GetField("m_TestListGUIs", flags);
                if (guisField != null)
                {
                    var arr = guisField.GetValue(window) as System.Array;
                    if (arr != null)
                    {
                        foreach (var gui in arr)
                        {
                            if (gui == null) continue;
                            ExpandTestListTree(gui, flags);
                        }
                    }
                }

                window.Repaint();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ProjectTools] Test Runner expand failed: {ex.Message}");
            }
        };
    }

    private static void ExpandTestListTree(object testListGui, System.Reflection.BindingFlags flags)
    {
        var guiType = testListGui.GetType();

        // Get TreeViewController to read actual item IDs.
        var treeField = guiType.GetField("m_TestListTree", flags);
        if (treeField == null) return;
        var tree = treeField.GetValue(testListGui);
        if (tree == null) return;

        // Get TreeViewState to set expandedIDs.
        var stateField = guiType.GetField("m_TestListState", flags);
        if (stateField == null) return;
        var state = stateField.GetValue(testListGui);
        if (state == null) return;

        // Collect item IDs from top 3 levels (root → dll → test classes), not individual tests.
        var ids = new System.Collections.Generic.List<int>();
        CollectItemIDsToDepth(tree, flags, ids, 3);

        if (ids.Count == 0)
        {
            // Fallback: brute-force range including negative IDs.
            for (int i = -1000; i < 1000; i++) ids.Add(i);
        }

        ids.Sort();

        var expandedProp = state.GetType().GetProperty("expandedIDs");
        if (expandedProp != null)
            expandedProp.SetValue(state, ids);

        // Reload tree to apply.
        var reload = tree.GetType().GetMethod("ReloadData", flags);
        if (reload != null) reload.Invoke(tree, null);
    }

    private static void CollectItemIDsToDepth(object tree, System.Reflection.BindingFlags flags,
        System.Collections.Generic.List<int> ids, int maxDepth)
    {
        var treeType = tree.GetType();

        // Try data source with root → children traversal (depth-limited).
        foreach (var prop in treeType.GetProperties(flags))
        {
            if (prop.Name == "data" || prop.Name == "dataSource")
            {
                try
                {
                    var data = prop.GetValue(tree);
                    if (data == null) continue;
                    var rootProp = data.GetType().GetProperty("root", flags);
                    if (rootProp != null)
                    {
                        var root = rootProp.GetValue(data);
                        if (root != null)
                        {
                            CollectIDsToDepth(root, flags, ids, 0, maxDepth);
                            if (ids.Count > 0) return;
                        }
                    }
                }
                catch { /* skip */ }
            }
        }

        // Fallback: use GetRows (flat list) — expand items with depth < maxDepth.
        var getRows = treeType.GetMethod("GetRows", flags);
        if (getRows != null)
        {
            try
            {
                var rows = getRows.Invoke(tree, null) as System.Collections.IEnumerable;
                if (rows != null)
                {
                    foreach (var item in rows)
                    {
                        var depthProp = item.GetType().GetProperty("depth", flags);
                        var idProp = item.GetType().GetProperty("id", flags);
                        if (idProp == null) continue;

                        int depth = depthProp != null ? (int)depthProp.GetValue(item) : 0;
                        if (depth < maxDepth)
                            ids.Add((int)idProp.GetValue(item));
                    }
                }
            }
            catch { /* skip */ }
        }

        // Last resort: brute force.
        if (ids.Count == 0)
            for (int i = -1000; i < 1000; i++) ids.Add(i);
    }

    private static void CollectIDsToDepth(object item, System.Reflection.BindingFlags flags,
        System.Collections.Generic.List<int> ids, int currentDepth, int maxDepth)
    {
        if (item == null || currentDepth >= maxDepth) return;

        var idProp = item.GetType().GetProperty("id", flags);
        if (idProp != null)
            ids.Add((int)idProp.GetValue(item));

        var childrenProp = item.GetType().GetProperty("children", flags);
        if (childrenProp == null) return;
        var children = childrenProp.GetValue(item) as System.Collections.IEnumerable;
        if (children == null) return;

        foreach (var child in children)
            CollectIDsToDepth(child, flags, ids, currentDepth + 1, maxDepth);
    }

    // ─── ML Training Logic ────────────────────────────────

    private enum TrainingMode { Force, Resume, InitFrom }

    /// <summary>Validate run ID to prevent command injection (alphanumeric + underscore only).</summary>
    private static bool IsValidRunId(string id)
    {
        return !string.IsNullOrEmpty(id) && Regex.IsMatch(id, @"^[a-zA-Z0-9_]+$");
    }

    private static void StartTraining(TrainingMode mode = TrainingMode.Force, string initFromRunId = null)
    {
        StopTraining();

        if (!IsValidRunId(runId))
        {
            Debug.LogError($"[ML-Train] Invalid run ID: '{runId}'. Only alphanumeric and underscore allowed.");
            return;
        }
        if (initFromRunId != null && !IsValidRunId(initFromRunId))
        {
            Debug.LogError($"[ML-Train] Invalid init-from run ID: '{initFromRunId}'. Only alphanumeric and underscore allowed.");
            return;
        }

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

        // Set silent training flag before entering Play mode.
        bool silent = EditorPrefs.GetBool("ProjectTools_SilentTraining", false);
        SessionState.SetBool("SilentTraining", silent);

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

        // Propagate silent training flag so GameManager picks it up on Play.
        bool silent = EditorPrefs.GetBool("ProjectTools_SilentTraining", false);
        SessionState.SetBool("SilentTraining", silent);

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
                "• All training results in results/\n" +
                "• Training run counter\n\n" +
                "Continue?",
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
        string replayRoot = GameReplayLogger.ReplayRootDir;
        if (Directory.Exists(replayRoot))
        {
            Directory.Delete(replayRoot, true);
            Debug.Log($"[Reset] Deleted {replayRoot}");
        }

        // 5. Training results.
        string resultsDir = Path.GetFullPath("results");
        if (Directory.Exists(resultsDir))
        {
            Directory.Delete(resultsDir, true);
            Debug.Log($"[Reset] Deleted {resultsDir}");
        }

        // 6. Reset run counter to 1.
        RunCounter = 1;
        runId = "run1";

        AssetDatabase.Refresh();
        Debug.Log("[Reset] All history, models, results, and replays cleared. Fresh start.");
    }

}
