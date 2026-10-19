using System;
using System.Collections.Generic;
using GateRush.Runtime;
using UnityEngine;

namespace GateRush.UI
{
    /// <summary>
    /// Every text, format string and colour the front end's screens set from
    /// code (Module 35, D68, D69): the texts that carry a number or change
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

            AddIfTransparent(problems, backgroundColor, "Background Color");
            AddIfTransparent(problems, iconColor, "Icon Color");
            AddIfTransparent(problems, idleTabIconColor, "Idle Tab Icon Color");
            AddIfTransparent(problems, coinColor, "Coin Color");
            AddIfTransparent(problems, heartColor, "Heart Color");
            AddIfTransparent(problems, buttonLabelColor, "Button Label Color");
            AddIfTransparent(problems, disabledButtonLabelColor, "Disabled Button Label Color");

            return problems;
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
    }
}
