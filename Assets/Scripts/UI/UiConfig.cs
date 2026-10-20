using System;
using System.Collections.Generic;
using DG.Tweening;
using GateRush.Meta;
using GateRush.Runtime;
using UnityEngine;

namespace GateRush.UI
{
    /// <summary>
    /// Every text, format string and colour the front end's screens and the
    /// level's interface set from code (Modules 35 to 40, D68, D69, D71 to
    /// D74): the texts that carry a number or change with the state, the
    /// joker names, the theme's colours, the colours that change at runtime,
    /// and the level HUD's timing. Fixed English titles, layout and colours
    /// that never change live on the prefabs.
    /// </summary>
    /// <remarks>
    /// Formats are read with the invariant culture (<see cref="HomeTexts"/>).
    /// <see cref="Problems"/> reports a blank text, a format with a
    /// placeholder beyond the numbers it is given, a fully transparent
    /// colour and an unusable timing, each by its field's name; the front end
    /// and the level's interface refuse to open with one
    /// (<see cref="FrontEnd.MissingSetup"/>, <see cref="LevelUi.MissingSetup"/>).
    /// </remarks>
    [CreateAssetMenu(fileName = "UiConfig", menuName = "Gate Rush/UI Config")]
    public sealed class UiConfig : ScriptableObject
    {
        // The ranks that carry a medal (Module 39): fixed by the spec, not tuned.
        private const int GoldRank = 1;
        private const int SilverRank = 2;
        private const int BronzeRank = 3;

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

        [Header("Win streak texts (formats use the invariant culture)")]
        [Tooltip("The streak beside the fire on Home's streak bar; {0} is the streak.")]
        [SerializeField] private string streakCountFormat = "{0}";

        [Tooltip("The streak bar's line at a streak of 0; {0} is the first reward's wins and {1} the reward.")]
        [SerializeField] private string firstRewardFormat = "Win {0} in a row: {1}";

        [Tooltip("The streak bar's line with more than one win to the next reward; {0} is the wins left and {1} the reward.")]
        [SerializeField] private string moreWinsFormat = "{0} more wins: {1}";

        [Tooltip("The streak bar's line with one win to the next reward; {0} is the reward.")]
        [SerializeField] private string oneMoreWinFormat = "1 more win: {0}";

        [Tooltip("The streak bar's line past the last reward.")]
        [SerializeField] private string allRewardsCollectedLabel = "Every reward collected";

        [Tooltip("The Win Streak overlay's current streak; {0} is the streak.")]
        [SerializeField] private string currentStreakFormat = "Streak {0}";

        [Tooltip("The Win Streak overlay's best streak; {0} is the best.")]
        [SerializeField] private string bestStreakFormat = "Best {0}";

        [Tooltip("A ladder row's wins; {0} is the wins.")]
        [SerializeField] private string ladderWinsFormat = "{0} wins";

        [Tooltip("A reward with its count, on a ladder row and wherever a count above 1 is named; {0} is the joker's name and {1} the count.")]
        [SerializeField] private string ladderRewardFormat = "{0} x{1}";

        [Tooltip("A marker's count on the streak bar, shown only above 1; {0} is the count.")]
        [SerializeField] private string markerCountFormat = "x{0}";

        [Tooltip("A ladder row's state when the reward was reached in the current streak.")]
        [SerializeField] private string collectedLabel = "Collected";

        [Tooltip("A ladder row's state on the first reward not reached.")]
        [SerializeField] private string nextLabel = "Next";

        [Tooltip("The rule at the bottom of the Win Streak overlay (D58, D61).")]
        [SerializeField] private string streakRuleText = "Win campaign levels in a row. A lost level resets the streak. Free play does not count.";

        [Tooltip("The Streak Reward overlay's headline; {0} is the streak of the latest rewarded win.")]
        [SerializeField] private string rewardHeadlineFormat = "{0} wins in a row!";

