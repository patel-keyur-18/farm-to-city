# 030. The businesses hub, supply between your businesses, and UI fixes

- Status: Approved (the owner, 2026-11-26)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Businesses (founding and running), Production chains, UI/UX (HUD, sheets)
- Related ADRs: 0016, 0043, 0068, 0091, 0093, 0101, 0140, 0158, 0172
- Depends on specs: 008 (HUD and panels), 016 (businesses), 017 (shops), 006 (hands and orders), 028 (business growth)

## 1. Goal

From one sheet, the player sees every business at a glance (how it's doing, its money, where its goods come from) and steers it: take money out, send it your own produce or let it buy, and have a hand grow what the vegetable stall sells. The lesson: a business is only as good as its supply, and every coin moved between your farm and your businesses is a real sale.

## 2. Scope

**In:**
- **UI fixes (US-265):** Collect all hidden when nothing is ready; long tab rows scroll sideways; HUD badges that grow to fit; an Editor check that opens every sheet and flags anything spilling out of its box.
- **One business run by you (US-266):** the rule, its refusal, and the card for an older town that runs two or more.
- **The Businesses hub (US-267):** a HUD button and the hub sheet.
- **Supply (US-268):** each business's *Today's supply* (from you or bought, by good), and *Automatic* or *Manual* with a daily amount from you for each good.
- **Grow for the vegetable stall (US-269):** a hand's field order that sows what your stall sells.

**Out:**
- Two towns: spec 031 (Sprint 29).
- The graphics redesign: spec 032.
- In-game time travel: dropped for now (ADR 0172 §6).
- Supply plans for other businesses' hands (only the vegetable stall gets a growing order): a later ad hoc request if wanted.
- Changes to any price, wage, recipe or market rule: none. Every amount moved is priced as today.

## 3. Rules and formulas

### 3.1 One business run by you (US-266)

| Rule | Formula or behaviour | Units | Bounds |
| --- | --- | --- | --- |
| Run by you | At most one business with `Worker = Founder` and `Status ≠ Closed` | businesses | 0–1 for new actions |
| Founding | `FoundBusiness(runYourself: true)` is refused with `AlreadyRunOne` when one already is | – | – |
| Running yourself | `RunBusinessYourself` is refused with `AlreadyRunOne` when another already is | – | – |
| An older town | A town loaded with two or more run by you keeps them until the player answers the card (§7.3); `KeepRunningYourself(id)` keeps that one and sets every other to `Worker = None` (*Nobody yet*: it stays shut until hired, as today when a worker leaves) | – | the card has no close |
| Nothing else changes | Founder hours (`FounderHours`), output, wages, the plan: as today | – | – |

### 3.2 Collect all, tabs, badges (US-265; view only)

| Rule | Behaviour |
| --- | --- |
| Collect all | Shown only when `ReadyCount > 0`; it appears with the HUD's pop when something becomes ready and goes when the last is collected. Dadi's first-return letter ("Tap Collect all…") is unchanged: it is only shown when something is ready. |
| Tab rows | A tab row wider than its sheet scrolls sideways (touch drag), with a 60-unit fade at each edge that has more; the showing tab is scrolled into view when the sheet opens. Tabs size to their words (no 260 minimum). Same strip on every sheet. |
| Badges | A badge is a circle for one digit and a pill for two or more (height 54, padding 12), text never wraps; 100 or more shows "99+". People, Orders and the alerts bell. |
| Overflow check | An Editor tool opens every sheet the game can open (from a scripted town at each era) and lists any element whose box leaves its sheet's or any label whose text is cut; the list is fixed to empty before the story is done. |

### 3.3 The Businesses hub (US-267; view only)

| Rule | Formula or behaviour |
| --- | --- |
| The button | Shown once the town has a business with `Status ≠ Closed`, left of Orders (right 538, bottom 28, 150 round). |
| Needs you (!) | A business needs you when it is in trouble as `BusinessBooks.Short` says (open with nobody working it, or its overdraft used to the limit so its inputs stop). (As built, ADR 0174: the one-business card at load covers a town that runs several, so this is not a hub reason.) The badge counts businesses that need you. |
| Profit this season | `BusinessBooks.NetProfit(BusinessBooks.Report(b, sim.Period))`: the season so far, as its Books tab shows. The top line adds them up. |
| Cash | `b.Ledger.Cash`; the top line adds them up. |
| Stars | Shown for businesses that earn stars (the shops and the mill, ADR 0092); others show none. |
| Order | Open first, then building, then shut; within each by id. Closed (sold or wound up) businesses are not listed. |
| Take out | Opens `BusinessCashSheet` (*Take*) for that business; Back returns to the hub. |

### 3.4 Today's supply (US-268)

| Rule | Formula or behaviour | Units |
| --- | --- | --- |
| From you | Grams a business took today from your store, your herd or your other businesses (`FromStore`, `FromYours`, `MilkIn`, the mill's own wheat, the mill's atta to the bakery) | g a good |
| Bought | Grams it bought today at the market, from neighbours or at the import price (`FromMarket`, `Neighbours`, the mill's and a recipe's market buys) | g a good |
| Day | Counted from 6:00 (the morning round) to the next 6:00; shown as today's; yesterday's buying kept only as `BoughtDays` for US-269's tie-break (as built, ADR 0175) | – |
| Value | Both at the price actually paid (the books' own entries); the *Why?* names the price | coins |

### 3.5 Automatic or Manual (US-268)

| Rule | Formula or behaviour | Units | Bounds |
| --- | --- | --- | --- |
| Which businesses | Those that take a good from you by `FromStore`, `FromYours` or the mill's atta: the vegetable stall, the kirana, the restaurant, the bakery, and any shop selling your products | – | – |
| Automatic | Today's rule exactly: it takes from you all it needs, then buys the rest if *buy at the market* is on. The default; every existing business starts here | – | – |
| Manual | For each good: the most it takes from you a day: none, a quarter, half or all of its day's amount (`PutOut[g]` for a stall, `NeedOf` for a recipe), or a fixed amount in kg from `supplySteps` | g a day | 0 to its day's amount |
| Applying the cap | `fromYou = min(need, cap − fromYouToday[g])`, then the rest at the market if its switch allows. The cap never makes it take more than it needs | g | ≥ 0 |
| Mill, dairy, tea stall | Their own input already has a daily amount (`OwnPerDay`: *your wheat a day*, *your milk a day*); the Supply section shows it as that good's row and changes it with the existing command. No Automatic/Manual switch for them | – | as today |
| Unsent goods | What you don't send stays in your store, for the trader or your orders | – | – |
| Rounding | A share of the day's amount floors to the gram | g | – |

### 3.6 Grow for the vegetable stall (US-269)

| Rule | Formula or behaviour | Bounds |
| --- | --- | --- |
| The order | A field order whose crop is *For your vegetable stall* (crop id `stall:<id>`), with the stall's business id; one row in the crop list for each open stall of yours. Every season, like *Let him choose* (the same *this season only* or *every season* step). Seed and fertiliser as any field order. The sell rule is **Keep in store only** (owner, 2026-11-26): the hand never sells it to the trader, and the stall takes it at its 6:00 round by its Supply (Automatic: all it needs; Manual: its caps), which is the hand's sale to the stall at the day's market price | one stall per order |
| The stall's preorder | What the stall needs tomorrow of each vegetable it sells, worked out when asked and never saved: `max(0, PutOut − its own Stock − your store's free stock − what is growing or standing ready for it)`. *Growing for it* is the expected harvest of the crops on fields whose stall order is this stall's. It shows on the hand's sheet ("Stall preorder: okra 12 kg, spinach 8 kg") | grams, ≥ 0 |
| The pick, at each sowing | The in-season crops whose good the stall sells (`VegShare > 0`), that `MaySow` on the field and whose seed the cash can pay. Ranked: (1) the biggest unmet preorder; (2) most grams the stall bought at the market over the last `supplyLookbackDays` days; (3) the stall's day's amount (`PutOut`); (4) crop index. The first is sown. Growing crops already count against the preorder, so the fields spread across the vegetables by themselves | – |
| Nothing fits | No such crop in season or none affordable: he sows his best pick, as *Let him choose* (ADR 0050) | – |
| Nothing needed | Some vegetable fits but the stall has no preorder for any of them (its own stock, your store or what is growing covers it, or its cap takes none from you): he **waits** ("Waiting: the stall needs nothing more") and sows again when it lacks something (owner, 2026-11-26, review ADR 0177). A good capped at none is never grown for | – |
| Sown in turn | Each field is resown when its crop is in, so fields sown on different days keep harvests apart; nothing holds a field back | – |
| The harvest | Goes to your store as any harvest; the stall takes it at the morning round by its Supply (Automatic: all it needs) | – |
| The stall gone | If the stall is sold or closed, the order becomes *Let him choose* and the hand's sheet says so | – |

## 4. Data

**New or changed definitions:**

| Definition | Field | Type | Default | Bounds | Notes |
| --- | --- | --- | --- | --- | --- |
| `businesses.json` | `supplySteps` | long[] (g) | 5,000, 10,000, 20,000, 50,000 | > 0, ascending | Manual's fixed amounts a day (kg steps) |
| `businesses.json` | `supplyLookbackDays` | int | 2 | 1–8 | Days of the stall's market buying the order ranks by |

Optional keys: no key, the frozen golden content stays as it is.

**New or changed state (save schema 25):**

| Entity | Field | Type | Notes |
| --- | --- | --- | --- |
| `Business` | `SupplyManual` | bool | false (Automatic) |
| `Business` | `CapShare`, `CapGrams` | int[] and long[] by good | share (0, 25, 50, 100 %) or grams (> 0 wins); only in Manual (as built, ADR 0175) |
| `Business` | `FromYouToday`, `BoughtToday` | long[] by good | g; they turn at 6:00 (a lazy roll: the first count or read of a new supply day starts it, so no pass order matters). "Yesterday" is kept only as `BoughtDays` (as built, ADR 0175) |
| `Business` | `BoughtDays` | list of long[good], newest first, up to `supplyLookbackDays` | g bought on each day before today (the order's ranking, US-269) |
| `Business` | `SupplyUsed`, `SupplyDay` | bool, long | the state is hashed only once Manual or a cap has been set, so no town that never uses it moves its hash |
| `StandingOrder` | `ForBusiness` | long | the stall's id for an order that grows for it, else 0; its `Crop` stays −1 (the hand's choice), so the fallback to *Let him choose* needs no second path (as built, ADR 0176) |

Older saves load with Automatic, zero counters and no stall orders.

## 5. Commands and events

| Command | Parameters | Validation and rejection reasons | Effect |
| --- | --- | --- | --- |
| `FoundBusiness` | (unchanged) | + `AlreadyRunOne` when `runYourself` and you run one | – |
| `RunBusinessYourself` | (unchanged) | + `AlreadyRunOne` | – |
| `KeepRunningYourself` | at, businessId | `UnknownBusiness`; `InvalidArgument` unless it is run by you and you run two or more | the others to *Nobody yet* |
| `SetSupplyMode` | at, businessId, manual | `UnknownBusiness`; `InvalidArgument` for the mill, dairy and tea stall | sets Automatic or Manual |
| `SetSupplyCap` | at, businessId, good, share, grams | `UnknownBusiness`; `InvalidArgument` (a good it doesn't take from you, a share not 0/25/50/100, grams not a step) | sets the good's cap (Manual) |
| `SetStandingOrder` | (unchanged); the crop id `stall:<id>` carries the stall (as built, ADR 0176) | `InvalidArgument` without an open vegetable stall of yours, or when the sell rule is not Keep in store | the order |

| Event | Payload | Emitted when | Consumers |
| --- | --- | --- | --- |
| `RunYourselfChosen` | businessId, stopped ids | the card's answer | play log, Chronicle (none) |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects | Reversibility |
| --- | --- | --- | --- | --- |
| Which business you run | none | the others shut until hired | your hours, wages | hire, or switch later (free the one, run the other) |
| Manual supply | none | less from you: your store sells more to the trader; the business buys more at the market (dearer) or sells less | farm sales vs the business's cost of goods | back to Automatic any time |
| Grow for the stall | the sowing, as any order | the stall buys less at the mandi; your farm earns the stall's price | the field's crop choice | change the order |

## 7. UI

### 7.1 The HUD

```
                                    [Businesses] [Orders] [People] [Menu]
[Collect all · 3]   (only when something is ready)
```

Badges: `(1)` a circle, `(11)` a pill, `(99+)` the cap.

### 7.2 The Businesses hub

```
+--------------------------------------------------------------------+
| [<-] (pic) Businesses                                        (X)   |
|      Profit this season 42,300 (?)   ·   Cash 1,18,400 (?)          |
| [Start a business]                                                  |
|---------------------------------------------------------------------|
| (mill)  Flour mill · Open · Manoj (miller)  ★★★☆  !                |
|         Profit 18,200 (?) · Cash 64,000 (?)    [Take out] [See]    |
| (stall) Vegetable stall · Open · You           ★★★★                 |
|         Profit 9,100 (?) · Cash 21,000 (?)     [Take out] [See]    |
|         Today: 38 kg from you, 12 kg bought (?)                     |
+--------------------------------------------------------------------+
```

- *Start a business* opens today's Menu → Businesses start list.
- A row's ! names why under it ("Nobody works here", "Overdraft used up: it can't buy inputs", "You run two: choose one").
- *Why?* on: profit (its Books' lines), cash, stars, today's supply (each good, from you and bought, price paid), the totals.

### 7.3 The card: you run more than one

```
+----------------------------------------------------------+
| You can run only one business                             |
| Your day has room for one. Pick the one you keep running; |
| the others wait for a hire.                               |
| (mill)  Flour mill · profit 18,200      [Keep running]    |
| (tea)   Tea stall · profit 6,400        [Keep running]    |
+----------------------------------------------------------+
```

Shown at load, over everything, until answered.

### 7.4 A business's card: Supply

```
Supply                                   (o) Automatic  ( ) Manual
  Tomato     from you 12 kg · bought 4 kg (?)      [All it needs ▾]
  Potato     from you 20 kg · bought 0 kg (?)      [Half ▾]
  Pav        from your bakery 6 kg (?)             [All it needs ▾]
  Buy the rest at the market                        [On]
```

The amount buttons show only in Manual. The mill, dairy and tea stall show their *a day* row here instead.

### 7.5 The hand's order

The crop list of a field order gets *For your vegetable stall* under *Let him choose* (shown only with an open stall). The hand's sheet (as built, ADR 0176): the order reads "For your vegetable stall (Okra now) · Keep in store · …" and a line "Stall preorder: okra 61.4 kg, amaranth 30.7 kg".

Previews (exact-looking artifacts) of 7.2, 7.3, 7.4 and 7.5 come before their UI code.

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Given nothing ready, the HUD has no Collect all; given 3 ready, it shows "Collect all · 3" | `HudViewTests.CollectHidden` |
| AC-2 | Given a 7-tab sheet, its tab row is wider than the sheet and scrolls; every tab is reachable | Editor overflow check, `SheetLayoutCheck` |
| AC-3 | Given 11 people waiting, the People badge reads "11" on one line inside its pill | Editor capture, `HudViewTests.BadgeText` |
| AC-4 | Given you run the mill, founding a tea stall run by you is refused with `AlreadyRunOne` | `RunOneTests.OnlyOneRunByYou` |
| AC-5 | Given you run the mill, `RunBusinessYourself` on the bakery is refused | `RunOneTests.OnlyOneRunByYou` |
| AC-6 | Given a saved town running two, the card shows both; keeping one leaves the other with nobody, and it makes nothing until hired | `RunOneTests.KeepRunningYourself`, `RunOneViewTests.RunTwoCard` |
| AC-7 | The hub's totals equal the sum of its rows; each row's profit equals its Books' season so far | `BusinessHubViewTests.Hub` |
| AC-8 | The hub's ! and the badge count follow §3.3's reasons | `BusinessHubViewTests.HubNeedsYou` |
| AC-9 | Today's supply counts every gram from you and bought, and rolls at 6:00 | `SupplyTests.Counts` |
| AC-10 | Automatic leaves every existing test and golden replay unchanged | `GoldenTests`, the full suite |
| AC-11 | Manual at half a stall's tomato: it takes at most half its day's amount from your store; the rest at the market if allowed, none if not | `SupplyTests.ManualCap` |
| AC-12 | Manual never takes more than needed, and never takes what the store doesn't hold | `SupplyTests.CapNeverForces` |
| AC-13 | A stall order sows the in-season vegetable with the biggest unmet preorder (ties: the most bought at the market), spreads across fields, keeps in store only, and falls back to the best pick | `SupplyTests.StallOrderPick` |
| AC-14 | A stall sold: its orders become *Let him choose* | `SupplyTests.StallGone` |
| AC-15 | Schema 24 saves load as Automatic with no stall orders; the schema 25 fixture round-trips | `MigrationTests.EverySchemaLoads` |
| AC-16 | No money loop: Manual and the stall order across 200 seeds keep `ExactLedger` and the money-loop checks | `GateTests.SupplyExploits` |
| AC-17 | Every number on the hub, the card and the Supply section has a *Why?* | `WhyTests.EveryNumberHasWhy` (its sheet list has the hub, each business's Supply tab and amounts list, and the crop list) |

## 9. Balancing hooks

| Metric | Expected effect | Tolerance |
| --- | --- | --- |
| Every existing gate and bot | unchanged (no bot uses Manual or the stall order) | identical |
| A stall bot with the order (new, measured only) | more of the stall's goods from you, farm crop sales up, the stall's cost of goods about as today | report only |
| Founder-run exploit | gone: no town runs two by you after load | 0 |

## 10. Open questions

- None open: every gap closed with the owner (ADR 0172).

## 11. Cut first

1. The *Today's supply* line on the hub rows (keep it on the card).
2. Manual's kg steps (keep the four shares).
3. The overflow check's eras past the Village (check those sheets by hand).
