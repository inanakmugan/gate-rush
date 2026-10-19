using System;
using System.Collections.Generic;
using GateRush.Runtime;
using UnityEngine;

namespace GateRush.UI
{
    /// <summary>
    /// Every text, format string and colour the front end's screens set from
    /// code (Modules 35 and 36, D68, D69, D72): the texts that carry a number or change
    /// with the state, the theme's background and icon colours, and the
    /// colours that change at runtime. Fixed English titles, layout and
    /// colours that never change live on the prefabs.
    /// </summary>
    /// <remarks>
    /// Formats are read with the invariant culture (<see cref="HomeTexts"/>).
    /// <see cref="Problems"/> reports a blank text, a format with a
    /// placeholder beyond the numbers it is given, and a fully transparent
    /// colour, each by its field's name; the front end refuses to open with
    /// one (<see cref="FrontEnd.MissingSetup"/>).
    /// </remarks>
    [CreateAssetMenu(fileName = "UiConfig", menuName = "Gate Rush/UI Config")]
    public sealed class UiConfig : ScriptableObject
    {
        [Header("Texts (formats use the invariant culture)")]
        [Tooltip("Home's play button while the campaign has a level; {0} is its number.")]
        [SerializeField] private string playLevelFormat = "Level {0}";

        [Tooltip("Home's play button after the last campaign level.")]
        [SerializeField] private string allPlayedLabel = "All Played";

        [Tooltip("The gold in the top bar; {0} is the balance.")]
        [SerializeField] private string goldFormat = "{0}";

        [Tooltip("The count inside the top bar's heart; {0} is the lives.")]
        [SerializeField] private string livesCountFormat = "{0}";

        [Tooltip("The Lives overlay's count; {0} is the lives and {1} the cap.")]
        [SerializeField] private string livesFractionFormat = "{0}/{1}";

        [Tooltip("Beside the lives while they are full, in the top bar and the Lives overlay.")]
        [SerializeField] private string livesFullLabel = "Full";

        [Tooltip("The time to the next life; {0} is the minutes and {1} the seconds, rounded up as the countdown is.")]
        [SerializeField] private string waitFormat = "{0}:{1:00}";

        [Tooltip("The Lives overlay's refill button; {0} is the price from the Meta Config.")]
        [SerializeField] private string refillFormat = "Refill ({0})";

        [Tooltip("A level button's number in the level grid; {0} is the number.")]
        [SerializeField] private string levelNumberFormat = "{0}";

        [Header("Store texts (formats use the invariant culture)")]
        [Tooltip("A gold pack's amount; {0} is the gold. {0:N0} adds thousands separators: 1,500.")]
        [SerializeField] private string goldAmountFormat = "{0:N0}";

        [Tooltip("A price in gold on a buy button, beside the coin; {0} is the price.")]
        [SerializeField] private string goldPriceFormat = "{0}";

        [Tooltip("Under a joker's name on the Store; {0} is how many the player holds.")]
        [SerializeField] private string jokerCountFormat = "You have {0}";

        [Tooltip("The Not Enough Gold prompt's line; {0} is the gold missing.")]
        [SerializeField] private string shortfallFormat = "You need {0} more gold.";

        [Tooltip("The Store's gold packs section header.")]
        [SerializeField] private string goldHeader = "Gold";

        [Tooltip("The Store's jokers section header.")]
        [SerializeField] private string jokersHeader = "Jokers";

        [Tooltip("The Store's lives section header.")]
        [SerializeField] private string livesHeader = "Lives";

        [Tooltip("The line under the Store's sections, never hidden (D72).")]
        [SerializeField] private string testStoreNote = "Test store: purchases are simulated and no money is charged.";

        [Tooltip("The test purchase sheet's title.")]
        [SerializeField] private string testPurchaseTitle = "Test Purchase";

        [Tooltip("The test purchase sheet's offer; {0} is the gold and {1} the price text.")]
        [SerializeField] private string testPurchaseOfferFormat = "{0:N0} gold for {1}";

        [Tooltip("The test purchase sheet's line saying nothing is charged (D72).")]
        [SerializeField] private string testPurchaseNote = "This is a simulated store. No money is charged.";

        [Tooltip("The message prompt's title for a purchase that failed.")]
        [SerializeField] private string purchaseFailedTitle = "Purchase failed";

        [Tooltip("The message prompt's line for a purchase that failed.")]
        [SerializeField] private string purchaseFailedText = "Nothing was charged. Please try again.";

        [Tooltip("The ribbon on a gold pack's card, by product id; a pack with no entry has none. Ids not empty and unique, texts not empty.")]
        [SerializeField] private RibbonEntry[] ribbons =
        {
            new RibbonEntry("gold_medium", "Popular"),
            new RibbonEntry("gold_large", "Best Value"),
        };

        [Header("Theme colours (applied through UiTint)")]
        [Tooltip("The flat colour behind every tab: the reference game's purple.")]
        [SerializeField] private Color backgroundColor = new Color(0.36f, 0.20f, 0.62f);

