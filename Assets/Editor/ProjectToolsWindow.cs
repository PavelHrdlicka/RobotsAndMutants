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
/// Open via: Tools > Project Tools Window
/// </summary>
public class ProjectToolsWindow : EditorWindow
{
    private Vector2 scrollPos;
    private bool autoPlay;
    private float autoPlayInterval = 0.1f;
    private double nextAutoPlayTime;

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
    private const string k_TrainingPidKey      = "ProjectTools_TrainingPID";
    private const string k_PythonReadyKey      = "ProjectTools_PythonReady";
    private const string k_AutoPlayPending     = "ProjectTools_AutoPlayPending";
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
        EditorApplication.delayCall += () =>
        {
            if (!HasOpenInstances<ProjectToolsWindow>())
                ShowWindow();

            // Run simulation even when Unity editor is not focused.
            Application.runInBackground = true;
        };
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

        // isPlayingOrWillChangePlaymode is true both when already playing AND
        // when a domain reload is happening as part of entering Play mode.
        // In that case Unity will finish entering Play mode on its own — do nothing.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.EraseBool(k_AutoPlayPending);
            return;
        }

        // We are truly in Edit mode (e.g. domain reload happened due to script
        // compilation mid-wait, not due to Play mode entry). Re-enter Play mode.
        bool trainingAlive = trainingProcess != null && !trainingProcess.HasExited;
        if (trainingAlive)
        {
            Debug.Log("[ML-Train] Resuming auto-Play after script-compile domain reload.");
            SessionState.EraseBool(k_AutoPlayPending);
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
        autoPlay = false;
    }

    // Track whether we already logged the training exit.
    private static bool trainingExitLogged;

    private void OnEditorUpdate()
    {
        // Once Play mode is confirmed running, clear the pending flag.
        if (EditorApplication.isPlaying && SessionState.GetBool(k_AutoPlayPending, false))
            SessionState.EraseBool(k_AutoPlayPending);

        // Monitor training process — log when it exits.
        if (trainingProcess != null && trainingProcess.HasExited && !trainingExitLogged)
        {
            trainingExitLogged = true;
            int exitCode = trainingProcess.ExitCode;
            if (exitCode == 0)
                Debug.Log($"[ML-Train] Training finished successfully (exit code 0). Models saved to results/{runId}/");
            else
                Debug.LogWarning($"[ML-Train] Training process exited with code {exitCode}. Check Console for errors.");
            Repaint();
        }

        if (!autoPlay || !EditorApplication.isPlaying) return;

        if (EditorApplication.timeSinceStartup >= nextAutoPlayTime)
        {
            nextAutoPlayTime = EditorApplication.timeSinceStartup + autoPlayInterval;
            StepGame(1);
            Repaint();
        }
    }

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

        // --- Game Config ---
        DrawSection("Game Config", () =>
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

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(config);

                // Apply live during play.
                if (EditorApplication.isPlaying)
                    Time.timeScale = config.TimeScale;
            }
        });

        // --- Quick Launch ---
        DrawSection("Quick Launch", () =>
        {
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("Reset + Setup + Play", GUILayout.Height(40)))
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
        });

        // --- ML Training ---
        DrawSection("ML Training", () =>
        {
            // Run ID field.
            runId = EditorGUILayout.TextField("Run ID", runId);

            EditorGUILayout.Space(4);

            // Start / Stop training.
            bool processExited = trainingProcess != null && trainingProcess.HasExited;
            if (!cachedTrainingRunning)
            {
                // -- Force (new run) --
                GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
                if (GUILayout.Button("Start Training (new)", GUILayout.Height(35)))
                    StartTraining();
                GUI.backgroundColor = Color.white;

                // -- Resume (continue same run) — only if checkpoint exists --
                GUI.backgroundColor = cachedHasCheckpoint ? new Color(0.5f, 0.8f, 1f) : Color.white;
                GUI.enabled = cachedHasCheckpoint;
                string resumeLabel = cachedHasCheckpoint ? $"Resume Training ({runId})" : $"Resume ({runId}) — no checkpoint";
                if (GUILayout.Button(resumeLabel, GUILayout.Height(28)))
                    StartTraining(TrainingMode.Resume);
                GUI.enabled = true;
                GUI.backgroundColor = Color.white;

                // -- Init from previous trained weights --
                GUI.backgroundColor = cachedHasModel ? new Color(0.8f, 0.6f, 1f) : Color.white;
                GUI.enabled = cachedHasModel;
                string initLabel = cachedHasModel ? $"New run (init from {cachedPrevRunId})" : "New run (init from previous) — no model yet";
                if (GUILayout.Button(initLabel, GUILayout.Height(28)))
                {
                    int next = RunCounter;
                    RunCounter = next + 1;
                    runId = $"run{next}";
                    StartTraining(TrainingMode.InitFrom, cachedPrevRunId);
                }
                GUI.enabled = true;
                GUI.backgroundColor = Color.white;

                if (processExited)
                {
                    int exitCode = trainingProcess.ExitCode;
                    var style = new GUIStyle(EditorStyles.boldLabel)
                        { normal = { textColor = exitCode == 0 ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.4f, 0.3f) } };
                    EditorGUILayout.LabelField(
                        exitCode == 0 ? $"Last run ({runId}): Finished OK" : $"Last run ({runId}): Exited with code {exitCode}",
                        style);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "1. Click 'Start Training'\n2. Wait for 'Listening on port 5004' in Console\n3. Press Play in Unity",
                        MessageType.Info);
                }
            }
            else
            {
                GUI.backgroundColor = new Color(1f, 0.4f, 0.3f);
                if (GUILayout.Button("Stop Training", GUILayout.Height(35)))
                    StopTraining();
                GUI.backgroundColor = Color.white;

                // Blinking indicator so user sees it's alive.
                bool blink = ((int)(EditorApplication.timeSinceStartup * 2)) % 2 == 0;
                string dot = blink ? " ●" : " ○";
                var statusStyle = new GUIStyle(EditorStyles.boldLabel)
                    { normal = { textColor = new Color(0.3f, 1f, 0.3f) } };
                string pid = "";
                try { pid = $" (PID {trainingProcess.Id})"; } catch { }
                EditorGUILayout.LabelField($"Status: Training running{pid}{dot}", statusStyle);

                // Force continuous repaint while training to show blinking.
                Repaint();
            }

            // One-click: Start Training + auto-Play.
            EditorGUILayout.Space(4);
            GUI.enabled = !cachedTrainingRunning;
            GUI.backgroundColor = !cachedTrainingRunning ? new Color(1f, 0.85f, 0.2f) : Color.white;
            if (GUILayout.Button("Train (auto: start + setup + play)", GUILayout.Height(30)))
                StartTrainingAndPlay();
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(8);

            // TensorBoard.
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

        // --- Hex Grid ---
        DrawSection("Hex Grid", () =>
        {
            if (GUILayout.Button("Setup Scene", GUILayout.Height(30)))
                HexGridSetup.SetupScene();

            if (GUILayout.Button("Reset (Delete Prefab + Material)"))
                HexGridSetup.Reset();
        });

        // --- Game ---
        DrawSection("Game", () =>
        {
            if (GUILayout.Button("Step (1x)", GUILayout.Height(25)))
                StepGame(1);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Step 10x"))  StepGame(10);
            if (GUILayout.Button("Step 100x")) StepGame(100);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = autoPlay ? new Color(1f, 0.4f, 0.3f) : new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button(autoPlay ? "Stop Autoplay" : "Autoplay", GUILayout.Height(28)))
            {
                autoPlay = !autoPlay;
                if (autoPlay) nextAutoPlayTime = EditorApplication.timeSinceStartup;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (autoPlay)
            {
                autoPlayInterval = EditorGUILayout.Slider("Interval (s)", autoPlayInterval, 0.01f, 1f);
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Reset Game"))
                ResetGame();
        });

        // --- Debug ---
        DrawSection("Debug", () =>
        {
            if (GUILayout.Button("Randomize Tile Ownership"))
                RandomizeTileOwnership();
        });

        // --- Testing ---
        DrawSection("Testing", () =>
        {
            if (GUILayout.Button("Run EditMode Tests Now", GUILayout.Height(30)))
                AutoTestRunner.RunEditModeTests();

            if (GUILayout.Button("Open Test Runner"))
                EditorApplication.ExecuteMenuItem("Window/General/Test Runner");

            // Safe PlayMode test runner — stops game first if needed.
            if (GUILayout.Button(
                    EditorApplication.isPlaying ? "STOP GAME + Run PlayMode Tests" : "Run PlayMode Tests",
                    GUILayout.Height(32)))
            {
                RunPlayModeTestsSafely();
            }

            if (EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Game is running — use button above, NOT 'Run All' in Test Runner!", MessageType.Error);

            EditorGUILayout.Space(4);
            bool autoTest = AutoTestRunner.Enabled;
            bool newAutoTest = EditorGUILayout.Toggle("Auto-run after compile", autoTest);
            if (newAutoTest != autoTest)
                AutoTestRunner.Enabled = newAutoTest;
        });

        EditorGUILayout.EndScrollView();
    }

    // ─── ML Training ──────────────────────────────────────

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
            Debug.LogError($"[ML-Train] Cannot resume: results/{runId}/ not found. Use 'Start Training (new)' first.");
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
                // Persist "auto-play pending" so a domain reload during Play mode
                // entry doesn't swallow the isPlaying=true call.
                SessionState.SetBool(k_AutoPlayPending, true);
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
        SessionState.EraseBool(k_StartTrainingPending);

        // Kill any orphaned mlagents processes still holding port 5004.
        // This happens when domain reload (script recompile) clears the static
        // trainingProcess reference but the OS process keeps running.
        KillOrphanedTrainers();

        // Auto-load the trained models.
        LoadBestModels(runId);

        // Force window repaint to update button state.
        if (HasOpenInstances<ProjectToolsWindow>())
            GetWindow<ProjectToolsWindow>().Repaint();
    }

    /// <summary>
    /// Find the latest .onnx models from a training run and copy them into
    /// Assets/ML-Agents/ so they can be assigned to agents at runtime.
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

        // Copy into Assets so Unity can import them as NNModel.
        string destDir = "Assets/Resources";
        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        string destRobot  = Path.Combine(destDir, "HexRobot.onnx");
        string destMutant = Path.Combine(destDir, "HexMutant.onnx");
        File.Copy(robotOnnx, destRobot, true);
        File.Copy(mutantOnnx, destMutant, true);
        AssetDatabase.Refresh();

        // Read training steps from status JSON.
        long totalSteps = 0;
        string statusPath = Path.Combine(resultsDir, "run_logs", "training_status.json");
        if (File.Exists(statusPath))
        {
            string json = File.ReadAllText(statusPath);
            // Simple parse: find "steps": NNN
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

        // Save model info to EditorPrefs.
        EditorPrefs.SetString(ModelRunIdKey, fromRunId);
        EditorPrefs.SetInt(ModelStepsKey, (int)totalSteps);

        // Compute training session delta and save to PlayerPrefs for runtime HUD.
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

        // If in Play mode, assign to all agents immediately.
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

    // ─── Game ─────────────────────────────────────────────

    private static void DrawSection(string title, System.Action content)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        content();
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

    private static void StepGame(int count)
    {
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            Debug.LogWarning("[ProjectTools] No GameManager found. Enter Play mode first.");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (gm.gameOver) break;

            // Sequential turn mode: turns advance automatically via FixedUpdate.
            // Manual step not applicable; ResetGame is available below.
        }
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

    /// <summary>
    /// Run PlayMode tests safely: stops the game first if needed, then executes tests via API.
    /// Direct "Run All" in Test Runner window fails when game is playing because
    /// EditorSceneManager.GetSceneManagerSetup() cannot be called during play mode.
    /// </summary>
    private static void RunPlayModeTestsSafely()
    {
        EditorApplication.ExecuteMenuItem("Window/General/Test Runner");

        if (EditorApplication.isPlaying)
        {
            Debug.Log("[ProjectTools] Stopping game — click 'Run All' in Test Runner once Unity exits Play mode.");
            EditorApplication.isPlaying = false;
        }
        // User clicks Run All in Test Runner after game has stopped.
        // Direct automation via TestRunnerApi causes NullRef in PlayModeRunTask (Unity bug).
    }
}
