using System;
using System.Collections.Generic;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>How a generated debris sprite is drawn on the board, for the comparison.</summary>
    public enum EffectLook
    {
        /// <summary>A lit sprite, tinted: its stored shade times the headroom and the tint, with a white highlight added (the cube).</summary>
        LitTinted,

        /// <summary>A plain sprite, as it is: the shards, baked in colour, and the white gate glow and sparkle.</summary>
        Plain
    }

    /// <summary>
    /// The arithmetic of <i>Render Effects Comparison</i> (Module 47), apart
    /// from the editor so it needs no texture: a generated debris sprite drawn
    /// as the board draws it and a reference sprite, each laid over the
    /// script's floor in linear light, and how many of their pixels agree.
    /// </summary>
    /// <remarks>
    /// Pixels are compared where either sprite covers anything — the sprite's
    /// footprint, so the empty floor around it never pads the share — and
    /// agree when no channel differs by more than the tolerance, in bytes
    /// after sRGB encoding, as Module 46's comparisons were measured.
    /// </remarks>
    public static class EffectsMatch
    {
        /// <summary>The reference script's floor, #281350, which every sprite is laid over.</summary>
        public static readonly Color32 Floor = new Color32(0x28, 0x13, 0x50, 0xFF);

        /// <summary>The most a channel may differ, in bytes, for a pixel to agree: Module 46's measure.</summary>
        public const int ToleranceBytes = 4;

        /// <summary>
        /// <paramref name="generated"/> drawn as <paramref name="look"/> says
        /// — with <paramref name="highlight"/> added and
        /// <paramref name="tint"/> for a tinted one — over <see cref="Floor"/>,
        /// as sRGB bytes, bottom row first.
        /// </summary>
        /// <param name="headroom">The art recipe's Shade Headroom, which the lit material multiplies back.</param>
        public static Color32[] Draw(EffectLook look, ArtImage generated, ArtImage highlight, Color32 tint, double headroom)
        {
            if (generated == null)
            {
                throw new ArgumentNullException(nameof(generated));
            }

            var pixels = generated.Pixels;
            var floor = Decode(Floor);
            var tinted = Decode(tint);
            var drawn = new Color32[pixels.Count];
            for (var i = 0; i < pixels.Count; i++)
            {
                var p = pixels[i];
                var a = p.a / (double)byte.MaxValue;
                var stored = Decode(p);
                double r;
                double g;
                double b;
                if (look == EffectLook.LitTinted)
                {
                    r = stored.r * headroom * tinted.r;
                    g = stored.g * headroom * tinted.g;
                    b = stored.b * headroom * tinted.b;
                }
                else
                {
                    r = stored.r;
                    g = stored.g;
                    b = stored.b;
                }

                var added = highlight == null ? 0.0 : highlight.Pixels[i].a / (double)byte.MaxValue;
                drawn[i] = Encode(
                    floor.r * (1.0 - a) + r * a + added, floor.g * (1.0 - a) + g * a + added, floor.b * (1.0 - a) + b * a + added,
                    Math.Max(p.a, highlight == null ? (byte)0 : highlight.Pixels[i].a));
            }

            return drawn;
        }

        /// <summary>
        /// <paramref name="reference"/>, straight sRGB with alpha, laid over
        /// <see cref="Floor"/>; turned upside down when
        /// <paramref name="isUpsideDown"/>, for a sprite the board draws with
        /// its rows the other way round (the gate glow's gate is its bottom).
        /// </summary>
        public static Color32[] DrawReference(IReadOnlyList<Color32> reference, int width, int height, bool isUpsideDown)
        {
            var floor = Decode(Floor);
            var drawn = new Color32[reference.Count];
            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    var p = reference[(isUpsideDown ? height - 1 - j : j) * width + i];
                    var a = p.a / (double)byte.MaxValue;
                    var c = Decode(p);
                    drawn[j * width + i] = Encode(
                        floor.r * (1.0 - a) + c.r * a, floor.g * (1.0 - a) + c.g * a, floor.b * (1.0 - a) + c.b * a, p.a);
                }
            }

            return drawn;
        }

        /// <summary>
        /// The share of the footprint — the pixels either drawing covers — where
        /// <paramref name="generated"/> and <paramref name="reference"/> agree
        /// within <see cref="ToleranceBytes"/> on every colour channel, and the
        /// footprint's size. Both drawings as <see cref="Draw"/> and
        /// <see cref="DrawReference"/> return them.
        /// </summary>
        /// <exception cref="ArgumentException">The drawings differ in size.</exception>
        public static (double share, int footprint) Agreement(IReadOnlyList<Color32> generated, IReadOnlyList<Color32> reference)
        {
            if (generated.Count != reference.Count)
            {
                throw new ArgumentException(
                    $"The generated sprite has {generated.Count} pixels and the reference {reference.Count}: their sizes differ.", nameof(reference));
            }

            var footprint = 0;
            var agreeing = 0;
            for (var i = 0; i < generated.Count; i++)
            {
                var g = generated[i];
                var r = reference[i];
                if (g.a == 0 && r.a == 0)
                {
                    continue;
                }

                footprint++;
                if (Math.Abs(g.r - r.r) <= ToleranceBytes && Math.Abs(g.g - r.g) <= ToleranceBytes && Math.Abs(g.b - r.b) <= ToleranceBytes)
                {
                    agreeing++;
                }
            }

            return (footprint == 0 ? 1.0 : agreeing / (double)footprint, footprint);
        }

        /// <summary>
        /// The share of <paramref name="generated"/>'s own bytes that agree with
        /// <paramref name="reference"/>'s — a plain sprite against its
        /// reference, before either is laid over anything: the alpha within
        /// <see cref="ToleranceBytes"/>, and the colour too where both show
        /// some — over the pixels either covers. Over a dark floor a sprite's
        /// faint edge turns one step of alpha into several of colour, which
        /// <see cref="Agreement"/> counts and this does not.
        /// </summary>
        /// <exception cref="ArgumentException">The sprites differ in size.</exception>
        public static (double share, int footprint) ByteAgreement(
            IReadOnlyList<Color32> generated, IReadOnlyList<Color32> reference, int width, int height, bool isUpsideDown)
        {
            if (generated.Count != width * height || reference.Count != width * height)
            {
                throw new ArgumentException($"Both sprites must be {width} by {height}.", nameof(reference));
            }

            var footprint = 0;
            var agreeing = 0;
            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    var g = generated[j * width + i];
                    var r = reference[(isUpsideDown ? height - 1 - j : j) * width + i];
                    if (g.a == 0 && r.a == 0)
                    {
                        continue;
                    }

                    footprint++;
                    var isColourClose = g.a == 0 || r.a == 0
                                        || (Math.Abs(g.r - r.r) <= ToleranceBytes && Math.Abs(g.g - r.g) <= ToleranceBytes
                                            && Math.Abs(g.b - r.b) <= ToleranceBytes);
                    if (Math.Abs(g.a - r.a) <= ToleranceBytes && isColourClose)
                    {
                        agreeing++;
                    }
                }
            }

            return (footprint == 0 ? 1.0 : agreeing / (double)footprint, footprint);
        }

        private static (double r, double g, double b) Decode(Color32 c) =>
            (DecodeChannel(c.r), DecodeChannel(c.g), DecodeChannel(c.b));

        // The sRGB transfer function's constants (IEC 61966-2-1): a standard,
        // not a look parameter.
        private const double SrgbEncodedLimit = 0.04045;
        private const double SrgbLinearLimit = 0.0031308;
        private const double SrgbLinearSlope = 12.92;
        private const double SrgbScale = 1.055;
        private const double SrgbOffset = 0.055;
        private const double SrgbExponent = 2.4;

        private static double DecodeChannel(byte value)
        {
            var c = value / (double)byte.MaxValue;
            return c <= SrgbEncodedLimit ? c / SrgbLinearSlope : Math.Pow((c + SrgbOffset) / SrgbScale, SrgbExponent);
        }

        private static Color32 Encode(double r, double g, double b, byte a) =>
            new Color32(EncodeChannel(r), EncodeChannel(g), EncodeChannel(b), a);

        private static byte EncodeChannel(double linear)
        {
            var c = Math.Min(1.0, Math.Max(0.0, linear));
            var encoded = c <= SrgbLinearLimit ? c * SrgbLinearSlope : SrgbScale * Math.Pow(c, 1.0 / SrgbExponent) - SrgbOffset;
            return (byte)Math.Round(encoded * byte.MaxValue, MidpointRounding.AwayFromZero);
        }
    }
}
