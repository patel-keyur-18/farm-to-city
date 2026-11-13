# 022. Taxes and policies

- Status: Approved (owner, 2026-11-13; G1–G10 closed one per message, ADR 0109; the layout accepted, to be previewed as an artifact that looks exactly like the game before US-140's and US-141's UI code)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Governance and public finance (revenue, policies); Citizens (budgets, happiness, migration); Workforce (wages)
- Related ADRs: 0107 §6, §13 (taxes and policies in Phase 3, the wall), 0108 (the treasury, spec 021), 0109 (this spec's decisions), 0101 §12 and 0103 (cards), 0063 (households), 0074 and 0095 (rivals and newcomers), 0107 §8 (rents and leases, US-240)
- Depends on specs: 021 (the treasury, its intake and departments), 014 (budgets, needs, migration), 016 (a business's books), 017 (rivals, newcomers), 006 and 015 (wages, renewals), 018 (pupils), 004 (the valley's supply)
- Stories: US-140 (Taxes, 5 points), US-141 (Policies, 8 points), Sprint 21

## 1. Goal

The player sets two taxes and up to three policies for their town, sees before every change who pays, who gains and what it does to the budget, and finds that taxes cost them too, that policies have winners and losers, and that some effects take a season to arrive.

**Lesson:** who really pays a tax; public services cost money; every policy has winners, losers and a lag; a subsidy can create dependency; a rule that protects one side costs the other.

## 2. Scope

**In:**
- Property tax on every house in the town (yours and the old village's), on each kind's assessed value (land value from spec 024).
- Business tax on each business's season profit (yours from their own books; the village's grocer, tea stall and newcomers on a reference profit).
- Rates set within bounds, applying from the next season, with a preview of their effect.
- Five policies (free school meals, farm input subsidy, small business grant, minimum wage, rent control) and the night market (Could), at most 3 active, each with its cost, lag, effect, winners and losers.
- The *Taxes and policies* sheet, your side's rows, the first-visit tour, the stress bots.

**Out** (and where it goes instead):
- Sales tax (it moves every shop's price): Phase 4, with the Town's markets.
- A tax ceiling set by approval, policy slots by council mood: Phase 4.
- Tourism promotion (hotels) and pollution limits (industry): Phase 4.
- Land value as the property tax's base: spec 024 (US-143) swaps the base; the rate stays.
- The old village's cohort itself (its houses, budget, pupils): spec 023; until it joins, only your houses and people count.
- Any subsidy or grant to your farm, household or businesses: never in Phase 3 (ADR 0107 §13).

## 3. Rules and formulas

Money is `long` in 1/100 coin, indexed by `MarketRules.Indexed`; rates are `Fixed`; every division rounds down.

### 3.1 Property tax (G1)

| Rule | Behaviour | Default | Bounds |
| --- | --- | --- | --- |
| Who pays | The **owner**, as a gram panchayat's house tax: you for every house you own (tenants' houses, a hand's family house, your own home); the old village's cohort for its houses (spec 023). Hands' huts are exempt (temporary, a household of one) | | |
| Base | Each house kind's assessed value: `buildings.json` `value` (the kutcha house 60,000; the pucca house 2,40,000 from spec 024); your home is assessed as a kutcha house (`assessAs`). From spec 024 the base is the house's land value plus its building | | |
| Tax a season | base × rate ÷ 4, rounded down, for the season that ended (in arrears), at the rate in force at that season's start (§3.4) | 1.6% a year: 240 a kutcha house | 0–2.4% a year |
| Your guard (owner, 2026-11-13) | A house's tax a season is at most **15% of its kind's base rent** (`maxShareOfRent` 0.15; the loader refuses content that breaks it) and, for a let house, at most 15% of its **actual** rent (so rent control can't let the tax eat your profit, §3.6). At the cap a kutcha house leaves you 2,500 − 360 − 500 wear = 1,640 a season | 0.15 | |
| Your books | Dr **5270 House tax** (other costs, not farm profit), Cr Cash; on *Spent* and in *Next season, already due* ("House tax, 20 houses · 4,800"). The bank's room reads it as a cost like the others | | |
| The treasury's | 4640 *House tax*, one *Why?* row a payer ("You: 20 houses · 4,800", "The old village: 30 houses · 7,200") | | |
| Short of cash | Your house tax is one of the close's charges (spec 019 §3.9's list, ADR 0101 §1): short, it is part of the shortfall warning with its levers and, unpaid, carried as *Owed to the town* (account 2400) and paid first at the next close; no penalty in Phase 3 | | |

### 3.2 Business tax (G2)

| Rule | Behaviour | Default | Bounds |
| --- | --- | --- | --- |
| Base | Each business's season profit from its own books (spec 016), before drawings; a loss pays nothing; no loss carried forward | | |
| Rate | Percent of profit, for the season that ended, at the rate in force at its start | 10% | 0–20% |
| Your businesses | From their own cash: business Dr **5830 Business tax**, Cr Cash; its report reads *profit before tax → after tax*; passive income (spec 019 §3.4) uses profit after tax | | |
| Short of cash | Carried as *Tax owed to the town* (2400) on the business's books and paid first at its next close, so drawings before a close never dodge it; no penalty interest | | |
| The village's shops | The grocer, the tea stall and each newcomer keep no books, so each pays the rate × its **reference profit** (`shops.json` `referenceProfit`: grocer 15,000, tea stall 7,000, newcomer 5,000, indexed), named on the treasury's *Why?* ("Grocer · 1,500"). Like the district's grant, this money comes from outside the model; every row shows its payer | | |
| The treasury's | 4650 *Business tax*, a row a payer | | |

### 3.3 How rates move happiness and migration (G3)

1. **The old village pays from its own budget** (spec 023's cohort, a typical household through spec 014's Engel budget): the tax comes from leisure and savings before food. Its happiness falls about −1 to −2 at the cap; a cohort family under 35 for 2 closes leaves (spec 014 §3.10 on the cohort's count).
2. **Your tenants pay no house tax** (you do): they feel taxes through services: their *Healthcare* row × Health's condition, *School* × Education's, Safety and Roads alike (spec 021 §3.5).
3. **New homes** (spec 024): private builders weigh the house tax; spec 024 sets the curve.

Nothing else reacts to a rate in Phase 3 (no approval, no protest: Phase 4).

### 3.4 Setting a rate (G4)

- On *Taxes and policies*, a slider for each tax with its preview row (§7).
- **A change applies from the next season**: the rate in force for a season is fixed at its start; the close bills that season at that rate. So a rate cut the minute before a close saves nothing.
- The new rate shows as pending ("From Summer: 2.0% · now 1.6% · [Undo]"), changeable until the close. No confirm card: a rate spends nothing now (ADR 0103).
- Defaults at the treasury's opening: 1.6% a year and 10%.
- Collection follows spec 021 §3.3: the office's condition sets the share of each bill collected; the payer pays only that; the treasury's row shows billed and collected.

### 3.5 Policies (G5–G7)

Each policy's cost is a line in a department (spec 021 §3.4's table, so the budget sees it): **Education** (meals), **Council's office** (the wage inspector, the rent officer), and a seventh department, **Policies**, paid last (the subsidy, the grant, the market's lights). All costs are content, indexed.

| Policy | Cost a season | Lag | Effect | Helps | Costs |
| --- | --- | --- | --- | --- | --- |
| **Free school meals** | `mealPerPupil` 700 × pupils (yours and, from spec 023, the old village's): about 28,000 | 1 | Each pupil's household needs 25% less of a child's food basket (`mealShare` 0.25): food met rises; children's health +0.05 a season to their cap. The food comes from the district's supplier at the regional price, never from your store, stall or businesses | Poorer families (+2 to +4 happiness when short of food), children's health | The treasury |
| **Farm input subsidy** | `subsidyPerAcre` 250 × neighbours' farmed acres on the map (not yours, not scrub or roads): about 15,000 | 1 | The valley's supply of every field crop × 1.05 (`region.json` `valleySupplyPerDay`): the local price about −1 to −2%. **On repeal**, × 0.97 for 2 seasons (dependency), then normal | Neighbours, the town's food budgets | The treasury; **you, at the local market**. The trader and mandi follow the region |
| **Small business grant** | `grantAmount` 10,000, at most one a season | 1 | Offered each season to the first adult (by id) among your townspeople with no town job; with it, spec 017's newcomer may open without a price gap (chance × 1.5), run by that person (a town job). **40% come to nothing** (`grantWasted` 0.40, seeded stream `Grants`): spent, no shop | That household (security need, employment rate), shoppers | The treasury; **your shops** (a competitor). Pays back about 500 a season in business tax if it opens |
| **Minimum wage** | 2,000 (an inspector, on the office's line) | At each contract's next renewal; labourers' day wage from the next day; asks at once | A floor at `minWageAbove` +10% over today's market wage: a hand at least 33,000 a season, a labourer's day 10,300, a business worker's ask at least their kind's market + 10%; **the treasury's clerk too**. Newcomers open less often (chance × 0.8). On repeal, wages stay until each renewal | Your hands, your businesses' workers, labourers from your town (+2 happiness a household at the market wage) | **You and your businesses**; fewer jobs at the margin (from your own choices and fewer newcomers) |
| **Rent control** | 1,000 (a rent officer, on the office's line) | At each lease's next renewal | A renewal's rise is capped at `rentControlCap` 3% a year (US-240's cap is 10%); a new family moving into an empty house still pays the market rent; a rent never goes down. On repeal, renewals move toward the market, capped at 10% | Tenants (lower burden, fewer leave over rent) | **You, as a landlord** (a capped rent falls behind inflation); builders build fewer rentals (spec 024: × 0.7). The renewal card's *Why?* says turnover pays you more |
| **Night market** (Could) | 2,000 lights (Policies) + 3,000 on Safety (the watchman's night round) | 0 | Square shops may open 20:00–23:00; chai and sweets demand of those hours +10%. Needs the watchman's post (spec 023) | Your shops and the rivals in the square | Households on the square's acres (−1 happiness); the treasury |

**The wall (ADR 0107 §13):** the subsidy, the grant and the meals never go to your farm, household or businesses; spec 021's sweep (`TreasuryTests.NeverPaysYou`) covers them.

### 3.6 Slots, enacting and repealing (G8)

- **At most 3 active** (`maxActive` 3; the night market counts). A 4th asks to repeal one first.
- **Enact:** a tap-to-spend confirm card (ADR 0101 §12) with the cost a season, the lag, the effect, helps and costs, and the treasury's due at the close before and after ("61,950 → 89,950; covered ✓" or amber). First paid at the next close, for the season ahead; the effect starts `lag` closes after that first payment ("Setting up: from Monsoon").
- **Unpaid:** a policy line pass 2 can't pay is **paused** that season (no effect, nothing charged), shown, and named by the town planner; it is not repealed by itself.
- **Repeal:** a hold-to-end card with what stops and any hangover (the subsidy's −3% for 2 seasons; minimum wage and rent control until each renewal); it takes effect at the next close.
- **No flip-flopping:** a repealed policy can be enacted again only after one full season (`reenactAfterSeasons` 1).
- While you're away, policies run and pause by these rules; the away card says what was paid, paused or started.

### 3.7 Clear for an average player (owner, 2026-11-13)

- **Hands-off works.** A player who never opens the treasury keeps a solvent town with every service at full on the default rates and no policies (`TaxGateTests.HandsOff`). Nothing needs a decision to keep running.
- **A first-visit tour**: the first time the treasury opens, and the first time *Taxes and policies* opens, a short chapter of nudge pills (spec 009's form): what the treasury is ("Town money: not yours"), where it comes from, what the budget does, what a tax costs you, what a policy trades. Once only; replayable from the letter.
- **One plain line under every control** ("Raises 3,000 a season; you pay 1,200 of it"; "Helps poorer families eat; costs the town 28,000 a season"), the numbers behind it under **?**. No formula on screen.
- **No surprises:** every change says when it applies; every cost you bear is named on your own screens before it happens (*Next season, already due*); nothing fails hard (a short treasury slows services, never ends the era).

### 3.8 Processing order

At each season close, inside spec 021 §3.10's step 1: house tax (your houses, then the cohort's), business tax (your businesses by id, then the village's shops), each through `TreasuryRules.CollectTax`. Spending (step 3) includes the policy lines in their departments. Then the policies' state moves on (setting up, active, paused, the re-enact wait), and the pending rates take effect for the new season. Daily: the minimum wage's floor on labourers; hourly: the night market's hours.

## 4. Data

**Definitions** (`content/treasury.json`, inside spec 021's file; optional as it is):

| Field | Type | Default | Bounds | Notes |
| --- | --- | --- | --- | --- |
| `taxes.house.rate` | Fixed | 0.016 | 0–0.024 | A year |
| `taxes.house.maxShareOfRent` | Fixed | 0.15 | 0–0.5 | Checked by the loader against each house kind's base rent |
| `taxes.business.rate` | Fixed | 0.10 | 0–0.20 | |
| `policies[]` | list | the six of §3.5 | | id, department, cost, lag, effect fields, `could` |
| `maxActive`, `reenactAfterSeasons` | int | 3, 1 | | |
| `mealPerPupil`, `mealShare` | long, Fixed | 700, 0.25 | | |
| `subsidyPerAcre`, `subsidySupply`, `subsidyHangover`, `subsidyHangoverSeasons` | | 250, 1.05, 0.97, 2 | | |
| `grantAmount`, `grantWasted`, `grantNewcomerChance` | | 10,000, 0.40, 1.5 | | |
| `minWageAbove`, `minWageNewcomerChance`, `inspectorCost` | | 0.10, 0.8, 2,000 | | |
| `rentControlCap`, `rentOfficerCost`, `builderFactor` | | 0.03, 1,000, 0.7 | | |
| `nightMarket` | | lights 2,000, safety 3,000, hours 20–23, demand 1.10 | | Could |
| `buildings.json` `assessAs` | string | | | Your home as a kutcha house |
| `shops.json` `referenceProfit` | map | grocer 15,000, tea stall 7,000, newcomer 5,000 | | |
| Accounts | | | | Founder 5270 House tax; business 5830 Business tax; 2400 Owed to the town (both); treasury 5970 Policies |

**State** (save schema 19 with spec 021's, hashed only once the treasury opens):

| Entity | Field | Notes |
| --- | --- | --- |
| `TreasuryState.Taxes` | rate in force and pending, each tax | |
| `TreasuryState.Policies[]` | id, state (none, setting up, active, paused, waiting), enacted at, first paid, repealed at, hangover left | |
| Founder and business ledgers | 2400 balance | Tax owed |

## 5. Commands and events

| Command | Parameters | Validation and rejection reasons | Effect |
| --- | --- | --- | --- |
| `SetTaxRate` | tax, rate | `NoTreasury`, `OutOfRange` | Pending for the next season (§3.4) |
| `UndoTaxRate` | tax | `NothingPending` | |
| `EnactPolicy` | policy | `NoTreasury`, `SlotsFull`, `AlreadyActive`, `WaitAfterRepeal`, `NeedsBuilding` (night market) | §3.6 |
| `RepealPolicy` | policy | `NotActive` | At the next close |

| Event | Payload | Emitted when | Consumers |
| --- | --- | --- | --- |
| `TaxBilled` | tax, payer, billed, collected | Each close | The treasury's rows, your *Spent*, a business's books |
| `TaxOwed` | payer, amount | Short of cash | The alert, the books |
| `TaxRateSet` | tax, from, to, from season | `SetTaxRate` | Play log |
| `PolicyEnacted`, `PolicyRepealed` | policy | Commands | Toast, play log, Codex |
| `PolicyStarted`, `PolicyPaused` | policy | At a close | Toast, the away card, the planner |
| `GrantWasted`, `GrantOpenedShop` | citizen | At a close | Toast, the away card |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects | Reversibility |
| --- | --- | --- | --- | --- |
| Raise the house tax | None now | More revenue next season | You pay most of it; the old village a little less happy | Lower it; from the next season |
| Raise the business tax | None now | More revenue | Your businesses' profit after tax falls | The same |
| Enact free school meals | 28,000 a season from the treasury | Poorer families eat better after a season | Higher taxes or fewer other policies | Repeal; a season's wait to re-enact |
| Enact the farm input subsidy | 15,000 a season | Cheaper food in town | Your local-market price dips; a hangover on repeal | The same |
| Enact the small business grant | 10,000 a season | A new shop, 60% of the time | A competitor to your shops | The same |
| Enact the minimum wage | 2,000 a season | Higher wages at renewals | Your wage bill rises | The same, until renewals |
| Enact rent control | 1,000 a season | Smaller rent rises | Your rent income grows more slowly | The same |

## 7. UI

Town treasury → *Taxes and policies*, two tabs, the *Town money: not yours* line at the top; accepted by the owner (2026-11-13) and **previewed as an artifact that looks exactly like the game before the UI code**:

```
Taxes and policies · Town money: not yours           Summer, year 3
| [ Taxes ] [ Policies · 2 of 3 ]                                     |
| 🏠 House tax                       1.6% a year   [——●———] 0–2.4% [?] |
|    From Monsoon: 2.0% · now 1.6%                           [Undo]   |
|    +3,000 a season · you 1,200 · the old village 1,800             |
|    The old village: about −1 happiness · Budget covered ✓          |
| 🏪 Business tax                     10% of profit [——●———] 0–20% [?] |
|    8,700 a season · your 3 businesses 6,000 · village shops 2,700  |
| Collected last season: billed 20,700 · collected 19,600 (office 80%) [?] |

Policies tab:
| 🍲 Free school meals     28,000 a season · Active since Spring  [Repeal] |
|    Helps: poorer families' food, children's health · Costs: the treasury [?] |
| 🌾 Farm input subsidy    15,000 a season · Setting up: from Monsoon [Repeal] |
| 🏪 Small business grant  10,000 a season                     [Enact]   |
| 👷 Minimum wage          2,000 a season · raises your wages   [Enact]   |
| 🏘 Rent control          1,000 a season · caps your rises at 3% [Enact] |
| 🌙 Night market          needs the watchman's post   (greyed, reason)   |
```

- A policy row: picture, cost a season, state, *Helps* and *Costs* in one line each, **?** with the numbers, *Enact* or *Repeal*.
- **Your side:** Money and loans' *Spent → House tax* and *Next season, already due → House tax (20 houses)*; a business's books' *Business tax* and *Tax owed to the town*; a house sheet's "House tax 240 a season" beside its rent; the renewal card's rent-control line.
- **The treasury:** *House tax* and *Business tax* rows with each payer, billed against collected.
- The first-visit tour (§3.7). Every number has a *Why?*.

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | The house tax bills each owner base × rate ÷ 4 a season in arrears, huts exempt, your home as a kutcha house; posted on both sides | `TaxTests.HouseTax` |
| AC-2 | A house's tax never exceeds 15% of its kind's base rent or of a let house's actual rent; the loader refuses content past it | `TaxTests.HouseTax_Guard`, `ContentTests.TaxGuard` |
| AC-3 | The business tax bills each business's positive season profit × rate, yours from their own cash, the village's shops on their reference profit; a loss pays nothing | `TaxTests.BusinessTax` |
| AC-4 | A tax that can't be paid is carried as owed (2400) and paid first at the next close; drawings before a close don't reduce it | `TaxTests.Owed_NoDodge` |
| AC-5 | A rate change applies from the next season; pending can be undone; a cut before a close saves nothing | `TaxTests.RateFromNextSeason` |
| AC-6 | Collection follows the office's condition; the payer pays only what's collected; the treasury shows billed and collected | `TaxTests.Collection` |
| AC-7 | The preview row shows revenue, who pays, the old village's happiness and whether the budget is covered, equal to what the next close does with nothing else changed | `TaxViewTests.Preview` |
| AC-8 | The old village's tax comes from leisure and savings before food; your tenants' budgets don't change | `TaxTests.Burden` |
| AC-9 | Each policy charges its department, starts after its lag, has its effect (meals' food need; the subsidy's supply and hangover; the grant's 40% waste; the minimum wage's floor at renewal, the clerk included; rent control's 3% cap on renewals only) | `PolicyTests.Meals`, `PolicyTests.Subsidy`, `PolicyTests.Grant`, `PolicyTests.MinimumWage`, `PolicyTests.RentControl` |
| AC-10 | At most 3 active; enacting is a tap-to-spend card, repealing hold-to-end; a repealed policy waits a season; an unpaid one pauses | `PolicyTests.Slots`, `PolicyTests.PauseAndWait`, `PolicyViewTests.Cards` |
| AC-11 | No subsidy, grant or meal reaches your farm, household or businesses | `TreasuryTests.NeverPaysYou` (spec 021) |
| AC-12 | A town left alone (no treasury sheet ever opened) stays solvent with every service at 100% on defaults | `TaxGateTests.HandsOff` |
| AC-13 | The stress bots of §9 pass | `TaxGateTests.*` |
| AC-14 | Your house tax and businesses' tax are on *Next season, already due*, *Spent* and the books; every number has a *Why?* | `MoneyViewTests.HouseTax`, `WhyTests.EveryTaxNumberHasWhy` |
| AC-15 | The tour runs once on each sheet's first open; the sheets, sliders and cards respond to scripted touch in the Editor | `GuideTests.TreasuryTour`, the story's Editor tour |
| AC-16 | Saves round-trip rates, pending rates, policies' states and owed tax; replay gives the same state | `TaxTests.SaveRoundTrip` |

## 9. Balancing hooks

| Metric | Expected | Pass |
| --- | --- | --- |
| Income at the unlock | Grants 37,500 + rents 9,000 + house tax about 12,000 + business tax about 8,700 = about 67,000 | |
| Spending at the unlock | About 62,000 before the clinic's and road's upkeep: a surplus of about 5,000 | Two or three policies need higher taxes, gifts or a choice |
| Every tax at 0 | Services decay; town happiness at least 5 lower than at defaults within 8 seasons; never stuck | Lower happiness, the same era |
| Every tax at the cap | A treasury surplus; your net worth lower than at defaults | No gain to you |
| Rate dodger | The same taxes as a steady rate | Exact |
| Policy flipper | No better than steady | ≤ steady |
| Milk the treasury | Zero coins to you | Exact |
| Business emptier | The same business tax, carried as owed | Exact |
| Your house tax | Under 15% of your rent at every rate | Test, not target |
| Hands-off town | Solvent, services 100% | 200 seeds to year 8 |
| The Township's pace | Unchanged (taxes start after the unlock) | |

## 10. Open questions and clarifications

All ten gaps were closed with the owner one per message on 2026-11-13 (ADR 0109):

- **G1. Property tax.** The owner pays, huts exempt, on each kind's assessed value, 1.6% a year (0–2.4%), **never more than 15% of the rent** (owner: "as an owner I should not be paying more taxes than rent, and I should see some profit from rent") (§3.1).
- **G2. Business tax.** On season profit from the business's own books, 10% (0–20%); the village's shops on a reference profit; unpaid carried as owed (§3.2).
- **G3. Happiness and migration.** Through the old village's budget, through services for your tenants, through builders (spec 024); nothing else (§3.3).
- **G4. Setting a rate.** From the next season; pending and undo; no card (§3.4).
- **G5. Free school meals and the farm input subsidy** (§3.5).
- **G6. The small business grant and the minimum wage** (§3.5).
- **G7. Rent control and the night market** (§3.5).
- **G8. Slots, enacting, repealing** (§3.6).
- **G9. The screens** (§7).
- **G10. The gate's checks and the balance** (§9), with the owner's rule: not a burden on an average player, everything explained on screen, achievable (§3.7).

**Clarifications made while writing (none changes a decision):**
- Your house tax short of cash is carried as owed (2400) like a business's, so the founder and a business follow one rule.
- The seventh department, *Policies*, is added to spec 021 §3.4's table (an addition, not a change to its rules).
- The cohort's houses, pupils and budget arrive with spec 023; until then the taxes and meals count only your town.
- Hooks into other specs, built with these stories: spec 014 (the meals' food need), spec 006 and 015 (the minimum wage's floor at renewal), spec 017 (newcomer chances, reference profits), US-240 (rent control's cap).

## 11. Cut first

1. The night market (Could).
2. The first-visit tour's second chapter (*Taxes and policies*); the treasury's tour stays.
3. The subsidy's hangover (keep the policy).
4. The preview row's happiness estimate (keep revenue and who pays).
