# 016. Businesses and production

- Status: Draft (G1–G7 closed; G8–G10 in §10 to close with the owner)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Businesses (catalogue, founding flow, books, lifecycle and trouble, failure); Economy (goods tiers, production chains); Citizens and workforce (jobs); Learning layer (P&L, cash flow, balance sheet)
- Amends: spec 004 (new goods: atta, bran; later bread and rusk), spec 005 (each business has its own ledger; the founder's investment in businesses; net worth), spec 014 (a job in a business is a town job), spec 007 (save schema 10)
- Related ADRs: 0008 (rupee-scale coins, season contracts, costs paid upfront), 0009 (the never-stuck rule; foreclosure), 0012 and 0013 (prices), 0056 and 0060 (milk goes to the co-op), 0057 (spec order), 0063 (households)
- Depends on specs: 004, 005, 006, 010, 012, 014

## 1. Goal

The player founds a first business beside the farm, a village flour mill (atta chakki), and later a bakery, each with its own books, so they see the difference between their own money and the business's, why a business can make a profit and still run short of cash, and why adding value to their own harvest pays better than buying everything in.

**Lesson:** a business is a separate set of books: revenue, cost of goods, gross margin, wages, depreciation and net profit; owner's capital and drawings; cash flow is not profit; processing adds value; thin margins need volume; an overdraft is expensive.

## 2. Scope

**In:**
- The business framework: types in data, founding (research, plan, place, fund, staff, open), its own ledger and statements, owner's capital and drawings, overdraft, trouble and closing.
- Production: recipes (inputs, outputs, rate, power), hourly work while open, live or away.
- The flour mill (Hamlet): grinding the area's grain for a fee (pisai) and milling wheat into atta to sell; bran as a by-product.
- The bakery (Village): atta and bakery supplies into pav and rusk.
- The dairy (Village): your animals' milk into paneer, curd and ghee (owner, 2026-10-25, §10 G1).
- New goods: atta, bran, bakery supplies, pav, rusk, paneer, curd, ghee.
- A job at a business is a town job (spec 014 §3.6).

**Out** (and where it goes instead):
- Shops selling to townspeople with customer choice, rivals, location scores, the farm stall and the market stall: spec 017.
- Full hiring (job posts for every role, interviews, probation, HR events): spec 015. Until then a business takes its worker as §3.6 says.
- Investors and partner citizens, managers, valuation and selling a business, branches: Phase 3 (F12.6).
- Invoices and payment terms between businesses: Phase 3 (F12.5).
- A sweet shop (paneer and ghee into sweets): later, with spec 017's shops or Phase 3.
- The town treasury: Phase 3 (F15.1).

## 3. Rules and formulas

Every coin amount is a year-0 price that follows the price index when charged (ADR 0034). Defaults are placeholders the balancer tunes in Sprint 14.

### 3.1 Business types (`content/businesses.json`)

| Type | Era | Building and equipment | Worker | Work |
| --- | --- | --- | --- | --- |
| Flour mill (atta chakki) | Hamlet | A small shed (40,000, 80 seasons) and a 10 HP stone chakki (60,000, 40 seasons), ready in 24 h | 1 miller | Grinds 40 kg of wheat an hour while open |
| Bakery | Village | A bakery shed (60,000, 80 seasons) and a wood-fired oven and mixer (50,000, 40 seasons), ready in 36 h | 1 baker | Bakes in batches (§3.9) |
| Dairy | Village | A dairy room (40,000, 80 seasons) with a cream separator, a kadhai and burner, a paneer press and a chest freezer (45,000, 40 seasons), ready in 24 h | 1 dairyman | Makes paneer, curd and ghee in batches (§3.9b) |

Only types the era allows are offered; a type whose worker can't be found is offered with the reason (§3.6).

### 3.2 Each business has its own books (§10 G2)

| Rule | Behaviour |
| --- | --- |
| Its own ledger | Spec 005's ledger, one per business: Cash, Overdraft, Stock (inputs and outputs at cost), Equipment, Buildings, Accumulated depreciation, Business loan, Interest payable, Owner's capital, Retained earnings; Sales, Service income; Cost of goods sold, Wages, Power, Repairs, Depreciation, Interest |
| Founding | The founder puts cash in: founder Dr 1800 Investment in businesses, Cr Cash; business Dr Cash, Cr Owner's capital. The building and equipment are bought by the business (capitalised, depreciated straight-line, spec 005 §3.3) |
| Drawings | The founder can take cash out: business Dr Owner's capital (drawings), Cr Cash; founder Dr Cash, Cr 1800 (down to 0, then 4300 Business drawings income) |
| Cash in | The founder can put more in at any time (the same entry as founding) |
| Season close | Each business closes with the founder (spec 005 §3.4 steps 1–3): depreciation, interest, its own P&L, cash flow and balance sheet; profit to its retained earnings |
| Net worth | The founder's net worth shows each business at its equity (assets − liabilities, never below 0) in place of 1800 at cost |
| Profitable season | A season whose net profit > 0 on the business's own P&L (the Village unlock, spec 018) |

### 3.3 Founding flow (§10 G8)

1. **Choose a type** (Menu → Businesses → Start a business): the types the era allows, each with what it does and what it costs.
2. **Market research** (a sheet, nothing paid): the grain the area brings a day and what the next village's chakki charges for grinding, the trader's atta and bran prices today, a miller's wage, and the competitor (the chakki in the next village, §3.7).
3. **The plan:** hours open (default 8:00–18:00) and the grinding fee; the game projects a season's revenue, costs, net profit and payback from today's prices and the town's size. Saved, and compared with actual results after 4 seasons ("You planned 18,000 a season; it made 12,400: the town ground less wheat than planned").
4. **Place it** on the map: any acre you own with nothing growing, one business an acre (the whole acre, drawn as its work yard), lit as the family house's are, refusals in words ("Something is growing here", "A family house stands here"). The acre stops farming and can't be sold while it stands (`ParcelInUse`). Its layout is previewed to the owner before it's final (owner, 2026-10-25; §10 G4).
5. **Fund it** (§3.8): cash put in, and a bank business loan if wanted.
6. **The worker** (§3.6).
7. **Build:** the shed and machine are built (24 h); it opens when built and staffed.

### 3.4 Work and the day

| Rule | Behaviour | Default |
| --- | --- | --- |
| Open hours | Set by the player; work only while open and staffed | 8:00–18:00 |
| Rate | Wheat ground an hour while open | 40 kg |
| Order of work | Each hour: the area's grinding first (a service, §3.5b), then wheat into atta for sale (§3.5c), up to the rate | |
| Power | Coins a kg ground, paid as it's used (Dr Power, Cr Cash) | 1.20 |
| Repairs | A share of the equipment's cost each season (Dr Repairs) | 2% |
| Away | Works while away like the farm, to the 72 h cap | |

### 3.5 The flour mill (§10 G5, G6)

**(a) Yields:** 1 kg of wheat gives 0.97 kg of atta and 0.03 kg of bran (chokar). Whole-wheat chakki atta loses little.

**(b) Grinding for the area (pisai; owner, 2026-10-25, §10 G5):** there's no chakki nearer than the next village, so your town, the village's families and the hamlets around it bring their own grain to be ground: `catchmentPeople` (default 260) plus your town's population, each bringing `grainPerPersonPerRealDay` (default 0.40 kg × 22.8: wheat, with maize, millet and gram in season), about 10,800 kg a season for 300 people. It comes during open hours, spread over them, and pays the grinding fee (default 4 coins a kg × index; the next village's chakki charges the same), Cr Service income; power is the mill's (1.20 a kg). Grain not ground in a day (closed, or past the rate) goes to the next village's chakki and is lost to you. A fee more than 1 coin above that chakki's loses the area's grain to it (spec 017 adds customer choice). Your own household's grain is ground free. This grain is the customers' own: it doesn't pass through the market.

