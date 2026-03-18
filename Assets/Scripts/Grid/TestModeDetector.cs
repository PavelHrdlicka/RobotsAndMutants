/// <summary>
/// Shared utility: detects whether the code is running inside a Unity test runner.
/// Cached after first call — safe to call every frame.
/// </summary>
public static class TestModeDetector
{
    private static bool? s_testMode;

    public static bool IsTestMode()
    {
        if (!s_testMode.HasValue)
        {
            s_testMode = false;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.FullName.StartsWith("UnityEngine.TestRunner"))
                {
                    s_testMode = true;
                    break;
                }
            }
        }
        return s_testMode.Value;
    }
}
