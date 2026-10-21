using System;
using GateRush.Meta;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 43's <see cref="JokerMarks"/>: nothing is marked at
    /// first; marking marks only that joker; unmarking and clearing take the
    /// marks away; an undefined kind throws.
    /// </summary>
    public class JokerMarksTests
    {
        [Test]
        public void IsMarked_NewMarks_IsFalseForEveryKind([Values] JokerKind kind)
        {
            var marks = new JokerMarks();

            var isMarked = marks.IsMarked(kind);

            Assert.IsFalse(isMarked);
        }

        [Test]
        public void Mark_OneKind_MarksOnlyThatKind([Values] JokerKind kind)
        {
            var marks = new JokerMarks();

            marks.Mark(kind);

            foreach (JokerKind other in Enum.GetValues(typeof(JokerKind)))
            {
                Assert.AreEqual(other == kind, marks.IsMarked(other), $"{other}");
            }
        }

        [Test]
        public void Unmark_MarkedKind_IsNoLongerMarked([Values] JokerKind kind)
        {
            var marks = new JokerMarks();
            marks.Mark(kind);

            marks.Unmark(kind);

            Assert.IsFalse(marks.IsMarked(kind));
        }

        [Test]
        public void Clear_EveryKindMarked_UnmarksThemAll()
        {
            var marks = new JokerMarks();
            foreach (JokerKind kind in Enum.GetValues(typeof(JokerKind)))
            {
                marks.Mark(kind);
            }

            marks.Clear();

            foreach (JokerKind kind in Enum.GetValues(typeof(JokerKind)))
            {
                Assert.IsFalse(marks.IsMarked(kind), $"{kind}");
            }
        }

        [Test]
        public void EveryMember_UndefinedKind_Throws()
        {
            var marks = new JokerMarks();
            var undefined = (JokerKind)99;

            Assert.Throws<ArgumentOutOfRangeException>(() => marks.IsMarked(undefined));
            Assert.Throws<ArgumentOutOfRangeException>(() => marks.Mark(undefined));
            Assert.Throws<ArgumentOutOfRangeException>(() => marks.Unmark(undefined));
        }
    }
}
