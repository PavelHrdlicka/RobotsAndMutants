/// <summary>
/// Shared utility: detects whether the code is running inside a Unity test runner.
/// Cached after first call — safe to call every frame.
/// </summary>
public static class TestModeDetector
{
    private static bool? s_testMode;

    /// <summary>Clear cached value on play-mode entry so test-run cache doesn't leak.</summary>
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => s_testMode = null;

    public static bool IsTestMode()
    {
        if (!s_testMode.HasValue)
        {
            // Check for an active NUnit test context — this is only present
            // when tests are actually executing, not just because the test
            // runner assemblies are loaded in the editor.
            s_testMode = false;
            try
            {
                var ctxType = System.Type.GetType(
                    "NUnit.Framework.TestContext, nunit.framework");
                if (ctxType != null)
                {
                    var curCtxProp = ctxType.GetProperty("CurrentContext",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (curCtxProp != null)
                    {
                        var ctx = curCtxProp.GetValue(null);
                        if (ctx != null)
                        {
                            var testProp = ctx.GetType().GetProperty("Test");
                            var test = testProp?.GetValue(ctx);
                            s_testMode = test != null;
                        }
                    }
                }
            }
            catch
            {
                s_testMode = false;
            }
        }
        return s_testMode.Value;
    }
}
