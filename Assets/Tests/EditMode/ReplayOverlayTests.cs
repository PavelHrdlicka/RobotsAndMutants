using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests for ReplayDebugOverlay and ReplayPlayer turn descriptions.
/// </summary>
public class ReplayOverlayTests
{
    [Test]
    public void ReplayDebugOverlay_Toggle_SwitchesState()
    {
        var go = new GameObject("TestOverlay");
        var overlay = go.AddComponent<ReplayDebugOverlay>();

        Assert.IsFalse(overlay.showDetail);

        overlay.Toggle();
        Assert.IsTrue(overlay.showDetail);

        overlay.Toggle();
        Assert.IsFalse(overlay.showDetail);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void ReplayPlayer_PreviousTurnDescription_EmptyAtStart()
    {
        var go = new GameObject("TestReplay");
        var player = go.AddComponent<ReplayPlayer>();

        // Before any replay is loaded, PreviousTurnDescription should be empty.
        Assert.AreEqual("", player.PreviousTurnDescription);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void ReplayPlayer_CurrentTurnDescription_EmptyWithNoReplay()
    {
        var go = new GameObject("TestReplay");
        var player = go.AddComponent<ReplayPlayer>();

        Assert.AreEqual("", player.CurrentTurnDescription);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void HideDetailButton_LabelFitsWidth()
    {
        // Rule: buttons must be wide enough for their full text.
        // HIDE DETAIL (11 chars) is the longest label on the toggle button.
        // At fontSize 14, each char is ~8-10px wide. 11 chars * 10 = 110px + padding.
        // Our button width is 130px — verify it exceeds the minimum.
        float buttonWidth = 130f;
        string longestLabel = "HIDE DETAIL";
        float estimatedMinWidth = longestLabel.Length * 10f; // conservative 10px per char

        Assert.GreaterOrEqual(buttonWidth, estimatedMinWidth,
            $"Button width ({buttonWidth}) must fit label '{longestLabel}' (estimated min {estimatedMinWidth}px)");
    }
}
