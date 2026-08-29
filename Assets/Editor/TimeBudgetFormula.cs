namespace GateRush.Editor
{
    /// <summary>
    /// Turns a solved level's shortest solution length and its available M10
    /// time bonuses into a suggested countdown, replacing a feel-based guess
    /// with a measured one (D12). The window still shows it only as a suggestion:
    /// <see cref="LevelDraft.SuggestedTimeBudgetSeconds"/> stays hand-editable.
    /// </summary>
    /// <remarks>
    /// <c>base + perMove * moves + Σ bonuses</c>, rounded up to the nearest
    /// <see cref="Rounding"/>. The three tunables live on
    /// <see cref="LevelEditorSettings"/> so no number is fixed at a call site.
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

        public int Suggest(int solutionMoves, int totalTimeBonusSeconds)
        {
            var raw = Base + (PerMove * solutionMoves) + totalTimeBonusSeconds;
            if (raw < 0)
            {
                raw = 0;
            }

            return ((raw + Rounding - 1) / Rounding) * Rounding;
        }
    }
}
