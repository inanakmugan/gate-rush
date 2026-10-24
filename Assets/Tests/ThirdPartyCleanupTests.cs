using System;
using System.Collections.Generic;
using System.Linq;
using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="ThirdPartyCleanup"/>'s pure logic (7.8): which
    /// approved files a run deletes, refuses or skips, that a second run
    /// deletes nothing, which folders the deletions leave empty, and the shape
    /// of the approved list. The fixtures use made-up paths, so no test
    /// source writes an approved path out and refuses it in a real run.
    /// </summary>
    public class ThirdPartyCleanupTests
    {
        private const string Root = "Assets/ThirdParty";
        private const string PackFolder = Root + "/Pack";
        private const string Unused = PackFolder + "/unused.png";
        private const string Used = PackFolder + "/used.png";
        private const string Gone = PackFolder + "/gone.png";

        private static readonly IReadOnlyList<KeyValuePair<string, string>> NoSources = new KeyValuePair<string, string>[0];

        [Test]
        public void PlanDeletions_FileNothingUses_IsDeleted()
        {
            var plan = ThirdPartyCleanup.PlanDeletions(new[] { Unused }, new HashSet<string>(), NoSources, Exists(Unused));

            CollectionAssert.AreEqual(new[] { Unused }, plan.ToDelete);
            Assert.IsEmpty(plan.Refused);
            Assert.IsEmpty(plan.AlreadyGone);
        }

        [Test]
        public void PlanDeletions_FileAnAssetUses_IsRefusedAsUsedByAnAsset()
        {
            var plan = ThirdPartyCleanup.PlanDeletions(new[] { Used, Unused }, new HashSet<string> { Used }, NoSources, Exists(Used, Unused));

            CollectionAssert.AreEqual(new[] { Unused }, plan.ToDelete);
            Assert.AreEqual(1, plan.Refused.Count);
            Assert.AreEqual(Used, plan.Refused[0].Path);
            Assert.IsTrue(plan.Refused[0].IsUsedByAsset);
        }

        [Test]
        public void PlanDeletions_FileACodeSourceWritesOut_IsRefusedNamingTheSource()
        {
            const string source = "Assets/Editor/Loader.cs";
            var sources = new[] { new KeyValuePair<string, string>(source, $"const string Path = \"{Used}\";") };

            var plan = ThirdPartyCleanup.PlanDeletions(new[] { Used }, new HashSet<string>(), sources, Exists(Used));

            Assert.IsEmpty(plan.ToDelete);
            Assert.AreEqual(1, plan.Refused.Count);
            Assert.IsFalse(plan.Refused[0].IsUsedByAsset);
            Assert.AreEqual(source, plan.Refused[0].Source);
        }

        [Test]
        public void PlanDeletions_FileAlreadyGone_IsSkippedNotRefused()
        {
            var plan = ThirdPartyCleanup.PlanDeletions(new[] { Gone }, new HashSet<string> { Gone }, NoSources, Exists());

            CollectionAssert.AreEqual(new[] { Gone }, plan.AlreadyGone);
            Assert.IsEmpty(plan.ToDelete);
            Assert.IsEmpty(plan.Refused);
        }

        [Test]
        public void PlanDeletions_SecondRunAfterDeleting_DeletesNothing()
        {
            var approved = new[] { Unused, Used, Gone };
            var present = new HashSet<string> { Unused, Used };
            var referenced = new HashSet<string> { Used };
            var first = ThirdPartyCleanup.PlanDeletions(approved, referenced, NoSources, present.Contains);
            present.ExceptWith(first.ToDelete);

            var second = ThirdPartyCleanup.PlanDeletions(approved, referenced, NoSources, present.Contains);

            Assert.IsEmpty(second.ToDelete);
            CollectionAssert.AreEquivalent(new[] { Unused, Gone }, second.AlreadyGone);
            CollectionAssert.AreEqual(first.Refused.Select(refusal => refusal.Path), second.Refused.Select(refusal => refusal.Path));
        }

        [Test]
        public void FoldersLeftEmpty_FolderWithARemainingFile_IsKept()
        {
            var deleted = new[] { $"{PackFolder}/Blue/a.png", $"{PackFolder}/Green/a.png" };
            var remaining = new[] { Root, PackFolder, $"{PackFolder}/Blue", $"{PackFolder}/Green", $"{PackFolder}/Green/kept.png" };

            var empty = ThirdPartyCleanup.FoldersLeftEmpty(deleted, remaining, Root);

            CollectionAssert.AreEqual(new[] { $"{PackFolder}/Blue" }, empty);
        }

        [Test]
        public void FoldersLeftEmpty_ParentEmptiedByItsChildren_FollowsThemDeepestFirst()
        {
            var deleted = new[] { $"{PackFolder}/License.txt", $"{PackFolder}/Round/a.png", $"{PackFolder}/Round/b.png" };
            var remaining = new[] { Root, PackFolder, $"{PackFolder}/Round", $"{Root}/Other/kept.png" };

            var empty = ThirdPartyCleanup.FoldersLeftEmpty(deleted, remaining, Root);

            CollectionAssert.AreEqual(new[] { $"{PackFolder}/Round", PackFolder }, empty);
        }

        [Test]
        public void FoldersLeftEmpty_RootItself_IsNeverIncluded()
        {
            var deleted = new[] { Unused };

            var empty = ThirdPartyCleanup.FoldersLeftEmpty(deleted, new[] { Root, PackFolder }, Root);

            CollectionAssert.AreEqual(new[] { PackFolder }, empty, "the root and what is above it stay");
        }

        [Test]
        public void FoldersLeftEmpty_FolderHoldingAnUntouchedEmptyFolder_IsKept()
        {
            var deleted = new[] { Unused };
            var remaining = new[] { Root, PackFolder, $"{PackFolder}/Empty" };

            var empty = ThirdPartyCleanup.FoldersLeftEmpty(deleted, remaining, Root);

            Assert.IsEmpty(empty, "only folders the deletions emptied are removed");
        }

        [Test]
        public void ApprovedDeletions_AreTheApprovedCountUnderTheRootWithoutDuplicates()
        {
            var approved = ThirdPartyCleanup.ApprovedDeletions;

            Assert.AreEqual(144, approved.Count, "the list the owner approved in 7.8");
            Assert.AreEqual(approved.Count, approved.Distinct(StringComparer.Ordinal).Count(), "no path twice");
            foreach (var path in approved)
            {
                StringAssert.StartsWith(ThirdPartyCleanup.Root + "/", path);
            }
        }

        [Test]
        public void ApprovedDeletions_EveryLicence_BelongsToARemovedFolder()
        {
            // A pack's licence goes only with the whole pack.
            var licences = ThirdPartyCleanup.ApprovedDeletions.Where(path => path.EndsWith("/License.txt", StringComparison.Ordinal)
                || path.EndsWith("/OFL.txt", StringComparison.Ordinal)).ToList();

            Assert.That(licences, Is.Not.Empty);
            foreach (var licence in licences)
            {
                var folder = licence.Substring(0, licence.LastIndexOf('/'));
                CollectionAssert.Contains(ThirdPartyCleanup.RemovedFolders, folder, $"{licence} goes only with its whole pack");
            }
        }

        private static Func<string, bool> Exists(params string[] paths) => new HashSet<string>(paths).Contains;
    }
}