**(c) Atta to sell:** the mill buys wheat and sells atta and bran.
- **Wheat in:** from the founder's store first, up to a daily amount the player sets ("Grind up to 200 kg a day of your wheat"), as an internal sale at today's market price (the farm's books: Cr 4000 Crop sales; the mill's: Dr Stock); then, if the player allows, from the local market at its price.
- **Atta out:** to the trader's truck at its morning price, up to 400 kg a day inside its 4-tonne load (spec 011, ADR 0054), and at the local market (atta joins it with its own demand curve, base 32 a kg, regional like every good); supermarket orders can ask for atta (spec 011 §3.5).
- **Bran (chokar) out:** to your shed's feed room first if you keep cattle (it replaces cattle feed kg for kg, counted at the feed's price: the mill's Cr Sales, the farm's Dr Inputs in store), the rest to the trader (base 15 a kg). By-products have value (owner, 2026-10-25, §10 G6).
- **The margin, at year-0 prices:** milling your own wheat (worth the trader's 23) into atta (the trader's 30) and bran adds about 5.5 a kg less 1.2 power; milling bought wheat at the market (25–27) adds 1–3. The lesson: processing pays most on your own harvest and needs volume.

### 3.6 The worker (§10 G3)

| Rule | Behaviour | Default |
| --- | --- | --- |
| Who | Until spec 015: the town's best-skilled adult without a town job (spec 014 §3.6) is offered the job; if the town has nobody, a worker comes daily from a neighbouring village (not a citizen) | |
| Wage | A season's wage paid upfront at each close (prepaid wages, as a hand's), from the business's cash | Miller 27,000; baker 30,000; dairyman 27,000 |
| You run it yourself | You can run it yourself: no wage, but your hours at the mill come off your farm hours (spec 003), so Care can fall | |
| A town job | A citizen working at a business has a town job (spec 014: the employment rate, the job need) | |
| Wages missed | A season's wage that can't be paid (no cash, overdraft full) isn't paid; the worker leaves at once (§3.10) | |

