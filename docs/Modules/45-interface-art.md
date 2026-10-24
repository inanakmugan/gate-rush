# Module 45 — Interface art

**Assembly:** art under `Assets/Art/Sprites/UI`, `GateRush.UI`, `GateRush.UI.Editor` (tests in `GateRush.Tests`)
**Depends on:** Modules 35 to 43
**Phase:** 7.4

---

## Responsibility

Replace the interface's flat Kenney look with a richer, glossy one in the
spirit of the Turkish puzzle studios the portfolio is aimed at, as D77
decides. The art is generated from the style guide below with an image
generator (ChatGPT), by the owner, a few sheets at a time; each sheet is
cut, cleaned, trimmed, given its 9-slice borders and sized before it
enters the project. The joker bar grows at the same time (the Samsung M33
note in ROADMAP, Phase 7).

In scope:

- The style guide and the prompts, kept in this file so later art matches.
- The interface's buttons, panels, pills, ribbon, bars, badge, avatar
  frame and its coloured icons, as new sprites.
- The builder and the hand-made prefabs moved to the new sprites.
- The joker bar about 1.4 times larger, raised off the bottom edge.
- A painted background for the front end, in three moods that change as
  the campaign goes on.

Not in scope: the board's own art (blocks, gates, frame, mechanics; D46),
which is drawn by code from tiles and cannot come from an image
generator; whether the blocks need an outline or palette closer to the
new interface is looked at once this module is in, as a code change. Also
out: the avatars, the fonts, effects and animation (7.5), sound (7.6).

---

## The style guide

**The look.** Chunky, rounded, glossy, friendly. Every shape has:

- a thick dark outline, deep purple `#2A1650`, about 6 percent of the
  shape's height;
- a flat, front-facing view: no perspective, no tilt;
- a soft vertical gradient on the face, lighter at the top;
- a glossy highlight: a white band across the top third, about 35 percent
  opaque, with rounded ends, inset from the outline;
- a darker "lip" along the bottom, about 12 percent of the height, in a
  darker shade of the face colour, so a button reads as raised;
- corners rounded to about a quarter of the short side;
- no texture, no noise, no drop shadow outside the outline, no text.

**The palette.** Faces and their lips:

```
Green   face #3CCB4A   lip #23963A    primary actions: Play, Next, Buy
Blue    face #36A3F0   lip #1E6FC2    secondary actions, round buttons, tabs
Red     face #F04B5A   lip #B52637    Close, Lose a life, badges
Yellow  face #FFC531   lip #D98A00    gold, highlights
Grey    face #A9A6BF   lip #77738F    disabled
Panel   face #F3EEFF   lip #CFC4EE    pop-up panels (light)
Pill    face #3A2370   lip #241446    counters on the purple background (dark)
Outline #2A1650        Background #5C33A0
```

**Icons.** The same outline, gradient and highlight, in full colour, drawn
as a single object, centred, filling about 80 percent of a square with
even padding. Small glyphs on buttons (plus, check, cross, return,
sound, phone) stay white Kenney glyphs: on the new buttons a white glyph
is what the genre uses.

**Generation.** Square images, 1024 by 1024, transparent background. A
sheet holds several pieces of one kind, laid out on a grid with wide gaps,
so their outline weight, gloss and colours match. Nothing on a sheet may
touch another piece.

---

## The sprites

Each file goes under `Assets/Art/Sprites/UI`, imported as Sprite (2D and
UI), Single, Max Size 512 unless noted. Buttons, panels, pills, the
ribbon, the bar and the badge are 9-sliced; their borders are given in
the batch notes when the sheet is cut.

**Buttons** (pre-coloured, not tinted):
`button_rect_green`, `button_rect_blue`, `button_rect_red`,
`button_rect_grey`; `button_round_green`, `button_round_blue`,
`button_round_red`, `button_round_grey`; `button_square_green`,
`button_square_blue`, `button_square_grey`.

