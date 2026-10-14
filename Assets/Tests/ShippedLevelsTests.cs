using GateRush.Runtime;
using GateRush.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Checks the level files the game ships, under
    /// <c>Assets/Resources/Levels</c>, against limits the code only holds for
    /// a range of values. Reads them the way the game does — every text asset
    /// under <see cref="LevelBootstrap.LevelsResourcePath"/> — so a level
    /// added later is covered without touching this file.
    /// </summary>
    public class ShippedLevelsTests
    {
        [Test]
        public void EveryTimeBonus_InEveryShippedLevel_IsWithinTheWidestTheMarkCheckCovers()
        {
            // RuntimeConfig.Problems() checks that a time-bonus mark cannot
            // overlap a block's other marks only up to this bonus (Module 28).
            const int widest = RuntimeConfig.WidestBonusSecondsChecked;
            var levels = Resources.LoadAll<TextAsset>(LevelBootstrap.LevelsResourcePath);

            Assert.IsNotEmpty(levels, $"no level files in Resources/{LevelBootstrap.LevelsResourcePath}");

            foreach (var level in levels)
            {
                var ctx = LevelSerializer.FromJson(level.text, level.name);

                // The slot space holds placed blocks, generator queue entries
                // and elevator wave blocks alike.
                for (var slot = 0; slot < ctx.TotalBlockCapacity; slot++)
                {
                    Assert.LessOrEqual(
                        ctx.SpecAt(slot).TimeBonusSeconds, widest,
                        $"{level.name}, block slot {slot}: its time bonus is above the widest the mark check covers");
                }
            }
        }
    }
}
