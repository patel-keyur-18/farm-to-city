# 025. Utilities, construction, the map's ring, hills and apples, the cold store and the brick kiln

- Status: Gaps G1–G8 closed with the owner (2026-11-18, ADR 0125); the text below is amended to match. For approval in Sprint 23. Every sheet and the ring's layout are previewed as artifacts that look exactly like the game before any code (ADR 0064, ADR 0101 §12).
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Land and soil (hills), Community and civic services (utilities), Economy (construction, storage), Governance and public finance
- Related ADRs: 0107 §2, §7, §9, §13, §15 (the plan, the ring, the kiln, the cold store, the scope), 0052 and 0053 (the map grows by era), 0064 (layouts previewed first), 0087 (the warehouse's rent), 0090 (orchards), 0125 (this spec's gaps closed), 0108 (the treasury), 0116 (spec 023's coverage rule), 0119 and 0121 (land value: the generator's smoke, the water part)
- Depends on specs: 004 (spoilage, the trader's limits), 013 (the granary and the warehouse, §3.4), 015 (the hiring board), 016 and 028 (businesses), 021 (departments, works), 023 (reach, capacity, staff; the plots), 024 (land value, zoning, builders)
- Stories: US-144 (utilities, 8), US-145 (construction, 8), US-253 (the ring, 5), US-243 (brick kiln, 5), US-242 (the cold store, 3), US-147 (hills and apples, 5, Sprint 24), US-254 to US-257 (the farm's Phase 3 promises, Should and Could, in Sprint 23 by the owner's decision)

## 1. Goal

A growing town runs into what it cannot see on a plan: water, power, drains, bricks, masons, room. The player decides, on the treasury's money and their own, which of these to build first, and sees a brick shortage slow the town and a diesel generator's smoke lower the land around it. The lesson: **growth is limited by infrastructure and inputs, not by demand**; a service built early is cheap, one built late is a queue.

## 2. Scope

**In (ADR 0107 §13):**
- **Utilities** (US-144): four ladders, each step built by the treasury on a confirm card with upkeep in its budget, a reach and a capacity: water (wells, then a water tower), power (none, a diesel generator, a grid connection), sanitation (pits, drains), waste (none, collection).
- **Construction** (US-145): from the Township every new building needs bricks, cement and timber, masons from the hiring board and build time; a shortage holds the build and says why.
- **The brick kiln** (US-243): the first Township business (F12.7): your own bricks.
- **The map's ring** (US-253): at the unlock the map grows from 16 × 12 to 24 × 18 acres; gravel lanes open zones; hill parcels, more of the old village and the highway's side are drawn.
- **Hills and apples** (US-147, Sprint 24): hill parcels in the ring; apple orchards only on hill land.
- **The cold store** (US-242): rented space in the mandi town, by the tonne for a season.
- **The farm's Phase 3 promises** (ADR 0107 §15): raising calves (US-254, Should), silage, a chaff cutter and more sheds (US-255, Could), your tractor for neighbours (US-256, Could), a tube well an acre and solar pumps (US-257, Could), kept short here (§3.8) and cut first.

**Out** (where it goes instead):
- A treatment plant, sewage plant, recycling, solar or wind power, a power plant: Phase 4.
- Steel, a construction firm: Phase 4 (builders are townspeople with a mason trade).
- Paved roads and avenues inside the town, congestion: Phase 4.
- Other hill crops; owning a cold store: Phase 4.
- Crime and its land-value part: spec 027 (this spec adds smoke only).

## 3. Rules and formulas

### 3.1 Utilities (US-144)

Each utility is a *work* the treasury commissions (spec 021 `CommissionWorks`, the budget guard of spec 023 §3.7 on the card), with a **reach** in metres from its plot (tile centre to a house, spec 023's reach rule), a **capacity** in people, and **upkeep** in the Utilities department. Coverage is the spec 023 rule: *covered = within reach, capacity left, staffed or condition above the floor*. A service's *condition* decays toward its funding like the others (spec 021 §3.4).

| Ladder | Step | Cost | Reach | Capacity | Upkeep a season | Plot |
| --- | --- | --- | --- | --- | --- | --- |
| Water | Wells (a pond or a well: spec 024's water test, no cost) | 0 | 300 | the old rule | 0 | existing |
| Water | **Water tower** | 1,50,000 | 400 (its own reach everywhere: coverage, sickness, builders and land value; the pond and wells stay 300) | 400 people | 3,000 | a village plot |
| Power | None | | | | | |
| Power | **Diesel generator** | 90,000 | 600 | 300 people | 2,000 + fuel 8,000 | an edge plot |
| Power | **Grid connection** (the district's line; your share) | 2,40,000 | the whole town | no limit | 1,500 | the highway side |
| Sanitation | Pits (the default) | 0 | | | | |
| Sanitation | **Drains** | 1,20,000 | 500 | 300 people | 2,000 | a village plot |
| Waste | None (the default) | | | | | |
| Waste | **Collection** (a collector's post, hired by the treasury like the clerk) | 60,000 | the whole town | 400 people | 6,000 + the collector's wage 12,000 | a village plot |

**Effects** (each *Why?* names the service, its reach and its condition):
- **Water.** A residential parcel with no water near it is skipped by builders (spec 024's rule, now the tower counts too). Households outside any water reach have sickness chance +20% (spec 023's sick-day roll) and the household card's *Survival* water note says so.
- **Sanitation.** Without drains the town's *Health* condition caps at 0.85 once it has 40 residents (about 5 homes an acre cluster), sickness +10%, and **land value −0.04** within the uncovered area; with drains, none.
- **Waste.** Without collection, the *Belonging* layer of every household in the uncovered area is reduced by 5 points (the town feels dirty) once the town has 40 residents.
- **Power.** Covered households get +10% of their *Leisure* budget (the lamp and the fan); businesses within reach keep their evening hours (spec 016's hours rule: 2 more shop hours a day).
- **The generator's smoke** (the GDD's diesel pollution): land value −0.06 within 250 m of the plot and *Health* −5% in covered houses within 250 m; connecting to the grid removes the generator and its smoke (the card says: *the generator is switched off and its upkeep stops*). So the grid is worth its cost, but not for fuel alone: it saves 8,500 a season (the generator's 2,000 + 8,000 against 1,500), which repays its extra 1,50,000 in about 17 seasons (4.4 game years); the real reasons are no smoke, no 300-person cap (the Township holds 80–500 people) and the whole town in reach. The smoke's land-value total is capped at −0.10 (generator −0.06 and kiln −0.04). The generator stands on an edge plot, so its smoke falls on fields and ring land, not homes.
- **Who builds (G1):** only the treasury commissions a utility, on spec 021's confirm card; you may give to the treasury and never build a public utility yourself. The card has a one-tap top-up ("Give 50,000 and build") when the treasury is short, and its budget-guard row counts the whole ladder's upkeep (a town with every step and default taxes runs 40,000–65,000 a year short; the card says so).
- **A utility left unfunded** decays like any service; a covered area shows the soft ring on the map (spec 023 §11, Should).

**Phase 3 rule: a town left alone always works.** No utility is required to reach the Township or to keep living; without them the town grows slower (builders skip uncovered land, §3.2) and is a little less happy, never stuck.

**As built (US-144, ADR 0130, owner 2026-11-19).** Five works through `CommissionWorks`, no `ConnectGrid`; the tower, drains and collection post take materials and masons (the post: 3,000 bricks, 20 bags, 1.3 m³, 12 mason-days, 48 h), the generator and the grid are fitted the same day. The top-up is part of the command (an optional gift, the shortfall rounded up to the next 10,000). One Utilities department; its condition under 50% means a utility covers nobody. The sickness effects are health lost a season (4 points without clean water, 2 without drains) because households have no sick-day roll; "2 more shop hours" is two hours past the usual close at the usual wage; drains' land value −0.04 only on acres with a home; drains and collection are missed from 40 residents. The generator's smoke footprint is the overlay's (US-153), not drawn under the sheet.

### 3.2 Construction (US-145)

From the Township, **a new building** (yours, the treasury's, a builder's; not a hut, a fence, a channel or a machine) needs **materials**, **masons** and **time**.

| Building | Bricks | Cement (bags) | Timber (m³) | Mason-days | Build hours | Today's cost |
| --- | --- | --- | --- | --- | --- | --- |
| Kutcha family house | 2,400 | 16 | 1.2 | 20 | 48 | 60,000 |
| Watchman's post | 1,200 | 8 | 0.7 | 9 | 48 | 30,000 |
| Playground | 300 | 4 | 0.6 | 9 | 24 | 20,000 |
| Community hall | 4,000 | 30 | 1.5 | 18 | 72 | 80,000 |
| Water tower | 6,000 | 60 | 3 | 42 | 96 | 1,50,000 |
| Drains | 5,000 | 50 | 1.5 | 42 | 96 | 1,20,000 |

Shops and workshops, the collector's post and the kiln get their rows at their stories from their own cost. **Rule (G2):** every row is set so materials at the base prices plus masons at 1,000 a mason-day equal today's cost within 2% (the house: 19,200 + 6,400 + 14,400 + 20,000 = 60,000). The clinic, school and quarters are built before the Township and keep the old rule.

- **Goods (new, `goods.json`):** *brick* (8 coins each), *cement* (bag, 400), *timber* (m³, 12,000), regional prices indexed like every good (spec 004). **A building's coin cost stays what it is today** at the base index; from the Township the cost is read as *materials at today's price + masons' wages*, so a brick price rise raises it a little (within ±10% of the Phase 2 cost at the base index; AC-5 walks every building with `materials` in content).
- **Where materials come from.** The trader sells at the regional price, **limited a day** (`dailyLimit`: 2,000 bricks, 30 bags, 3 m³ for the whole town, shared between everyone's builds in the order they were started); or from **your kiln** (§3.3). A build with its materials in hand starts; one without them is *held* and says "waiting for bricks: the trader has 800 left today" (the day's number, never a guess).
- **Masons.** A mason is a townsperson with a *mason* trade on the hiring board (spec 015), paid the going wage for the mason-days a build needs (about 1,000 a day). A build's hours are its base hours ÷ masons (1 to 3, from the board); with none hired the build uses the district's gang at 1.5× the wage and 1.5× the hours (always possible: a town left alone still builds, slowly and dearly).
- **Builders (spec 024)** take materials from the same daily limit **after** yours and the treasury's, so a brick shortage slows zoning too: the builders' one parcel a season waits for bricks, then builds. This is the lesson: the kiln pays when the town grows.
- **Before the Township** buildings keep their Phase 2 rule (time only).
- **Decided with the owner (2026-11-18, the US-145 design check, ADR 0125 addendum):** **you pay in full when you start**, at that day's prices (materials at the trader's price, masons by their mason-days); the build then draws its materials from the day's limit, in order of starting, and **waits when short** (the money stays in the unfinished building). **Masons are townspeople with a mason trade, listed on the hiring board, not signed:** their pay is part of the build's price (1,000 a mason-day, paid to the mason's household); the masons free when you start are reserved for the build (up to 3) and it takes its hours ÷ masons; with none free it uses the district's gang (the masons' part ×1.5 and the hours ×1.5), fixed at the start.

### 3.3 The brick kiln (US-243)

**As built (ADR 0129, Preview 3 approved 2026-11-18):** one kiln hand (not two), fuel a cost in the brick (not a good), the kiln's own building by its build hours (not a construction project), it stops in the Monsoon, and what the trader buys from it adds to his daily stock for every build. Smoke is a shared rule in `township.json`. The numbers below are the first placeholders; the balance pass sets them.

A Township business (spec 016's machinery, F12.7): **Brick kiln**, built on an acre you own (any, but it counts as a building, so the parcel is locked to other uses). Inputs: clay from the acre (the soil is not used up), *kiln fuel* (one new good, kg, regional price, indexed: wood and coal; 0.4 kg a brick). Output: up to **2,000 bricks a day** with 2 workers (hired or you). Bricks go first to your own builds at cost, then to the trader at 90% of the regional price (limited to the trader's day), or sit in the yard (10,000 at most; weather spoils none). Cost 3,00,000 (a business loan is possible). Smoke (G5): land value −0.04 and health −3% in covered homes within 150 m. The placement ghost draws the footprint hatched red and the confirm card says what it touches ("Lowers land value on 9 acres, 3 of them yours, and 4 homes"); there is no ban on where it stands. **Targets** (measured by the bots, spec 016's rule): hired, payback 4–8 seasons; run by you, 2–4 seasons. The output, workers and the trader's daily limit on *buying* bricks are set in the balance pass (at a 4-day season 2,000 a day is about 8,000 bricks, 57,600 at 90%, against two hired hands at 60,000, so the placeholders will move); a miss comes to the owner with its numbers.

### 3.4 The map's ring (US-253)

- **The map is 24 × 18 acres from save schema 21 for every game (G3), the ring locked until the Township** (not buyable, not simulated, the camera inside today's edge); the unlock flips it open, so no live renumbering and no command or replay problem. **Tile ids do not change** (as built, ADR 0125 addendum): the 16 × 12 map's tiles keep ids 0–191, standing at (id % 16 + 4, id / 16 + 3) of the 24 × 18 grid, and the ring's 240 tiles and 43 parcels are appended after them with explicit coordinates (`map.json` `legacyWidth`, `offsetX`, `offsetY`, a tile's `x` and `y`). An older save's land is padded from content, closed. No test, fixture or command log needed renumbering; the earlier plan's id formula (y + 3) × 24 + (x + 4) is dropped. The new tiles are **a ring four acres wide on the east and west and three on the north and south**, drawn spaciously (ADR 0064): meadow and scrub, a hill range along the north-west, the highway along the south edge, more village lanes, and about **80 acres** for sale at land value (the rest is hills, highway, lanes and scrub that cannot be bought). The hill parcels are in the map and locked until US-147 (G6).
- **Gravel lanes** (the treasury, card B): a lane opens a zone of the ring to zoning and builders; 20,000 a lane section; until a lane reaches a parcel it has no road access (land value's road part, spec 024).
- **Land value and builders** read the ring like any parcel; zoning's pace stays one parcel a season, so **a town left alone grows on the ring** (spec 024 AC-7 passes here).
- **Layout approved from Preview 1 (owner, 2026-11-18, every recommendation):** 53 acres for sale at the unlock (west 24 on a lane at column 2, east 6, south 23), 42 hill acres locked, hills in the north-west, the old village's five houses and a well on the east lane (column 21, rows 8–13), a free well at a highway tea stall on the south verge (16, 16) so a left-alone town can grow, two gravel lanes of 7 sections in all, the district road paved through the middle, the highway on row 17 with the link-road spur at column 11. **Public plots (ring coordinates):** generator (20, 7), a spare plot (20, 8), water tower (20, 9), drains (20, 10), collection post (20, 11), the grid on the verge (12, 16). **The furthest zoom-out is 85% of today's window** (preview 1 said 90%: the Editor's render statistics measured 90% at +13.5% draw calls, 85% at +9%, 80% at +2%, so 85% keeps the 10% budget). The preview is `docs/art/s23-preview1-ring.md`.
- The layout is binding (ADR 0064).
- **The ring's frame cost (G3):** batches equal draw calls (about one per visible sprite) and Unity culls what is off screen, so cost follows the window, not the map. The furthest zoom-out is capped at today's window (about 16 × 12 acres) and the player pans across the ring; the render statistics' "all" view then stays within 10% of today's. Fallback: bake the plain ring ground into larger sprites. Over 10 ms GPU on the phone cuts the edge rings' detail first.

### 3.5 Hills and apples (US-147, Sprint 24)

**Decided (G6):** the hill tiles and parcels are drawn in Sprint 23's map and locked; Sprint 24 opens them and adds the apple rules, with no second map change. **The figures below are placeholders and do not hold up: a Sprint 24 review redoes them with the owner, realism first** (150 kg a tree at real density is about 24 t an acre against India's 3–4; 8 coins a kg is far below mango 50; fruit spoils 20–30% a day, so apples need their own good and keeping time).

Hill parcels (terrain *hill*) sit in the ring's north-west: poorer access (no lane until the treasury builds one), cooler air, price 0.75 × the soil's. **Apple orchards only on hill land** (ADR 0021): the orchard rules of ADR 0090 (whole acres, real density, young-row intercrops at half yield), `terrain: "hill"` in `trees.json`; sapling 500; fruiting in the Monsoon after 8 seasons (two game years); about 150 kg a tree a season at 8 coins a kg is not enough alone, so apples go to the **cold store** (§3.6) to be sold out of season at +30%. An apple orchard on hill land pays back in about 14 seasons: a patient crop.

### 3.6 The cold store (US-242)

From the Township: **space in the mandi town, rented by the tonne for a season** (ADR 0087's rental pattern), `coldStoreRentPerKgSeason` **1.00**, upfront at the close and listed in *Next season, already due*; up to 20 tonnes. It keeps **potato and seed potato at a tenth of their spoilage** (0.3% a day against 3%), **fruit** (mango, guava, apple) at a quarter; vegetables and milk are not kept (the co-op chills the milk daily). Goods go in and out with the other warehouse rules (`PutCash`-free, no fee); *Stop renting* / *Keep renting* as the warehouse's. **Measured (G8):** potato's seasonal factor is 0.85 in Spring and 1.15 in the Monsoon, 6.80 to 9.20 at a base of 8: +2.40 a kg, less two seasons' rent 2.00 and about 0.16 of cold spoilage, **about +0.24 a kg** (3.5%): thin and risky. At the 20 t cap that is about 5,000 on 1.36 lakh locked, about 2.5% a season against the deposit's 1.5%: no loop (`GateTests.ColdStore` runs a stock-and-hold bot). Only mango clearly pays among fruit (Summer 0.9 to Monsoon 1.2 against about 19% spoiled); guava goes the wrong way. The sheet shows each good's "keeps X of every 100 kg" (potato 97 against about 78 at home over two seasons) and the rent as "1 coin a kilo a season, paid upfront". Owning a cold store is Phase 4.

**As built (US-242, ADR 0134):** `RentColdStore` books whole tonnes (1 to 20) like the warehouse's rent: more now for the days left, less from the next close and never below the goods in it, the close rents the tonnes upfront (a prepayment, *Next season, already due*), unpaid the space ends. `township.json` `coldStore` holds the rent, the cap and each good's share of its spoilage: potato 0.1, mango and guava 0.25 (**seed potato is left out**: it spoils 0.1% a day already, so the rent buys under a kilo saved a season; apples join in Sprint 24). Goods fill it fastest-spoiling first by themselves. The sheet shows "keeps X of every 100 kg over a season (Y in the hut)" and a worked *Is it worth it?* from the sim's own prices (potato Spring to Monsoon about +0.19 a kg, mango Summer to Monsoon about +2.99, guava none). Schema 21 gains the two tonnes in the godown's state.

### 3.7 The Utilities and Construction sheets

Each utility and each held build is explained on the Town sheet's *Infrastructure* section: what is covered, by what, how much condition, what is held and why, every number with a *Why?* in words (ADR 0043).

### 3.8 The farm's Phase 3 promises (kept short; cut first)

| Story | Rule | Priority |
| --- | --- | --- |
| US-254 Raise calves | A cow's calf can be kept: it grows for 6 seasons into a cow (the shed's places, spec 012), costing feed; sold at the cattle fair at 60% of a cow's price | Should |

**As built (US-254, ADR 0135, supersedes the figures above):** a switch on the shed sheet keeps heifer calves (off by default; half the calves are heifers, bulls are sold; kept only if a shed place is free). A heifer grows **8 seasons (cow) or 10 (buffalo)**, eats 40% of a grown ration, works 0.5 h a day, and grows only on a fed day. Her book value is her worth for her age (the calf's price rising evenly to a grown animal's price), the rise posted as **4810 Herd growth (not cash)** in *Other income*, outside farm profit and the cash flow; selling her gives that worth × 0.80 × health; she is no loan collateral until grown. The shed sheet says what raising costs against buying.
| US-255 Silage, chaff cutter, more sheds | Silage keeps green fodder over the dry season (fodder cost −15%), a chaff cutter cuts the feed's hours 10%, a third shed | Could |
| US-256 Your tractor for neighbours | Hire your tractor out by the day when idle: 80% of the hire rate, passive income, never the best use of it | Could |
| US-257 Tube well and solar pumps | A tube well on an acre waters 2 acres more (cost 40,000); a solar pump cuts pumping power by 80% (cost 60,000) | Could |

**Decided (G7): all four are in Sprint 23, cut first.** US-257 changes: the tube well is dropped (the borewell of ADR 0086 is it: 4 acres for 30,000; a second source on an acre is refused by ADR 0087), and the solar pump stays only if its payback is under about 20 seasons (80% off a borewell's 250 an acre a season is at most 800 on 4 acres, against 60,000: about 75 seasons, so it probably moves to Phase 4 with solar and wind). US-256 gets a demand cap (neighbours' acres a day) and an exploit check in US-248. Their numbers are set when the story is picked up.

## 4. Data

**New or changed definitions** (`content/`):

| Definition | Field | Type | Default | Bounds | Notes |
| --- | --- | --- | --- | --- | --- |
| `township.json` `utilities` | each step: `cost`, `reach`, `capacity`, `upkeep`, `plot` | numbers | §3.1 | cost 0–10,00,000; reach 100–2,000 | like `civic` |
| `township.json` `smoke` | `generator`, `kiln`: `reach`, `landValue`, `health` | numbers | 250 / −0.06 / −5%; 150 / −0.04 | 0–500, −0.2–0 | feeds spec 024's land value |
| `goods.json` | `brick`, `cement`, `timber` | goods | §3.2 | | `dailyLimit` in `region.json` |
| `buildings.json` | each building: `materials`, `masonDays`, `buildHours` | numbers | §3.2 | | read from the Township |
| `businesses.json` | `brick_kiln` | business | §3.3 | | spec 016's definitions |
| `map.json` | the ring's tiles and parcels | tiles | §3.4 | | a new map version |
| `trees.json` | `apple` with `terrain: "hill"` | tree | §3.5 | | |
| `buildings.json` | `cold_store` rent per kg | number | 1.00 | 0.3–3 | `maxRentTonnes` 20 |

**New or changed state** (schema 21, shipped once): `TownshipState.Utilities` (each step's status, condition, built-at); a building's `Held` reason and the materials reserved; the kiln's business state and yard stock; the ring's tiles and parcels (`MapMigration` 16 × 12 → 24 × 18); the cold store's rented tonnes per good; the parcels' `Terrain` hill.

## 5. Commands and events

| Command | Parameters | Rejection reasons | Effect |
| --- | --- | --- | --- |
| `CommissionWorks` (existing, spec 021) | a utility step | `NoTownship`, `AlreadyBuilt`, `BudgetGuard`, `InsufficientFunds` | §3.1 |
| `ConnectGrid` | | `NoTownship`, `AlreadyBuilt`, `InsufficientFunds` | switches the generator off |
| `CommissionWorks` on a lane work (`lane_w1`–`lane_w4`, `lane_e1`–`lane_e3`) | a lane section's id | `NoTreasury`, `NoTownship`, `AlreadyBuilt`, `InsufficientFunds` | §3.4: the same confirm card as the civic buildings, 20,000 a section; there is no separate `BuildLane` command |
| `FoundBusiness` (existing) | `brick_kiln` | `EraLocked`, `ParcelInUse` | §3.3 |
| `RentColdStore` | tonnes | `EraLocked`, `TooMuch`, `InsufficientFunds` | §3.6 (the warehouse's command pattern) |

| Event | Emitted when |
| --- | --- |
| `UtilityBuilt`, `UtilityUnfunded`, `GridConnected` | §3.1 |
| `BuildHeld` (with the reason), `BuildStarted`, `BuildFinished` | §3.2 |
| `BricksMade`, `BricksSold` | §3.3 |
| `LaneBuilt`, `RingOpened` | §3.4 |
| `ColdStoreRented`, `ColdStoreExpired` | §3.6 |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects | Reversibility |
| --- | --- | --- | --- | --- |
| Water tower | 1,50,000 (the treasury's, or your gift) | builders build, sickness falls | upkeep 3,000 a season | No |
| Diesel generator | 90,000 | evening hours, leisure | smoke: land value −0.06 within 250 m | Switch off, or replace with the grid |
| Grid connection | 2,40,000 | no smoke, no fuel | the generator goes | No |
| Drains | 1,20,000 | health, land value | upkeep | No |
| Collection | 60,000 | happiness | a collector's wage | Dismiss |
| Brick kiln | 3,00,000 | bricks at cost, a sale to the trader | smoke on neighbours | Close |
| Cold store | 1.00 a kg a season, upfront | potato and fruit keep | a price risk | Stop renting |
| A lane | 20,000 | a zone opens | none | No |

## 7. UI

Previewed as artifacts that look exactly like the game before any code: the **Infrastructure section** of the Town sheet, a **utility's commission card** (with the budget guard), a **held build's row** ("waiting for bricks"), the **kiln's business sheet**, the **cold store's rent sheet**, the **ring's map** with its lanes and plots (ADR 0064: the owner approves the layout first), the **hill parcels' sheet**. Every number has a *Why?*.

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Each utility is built by `CommissionWorks`, has its reach, capacity and upkeep, and decays unfunded like the other services | `UtilityTests.Ladders` |
| AC-2 | Without water, builders skip the parcel and sickness rises; the tower covers within 400 m | `UtilityTests.Water` |
| AC-3 | The generator lowers land value and health within 250 m; the grid removes it | `UtilityTests.Smoke`, `LandValueTests` |
| AC-4 | A build with no materials is held with its reason; the day's trader limit is shared; a left-alone town still builds with the gang | `ConstructionTests.Held`, `Limit`, `Gang` |
| AC-5 | A building's cost is materials at today's price plus masons, within ±10% of the Phase 2 cost at the base index | `ConstructionTests.Cost` |
| AC-6 | Builders take materials after you and the treasury; a shortage slows zoning | `ConstructionTests.BuildersWait` |
| AC-7 | The kiln makes bricks, supplies your builds at cost and sells the rest to the trader at 90% | `KilnTests.*` |
| AC-8 | The ring is in the map from schema 21 and opens at the unlock, tile ids stay, every older schema loads with the ring closed, the farm and village are where they were | `RingTests.*`, `MigrationTests.Schema21_Ring`, `MigrationTests.Schema21`, `LaneViewTests.*` (the lane card and its sheet) |
| AC-9 | A town left alone grows on the ring, slowly, solvent (spec 024 AC-7) | `ZoningTests.LeftAlone` on the ring |
| AC-10 | Apple orchards only on hill land; fruit keeps in the cold store | `TreeTests.Apple`, `ColdStoreTests.*` |
| AC-11 | The cold store keeps potato at a tenth of its spoilage; the margin is thin | `ColdStoreTests.Potato_KeepsAtATenth`, `Worth_PotatoThin_MangoPays_GuavaNot`, `NoMoneyLoop` (arithmetic on the sim's own prices, in place of a bot) |
| AC-12 | Every number has a *Why?* in words; the cards and sheets respond to scripted touch in the Editor | `WhyTests`, the stories' Editor tours |

## 9. Balancing hooks

| Metric | Expected | Pass |
| --- | --- | --- |
| A utility's cost against the treasury | the Township's second-year surplus pays a tower in about 2 years if the player saves | never stuck; a town left alone solvent (spec 023 AC-10) |
| Bricks | the trader's daily limit lets the town build about 1 house every 1.5 days; the kiln lifts it to 1 a day | the builders' pace stays 1 parcel a season; a kiln pays back 4–8 seasons hired |
| The cold store | potato margin +0.40 a kg after two seasons' rent | a slump loses it; never a loop |
| The generator | the grid pays back in about 5 years of fuel | |
| The ring | frame cost +≤ 10% at the most crowded view | render statistics |
| The 20% rule | the archetypes stay within 20% with these costs | ADR 0016 |

## 10. Open questions and clarifications

All closed with the owner on 2026-11-18, one per message (ADR 0125): **G1** the treasury commissions utilities, you may give, one-tap top-up and an upkeep-aware budget guard. **G2** materials from the Township only, quantities re-derived from today's costs. **G3** 24 × 18 from schema 21, the ring locked until the Township, zoom-out capped. **G4** fuel 8,000, one water reach (the tower's 400 m), smoke as written. **G5** the kiln on any owned acre, footprint shown, health −3%, a fuel good, economics in the balance pass. **G6** hills drawn and locked now, apples in Sprint 24 after a realism review. **G7** the farm's promises in Sprint 23 (tube well dropped, solar conditional, tractor capped). **G8** cold store rent 1.00 with the 20 t cap, only mango clearly pays.

## 11. Cut first

US-255, US-256, US-257 (Could), then US-254, then hill apples (Sprint 24 already), then the kiln's trader sales, then the collection ladder, then the soft ring on utilities. **Never cut:** water, construction's *held with a reason*, the ring, a town left alone that works.
