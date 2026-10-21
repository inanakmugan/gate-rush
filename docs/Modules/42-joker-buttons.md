# Module 42 — Joker buttons and icons

**Assembly:** `GateRush.UI`, `GateRush.UI.Editor` (tests in `GateRush.Tests`)
**Depends on:** Modules 36, 37, 40
**Phase:** 6.8

---

## Responsibility

Fix three things the owner found in play, all visual, none touching a
rule or a flow:

- The Rocket and Broom icons (Module 36's stand-ins) do not say what they
  are. They, the Clock and the settings cog are replaced by clearer icons
  from game-icons.net (D69's CC BY 3.0 exception).
- The joker buttons become square, as in the reference game's layout:
  the icon large and centred, the count or price in a small red badge on
  the bottom-right corner.
- The icon of every round button sits low on its face, because the
  button sprite has a darker depth strip at its foot; the owner raises it
  on the hand-made Button Round.

Not in scope: new colourful art (a later module), any change to what the
joker buttons do (D65 to D67, D74), the HUD's other parts.

---

## Design decisions (owner)

### Icons

- `Assets/ThirdParty/GameIconsNet/`: `hourglass.png` (Clock),
  `rocket.png` (Rocket), `magic-broom.png` (Broom), `cog.png` (settings),
  white on transparent, 512 by 512, with `License.txt`.
- The joker icons are set in `JokerIcons.asset`, so the joker bar, the
  Store's cards and the streak screens all change at once.
- The cog replaces the gear on the hand-made Top Bar; the owner sets it.

### The joker button

- A square green button (UI Pack `Green/button_square_depth_gradient`,
  with a disabled look in grey), sized to the bottom band as today.
- The icon fills most of the face, centred on the face above the depth
  strip, white, greyed with the button when it cannot be pressed.
- The badge: a small red rounded square on the bottom-right corner,
  overlapping the button's edge, with the count in white; in the campaign
  with none held, the coin and the price, wider as needed. A short price
  keeps its own colour (Module 40).
- The selected joker stays raised with its ring (D67).
- The badge's texts, colours and the layout come from `UiConfig` and the
  builder's constants as today.

---

## Left to you

- The sizes within the band, the badge's size and overlap, and how the
  badge grows for a price.
- Whether the square sprite needs the owner to set its import settings;
  list it if so.

---

## Tests

- The builder test for the joker bar checks the square sprite, the
  centred icon and the corner badge wiring; nothing else changes.

**By hand (owner)**
- The joker bar shows three square buttons with the new hourglass,
  rocket and broom icons and the count in a red corner badge; in the
  campaign with none held, the badge shows the coin and the price.
- Selecting the Rocket raises it with its ring; a button that cannot be
  pressed is grey.
- The Store and the Win Streak screens show the new icons.
- The Top Bar shows the cog, centred on its button; every round button's
  icon sits in the middle of its face.

---

## Resolved during implementation

- **Icons.** `JokerIcons.asset` holds the hourglass, rocket and magic
  broom, so the joker bar, the Store and the streak screens changed
  together; the owner reassigned them by hand. The cog replaces the gear
  on the Top Bar's settings button.
- **The joker button.** Square, green, grey when it cannot be pressed,
  with the icon centred above the depth strip. The corner badge's colour
  is a new `UiTint` role, JokerBadge, from `UiConfig`; a short price
  keeps its own colour.
- **Round buttons.** On the hand-made Button Round the root stays at 0
  and the Icon child sits at Pos Y 3, centring every round button's icon
  on its face. The Top Bar's plus icons also sit at Pos X -1, because the
  Kenney plus is drawn off-centre in its file.
- **Lives.** The Top Bar's heart is 96 by 96 and the count inside it is
  size 38 with a bottom margin of 6.
- **Coins, added in play.** The Kenney coin stand-ins read as empty
  outlines at small sizes. `Assets/Art/Sprites/UI/coin.png` (a filled
  coin with a star) and `coins.png` (two piles) are the project's own,
  drawn in grey and tinted by the Coin role, imported with Max Size 512;
  the builder reads them for every built prefab.
- **The player's name, added in play.** `TopBar` gains `nameLabel`, filled
  from the front end state and reported by `AddMissing` when unset. On
  the hand-made Top Bar the avatar sits inside a 110 by 110 Avatar Frame
  (`avatar_frame.png`, the project's own, in the greys of
  `button_square_flat`, tinted like the pills), inset by 8. The name sits
  on a 130 by 34 Name Tag, a sliced `button_square_flat` in the pills'
  colour, centred on the frame's bottom edge; Fredoka, auto size 16 to
  24, no wrapping, an ellipsis. The frame and the tag ignore raycasts, so
  the avatar still opens Profile.
- **Tests.** 9 new; 2,553 in all.
