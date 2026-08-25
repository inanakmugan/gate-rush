using System;

namespace GateRush.Core
{
    /// <summary>
    /// An integer grid coordinate. Origin is bottom-left: +X is right, +Y is up.
    /// Defined locally because <c>GateRush.Core</c> may not reference
    /// <c>UnityEngine</c> (see <c>noEngineReferences</c> in its assembly definition).
    /// </summary>
    public readonly struct Coord : IEquatable<Coord>
    {
        public int X { get; }
        public int Y { get; }

        public Coord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Coord operator +(Coord a, Coord b) => new Coord(a.X + b.X, a.Y + b.Y);

        public static Coord operator -(Coord a, Coord b) => new Coord(a.X - b.X, a.Y - b.Y);

        public static bool operator ==(Coord a, Coord b) => a.Equals(b);

        public static bool operator !=(Coord a, Coord b) => !a.Equals(b);

        public bool Equals(Coord other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is Coord other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ Y;

        public override string ToString() => $"({X}, {Y})";
    }
}