### 3.7 The chakki in the next village (competitor)

Your village has no chakki: its people carry their wheat to the one in the next village, off the map. It sets the usual grinding fee (4 coins a kg × index) and grinds the area's grain when you have no mill, are closed or busy, or charge more (§3.5b). It never closes or changes its fee in Phase 2 (rivals: spec 017).

### 3.8 Funding (§10 G7)

| Rule | Behaviour | Default |
| --- | --- | --- |
| Own cash | You put in at least the building and equipment's cost less any loan, plus a season's running cash (the wage or, run by you, a season's power and repairs), as owner's capital | |
| Business loan | The cooperative bank's small-business loan (like India's MUDRA loans), to the business: up to `businessLtv` of its building and equipment, at the bank's rate for your score (spec 005), equal principal over up to `termSeasons`; you guarantee it (§3.10). Example: a 1,00,000 mill, 60,000 borrowed, 40,000 + about 15,000 running cash put in | 0.6; 12 seasons |
| Overdraft | The bank's overdraft for the business up to `overdraftLimit` × a season's costs, at `overdraftRate` a season on the balance at each close | 0.5; 4% |

### 3.9 The bakery (Village; §10 G10)

| Rule | Behaviour | Default |
| --- | --- | --- |
| Recipe | A batch: 10 kg atta + 2 kg bakery supplies → 40 pav (8 kg) or 6 kg rusk; 1 h a batch while open | |
| Inputs | Atta from your mill first (internal sale at the market price), else the market; bakery supplies (sugar, oil, yeast) bought at their price | Supplies base 60 a kg |
| Selling | Pav and rusk to the trader and to the local market; the town's households buy them from their leisure share (spec 014 §3.5) | Pav base 50, rusk base 90 a kg |
| Power and wood | 15 coins a batch | |