        [Tooltip("A line of the Streak Reward overlay; {0} is the count and {1} the joker's name.")]
        [SerializeField] private string prizeFormat = "+{0} {1}";

        [Header("Profile and Settings texts (formats use the invariant culture)")]
        [Tooltip("The Profile overlay's title.")]
        [SerializeField] private string profileTitle = "Profile";

        [Tooltip("The hint under Profile's name field.")]
        [SerializeField] private string nameEditHint = "Tap to edit";

        [Tooltip("The line under Profile's name field when a name is refused by the name rule; keep it in step with the rule's limits.")]
        [SerializeField] private string nameRefusedText = "Use 1 to 16 letters, digits, spaces, - _ or .";

        [Tooltip("Profile's statistic: the levels the save marks completed.")]
        [SerializeField] private string levelsCompletedLabel = "Levels Completed";

        [Tooltip("Profile's statistic: the campaign level, shown as Home's play button shows it.")]
        [SerializeField] private string campaignLevelLabel = "Campaign Level";

        [Tooltip("Profile's statistic: the best win streak.")]
        [SerializeField] private string bestStreakLabel = "Best Streak";

        [Tooltip("Profile's statistic: all the gold ever earned, not bought.")]
        [SerializeField] private string lifetimeGoldLabel = "Lifetime Gold";

        [Tooltip("A numeric statistic on Profile; {0} is the number. {0:N0} adds thousands separators: 1,250.")]
        [SerializeField] private string statisticFormat = "{0:N0}";

        [Tooltip("The label of the Settings overlay's haptics row.")]
        [SerializeField] private string hapticsLabel = "Haptics";

        [Header("Leaderboard texts (formats use the invariant culture)")]
        [Tooltip("The line under the Leaderboard's title, never hidden while the table is simulated (D73).")]
        [SerializeField] private string leaderboardSimulatedNote = "Demo players. The online leaderboard is not connected.";

        [Tooltip("The Leaderboard page while a fetch is pending.")]
        [SerializeField] private string leaderboardLoadingText = "Loading…";

        [Tooltip("The Leaderboard page after a fetch failed, above Retry.")]
        [SerializeField] private string leaderboardFailedText = "Could not load the leaderboard.";

        [Tooltip("The label of the Leaderboard page's Retry button.")]
        [SerializeField] private string leaderboardRetryLabel = "Retry";

        [Tooltip("A row's score beside the coin; {0} is the lifetime gold. {0:N0} adds thousands separators: 1,250.")]
        [SerializeField] private string leaderboardScoreFormat = "{0:N0}";

        [Tooltip("A row's rank; {0} is the rank, 1 at the top.")]
        [SerializeField] private string leaderboardRankFormat = "{0}";

        [Header("Level HUD and joker bar texts (Module 40; formats use the invariant culture)")]
        [Tooltip("The HUD's label at the right during free play, in place of the level number (D58).")]
        [SerializeField] private string freePlayLabel = "Free Play";

        [Tooltip("The HUD's label at the right in the campaign; {0} is the level's number.")]
        [SerializeField] private string levelLabelFormat = "Level {0}";

        [Tooltip("The timer; {0} is the minutes and {1} the seconds, rounded up as the countdown is.")]
        [SerializeField] private string timerFormat = "{0}:{1:00}";

        [Tooltip("Shown beside the timer when time is added (M10, the Clock); {0} is the seconds.")]
        [SerializeField] private string timeBonusFormat = "+{0}";

        [Tooltip("A joker button's badge while some are held, or left in free play; {0} is the count.")]
        [SerializeField] private string jokerCountBadgeFormat = "{0}";

        [Tooltip("A joker button's badge in the campaign with none held, beside the coin; {0} is the price.")]
        [SerializeField] private string jokerPriceBadgeFormat = "{0}";

        [Tooltip("The title of the gold packs over a level, opened by Get Gold.")]
        [SerializeField] private string goldPacksTitle = "Get Gold";

