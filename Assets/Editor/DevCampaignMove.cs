using System;
using System.Collections.Generic;
using GateRush.Meta;
using GateRush.Runtime;
using GateRush.Serialization;
using UnityEditor;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// <i>Gate Rush → Dev: Move Campaign To Last Level</i> (Module 49): a
    /// development shortcut to the end of the campaign. It moves the saved
    /// campaign position to the last level of the game's level order, so the
    /// next Play on Home starts that level in the campaign and its win
    /// finishes the campaign. Nothing else in the save changes.
    /// </summary>
    /// <remarks>
    /// <para>Editor only: this assembly never ships. It refuses to run in Play
    /// Mode, where the running game holds its own copy of the save and would
    /// write over the change.</para>
    /// <para>It goes through the game's own save code: the
    /// <see cref="SaveRepository"/> over the player's
    /// <see cref="PlayerPrefsSaveStore"/> and the <see cref="JsonSaveCodec"/>,
    /// with the Meta Config's rules, so the save it writes is one the game
    /// reads. An unreadable save is refused rather than replaced by a new
    /// player's.</para>
    /// <para>Undo: <i>Edit → Clear All PlayerPrefs</i>, which resets the whole
    /// save, not only the campaign.</para>
    /// </remarks>
    public static class DevCampaignMove
    {
        private const string MenuPath = "Gate Rush/Dev: Move Campaign To Last Level";
        private const string MetaConfigPath = "Assets/Config/MetaConfig.asset";
        private const string DialogTitle = "Move Campaign To Last Level";
        private const string LogPrefix = "Move Campaign To Last Level";

        /// <summary>
        /// The campaign position of the last level of <paramref name="catalog"/>:
        /// its index in the order, one less than the number of levels. False,
        /// with <paramref name="position"/> 0, with no usable level order.
        /// </summary>
        public static bool TryGetLastLevelPosition(LevelCatalog catalog, out int position)
        {
            if (catalog == null || catalog.Names.Count == 0)
            {
                position = 0;
                return false;
            }

            position = catalog.Names.Count - 1;
            return true;
        }

        /// <summary>
        /// A save holding every section of <paramref name="save"/>, the same
        /// objects, but the campaign at <paramref name="position"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="save"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative.</exception>
        public static SaveData WithCampaignAt(SaveData save, int position)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            return new SaveData(
                save.Completed,
                save.Sound,
                save.Wallet,
                save.Lives,
                save.Jokers,
                save.Streak,
                new CampaignProgress(position),
                save.Profile,
                save.Haptics);
        }

        [MenuItem(MenuPath)]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Refuse("Play Mode is running, and the running game would write its own save over the change. Stop Play Mode first.");
                return;
            }

            var files = new List<(string name, string json)>();
            foreach (var asset in Resources.LoadAll<TextAsset>(LevelBootstrap.LevelsResourcePath))
            {
                files.Add((asset.name, asset.text));
            }

            var roster = LevelRoster.Read(files);
            if (!TryGetLastLevelPosition(roster.Catalog, out var lastPosition))
            {
                var why = roster.CatalogError ?? $"there are no levels in Resources/{LevelBootstrap.LevelsResourcePath}";
                Refuse($"The level order is unusable ({why}), so there is no last level to move to. Nothing was changed.");
                return;
            }

            var metaConfig = AssetDatabase.LoadAssetAtPath<MetaConfig>(MetaConfigPath);
            if (metaConfig == null)
            {
                Refuse($"There is no Meta Config at {MetaConfigPath}, whose rules the save is read with. Nothing was changed.");
                return;
            }

            var configProblems = metaConfig.Problems();
            if (configProblems.Count > 0)
            {
                Refuse($"The Meta Config has problems, so the save cannot be read with its rules: {string.Join("; ", configProblems)}. Nothing was changed.");
                return;
            }

            var saves = new SaveRepository(new PlayerPrefsSaveStore(), new JsonSaveCodec(), metaConfig.ToRules());
            var load = saves.Load();
            foreach (var problem in load.Problems)
            {
                Debug.LogWarning($"{LogPrefix}: {problem}");
            }

            if (load.Outcome == SaveLoadOutcome.Unreadable)
            {
                Refuse($"The save under '{SaveRepository.SaveKey}' cannot be read; moving the campaign would replace it with a new player's. Nothing was changed.");
                return;
            }

            var positionBefore = load.Data.Campaign.Position;
            saves.Save(WithCampaignAt(load.Data, lastPosition));

            var lastName = roster.Catalog.Names[lastPosition];
            var fresh = load.Outcome == SaveLoadOutcome.Fresh ? " There was no save yet, so the rest is a new player's." : string.Empty;
            Debug.Log(
                $"{LogPrefix}: the campaign position moved from {positionBefore} (level {positionBefore + 1}) to {lastPosition} " +
                $"(level {lastPosition + 1}, '{lastName}') in the save under '{SaveRepository.SaveKey}'. Nothing else changed.{fresh} " +
                "Play on Home now starts that level in the campaign. Undo with Edit > Clear All PlayerPrefs, which resets the whole save.");
        }

        private static void Refuse(string message)
        {
            EditorUtility.DisplayDialog(DialogTitle, message, "OK");
        }
    }
}