**Frames:** `panel` (light pop-up panel), `pill` (dark counter), `ribbon`
(title banner), `bar_track` and `bar_fill` (the streak bar), `badge` (the
small red rounded square), `tab_bar` (the bottom bar's strip),
`avatar_frame` (redrawn round frame).

**Icons** (full colour): `coin`, `coins`, `heart`, `fire`, `hourglass`,
`rocket`, `broom`, `trophy`, `shop`, `home`.

The generator's quota ran short, so the cog, the lock and the medals keep
their current icons: the cog is a white glyph on a round button, as the
genre draws it, and the lock and the medals stay tinted. They can be
redrawn later in the same style without a code change.

**Backgrounds:** `bg_town_day`, `bg_town_sunset`, `bg_town_night` under
`Assets/Art/Backgrounds`, 1024 by 1536. The sunset is the generated
painting; the day and the night are colour grades of it, the night with
lit windows and stars.

The Kenney files and the game-icons.net icons stay in the project until
nothing uses them; this module removes what it replaces from use, and the
README credits only what is still shown.

---

## Design decisions (owner)

- Pre-coloured sprites are drawn white-tinted: the `UiTint` roles Coin,
  Heart and Streak, and the medal colours, turn white in `UiConfig`, or
  their images stop being tinted; the plan says which. The Icon and
  IdleTabIcon roles stay for the white glyphs.
- Text on a button stays white with the theme's outline; text on a light
  panel stays the title purple.
- The joker bar: its buttons about 1.4 times their size today, the badge
  with them, more room between them, and the bar raised a little off the
  bottom edge, within the bottom band (`ScreenBands`). The board still fits
  between the bands on a tall phone and in the web build's window.
- The front end's background is the painting, filling the screen behind
  everything and cropped, never stretched: it covers the screen whatever
  its shape, centred. The level keeps its plain gradient, so the board
  stays easy to read.
- Which painting shows follows the campaign: day for levels 1 to 10,
  sunset for 11 to 20, night from 21 and after the campaign is over. The
  levels are configuration, in `UiConfig`, as a list of the level each
  painting starts at; the front end picks the last one the player has
  reached.
- The hand-made prefabs (Button Primary, Button Secondary, Button Round,
  Button Close, Level Button, Top Bar, Tab Bar, Home Page) are changed by
  the owner in the Editor, with steps given in the report; the builder
  writes the rest.

---

## The prompts

Every prompt starts with the shared style block, then the sheet's own
part. Generate one sheet, check it, then the next; a sheet that drifts
from the first is generated again with "match the previous sheet's
outline weight and gloss exactly".

**Shared style block**

```
Mobile puzzle game UI asset sheet, glossy casual style. Flat front-facing
view, no perspective. Every piece has a thick dark purple outline
(#2A1650), a soft vertical gradient lighter at the top, a white glossy
highlight band across the top third at about 35% opacity with rounded
ends, and a darker lip along the bottom edge so it looks raised. Rounded
corners. No text, no letters, no numbers, no drop shadow outside the
outline, no texture, no background scene. Transparent background.
Pieces laid out on a clear grid with wide empty gaps; no piece touches
another. 1024x1024.
```

**Sheet 1 — rectangle buttons.** Four wide rounded-rectangle buttons,
about 3:1, one per row: green face #3CCB4A with lip #23963A; blue face
#36A3F0 with lip #1E6FC2; red face #F04B5A with lip #B52637; grey face
#A9A6BF with lip #77738F. Empty faces.

**Sheet 2 — round and square buttons.** Top row: four round buttons,
green, blue, red, grey, same colours as sheet 1. Bottom row: three square
buttons with rounded corners, green, blue, grey. Empty faces.

**Sheet 3 — frames.** A light pop-up panel (face #F3EEFF, lip #CFC4EE),
square; a dark counter pill (face #3A2370, lip #241446), about 3:1; a
red ribbon banner (face #F04B5A) with folded tails at both ends, about
4:1; a long thin bar track (face #3A2370) and a long thin bar fill
(orange #FF8C1A) of the same size; a small red rounded-square badge; a
round frame ring (face #3A2370) with an empty transparent centre.

**Sheet 4 — icons, part 1.** Five game icons in full colour, each
centred in its own square cell: a gold coin with a star embossed on it;
a small pile of three gold coins; a glossy red heart; an orange-yellow
flame; an hourglass with a purple frame and golden sand.

**Sheet 5 — icons, part 2.** Five game icons: a small red and white
rocket pointing up and to the right with an orange flame; a magic broom
with a brown handle, yellow bristles and a few sparkles; a gold trophy
cup; a small shop stall with a striped red and white awning; a house
with a red roof.

**Sheet 6 — icons, part 3.** Six game icons: a grey-blue gear cog; a
golden padlock, closed; three round medals with ribbons, gold, silver
and bronze.

---

## Left to you

- How the builder moves to the new sprites, and whether the sprite paths
  stay constants in the builder or move to configuration.
- Whether the tinted roles turn white or the images stop being tinted.
- The joker bar's exact sizes within the band, as long as the board
  still fits.
- Where the background image sits in Front End, and how the front end
  learns the campaign level from `FrontEndState`.
- The prefab changes and sizes, given in the plan as before.

---

## Tests

**Builder**
- Each built prefab uses the new sprites where this module replaces
  them; the joker buttons, badge and spacing have the new sizes; Home
  Page and Top Bar stay unwritten.

**`UiConfig`, `ScreenBands`**
- Any changed colour is checked as before; the larger bottom band still
  leaves the board its minimum room.
- The background list: empty, a missing sprite, or start levels not
  rising are reported; the painting for levels 1, 10, 11, 20, 21 and a
  finished campaign is the right one.

**By hand (owner)**
- Every screen shows the new buttons, panels, pills and icons, with no
  Kenney piece left where a new one exists.
- Pressed and disabled buttons read clearly.
- The joker bar's buttons are larger and clear of the bottom edge on a
  tall phone and in the web build's window; the board is not squeezed.
- 9-sliced pieces stretch without blurring their corners.
- Home shows the day town on a new save, sunset from level 11 and night
  from level 21; the painting covers a tall phone and the web build's
  window without stretching.

---

## Resolved during implementation

- **The sprites.** Everything listed above was drawn except `tab_bar`.
  The quota came back, so the cog, the lock and the three medals were
  generated after all, and a gold selection ring (`ring_select`) was
  added for the selected joker and the next streak milestone. The note
  above about keeping the old cog, lock and medals no longer holds.
- **Avatars.** Thirty 256 by 256 animal avatars were generated in the
  same style and replace Animal Pack Redux in `AvatarCatalog`, although
  the spec left the avatars out (D77 has the note).
- **Import.** `InterfaceArtImportRules` holds every sprite's import
  settings and 9-slice borders, and `InterfaceArtImporter` applies them
  on import, so a re-cut sprite needs no Inspector work.
- **Tints.** The pre-coloured sprites stop being tinted rather than being
  tinted white: the `UiTint` roles Coin, Heart and JokerBadge are
  removed, and the remaining roles keep their numbers. The Streak role
  and its colour are unused since the streak markers became round, and
  stay until a cleanup.
- **The scrim.** Black at 0.85 alpha: the project renders in linear
  colour space, where the planned lighter scrim looked washed out.
- **The joker bar.** First 1.4 times larger as planned. After the
  owner's review it was designed 20 percent larger again, with half a
  button between buttons and the three grouped in the centre, a lock
  half the button's size and locked icons at 0.7 alpha. The bottom band
  grows from 0.14 to 0.145, the most the board-fit floor of 0.9 allows
  (level 4 comes out at about 0.902, and `CampaignBoardFitTests` checks
  every campaign board). What is drawn is held by the band: about 16
  percent larger on a tall phone and 7 percent in the web window.
- **Hand-made prefabs.** The owner's Inspector steps were replaced by
  one permanent command, `HandMadePrefabSettings` (*Gate Rush → Apply
  Hand-Made Prefab Settings*, D78). It sets Button Round's glyph box,
  the Top Bar's plus icons, heart, lives count, avatar frame and avatar,
  the Tab Bar's labels and the Home Page's Play button and notice.
- **The owner's review.** Close buttons sit 40 in from every overlay's
  corner, like the introduction cards. White text on the painting uses a
  generated Fredoka material with a purple outline and a soft underlay,
  kept within the font atlas's padding. Scrolling lists (Store,
  Leaderboard, Levels, Profile) fade over 40 units at their edges, with
  padding so nothing is faded at rest; the Win Streak ladder does not
  scroll and keeps a hard edge. Smaller fixes: the Lives overlay's
  stack, the Settings icons shifted by their measured drawn centres,
  one tab label size that fits "Leaderboard", the Play button low above
  the tab bar, the Store's text-only "Full" label centred on the button's
  face, disabled labels in the theme's dark purple, and shorter or
  corrected texts.
- **Third-party art.** Still shown: Kenney's white Game Icons glyphs
  (plus, cross, home, return, sound on and off, phone, checkmark) and the
  UI Pack's green checkmark. Animal Pack Redux, Board Game Icons and the
  game-icons.net icons are no longer shown; the game-icons.net cog is
  only a hidden leftover override on the Top Bar's settings button. The
  unused files stay in the project until the cleanup in 7.8, and README
  credits only what is shown. *Later (7.8):* the unused files and the
  cog override are removed.
- **Numbering.** The board's art becomes its own module, 7.5 (Module
  46), so effects move to 7.6 and sound balance to 7.7.
- **Tests.** 2,707 after the main implementation, 2,764 at the end.