### 3.9b The dairy (Village; owner, 2026-10-25; §10 G10)

| Rule | Behaviour | Default |
| --- | --- | --- |
| Milk in | Your animals' morning milk, up to a daily amount you set ("Keep up to 20 L a day for the dairy"), taken at the co-op's price that morning (an internal sale: the farm's Cr 4020 Milk sales); the rest still goes to the co-op at 6:00 (spec 012 §3.5). If you allow it, neighbours' milk at the co-op's price + 2 a litre | |
| Recipes | Paneer: 5 L buffalo milk (6.5 L cow) → 1 kg, a 20 L batch in 1 h. Curd: 1 L → 1 kg, set overnight (a batch of up to 20 L). Ghee: 15 L buffalo milk (22 L cow) → 1 kg, a batch of 30 L in 2 h | Yields by fat (spec 012 §3.5) |
| Fuel and culture | Coins a batch | 20 |
| Keeping | Paneer 3 days in the freezer, curd 1 day, ghee 6 months | Spoilage in `goods.json` |
| Selling | Paneer, curd and ghee to the trader and the local market; the town's households buy curd and paneer from their food and leisure shares | Paneer base 320, curd 75, ghee 900 a kg |
| The margin, year-0 | Paneer about 30 a kg on milk at 57.50; curd about 15; ghee little at today's price but it keeps, so it can wait for a better one | |

### 3.10 Overdraft, trouble and closing (§10 G9)

| Rule | Behaviour |
| --- | --- |
| Short of cash | A payment the business's cash can't cover draws on its overdraft, up to the limit (Cr Overdraft). Interest at each close |
| Overdraft full | Inputs can't be bought (production of atta for sale stops; the town's grinding goes on), and at the close the wage isn't paid: the worker leaves. The business card and an alert say why and what to do |
| What the player can do | Put cash in; take or restructure the business loan; sell the stock; close |
| Closing | The worker leaves; stock is sold to the trader; the equipment and building sell at 60% of book value (or stay as an empty shed); loans and overdraft are repaid from that; what's still owed becomes your own debt (you guaranteed it), joining spec 005's loans; any cash left comes to you |
| Credit | A business that misses a loan payment counts on your credit score as your own (spec 005 §3.7) |
| Foreclosure | A business on bought land is seized with that land (spec 005 §3.9 step 1); one on the inherited acres is protected with them (ADR 0009), as the family houses are |
| Never stuck | Closing always frees the founder; the farm carries on (ADR 0009) |

### 3.11 Processing order

Hourly pass, system Business (after Market): each open business works in id order (§3.4); its sales join the market's hour. Seasonal: with spec 005's close, each business closes its books in id order, then pays wages, loan payments and overdraft interest from its own cash.

## 4. Data

**New definitions** (`content/businesses.json`): types (era, building and equipment costs and lives, build hours, worker role and wage, rate, power, repairs, recipes), `catchmentPeople`, `grainPerPersonPerRealDay`, `businessLtv`, `termSeasons`, `overdraftLimit`, `overdraftRate`, the next village's chakki fee, `closingSaleShare`. **Goods** (`goods.json`): atta, bran, bakery supplies, pav, rusk (base prices, elasticities, spoilage). **Strings** for every type, recipe and account.

**New state** (save schema 10): `Business` (id, type, tile, opened, hours, fee, own-wheat daily amount, buy-from-market flag, worker (citizen id, neighbour, or the founder), its `Ledger`, stock by good, plan (projected figures, made at), loan ids, overdraft balance, state (building, open, short, closed)), `BusinessesState` (businesses, next id).

## 5. Commands and events

