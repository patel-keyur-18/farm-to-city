# 017. Shops, customer choice and rivals

- Status: Draft (Sprint 14): gaps G1–G8 open with the owner, one per message (§10)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Businesses (founding flow step 4 location, operating levers, customer choice, competition); Economy (services priced by businesses, §Market scope); Citizens (household budgets)
- Amends: spec 016 (shops are business types with their own books; the mill's area grinding uses customer choice instead of the fee cliff, §3.6), spec 014 (part of the households' food and leisure budget is spent at shops, §3.2), spec 010 (the village square's pitches; the grocer and tea stall are rivals with state)
- Related ADRs: 0053 and 0065 (the village centre, the grocer and tea stall), 0061 (the village is the town), 0063 (Engel budgets), 0068 and 0073 (businesses, the mill), 0057 (spec order)
- Depends on specs: 004, 010, 014, 016

## 1. Goal

From the Village, the player opens shops that sell straight to people (a vegetable stall and a tea stall) beside the village's own shopkeepers, and watches customers choose between them on price, quality, distance, reputation and waiting time, so that pricing becomes a decision with visible consequences and a gap left open draws a rival.

**Lesson:** customers weigh more than price; underpricing wins customers but loses margin; reputation is earned slowly and lost fast; leaving a gap, or charging too much, invites a competitor; a shop's location sets how many people pass it.

## 2. Scope

**In:**
- Shops as business types (spec 016's framework: research, plan, place, fund, staff, books): the **vegetable stall** (sabzi stall: your produce and products sold retail) and the **tea stall** (chai and snacks, a service) (§10 G1).
- Where shops stand: a rented pitch in the village square, or your own acre beside a lane, each with a location score (§3.4, §10 G3).
- Customer demand from your town's households and the village's families, by kind of shop (§3.2).
- Customer choice: each customer group shares its demand between the shops of its kind by an attraction score of price, quality, distance, reputation and waiting time (§3.3).
- Reputation: 0–5 stars from service, stock-outs and value for money (§3.5).
- Rivals: the village grocer and tea stall as computer-run shops with prices, quality and reputation; rivals that open in a gap and close when they lose money (§3.7).
- The mill's area grinding uses the same choice against the next village's chakki (§3.6).

**Out** (and where it goes instead):
- A grocery (kirana) shop of your own, a sweet shop, a clinic, tuition, transport: Phase 3 (F12.3 later stories).
- Marketing budgets, loyalty, credit to customers (khata): Phase 3 (F12.5 invoices and payment terms).
- Rivals merging or offering to buy you out: Phase 3 (F12.6 valuation and exit).
- Price wars as events, health inspections, viral rushes: the event engine (F16.1).
- Location scores from land value and zoning: Phase 3 (F15.3).

## 3. Rules and formulas

Every coin amount is a year-0 price that follows the price index when charged (ADR 0034). Time compression as spec 016 §3: a real hour of a shop's work is about one working day of the real shop; people's daily amounts are a day of life × 22.8 a real day. Defaults are placeholders for the balancer.

### 3.1 Shop types (`content/businesses.json`, spec 016's framework)

| Type | Era | Pitch or building | Worker | Sells | Rate (a real hour = a working day) |
| --- | --- | --- | --- | --- | --- |
| Vegetable stall (sabzi stall) | Village | A handcart and a shade (8,000, 20 seasons) on a rented pitch, or a stall shed (15,000, 40 seasons) on your acre | 1 stallholder | Your produce from the store (radish, spinach, coriander, potato, fruit) and your products (atta, pav, paneer, curd) at a retail price you set | Up to 400 kg a real hour |
| Tea stall (chai stall) | Village | A counter, stove and benches (12,000, 20 seasons) | 1 tea maker | Cups of chai (milk 0.06 L, tea and sugar 1.50 coins) and a snack (rusk or pav, 0.05 kg) | Up to 300 cups a real hour |

Each shop sets its **price** (a few presets round the reference, as the mill's fee), its **quality** (§3.5: standard or good), its **hours** and, for the stall, which goods to put out. Its books, funding, worker, overdraft and closing are spec 016's.

### 3.2 Demand: who buys and how much

Two customer groups: **your town** (spec 014's households, from their budgets) and **the village** (its old families, `villagePeople`, default 250, not simulated one by one: ADR 0061). Each person, each real day:

| Kind | Demand a person a real day | From |
| --- | --- | --- |
| Vegetables and fruit (retail) | `vegPerPerson` 0.20 kg × 22.8 = 4.6 kg, at the shops' prices | Town: inside the food budget (spec 014 §3.5); what they don't buy at a stall they buy at the local market as now |
| Chai | `chaiPerPerson` 0.4 cups × 22.8 = 9 cups (adults only, about 70% of people) | Town: from the leisure budget; the village: a fixed habit |

Demand is shared each hour across the hours the shops are usually open (8:00–20:00); an hour's demand not served (every shop closed or full) is lost (people make tea at home, buy at the weekly market).

**Price response:** a group's total spend falls when prices rise above the reference: `quantity × (reference ÷ mean price)^elasticity`, elasticity vegetables 0.5, chai 0.8, computed as a Fixed power by table lookup (§4).

### 3.3 Customer choice (§10 G2)

Each hour, each customer group splits its demand for a kind of shop between the shops open that hour (yours, the village's, rivals) in proportion to each shop's **attraction**, a sum of plain parts (so the *Why?* can show each):

```
attraction = max(0.05,  1
            + wPrice   × (reference − price) ÷ reference        price 10% below the reference: +0.15 (wPrice 1.5)
            + wQuality × (quality − 1)                          good quality: +0.25; standard 0
            + wStars   × (stars − 2.5) ÷ 2.5                    5 stars +0.30, 0 stars −0.30
            − wDistance × distance                              per 100 m from the group's centre: −0.10
            − wWait    × wait ÷ 10 minutes)                     a 10-minute queue: −0.20
share(shop) = attraction(shop) ÷ Σ attraction(open shops of that kind)
```

- **Reference price:** vegetables: the good's local market price (spec 004) × `retailMarkup` 1.25; chai: 10 coins.
- **Distance:** metres from the group's centre (the town: the middle of your houses; the village: the square) to the shop's tile, 100 m an acre, Manhattan, rounded down.
- **Wait:** an hour's customers over the shop's capacity that hour: `wait = 10 × max(0, arrivals ÷ capacity − 0.8) ÷ 0.2` minutes, from last hour's figures (no loop within an hour).
- A shop that runs out of a good serves no more of it that hour; the rest of its share goes to the others (§3.5 counts a stock-out).
- Rounded: shares in Fixed, grams and cups rounded down; the remainder to the lowest shop id.

### 3.4 Where a shop stands (§10 G3)

| Place | Rent | Location | Notes |
| --- | --- | --- | --- |
| **A pitch in the village square** (3 pitches, `MapDef.places` kind `pitch`, round the banyan) | `pitchRent` 3,000 a season, paid upfront at the close to the council (Cr Cash, Dr Rent) | Distance 0 from the village, the town's centre distance from your houses | The village's own sellers keep their places; a rival can take a free pitch |
| **Your own acre** (spec 016 §3.3 step 4) beside the village lane or the farm lane | None | Distance from each group's centre: an inherited acre is 300–500 m from the square | Teaches location: cheaper, but fewer pass by |

### 3.5 Quality and reputation (§10 G4)

- **Quality:** standard or good. Good costs more: vegetables: only produce harvested in the last 2 days goes out (older goes back to the store or the trader); chai: full-cream milk 0.08 L and fresh snacks (+1.20 coins a cup).
- **Stars** (0–5, start 2.5): each closing hour (the shop's day) gets a **day score**: served share of its customers (1 − stock-out share) × 2 + quality (standard 1, good 1.5) + value (+1 at or below the reference, falling to 0 at 30% above) = 0–4.5, scaled to 0–5. Stars = 0.9 × stars + 0.1 × day score: about a season to move half way. A stock-out or a 20-minute queue costs more than a cheap price earns, as in life.

### 3.6 The mill and the next village's chakki

Spec 016 §3.5(b)'s rule (a fee more than 1 above the next village's loses all the area's grain) becomes customer choice between your mill and the chakki: the chakki at its fee, 3 stars, good quality, distance: the area's people walk 1.5 km to it (−1.5 at wDistance 0.10 per 100 m, floor 0.05); yours at your fee, quality, stars and distance from the square. At equal fees almost all the area grinds with you (as now); every coin above takes a share away instead of all of it.

### 3.7 Rivals (§10 G5, G6)

| Rival | Behaviour |
| --- | --- |
| **The village grocer** | Sells vegetables at the reference × 1.0, standard quality, 3 stars, open 7:00–21:00, never runs out (it buys from the trader). Never closes in Phase 2 |
| **The village tea stall** | Chai at 10, standard, 3 stars, open 6:00–22:00, capacity 200 cups a real hour. Never closes in Phase 2 |
| **A newcomer** | At each season close, for each kind of shop: if the last season's served demand was under 80% of demand (a gap), or the mean price was over the reference × 1.25 (rich margins), a newcomer opens on a free pitch with probability 0.5 (seeded, `rival` stream): reference price, standard, 2.5 stars, the village's hours. It closes when its season's share falls under 10% two seasons running. At most one newcomer of each kind. |
| **Reaction** | Once a season, a rival whose share fell by more than a third lowers its price 5% (never under its cost); at its cost floor it stops. Rivals never raise prices above the reference |

Rivals have no ledgers: their sales and costs are counted for their share and their closing, not booked.

### 3.8 Processing order

Hourly pass, system Shops (after Market and Business): demand per group, attraction per open shop, sales in shop id order (yours first by id, then the village's, then newcomers), stock taken, money booked on your shops' ledgers, waits for the next hour. Daily: stars. Seasonal (with spec 016's close): pitch rent, rivals' entry, exit and reaction, in id order.

## 4. Data

**New definitions:** `content/shops.json`: `villagePeople`, demand per kind (`vegPerPerson`, `chaiPerPerson`, adult share), elasticities and their power table, the attraction weights (`wPrice` 1.5, `wQuality` 0.25, `wStars` 0.30, `wDistance` 0.10 per 100 m, `wWait` 0.20 per 10 minutes), `retailMarkup`, `pitchRent`, the reputation weights, the rivals (grocer, tea stall: prices, hours, capacity, stars), newcomer rules (gap 0.8, margin 1.25, chance 0.5, exit share 0.1). `businesses.json`: the two shop types (spec 016's fields plus `kind`, `capacityPerHour`, `qualityCosts`). `map.json`: three `pitch` places round the banyan. Goods: tea and sugar as one input (`chai_supplies`, 1.50 a cup).

**New state** (save schema 11): on `Business`: price, quality, the goods put out, stars, last hour's arrivals and capacity, the season's customers and served. `ShopsState`: rivals (kind, place, price, quality, stars, open hours, opened at, low seasons), pitch tenants.

## 5. Commands and events

| Command | Parameters | Validation | Effect |
| --- | --- | --- | --- |
| `FoundBusiness` (spec 016) | + `pitch` | `PitchTaken`, `EraLocked` | §3.4 |
| `SetShop` | business, price, quality, hours, goods | `UnknownBusiness`, `InvalidArgument` | §3.1 |
| `StockStall` | business, good, kg | `InsufficientStock` | Moves produce from your store to the stall at today's market price (an internal sale, as spec 016's own wheat) |

| Event | Payload | Consumers |
| --- | --- | --- |
| `ShopSales` | business, customers, served, amount | Motion, away card |
| `RivalOpened`, `RivalClosed`, `RivalPriceCut` | kind, place, price | Alerts, away card |
| `StarsChanged` | business, stars | The card |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects | Reversibility |
| --- | --- | --- | --- | --- |
| Price below the reference | Margin a cup or kg | More customers, stars for value | A rival may cut too | Change any time |
| Good quality | Higher cost a cup or kg | Stars rise over a season | More customers later | Change any time |
| A pitch in the square | Rent each season | Many customers pass | A rival can't take that pitch | Move: close and refound |
| Leave the tea stall unopened | Nothing | A newcomer may open | Harder to enter later | Open later |

## 7. UI

```
Tea stall card:  [Today] [Customers] [Books] [Settings]
Customers:  Today 1,840 cups · your share 46% [?]
            You       10 coins · good · ★★★★☆ · 2 min wait     46% [?]
            Village   10 coins · standard · ★★★☆☆ · no wait    38% [?]
            Newcomer   9 coins · standard · ★★☆☆☆            16% [?]
            Lost (closed or full): 120 cups [?]
Settings:   price 10 [Change] · quality good [Change] · hours 6:00–20:00 [Change]
```

The share's *Why?* lists each part of the attraction in words ("10% below the village's price: +0.15", "4 stars: +0.18", "a 2-minute wait: −0.04"). Map: the stall on its pitch with customers at the counter; a newcomer's stall appears on a pitch with an alert.

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Two equal shops share a group's demand equally; a 10% lower price takes the share the formula gives; a closed shop gets none | `ShopTests.Choice_Shares` |
| AC-2 | A stock-out passes the rest of its share to the others and lowers the day score | `ShopTests.StockOut` |
| AC-3 | Stars move a tenth of the way to the day score a day | `ShopTests.Stars` |
| AC-4 | A gap (under 80% served) or a high price opens a newcomer with the seeded chance; two low seasons close it | `RivalTests.Entry_Exit` |
| AC-5 | The mill loses area grain smoothly as its fee rises above the chakki's | `ShopTests.Mill_Choice` |
| AC-6 | A pitch's rent is paid at the close; a taken pitch is refused | `ShopTests.Pitch` |
| AC-7 | Same seed and commands give the same shares, in one chunk or many, live or away | `ShopTests.ChunkingInvariance` |
| AC-8 | A schema-10 save loads into 11 with the village's rivals and no player shops | `MigrationTests.Schema10_Shops` |
| AC-9 | Every share and star has a *Why?* in words | `WhyTests.EveryNumberHasWhy` |

## 9. Balancing hooks

| Metric | Expected | Tolerance |
| --- | --- | --- |
| A tea stall on a pitch, hired, good quality, 10 coins | 5,000–12,000 a season (a hired mill's level, ADR 0073) | Measured by the business bot |
| A vegetable stall selling your own produce | 10–20% more for your produce than the trader and market, less the wage | |
| Share against the village shop at equal terms | About 50% on the square; 25–35% from an inherited acre | |
| Newcomers in a game where the player opens nothing | 0–1 of each kind by the Township | |

## 10. Open questions (closed one per message with the owner)

- **G1. Which shops in Phase 2.** Recommendation: the vegetable stall and the tea stall from the Village (both real village businesses, one goods and one service, small capital); a kirana, sweet shop and others in Phase 3.
- **G2. How customers choose.** Recommendation: the additive attraction score (§3.3), one line a factor in the *Why?*, instead of a hidden logit.
- **G3. Where shops stand.** Recommendation: rented pitches in the square (rent 3,000 a season) or your own acre with a distance cost; the square holds 3 pitches.
- **G4. Quality and reputation.** Recommendation: two quality levels, stars from served share, quality and value, moving a tenth a day.
- **G5. The village grocer and tea stall.** Recommendation: rivals with fixed terms that never close in Phase 2 (they're the village's own, ADR 0061).
- **G6. Newcomers.** Recommendation: one per kind at most, on a gap or rich prices with a seeded 50% chance a season, closing after two seasons under 10%; buy-outs in Phase 3.
- **G7. The mill.** Recommendation: customer choice against the next village's chakki (§3.6) instead of the cliff.
- **G8. Selling your own produce.** Recommendation: the stall stocks from your store at today's market price as an internal sale (as spec 016's own wheat), so the farm's books show the sale and the stall's show the margin.

## 11. Cut first

1. Newcomers (the village's shops only).
2. Waiting time (capacity simply caps sales).
3. Shops on your own acre (pitches only).
4. The mill's choice (keep spec 016's cliff).
