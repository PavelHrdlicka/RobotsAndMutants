using NUnit.Framework;

/// <summary>
/// Tests for HexCoord: neighbors, distance, board bounds validation.
/// </summary>
public class HexCoordTests
{
    [Test]
    public void CubeCoordinate_S_IsCorrect()
    {
        var coord = new HexCoord(2, -3);
        Assert.AreEqual(1, coord.S); // s = -q - r = -2 + 3 = 1
    }

    [Test]
    public void Neighbor_ReturnsCorrectCoords()
    {
        var center = new HexCoord(0, 0);

        Assert.AreEqual(new HexCoord(1, 0),  center.Neighbor(0));  // E
        Assert.AreEqual(new HexCoord(1, -1), center.Neighbor(1));  // NE
        Assert.AreEqual(new HexCoord(0, -1), center.Neighbor(2));  // NW
        Assert.AreEqual(new HexCoord(-1, 0), center.Neighbor(3));  // W
        Assert.AreEqual(new HexCoord(-1, 1), center.Neighbor(4));  // SW
        Assert.AreEqual(new HexCoord(0, 1),  center.Neighbor(5));  // SE
    }

    [Test]
    public void Distance_AdjacentHexes_IsOne()
    {
        var a = new HexCoord(0, 0);
        for (int i = 0; i < 6; i++)
            Assert.AreEqual(1, HexCoord.Distance(a, a.Neighbor(i)));
    }

    [Test]
    public void Distance_SameHex_IsZero()
    {
        var a = new HexCoord(3, -2);
        Assert.AreEqual(0, HexCoord.Distance(a, a));
    }

    [Test]
    public void Distance_AcrossBoard()
    {
        var a = new HexCoord(-3, 0);
        var b = new HexCoord(3, 0);
        Assert.AreEqual(6, HexCoord.Distance(a, b));
    }

    [Test]
    public void IsInsideHexBoard_Center_IsInside()
    {
        var center = new HexCoord(0, 0);
        Assert.IsTrue(center.IsInsideHexBoard(10));
    }

    [Test]
    public void IsInsideHexBoard_Edge_IsInside()
    {
        // Side 10 → max coord = 9
        var edge = new HexCoord(9, 0);
        Assert.IsTrue(edge.IsInsideHexBoard(10));
    }

    [Test]
    public void IsInsideHexBoard_Outside_IsFalse()
    {
        var outside = new HexCoord(10, 0);
        Assert.IsFalse(outside.IsInsideHexBoard(10));
    }

    [Test]
    public void IsInsideHexBoard_CornerOutside_IsFalse()
    {
        // q=9, r=1 → s = -10, |s|=10 > 9
        var corner = new HexCoord(9, 1);
        Assert.IsFalse(corner.IsInsideHexBoard(10));
    }

    [Test]
    public void Equality_SameCoords_AreEqual()
    {
        var a = new HexCoord(3, -1);
        var b = new HexCoord(3, -1);
        Assert.AreEqual(a, b);
        Assert.IsTrue(a == b);
    }

    [Test]
    public void Equality_DifferentCoords_AreNotEqual()
    {
        var a = new HexCoord(3, -1);
        var b = new HexCoord(3, -2);
        Assert.AreNotEqual(a, b);
        Assert.IsTrue(a != b);
    }

    [Test]
    public void HexBoard_Side10_Has271Tiles()
    {
        // 3*10*10 - 3*10 + 1 = 271
        Assert.AreEqual(271, HexGrid.TileCount(10));
    }
}