        [Header("Level HUD timing (Module 40; unscaled time)")]
        [Tooltip("The timer turns Timer Warning Color once the displayed second is at most this. 0 turns it only at 0:00.")]
        [SerializeField] private int timerWarningSeconds = 10;

        [Tooltip("Seconds the time bonus takes to rise and fade.")]
        [SerializeField] private float timeBonusSeconds = 0.9f;

        [Tooltip("How far the time bonus rises while it fades, in canvas units.")]
        [SerializeField] private float timeBonusRiseUnits = 60f;

        [SerializeField] private Ease timeBonusEase = Ease.OutQuad;

        [Tooltip("The scale the timer pulses to when time is added: at least 1.")]
        [SerializeField] private float timerPulseScale = 1.15f;

        [Tooltip("Seconds the timer's pulse takes, up and back.")]
        [SerializeField] private float timerPulseSeconds = 0.25f;

        [Header("Joker names")]
        [SerializeField] private string clockName = "Clock";
        [SerializeField] private string rocketName = "Rocket";
        [SerializeField] private string broomName = "Broom";

        [Header("Theme colours (applied through UiTint)")]
        [Tooltip("The flat colour behind every tab: the reference game's purple.")]
        [SerializeField] private Color backgroundColor = new Color(0.36f, 0.20f, 0.62f);

        [Tooltip("The white icons on buttons and on the selected tab.")]
        [SerializeField] private Color iconColor = Color.white;

        [Tooltip("A tab's icon while another tab is selected.")]
        [SerializeField] private Color idleTabIconColor = new Color(1f, 1f, 1f, 0.55f);

        [SerializeField] private Color coinColor = new Color(1f, 0.80f, 0.20f);
        [SerializeField] private Color heartColor = new Color(0.95f, 0.27f, 0.36f);

        [Tooltip("The win streak's fire, the bar's fill and the next reward's highlight.")]
        [SerializeField] private Color streakColor = new Color(1f, 0.55f, 0.12f);

        [Tooltip("The ring around the selected Rocket or Broom in a level (D67).")]
        [SerializeField] private Color jokerSelectedColor = new Color(1f, 0.76f, 0.2f);

        [Header("Colours that change")]
        [Tooltip("A button's label while it can be pressed.")]
        [SerializeField] private Color buttonLabelColor = Color.white;

        [Tooltip("A button's label while it cannot be pressed, readable on the grey sprite.")]
        [SerializeField] private Color disabledButtonLabelColor = new Color(0.43f, 0.44f, 0.56f);

        [Tooltip("The medal on the Leaderboard's first rank.")]
        [SerializeField] private Color goldMedalColor = new Color(0.961f, 0.718f, 0.039f);

        [Tooltip("The medal on the Leaderboard's second rank.")]
        [SerializeField] private Color silverMedalColor = new Color(0.722f, 0.761f, 0.800f);

        [Tooltip("The medal on the Leaderboard's third rank.")]
        [SerializeField] private Color bronzeMedalColor = new Color(0.804f, 0.498f, 0.196f);

        [Tooltip("The player's own row on the Leaderboard.")]
        [SerializeField] private Color leaderboardHighlightColor = new Color(1f, 0.914f, 0.659f);

        [Tooltip("Every other row on the Leaderboard.")]
        [SerializeField] private Color leaderboardRowColor = Color.white;

        [Tooltip("The timer's digits in a level.")]
        [SerializeField] private Color timerColor = Color.white;

        [Tooltip("The timer's digits once the displayed second reaches Timer Warning Seconds.")]
        [SerializeField] private Color timerWarningColor = new Color(1f, 0.32f, 0.32f);

        [Tooltip("The time bonus beside the timer; it fades from this.")]
        [SerializeField] private Color timeBonusColor = new Color(0.45f, 1f, 0.45f);

        [Tooltip("A joker button's badge text: its count, or a price the balance covers.")]
        [SerializeField] private Color jokerBadgeTextColor = Color.white;

