using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="TimeBudgetFormula.Suggest"/>: the suggestion is
    /// <c>base + perMove * moves</c>, rounded up to the rounding step, and
    /// nothing else — time bonuses are not a parameter (D54).
    /// </summary>
    public class TimeBudgetFormulaTests
    {
        private const int Base = 10;
        private const int PerMove = 3;
        private const int Rounding = 5;

        private static readonly TimeBudgetFormula Formula = new TimeBudgetFormula(Base, PerMove, Rounding);

        [Test]
        public void Suggest_LengthLandingOnTheStep_IsBasePlusPerMoveTimesMoves()
        {
            const int Moves = 5;

            var suggested = Formula.Suggest(Moves);

            Assert.AreEqual(0, (Base + (PerMove * Moves)) % Rounding, "The raw value must sit on the step, or this proves nothing.");
            Assert.AreEqual(Base + (PerMove * Moves), suggested);
        }

        [Test]
        public void Suggest_LengthBetweenSteps_RoundsUpToTheNextStep()
        {
            const int Moves = 2;
            var raw = Base + (PerMove * Moves);

            var suggested = Formula.Suggest(Moves);

            Assert.AreNotEqual(0, raw % Rounding, "The raw value must sit between steps, or this proves nothing.");
            Assert.AreEqual(0, suggested % Rounding);
            Assert.Greater(suggested, raw);
            Assert.Less(suggested - raw, Rounding);
        }

        [Test]
        public void Suggest_LongerSolution_SuggestsMore()
        {
            var shorter = Formula.Suggest(2);
            var longer = Formula.Suggest(8);

            Assert.Greater(longer, shorter);
        }

        [Test]
        public void Suggest_RoundingBelowOne_IsTreatedAsOne()
        {
            const int Moves = 2;
            var unrounded = new TimeBudgetFormula(Base, PerMove, 0);

            var suggested = unrounded.Suggest(Moves);

            Assert.AreEqual(1, unrounded.Rounding);
            Assert.AreEqual(Base + (PerMove * Moves), suggested);
        }

        [Test]
        public void Suggest_NegativeRawValue_IsZero()
        {
            var negative = new TimeBudgetFormula(-20, PerMove, Rounding);

            var suggested = negative.Suggest(1);

            Assert.AreEqual(0, suggested);
        }
    }
}