| Command | Parameters | Validation | Effect |
| --- | --- | --- | --- |
| `FoundBusiness` | type, tile, capital, loan, hours, fee, worker | `EraLocked`, `NotOwned`, `TileOccupied`, `NoRoom`, `InsufficientFunds`, `CreditRefused`, `NoWorker` | §3.3 |
| `PutCash`, `TakeCash` | business, amount | `UnknownBusiness`, `InsufficientFunds` | §3.2 |
| `SetBusinessHours`, `SetFee`, `SetOwnWheat` | business, value | `UnknownBusiness`, `InvalidArgument` | §3.4, §3.5 |
| `ChangeWorker` | business, worker | `NoWorker` | §3.6 |
| `CloseBusiness` | business | `UnknownBusiness` | §3.10 |

| Event | Payload | Consumers |
| --- | --- | --- |
| `BusinessFounded`, `BusinessOpened`, `BusinessClosed` | business | Map, away card, alerts |
| `Produced` | business, good, kg | Motion (the hour's work) |
| `ServicePaid` | business, kg, amount | Away card |
| `OverdraftUsed`, `WagesMissed` | business, amount | Alerts, notifications |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects | Reversibility |
| --- | --- | --- | --- | --- |
| Found a flour mill | About 1,00,000 + a season's running cash | Grinding fees from the town; value added to your wheat | A town job; an acre out of farming; less wheat sold raw | Close at a loss (60% of book value) |
| Run it yourself | No wage | Your farm hours fall | Care may drop | Change the worker at any time |
| Grind your own wheat | Wheat leaves the store at today's price | Atta and bran sell for more | The farm's sales show it as sold | Change the daily amount |
| Take a business loan | Interest | Opens sooner | You guarantee it | Repay early |

## 7. UI

```
Menu → Businesses
| Your businesses                                   [Start a business]    |
| Flour mill · open 8:00–18:00 · today 320 kg ground · cash 18,400   [›] |

Flour mill card:  [Today] [Books] [Settings]
Today:  Ground for the area 2,400 kg (9,600 coins) · Atta made 194 kg · In stock: wheat 300 kg, atta 450 kg, bran 20 kg
        Worker: Manoj (miller) · Cash 18,400 · Overdraft 0 of 25,000
Books:  P&L this season [?]   Cash flow [?]   Balance sheet [?]   Plan vs actual (after 4 seasons)
        Revenue 41,200 · Cost of wheat 30,100 · Gross margin 11,100 · Wages 6,750 · Power 2,300 · Depreciation 1,750 · Net profit 300
Settings: hours · grinding fee · grind up to N kg a day of your wheat · buy wheat at the market · put cash in · take cash out · close
```

Every number has a *Why?* (margin: "Atta 30 × 0.97 + bran 15 × 0.03 − wheat 23 − power 1.20"). The map draws the mill's shed with a belt-driven chakki, sacks and a queue of townspeople with wheat while it's open (art previewed to the owner first, US-115).

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Founding moves the founder's cash into the business (1800 / Owner's capital); the business buys and capitalises its building and equipment; both ledgers reconcile | `BusinessTests.Founding_Books` |
| AC-2 | A mill open 8–18 grinds at most 40 kg an hour, the area's grain first (catchment plus town × the grain a person), then the player's own wheat up to the daily amount; yields 0.97 atta and 0.03 bran; grain not ground in the day is lost to the next village | `ProductionTests.Mill_Rate_Order_Yield` |
| AC-3 | The founder's wheat goes to the mill at today's market price: Crop sales on the farm, Stock on the mill | `BusinessTests.InternalSale` |
| AC-4 | Each business closes its season: depreciation, interest, its own P&L, cash flow and balance sheet; a profitable season has net profit > 0 | `BusinessTests.SeasonClose` |
| AC-5 | Net worth shows a business at its equity, never below 0 | `BusinessTests.NetWorth` |
| AC-6 | A short business uses its overdraft to the limit; past it, inputs stop and the unpaid worker leaves at the close | `BusinessTests.Overdraft_Trouble` |
| AC-7 | Closing sells stock and equipment, repays the business's debts, and moves what's still owed to you | `BusinessTests.Closing_Guarantee` |
| AC-8 | A citizen working at a business has a town job | `HappinessTests.BusinessJob` |
| AC-9 | The same seed and commands give the same town with a business, in one chunk or many, live or away | `BusinessTests.ChunkingInvariance` |
| AC-10 | A schema-9 save loads into schema 10 with no businesses and round-trips | `MigrationTests.Schema9_Businesses` |

