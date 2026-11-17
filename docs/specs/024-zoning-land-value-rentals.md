# 024. Zoning, land value, rentals, upgrades and overlays

- Status: Draft (2026-11-17). The eight gaps below carry **recommendations**; they were not yet closed one per message with the owner (the owner asked for the sprint to be finished in one go and will review at the pull request). Nothing here is coded until the owner approves; every sheet is previewed as an artifact that looks exactly like the game before any UI code.
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Land and soil, Community and civic services, Governance and public finance, Economy (rent, land and property)
- Related ADRs: 0107 §3, §8, §13, §15 (the plan, rents, the scope), 0110 (rent as built), 0109 and 0117 (taxes), 0116 (spec 023), 0119 (this spec's recommendations), 0028 and 0033 (land priced by earning power), 0064 (maps previewed first), 0101 §12 (cards)
- Depends on specs: 002 (land and parcels), 010 (the map), 014 (households, rent, leases), 017 (shops and newcomers), 021 (the treasury's income), 022 (the property tax), 023 (reach, the clinic, the school, the ring)
- Stories: US-142 (zoning, 8), US-143 (land value, 5), US-261 (lease land out, 3), US-152 (building upgrades, 5), US-153 (overlays, 5); Sprints 22–23

## 1. Goal

The town grows itself on land that has a value you can see and explain, and you can decide, without being forced to, whether land you own is farmed, leased, sold or left for builders. The lesson: **what makes land valuable is what is near it** (roads, the school, the clinic, jobs, neighbours), and a town that grows raises the value of every acre in it, yours included.

## 2. Scope

**In (ADR 0107 §13):**
- **Land value** for every acre (US-143): road access, services within reach, jobs within reach, the town's size, less smoke and (from spec 027) crime. It sets rent's place from the Township, the house tax's base, where builders build, and the price of land you buy or sell.
- **Zoning** (US-142): from the Township you zone an acre you own residential, commercial or agricultural. A zoned acre is offered to builders at its land value; builders also buy the ring's unowned land.
- **Selling land** slowly at its land value or at once at 80% (spec 002's promise).
- **Leasing land out** (US-261): an acre you own to a neighbour farmer for a season, rent upfront.
- **Building upgrades** (US-152): a kutcha house becomes pucca; a hand's hut can become a kutcha house.
- **Map overlays** (US-153): fertility, land value, coverage, happiness, crime; a tap on an acre shows its value with a *Why?*.

**Out** (where it goes instead):
- Industrial and mixed-use zones, more than 2 homes an acre, villas, row houses and apartments, noise, pollution beyond the generator's smoke, traffic: Phase 4.
- Sharecropping: Phase 4.
- The materials, masons and time a build takes: spec 025 (this spec says only *who builds and when*; before spec 025 a builder's house appears at the close, and its cost is the builder's).

## 3. Rules and formulas

### 3.1 Land value (G1)

An acre's **land value multiplier** `LV` starts at **1.00** and adds, each from content:

| Part | Rule | Adds |
| --- | --- | --- |
| Road access | on the district road, +0.20; on the village lane, +0.10; inner, 0 | 0 to +0.20 |
| Services within reach | school within `reach` of spec 023 (1,500 m): +0.08; clinic (2,000 m): +0.08; the square (500 m): +0.06; water (a well or the tower within 300 m): +0.04 | 0 to +0.26 |
| Jobs within reach | each open business with a worker within 1,000 m: +0.02, at most 5 | 0 to +0.10 |
| The town's size | +0.02 for each 10 of your households over 20, at most +0.20 | 0 to +0.20 |
| Smoke | within the diesel generator's reach: −0.10 | −0.10 to 0 |
| Crime | from spec 027, −0.15 at the worst; 0 until it exists | −0.15 to 0 |

`LV` is held between **0.80 and 1.80** and re-read once a season at the close, so it never jumps while the player is looking.
**Land value in coins** = `ParcelPrice` (spec 002: soil, drainage, fertility, location) × `LV`. Rounding: whole coins, once.

### 3.2 Rent and tax follow land value (G1, G8)

- **Rent's place** from the Township is `0.85 + 0.35 × (LV − 1.00) ÷ 0.20`, between **0.85 and 1.50** (an inner acre 0.85; the district road with a school and a clinic about 1.50). It replaces ADR 0110's three constants; a lease still renews at the lower of the market, the 10% cap (or rent control's 3%) and what 30% of the family's income allows, so a rise never pushes a family out.
- **House tax's base** (spec 022 §3.1) becomes the house's value × `LV` (its kind's value until the Township), and the 15%-of-rent guard still holds at every rate.

### 3.3 Zoning and builders (G2, G3)

- **Zone** an acre you own: *Residential*, *Commercial* or *Agricultural* (the default; it stays farmland). **Un-zone** any time before it is sold.
- **Residential:** builders buy a zoned acre at its land value when **households want homes**: at least one family waiting (a migrant or a growing family with no empty house) and the acre has road access and water. At most **1 acre a season** is bought across the whole town; the closer to the square, the earlier (value order, then id). The acre leaves your land and you receive its land value; the builder builds **2 homes** (kutcha, or pucca when §3.7's thresholds hold). A builder's house is the builder's: you receive no rent, and its house tax is the builder's to pay (spec 022's owner pays; `TaxRules.YourHouseBill` must bill only houses whose owner is you before builders exist). Builders weigh the house tax and rent control when they decide to build: rent control multiplies the builders' pace by 0.7 (spec 022 §3.5), and a tax above the default slows it in proportion.
- **Commercial:** an acre zoned commercial gives a newcomer's shop (spec 017) a pitch there; a shop opens by spec 017's rules (a gap or rich prices), run by a townsperson. The acre stays yours; the shop pays the acre's rent (its land value × `leaseYield` of §3.6) to you.
- **The ring** (spec 023/025): builders buy the ring's unowned land from its owner at land value on the same pace (counted inside the 1 acre a season), so a town you never zone still grows, slowly.
- **Permit fee:** each build pays the treasury `permitFee` **2%** of the acre's land value (income *Permits*, spec 021's intake), so growth funds the town.
- **A town left alone works:** nothing needs zoning; the ring alone adds a family house every few seasons; unzoned land is farmed or left as it is.

### 3.4 Selling land, quick or slow (G4)

`SellParcel` keeps spec 002's quick sale (80% of the price, now). Added: **Sell slowly** lists the acre for `slowSaleSeconds` (**2 days**) at **100% of its land value**; it can be taken off the market before it sells, and a slow sale never sells a protected parcel or one with a crop, tree or building (spec 002's refusals). The card shows both with the difference in coins.

### 3.5 Land's price follows the town (G1)

A parcel's price (what you pay) is spec 002's price × `LV`, so land near a growing town costs more and your own acres rise with it. ADR 0028's payback targets are re-measured by the bots (§9).

### 3.6 Leasing land out (US-261, G5)

- `LeaseOutLand(parcel)`: an acre you own, empty of crops, trees and buildings, is leased to a neighbour farmer for **one season**, renewing each season until you **End lease** (it ends at the next close).
- **Rent** = land value × `leaseYield` **2.5%** a season (about 3,000 for an average acre), paid upfront at the close into your cash (account *Rent received: land*, passive income, spec 019). It sits between a hired acre's net (2,000–5,000) and an owner-operated acre's (8,000–15,000): leasing is the safe, small choice.
- While leased the acre can't be farmed, built on, zoned or sold; the inherited acres can be leased, never sold (spec 002).

### 3.7 Upgrades (US-152, G6)

| Upgrade | Cost | When it is offered |
| --- | --- | --- |
| Hand's hut → kutcha house | the difference in value (**45,000**) | the hut's household is a family and a family house is allowed (spec 014) |
| Kutcha → pucca house | the difference (**1,80,000** of the pucca house's 2,40,000) | `LV` ≥ **1.15**, water, and the household's income allows the pucca rent: rent 7,000 ≤ 30% of its income |

A builder upgrades its own houses by the same thresholds, by itself. Yours is a tap card (spec 008): cost, the new rent at the lease's next renewal, who pays. The tenant's rent moves at the renewal (spec 014 §3.14), never at once.

### 3.8 Overlays (US-153, G7)

A button on the map opens one overlay at a time: **fertility**, **land value**, **coverage** (school, clinic, watchman, water; the generator's smoke in red), **happiness**, **crime** (a grey "no crime yet" until spec 027). Each is a five-step colour scale from cool to warm with a key; a tap on an acre shows its number and its *Why?* (the parts of §3.1). An overlay changes nothing and costs no time; it closes with the button.

### 3.9 Processing order

At each season close, after the treasury (spec 021 §3.10): re-read `LV` for every acre; builders' purchases (one acre); permit fee into the treasury; zoned shops' pitches; slow sales that have waited out their days; leases' rents (upfront); upgrades by builders; the new season's rents at the renewals.

### 3.10 Clear for an average player

- Nothing here is required: a town left alone grows slowly on the ring, and every acre of yours stays as it is.
- One line under every choice: "Offer to builders: you receive 1,20,000 now and lose the acre"; "Lease out: about 3,000 a season, you keep the land".
- The land value of any acre is one tap on the overlay, and its **?** lists the parts in plain words ("Near the school +0.08").
- No surprises: a sale, a zoning and a lease each say what you receive, when, and what you lose.

## 4. Data

**Definitions** (`content/land.json`, `content/population.json`, `content/treasury.json`; optional as the treasury is):

| Field | Default | Bounds | Notes |
| --- | --- | --- | --- |
| `landValue.road` (district, lane) | 0.20, 0.10 | 0–0.5 | §3.1 |
| `landValue.service` (school, clinic, square, water) | 0.08, 0.08, 0.06, 0.04 | 0–0.3 each | reach from spec 023 |
| `landValue.jobEach`, `jobMax` | 0.02, 5 | | |
| `landValue.sizePer10`, `sizeMax` | 0.02, 0.20 | | |
| `landValue.smoke`, `crimeMax` | −0.10, −0.15 | | |
| `landValue.floor`, `ceiling` | 0.80, 1.80 | 0.5–3 | |
| `rentPlace.base`, `rentPlace.slope`, `min`, `max` | 0.85, 0.35 per 0.20, 0.85, 1.50 | | replaces ADR 0110's constants from the Township |
| `zoning.builderAcresPerSeason`, `homesPerAcre` | 1, 2 | | |
| `zoning.permitFee` | 0.02 | 0–0.1 | treasury income |
| `sale.slowSeconds`, `sale.quickFactor` | 2 days, 0.80 | | spec 002 |
| `lease.yield` | 0.025 | 0–0.1 | a season |
| `upgrade.hutToKutcha`, `kutchaToPucca` | 45,000, 1,80,000 | | |
| `upgrade.puccaMinLandValue`, `puccaRent` | 1.15, 7,000 | | |
| `buildings.json` `pucca_house` | 2,40,000, life 120 seasons | | spec 025 prices its materials |

**State** (save schema 20 with spec 023's, hashed only once used): per acre `Zone`, `LandValue` (read each close), `LeasedOutUntil`, `SlowSaleEnds`; a building's `Kind` (hut, kutcha, pucca) and `Owner` (you or a builder); the treasury's `Permits` income.

## 5. Commands and events

| Command | Parameters | Rejections | Effect |
| --- | --- | --- | --- |
| `ZoneAcre` | tile, zone | `NotOwned`, `NoTownship`, `HasBuilding`, `Leased` | §3.3 |
| `UnzoneAcre` | tile | `NotZoned` | back to agricultural |
| `SellParcel` (slow) | parcel, slow | the quick sale's | §3.4 |
| `CancelSale` | parcel | `NotListed` | |
| `LeaseOutLand` / `EndLease` | parcel | `NotOwned`, `InUse`, `Protected` (sale only) | §3.6 |
| `UpgradeHouse` | building | `NotOffered`, `CantAfford` | §3.7 |

| Event | Emitted when | Consumers |
| --- | --- | --- |
| `AcreZoned`, `AcreSoldToBuilder`, `HomesBuilt` | zoning, a builder's purchase and build | toast, away card, play log |
| `LandValueRead` | each close | state only |
| `SlowSaleListed`, `SlowSaleDone` | §3.4 | cash, away card |
| `LeaseOut`, `LeaseEnded`, `LandRentPaid` | §3.6 | statements |
| `HouseUpgraded` | §3.7 | toast |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects | Reversibility |
| --- | --- | --- | --- | --- |
| Zone residential | None | A builder buys the acre; you receive its value | The acre leaves your land; the town grows; value rises near it | Un-zone before it sells |
| Zone commercial | None | A shop may open on the acre; you receive its rent | Competes with your shops | Un-zone |
| Sell slowly | None | 100% of land value in 2 days | The acre leaves your land | Cancel before it sells |
| Lease out | None | About 3,000 a season | Can't farm or build | End lease (next close) |
| Upgrade to pucca | 1,80,000 | A higher rent at the renewal | The tenant's burden rises (capped at 30%) | No |

## 7. UI

Previewed as artifacts that look exactly like the game before any UI code: the **zoning card** (an acre's tap card: farm, zone, sell, lease, with each line's coins), the **land value** *Why?*, the **overlay button and key**, the **upgrade card**. The slow-sale option joins the existing sell card.

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | An acre's `LV` is 1.00 plus each part of §3.1, held between 0.80 and 1.80, read once a season | `LandValueTests.Parts`, `LandValueTests.Bounds` |
| AC-2 | Rent's place follows `LV` between 0.85 and 1.50; a lease's rise is still capped and never pushes a family out | `LandValueTests.RentPlace`, `LeaseTests` (spec 014) |
| AC-3 | The house tax's base follows `LV` and never passes 15% of the rent | `TaxTests.HouseTax_Guard` (extended) |
| AC-4 | A residential acre sells to a builder at its land value when a family waits, road and water present; at most one acre a season; the builder's 2 homes are the builder's | `ZoningTests.BuilderBuys`, `ZoningTests.Pace` |
| AC-5 | A commercial acre gives a newcomer's shop a pitch and pays you its rent | `ZoningTests.Commercial` |
| AC-6 | The permit fee is 2% of land value into the treasury | `ZoningTests.PermitFee` |
| AC-7 | A town with no zoning still grows on the ring, slowly; nothing breaks | `ZoningTests.LeftAlone` |
| AC-8 | A quick sale pays 80%; a slow sale pays 100% after 2 days and can be cancelled | `SaleTests.Slow`, `SaleTests.Cancel` |
| AC-9 | Leasing out pays upfront at the close, blocks farming, building and selling, and ends at the next close | `LeaseOutTests.*` |
| AC-10 | Upgrades are offered only past the thresholds; the rent moves at the renewal | `UpgradeTests.*` |
| AC-11 | An overlay changes no state; each acre's number equals the sim's; every number has a *Why?* | `OverlayViewTests`, `WhyTests.EveryLandValueHasWhy` |
| AC-12 | Saves round-trip zones, leases, slow sales and building kinds; every older schema loads | `MigrationTests.Schema20_Zoning` |
| AC-13 | The cards and overlays respond to scripted touch in the Editor | the stories' Editor tours |

## 9. Balancing hooks

| Metric | Expected | Pass |
| --- | --- | --- |
| Land price after the town grows to 50 households | +15% to +35% on the district road | the bots' land payback still 8–15 seasons (ADR 0028) |
| Builders' pace | 1 acre a season at most; the town reaches 50 households a season or two sooner than without zoning | never stuck, never flooded |
| Leasing an acre | below a hired acre's net or equal; never the best use of land | exploit check |
| Zoning everything | the owner's land fades into builders' homes; net worth not above the best archetype by over 20% | ADR 0016 |
| A town left alone | grows on the ring; solvent | 200 seeds to year 8 |
| The Township's pace | unchanged (zoning starts after the unlock) | |

## 10. Open questions and clarifications (recommendations to the owner)

1. **G1. Land value.** *Recommended:* the parts and weights of §3.1; rent's place and the house tax follow. *Why:* the one idea "what is near it makes it worth more" explains rent, tax, price and overlays, all from one number.
2. **G2. Builders' demand.** *Recommended:* one acre a season while a family waits, road and water present. *Why:* the town grows by itself, never floods the map, and the owner is never forced.
3. **G3. Permit fee.** *Recommended:* 2% of land value to the treasury. *Why:* growth pays for the town without a new tax.
4. **G4. Selling.** *Recommended:* quick 80% now, or slow 100% in 2 days, cancellable. *Why:* teaches that liquidity costs; spec 002 promised it.
5. **G5. Leasing out.** *Recommended:* 2.5% of land value a season, upfront, one season at a time. *Why:* a safe small passive income that is never the best use of land.
6. **G6. Pucca thresholds.** *Recommended:* `LV` ≥ 1.15, water, rent ≤ 30% of the household's income; yours for the difference. *Why:* a better house is for the families who can afford it, never a push on a family that cannot.
7. **G7. Overlays.** *Recommended:* five, one at a time, five-step scales, a tap shows the number and its *Why?*. *Why:* the hidden numbers become visible without a table.
8. **G8. House tax's base.** *Recommended:* house value × `LV`, still capped at 15% of rent. *Why:* the spec 022 promise ("by land value once it exists"), with the owner's rule kept.

## 11. Cut first

The commercial zone's shops, then the overlays' happiness and crime scales, then builders' pucca upgrades, then the slow sale's cancel, then leasing out (it stands alone).
