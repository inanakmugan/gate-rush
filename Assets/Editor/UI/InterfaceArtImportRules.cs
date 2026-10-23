using System;
using System.Collections.Generic;
using UnityEngine;

namespace GateRush.UI.Editor
{
    /// <summary>
    /// How the interface art of Module 45 (D77) is imported, as data: the
    /// three folders — the sprites, the paintings and, since the Module 45
    /// follow-up, the avatars — their largest texture size, the pixels per
    /// unit the builder's slice multipliers assume, and the 9-slice border of
    /// every sliced sprite. <see cref="InterfaceArtImporter"/> applies it on
    /// import, so no one sets these by hand.
    /// </summary>
    /// <remarks>
    /// Only PNG files directly in one of the three folders have a rule; a
    /// subfolder, another folder or another file type is left alone. A file
    /// with no entry in <see cref="SliceBorders"/> imports with no border,
    /// and no avatar or painting is sliced.
    /// </remarks>
    public static class InterfaceArtImportRules
    {
        /// <summary>The interface's sprites: buttons, frames and full-colour icons.</summary>
        public const string SpriteFolder = "Assets/Art/Sprites/UI";

        /// <summary>The front end's painted backgrounds.</summary>
        public const string BackgroundFolder = "Assets/Art/Backgrounds";

        /// <summary>The avatars a player chooses from, one disc per avatar id.</summary>
        public const string AvatarFolder = "Assets/Art/Sprites/Avatars";

        /// <summary>The largest size an interface sprite is imported at.</summary>
        public const int SpriteMaxSize = 512;

        /// <summary>The largest size a background is imported at: the paintings are 1024 by 1536.</summary>
        public const int BackgroundMaxSize = 2048;

        /// <summary>The largest size an avatar is imported at: the discs are 256 by 256.</summary>
        public const int AvatarMaxSize = 256;

        /// <summary>
        /// Pixels per unit for every folder: Unity's default, set explicitly
        /// because every Pixels Per Unit Multiplier the UI builder uses assumes
        /// it.
        /// </summary>
        public const float PixelsPerUnit = 100f;

        private const string PngExtension = ".png";

        /// <summary>The 9-sliced sprites of <see cref="SpriteFolder"/>, by file name, with their borders in pixels.</summary>
        public static IReadOnlyList<SliceBorder> SliceBorders { get; } = Array.AsReadOnly(new[]
        {
            new SliceBorder("button_rect_green", 63, 61, 63, 61),
            new SliceBorder("button_rect_blue", 64, 62, 64, 62),
            new SliceBorder("button_rect_red", 64, 62, 64, 62),
            new SliceBorder("button_rect_grey", 64, 62, 64, 62),
            new SliceBorder("button_square_green", 77, 77, 77, 77),
            new SliceBorder("button_square_blue", 77, 77, 77, 77),
            new SliceBorder("button_square_grey", 77, 77, 77, 77),
            new SliceBorder("panel", 54, 51, 54, 58),
            new SliceBorder("pill", 63, 61, 63, 61),
            new SliceBorder("ribbon", 102, 53, 102, 53),
            new SliceBorder("bar_track", 22, 21, 22, 21),
            new SliceBorder("bar_fill", 23, 22, 23, 22),
            new SliceBorder("badge", 41, 41, 41, 41),
        });

        /// <summary>
        /// The rule for <paramref name="assetPath"/>: false unless it is a PNG
        /// directly in <see cref="SpriteFolder"/>, <see cref="BackgroundFolder"/>
        /// or <see cref="AvatarFolder"/>.
        /// </summary>
        public static bool TryGetRule(string assetPath, out ImportRule rule)
        {
            rule = default;
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(PngExtension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var slash = assetPath.LastIndexOf('/');
            if (slash < 0)
            {
                return false;
            }

            var folder = assetPath.Substring(0, slash);
            var fileName = assetPath.Substring(slash + 1, assetPath.Length - slash - 1 - PngExtension.Length);
            if (string.Equals(folder, SpriteFolder, StringComparison.Ordinal))
            {
                rule = new ImportRule(SpriteMaxSize, BorderOf(fileName));
                return true;
            }

            if (string.Equals(folder, BackgroundFolder, StringComparison.Ordinal))
            {
                rule = new ImportRule(BackgroundMaxSize, Vector4.zero);
                return true;
            }

            if (string.Equals(folder, AvatarFolder, StringComparison.Ordinal))
            {
                rule = new ImportRule(AvatarMaxSize, Vector4.zero);
                return true;
            }

            return false;
        }

        /// <summary>The border of <paramref name="fileName"/> as Unity stores it (left, bottom, right, top); zero when it is not sliced.</summary>
        private static Vector4 BorderOf(string fileName)
        {
            for (var i = 0; i < SliceBorders.Count; i++)
            {
                if (string.Equals(SliceBorders[i].FileName, fileName, StringComparison.Ordinal))
                {
                    return SliceBorders[i].Border;
                }
            }

            return Vector4.zero;
        }

        /// <summary>One sliced sprite's file name, without its extension, and its border in pixels.</summary>
        public readonly struct SliceBorder
        {
            public SliceBorder(string fileName, int left, int bottom, int right, int top)
            {
                FileName = fileName;
                Left = left;
                Bottom = bottom;
                Right = right;
                Top = top;
            }

            public string FileName { get; }
            public int Left { get; }
            public int Bottom { get; }
            public int Right { get; }
            public int Top { get; }

            /// <summary>The border in Unity's order: left, bottom, right, top.</summary>
            public Vector4 Border => new Vector4(Left, Bottom, Right, Top);
        }

        /// <summary>What one file is imported with: its largest size and its border (zero for none).</summary>
        public readonly struct ImportRule
        {
            public ImportRule(int maxSize, Vector4 border)
            {
                MaxSize = maxSize;
                Border = border;
            }

            public int MaxSize { get; }
            public Vector4 Border { get; }
        }
    }
}
