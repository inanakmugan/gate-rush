# Module 36 — Store

**Assembly:** `GateRush.UI`, `GateRush.Runtime`, `GateRush.Meta`, `GateRush.Platform`, `GateRush.UI.Editor` (tests in `GateRush.Tests`)
**Depends on:** Modules 30 to 35
**Phase:** 6.2

---

## Responsibility

Fill the Store tab and show the money loop a mobile puzzle game is built
on: gold runs short, the game offers more, the player buys it. The Store
sells gold packs for real money through a purchase service (D72), and
jokers and a lives refill for gold (D62). The first purchase service is
simulated and says so, so nothing is charged and nothing pretends to be.
Whenever a gold purchase is refused for want of gold, a prompt offers the
Store's gold packs.

In scope:

- `IPurchaseService` in `Platform`, and `SimulatedPurchaseService` in
  `Runtime`, which confirms each purchase on a clearly labelled test
  sheet.
- Gold packs as rules in `Meta`: each pack's product id and its gold.
  Bought gold goes into the balance, never into the lifetime gold (D22).
- The Store page: gold packs, the three jokers and the lives refill.
- The Not Enough Gold prompt and its "Get Gold" route to the Store, from
  the Store and from the Lives overlay.
- The test purchase sheet, and a message for a purchase that fails.
- The builder writes the new prefabs, and gives tinted graphics their
  `UiConfig` colour in the prefab so the Editor shows them as the game
  does.

Not in scope: real store integration (Google Play, App Store), receipts
and restoring purchases, bundles and offers that expire, the in-level
joker purchase when the gold is short (6.6), animations of gold arriving
(Phase 7).

---

## Public surface

```
IPurchaseService                    interface, Platform
    IReadOnlyList<StoreProduct> Products { get; }
    bool IsBusy { get; }            // a purchase is waiting for its outcome
    void Purchase(string productId, Action<PurchaseOutcome> done)

StoreProduct                        readonly struct, Platform
    string Id, string PriceText     // the price as the platform shows it

PurchaseOutcome                     enum, Platform
    Succeeded, Cancelled, Failed

SimulatedPurchaseService            sealed class, Runtime; IPurchaseService
    SimulatedPurchaseService(IReadOnlyList<StoreProduct> products,
                             Action<StoreProduct, Action<bool>> confirm)

MetaRules                           existing
    IReadOnlyList<GoldPack> GoldPacks   // product id, gold

MetaGame                            existing
    int ReportGoldPurchase(string productId)  // the gold added; throws for
                                              // an unknown id

Wallet                              existing
    void AddPurchased(int amount)   // the balance only, not the lifetime gold

FrontEndState                       existing, gains
    joker counts and prices, IsLivesFull,
    IReadOnlyList<GoldOffer> GoldOffers  // product id, gold, price text

IFrontEnd                           existing, gains
    event Action<string> GoldPackRequested
    event Action<JokerKind> JokerPurchaseRequested
    void ShowNotEnoughGold(int shortfall)
    void ShowPurchaseFailed()
    void ConfirmTestPurchase(GoldOffer offer, Action<bool> answer)

StoreScreen                         MonoBehaviour, UI: the Store page
StoreTexts                          static, plain C#, UI
```

Names are negotiable. That the screens hold no rules, that every number
on them comes from `FrontEndState`, and that a simulated purchase is
always labelled as one, is not.

---

## Design decisions (owner)

### The Store page

- A vertical scroll under the title, in three sections, each with a
  header:
  - **Gold:** one card per gold pack: the coin stack icon, the amount
    ("1,500"), a ribbon where the pack has one, and a green button with
    the price text from the purchase service ("$2.49").
  - **Jokers:** a card for each of the Clock, the Rocket and the Broom:
    its icon, its name, "You have N", and a green button with the coin
    icon and the gold price ("100").
  - **Lives:** a card with the heart, "Refill Lives", the lives as "3/5"
    or "Full", and a green button with the coin icon and the refill price.
    With full lives the button cannot be pressed and reads "Full".
- Under the sections, one line: "Test store: purchases are simulated and
  no money is charged." It is never hidden.
- Selecting the Store tab scrolls to the top, so the gold packs are the
  first thing seen. The gold "+" in the top bar selects the Store tab, as
  it does now.

### Gold packs (D72)

- Three packs, as configuration in `MetaConfig`:

  | Product id | Gold | Simulated price | Ribbon |
  |---|---|---|---|
  | `gold_small` | 500 | $0.99 | — |
  | `gold_medium` | 1,500 | $2.49 | Popular |
  | `gold_large` | 4,000 | $4.99 | Best Value |

- A pack's gold is a rule (`Meta`); its price text belongs to the purchase
  service, as it would with a real store. The simulated prices and the
  ribbons are configuration on the `Runtime` and `UI` side.
- A pack is shown only when both `Meta` and the purchase service know its
  product id. An id known to one and not the other is a configuration
  problem reported by `MissingSetup`.
- Bought gold is added to the balance only. Lifetime gold counts gold
  earned by playing, which the leaderboard ranks on (D22, D70); bought
  gold would let a player pay up the ranking.

### Buying a gold pack

1. The player presses a pack's price button: `GoldPackRequested(id)`.
2. The bootstrap asks the purchase service. The simulated service calls
   its confirm delegate, and the bootstrap passes it on to
   `ConfirmTestPurchase`.
3. The **test purchase sheet** opens over everything: the title "Test
   Purchase", the gold and the price ("1,500 gold for $2.49"), the line
   "This is a simulated store. No money is charged.", and two buttons,
   **Buy** and **Cancel**. Back and the close button count as Cancel.
