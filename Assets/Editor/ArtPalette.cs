using System;
using GateRush.Runtime;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// The colours <see cref="ArtPainter"/> paints into the mechanics' sprites
    /// whose colour never varies (Module 46, pass 2): gold, silver, metal,
    /// glass, the shutter's and the elevator's, and Module 47's ice shards. They stay in
    /// <see cref="RuntimeConfig"/> and are read from it when the art is
    /// generated, so changing one means running <i>Gate Rush → Generate Art</i>
    /// again. What varies — a block's colour, a gem's, a badge's — stays grey
    /// in its sprite and is tinted at runtime (D46).
    /// </summary>
    /// <remarks>
    /// Each colour is held in linear light, decoded from its sRGB byte as the
    /// reference script decodes its hex values, so a colour set as a byte paints
    /// the script's own value.
    /// </remarks>
    public sealed class ArtPalette
    {
        private ArtPalette(RuntimeConfig config)
        {
            IceTint = Linear.Of(config.IceTintColor);
            IceRim = Linear.Of(config.IceRimColor);
            Silver = Linear.Of(config.ChainSilverColor);
            Gold = Linear.Of(config.LockGoldColor);
            Keyhole = Linear.Of(config.KeyholeColor);
            Coin = Linear.Of(config.CoinColor);
            CoinFace = Linear.Of(config.CoinFaceColor);
            CoinHands = Linear.Of(config.CoinHandColor);
            ShutterRim = Linear.Of(config.ShutterRimColor);
            ShutterCylinder = Linear.Of(config.ShutterCylinderColor);
            MachineMetal = Linear.Of(config.MachineMetalColor);
            MachineGlass = Linear.Of(config.MachineGlassColor);
            MachineRivet = Linear.Of(config.MachineRivetColor);
            MachineButton = Linear.Of(config.MachineButtonColor);
            ElevatorRim = Linear.Of(config.ElevatorRimColor);
            Hazard = Linear.Of(config.HazardColor);
            HazardDark = Linear.Of(config.HazardDarkColor);
            ElevatorRecess = Linear.Of(config.ElevatorRecessColor);
            ElevatorDoor = Linear.Of(config.ElevatorPanelColor);
            ElevatorHandle = Linear.Of(config.ElevatorHandleColor);
            Shard = Linear.Of(config.ShardColor);
        }

        /// <summary>The palette <paramref name="config"/> holds.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static ArtPalette From(RuntimeConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            return new ArtPalette(config);
        }

        internal Linear IceTint { get; }
        internal Linear IceRim { get; }
        internal Linear Silver { get; }
        internal Linear Gold { get; }
        internal Linear Keyhole { get; }
        internal Linear Coin { get; }
        internal Linear CoinFace { get; }
        internal Linear CoinHands { get; }
        internal Linear ShutterRim { get; }
        internal Linear ShutterCylinder { get; }
        internal Linear MachineMetal { get; }
        internal Linear MachineGlass { get; }
        internal Linear MachineRivet { get; }
        internal Linear MachineButton { get; }
        internal Linear ElevatorRim { get; }
        internal Linear Hazard { get; }
        internal Linear HazardDark { get; }
        internal Linear ElevatorRecess { get; }
        internal Linear ElevatorDoor { get; }
        internal Linear ElevatorHandle { get; }

        /// <summary>The glassy ice of Module 47's shards.</summary>
        internal Linear Shard { get; }
    }

    /// <summary>A colour in linear light, each channel a double: what the painter blends in.</summary>
    internal readonly struct Linear
    {
        public Linear(double r, double g, double b)
        {
            R = r;
            G = g;
            B = b;
        }

        public double R { get; }
        public double G { get; }
        public double B { get; }

        public static Linear White => new Linear(1.0, 1.0, 1.0);

        public static Linear Black => new Linear(0.0, 0.0, 0.0);

        /// <summary>A grey of <paramref name="tone"/> in every channel: a share of the tint a grey sprite is drawn in.</summary>
        public static Linear Grey(double tone) => new Linear(tone, tone, tone);

        /// <summary><paramref name="color"/>, an sRGB colour, rounded to its bytes and decoded to linear light.</summary>
        public static Linear Of(Color color) =>
            new Linear(DecodeByte(color.r), DecodeByte(color.g), DecodeByte(color.b));

        public static Linear operator *(Linear c, double k) => new Linear(c.R * k, c.G * k, c.B * k);

        public static Linear operator +(Linear a, Linear b) => new Linear(a.R + b.R, a.G + b.G, a.B + b.B);

        /// <summary>
        /// The sRGB channel <paramref name="channel"/>, rounded to a byte, in
        /// linear light: the standard's transfer function (IEC 61966-2-1),
        /// through a rounded power so a platform's last bit never shows.
        /// </summary>
        private static double DecodeByte(float channel)
        {
            var c = Math.Round(Math.Min(1.0, Math.Max(0.0, channel)) * ByteMax, MidpointRounding.AwayFromZero) / ByteMax;
            return c <= SrgbEncodedLimit
                ? c / SrgbLinearSlope
                : Math.Round(Math.Pow((c + SrgbOffset) / SrgbScale, SrgbExponent), StableDecimals, MidpointRounding.AwayFromZero);
        }

        // The sRGB transfer function's constants (IEC 61966-2-1) and a byte's
        // range: standards, not look parameters.
        private const double ByteMax = 255.0;
        private const double SrgbEncodedLimit = 0.04045;
        private const double SrgbLinearSlope = 12.92;
        private const double SrgbScale = 1.055;
        private const double SrgbOffset = 0.055;
        private const double SrgbExponent = 2.4;

        /// <summary>The decimals the decoding power is rounded to, as the painter's (<c>ArtPainter.StablePow</c>).</summary>
        private const int StableDecimals = 9;
    }
}