## 9. Balancing hooks

| Metric | Expected | Tolerance |
| --- | --- | --- |
| A mill grinding for the area (300 people) and milling the farm's own wheat, miller hired | Net profit 5,000–10,000 a season | The Village unlock (spec 018) |
| The same mill run by you | About 35,000 a season, with your farm hours less | |
| Payback of a mill | 6–12 seasons | Realistic for a village chakki |
| Milling wheat bought at the market | 1–2 a kg | Teaches that margins are thin |
| A business bot in the balancer | Never stuck (ADR 0009); overdraft never permanent | |

## 10. Open questions

- **G1. Which businesses in Phase 2.** **Closed (owner, 2026-10-25):** the flour mill (Hamlet) and the bakery (Village); the farm and market stalls with spec 017's shops. The owner asked that a dairy business come in this phase unless a later one plans it; none does, so the dairy (Village: paneer, curd, ghee from your own milk) is added (§3.1, §3.9b). Milk not kept for it still goes to the co-op.
- **G2. Books.** **Closed (owner, 2026-10-25):** each business has its own ledger, capital and drawings; your investment at cost on your books, each business at its equity in net worth; your own wheat and milk go to it at today's market price (a sale on the farm, a cost on the business) (§3.2).
- **G3. Who runs it.** **Closed (owner, 2026-10-25):** the best-skilled adult in your town without a town job (else a daily worker from a neighbouring village) at a season wage paid upfront, until spec 015 brings full hiring; or you run it yourself, no wage, your hours there coming off your farm work; a business job is a town job (§3.6).
- **G4. Where it stands.** **Closed (owner, 2026-10-25):** any acre you own with nothing growing (none of the inherited acres touch a road, so a roadside rule would force a land purchase first); one business an acre, the acre out of farming and not for sale while it stands; the layout previewed to the owner first; location and passing trade are spec 017's, for shops (§3.3).
- **G5. The mill's work.** **Closed (owner, 2026-10-25):** grinding for your town, the village and the hamlets around (about 300 people, 10,800 kg a season) at 4 a kg; your own wheat into atta and bran (about 4 a kg net); market wheat optional (1–2 a kg); 40 kg an hour, 8:00–18:00, power 1.20 a kg, yields 0.97 and 0.03. Calibration: wheat is 1,200 kg an acre in Winter only, and a village of 35 alone can't keep a miller, so the catchment carries the business, as a real chakki's does (§3.5).
- **G6. Wheat in, atta out.** **Closed (owner, 2026-10-25):** your store first up to a daily amount at today's market price, the market only if allowed; atta to the trader (400 kg a day) and the local market, and in supermarket orders; bran to your cattle's feed room first, the rest sold (§3.5c).
- **G7. Funding.** **Closed (owner, 2026-10-25):** your cash as owner's capital and the co-op bank's small-business loan (up to 60% of building and equipment, the bank's rate, up to 12 seasons, equal principal, your guarantee); at least the rest plus a season's running cash put in; investors and partner citizens in Phase 3 (§3.8).
- **G8. Research and the plan.** A research sheet, a projected season, plan vs actual after 4 seasons.
- **G9. Overdraft and failure.** Overdraft to half a season's costs at 4% a season; past it inputs stop and the worker leaves; closing at 60%; you guarantee what's left.
- **G10. The bakery and the dairy.** Pav and rusk from atta and bought supplies; paneer, curd and ghee from your own milk (§3.9, §3.9b); sold to the trader, the market and the town's food and leisure spending.

## 11. Cut first

1. Plan vs actual (the plan is shown, not compared).
2. Rusk (pav only).
3. Bran (atta only, at 1.0 yield).
4. Running it yourself.
