using System;

/// <summary>
/// Axial hex coordinate (q, r). The third cube coordinate s = -q - r is derived.
/// Used for all hex grid logic (neighbors, distance, validation).
/// </summary>
[Serializable]
public struct HexCoord : IEquatable<HexCoord>
{
    public int q;
    public int r;

    public int S => -q - r;

    public HexCoord(int q, int r)
    {
        this.q = q;
        this.r = r;
    }

    /// <summary>
    /// The six neighbor directions in axial coordinates (flat-top hex).
    /// Order: E, NE, NW, W, SW, SE
    /// </summary>
    public static readonly HexCoord[] Directions = new HexCoord[]
    {
        new HexCoord(+1,  0), // E
        new HexCoord(+1, -1), // NE
        new HexCoord( 0, -1), // NW
        new HexCoord(-1,  0), // W
        new HexCoord(-1, +1), // SW
        new HexCoord( 0, +1), // SE
    };

    public HexCoord Neighbor(int direction)
    {
        var d = Directions[direction];
        return new HexCoord(q + d.q, r + d.r);
    }

    /// <summary>
    /// Hex distance (cube/axial) between two coordinates.
    /// </summary>
    public static int Distance(HexCoord a, HexCoord b)
    {
        int dq = Math.Abs(a.q - b.q);
        int dr = Math.Abs(a.r - b.r);
        int ds = Math.Abs(a.S - b.S);
        return Math.Max(dq, Math.Max(dr, ds));
    }

    /// <summary>
    /// Returns true if this coordinate lies within a hex-shaped board of given side length.
    /// Side N means max(|q|, |r|, |s|) &lt;= N - 1.
    /// </summary>
    public bool IsInsideHexBoard(int side)
    {
        int maxCoord = side - 1;
        return Math.Abs(q) <= maxCoord
            && Math.Abs(r) <= maxCoord
            && Math.Abs(S) <= maxCoord;
    }

    public bool Equals(HexCoord other) => q == other.q && r == other.r;
    public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
    public override int GetHashCode() => q * 397 ^ r;
    public override string ToString() => $"({q},{r})";

    public static bool operator ==(HexCoord a, HexCoord b) => a.q == b.q && a.r == b.r;
    public static bool operator !=(HexCoord a, HexCoord b) => !(a == b);
}