        [Tooltip("The white icons on buttons and on the selected tab.")]
        [SerializeField] private Color iconColor = Color.white;

        [Tooltip("A tab's icon while another tab is selected.")]
        [SerializeField] private Color idleTabIconColor = new Color(1f, 1f, 1f, 0.55f);

        [SerializeField] private Color coinColor = new Color(1f, 0.80f, 0.20f);
        [SerializeField] private Color heartColor = new Color(0.95f, 0.27f, 0.36f);

        [Header("Colours that change")]
        [Tooltip("A button's label while it can be pressed.")]
        [SerializeField] private Color buttonLabelColor = Color.white;

        [Tooltip("A button's label while it cannot be pressed, readable on the grey sprite.")]
        [SerializeField] private Color disabledButtonLabelColor = new Color(0.43f, 0.44f, 0.56f);

        /// <summary>Home's play button with a campaign level; {0} is its number.</summary>
        public string PlayLevelFormat => playLevelFormat;

        /// <summary>Home's play button after the last campaign level.</summary>
        public string AllPlayedLabel => allPlayedLabel;

        /// <summary>The gold; {0} is the balance.</summary>
        public string GoldFormat => goldFormat;

        /// <summary>The count inside the heart; {0} is the lives.</summary>
        public string LivesCountFormat => livesCountFormat;

        /// <summary>The Lives overlay's count; {0} the lives, {1} the cap.</summary>
        public string LivesFractionFormat => livesFractionFormat;

        /// <summary>Shown beside the lives while they are full.</summary>
        public string LivesFullLabel => livesFullLabel;

        /// <summary>The time to the next life; {0} the minutes, {1} the seconds.</summary>
        public string WaitFormat => waitFormat;

        /// <summary>The refill button's label; {0} is the price.</summary>
        public string RefillFormat => refillFormat;

        /// <summary>A level button's number; {0} is the number.</summary>
        public string LevelNumberFormat => levelNumberFormat;

        /// <summary>A gold pack's amount; {0} is the gold.</summary>
        public string GoldAmountFormat => goldAmountFormat;

        /// <summary>A price in gold on a buy button; {0} is the price.</summary>
        public string GoldPriceFormat => goldPriceFormat;

        /// <summary>A joker's count on the Store; {0} is the count.</summary>
        public string JokerCountFormat => jokerCountFormat;

        /// <summary>The Not Enough Gold line; {0} is the gold missing.</summary>
        public string ShortfallFormat => shortfallFormat;

        /// <summary>The Store's gold packs section header.</summary>
        public string GoldHeader => goldHeader;

        /// <summary>The Store's jokers section header.</summary>
        public string JokersHeader => jokersHeader;

        /// <summary>The Store's lives section header.</summary>
        public string LivesHeader => livesHeader;

        /// <summary>The line under the Store's sections saying the store is simulated.</summary>
        public string TestStoreNote => testStoreNote;

        /// <summary>The test purchase sheet's title.</summary>
        public string TestPurchaseTitle => testPurchaseTitle;

        /// <summary>The test purchase sheet's offer; {0} the gold, {1} the price text.</summary>
        public string TestPurchaseOfferFormat => testPurchaseOfferFormat;

        /// <summary>The test purchase sheet's line saying nothing is charged.</summary>
        public string TestPurchaseNote => testPurchaseNote;

        /// <summary>The message prompt's title for a failed purchase.</summary>
        public string PurchaseFailedTitle => purchaseFailedTitle;

        /// <summary>The message prompt's line for a failed purchase.</summary>
        public string PurchaseFailedText => purchaseFailedText;

        /// <summary>The flat colour behind every tab.</summary>
        public Color BackgroundColor => backgroundColor;

        /// <summary>The white icons on buttons and on the selected tab.</summary>
        public Color IconColor => iconColor;

        /// <summary>A tab's icon while another tab is selected.</summary>
        public Color IdleTabIconColor => idleTabIconColor;

        /// <summary>The gold's coin.</summary>
        public Color CoinColor => coinColor;

        /// <summary>The lives' heart.</summary>
        public Color HeartColor => heartColor;

        /// <summary>A button's label while it can be pressed.</summary>
        public Color ButtonLabelColor => buttonLabelColor;

        /// <summary>A button's label while it cannot be pressed.</summary>
        public Color DisabledButtonLabelColor => disabledButtonLabelColor;

        /// <summary>The colour <paramref name="role"/> stands for.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="role"/> is not a declared role.</exception>
        public Color ColorOf(UiColorRole role)
        {
            switch (role)
            {
                case UiColorRole.Background:
                    return backgroundColor;
                case UiColorRole.Icon:
                    return iconColor;
                case UiColorRole.IdleTabIcon:
                    return idleTabIconColor;
                case UiColorRole.Coin:
                    return coinColor;
                case UiColorRole.Heart:
                    return heartColor;
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role, "Not a colour role.");
            }
        }

