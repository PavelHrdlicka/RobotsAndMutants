using NUnit.Framework;

/// <summary>
/// Tests for hex movement logic using HexCoord (no Agents assembly dependency needed).
/// Validates neighbor calculation, distance, and board boundary checks.
/// </summary>
public class HexMovementTests
{
    [Test]
    public void MoveToNeighbor_IsDistance1()
    {
        var start = new HexCoord(0, 0);
        for (int dir = 0; dir < 6; dir++)
        {
            var target = start.Neighbor(dir);
            Assert.AreEqual(1, HexCoord.Distance(start, target),
                $"Neighbor in direction {dir} should be distance 1.");
        }
    }

    [Test]
    public void MoveToNonNeighbor_IsDistanceGreaterThan1()
    {
        var start = new HexCoord(0, 0);
        var farAway = new HexCoord(3, -1);
        Assert.Greater(HexCoord.Distance(start, farAway), 1,
            "Non-adjacent hex should have distance > 1.");
    }

    [Test]
    public void MoveOffBoard_TargetIsOutside()
    {
        int side = 10;
        int max = side - 1;

        // Start at edge, try to move further out.
        var edge = new HexCoord(max, 0);
        var outside = edge.Neighbor(0); // E direction, goes to (max+1, 0)
        Assert.IsFalse(outside.IsInsideHexBoard(side),
            "Moving east from the eastern edge should go outside the board.");
    }

    [Test]
    public void MoveOnBoard_TargetIsInside()
    {
        int side = 10;
        var center = new HexCoord(0, 0);
        for (int dir = 0; dir < 6; dir++)
        {
            var target = center.Neighbor(dir);
            Assert.IsTrue(target.IsInsideHexBoard(side),
                $"Moving from center in direction {dir} should stay inside the board.");
        }
    }

    [Test]
    public void HexToWorldAndBack_RoundTrips()
    {
        // Verify the conversion formulas are consistent.
        float outerRadius = 0.5f;
        var coord = new HexCoord(3, -2);

        float x = outerRadius * 1.5f * coord.q;
        float z = outerRadius * (UnityEngine.Mathf.Sqrt(3f) * 0.5f * coord.q
                               + UnityEngine.Mathf.Sqrt(3f) * coord.r);

        // Reverse: q from x
        float qFloat = x / (outerRadius * 1.5f);
        float rFloat = (z - outerRadius * UnityEngine.Mathf.Sqrt(3f) * 0.5f * qFloat)
                     / (outerRadius * UnityEngine.Mathf.Sqrt(3f));

        Assert.AreEqual(coord.q, UnityEngine.Mathf.RoundToInt(qFloat), "q should round-trip.");
        Assert.AreEqual(coord.r, UnityEngine.Mathf.RoundToInt(rFloat), "r should round-trip.");
    }
}
