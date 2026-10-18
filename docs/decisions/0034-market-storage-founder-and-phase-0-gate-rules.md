# 0034. Market, storage, founder and Phase 0 gate rules (Sprint 3 planning review)

- Status: Accepted
- Date: 2026-10-18
- Deciders: Keyur Patel
- Amends: ADR 0002 (ready to harvest waits for a grace period, then spoils), ADR 0010 (subsistence is automatic only), ADR 0012 (the price index applies to every coin amount in content), ADR 0021 (ripe fruit waits for the same grace), ADR 0031 (the mandi trip's labour cost), spec 002 §3.5, spec 003 §3.3, §3.8, §3.11, §3.12, spec 004 §3.1–§3.6, spec 005 §3.1, §3.5, spec 006 §3.6
- Related: ADR 0008 (per-season realism), ADR 0009 (never stuck), ADR 0016 (balance rule), ADR 0026 (half frame budget), ADR 0027 (on-device determinism), ADR 0032 (time-weighted founder labour)
- GDD sections: Economy (Inflation, Storage and selling), Land and farming (Ready to harvest), The founder's routine, Phase 0 gate

## Context

The Sprint 3 planning review (2026-10-18) checked the Sprint 3 stories against specs 002–007, the code and each other before any code was written. It found eight gaps and twelve small ambiguities:

1. **The mandi trip's labour.** "The founder's hours that day −6" doesn't fit the time-weighted labour rate (ADR 0032): leaving at 00:05 cost 5.4 h, leaving at 23:00 about 0.2 h. At 5.4 h (about 6,000 coins at the day-labour wage) the trip never paid against its gain of about 500 on a full potato load. Under per-season realism a game day stands for about 22 real working days, so one real trip is about 0.4 of the founder's 8 game hours.
2. **A glutted offer.** When the hour's target fell to the floor, the trader bought the whole remaining offer in that hour: a cliff (potato in Spring: above about 80 kg everything sold at the floor at once). The offer's geometric drain also never finished.
3. **Inflation.** The price index moved goods prices only. Living costs, wages, medicine, cart hire, land and buildings stayed at year-0 values, so farming grew about 48% richer against costs over 10 game years and the GDD's "cash under the mattress loses value" never showed.
4. **Inputs bought ahead** were expensed at purchase while net worth counted them as an asset. Buying ahead lowered that season's farm profit (the Hamlet gate, the credit limit), and the inherited seed was never expensed.
5. **Free field storage.** Ready crops and ripe fruit waited forever without loss, so idle land stored produce for a better season without spoilage: potato held from Spring to Monsoon gained 45%, a mango batch held past Summer 34% at no cost at all.
6. **Subsistence dominated the diet plans.** Choosing it saved about 18,000 a season for about 3,800 of labour and health (cheap medicine offsets the health loss). Being broke at a season close also skipped a whole season of living costs.
7. **The Phase 0 gate checks** (US-015, US-016) had no definition of a loop, of recovery, or of pass rates.
8. **The performance spike** (US-021 to US-023) had no map size, stub workload, build type or pass rule.

## Decision

### 1. The mandi trip costs a realistic slice of labour

The trip removes exactly `MandiDef.founderHours` (default **0.5 h**) of the founder's delivered labour, taken evenly over the trip as a cut in the labour rate: during the `tripHours` the rate is reduced by `founderHours × 24 ÷ tripHours` hours a day, never below 0. The cut is time-weighted like every other labour change, so the departure time doesn't matter. *Why?* shows "Mandi trip −0.5 h". The trip pays on full loads of cheap, heavy goods and whenever the trader or the store is full; its cart hire and commission are tuned in US-016 (data only).

### 2. The trader absorbs a glut hour by hour

When the hour's target is raised to the floor, the trader takes only that hour's unsold share: `toTrader = min(S_founder − sold, traderCapacityLeft)`, in kg for the hour. Each hour at least `min(eligible, MarketDef.minLotKg)` (default 0.1 kg, tuned in Sprint 3: 1 kg alone pushed wheat's thin local market to the floor) reaches the stall, so an offer always finishes. Sold quantities round down to the gram. `SellToTrader` stays the instant "sell it all now" way.

### 3. Every coin amount in content follows the price index

Every coin amount in content is a **year-0 price**, multiplied by the price index at the moment it is charged and rounded once: land prices (buy, quick sale, net worth), building costs, living costs (at each payment and plan change), the day-labour wage (at each daily pay), medicine, cart hire, and wages (spec 006: fixed when a contract is signed or renewed, which is the GDD's lag). **Not indexed:** price-controlled goods (chemical fertiliser), loans and their terms (nominal contracts: inflation helps borrowers and hurts savers), ledger book values (historical cost), and the credit-limit figures (base limit, era caps; revisited in Phase 2).

### 4. Inputs in store are an asset

A new asset account **1200 Inputs in store** holds bought inputs at average cost per good. `Buy`: Dr 1200, Cr Cash. Using stock (sowing, the garden, spraying): Dr 5000 or 5010 at average cost, Cr 1200. A shortfall bought at use goes straight to expense, as before. Inputs that spoil are expensed at average cost to their use account (5000 seed, 5010 the rest). The inherited inputs enter the opening balance sheet at their start-time input price (Dr 1200, Cr 3000). Net worth still counts inputs at today's input price; the difference from book is a revaluation, like land. Produce stays off the ledger until Phase 2.

### 5. Ready waits for a grace period, then spoils

A ready crop or a ripe fruit batch waits without loss for `FarmDef.readyGraceHours` (default **72 h**, the away cap). After that, each daily pass takes `round(grams × spoilagePerDay)` of its good off the frozen yield or batch and emits `GoodsSpoiled` (source: field or tree). One absence runs at most 72 h, so nobody loses a harvest just by being away; only holding on purpose costs. The HUD and notifications say "starts spoiling in N h".

### 6. Subsistence is automatic only

Subsistence is what happens when the season's living costs can't be paid; `SetDietPlan` rejects it with `NotSelectable`. The household returns to its plan at the **first daily pass** that can pay the rest of the season (pro rata), not only at the next close, so being broke at a close skips at most a day. Never stuck (ADR 0009) is unchanged: day labour is always available.

### 7. The Phase 0 gate checks

All run in `dotnet test` (category `Balance`) in CI on every push. Every bot uses only public player commands through `Execute`, so whatever a bot does a player can do.

| Check | Story | Pass rule |
| --- | --- | --- |
| Structural | US-015 | No command buys produce or sells inputs; the trader, the mandi and the market take produce only; `Buy` takes inputs only |
| Zero-time loops | US-015 | From 200 seeded mid-game states, 1,000 random command sequences applied at one instant never raise net worth (spec 005 §3.5) |
| Bounded creation | US-015 | Across 1,000 seeds of bot play, each season's output stays within its cap: crops ≤ `yieldCap × seasonYield` per acre-season, the garden ≤ its season output per bed, trees ≤ one batch per fruiting season |
| Stress bots | US-015 | Field-holder, subsistence-dodger, land-flipper, offer-churner and mandi-every-day never beat the competent bot's median net worth by more than 20% after 2 game years (ADR 0016) |
| Fresh start | US-016 | The competent founder makes farm profit > 0 in the first full season (Spring) in ≥ 99% of 1,000 seeds, and pays living costs at every close in 100% |
| Broke founder | US-016 | From 0 cash, an empty store and no credit: back on the basic plan within 1 season in 100% of seeds, and holding a season of living costs within 2 seasons in ≥ 99% |
| Report | US-016 | Pass rates, per-season farm profit (median, 5th and 95th percentile), land payback, and the Hamlet gate's progress (information) |

Every Sprint 3 mechanic is used by at least one bot, and the report shows whether it pays. A mechanic that never pays is brought to the owner rather than shipped as a dead choice.

### 8. The spike and the on-device check

| Item | Definition |
| --- | --- |
| Build | One Release IL2CPP build (not a development build) with a "Phase 0 checks" menu: Game, Spike, Golden replays. Everything created in code (ADR 0017) |
| Map | 64 × 64 isometric tiles in 16 × 16 chunks with chunk culling |
| Agents | 2,000 pooled sprites sharing one atlas and material, walking tile to tile; positions in one plain struct array updated by one manager |
| Views | The whole map zoomed out with every agent visible, and the normal play zoom; 30 s each |
| Sim stub | The real Phase 1 sim syncing every second, plus a synthetic 2,000-citizen hourly update in `Fixed` maths run once a second (3,600 times the real rate) |
| Measured | `FrameTimingManager` CPU main-thread and GPU frame times and a stopwatch on the stub: average, 95th percentile, max |
| Pass | After 10 s of warm-up, in both views: 95th-percentile CPU ≤ 8.3 ms and GPU ≤ 8.3 ms, and under 1% of frames over 16.7 ms |
| Also | A 5-minute run on battery (thermal throttling), and a 72 h away advance of the real sim timed on the device (budget 2 s) |
| Determinism | Golden replays 001, 003 and 004 run from StreamingAssets on frozen content; each shows its hash, the expected hash and PASS or FAIL |

The measured results are recorded in ADR 0035 (US-022), with follow-ups if over budget.

### 9. Small clarifications

1. `GoToMandi` takes stock not on offer only (`InsufficientStock`), and is rejected on a sick day (`FounderSick`).
2. The Winter no-warm-clothing penalty is time-weighted by the share of the day spent on subsistence.
3. The hourly price follows the regional price: when P_regional changed since the last clearing (a season change or an index step), P is first scaled by new ÷ old; damping then handles supply effects only.
4. Pest repeats count consecutive harvests of the same crop on the tile. `ClearField` leaves the count unchanged; a different crop harvested, or a fallow day (spec 002), resets it to 0.
5. `Spray` after a check that found no outbreak is rejected with `NoPestThreat`.
6. Tree payback counts from the first fruiting season at or after maturity; if the harvest limit ends the tree first, it doesn't pay back and *Why?* says so.
7. Golden replays 003 and 004 run on a frozen content snapshot committed next to them, so tuning `content/` doesn't re-pin them; the device build bundles the same snapshot. The chunking and ledger tests keep running on the live content.
8. This ADR is 0034 and the spike result is ADR 0035; spec 007's notification-package ADR takes the next free number when it is written (Sprint 6).

## Consequences

- **Spec 002:** land prices are indexed (§3.5).
- **Spec 003:** ready crops and ripe fruit wait for the grace, then spoil (AC-4, AC-25 amended; new AC-38); subsistence is automatic only and returns at the first affordable daily pass (AC-22 amended); pest repeats and `NoPestThreat` (§3.8); tree payback from maturity (§3.12); the warm-clothing penalty time-weighted (AC-34); `GoToMandi` labour (§3.6).
- **Spec 004:** the trader's hourly share of a glut and the minimum lot (§3.2, AC-8); the price rescale (§3.2, AC-5); `Buy` to 1200 (§3.4, AC-19); spoiled inputs at average cost (§3.5); the mandi trip's labour, stock and sick-day rules (§3.6, AC-21, AC-22); golden 004 on frozen content.
- **Spec 005:** account 1200 Inputs in store, the opening balance includes the inherited inputs (AC-3), reconciliation includes input inventory (AC-4).
- **Spec 006:** wages and the hut's cost are indexed when charged.
- **Spec 007:** the notifications ADR number is the next free one.
- **The economy model** switches to the real commands and the market, and the gate checks run as tests.
- **First Spring's farm profit** falls by the inherited seed's cost (about 13,000), still above 0; the Hamlet pacing is re-checked in US-016.
- **Planning:** US-178 (mandi labour), US-179 (input inventory), US-058 (field and tree spoilage) and US-052 (automatic subsistence) grow slightly. Points stay as planned; any slip is recorded in the sprint review.