        [Tooltip("A joker button's price while the balance is short of it: a press opens Not Enough Gold (D74).")]
        [SerializeField] private Color shortPriceColor = new Color(1f, 0.45f, 0.45f);

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

        /// <summary>The streak beside the fire; {0} is the streak.</summary>
        public string StreakCountFormat => streakCountFormat;

        /// <summary>The bar's line at a streak of 0; {0} the first reward's wins, {1} the reward.</summary>
        public string FirstRewardFormat => firstRewardFormat;

        /// <summary>The bar's line with more than one win left; {0} the wins left, {1} the reward.</summary>
        public string MoreWinsFormat => moreWinsFormat;

        /// <summary>The bar's line with one win left; {0} the reward.</summary>
        public string OneMoreWinFormat => oneMoreWinFormat;

        /// <summary>The bar's line past the last reward.</summary>
        public string AllRewardsCollectedLabel => allRewardsCollectedLabel;

        /// <summary>The Win Streak overlay's current streak; {0} is the streak.</summary>
        public string CurrentStreakFormat => currentStreakFormat;

        /// <summary>The Win Streak overlay's best streak; {0} is the best.</summary>
        public string BestStreakFormat => bestStreakFormat;

        /// <summary>A ladder row's wins; {0} is the wins.</summary>
        public string LadderWinsFormat => ladderWinsFormat;

        /// <summary>A reward with its count; {0} the joker's name, {1} the count.</summary>
        public string LadderRewardFormat => ladderRewardFormat;

        /// <summary>A marker's count above 1; {0} is the count.</summary>
        public string MarkerCountFormat => markerCountFormat;

        /// <summary>A ladder row's state for a reward reached in the current streak.</summary>
        public string CollectedLabel => collectedLabel;

        /// <summary>A ladder row's state for the first reward not reached.</summary>
        public string NextLabel => nextLabel;

        /// <summary>The rule at the bottom of the Win Streak overlay.</summary>
        public string StreakRuleText => streakRuleText;

        /// <summary>The Streak Reward overlay's headline; {0} is the latest rewarded streak.</summary>
        public string RewardHeadlineFormat => rewardHeadlineFormat;

        /// <summary>A line of the Streak Reward overlay; {0} the count, {1} the joker's name.</summary>
        public string PrizeFormat => prizeFormat;

        /// <summary>The Profile overlay's title.</summary>
        public string ProfileTitle => profileTitle;

        /// <summary>The hint under Profile's name field.</summary>
        public string NameEditHint => nameEditHint;

        /// <summary>The line under Profile's name field after a refused name.</summary>
        public string NameRefusedText => nameRefusedText;

        /// <summary>Profile's levels completed label.</summary>
        public string LevelsCompletedLabel => levelsCompletedLabel;

        /// <summary>Profile's campaign level label.</summary>
        public string CampaignLevelLabel => campaignLevelLabel;

        /// <summary>Profile's best streak label.</summary>
        public string BestStreakLabel => bestStreakLabel;

        /// <summary>Profile's lifetime gold label.</summary>
        public string LifetimeGoldLabel => lifetimeGoldLabel;

        /// <summary>A numeric statistic on Profile; {0} is the number.</summary>
        public string StatisticFormat => statisticFormat;

        /// <summary>The Settings overlay's haptics row label.</summary>
        public string HapticsLabel => hapticsLabel;

        /// <summary>The Leaderboard's line saying the table is simulated.</summary>
        public string LeaderboardSimulatedNote => leaderboardSimulatedNote;

        /// <summary>The Leaderboard page while a fetch is pending.</summary>
        public string LeaderboardLoadingText => leaderboardLoadingText;

        /// <summary>The Leaderboard page after a failed fetch.</summary>
        public string LeaderboardFailedText => leaderboardFailedText;

        /// <summary>The Leaderboard page's Retry label.</summary>
        public string LeaderboardRetryLabel => leaderboardRetryLabel;

