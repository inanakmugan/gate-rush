using System;
using System.Collections.Generic;
using System.Linq;
using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="ThirdPartyFontMove"/>'s pure logic (7.8): the step
    /// each file takes from which of its paths exist, the refusal that keeps
    /// a run from leaving the font half moved, a second run doing nothing,
    /// and the move list itself.
    /// </summary>
    public class ThirdPartyFontMoveTests
    {
        private static readonly ThirdPartyFontMove.FontMove Move =
            new ThirdPartyFontMove.FontMove("Assets/Art/Fonts/a.ttf", "Assets/ThirdParty/Fonts/A/a.ttf");

        [Test]
        public void StepFor_OnlyTheSource_IsMove()
        {
            var step = ThirdPartyFontMove.StepFor(Move, Exists(Move.From));

            Assert.AreEqual(ThirdPartyFontMove.MoveStep.Move, step);
        }

        [Test]
        public void StepFor_OnlyTheTarget_IsAlreadyMoved()
        {
            var step = ThirdPartyFontMove.StepFor(Move, Exists(Move.To));

            Assert.AreEqual(ThirdPartyFontMove.MoveStep.AlreadyMoved, step);
        }

        [Test]
        public void StepFor_Both_IsConflict()
        {
            var step = ThirdPartyFontMove.StepFor(Move, Exists(Move.From, Move.To));

            Assert.AreEqual(ThirdPartyFontMove.MoveStep.Conflict, step);
        }

        [Test]
        public void StepFor_Neither_IsMissing()
        {
            var step = ThirdPartyFontMove.StepFor(Move, Exists());

            Assert.AreEqual(ThirdPartyFontMove.MoveStep.Missing, step);
        }

        [Test]
        public void Refusal_MovableOrAlreadyMovedFiles_IsNull()
        {
            var other = new ThirdPartyFontMove.FontMove("Assets/Art/Fonts/b.asset", "Assets/ThirdParty/Fonts/A/b.asset");

            var refusal = ThirdPartyFontMove.Refusal(new[] { Move, other }, Exists(Move.From, other.To));

            Assert.IsNull(refusal);
        }

        [Test]
        public void Refusal_AConflictAndAMissingFile_NamesBoth()
        {
            var missing = new ThirdPartyFontMove.FontMove("Assets/Art/Fonts/b.asset", "Assets/ThirdParty/Fonts/A/b.asset");

            var refusal = ThirdPartyFontMove.Refusal(new[] { Move, missing }, Exists(Move.From, Move.To));

            Assert.IsNotNull(refusal);
            StringAssert.Contains(Move.To, refusal);
            StringAssert.Contains(missing.From, refusal);
        }

        [Test]
        public void Moves_AfterEveryFileIsMoved_ASecondRunMovesNothing()
        {
            var present = new HashSet<string>(ThirdPartyFontMove.Moves.Select(move => move.From));
            foreach (var move in ThirdPartyFontMove.Moves)
            {
                present.Remove(move.From);
                present.Add(move.To);
            }

            var steps = ThirdPartyFontMove.Moves.Select(move => ThirdPartyFontMove.StepFor(move, present.Contains)).ToList();

            Assert.IsNull(ThirdPartyFontMove.Refusal(ThirdPartyFontMove.Moves, present.Contains));
            Assert.That(steps, Is.All.EqualTo(ThirdPartyFontMove.MoveStep.AlreadyMoved));
        }

        [Test]
        public void Moves_EachFile_KeepsItsNameFromTheSourceToTheTargetFolder()
        {
            var moves = ThirdPartyFontMove.Moves;

            Assert.AreEqual(3, moves.Count, "the TrueType file and its two font assets");
            foreach (var move in moves)
            {
                Assert.AreEqual(ThirdPartyFontMove.SourceFolder, Folder(move.From), move.From);
                Assert.AreEqual(ThirdPartyFontMove.TargetFolder, Folder(move.To), move.To);
                Assert.AreEqual(Name(move.From), Name(move.To));
                StringAssert.StartsWith("LilitaOne-Regular", Name(move.From));
            }
        }

        private static Func<string, bool> Exists(params string[] paths) => new HashSet<string>(paths).Contains;

        private static string Folder(string path) => path.Substring(0, path.LastIndexOf('/'));

        private static string Name(string path) => path.Substring(path.LastIndexOf('/') + 1);
    }
}
