using UnityEngine;
using UnityEditor;
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

    // Python processes.
    private static Process trainingProcess;
    private static Process tensorboardProcess;
    private static string runId = "run1";

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
    private const string ModelRunIdKey   = "MLTrain_LoadedModelRunId";
    private const string ModelStepsKey   = "MLTrain_LoadedModelSteps";

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
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        autoPlay = false;
    }

    private void OnEditorUpdate()
    {
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
                    EditorApplication.isPlaying = false;

                EditorApplication.delayCall += () =>
                {
                    HexGridSetup.Reset();
                    HexGridSetup.SetupScene();
                    EditorApplication.isPlaying = true;
                };
            }
            GUI.backgroundColor = Color.white;
        });

        // --- ML Training ---
        DrawSection("ML Training", () =>
        {
            bool trainingRunning = trainingProcess != null && !trainingProcess.HasExited;
            bool tbRunning = tensorboardProcess != null && !tensorboardProcess.HasExited;

            // Run ID field.
            runId = EditorGUILayout.TextField("Run ID", runId);

            EditorGUILayout.Space(4);

            // Start / Stop training.
            if (!trainingRunning)
            {
                GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
                if (GUILayout.Button("Start Training", GUILayout.Height(35)))
                    StartTraining();
                GUI.backgroundColor = Color.white;

                EditorGUILayout.HelpBox(
                    "1. Click 'Start Training'\n2. Wait for 'Listening on port 5004' in Console\n3. Press Play in Unity",
                    MessageType.Info);
            }
            else
            {
                GUI.backgroundColor = new Color(1f, 0.4f, 0.3f);
                if (GUILayout.Button("Stop Training", GUILayout.Height(35)))
                    StopTraining();
                GUI.backgroundColor = Color.white;

                EditorGUILayout.LabelField("Status: Training running", EditorStyles.boldLabel);
            }

            // One-click: Start Training + auto-Play.
            if (!trainingRunning)
            {
                EditorGUILayout.Space(4);
                GUI.backgroundColor = new Color(1f, 0.85f, 0.2f);
                if (GUILayout.Button("Train (auto: start + setup + play)", GUILayout.Height(30)))
                    StartTrainingAndPlay();
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(8);

            // TensorBoard.
            if (!tbRunning)
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

            EditorGUILayout.Space(4);
            bool autoTest = AutoTestRunner.Enabled;
            bool newAutoTest = EditorGUILayout.Toggle("Auto-run after compile", autoTest);
            if (newAutoTest != autoTest)
                AutoTestRunner.Enabled = newAutoTest;
        });

        EditorGUILayout.EndScrollView();
    }

    // ─── ML Training ──────────────────────────────────────

    private static void StartTraining()
    {
        StopTraining();

        var psi = new ProcessStartInfo
        {
            FileName = PythonExe,
            Arguments = $"-m mlagents.trainers.learn \"{ConfigPath}\" --run-id={runId} --force",
            WorkingDirectory = System.IO.Path.GetFullPath("."),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            EnvironmentVariables = { ["PYTHONIOENCODING"] = "utf-8" }
        };

        trainingProcess = new Process { StartInfo = psi };
        trainingProcess.OutputDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                UnityEngine.Debug.Log($"[ML-Train] {e.Data}");
        };
        trainingProcess.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                // Filter deprecation warnings.
                if (e.Data.Contains("UserWarning") || e.Data.Contains("pkg_resources"))
                    return;
                UnityEngine.Debug.Log($"[ML-Train] {e.Data}");
            }
        };

        trainingProcess.Start();
        trainingProcess.BeginOutputReadLine();
        trainingProcess.BeginErrorReadLine();

        UnityEngine.Debug.Log($"[ML-Train] Started training (run-id: {runId}). Wait for 'Listening on port 5004' then press Play.");
    }

    private static void StartTrainingAndPlay()
    {
        // Stop play if running.
        if (EditorApplication.isPlaying)
            EditorApplication.isPlaying = false;

        EditorApplication.delayCall += () =>
        {
            // Increment run counter for unique ID.
            int next = RunCounter;
            RunCounter = next + 1;
            runId = $"run{next}";

            // Reset and setup scene.
            HexGridSetup.Reset();
            HexGridSetup.SetupScene();

            // Start Python training.
            StartTraining();

            // Wait a bit for Python to start listening, then press Play.
            double startTime = EditorApplication.timeSinceStartup;
            EditorApplication.CallbackFunction waitForPython = null;
            waitForPython = () =>
            {
                // Wait 3 seconds for Python to start.
                if (EditorApplication.timeSinceStartup - startTime > 3.0)
                {
                    EditorApplication.update -= waitForPython;
                    EditorApplication.isPlaying = true;
                    UnityEngine.Debug.Log("[ML-Train] Auto-starting Play mode.");
                }
            };
            EditorApplication.update += waitForPython;
        };
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

        // Auto-load the trained models.
        LoadBestModels(runId);
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

        Debug.Log($"[ML-Train] Loaded models from {fromRunId} ({totalSteps:N0} steps). Models at {destDir}/");

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
}
