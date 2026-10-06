# Module 20 — Layered blocks and editor marks

**Assembly:** `GateRush.Runtime` and `GateRush.Editor` (tests in `GateRush.Tests`)
**Depends on:** Modules 09, 15, 16, 18, 19
**Phase:** 2.4e

---

## Responsibility

Give layered blocks (M4) their real look, which 2.4a and 2.4b both left
for later: today a layered block still shows 2.1's placeholder, one small
square of the colour beneath in every cell, so a 2×2 layered block reads
as four blocks. After this module a layered block reads as **one block of
its outer colour with one block of the colour beneath set inside it**,
following the footprint, bends included. The Level Editor shows layered
and frozen blocks for what they are.

In scope:

- The board's layered look, its peel, and the Layered Block card's
  illustration.
- The layer count (stacks deeper than two) on the shared badge, placed
  with the other marks.
- The Level Editor grid: layered blocks show the colour beneath and the
  depth; frozen blocks show as ice with their count (known debt).

Not in scope: any rule change (M4 is unchanged), new animations beyond
the peel, the rest of the editor's known debt.

---

## Public surface

```
LayerInset                          plain C#, Runtime
    static Rect QuarterRect(QuarterTile tile, float insetCells)
        // the quarter's rect with its outward sides moved inward by
        // insetCells; inner sides untouched, so neighbouring quarters still
        // meet and the inner shape stays one piece

EditorLayerInset                    plain C#, Editor (or shared helper)
    the editor grid's inset rects for a footprint, by the same rule
```

Names are negotiable. That the inner shape is the footprint's own quarter
tiling pulled inward — one piece, bends and concave corners included —
and not a mark per cell, is not.

---

## Design decisions (owner)

### The look on the board

- The block's face is drawn in its **outer colour**, as today: lip, quarters.
- Inside it sits the **inner shape**: the same quarter tiles in the
  **colour beneath**, each pulled inward on its outward sides by a
  configured inset (`layerInsetCells`), so the outer colour shows as an
  even rim around a single inner piece. A 2×2 shows one rounded square in
  the middle; an L shows one L.
- The inner shape carries the block's **studs**, tinted in the colour
  beneath, and a thin darker edge along its outer boundary (the lip rule
  applied to the beneath colour) so it reads as set into the block. The
  outer rim has no studs.
- An axis-restricted layered block shows its arrow on top, as today.
- **Depth.** A stack deeper than two shows the number of colours left on
  the shared count badge (Module 16), placed through `MarkLayout` with the
  block's other marks (key, padlock, time bonus), so it never overlaps
  them. This retires the known debt "layer numeral overlaps the padlock".
- **Frozen.** A frozen layered block is ice like any frozen block: no
  colour and no inner shape (M3 hides colours).
- The placeholder beneath squares, their config fields and sorting order
  go.

### The peel (Module 12, 18)

A layered block cleared at a gate loses its outer colour in place. The
outer rim fades and shrinks onto the inner shape while the inner shape
grows to the full footprint, so the colour beneath visibly becomes the
block; if a third colour lies under it, its inner shape appears inside as
the peel ends (the redraw already shows it). Timing stays
`peelSeconds` / `peelEase`.

### The card

The Layered Block card's illustration (Module 19) follows the new look;
`MechanicIllustration` mirrors `BoardView` as before.

### The Level Editor

- A layered block's cells show the **outer colour** with an **inset
  rectangle of the second colour** drawn as one piece over the footprint
  (the same inset rule over cells, inner edges merged), and the stack's
  depth as a small number when it is deeper than two.
- A **frozen block** shows in a pale ice colour with its count, so the
  author sees at a glance which blocks are frozen. Its own colours stay
  visible in the properties panel.
- Editor colours and sizes stay in `LevelEditorSettings`; no magic numbers.

### Engine rules that carry over

- No magic numbers: inset, edge darkening and badge placement come from
  `RuntimeConfig`, `ArtRecipe` or `LevelEditorSettings`, with
  `Problems()` checks (the inset must leave a visible inner shape: below
  half a cell minus the quarter's corner radius).
- No new sprites unless the existing quarters cannot do it; say so if
  they cannot.
- The drag, lift outline, exit, peel and every Module 18 effect keep
  working: every new part sits under the block's body.

---

## Left to you

- How the inner shape's edge is drawn (a second, slightly larger inner
  tiling in the darker colour under it, or another way that keeps it one
  piece).
- How the peel's rim and inner shape are animated within the timing.
- How the editor draws the inset (handles, rects, a texture).
- Starting values for every new field. The owner tunes them.
- The exact Editor steps for the owner.

---

## Tests

**`LayerInset`**
- A fill quarter is unchanged; an outer corner moves in on both outward
  sides; an edge along x moves in only on its y side.
- Over a 2×2 the inset quarters cover one connected region, the footprint
  shrunk by the inset on every outer side (the central decision: one
  piece, not four).
- Over an L the inset region is connected through the bend.

**`VisibilityLayer` / marks**
- A frozen layered block reports no colour beneath.
- A 1×1 layered locked block places the layer badge and the padlock
  without overlap.

**Editor**
- The editor's inset rects for a 2×2 layered block form one region; a
  single-colour block gets none.

**By hand (owner)**
- 1×1, 1×2, 2×2 and L layered blocks each read as one block with one
  inner block; a three-colour stack shows its count.
- Peeling at a gate turns the inner colour into the block; drag, lift,
  exit and restart still look right.
- The Layered Block card matches the board.
- In the Level Editor, layered blocks show their second colour and depth,
  and frozen blocks show as ice with their count.
