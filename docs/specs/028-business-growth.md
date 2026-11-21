# 028. Business growth: invoices, managers, sell or expand, investors, the Township's businesses and your people

- Status: In review (G1–G11 closed with the owner one per message, 2026-11-20 and 2026-11-21, each on my recommendation; the whole spec awaits approval, then ADR 0153). The five UI previews (16–20, §7) are shown as artifacts that look exactly like the game and approved before any UI code.
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Businesses (credit and payment terms, delegation, exit and valuation, Township businesses); People (hours and shifts, structure and span of control, benefits); Economy (Engel's law, services)
- Related ADRs: 0057 (specs a sprint ahead), 0068 (spec 016 businesses), 0069 and 0082 (hiring, HR events, reputation), 0074 and 0091 to 0093 (shops and customer choice), 0089 (equipment), 0107 (Phase 3 plan, §7, §13, §15), 0108 (treasury), 0130 (utilities: the grid), 0134 (cold store), 0141 (spec 027's engine), 0144 (Sprint 24 plan: US-247 belongs here)
- Depends on specs: 006 (posts), 011 (the mandi trip), 014 (households, incomes), 015 (hiring, workers, HR events), 016 (businesses, books), 017 (shops, stars, rivals), 019 (passive income, statements), 023 (clinic), 025 (equipment, utilities, the road), 026 (festivals, reputation), 027 (the event engine)
- Stories: Sprint 25 (39 points): US-123 invoices (5), US-124 managers (8), US-125 sell or expand (5), US-247 investors, partners and buy-outs (5), US-244 tuition centre (5), US-245 tempo (5), US-246 restaurant (3), US-258 benefits and night shifts (3). Sprint 26: US-259 supervisors, associations, conflicts and accidents (5, Could), US-260 your kirana, marketing and khata (5). Save schema 23 (Sprint 25) and 24 (Sprint 26).

## 1. Goal

The player grows from owning businesses to running a business empire: sells on credit and learns that profit is not cash, hands a business to a manager and measures what that costs, learns what a business is worth and whether to sell it, take an investor or open a branch, and serves a richer town with a school's extras, a vehicle and a restaurant, while keeping good people with benefits and facing the problems of people at scale.

**Lesson:** a profitable business can run out of cash (receivables); delegation costs a share of profit and is worth it only when your hours are worth more; a business is worth a multiple of its profit, and a multiple depends on growth and risk; equity is a price paid for money now; a vehicle must earn its keep on volume; extra hours help only when customers are waiting; benefits are paid to keep people, not to raise output; credit sells goods and costs losses; advertising pays only with a margin.

## 2. Scope

**In (ADR 0107 §13, §15):**
- **Invoices** (US-123, §3.1): sales between businesses on terms, a receivable and a payable, ageing, the stop rule.
- **Managers** (US-124, §3.2): a post on the hiring board, three styles, skill, a wage that follows profit, a target and a report.
- **Sell or expand** (US-125, §3.3): valuation, selling, branches.
- **Investors, partners and buy-outs** (US-247, §3.4).
- **Township businesses** (F12.7): tuition centre (US-244, §3.5), tempo transport (US-245, §3.6), restaurant (US-246, §3.7, Could); the kirana (US-260, §3.10).
- **People** (F11.4): benefits and night shifts (US-258, §3.8); supervisors, associations, conflicts and accidents (US-259, §3.9, Could).
- **Shops** (F12.3): marketing and khata (US-260, §3.10).

**Out** (with where it goes instead):
- Invoices with the trader and the supermarket (they stay cash): not in Phase 3.
- A manager of several businesses, a holding company, franchises: Phase 4.
- Shares traded on a market: the City.
- Pensions and provident fund, benefit tiers by skill: Phase 4.
- Town-wide strikes: Phase 4. Hotels, delivery and highway travellers for the restaurant: the Town and Phase 4.
- Trucks, a transport company: Phase 4. Exam coaching, a college: the Town.
- Loyalty schemes and chains: Phase 4.
- The Chronicle, Almanac and citizen stories: spec 029.

## 3. Rules and formulas

Everything is Fixed-point (ADR 0018). Money is in 1/100 coin, indexed by the price index at the moment of the spend. Rounding: division rounds half away from zero unless a rule says otherwise. Every number has a *Why?* in words, never a factor (ADR 0043). Values live in `content/invoices.json`, `content/managers.json`, `content/exit.json`, `content/businesses.json` (the new kinds), `content/benefits.json`, `content/hr_events.json`. Season lengths: a season is 4 real days, a half-season 2 real days.

### 3.1 Invoices (US-123, G1)

| Rule | Behaviour |
| --- | --- |
| Which sales are invoiced | From the Township, a sale between two businesses: your mill to your bakery, your bakery or dairy to your stalls, neighbours' milk to your dairy, and your goods to or from the village grocer and tea stall. The farm's own wheat and milk stay cash (the farm is you, not a business with books). Households, the trader and the supermarket stay cash. |
| Wholesale buyers | The village grocer and tea stall buy your pav, paneer and curd on terms at the trader's price for the good, shared by the shop score with their present supplier, up to 20 kg a day each. They pay at term end and never default or close (ADR 0074). |
| Terms | The seller sets Cash, Half-season (2 real days) or Season (4 real days) for each buyer. Default Half-season. Longer terms win nothing and cost the seller cash. |
| Paying | An invoice is paid at term end from the buyer's cash, then its overdraft. If neither covers it, it ages: **current** (before due), **late** (up to one term past due), **overdue** (more than one term). No late fee, no write-off; it carries over each close. |
| The stop rule | A seller stops delivering to a buyer with an overdue invoice until it is paid. A stopped supplier means a bakery with no atta: a profitable business can run out of cash and halt. |
| Books | Each business has a trade receivable (asset) and a trade payable (liability). Cash flow shows the receivable as the reason profit and cash differ. Between your own businesses the two cancel in net worth, so terms never create money. |

### 3.2 Managers (US-124, G2)

| Rule | Behaviour |
| --- | --- |
| The post | Manager is a post on the hiring board with the contract, renewal and "asks for a raise when skill grows" flow of any worker (spec 015). He runs one business and is paid from its own cash. One manager per business; none for two businesses. |
| Wage | 4,000 a season plus 10% of the business's profit when positive (nothing when it loses), so a manager never turns a profit into a loss. Shown on the card and in the plan. |
| Styles | **Cost-cutter:** reference price, standard quality, 1 day's stock, hires the cheapest worker (wage −10%). **Growth-chaser:** 5% under the reference price, standard, 2 days' stock and never a stock-out, hires the highest skill. **Quality-first:** reference + 5%, good quality, 1 day's stock, hires the best worker paying +10%. |
| Skill | 1 to 5 stars sets the share of days he mis-sets stock (a stock-out or overstock): 20% at 1 star, 15.5, 11, 6.5, 2% at 5 stars. A mis-set day loses that day's sales (about 0 to 10% of profit). Skill grows with seasons managed. |
| Target and report | You set a target, profit or growth. At each close he reports: met or missed, what he changed and why (a *Why?* on each line). Two misses in a row lower his motivation (spec 015). A refused raise while underpaid: he leaves at the next close (ADR 0082). |
| Passive income | A managed business is passive income, as a hired one is. You can dismiss him any time (severance as for workers) and take it back. |
| Expected | Managed profit at least the hired profit; below run-by-you, because he is paid and you are not (measured in the balance pass). |

### 3.3 Valuation, selling and branches (US-125, G3)

| Rule | Behaviour |
| --- | --- |
| Valuation | `value = max(book, multiple × profit)`, profit the last 4 closed seasons (annualised when fewer, at least 1), book the business's own equity. Multiple: base 4.5, **+1 growth** if the last 2 seasons' profit is 10% or more above the 2 before (**−1** if 10% or more below), **−1 risk** if any of the last 4 seasons lost money or the business is under 4 seasons old, **+1** if it has a manager and 4 profitable seasons; clamped to 3 to 6. A sale never ends in a paper loss. A plain breakdown under a ? |
| Selling | You list it; a buyer (a townsperson with working capital, a rival or a regional buyer, seeded) completes in 3 to 6 real days at 90 to 105% of the value, never under book. **Rushed:** 1 day at 85% of the value, never under book. You can cancel any time before completion. The business runs until the day it closes. |
| What goes | The business, its building, equipment, stock and receivables go to the buyer, who moves it off your acre; the acre is cleared and yours again (a pitch is freed). Its worker and manager are released (severance as for workers). Outside shares are settled from the proceeds first (§3.4). |
| Gain | The gain over book is business income in the season of the sale, taxed at the business tax (ADR 0109). |
| Branches | A second (and third) business of the same kind on another acre or pitch, at most 2 branches a kind, paid like the first (building, equipment, cash). Its own books, its own manager, its own valuation; the first business's research and plan carry over as a default. No franchises. |

### 3.4 Investors, partners and buy-outs (US-247, G4)

| Rule | Behaviour |
| --- | --- |
| Investor | An opportunity event (spec 027's engine, this spec owns the card) once a business has a profitable season and a valuation above 50,000. The investor offers cash of 10, 15, 20, 25 or 30% of the value for the same share of equity and profit (`share = cash ÷ valuation`). The cash goes into the business's capital. One investor a business; outside shares (investor plus partner) never above 40%. The card is open 48 h, default: decline. |
| Profit share | Paid from the business's cash at each close, `share × profit` when positive. |
| Buying back | At the current valuation × the share, after 4 seasons. Selling the business settles the share from the proceeds. |
| Partner | A townsperson with savings of at least their stake joins: stake 20% of the value for a fixed 20% profit share; one partner a business. Partners are not shares and are not traded. You cannot sell or close the business without buying the partner out at value × 20%. They set no prices; they share the books and the buy-out. |
| Buy-outs | A rival (or a regional buyer if none is open) offers 90 to 110% of the value for one of your businesses, never under book; at most one offer a business each 2 seasons. Open 24 h (48 h when the business is a quarter or more of your net worth). Refusing costs nothing. Accepting is a sale (§3.3) with outside shares settled first. |
| Books | Outside equity sits in the business's own books and in net worth (a share is a liability of the business to the investor, not income). |

### 3.5 Tuition centre (US-244, G5)

| Rule | Behaviour |
| --- | --- |
| Business | From the Township, on a rented pitch (3,000 a season) or an acre. Building 40,000, equipment 15,000, a teacher from the hiring board at 20,000 a season (crafting skill) or you run it. Open 15:00 to 19:00. |
| Capacity | 20 pupils a batch, 2 batches an evening: 40 pupils a season. |
| Demand | A child aged 7 to 17 attends if the household can afford it: the education budget (5% of income) buys `floor(budget ÷ fee)` children, never more than it has. Several centres share pupils by the shop score. |
| Fee | Reference 800 a pupil a season; presets −10%, reference, +10%, +20%. Stars from how full the batches are and how long pupils stay. |
| Gain | +1 skill (farming and crafting) per season of tuition, capped at +10 (one course). A pupil is a better hand at 18 on the board. |
| Money | 40 pupils × 800 = 32,000 against a 20,000 wage, 3,000 rent and about 2,000 running. About +7,000 at full; breaks even near 30 pupils (about 45 families). Target hired 3,000 to 8,000 a season, run by you 18,000 to 25,000. |

### 3.6 Tempo transport (US-245, G6)

| Rule | Behaviour |
| --- | --- |
| Machine | A second kind of machine, from the Township: price 3,50,000, lease 18,000 a season, a 48-season life, upkeep 1.5%, a bank loan allowed, parked on the lane acre, breaks and is repaired like the tractor. One tempo. |
| Trips | 1,000 kg, 4 hours on the paved road, 300 a trip (diesel 200, wear 100), two trips a day (your time 0.5 h each). Without a tempo, one trip a day as before. |
| Which vehicle goes | The cheapest that fits the load, said on the mandi sheet: the tractor-trolley (free per kg), then the tempo (0.30 a kg), then the hired trolley (0.50 a kg). A load over 1,000 kg with no tractor goes in two tempo trips or the hired trolley. |
| Letting it out | The tractor's switch, "Let it out for neighbours' trips": farm income, not passive; diesel and wear yours. Neighbours' trips come before yours, up to 2 a day, each paying 600 for 1,000 kg. About 300 net a trip, 600 a day at most. |
| Is it worth it | A line on its sheet like the cold store's. Own trips alone: it pays its lease only above about 45 of a season's 90 days. With neighbours' trips: about +36,000 a season, payback about 10 seasons. |

### 3.7 Restaurant (US-246, G7, Could)

| Rule | Behaviour |
| --- | --- |
| Business | From the Township, on a rented pitch (3,000) or an acre. Building 30,000, equipment 20,000, a cook from the board at 20,000 (crafting skill) or you run it. Open 11:00 to 15:00 and 18:00 to 22:00. |
| Menu | **Thali** 60: rice 0.12 kg, lentils 0.05, vegetables 0.15, fuel 6, supplies 14. **Paratha plate** 45: atta 0.15 kg, ghee 0.02, curd 0.1, fuel 4, supplies 10. |
| Buying | From your store first, at the trader's price (an invoiced sale under §3.1), then the market at +15%. |
| Demand | A household with income of at least 20,000 a season sets aside 25% of its leisure budget and buys `floor(that ÷ price)` portions a season, spread over the days; below 20,000 it eats at home. Shared by the score with any rival (a newcomer may open when your prices run 10% above the reference). Good quality: +10% on the price, lifts stars. |
| Capacity | 15 portions an hour a cook, 120 a day. |
| Money | About 1,000 portions × 53, against a 20,000 wage, about 25,000 ingredients and supplies and 3,000 rent: about +5,000 hired, +25,000 run by you; needs about 35 households with the income. Targets hired 3,000 to 8,000, run by you 18,000 to 28,000. |

### 3.8 Benefits and night shifts (US-258, G8)

| Rule | Behaviour |
| --- | --- |
| Scope | Per employer (all its workers): the business's Settings tab, and People for your farm hands. Cost is per worker a season. |
| Benefits | **A meal each working day:** 1,800, motivation +0.03. **Health cover** (needs the clinic): 600, sick leave 2% to 1%, no sick day's wage lost, motivation +0.02. **Festival bonus:** 1,000, paid at each festival, motivation +0.04 that season, reputation +1. **Paid leave, 4 days a season:** 900 and those days' work lost, motivation +0.03, fatigue −0.10 once a season. Motivation still stops at 1.2. |
| Loyalty | Each benefit multiplies the poaching chance and the chance a worker leaves unhappy by 0.85 (all four: poaching 5% to about 2.6% a season). Two or more benefits kept through a close: employer reputation +1. |
| Honest value | A "Does it pay?" line per benefit with your own numbers: benefits do not pay in output alone (+0.03 is about +3% output, about 600 on a 20,000 worker); they pay in keeping a skilled worker and in reputation. |
| Night shift | Needs the grid (not a generator) and a second worker hired for the same role; offered only on the mill, bakery, dairy and sweet shop. 20:00 to 04:00, 8 hours, no overtime; wage +25% (25,000 on a 20,000 role); fatigue 0.07 a day; the business's power as usual. |
| When it pays | Offered when the business sold out before the day shift ended on 3 days (the sheet shows "busy 100% of the 12 hours; sold out by 14:00 on 5 of the last 8 days"). Demand-limited businesses will pay only in a large town. |

### 3.9 Supervisors, associations, conflicts and accidents (US-259, G9, Could)

A **place** is one employer: all your farm hands together, or one business. The events ride spec 015's HR-event engine (cards with a free default).

| Rule | Behaviour |
| --- | --- |
| Supervisor | A board post (30,000 a season) for a place with 3 or more workers; one covers up to 5. +0.8% output a star on each supervised worker (+4% at 5 stars); accident chance halved; conflicts settled for you; an association half as likely. Pays only above about 5 hands in output; below that in accidents and conflicts avoided (shown as a count). |
| Accidents | A daily roll for each worker at work: 0.02% a day (about 1.8% a season) ×2 on a night shift, ×1.5 with overtime, ×2 with machine maintenance skipped, ×0.5 with a supervisor. Out 7 days (3 with the clinic); the bill 2,000 (free with health cover). The card: pay the days' wage (reputation +1, motivation +0.05) or not (−0.10, reputation −1); **default after 24 h: pay.** 5% are severe: out 30 days, a bill of 8,000. Nobody dies. |
| Conflicts | A place with 2 or more workers rolls 3% a season × (1 + 2 × average fatigue). **Talk to both** (2 of your hours, resolved 70%); **Move one** to another place; **Let it be** (both motivation −0.05, one resigns at the next close half the time; the default after 24 h). |
| Association | A place with 4 or more workers can form one at a close if pay is more than 5% under the reference, or average fatigue is above 0.3 for 2 closes, or employer reputation is under 50. It asks for a 10% raise and a condition. **Accept:** all wages +10%, none for 4 closes. **Negotiate:** +5% and one benefit, accepted 60%, else a refusal. **Refuse:** motivation −0.10 and output ×0.85 that season; a second refusal is a 2-day stoppage at that place only. |

### 3.10 Your kirana, marketing and khata (US-260, G10)

| Rule | Behaviour |
| --- | --- |
| Kirana | A shop of the new kind "grocery" from the Township, on a pitch (3,000) or an acre. Building 20,000, equipment 8,000, a shopkeeper at 12,000, open 07:00 to 21:00. Stocks atta, rice, lentils, ghee, rusk, pav, paneer and curd: 0.25 kg a person a day split by shares (atta 35%, rice 25%, lentils 10%, the rest 30%). Your store's goods first, then the market at +15%. The village grocer gets a staples entry and competes by the same score, cutting 5% when you take a third of its share. |
| Marketing | A budget per shop per season: 0, 2,000, 4,000 or 8,000, adding +0.019 to the score per 1,000 spent up to +0.15 (about half a star or a 10% price cut). It lasts only while spent; stopped, it fades by a fifth each close. |
| Khata | A switch on a shop that sells to households (kirana, vegetable stall, tea stall). A regular household (bought in at least 3 of the last 5 days) may take goods on credit up to your limit per household: 500, 1,000 or 2,000. Adds +0.10 to the score for households with income under 20,000 and lifts their kilos 10%. Each credit sale is a customer receivable on the shop's books (§3.1's account), collected at the close. A household over its limit or overdue is cut off; you can cut one off yourself. |
| Losses | Never paid, by income a season: 8% under 10,000, 4% for 10,000 to 20,000, 1% above; a further 5% of what is owed is paid a season late; never-paid amounts are written off as bad debt at the close. |
| Honest value | A 4% loss on the credit part is a quarter of a 15% margin. The sheet shows khata's sales gain against its losses at each close, and marketing's share gained against its spend. |
| Money | About 80 kg of groceries a day for the village plus your town, you take about 40% at +15%: about 18,000 a season less 12,000 wage and 3,000 rent. Targets hired 3,000 to 8,000, run by you 15,000 to 25,000. |

## 4. Data

**New definitions** (loaded by `Sim.Content`; every value bounded):

| Definition | Field | Type | Default | Bounds | Notes |
| --- | --- | --- | --- | --- | --- |
| invoices | `terms` | list | cash, half (2 d), season (4 d) | | |
| invoices | `defaultTerm`, `wholesaleKgDay`, `lateTerms` | | half, 20, 1 | 0–60, 0–4 | |
| managers | `wageBase`, `wageProfitShare` | | 4,000, 0.10 | 0–1 | |
| managers | `styles[]` | | §3.2 | | price, quality, stock days, hire rule |
| managers | `missDay[5]` | Fixed | 0.20 to 0.02 | 0–1 | |
| exit | `baseMultiple`, `growth`, `risk`, `managerBonus`, `minMultiple`, `maxMultiple` | Fixed | 4.5, 1, 1, 1, 3, 6 | | |
| exit | `saleDays`, `rushDiscount`, `buyerRange`, `maxBranches` | | 3–6, 0.15, 0.90–1.05, 2 | | |
| exit | `investorShares`, `maxOutside`, `buyBackSeasons`, `investorValueMin` | | 10–30%, 0.40, 4, 50,000 | | |
| exit | `partnerStake`, `partnerProfit`, `buyOutRange`, `buyOutHours`, `buyOutBigHours` | | 0.20, 0.20, 0.90–1.10, 24, 48 | | |
| business kinds | `tuition`, `restaurant`, `grocery` | | §3.5, 3.7, 3.10 | | building, equipment, wage, hours, rent, demand, fee |
| equipment | `tempo` | | §3.6 | | price, lease, life, upkeep, capacity, trip hours, trip cost |
| benefits | `meal`, `health`, `festival`, `leave`; `loyalty`, `night` | | §3.8 | | |
| hr events | `accident`, `conflict`, `association`, `supervisor` | | §3.9 | | |
| shops | `marketing`, `khata` | | §3.10 | | budgets, limits, loss rates |

**New state** (saved):

| Schema | Entity | Field | Notes |
| --- | --- | --- | --- |
| 23 | Business | `Invoices` (list: seller, buyer, good, amount, issued, due), `Terms[buyer]`, `Receivable`, `Payable` | hashed only when used |
| 23 | Business | `ManagerId`, `Target`, `Report`, `Style`, `StarsSeasons` | |
| 23 | Business | `ValueHistory`, `Listing` (price, buyer kind, completes at, rushed), `BranchOf` | |
| 23 | Business | `Investor` (share, cash, since), `Partner` (citizen, stake, share), `OutsideEquity` | |
| 23 | Business | `Pupils`, `TuitionStars`; `Benefits` (4 flags, per employer), `NightShift` | |
| 23 | Equipment | `Tempo` (state, leased or owned, let-out switch), `Trips` | |
| 23 | Citizen | `TuitionSeasons` | skill gain cap |
| 24 | Place | `Supervisor`, `HrEvents` (accidents, conflicts, association state, refusals), `OutDays` | |
| 24 | Shop | `Marketing`, `Khata` (per household balance, limit, overdue, written off) | |

An older save loads with no invoices (every internal sale cash), no manager, no listing, no outside equity, no benefits, no tempo, no marketing or khata, so a town is unchanged until the player uses a feature. Fixtures: `schema23-growth.ftc` (Sprint 25), `schema24-people.ftc` (Sprint 26).

## 5. Commands and events

| Command | Parameters | Validation and rejection reasons | Effect |
| --- | --- | --- | --- |
| `SetTerms` | seller, buyer, term | `UnknownBusiness`; `NoTownship` | sets the term for new invoices |
| `HireManager` / `DismissManager` | business, applicant | `UnknownBusiness`; `HasManager`; `InsufficientFunds` | the contract flow of spec 015 |
| `SetManagerTarget` | business, kind | `NoManager` | profit or growth |
| `ListBusiness` / `CancelSale` | business, rushed | `UnknownBusiness`; `HasPartner` (buy out first); `AlreadyListed` | lists or cancels |
| `OpenBranch` | business, tile or pitch | `TooManyBranches`; the founding rules | founds the branch |
| `AnswerInvestor` / `BuyBackInvestor` | event, choice / business | `EventClosed`; `TooEarly`; `OutsideCap`; `InsufficientFunds` | takes the money, or pays the share |
| `AcceptPartner` / `BuyOutPartner` | event / business | as above | |
| `AnswerBuyOut` | event, accept or refuse | `EventClosed` | a sale, or nothing |
| `SetTuitionFee`, `SetMenu`, `SetQuality` | business, value | as other shops | |
| `BuyTempo` / `LeaseTempo` / `LetTempoOut` | | `NoTownship`; `AlreadyOwned`; as the tractor | |
| `SetBenefit` | employer, benefit, on | `NoClinic` (health cover); `NoTownship` | |
| `SetNightShift` | business, on | `NoGrid`; `NotOffered`; `NoSecondWorker` | |
| `HireSupervisor` | place, applicant | `TooFewWorkers` | |
| `AnswerHrEvent` | event, choice | the existing reasons | the accident, conflict and association choices |
| `SetMarketing` / `SetKhata` / `SetKhataLimit` / `CutOffHousehold` | shop, value | `UnknownBusiness`; `NoTownship` | |

| Event | Payload | Emitted when | Consumers |
| --- | --- | --- | --- |
| `InvoiceIssued` / `InvoicePaid` / `InvoiceOverdue` / `SupplyStopped` | seller, buyer, amount | a sale, the term end, the stop rule | the Orders tab, alerts |
| `ManagerHired` / `ManagerReport` / `ManagerLeft` | business | the board, the close | the business sheet, the season page |
| `BusinessListed` / `BusinessSold` / `SaleCancelled` / `BranchOpened` | business, price | commands, the sale's day | the sheet, the season page |
| `InvestorOffered` / `InvestorJoined` / `PartnerJoined` / `BuyOutOffered` / `ShareBoughtBack` | business, share, amount | the event engine, commands | the card, the books |
| `TempoTrip` / `TempoLetOut` | trip, pay | the mandi trip | the mandi sheet |
| `BenefitChanged` / `NightShiftOn` | employer | commands, the close | People, the business sheet |
| `AccidentHappened` / `ConflictRaised` / `AssociationFormed` | place | the HR roll | cards, the season page |
| `KhataGranted` / `KhataWrittenOff` | shop, household, amount | a sale, the close | the shop sheet |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects | Reversibility |
| --- | --- | --- | --- | --- |
| Longer invoice terms | nothing | cash arrives later | a late payer stops its supplier | change for later invoices |
| Hire a manager | 4,000 + 10% of profit | profit report; your hours free | passive income | dismiss any time |
| List a business | nothing | cash in 3 to 6 days at 90 to 105% | its jobs and income end | cancel before the day |
| Rush a sale | 15% off | cash in a day | as above | none |
| Take an investor | a share of profit | cash now | outside equity, a buy-back price | buy back after 4 seasons |
| Open a branch | the founding cost | a second set of books | the first's plan carries over | sell it |
| A benefit | 600 to 1,800 a worker | motivation, loyalty, reputation | does not pay in output alone | off at once |
| Night shift | +25% wage | sells more only if sold out | fatigue 0.07; accident chance ×2 | off at once |
| A tempo | 3,50,000 or 18,000 a season | cheaper trips, neighbours' pay | needs volume | sell it |
| Khata | losses of 1 to 8% of credit | more sales to poorer households | a receivable | cut off a household |
| Marketing | 2,000 to 8,000 a season | share gained while spent | fades a fifth a close | stop at once |

## 7. UI

Five previews, each an exact artifact that looks like the game and approved before its screens (the rule). Icons and sprites (tuition centre, tempo, restaurant, kirana) are rendered in Blender.

- **Preview 16 (Sprint 25):** the business sheet with an *Orders and invoices* tab (each customer's terms and the invoices with their age), the hiring board's manager post and card, the manager's close report.
- **Preview 17 (Sprint 25):** the *Sell or expand* sheet (valuation with its ?, a listing, a branch, an offer), an investor's offer card, the shares ledger, a buy-out card.
- **Preview 18 (Sprint 25):** the tuition centre's Today, the tempo on the Equipment sheet and the mandi sheet's "who carries it" line, the restaurant menu, the Benefits and night shift section.
- **Preview 19 (Sprint 26):** the supervisor post, the association, conflict and accident cards.
- **Preview 20 (Sprint 26):** the kirana, marketing and khata sections.

*Why?* rows: why an invoice is late, the stop rule, a manager's wage and his mis-set days, a valuation's parts (profit, multiple, growth, risk, book), a sale's price, a share's cost, each business's demand (which households could afford it and why others didn't), the tempo's cost per kg and its worth, each benefit's cost and effect and its "Does it pay?", the accident's chance and its multipliers, the conflict and association triggers, the khata limit and its loss rate, the marketing share gained.

## 8. Acceptance criteria

Tests are named for the story that builds them; the sprint's tests file under `tests/Sim.Core.Tests` and `tests/Sim.View.Tests`.

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | From the Township a sale between two businesses is an invoice on its term; cash terms pay at once; the farm's wheat, households, trader and supermarket stay cash | `InvoiceTests.Which_Sales_Are_Invoiced`, `InvoiceTests.Terms` |
| AC-2 | The village grocer and tea stall buy on terms, pay at term end, never default | `InvoiceTests.Wholesale_Buyers` |
| AC-3 | An unpaid invoice ages current, late, overdue; an overdue one stops the seller's deliveries until paid | `InvoiceTests.Ageing`, `InvoiceTests.StopRule` |
| AC-4 | Receivable and payable keep the books exact, cancel between your businesses in net worth, and cash flow shows the difference from profit | `InvoiceTests.Books_Reconcile`, `InvoiceTests.NetWorth_Cancels` |
| AC-5 | A manager's post, wage (4,000 + 10% of positive profit, never a loss from it), one a business, dismissal | `ManagerTests.Post_Wage`, `ManagerTests.One_Per_Business` |
| AC-6 | Each style sets the price, quality, stock and hire by §3.2; the mis-set day chance follows the stars | `ManagerTests.Styles`, `ManagerTests.MisSet_By_Stars` |
| AC-7 | The target, the close report, motivation after two misses, the refused raise, passive income | `ManagerTests.Report_Target`, `ManagerTests.Passive_Income` |
| AC-8 | Valuation follows §3.3 (base, growth, risk, bonus, clamp, floor at book) | `ExitTests.Valuation` |
| AC-9 | A listing completes in 3 to 6 days at 90 to 105% (rushed: 1 day at 85%), never under book; cancel; what goes; the gain taxed | `ExitTests.Sale`, `ExitTests.Rushed`, `ExitTests.Cancel`, `ExitTests.Gain_Taxed` |
| AC-10 | A branch of the same kind on another acre, at most 2, its own books and manager | `ExitTests.Branch` |
| AC-11 | An investor's offer card, the share, the capital, the profit share at the close, the cap, the buy-back after 4 seasons | `InvestorTests.Offer`, `InvestorTests.Share_Profit`, `InvestorTests.BuyBack`, `InvestorTests.OutsideCap` |
| AC-12 | A partner's stake and profit; the business cannot be sold or closed without buying out | `InvestorTests.Partner`, `InvestorTests.Partner_Blocks_Sale` |
| AC-13 | A rival's buy-out offer (90 to 110%, never under book), 24 or 48 h, refusing costs nothing, accepting settles outside shares first | `InvestorTests.BuyOut_Offer`, `InvestorTests.BuyOut_Accept_Settles` |
| AC-14 | The tuition centre: capacity, demand from the education budget, the fee presets, the skill gain capped at +10, stars | `TuitionTests.Demand`, `TuitionTests.Capacity_Fee`, `TuitionTests.Skill_Gain` |
| AC-15 | The tempo: price, lease, trip time and cost, two trips a day, the cheapest vehicle chosen, breakdown | `TempoTests.Machine`, `TempoTests.Trips`, `TempoTests.Cheapest_Vehicle` |
| AC-16 | The tempo let out: neighbours' trips first, up to 2 a day, farm income and not passive | `TempoTests.LetOut` |
| AC-17 | The restaurant: menu, inputs from the store then the market at +15%, demand from the leisure budget, capacity | `RestaurantTests.Menu_Buying`, `RestaurantTests.Demand` |
| AC-18 | Each benefit's cost and effect, loyalty on poaching and leaving, the reputation step | `BenefitTests.Costs_Effects`, `BenefitTests.Loyalty`, `BenefitTests.Health_Needs_Clinic` |
| AC-19 | The night shift: needs the grid and a second worker, offered after 3 sold-out days, wage +25%, fatigue 0.07, production businesses only | `NightShiftTests.Rules`, `NightShiftTests.Offered_When_SoldOut` |
| AC-20 | A supervisor's span, output, and the accident and conflict effects | `SupervisorTests.Span_Output`, `SupervisorTests.Halves_Risk` |
| AC-21 | Accidents: the daily chance and its multipliers, the card, the 24 h default, the severe case, nobody dies | `AccidentTests.Chance`, `AccidentTests.Card_Default`, `AccidentTests.Severe` |
| AC-22 | Conflicts: the chance, the three choices, the default | `ConflictTests.Chance`, `ConflictTests.Choices` |
| AC-23 | Associations: the triggers, accept, negotiate, refuse, the stoppage after a second refusal, none town-wide | `AssociationTests.Triggers`, `AssociationTests.Choices`, `AssociationTests.Stoppage` |
| AC-24 | The kirana: stock, demand shares, the rival's price cut, inputs from the store first | `KiranaTests.Stock_Demand`, `KiranaTests.Rival` |
| AC-25 | Marketing: the score gain per 1,000, the cap, the fade; khata: regulars, the limit, the score and kilos lift, the loss rates, the cut-off, the books | `MarketingTests.Score_Cap_Fade`, `KhataTests.Regulars_Limit`, `KhataTests.Losses`, `KhataTests.Books` |
| AC-26 | The ledger stays exact through every new flow; nothing creates money; the new businesses' plan and books follow the other shops | `GrowthLedgerTests.Reconciles` |
| AC-27 | Schema 23 and 24 round-trip; an older save loads with every feature off and a town unchanged; the fixtures carry on through two closes | `MigrationTests.Schema23_Growth`, `MigrationTests.Schema24_People` |
| AC-28 | Every number on the new sheets has a *Why?* in words; no raw factors | `WhyTests.EveryNumberHasWhy` (over the new situations) |
| AC-29 | The sheets and cards respond to scripted touch in the Editor; the render budget stays within 10% | the stories' Editor tours and render statistics |
| AC-30 | A town that answers nothing still works; no zero-time loop over the new commands | `GrowthTests.LeftAlone`, `GateTests.Growth` |
| AC-31 | Exploit bots find no money loop: terms and factoring, sell-and-buy-back, investor dilution, all four benefits stacked, khata with the worst payers, marketing spirals, night shifts without demand | `GateTests.InvoiceLoops`, `GateTests.SellBuyBack`, `GateTests.InvestorDilution`, `GateTests.StackBenefits`, `GateTests.KhataWorst`, `GateTests.MarketingSpiral`, `GateTests.NightNoDemand` |

## 9. Balancing hooks

| Metric | Expected effect | Tolerance |
| --- | --- | --- |
| Managed profit | at least the hired profit, below run-by-you | measured, reported |
| Invoices | a late payer's cash gap shows in cash flow; no money created | exact |
| Valuation | 3 to 6 times a year's profit; never below book | exact |
| Tuition centre | hired 3,000 to 8,000, run by you 18,000 to 25,000 a season | measured, tuned |
| Restaurant | hired 3,000 to 8,000, run by you 18,000 to 28,000 | measured, tuned |
| Kirana | hired 3,000 to 8,000, run by you 15,000 to 25,000 | measured, tuned |
| Tempo | payback about 10 seasons with neighbours' trips, no payback on own trips below about 45 days | measured |
| Benefits | each costs more than the output it adds; poaching 5% to about 2.6% with all four | ±1 point |
| Accidents | about 1.8% a season a worker | ±0.5 points |
| The Township's pace (day 101 competent) | unchanged within 2 days: nothing here starts before the Township | ±2 days |
| Archetype bots (ADR 0016) | within 20% on net worth and happiness | 1.13 holds |
| A town left alone | solvent after a year of defaults | none short |

## 10. Open questions

Each was closed with the owner, one per message, with my recommendation, before this spec.

- **G1 Invoices. Closed (owner, 2026-11-20, my recommendation):** invoices between businesses from the Township, a new wholesale buyer (the grocer and tea stall), Cash, Half-season and Season terms, ageing and the stop rule, a receivable and payable that cancel in net worth.
- **G2 Managers. Closed (owner, 2026-11-20, my recommendation):** wage 4,000 + 10% of positive profit, three styles, skill 1 to 5 stars setting the mis-set days (20% to 2%), a target and a close report, one manager a business.
- **G3 Valuation, selling and branches. Closed (owner, 2026-11-20, my recommendation):** 3 to 6 times profit adjusted for growth and risk, never under book; 3 to 6 days to sell or 1 day at 15% off; a branch of the same kind.
- **G4 Investors, partners and buy-outs. Closed (owner, 2026-11-20, my recommendation):** an investor for a share of equity and profit (buy back after 4 seasons), a partner with a fixed profit share, a rival's buy-out offer at 90 to 110% of the value.
- **G5 Tuition centre. Closed (owner, 2026-11-21, my recommendation).**
- **G6 Tempo. Closed (owner, 2026-11-21, my recommendation):** price 3,50,000 and a trip at 300.
- **G7 Restaurant. Closed (owner, 2026-11-21, my recommendation):** two dishes; highway travellers left out.
- **G8 Benefits and night shifts. Closed (owner, 2026-11-21, my recommendation):** per employer, four benefits, a night shift on the grid.
- **G9 Supervisors, associations, conflicts and accidents. Closed (owner, 2026-11-21, my recommendation):** the pay-the-wage and let-it-be defaults.
- **G10 Kirana, marketing and khata. Closed (owner, 2026-11-21, my recommendation):** khata's 8, 4 and 1% losses and the 0.15 marketing cap.
- **G11 Screens, scale and schema. Closed (owner, 2026-11-21, my recommendation):** five previews, the build order and cut line of §11, schema 23 in Sprint 25 and 24 in Sprint 26.

**Values I filled in while drafting (placeholders for the balance pass, ADR 0027; tell me if you want any changed before approving):** the valuation's base multiple 4.5 and its +1 manager bonus; the sale's buyer range 90 to 105%; the sale taking the building off your acre and the acre clearing; the gain over book taxed as business income; at most 2 branches a kind; the investor's offer sizes (10 to 30% in 5% steps), the 40% outside cap, the 50,000 value floor and the 48 h open card; the partner's 20% stake for a 20% profit share and "sets no prices"; the wholesale buyers' 20 kg a day at the trader's price; the mis-set chances between 1 and 5 stars (linear). None changes a rule the owner approved.

## 11. Cut first

If Sprint 25 runs over, cut in this order:
1. **US-246 restaurant** (Could, 3 points).
2. **US-245 tempo** (5).
3. **US-244 tuition centre** (5).
4. **US-258 benefits and night shifts** (3).
5. **US-247 investors, partners and buy-outs** (5).
6. **US-125 sell or expand** (5).

In Sprint 26: **US-259** first (Could), then US-260.

**Never cut:** US-123 invoices and US-124 managers; every number's *Why?*; the schema fixtures; the exploit checks (AC-31).
