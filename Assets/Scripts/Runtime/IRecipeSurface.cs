using System;
using System.Collections.Generic;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// How a generated sprite is drawn (Module 46): as it is, through the lit
    /// material, which multiplies its stored shade back by the art recipe's
    /// headroom, or through the specular material, which adds its white.
    /// </summary>
    internal enum SpriteLook
    {
        /// <summary>As it is, with <c>Sprite-Unlit-Default</c>, tinted.</summary>
        Plain,

        /// <summary>A lit part, its shade or its colours stored divided by the headroom, tinted.</summary>
        Lit,

        /// <summary>A lit part's white highlight, added.</summary>
        Highlight
    }

    /// <summary>
    /// What <see cref="BoardRecipes{TGroup, TPiece}"/> draws on: the handful of
    /// ways one piece is put down, which the board does with
    /// <c>SpriteRenderer</c>s in the world (<see cref="BoardView"/>) and an
    /// introduction card with <c>Image</c>s on the canvas
    /// (<see cref="MechanicIllustration"/>). A recipe says which pieces a
    /// block, a mark or an edge feature is made of, where and how large; a
    /// surface only knows how its medium shows one of them.
    /// </summary>
    /// <remarks>
    /// <para><b>Units.</b> Every position and size is in cells, in the frame
    /// of the group the piece goes under; the surface converts to its own
    /// units. A group's frame starts at its centre.</para>
    /// <para><b>Order.</b> A recipe adds its pieces from the bottom up and
    /// gives each its sorting order. A surface whose medium has sorting orders
    /// uses the order; one that draws in hierarchy order, as uGUI does, relies
    /// on the sequence. A recipe must therefore keep both true.</para>
    /// </remarks>
    /// <typeparam name="TGroup">What holds pieces: a transform.</typeparam>
    /// <typeparam name="TPiece">One drawn sprite.</typeparam>
    internal interface IRecipeSurface<TGroup, TPiece>
        where TGroup : class
        where TPiece : class
    {
        /// <summary>
        /// An empty group named <paramref name="name"/> under
        /// <paramref name="parent"/>, centred on <paramref name="center"/> and
        /// scaled by <paramref name="scale"/> about it.
        /// </summary>
        TGroup Group(TGroup parent, string name, Vector2 center, float scale);

        /// <summary>
        /// One piece per quarter of <paramref name="tiles"/>, in their order,
        /// drawn with <paramref name="layer"/>'s sprite and material for its
        /// piece and posed as its tile says over the rectangle
        /// <paramref name="rectOf"/> gives it.
        /// </summary>
        List<TPiece> Quarters(
            TGroup parent, IReadOnlyList<QuarterTile> tiles, Func<QuarterTile, Rect> rectOf, QuarterLayer layer, Color color,
            int order);

        /// <summary>
        /// <paramref name="sprite"/> stretched over <paramref name="size"/>
        /// and then turned by <paramref name="rotationDegrees"/>, drawn as
        /// <paramref name="look"/> says.
        /// </summary>
        TPiece Sprite(
            TGroup parent, string name, Sprite sprite, Vector2 center, Vector2 size, Color color, int order,
            float rotationDegrees, SpriteLook look);

        /// <summary>
        /// A 9-sliced <paramref name="sprite"/> over <paramref name="size"/>
        /// whose left slice border draws <paramref name="border"/> wide, capped
        /// at half the smaller side, drawn as <paramref name="look"/> says.
        /// Draws nothing, and returns null, when the border is not positive.
        /// </summary>
        TPiece Sliced(
            TGroup parent, string name, Sprite sprite, Vector2 center, Vector2 size, float border, Color color, int order,
            SpriteLook look);

        /// <summary>
        /// <paramref name="sprite"/> repeated over <paramref name="size"/>,
        /// drawn so that one tile's width — or its height, when
        /// <paramref name="tileIsMeasuredByHeight"/> — covers
        /// <paramref name="tileCells"/>, as <paramref name="look"/> says.
        /// </summary>
        TPiece Tiled(
            TGroup parent, string name, Sprite sprite, Vector2 center, Vector2 size, float tileCells,
            bool tileIsMeasuredByHeight, Color color, int order, SpriteLook look);

        /// <summary>
        /// A 9-sliced <paramref name="sprite"/> drawn as a strip
        /// <paramref name="length"/> long and <paramref name="thickness"/>
        /// across, along x, or along y when <paramref name="isAlongY"/>: the
        /// sprite's whole extent across covers the thickness, and only its
        /// sliced middle stretches along the length. Never turned, so a lit
        /// sprite keeps its light.
        /// </summary>
        TPiece Strip(
            TGroup parent, string name, Sprite sprite, Vector2 center, float length, float thickness, bool isAlongY,
            Color color, int order, SpriteLook look);

        /// <summary>
        /// <paramref name="text"/> in the label font, centred on
        /// <paramref name="center"/>, at the size the board's label of
        /// <paramref name="boardFontSize"/> has on a cell, with the label
        /// shadow's soft drop shadow when <paramref name="isShadowed"/>.
        /// </summary>
        void Label(TGroup parent, string name, Vector2 center, string text, float boardFontSize, Color color, bool isShadowed);
    }
}
