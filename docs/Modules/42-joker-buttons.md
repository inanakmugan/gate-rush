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
