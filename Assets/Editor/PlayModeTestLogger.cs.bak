using UnityEngine;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using System.IO;
using System.Text;

/// <summary>
/// Logs PlayMode test results to a file so they can be read externally.
/// Registers automatically on domain reload.
/// </summary>
[InitializeOnLoad]
public static class PlayModeTestLogger
{
    private const string ResultsPath = "Assets/../playmode-test-results.txt";

    static PlayModeTestLogger()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Callbacks());
    }

    private class Callbacks : ICallbacks
    {
        private readonly StringBuilder sb = new();
        private int passed, failed;

        public void RunStarted(ITestAdaptor testsToRun)
        {
            sb.Clear();
            passed = 0;
            failed = 0;
            sb.AppendLine("=== PlayMode Test Results ===");
        }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.HasChildren) return;
            if (result.TestStatus == TestStatus.Passed)
            {
                passed++;
                sb.AppendLine($"PASS: {result.FullName}");
            }
            else if (result.TestStatus == TestStatus.Failed)
            {
                failed++;
                sb.AppendLine($"FAIL: {result.FullName}");
                sb.AppendLine($"  Message: {result.Message}");
                if (!string.IsNullOrEmpty(result.StackTrace))
                    sb.AppendLine($"  Stack: {result.StackTrace.Split('\n')[0]}");
            }
            else
            {
                sb.AppendLine($"SKIP: {result.FullName}");
            }
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            sb.AppendLine($"\n=== Summary: {passed} passed, {failed} failed ===");
            File.WriteAllText(ResultsPath, sb.ToString());
            Debug.Log($"[PlayModeTestLogger] Results written to {Path.GetFullPath(ResultsPath)}");
        }
    }
}