        /// <summary>A row's score; {0} is the lifetime gold.</summary>
        public string LeaderboardScoreFormat => leaderboardScoreFormat;

        /// <summary>A row's rank; {0} is the rank.</summary>
        public string LeaderboardRankFormat => leaderboardRankFormat;

        /// <summary>The HUD's label during free play.</summary>
        public string FreePlayLabel => freePlayLabel;

        /// <summary>The HUD's label in the campaign; {0} is the level's number.</summary>
        public string LevelLabelFormat => levelLabelFormat;

        /// <summary>The timer; {0} the minutes, {1} the seconds.</summary>
        public string TimerFormat => timerFormat;

        /// <summary>The time bonus beside the timer; {0} is the seconds.</summary>
        public string TimeBonusFormat => timeBonusFormat;

        /// <summary>A joker button's count badge; {0} is the count.</summary>
        public string JokerCountBadgeFormat => jokerCountBadgeFormat;

        /// <summary>A joker button's price badge; {0} is the price.</summary>
        public string JokerPriceBadgeFormat => jokerPriceBadgeFormat;

        /// <summary>The title of the gold packs over a level.</summary>
        public string GoldPacksTitle => goldPacksTitle;

        /// <summary>The displayed second at or below which the timer takes <see cref="TimerWarningColor"/>.</summary>
        public int TimerWarningSeconds => timerWarningSeconds;

        /// <summary>Seconds the time bonus takes to rise and fade.</summary>
        public float TimeBonusSeconds => timeBonusSeconds;

        /// <summary>How far the time bonus rises, in canvas units.</summary>
        public float TimeBonusRiseUnits => timeBonusRiseUnits;

        /// <summary>The easing of the time bonus's rise and fade.</summary>
        public Ease TimeBonusEase => timeBonusEase;

        /// <summary>The scale the timer pulses to when time is added.</summary>
        public float TimerPulseScale => timerPulseScale;

        /// <summary>Seconds the timer's pulse takes, up and back.</summary>
        public float TimerPulseSeconds => timerPulseSeconds;

        /// <summary>The ring around the selected joker's button.</summary>
        public Color JokerSelectedColor => jokerSelectedColor;

        /// <summary>The timer's digits.</summary>
        public Color TimerColor => timerColor;

        /// <summary>The timer's digits near the end.</summary>
        public Color TimerWarningColor => timerWarningColor;

        /// <summary>The time bonus beside the timer.</summary>
        public Color TimeBonusColor => timeBonusColor;

        /// <summary>A joker button's badge text, except a price the balance is short of.</summary>
        public Color JokerBadgeTextColor => jokerBadgeTextColor;

        /// <summary>A joker button's price while the balance is short of it.</summary>
        public Color ShortPriceColor => shortPriceColor;

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

        /// <summary>The win streak's fire, the bar's fill and the next reward's highlight.</summary>
        public Color StreakColor => streakColor;

        /// <summary>A button's label while it can be pressed.</summary>
        public Color ButtonLabelColor => buttonLabelColor;

        /// <summary>A button's label while it cannot be pressed.</summary>
        public Color DisabledButtonLabelColor => disabledButtonLabelColor;

        /// <summary>The player's own row on the Leaderboard.</summary>
        public Color LeaderboardHighlightColor => leaderboardHighlightColor;

        /// <summary>Every other row on the Leaderboard.</summary>
        public Color LeaderboardRowColor => leaderboardRowColor;

        /// <summary>The first rank's medal.</summary>
        public Color GoldMedalColor => goldMedalColor;

        /// <summary>The second rank's medal.</summary>
        public Color SilverMedalColor => silverMedalColor;

        /// <summary>The third rank's medal.</summary>
        public Color BronzeMedalColor => bronzeMedalColor;

