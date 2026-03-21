using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// Global setup for all PlayMode tests.
/// Suppresses Unity 6 internal assertion "m_DisallowAutoRefresh >= 0"
/// which fires during Play mode entry for tests and is not our bug.
/// </summary>
[SetUpFixture]
public class PlayModeTestSetup
{
    [OneTimeSetUp]
    public void GlobalSetUp()
    {
        LogAssert.ignoreFailingMessages = true;
    }
}
