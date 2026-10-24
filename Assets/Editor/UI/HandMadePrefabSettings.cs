using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GateRush.UI.Editor
{
    /// <summary>
    /// <i>Gate Rush → Apply Hand-Made Prefab Settings</i>: the one home of the
    /// values of the hand-made prefabs, which <see cref="UiPrefabBuilder"/>
    /// never writes. Each value is a named constant here, applied by a step
    /// of its prefab; a new hand-made change is added here as a constant and
    /// a step, never as a new one-shot script. Safe to run at any time.
    /// </summary>
    /// <remarks>
    /// <para>So far: Button Round's icon sits in a smaller, centred glyph box;
    /// in Top Bar the plus buttons' icons follow it, the lives count takes
    /// the HUD's font size, the heart sits close to the coin's size and the
    /// avatar fills its frame inside a thin rim (Module 45 follow-ups), and
    /// the gold has a canvas and a raycaster of its own (Module 47); in
    /// Tab Bar the selected tabs' labels share the largest size at which
    /// "Leaderboard" fits its tile; in Home Page the Play button sits just
    /// above the tab bar with the notice above it (the interface review).
    /// Button Round and Top Bar run first, then Tab Bar and Home Page, which
    /// depend on neither.</para>
    /// <para>Every object is found by its exact Hierarchy path. Each value is
    /// compared before it is set, so a run over prefabs that already hold
    /// every value changes nothing and saves nothing; a prefab is saved only
    /// when something changed. A missing object or component refuses that
    /// prefab, and nothing of it is saved; Top Bar is refused too when Button
    /// Round was, since its plus icons follow Button Round.</para>
    /// <para>The glyph box: the Kenney glyphs are drawn about 2 percent right
    /// of and below their file's centre, and a glyph centred on the round
    /// sprite's raised face reads as too high, so the box puts the glyph's
    /// centre at about (0.50, 0.505) of the button, filling about 48 percent
    /// of it.</para>
    /// </remarks>
    public static class HandMadePrefabSettings
    {
        // Button Round.

        /// <summary>Button Round's icon, anchored here with no offsets, so it scales with any button.</summary>
        public static readonly Vector2 GlyphAnchorMin = new Vector2(0.11f, 0.145f);

        /// <inheritdoc cref="GlyphAnchorMin"/>
        public static readonly Vector2 GlyphAnchorMax = new Vector2(0.86f, 0.895f);

        // Top Bar.

        /// <summary>The Top Bar heart's preferred width and height, closer to the gold's coin beside it.</summary>
        public const float TopBarHeartSize = 86f;

        /// <summary>The Top Bar avatar frame's preferred width and height, beside the 90-unit pills and the 110 settings button.</summary>
        public const float AvatarFrameSize = 120f;

        /// <summary>The avatar's anchors in its frame: it fills about 88 percent, so the frame shows as a thin rim.</summary>
        public static readonly Vector2 AvatarAnchorMin = new Vector2(0.06f, 0.06f);

        /// <inheritdoc cref="AvatarAnchorMin"/>
        public static readonly Vector2 AvatarAnchorMax = new Vector2(0.94f, 0.94f);

        // Tab Bar.

        /// <summary>The largest size the selected tabs' labels may take; one size for all three.</summary>
        public const float TabLabelMaxFontSize = 44f;

        /// <summary>The selected tile's dark outline on each side (16 of the 256-pixel square sprite, drawn at its own scale): the face is the tile less this.</summary>
        public const float TabTileOutline = 16f;

        /// <summary>The space kept between the longest label and the tile's face on each side.</summary>
        public const float TabLabelMargin = 12f;

        // Home Page.

        /// <summary>
        /// The Play button's foot above the page's foot, which is the tab bar's
        /// top: in the lower third, within a thumb's reach, and clear of the
        /// selected tab's raised tile, which stands about 25 units above the
        /// bar. The notice sits <see cref="NoticeGap"/> above the button.
        /// </summary>
        public const float PlayButtonBottomGap = 120f;

        /// <summary>The space between the Play button's top and the notice above it.</summary>
        public const float NoticeGap = 20f;

        /// <summary>The anchor and pivot the Play button and the notice are pinned by: the middle of the page's foot.</summary>
        public static readonly Vector2 BottomCentre = new Vector2(0.5f, 0f);

        /// <summary>The prefabs the command sets.</summary>
        public const string ButtonRoundPath = "Assets/Prefabs/UI/Button Round.prefab";

        /// <inheritdoc cref="ButtonRoundPath"/>
        public const string TopBarPath = "Assets/Prefabs/UI/Top Bar.prefab";

        /// <inheritdoc cref="ButtonRoundPath"/>
        public const string TabBarPath = "Assets/Prefabs/UI/Tab Bar.prefab";

        /// <inheritdoc cref="ButtonRoundPath"/>
        public const string HomePagePath = "Assets/Prefabs/UI/Home Page.prefab";

        /// <summary>Hierarchy paths, from each prefab's root.</summary>
        public const string ButtonRoundIconPath = "Button Round/Icon";

        /// <inheritdoc cref="ButtonRoundIconPath"/>
        public const string GoldPlusIconPath = "Top Bar/Gold/Gold Plus/Icon";

        /// <inheritdoc cref="ButtonRoundIconPath"/>
        public const string LivesPlusIconPath = "Top Bar/Lives/ Lives Plus/Icon";

        /// <inheritdoc cref="ButtonRoundIconPath"/>
        public const string HeartPath = "Top Bar/Lives/Heart";

        /// <inheritdoc cref="ButtonRoundIconPath"/>
        public const string TopBarGoldPath = "Top Bar/Gold";

        /// <inheritdoc cref="ButtonRoundIconPath"/>
        public const string LivesCountPath = "Top Bar/Lives/Heart/Lives Count";

        /// <inheritdoc cref="ButtonRoundIconPath"/>
        public const string AvatarFramePath = "Top Bar/Avatar Frame";

        /// <inheritdoc cref="ButtonRoundIconPath"/>
        public const string AvatarPath = "Top Bar/Avatar Frame/Avatar";

        /// <inheritdoc cref="ButtonRoundIconPath"/>
        public const string NameTagPath = "Top Bar/Avatar Frame/Name Tag";

        /// <inheritdoc cref="ButtonRoundIconPath"/>
        public const string HomeTabLabelPath = "Tab Bar/Home Tab/Selected/Plate/Name";

        /// <inheritdoc cref="ButtonRoundIconPath"/>
        public const string StoreTabLabelPath = "Tab Bar/Store Tab/Selected/Plate/Name";

        /// <inheritdoc cref="ButtonRoundIconPath"/>
        public const string LeaderboardTabLabelPath = "Tab Bar/LeaderBoard Tab/Selected/Plate/Name";

        /// <inheritdoc cref="ButtonRoundIconPath"/>
        public const string PlayButtonPath = "Home Page/Play Button";

        /// <inheritdoc cref="ButtonRoundIconPath"/>
        public const string NoticePath = "Home Page/Notice";

        /// <summary>The selected tabs' labels; the Leaderboard's, the longest, sets their one size.</summary>
        public static IReadOnlyList<string> TabLabelPaths { get; } = Array.AsReadOnly(new[] { HomeTabLabelPath, StoreTabLabelPath, LeaderboardTabLabelPath });

        // The smallest step the tab labels' font is searched in: whole sizes.
        private const float FontSizeStep = 1f;

        private const string LogPrefix = "Apply Hand-Made Prefab Settings";

        // A Canvas's saved Override Sorting flag, read and written directly
        // where the canvas is a root one in the prefab's contents.
        private const string CanvasOverrideSortingProperty = "m_OverrideSorting";
        private const char PathSeparator = '/';

        /// <summary>Applies the settings to Button Round, then to Top Bar, and logs one line for each.</summary>
        [MenuItem("Gate Rush/Apply Hand-Made Prefab Settings")]
        public static void ApplyFromMenu()
        {
            var isButtonRoundUsable = Apply(ButtonRoundPath, SetButtonRound);
            if (isButtonRoundUsable)
            {
                Apply(TopBarPath, SetTopBar);
            }
            else
            {
                Debug.LogError($"{LogPrefix}: {TopBarPath} refused: Button Round was refused, and its plus icons follow Button Round.");
            }

            Apply(TabBarPath, SetTabBar);
            Apply(HomePagePath, SetHomePage);
        }

        /// <summary>
        /// The one font size the selected tabs' labels share: the largest whole
        /// size, up to <see cref="TabLabelMaxFontSize"/>, at which
        /// <paramref name="longest"/>'s text fits <paramref name="room"/>.
        /// Leaves <paramref name="longest"/> at that size, not auto-sized.
        /// </summary>
        public static float FittingFontSize(TMP_Text longest, float room)
        {
            longest.enableAutoSizing = false;
            var size = TabLabelMaxFontSize;
            longest.fontSize = size;
            while (size > FontSizeStep && longest.GetPreferredValues(longest.text).x > room)
            {
                size -= FontSizeStep;
                longest.fontSize = size;
            }

            return size;
        }

        /// <summary>The room a selected tab's label has on <paramref name="tileWidth"/>: the tile's face less the margin on both sides.</summary>
        public static float TabLabelRoom(float tileWidth) => tileWidth - (TabTileOutline + TabLabelMargin) - (TabTileOutline + TabLabelMargin);

        /// <summary>
        /// The object at <paramref name="path"/> under <paramref name="root"/>,
        /// whose first segment is the root's own name; null when any segment
        /// is missing.
        /// </summary>
        public static Transform FindByPath(GameObject root, string path)
        {
            var separator = path.IndexOf(PathSeparator);
            var rootName = separator < 0 ? path : path.Substring(0, separator);
            if (root == null || !string.Equals(root.name, rootName, StringComparison.Ordinal))
            {
                return null;
            }

            return separator < 0 ? root.transform : root.transform.Find(path.Substring(separator + 1));
        }

        /// <summary>
        /// Loads <paramref name="prefabPath"/>, runs <paramref name="steps"/> on
        /// its contents, saves only when they changed something, and logs the
        /// outcome. Returns false when the prefab was refused.
        /// </summary>
        private static bool Apply(string prefabPath, Func<GameObject, List<string>, string> steps)
        {
            GameObject contents;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(prefabPath);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError($"{LogPrefix}: {prefabPath} refused: it cannot be loaded ({exception.Message}).");
                return false;
            }

            try
            {
                var changes = new List<string>();
                var refusal = steps(contents, changes);
                if (refusal != null)
                {
                    Debug.LogError($"{LogPrefix}: {prefabPath} refused: {refusal}. Nothing was saved.");
                    return false;
                }

                if (changes.Count == 0)
                {
                    Debug.Log($"{LogPrefix}: {prefabPath} unchanged.");
                    return true;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                Debug.Log($"{LogPrefix}: {prefabPath} changed: {string.Join("; ", changes)}.");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>Button Round's steps: its icon in the glyph box. Returns the refusal, or null.</summary>
        private static string SetButtonRound(GameObject root, List<string> changes)
        {
            var icon = FindByPath(root, ButtonRoundIconPath) as RectTransform;
            if (icon == null)
            {
                return $"no RectTransform at '{ButtonRoundIconPath}'";
            }

            SetAnchoredBox(icon, GlyphAnchorMin, GlyphAnchorMax, ButtonRoundIconPath, changes);
            return null;
        }

        /// <summary>
        /// Top Bar's steps: the plus icons, the lives count, the heart and the
        /// avatar. Everything is found before anything is changed, so a
        /// refusal leaves the contents as they were loaded. Returns the
        /// refusal, or null.
        /// </summary>
        private static string SetTopBar(GameObject root, List<string> changes)
        {
            var goldPlusIcon = FindByPath(root, GoldPlusIconPath) as RectTransform;
            var livesPlusIcon = FindByPath(root, LivesPlusIconPath) as RectTransform;
            var heart = FindByPath(root, HeartPath);
            var livesCount = FindByPath(root, LivesCountPath);
            var frame = FindByPath(root, AvatarFramePath);
            var avatar = FindByPath(root, AvatarPath) as RectTransform;
            var goldPill = FindByPath(root, TopBarGoldPath);

            var heartLayout = heart == null ? null : heart.GetComponent<LayoutElement>();
            var countText = livesCount == null ? null : livesCount.GetComponent<TMP_Text>();
            var frameLayout = frame == null ? null : frame.GetComponent<LayoutElement>();

            var missing = new List<string>();
            AddIfMissing(missing, goldPlusIcon, $"a RectTransform at '{GoldPlusIconPath}'");
            AddIfMissing(missing, livesPlusIcon, $"a RectTransform at '{LivesPlusIconPath}'");
            AddIfMissing(missing, heartLayout, $"a Layout Element at '{HeartPath}'");
            AddIfMissing(missing, countText, $"a TextMeshPro text at '{LivesCountPath}'");
            AddIfMissing(missing, frameLayout, $"a Layout Element at '{AvatarFramePath}'");
            AddIfMissing(missing, avatar, $"a RectTransform at '{AvatarPath}'");
            AddIfMissing(missing, goldPill, $"an object at '{TopBarGoldPath}'");
            if (goldPlusIcon != null && !PrefabUtility.IsPartOfPrefabInstance(goldPlusIcon))
            {
                missing.Add($"'{GoldPlusIconPath}' to be part of a Button Round instance");
            }

            if (livesPlusIcon != null && !PrefabUtility.IsPartOfPrefabInstance(livesPlusIcon))
            {
                missing.Add($"'{LivesPlusIconPath}' to be part of a Button Round instance");
            }

            if (missing.Count > 0)
            {
                return $"it needs {string.Join(", ", missing)}";
            }

            var refusal = RevertIconRect(goldPlusIcon, GoldPlusIconPath, changes) ?? RevertIconRect(livesPlusIcon, LivesPlusIconPath, changes);
            if (refusal != null)
            {
                return refusal;
            }

            if (!Mathf.Approximately(countText.fontSize, UiPrefabBuilder.LivesCountFontSize))
            {
                countText.fontSize = UiPrefabBuilder.LivesCountFontSize;
                changes.Add($"{LivesCountPath} font size {UiPrefabBuilder.LivesCountFontSize}");
            }

            SetPreferredSquare(heartLayout, TopBarHeartSize, HeartPath, changes);
            SetPreferredSquare(frameLayout, AvatarFrameSize, AvatarFramePath, changes);
            SetAnchoredBox(avatar, AvatarAnchorMin, AvatarAnchorMax, AvatarPath, changes);

            // The anchors size the avatar: its frame has no layout group, so
            // a Layout Element on it is read by nothing.
            var avatarLayout = avatar.GetComponent<LayoutElement>();
            if (avatarLayout != null)
            {
                Object.DestroyImmediate(avatarLayout);
                changes.Add($"{AvatarPath} Layout Element removed");
            }

            SetGoldPillCanvas(goldPill.gameObject, changes);
            return null;
        }

        /// <summary>
        /// The Top Bar's gold gets a canvas of its own (Module 47), sorting with
        /// its parent until a coin flight lifts it over a scrim, so its count
        /// and its pop rebuild only the pill; and a raycaster, since a nested
        /// canvas's graphics are raycast by its own and the gold's "+" must
        /// stay pressable. Noting each change.
        /// </summary>
        private static void SetGoldPillCanvas(GameObject goldPill, List<string> changes)
        {
            var canvas = goldPill.GetComponent<Canvas>();
            if (canvas == null)
            {
                goldPill.AddComponent<Canvas>();
                changes.Add($"{TopBarGoldPath} Canvas added, sorting with its parent");
            }
            else
            {
                // The saved flag, not the getter: in the prefab's contents this
                // is a root canvas, whose overrideSorting reads false and
                // ignores a write whatever is saved.
                using (var serialized = new SerializedObject(canvas))
                {
                    var overrideSorting = serialized.FindProperty(CanvasOverrideSortingProperty);
                    if (overrideSorting.boolValue)
                    {
                        overrideSorting.boolValue = false;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        changes.Add($"{TopBarGoldPath} Canvas sorting with its parent");
                    }
                }
            }

            if (goldPill.GetComponent<GraphicRaycaster>() == null)
            {
                goldPill.AddComponent<GraphicRaycaster>();
                changes.Add($"{TopBarGoldPath} Graphic Raycaster added");
            }
        }

        /// <summary>
        /// Tab Bar's steps: the three selected tabs' labels share one fixed
        /// size, the largest at which "Leaderboard" fits its tile's face with
        /// the margin, on one line, their rects inset to that room. Returns
        /// the refusal, or null.
        /// </summary>
        private static string SetTabBar(GameObject root, List<string> changes)
        {
            var labels = new List<TMP_Text>();
            var missing = new List<string>();
            foreach (var path in TabLabelPaths)
            {
                var found = FindByPath(root, path);
                var label = found == null ? null : found.GetComponent<TMP_Text>();
                AddIfMissing(missing, label, $"a TextMeshPro text at '{path}'");
                labels.Add(label);
            }

            var longest = labels[labels.Count - 1];
            var tile = longest == null ? null : longest.transform.parent as RectTransform;
            if (longest != null && tile == null)
            {
                missing.Add($"a RectTransform tile above '{LeaderboardTabLabelPath}'");
            }

            if (missing.Count > 0)
            {
                return $"it needs {string.Join(", ", missing)}";
            }

            // Read before the measurement below changes the longest label.
            var before = new List<(bool isAutoSized, float fontSize, TextWrappingModes wrapping, float width)>();
            foreach (var label in labels)
            {
                before.Add((label.enableAutoSizing, label.fontSize, label.textWrappingMode, ((RectTransform)label.transform).sizeDelta.x));
            }

            var room = TabLabelRoom(tile.sizeDelta.x);
            var size = FittingFontSize(longest, room);
            for (var i = 0; i < labels.Count; i++)
            {
                var label = labels[i];
                var rect = (RectTransform)label.transform;
                label.enableAutoSizing = false;
                label.fontSize = size;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                rect.sizeDelta = new Vector2(room - tile.sizeDelta.x, rect.sizeDelta.y);

                var was = before[i];
                if (was.isAutoSized || !Mathf.Approximately(was.fontSize, size) || was.wrapping != TextWrappingModes.NoWrap
                    || !Mathf.Approximately(was.width, rect.sizeDelta.x))
                {
                    changes.Add($"{TabLabelPaths[i]} font size {size}, no auto-size, one line, {room} wide");
                }
            }

            return null;
        }

        /// <summary>
        /// Home Page's steps: the Play button just above the tab bar, its size
        /// kept, and the notice just above it. Returns the refusal, or null.
        /// </summary>
        private static string SetHomePage(GameObject root, List<string> changes)
        {
            var play = FindByPath(root, PlayButtonPath) as RectTransform;
            var notice = FindByPath(root, NoticePath) as RectTransform;
            var missing = new List<string>();
            AddIfMissing(missing, play, $"a RectTransform at '{PlayButtonPath}'");
            AddIfMissing(missing, notice, $"a RectTransform at '{NoticePath}'");
            if (missing.Count > 0)
            {
                return $"it needs {string.Join(", ", missing)}";
            }

            SetBottomCentred(play, PlayButtonBottomGap, PlayButtonPath, changes);
            SetBottomCentred(notice, PlayButtonBottomGap + play.sizeDelta.y + NoticeGap, NoticePath, changes);
            return null;
        }

        /// <summary>Pins <paramref name="rect"/> by its foot's middle to its parent's foot, <paramref name="height"/> above it, its size kept, noting a change.</summary>
        private static void SetBottomCentred(RectTransform rect, float height, string path, List<string> changes)
        {
            var position = new Vector2(0f, height);
            if (rect.anchorMin == BottomCentre && rect.anchorMax == BottomCentre && rect.pivot == BottomCentre && rect.anchoredPosition == position)
            {
                return;
            }

            var size = rect.sizeDelta;
            rect.anchorMin = BottomCentre;
            rect.anchorMax = BottomCentre;
            rect.pivot = BottomCentre;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            changes.Add($"{path} bottom centre, {height} above the page's foot");
        }

        /// <summary>
        /// Reverts the RectTransform overrides of a plus button's icon — only
        /// the icon's, never the instance root's, which place the button in
        /// the bar — so it follows Button Round, then checks it does.
        /// Returns the refusal, or null.
        /// </summary>
        private static string RevertIconRect(RectTransform icon, string path, List<string> changes)
        {
            var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(icon);
            var hasOverride = false;
            foreach (var objectOverride in PrefabUtility.GetObjectOverrides(instanceRoot))
            {
                if (objectOverride.instanceObject == icon)
                {
                    hasOverride = true;
                }
            }

            if (hasOverride)
            {
                PrefabUtility.RevertObjectOverride(icon, InteractionMode.AutomatedAction);
            }

            if (icon.anchorMin != GlyphAnchorMin || icon.anchorMax != GlyphAnchorMax)
            {
                return $"'{path}' has anchors {icon.anchorMin} to {icon.anchorMax} after the revert, not Button Round's glyph box";
            }

            if (hasOverride)
            {
                changes.Add($"{path} RectTransform overrides reverted");
            }

            return null;
        }

        /// <summary>Anchors <paramref name="rect"/> from <paramref name="min"/> to <paramref name="max"/> with no offsets, noting a change.</summary>
        private static void SetAnchoredBox(RectTransform rect, Vector2 min, Vector2 max, string path, List<string> changes)
        {
            if (rect.anchorMin == min && rect.anchorMax == max && rect.anchoredPosition == Vector2.zero && rect.sizeDelta == Vector2.zero)
            {
                return;
            }

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            changes.Add($"{path} anchors {min} to {max}, offsets 0");
        }

        /// <summary>Sets <paramref name="layout"/>'s preferred width and height to <paramref name="size"/>, noting a change.</summary>
        private static void SetPreferredSquare(LayoutElement layout, float size, string path, List<string> changes)
        {
            if (Mathf.Approximately(layout.preferredWidth, size) && Mathf.Approximately(layout.preferredHeight, size))
            {
                return;
            }

            layout.preferredWidth = size;
            layout.preferredHeight = size;
            changes.Add($"{path} Layout Element {size} by {size}");
        }

        private static void AddIfMissing(List<string> missing, Object found, string what)
        {
            if (found == null)
            {
                missing.Add(what);
            }
        }
    }
}