        /// <summary>
        /// The medal colour of <paramref name="rank"/>: gold, silver and bronze
        /// for the first three ranks. False, with <paramref name="color"/>
        /// clear, for every other rank, which has no medal.
        /// </summary>
        public bool TryGetMedalColor(int rank, out Color color)
        {
            switch (rank)
            {
                case GoldRank:
                    color = goldMedalColor;
                    return true;
                case SilverRank:
                    color = silverMedalColor;
                    return true;
                case BronzeRank:
                    color = bronzeMedalColor;
                    return true;
                default:
                    color = Color.clear;
                    return false;
            }
        }

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
                case UiColorRole.Streak:
                    return streakColor;
                case UiColorRole.JokerSelected:
                    return jokerSelectedColor;
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role, "Not a colour role.");
            }
        }

        /// <summary>The name <paramref name="kind"/> is shown by, on the Store and the win streak's screens.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public string JokerName(JokerKind kind)
        {
            switch (kind)
            {
                case JokerKind.Clock:
                    return clockName;
                case JokerKind.Rocket:
                    return rocketName;
                case JokerKind.Broom:
                    return broomName;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a joker kind.");
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

            AddIfUnusableFormat(problems, streakCountFormat, 1, "Streak Count Format", "{0} for the streak");
            AddIfUnusableFormat(problems, firstRewardFormat, 2, "First Reward Format", "{0} for the wins and {1} for the reward");
            AddIfUnusableFormat(problems, moreWinsFormat, 2, "More Wins Format", "{0} for the wins left and {1} for the reward");
            AddIfUnusableFormat(problems, oneMoreWinFormat, 1, "One More Win Format", "{0} for the reward");
            AddIfBlank(problems, allRewardsCollectedLabel, "All Rewards Collected Label");
            AddIfUnusableFormat(problems, currentStreakFormat, 1, "Current Streak Format", "{0} for the streak");
            AddIfUnusableFormat(problems, bestStreakFormat, 1, "Best Streak Format", "{0} for the best");
            AddIfUnusableFormat(problems, ladderWinsFormat, 1, "Ladder Wins Format", "{0} for the wins");
            AddIfUnusableFormat(problems, ladderRewardFormat, 2, "Ladder Reward Format", "{0} for the joker's name and {1} for the count");
            AddIfUnusableFormat(problems, markerCountFormat, 1, "Marker Count Format", "{0} for the count");
            AddIfBlank(problems, collectedLabel, "Collected Label");
            AddIfBlank(problems, nextLabel, "Next Label");
            AddIfBlank(problems, streakRuleText, "Streak Rule Text");
            AddIfUnusableFormat(problems, rewardHeadlineFormat, 1, "Reward Headline Format", "{0} for the streak");
            AddIfUnusableFormat(problems, prizeFormat, 2, "Prize Format", "{0} for the count and {1} for the joker's name");
            AddIfBlank(problems, profileTitle, "Profile Title");
            AddIfBlank(problems, nameEditHint, "Name Edit Hint");
            AddIfBlank(problems, nameRefusedText, "Name Refused Text");
            AddIfBlank(problems, levelsCompletedLabel, "Levels Completed Label");
            AddIfBlank(problems, campaignLevelLabel, "Campaign Level Label");
            AddIfBlank(problems, bestStreakLabel, "Best Streak Label");
            AddIfBlank(problems, lifetimeGoldLabel, "Lifetime Gold Label");
            AddIfUnusableFormat(problems, statisticFormat, 1, "Statistic Format", "{0} for the number");
            AddIfBlank(problems, hapticsLabel, "Haptics Label");
            AddIfBlank(problems, leaderboardSimulatedNote, "Leaderboard Simulated Note");
            AddIfBlank(problems, leaderboardLoadingText, "Leaderboard Loading Text");
            AddIfBlank(problems, leaderboardFailedText, "Leaderboard Failed Text");
            AddIfBlank(problems, leaderboardRetryLabel, "Leaderboard Retry Label");
            AddIfUnusableFormat(problems, leaderboardScoreFormat, 1, "Leaderboard Score Format", "{0} for the score");
            AddIfUnusableFormat(problems, leaderboardRankFormat, 1, "Leaderboard Rank Format", "{0} for the rank");
            AddIfBlank(problems, freePlayLabel, "Free Play Label");
            AddIfUnusableFormat(problems, levelLabelFormat, 1, "Level Label Format", "{0} for the number");
            AddIfUnusableFormat(problems, timerFormat, 2, "Timer Format", "{0} for the minutes and {1} for the seconds");
            AddIfUnusableFormat(problems, timeBonusFormat, 1, "Time Bonus Format", "{0} for the seconds");
            AddIfUnusableFormat(problems, jokerCountBadgeFormat, 1, "Joker Count Badge Format", "{0} for the count");
            AddIfUnusableFormat(problems, jokerPriceBadgeFormat, 1, "Joker Price Badge Format", "{0} for the price");
            AddIfBlank(problems, goldPacksTitle, "Gold Packs Title");
            AddLevelTimingProblems(problems);
            AddIfBlank(problems, clockName, "Clock Name");
            AddIfBlank(problems, rocketName, "Rocket Name");
            AddIfBlank(problems, broomName, "Broom Name");

            AddIfTransparent(problems, backgroundColor, "Background Color");
            AddIfTransparent(problems, iconColor, "Icon Color");
            AddIfTransparent(problems, idleTabIconColor, "Idle Tab Icon Color");
            AddIfTransparent(problems, coinColor, "Coin Color");
            AddIfTransparent(problems, heartColor, "Heart Color");
            AddIfTransparent(problems, streakColor, "Streak Color");
            AddIfTransparent(problems, buttonLabelColor, "Button Label Color");
            AddIfTransparent(problems, disabledButtonLabelColor, "Disabled Button Label Color");
            AddIfTransparent(problems, goldMedalColor, "Gold Medal Color");
            AddIfTransparent(problems, silverMedalColor, "Silver Medal Color");
            AddIfTransparent(problems, bronzeMedalColor, "Bronze Medal Color");
            AddIfTransparent(problems, leaderboardHighlightColor, "Leaderboard Highlight Color");
            AddIfTransparent(problems, leaderboardRowColor, "Leaderboard Row Color");
            AddIfTransparent(problems, jokerSelectedColor, "Joker Selected Color");
            AddIfTransparent(problems, timerColor, "Timer Color");
            AddIfTransparent(problems, timerWarningColor, "Timer Warning Color");
            AddIfTransparent(problems, timeBonusColor, "Time Bonus Color");
            AddIfTransparent(problems, jokerBadgeTextColor, "Joker Badge Text Color");
            AddIfTransparent(problems, shortPriceColor, "Short Price Color");

            return problems;
        }

        /// <summary>
        /// The level HUD's timing (Module 40): the warning second is not
        /// negative, every time is positive, the rise is not negative, and the
        /// pulse grows the timer; every number finite.
        /// </summary>
        private void AddLevelTimingProblems(List<string> problems)
        {
            if (timerWarningSeconds < 0)
            {
                problems.Add($"{name}: Timer Warning Seconds may not be negative.");
            }

            if (!(timeBonusSeconds > 0f) || float.IsInfinity(timeBonusSeconds))
            {
                problems.Add($"{name}: Time Bonus Seconds must be positive and finite.");
            }

            if (!(timerPulseSeconds > 0f) || float.IsInfinity(timerPulseSeconds))
            {
                problems.Add($"{name}: Timer Pulse Seconds must be positive and finite.");
            }

            if (!(timeBonusRiseUnits >= 0f) || float.IsInfinity(timeBonusRiseUnits))
            {
                problems.Add($"{name}: Time Bonus Rise Units must be at least 0 and finite.");
            }

            if (!(timerPulseScale >= 1f) || float.IsInfinity(timerPulseScale))
            {
                problems.Add($"{name}: Timer Pulse Scale must be at least 1 and finite.");
            }
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
