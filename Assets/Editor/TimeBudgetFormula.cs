namespace GateRush.Editor
{
    /// <summary>
    /// Turns a solved level's shortest solution length into a suggested
    /// countdown, replacing a feel-based guess with a measured one (D12). The
    /// window still shows it only as a suggestion:
    /// <see cref="LevelDraft.SuggestedTimeBudgetSeconds"/> stays hand-editable.
    /// </summary>
    /// <remarks>
    /// <c>base + perMove * moves</c>, rounded up to the nearest
    /// <see cref="Rounding"/>. The three tunables live on
    /// <see cref="LevelEditorSettings"/> so no number is fixed at a call site.
    /// The level's M10 time bonuses are deliberately left out (D54): every
    /// winning play destroys every block and so collects every bonus, and
    /// adding them here as well would hand the player the same seconds twice.
    /// The window shows their total beside the suggestion instead
    /// (<see cref="DraftMetrics.AvailableTimeBonusSeconds"/>).
    /// </remarks>
    public readonly struct TimeBudgetFormula
    {
        public int Base { get; }
        public int PerMove { get; }
        public int Rounding { get; }

        public TimeBudgetFormula(int baseSeconds, int perMove, int rounding)
        {
            Base = baseSeconds;
            PerMove = perMove;
            Rounding = rounding < 1 ? 1 : rounding;
        }

        public int Suggest(int solutionMoves)
        {
            var raw = Base + (PerMove * solutionMoves);
            if (raw < 0)
            {
                raw = 0;
            }

            return ((raw + Rounding - 1) / Rounding) * Rounding;
        }
    }
}
