using NUnit.Framework;

/// <summary>
/// Tests for base placement on the hex board.
/// Uses HexCoord logic only (no GameObjects needed).
/// Parametrized to work with any board size.
/// </summary>
public class HexBaseTests
{
    private static readonly int[] BoardSizes = { 3, 5, 7, 10 };

    [Test]
    public void RobotBaseCorner_IsInsideBoard([ValueSource(nameof(BoardSizes))] int side)
    {
        int max = side - 1;
        var corner = new HexCoord(-max, max);
        Assert.IsTrue(corner.IsInsideHexBoard(side), $"Robot base corner should be inside board (side={side}).");
    }

    [Test]
    public void MutantBaseCorner_IsInsideBoard([ValueSource(nameof(BoardSizes))] int side)
    {
        int max = side - 1;
        var corner = new HexCoord(max, -max);
        Assert.IsTrue(corner.IsInsideHexBoard(side), $"Mutant base corner should be inside board (side={side}).");
    }

    [Test]
    public void RobotBaseCorner_Has3NeighborsInsideBoard([ValueSource(nameof(BoardSizes))] int side)
    {
        int max = side - 1;
        var corner = new HexCoord(-max, max);

        int insideCount = 0;
        for (int i = 0; i < 6; i++)
        {
            if (corner.Neighbor(i).IsInsideHexBoard(side))
                insideCount++;
        }

        Assert.AreEqual(3, insideCount, $"Corner hex should have 3 neighbors inside the board (side={side}).");
    }

    [Test]
    public void BasesAreOnOppositeCorners([ValueSource(nameof(BoardSizes))] int side)
    {
        int max = side - 1;
        var robotBase = new HexCoord(-max, max);
        var mutantBase = new HexCoord(max, -max);

        int distance = HexCoord.Distance(robotBase, mutantBase);
        Assert.AreEqual(2 * max, distance, $"Bases should be at maximum distance across the board (side={side}).");
    }

    [Test]
    public void BaseCluster_Has4Tiles([ValueSource(nameof(BoardSizes))] int side)
    {
        int max = side - 1;
        var corner = new HexCoord(-max, max);

        int count = 1;
        for (int i = 0; i < 6; i++)
        {
            if (corner.Neighbor(i).IsInsideHexBoard(side))
                count++;
        }
        Assert.AreEqual(4, count, $"Base cluster should have 4 tiles (side={side}).");
    }

    [Test]
    public void TileCount_IsCorrect([ValueSource(nameof(BoardSizes))] int side)
    {
        int expected = 3 * side * side - 3 * side + 1;
        Assert.AreEqual(expected, HexGrid.TileCount(side), $"Tile count formula should match (side={side}).");
    }
}
