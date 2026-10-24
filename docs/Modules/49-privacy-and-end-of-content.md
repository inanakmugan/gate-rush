# Module 49 — Privacy text and the end of content

**Assembly:** `GateRush.Runtime` (the bootstrap's flow, `RuntimeConfig`), `GateRush.UI` and `GateRush.UI.Editor` (the overlays, `UiConfig`, the builder) (tests in `GateRush.Tests`)
**Depends on:** Modules 35, 38, 41, 44, 47
**Phase:** 7.8

---

## Responsibility

Two small pieces of the finished game that are missing. A player can
read, from inside the game, what it does and does not do with their
data. And a player who finishes the thirtieth level reaches a real end
screen that says so, promises more, and lets them play again from level
1 without losing anything.

In scope:

- A Privacy row in Settings that opens a short privacy text.
- An End of Content overlay in the front end, shown after the campaign
  is finished.
- Its Restart button: level 1 in free play, progress kept.

Not in scope: any change to the rules, the save format, the campaign's
levels or the economy; new art (the overlays use the existing frame,
buttons and icons); new sound cues; a privacy page outside the game (the
same text goes on the itch.io page by hand).

---

## Public surface

```
SettingsOverlay
    event Action PrivacyRequested          // the new Privacy row

UiConfig
    string PrivacyTitle, PrivacyText
    string EndOfContentTitle, EndOfContentLine, EndOfContentRestartLabel

EndOfContentOverlay                UI; a front end overlay
    event Action RestartRequested
    event Action CloseRequested

IFrontEnd
    void ShowPrivacy()
    void ShowEndOfContent()
    event Action PrivacyRequested
    event Action EndOfContentRestartRequested
```

Names are negotiable.

---

## Design decisions (owner)

### The privacy text

- Settings gains a **Privacy** row under Haptics, in the same style as
  its other rows. It opens the existing Message Overlay over Settings
  with a title and the text below; OK closes it back to Settings.
- The text, in `UiConfig` (so it can change without code), starts as:

  > Gate Rush collects no personal data. Your progress (levels, gold,
  > lives, your name and avatar, and your settings) is saved only in
  > this browser, on this device, and is never sent anywhere. There are
  > no ads, no analytics and no accounts. Purchases are simulated: no
  > payment is ever taken. The leaderboard is local, and its other
  > players are labelled demo players. Clearing this site's data resets
  > your progress.

- The text must stay true. If a later change sends anything off the
  device, the text changes in the same change.

### The end of content

- **When it shows.**
  - After the player wins the last campaign level and leaves the result
    panel by Next or Menu: Home opens, as today, and the End of Content
    overlay opens over it, after the screen fade has lifted and after any
    streak reward announcement (rewards first, then this).
  - Whenever Play is pressed on Home while the campaign is finished.
  - It replaces today's "All levels played" notice. Home's Play button
    keeps its "All Played" label.
- **What it shows.** The overlay frame of every overlay, with:
  - the title, "You finished every level!";
  - one line, "New levels are coming soon.";
  - a primary **Restart** button;
  - the close button in the corner, as other overlays have.
  The texts live in `UiConfig`.
- **Restart (owner's choice A: progress kept).** Starts level 1 (the
  first level of `CampaignLevels`) in **free play** (D58): it needs no
  life, costs none and pays nothing. Completed levels, the campaign
  position, gold, lives, jokers, the streak and the profile are exactly
  as before. It goes into the level through the screen fade, as Play
  does. From there the player continues with Next through the levels in
  free play, as free play already does.
- **Close** returns to Home with nothing else changed.
- The overlay opens and closes with the shared `OverlayMotion`, and its
  buttons carry `PressFeedback` (Module 47).

### Engine rules that carry over

- Texts in `UiConfig`, checked in `Problems()` (not empty).
- The overlay is built by `UiPrefabBuilder` like the other front end
  overlays and listed in `UiNavigation`'s overlays; the Settings row is
  built by the builder too. No Inspector steps.
- No mutable statics; every subscription undone; no magic numbers.

---

## Left to you

- The row's exact layout in Settings, matching the other rows.
- How the bootstrap decides to open the overlay after the final win
  (the campaign being over when Home opens from a level).
- Whether the overlay reuses the Message Overlay's layout with a second
  button or has its own.

---

## Tests

**Flow (plain code where it lives)**
- With the campaign finished, Play opens the End of Content overlay and
  starts nothing.
- Leaving the last campaign level's win by Next or by Menu opens Home
  and then the overlay; with a pending streak reward, the reward comes
  first.
- Restart starts level 1 in free play; the completed levels, campaign
  position, gold, lives, jokers and streak are unchanged afterwards.
  This test fails if Restart ever resets progress or starts a campaign
  attempt.
- Close leaves everything unchanged.
- With the campaign not finished, Play starts the campaign level as
  today and the overlay never opens.

**Settings and texts**
- The Privacy row raises `PrivacyRequested`; the front end opens the
  Message Overlay with the configured title and text over Settings.
- Every new text is reported by `Problems()` when empty.

**Builder**
- The End of Content overlay and the Privacy row are built and wired,
  with `OverlayMotion` and `PressFeedback`.

**By hand (owner)**
- Settings → Privacy shows the text and OK returns to Settings.
- Finishing level 30 in the campaign: the result panel, then Home with
  the overlay; Restart plays level 1 in free play with gold, lives and
  completed levels unchanged; Play on Home opens the overlay again.
