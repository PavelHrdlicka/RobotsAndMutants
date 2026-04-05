using NUnit.Framework;

/// <summary>
/// Tests for edge-based base placement on the hex board.
/// Robots spread along left edge (q = -max), Mutants along right edge (q = +max).
/// </summary>
public class HexBaseTests
{
    private static readonly int[] BoardSizes = { 3, 5, 7, 10 };

    [Test]
    public void RobotEdgeBase_AllHexesInsideBoard([ValueSource(nameof(BoardSizes))] int side)
    {
        int max = side - 1;
        int q = -max;
        int rMin = System.Math.Max(-max, -q - max);
        int rMax = System.Math.Min(max, -q + max);

        for (int r = rMin; r <= rMax; r++)
        {
            var coord = new HexCoord(q, r);
            Assert.IsTrue(coord.IsInsideHexBoard(side),
                $"Robot edge hex ({q},{r}) should be inside board (side={side}).");
        }
    }

    [Test]
    public void MutantEdgeBase_AllHexesInsideBoard([ValueSource(nameof(BoardSizes))] int side)
    {
        int max = side - 1;
        int q = max;
        int rMin = System.Math.Max(-max, -q - max);
        int rMax = System.Math.Min(max, -q + max);

        for (int r = rMin; r <= rMax; r++)
        {
            var coord = new HexCoord(q, r);
            Assert.IsTrue(coord.IsInsideHexBoard(side),
                $"Mutant edge hex ({q},{r}) should be inside board (side={side}).");
        }
    }

    [Test]
    public void BasesAreOnOppositeEdges([ValueSource(nameof(BoardSizes))] int side)
    {
        int max = side - 1;
        var robotEdge = new HexCoord(-max, 0);
        var mutantEdge = new HexCoord(max, 0);

        int distance = HexCoord.Distance(robotEdge, mutantEdge);
        Assert.AreEqual(2 * max, distance,
            $"Center of opposite edges should be at maximum q-distance (side={side}).");
    }

    [Test]
    public void EdgeHexCount_MatchesBoardSide([ValueSource(nameof(BoardSizes))] int side)
    {
        int max = side - 1;
        int q = -max;
        int rMin = System.Math.Max(-max, -q - max);
        int rMax = System.Math.Min(max, -q + max);
        int edgeCount = rMax - rMin + 1;

        Assert.AreEqual(side, edgeCount,
            $"Edge at q={q} should have {side} hexes (side={side}).");
    }

    [Test]
    public void RobotAndMutantEdges_DoNotOverlap([ValueSource(nameof(BoardSizes))] int side)
    {
        int max = side - 1;
        Assert.AreNotEqual(-max, max,
            "Robot edge (q=-max) and Mutant edge (q=+max) must differ when side > 1.");
    }

    [Test]
    public void TileCount_IsCorrect([ValueSource(nameof(BoardSizes))] int side)
    {
        int expected = 3 * side * side - 3 * side + 1;
        Assert.AreEqual(expected, HexGrid.TileCount(side), $"Tile count formula should match (side={side}).");
    }
}
