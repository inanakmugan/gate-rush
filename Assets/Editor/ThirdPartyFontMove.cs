using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// <i>Gate Rush → Move Fonts To Third Party</i> (7.8): moves Lilita One —
    /// its TrueType file and the two TextMeshPro font assets made from it —
    /// from <see cref="SourceFolder"/> to <see cref="TargetFolder"/>, beside
    /// Fredoka and Nunito. Each font asset holds its material and atlas
    /// inside it, so they move with it.
    /// </summary>
    /// <remarks>
    /// <para>Moving goes through <see cref="AssetDatabase.MoveAsset"/>, so
    /// every GUID, and every reference to it, is kept. Idempotent: a file
    /// already at its target is skipped. Nothing is moved when any file is
    /// missing from both places or present in both, so a run never leaves
    /// the font half moved.</para>
    /// <para>The materials generated from the font stay where the tools that
    /// write them expect them: LabelShadow and LabelOutline in
    /// <c>Assets/Art/Materials</c>, and the Fredoka outline material in
    /// <see cref="SourceFolder"/>, which stays as the folder for generated
    /// font materials. The font's OFL licence is added to the target by the
    /// owner; the command says when it is missing.</para>
    /// </remarks>
    public static class ThirdPartyFontMove
    {
        /// <summary>Where Lilita One was imported.</summary>
        public const string SourceFolder = "Assets/Art/Fonts";

        /// <summary>The folder the third-party fonts share.</summary>
        public const string TargetParent = "Assets/ThirdParty/Fonts";

        /// <summary>Lilita One's own folder, named like Fredoka's and Nunito's.</summary>
        public const string TargetFolderName = "LilitaOne";

        /// <inheritdoc cref="TargetFolderName"/>
        public const string TargetFolder = TargetParent + "/" + TargetFolderName;

        /// <summary>The licence the owner adds beside the font, as Fredoka and Nunito have.</summary>
        public const string LicencePath = TargetFolder + "/OFL.txt";

        private const string LogPrefix = "Move Fonts To Third Party";

        private static readonly string[] FileNames =
        {
            "LilitaOne-Regular.ttf", "LilitaOne-Regular SDF.asset", "LilitaOne-Regular SDF Outline.asset",
        };

        /// <summary>What a run does with one file.</summary>
        public enum MoveStep
        {
            /// <summary>Only the source exists: it is moved.</summary>
            Move,

            /// <summary>Only the target exists: nothing to do.</summary>
            AlreadyMoved,

            /// <summary>Both exist: the run is refused.</summary>
            Conflict,

            /// <summary>Neither exists: the run is refused.</summary>
            Missing,
        }

        /// <summary>One file's move, from its old path to its new one.</summary>
        public readonly struct FontMove
        {
            /// <summary>Creates the move of <paramref name="from"/> to <paramref name="to"/>.</summary>
            public FontMove(string from, string to)
            {
                From = from;
                To = to;
            }

            /// <summary>The path in <see cref="SourceFolder"/>.</summary>
            public string From { get; }

            /// <summary>The path in <see cref="TargetFolder"/>.</summary>
            public string To { get; }
        }

        /// <summary>Lilita One's three files, each from <see cref="SourceFolder"/> to <see cref="TargetFolder"/>.</summary>
        public static IReadOnlyList<FontMove> Moves { get; } = Array.AsReadOnly(BuildMoves());

        /// <summary>The step for <paramref name="move"/>, given which paths <paramref name="exists"/>.</summary>
        public static MoveStep StepFor(FontMove move, Func<string, bool> exists)
        {
            var isAtSource = exists(move.From);
            var isAtTarget = exists(move.To);
            if (isAtSource)
            {
                return isAtTarget ? MoveStep.Conflict : MoveStep.Move;
            }

            return isAtTarget ? MoveStep.AlreadyMoved : MoveStep.Missing;
        }

        /// <summary>
        /// The refusal of a run over <paramref name="moves"/>: every file that
        /// is in both places or in neither, named; null when the run may go
        /// ahead.
        /// </summary>
        public static string Refusal(IReadOnlyList<FontMove> moves, Func<string, bool> exists)
        {
            var problems = new List<string>();
            foreach (var move in moves)
            {
                switch (StepFor(move, exists))
                {
                    case MoveStep.Conflict:
                        problems.Add($"{move.From} and {move.To} both exist");
                        break;
                    case MoveStep.Missing:
                        problems.Add($"neither {move.From} nor {move.To} exists");
                        break;
                }
            }

            return problems.Count == 0 ? null : string.Join("; ", problems);
        }

        /// <summary>
        /// Creates <see cref="TargetFolder"/> when missing, moves every file
        /// still at its source, logs each one, and says whether the licence is
        /// in place.
        /// </summary>
        [MenuItem("Gate Rush/Move Fonts To Third Party")]
        public static void MoveFromMenu()
        {
            var refusal = Refusal(Moves, File.Exists);
            if (refusal != null)
            {
                Debug.LogError($"{LogPrefix}: nothing was moved: {refusal}.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(TargetFolder))
            {
                if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(TargetParent, TargetFolderName)))
                {
                    Debug.LogError($"{LogPrefix}: could not create {TargetFolder}; nothing was moved.");
                    return;
                }

                Debug.Log($"{LogPrefix}: created {TargetFolder}.");
            }

            var moved = 0;
            foreach (var move in Moves)
            {
                if (StepFor(move, File.Exists) != MoveStep.Move)
                {
                    continue;
                }

                var error = AssetDatabase.ValidateMoveAsset(move.From, move.To);
                if (string.IsNullOrEmpty(error))
                {
                    error = AssetDatabase.MoveAsset(move.From, move.To);
                }

                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"{LogPrefix}: could not move {move.From} to {move.To}: {error}. The files after it were not moved.");
                    return;
                }

                moved++;
                Debug.Log($"{LogPrefix}: moved {move.From} to {move.To}.");
            }

            Debug.Log($"{LogPrefix}: {moved} files moved, {Moves.Count - moved} already in {TargetFolder}.");
            if (File.Exists(LicencePath))
            {
                Debug.Log($"{LogPrefix}: the licence is in place at {LicencePath}.");
            }
            else
            {
                Debug.LogWarning($"{LogPrefix}: Lilita One's licence is expected at {LicencePath}; add the OFL text there.");
            }
        }

        private static FontMove[] BuildMoves()
        {
            var moves = new FontMove[FileNames.Length];
            for (var i = 0; i < FileNames.Length; i++)
            {
                moves[i] = new FontMove($"{SourceFolder}/{FileNames[i]}", $"{TargetFolder}/{FileNames[i]}");
            }

            return moves;
        }
    }
}