        /// <summary>
        /// The ribbon text of the gold pack with <paramref name="productId"/>;
        /// null when it has none.
        /// </summary>
        public string RibbonFor(string productId)
        {
            var entries = ribbons ?? Array.Empty<RibbonEntry>();
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && string.Equals(entries[i].ProductId, productId, StringComparison.Ordinal))
                {
                    return entries[i].Text;
                }
            }

            return null;
        }

        /// <summary>
        /// Every problem with the values, each message naming its field: a
        /// blank text, a format with a placeholder beyond the numbers it is
        /// formatted with or unbalanced braces, and a fully transparent
        /// colour. Empty when every value can be used.
        /// </summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();

            AddIfUnusableFormat(problems, playLevelFormat, 1, "Play Level Format", "{0} for the number");
            AddIfBlank(problems, allPlayedLabel, "All Played Label");
            AddIfUnusableFormat(problems, goldFormat, 1, "Gold Format", "{0} for the balance");
            AddIfUnusableFormat(problems, livesCountFormat, 1, "Lives Count Format", "{0} for the lives");
            AddIfUnusableFormat(problems, livesFractionFormat, 2, "Lives Fraction Format", "{0} for the lives and {1} for the cap");
            AddIfBlank(problems, livesFullLabel, "Lives Full Label");
            AddIfUnusableFormat(problems, waitFormat, 2, "Wait Format", "{0} for the minutes and {1} for the seconds");
            AddIfUnusableFormat(problems, refillFormat, 1, "Refill Format", "{0} for the price");
            AddIfUnusableFormat(problems, levelNumberFormat, 1, "Level Number Format", "{0} for the number");

            AddIfUnusableFormat(problems, goldAmountFormat, 1, "Gold Amount Format", "{0} for the gold");
            AddIfUnusableFormat(problems, goldPriceFormat, 1, "Gold Price Format", "{0} for the price");
            AddIfUnusableFormat(problems, jokerCountFormat, 1, "Joker Count Format", "{0} for the count");
            AddIfUnusableFormat(problems, shortfallFormat, 1, "Shortfall Format", "{0} for the gold missing");
            AddIfBlank(problems, goldHeader, "Gold Header");
            AddIfBlank(problems, jokersHeader, "Jokers Header");
            AddIfBlank(problems, livesHeader, "Lives Header");
            AddIfBlank(problems, testStoreNote, "Test Store Note");
            AddIfBlank(problems, testPurchaseTitle, "Test Purchase Title");
            AddIfUnusableFormat(
                problems, testPurchaseOfferFormat, 2, "Test Purchase Offer Format", "{0} for the gold and {1} for the price text");
            AddIfBlank(problems, testPurchaseNote, "Test Purchase Note");
            AddIfBlank(problems, purchaseFailedTitle, "Purchase Failed Title");
            AddIfBlank(problems, purchaseFailedText, "Purchase Failed Text");
            AddRibbonProblems(problems);

            AddIfTransparent(problems, backgroundColor, "Background Color");
            AddIfTransparent(problems, iconColor, "Icon Color");
            AddIfTransparent(problems, idleTabIconColor, "Idle Tab Icon Color");
            AddIfTransparent(problems, coinColor, "Coin Color");
            AddIfTransparent(problems, heartColor, "Heart Color");
            AddIfTransparent(problems, buttonLabelColor, "Button Label Color");
            AddIfTransparent(problems, disabledButtonLabelColor, "Disabled Button Label Color");

            return problems;
        }

        private void AddRibbonProblems(List<string> problems)
        {
            var entries = ribbons ?? Array.Empty<RibbonEntry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var productId = entry?.ProductId;
                if (string.IsNullOrWhiteSpace(productId))
                {
                    problems.Add($"{name}: Ribbons element {i}: Product Id is empty.");
                }
                else if (!seen.Add(productId))
                {
                    problems.Add($"{name}: Ribbons element {i}: Product Id '{productId}' is used by an earlier element.");
                }

                if (string.IsNullOrWhiteSpace(entry?.Text))
                {
                    problems.Add($"{name}: Ribbons element {i}: Text is empty.");
                }
            }
        }

        private void AddIfBlank(List<string> problems, string text, string field)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                problems.Add($"{name}: {field} is empty.");
            }
        }

        private void AddIfUnusableFormat(List<string> problems, string format, int argumentCount, string field, string placeholders)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                problems.Add($"{name}: {field} is empty.");
            }
            else if (!FormatStrings.IsUsable(format, argumentCount))
            {
                problems.Add($"{name}: {field} must be a format with at most these placeholders: {placeholders}.");
            }
        }

        private void AddIfTransparent(List<string> problems, Color color, string field)
        {
            if (!(color.a > 0f))
            {
                problems.Add($"{name}: {field} must not be fully transparent.");
            }
        }

        /// <summary>One gold pack's ribbon, as the Inspector edits it.</summary>
        [Serializable]
        private sealed class RibbonEntry
        {
            [SerializeField] private string productId;
            [SerializeField] private string text;

            public RibbonEntry(string productId, string text)
            {
                this.productId = productId;
                this.text = text;
            }

            public string ProductId => productId;

            public string Text => text;
        }
    }
}
