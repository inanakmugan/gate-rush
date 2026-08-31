namespace GateRush.Editor
{
    /// <summary>
    /// A window dimension that scales with the window instead of staying fixed:
    /// a ratio of the window's own size, floored so it stays usable when the
    /// window is small (docs/Modules/09a follow-up, item 4). Used for the
    /// properties column's width and the warnings list's height. Mirrors
    /// <see cref="TimeBudgetFormula"/> — a small tunable-holding type with one
    /// method, no <c>UnityEditor</c>/<c>UnityEngine</c> in its signature — so the
    /// arithmetic the window used to guess at is testable on its own.
    /// </summary>
    public readonly struct ProportionalSize
    {
        public float Ratio { get; }
        public float Floor { get; }

        public ProportionalSize(float ratio, float floor)
        {
            Ratio = ratio;
            Floor = floor;
        }

        /// <summary>
        /// The resolved dimension for a window measuring
        /// <paramref name="windowDimension"/> along this axis: <see cref="Ratio"/>
        /// of it, or <see cref="Floor"/>, whichever is larger. The floor can
        /// exceed the window itself on a small enough window; that is returned
        /// as-is rather than clamped further — this type only answers "how big
        /// should it be", not "does it fit".
        /// </summary>
        public float Resolve(float windowDimension)
        {
            var scaled = windowDimension * Ratio;
            return scaled > Floor ? scaled : Floor;
        }
    }
}
