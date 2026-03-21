using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests for HexVisuals color mapping logic.
/// </summary>
public class HexVisualsTests
{
    [Test]
    public void NeutralTile_ReturnsNeutralColor()
    {
        var color = HexVisuals.GetColorForState(Team.None, TileType.Empty, false, Team.None, 0);
        Assert.AreEqual(new Color(0.55f, 0.55f, 0.50f), color);
    }

    [Test]
    public void RobotOwned_ReturnsBlue()
    {
        var color = HexVisuals.GetColorForState(Team.Robot, TileType.Empty, false, Team.None, 0);
        Assert.AreEqual(new Color(0.30f, 0.50f, 0.85f), color);
    }

    [Test]
    public void MutantOwned_ReturnsGreen()
    {
        var color = HexVisuals.GetColorForState(Team.Mutant, TileType.Empty, false, Team.None, 0);
        Assert.AreEqual(new Color(0.45f, 0.75f, 0.30f), color);
    }

    [Test]
    public void RobotBase_ReturnsSaturatedBlue()
    {
        var color = HexVisuals.GetColorForState(Team.Robot, TileType.Empty, true, Team.Robot, 0);
        Assert.AreEqual(new Color(0.15f, 0.30f, 0.70f), color);
    }

    [Test]
    public void MutantBase_ReturnsSaturatedGreen()
    {
        var color = HexVisuals.GetColorForState(Team.Mutant, TileType.Empty, true, Team.Mutant, 0);
        Assert.AreEqual(new Color(0.25f, 0.55f, 0.15f), color);
    }

    [Test]
    public void WallOnRobotTile_ReturnsWallColor()
    {
        var color = HexVisuals.GetColorForState(Team.Robot, TileType.Wall, false, Team.None, 0);
        Assert.AreEqual(new Color(0.25f, 0.30f, 0.50f), color);
    }

    [Test]
    public void WallHP_BrightensWallColor()
    {
        var baseColor = HexVisuals.GetColorForState(Team.Robot, TileType.Wall, false, Team.None, 0);
        var hpColor   = HexVisuals.GetColorForState(Team.Robot, TileType.Wall, false, Team.None, 3);

        Assert.Greater(hpColor.r, baseColor.r, "Wall with HP should be brighter.");
        Assert.Greater(hpColor.g, baseColor.g, "Wall with HP should be brighter.");
        Assert.Greater(hpColor.b, baseColor.b, "Wall with HP should be brighter.");
    }

    [Test]
    public void SlimeOnMutantTile_ReturnsSlimeColor()
    {
        var color = HexVisuals.GetColorForState(Team.Mutant, TileType.Slime, false, Team.None, 0);
        Assert.AreEqual(new Color(0.35f, 0.85f, 0.20f), color);
    }

    [Test]
    public void BaseOverridesOwnership()
    {
        // Even if owner is Mutant, a Robot base should show Robot base color.
        var color = HexVisuals.GetColorForState(Team.Mutant, TileType.Empty, true, Team.Robot, 0);
        Assert.AreEqual(new Color(0.15f, 0.30f, 0.70f), color);
    }
}
