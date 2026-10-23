using System.Linq;
using GateRush.UI.Editor;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers the hand-made prefabs after <see cref="HandMadePrefabSettings"/>
    /// has been applied: Button Round's icon sits in the glyph box with no
    /// offsets; Top Bar's plus icons carry no RectTransform override of their
    /// own and follow it; the lives count takes the HUD's font size; the
    /// heart is 86 square with Preserve Aspect and the count stretched in it;
    /// the avatar fills its 120-unit frame inside a thin rim with no Layout
    /// Element of its own; the Name Tag stays centred on the frame's bottom
    /// edge; the selected tabs' labels share the largest fixed size at which
    /// "Leaderboard" fits its tile; and Home's Play button sits just above
    /// the tab bar, its size kept, with the notice just above it.
    /// </summary>
    /// <remarks>
    /// These read the project's real prefabs, so they fail until the owner
    /// runs <i>Gate Rush → Apply Hand-Made Prefab Settings</i> after a value
    /// here changes.
    /// </remarks>
    public class HandMadePrefabSettingsTests
    {
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 BottomMiddle = new Vector2(0.5f, 0f);

        // Home's Play button keeps the size it was laid out at by hand.
        private static readonly Vector2 PlayButtonSize = new Vector2(620f, 190f);

        // The tab labels' font is searched in whole sizes.
        private const float FontSizeStep = 1f;

        private GameObject buttonRound;
        private GameObject topBar;

        [OneTimeSetUp]
        public void LoadPrefabs()
        {
            buttonRound = AssetDatabase.LoadAssetAtPath<GameObject>(HandMadePrefabSettings.ButtonRoundPath);
            topBar = AssetDatabase.LoadAssetAtPath<GameObject>(HandMadePrefabSettings.TopBarPath);
            Assert.IsNotNull(buttonRound, HandMadePrefabSettings.ButtonRoundPath);
            Assert.IsNotNull(topBar, HandMadePrefabSettings.TopBarPath);
        }

        [Test]
        public void ButtonRound_Icon_IsAnchoredToTheGlyphBoxWithNoOffsets()
        {
            var icon = Rect(buttonRound, HandMadePrefabSettings.ButtonRoundIconPath);

            AssertGlyphBox(icon, HandMadePrefabSettings.ButtonRoundIconPath);
        }

        [TestCase(HandMadePrefabSettings.GoldPlusIconPath)]
        [TestCase(HandMadePrefabSettings.LivesPlusIconPath)]
        public void TopBar_PlusIcon_HasNoRectOverrideAndFollowsButtonRound(string path)
        {
            // Loaded as contents, as the command edits it, so the nested
            // Button Round instance reports its overrides.
            var contents = PrefabUtility.LoadPrefabContents(HandMadePrefabSettings.TopBarPath);
            try
            {
                var icon = Rect(contents, path);
                var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(icon);

                var overridden = PrefabUtility.GetObjectOverrides(instanceRoot).Any(objectOverride => objectOverride.instanceObject == icon);

                Assert.IsFalse(overridden, $"{path} carries RectTransform overrides of its own");
                AssertGlyphBox(icon, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [Test]
        public void TopBar_LivesCount_IsTheHudsFontSize()
        {
            var count = Rect(topBar, HandMadePrefabSettings.LivesCountPath).GetComponent<TMP_Text>();

            Assert.IsNotNull(count, HandMadePrefabSettings.LivesCountPath);
            Assert.AreEqual(UiPrefabBuilder.LivesCountFontSize, count.fontSize, "the Level HUD's lives count size");
        }

        [Test]
        public void TopBar_Heart_IsEightySixSquareWithPreserveAspectAndTheCountInside()
        {
            var heart = Rect(topBar, HandMadePrefabSettings.HeartPath);
            var layout = heart.GetComponent<LayoutElement>();
            var image = heart.GetComponent<Image>();
            var count = Rect(topBar, HandMadePrefabSettings.LivesCountPath);

            Assert.IsNotNull(layout, HandMadePrefabSettings.HeartPath);
            Assert.AreEqual(HandMadePrefabSettings.TopBarHeartSize, layout.preferredWidth);
            Assert.AreEqual(HandMadePrefabSettings.TopBarHeartSize, layout.preferredHeight);
            Assert.IsTrue(image.preserveAspect, "Preserve Aspect stays on");
            Assert.AreEqual(heart, count.parent, "the count is in the heart");
            Assert.AreEqual(Vector2.zero, count.anchorMin, "stretched in the heart");
            Assert.AreEqual(Vector2.one, count.anchorMax, "stretched in the heart");
        }

        [Test]
        public void TopBar_Avatar_FillsTheFrameInsideItsRim()
        {
            var frameLayout = Rect(topBar, HandMadePrefabSettings.AvatarFramePath).GetComponent<LayoutElement>();
            var avatar = Rect(topBar, HandMadePrefabSettings.AvatarPath);

            Assert.IsNotNull(frameLayout, HandMadePrefabSettings.AvatarFramePath);
            Assert.AreEqual(HandMadePrefabSettings.AvatarFrameSize, frameLayout.preferredWidth);
            Assert.AreEqual(HandMadePrefabSettings.AvatarFrameSize, frameLayout.preferredHeight);
            Assert.AreEqual(HandMadePrefabSettings.AvatarAnchorMin, avatar.anchorMin);
            Assert.AreEqual(HandMadePrefabSettings.AvatarAnchorMax, avatar.anchorMax);
            Assert.AreEqual(Vector2.zero, avatar.anchoredPosition, "no offset");
            Assert.AreEqual(Vector2.zero, avatar.sizeDelta, "no offset");
            Assert.IsNull(avatar.GetComponent<LayoutElement>(), "the anchors size the avatar; no Layout Element");
        }

        [Test]
        public void TopBar_NameTag_StaysCentredOnTheFramesBottomEdge()
        {
            var tag = Rect(topBar, HandMadePrefabSettings.NameTagPath);

            Assert.AreEqual(Rect(topBar, HandMadePrefabSettings.AvatarFramePath), tag.parent);
            Assert.AreEqual(BottomMiddle, tag.anchorMin);
            Assert.AreEqual(BottomMiddle, tag.anchorMax);
            Assert.AreEqual(Centre, tag.pivot, "its middle on the edge");
            Assert.AreEqual(Vector2.zero, tag.anchoredPosition);
        }

        [Test]
        public void TabBar_Labels_ShareTheLargestFontThatFitsLeaderboard()
        {
            var contents = PrefabUtility.LoadPrefabContents(HandMadePrefabSettings.TabBarPath);
            try
            {
                var labels = HandMadePrefabSettings.TabLabelPaths.Select(path => Rect(contents, path).GetComponent<TMP_Text>()).ToList();
                var longest = labels.Last();
                var tile = (RectTransform)longest.transform.parent;
                var room = HandMadePrefabSettings.TabLabelRoom(tile.sizeDelta.x);
                var size = longest.fontSize;

                foreach (var label in labels)
                {
                    Assert.IsFalse(label.enableAutoSizing, $"{label.name}: one fixed size");
                    Assert.AreEqual(size, label.fontSize, $"{label.text}: the same size as Leaderboard");
                    Assert.AreEqual(TextWrappingModes.NoWrap, label.textWrappingMode, $"{label.text}: one line");
                    Assert.AreEqual(room - tile.sizeDelta.x, ((RectTransform)label.transform).sizeDelta.x, $"{label.text}: inset to the face less the margin");
                }

                var width = longest.GetPreferredValues(longest.text).x;
                longest.fontSize = size + FontSizeStep;
                var widthOneLarger = longest.GetPreferredValues(longest.text).x;

                Assert.That(width, Is.LessThanOrEqualTo(room), "Leaderboard fits its tile's face with the margin");
                Assert.That(
                    size >= HandMadePrefabSettings.TabLabelMaxFontSize || widthOneLarger > room, "the largest such size, up to the cap");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [Test]
        public void HomePage_PlayButton_SitsJustAboveTheTabBarWithTheNoticeAboveIt()
        {
            var homePage = AssetDatabase.LoadAssetAtPath<GameObject>(HandMadePrefabSettings.HomePagePath);
            Assert.IsNotNull(homePage, HandMadePrefabSettings.HomePagePath);
            var play = Rect(homePage, HandMadePrefabSettings.PlayButtonPath);
            var notice = Rect(homePage, HandMadePrefabSettings.NoticePath);
            var bottom = HandMadePrefabSettings.BottomCentre;

            Assert.AreEqual(bottom, play.anchorMin, "pinned to the page's foot, which is the tab bar's top");
            Assert.AreEqual(bottom, play.anchorMax);
            Assert.AreEqual(bottom, play.pivot);
            Assert.AreEqual(new Vector2(0f, HandMadePrefabSettings.PlayButtonBottomGap), play.anchoredPosition, "a gap above the tab bar");
            Assert.AreEqual(PlayButtonSize, play.sizeDelta, "its size kept");
            Assert.AreEqual(bottom, notice.anchorMin);
            Assert.AreEqual(bottom, notice.anchorMax);
            Assert.AreEqual(bottom, notice.pivot);
            Assert.AreEqual(
                HandMadePrefabSettings.PlayButtonBottomGap + play.sizeDelta.y + HandMadePrefabSettings.NoticeGap, notice.anchoredPosition.y,
                "the notice just above the button");
            Assert.AreEqual(0f, notice.anchoredPosition.x);
        }

        private static RectTransform Rect(GameObject root, string path)
        {
            var found = HandMadePrefabSettings.FindByPath(root, path) as RectTransform;
            Assert.IsNotNull(found, path);
            return found;
        }

        private static void AssertGlyphBox(RectTransform icon, string path)
        {
            Assert.AreEqual(HandMadePrefabSettings.GlyphAnchorMin, icon.anchorMin, path);
            Assert.AreEqual(HandMadePrefabSettings.GlyphAnchorMax, icon.anchorMax, path);
            Assert.AreEqual(Vector2.zero, icon.anchoredPosition, $"{path}: no offset");
            Assert.AreEqual(Vector2.zero, icon.sizeDelta, $"{path}: no offset");
        }
    }
}