4. **Succeeded:** `MetaGame.ReportGoldPurchase(id)` adds the gold, the
   bootstrap saves and refreshes. The player stays on the Store.
   **Cancelled:** nothing changes. **Failed:** the **message** prompt
   opens with "Purchase failed" and "Nothing was charged. Please try
   again.", and an OK button.
5. While a purchase is pending (`IsBusy`), another pack press is ignored.

A real store would replace the sheet with its own, through a new
`IPurchaseService` implementation; no screen changes.

### Buying jokers and refilling

- A joker's button raises `JokerPurchaseRequested(kind)`; the bootstrap
  calls `MetaGame.TryBuyJoker`. **Bought:** save and refresh, and the
  card's count goes up. **NotEnoughGold:** the Not Enough Gold prompt.
- The refill button, on the Store and on the Lives overlay, raises
  `RefillRequested`, handled as today. It can now be pressed whenever the
  lives are not full, whatever the balance: **NotEnoughGold** opens the
  Not Enough Gold prompt. `FrontEndState.CanRefill` gives way to
  `IsLivesFull`.
- One press is one purchase; there is no confirmation for gold
  purchases, as in the reference game.

### The Not Enough Gold prompt

- An overlay: the title "Not Enough Gold", the coin stack icon, the line
  "You need N more gold." with the shortfall the bootstrap computes from
  the price and the balance, a green **Get Gold** button and the red close
  button.
- **Get Gold** selects the Store tab, which closes every overlay and
  scrolls to the gold packs. The player then buys gold and comes back to
  what they wanted; nothing is bought for them on return.
- The shortfall is worked out in `Runtime` from `MetaRules`, not on the
  screen.

### Overlays and navigation

- Two new overlays join the stack: **Message** (title, text, one button)
  and **Test Purchase**. The Not Enough Gold prompt is its own overlay.
  They open over whatever is showing, the Lives overlay included.
- The test purchase sheet is always on top while open; selecting a tab
  cannot happen under it, since its scrim takes every tap.

### The builder

- The builder writes the Store page's content, the gold pack card and the
  three new overlays from the hand-made buttons, as it does today. A gold
  pack card is a prefab the Store page instantiates per offer, as the
  level grid does with Level Button.
- Every graphic with a `UiTint` is given its role's colour from
  `UiConfig` when it is built, so the prefab shows a red heart and a gold
  coin in the Editor; `UiTint` still applies the colour at runtime.
- Rebuilding writes the Store page, Front End and the new prefabs. After
  a rebuild, `LevelBootstrap`'s Front End field is checked again (Module
  35).

### Configuration

- `MetaConfig`: the gold packs (product id, gold), checked in
  `Problems()`: ids non-empty and unique, gold above 0.
- `Runtime`: the simulated products (product id, price text), checked the
  same way.
- `UiConfig`: every new text and format ("You have {0}", "You need {0}
  more gold.", the section headers, the sheet's lines, the test store
  line, the ribbons by product id), checked in `Problems()`.

---

## Left to you

- Where the simulated products and the ribbons live, within the rule that
  each is configuration with checks.
- The joker icons on the Store: reuse what the joker bar draws, or a
  sprite from the imported packs. If neither fits, say so in the plan and
  the owner adds one.
- Whether the joker and refill cards are fixed in the Store page or
  instantiated like the gold pack cards.
- How `FrontEndState` carries the joker counts and prices.
- The prefab hierarchies and their sizes, within the decisions above;
  give them in the plan as for Module 35's builder.

---

## Tests

**`Wallet`, `MetaGame`, `MetaRules`**
- `AddPurchased` raises the balance and leaves the lifetime gold; a
  negative amount throws; the sum stops at `int.MaxValue`.
- `ReportGoldPurchase` adds the pack's gold to the balance only and
  returns it; an unknown id throws and changes nothing; the streak, the
  lives, the jokers and the campaign never change.
- The gold packs come from the config; a pack with an empty or repeated
  id, or no gold, is reported.

**`SimulatedPurchaseService`**
- Confirm true gives `Succeeded`, false gives `Cancelled`, an unknown id
  gives `Failed` without asking. `IsBusy` is true until the outcome is
  given, and a purchase while busy is refused. `done` is called exactly
  once.

**`FrontEndState`**
- The joker counts and prices, `IsLivesFull`, and the gold offers: only
  packs known to both sides, in the rules' order, with the service's
  price text.

**`StoreTexts`**
- The gold amount with thousands separators, "You have N", the
  shortfall line, the price with the coin; every text from the config.

**`UiNavigation`**
- The new overlays stack and close with Back; Get Gold selects the Store
  and closes every overlay.

**Builder**
- The new prefabs are written with every field wired; the tinted graphics
  carry their `UiConfig` colour; the hand-made prefabs stay unwritten.

**By hand (owner)**
- The Store shows three gold packs with their ribbons, the three jokers
  with their counts, the refill, and the test store line.
- Buying a gold pack opens the test sheet; Buy adds the gold and saves
  it across a restart; Cancel and Back change nothing.
- Buying a joker with enough gold raises its count and lowers the gold;
  without enough, Not Enough Gold opens with the right shortfall, and Get
  Gold lands on the gold packs.
- The refill on the Store and on the Lives overlay: refused when full;
  Not Enough Gold when short; fills the lives otherwise.
- The leaderboard's lifetime gold does not grow from a gold purchase
  (checked in the save until 6.5 shows it).
- The layout holds on a tall phone and in the web build's window.
