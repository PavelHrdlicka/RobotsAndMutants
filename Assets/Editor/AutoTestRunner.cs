using UnityEngine;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using System.Linq;

/// <summary>
/// Automatically runs all EditMode tests after every script recompilation.
/// Results are logged to Unity Console.
/// Toggle via: Tools > Auto Test Runner
/// </summary>
[InitializeOnLoad]
public static class AutoTestRunner
{
    private const string PrefKey = "AutoTestRunner_Enabled";

    public static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    static AutoTestRunner()
    {
        // Called after every domain reload (= script recompilation).
        if (Enabled && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // Delay to let editor finish loading.
            EditorApplication.delayCall += RunEditModeTests;
        }
    }

    [MenuItem("Tools/Auto Test Runner/Toggle (On/Off)")]
    public static void ToggleAutoTest()
    {
        Enabled = !Enabled;
        Debug.Log($"[AutoTestRunner] Auto-run tests after compile: {(Enabled ? "ON" : "OFF")}");
    }

    [MenuItem("Tools/Auto Test Runner/Run EditMode Tests Now")]
    public static void RunEditModeTests()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var filter = new Filter { testMode = TestMode.EditMode };

        var callbacks = new TestCallbacks();
        api.RegisterCallbacks(callbacks);
        api.Execute(new ExecutionSettings(filter));
    }

    [MenuItem("Tools/Auto Test Runner/Run All Tests Now")]
    public static void RunAllTests()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var callbacks = new AllTestCallbacks();
        api.RegisterCallbacks(callbacks);

        // Run EditMode first, then PlayMode on completion.
        var editFilter = new Filter { testMode = TestMode.EditMode };
        callbacks.onEditDone = () =>
        {
            var api2 = ScriptableObject.CreateInstance<TestRunnerApi>();
            var playCallbacks = new TestCallbacks("PlayMode");
            api2.RegisterCallbacks(playCallbacks);
            var playFilter = new Filter { testMode = TestMode.PlayMode };
            api2.Execute(new ExecutionSettings(playFilter));
        };
        api.Execute(new ExecutionSettings(editFilter));
    }

    private class TestCallbacks : ICallbacks
    {
        private int passed;
        private int failed;
        private string firstFailure;
        private readonly string label;

        public TestCallbacks(string label = null) { this.label = label; }

        public void RunStarted(ITestAdaptor testsToRun) { passed = 0; failed = 0; firstFailure = null; }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.HasChildren)
            {
                if (result.TestStatus == TestStatus.Passed)
                    passed++;
                else if (result.TestStatus == TestStatus.Failed)
                {
                    failed++;
                    if (firstFailure == null)
                        firstFailure = $"{result.Name}: {result.Message}";
                }
            }
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            int total = passed + failed;
            string prefix = label != null ? $"[AutoTest/{label}]" : "[AutoTest]";
            if (failed == 0)
                Debug.Log($"<color=green>{prefix} All {total} tests passed.</color>");
            else
                Debug.LogWarning($"{prefix} {failed}/{total} tests FAILED. First: {firstFailure}");
        }
    }

    private class AllTestCallbacks : ICallbacks
    {
        private int passed;
        private int failed;
        private string firstFailure;
        public System.Action onEditDone;

        public void RunStarted(ITestAdaptor testsToRun) { passed = 0; failed = 0; firstFailure = null; }
        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.HasChildren)
            {
                if (result.TestStatus == TestStatus.Passed)
                    passed++;
                else if (result.TestStatus == TestStatus.Failed)
                {
                    failed++;
                    if (firstFailure == null)
                        firstFailure = $"{result.Name}: {result.Message}";
                }
            }
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            int total = passed + failed;
            if (failed == 0)
                Debug.Log($"<color=green>[AutoTest/EditMode] All {total} tests passed.</color>");
            else
                Debug.LogWarning($"[AutoTest/EditMode] {failed}/{total} tests FAILED. First: {firstFailure}");

            // Chain PlayMode tests.
            if (onEditDone != null)
                EditorApplication.delayCall += () => onEditDone();
        }
    }
}
