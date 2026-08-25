namespace GateRush.Core
{
    /// <summary>
    /// A rectangular region that hides and locks its contents until its
    /// threshold is reached (M5).
    /// </summary>
    public sealed class ShutterDefinition
    {
        public int Id { get; }
        public Coord Min { get; }
        public Coord Max { get; }
        public int Threshold { get; }
        public BlockColor? RequiredColor { get; }

        public ShutterDefinition(int id, Coord min, Coord max, int threshold, BlockColor? requiredColor)
        {
            if (min.X > max.X || min.Y > max.Y)
            {
                throw new System.ArgumentException($"Shutter {id} has Min {min} greater than Max {max}.");
            }

            if (threshold < 0)
            {
                throw new System.ArgumentException($"Shutter {id} has a negative Threshold ({threshold}).");
            }

            Id = id;
            Min = min;
            Max = max;
            Threshold = threshold;
            RequiredColor = requiredColor;
        }
    }
}
