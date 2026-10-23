using GateRush.Runtime;
using GateRush.Serialization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Checks <see cref="BoardLayout.FitOrthographicSize"/> with the bands of
    /// the shipped <c>RuntimeConfig</c> against every shipped level (Module
    /// 45): the larger bottom band that holds the larger joker bar, and the
    /// larger top band that holds the follow-up's three-row HUD, still leave
    /// each board its room. On a tall phone no board is held back by
    /// the bands at all; in the web build's 9:16 window none is drawn below
    /// <see cref="MinimumBandedScale"/> of the size it would have with no
    /// bands.
    /// </summary>
    /// <remarks>
    /// The screens have no unsafe edges, so the bands are the config's own.
    /// Reads the levels the way the game does, so a level added later is
    /// covered without touching this file.
    /// </remarks>
    public class CampaignBoardFitTests
    {
        private const string RuntimeConfigPath = "Assets/Config/RuntimeConfig.asset";

        // A tall phone (the Samsung M33) and the web build's window.
        private const float TallPhoneAspect = 1080f / 2408f;
        private const float WebWindowAspect = 540f / 960f;

        // The least a board may be drawn at in the web window, as a share of
        // its size with no bands: the bands may shrink a tall board a little,
        // never squeeze it.
        private const float MinimumBandedScale = 0.9f;

        private const float NoBand = 0f;
        private const float Tolerance = 1e-4f;

        [Test]
        public void FitOrthographicSize_TallPhone_NoShippedBoardIsHeldBackByTheBands()
        {
            var config = LoadConfig();

            foreach (var (name, layout) in ShippedLayouts(config))
            {
                var banded = layout.FitOrthographicSize(
                    TallPhoneAspect, config.SideMarginCells, config.TopBandScreenFraction, config.BottomBandScreenFraction);
                var unbanded = layout.FitOrthographicSize(TallPhoneAspect, config.SideMarginCells, NoBand, NoBand);

                Assert.AreEqual(unbanded, banded, Tolerance, $"{name} is fitted by its width, not by the bands");
            }
        }

        [Test]
        public void FitOrthographicSize_WebWindow_EveryShippedBoardKeepsItsMinimumScale()
        {
            var config = LoadConfig();

            foreach (var (name, layout) in ShippedLayouts(config))
            {
                var banded = layout.FitOrthographicSize(
                    WebWindowAspect, config.SideMarginCells, config.TopBandScreenFraction, config.BottomBandScreenFraction);
                var unbanded = layout.FitOrthographicSize(WebWindowAspect, config.SideMarginCells, NoBand, NoBand);

                // A larger orthographic size draws the board smaller.
                Assert.That(unbanded / banded, Is.GreaterThanOrEqualTo(MinimumBandedScale), $"{name} between the bands");
            }
        }

        private static RuntimeConfig LoadConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<RuntimeConfig>(RuntimeConfigPath);
            Assert.IsNotNull(config, RuntimeConfigPath);
            return config;
        }

        /// <summary>Every shipped level's layout as the bootstrap makes it, generator machines' reach included.</summary>
        private static (string name, BoardLayout layout)[] ShippedLayouts(RuntimeConfig config)
        {
            var levels = Resources.LoadAll<TextAsset>(LevelBootstrap.LevelsResourcePath);
            Assert.IsNotEmpty(levels, $"no level files in Resources/{LevelBootstrap.LevelsResourcePath}");

            var machine = config.CreateGeneratorMachine();
            var layouts = new (string, BoardLayout)[levels.Length];
            for (var i = 0; i < levels.Length; i++)
            {
                var ctx = LevelSerializer.FromJson(levels[i].text, levels[i].name);
                layouts[i] = (levels[i].name, new BoardLayout(ctx.Width, ctx.Height, config.CellSize, config.FrameThicknessCells, machine.Reach(ctx)));
            }

            return layouts;
        }
    }
}
