# 0107. Phase 3 plan check: nine specs in eight sprints, the Township gate, the treasury's first day, kutcha and pucca houses, the cold store, part-harvests for hands, and the Phase 3 gate

- Status: Accepted (owner, 2026-11-13: each gap decided one at a time; the plan approved the same day; §13 and §14 approved; §15 written on the owner's "nothing Phase 3 needs moved to Phase 4" and taken forward by the owner the same day)
- Date: 2026-11-13
- Deciders: Keyur Patel
- Amends: the GDD's eras table (the Township's unlock), *Player journey and eras*, *Land, farming and livestock* (the map's ring, houses, storage, standing orders), *Economy* (the road tier), *Businesses* (the Township catalogue), *Community and civic services* (literacy, the clinic), *Governance and public finance* (the treasury's first day), *Roadmap* (the Phase 3 gate); ADR 0011 §3 (who pays the first doctor); ADR 0028 (the Township's pace); ADR 0063 §2 (the family house and its rent); ADR 0102 §3 (c) (decided here); ADR 0105's open question (decided here); spec 013 §3.4 (cold storage); spec 006 (hands' harvests); spec 014 (rent); spec 018 (the Township's literacy)
- Related: ADR 0019 (rolling wave), 0025 (sprints), 0052 (the map grows by era), 0057 (specs a sprint before their code), 0077 (the treasury with the Township), 0094 (frame time), 0095 and 0101 (the plan checks this one follows), 0097 (the Village's pace), 0106 (Phase 2 closed)
- GDD sections: as amended above

## Context

Phase 2 closed on 2026-11-13 (ADR 0106). Before any Phase 3 spec or code, the Phase 3 scope (E15, E16, F12.5, F12.6 and US-103) was cut into sprints, and every story was checked against the GDD, the ADRs and the code after PR #28. As it stands:

- Phase 3's feature files hold 26 stories at 103 points, every one "From the spec". There is no treasury; `SchedulingTypes.Treasury` is an empty slot in the system order.
- The Township's gate (GDD): 20 households, a staffed clinic with a migrant doctor, a road to the highway, 3 businesses, literacy above 40%. Literacy already reaches about 45% at the Village (ADR 0100). ADR 0028's "Township 4–6 weeks" predates the Village's 4–5 weeks (ADR 0097).
- Your people are the households on your land (ADR 0061, 0097). The old village is 6 drawn houses and a fixed catchment of 375 people, not citizens.
- `region.json`'s road tier is fixed at `track` (1.50 a kg; road 1.00, highway 0.50).
- A family house costs 60,000 and lets for 6,000 a season (a 10% yield a season), indexed every season (ADR 0063, 0102 §3).
- Potato, seed potato, vegetables, fruit and milk have no cold storage (spec 013 §3.4). Hands and labourers harvest only when the whole crop fits (ADR 0105).
- Promised for Phase 3 without a story: the Township's businesses, the brick kiln, investors and partner citizens (spec 016), rival buy-outs (ADR 0074, 0095), the combine harvester (ADR 0089), the cold store, the map's ring (ADR 0052).
- The Phase 3 gate ("4 weeks of play; the balancer finds no exploits") is undefined. Phase 2's frame-time p95s and the phone's golden replays are deferred (ADR 0106).

Before the plan check, the stale documents were fixed and Sprint 18's code reviewed (PR #28: bills priced at the close's index, no *Debt-free* after a foreclosure, card B for always paying from savings, the savings slider).

Twelve gaps came out of it.

## Decisions

1. **The specs and sprints (G1).** Nine specs, two to review a sprint, each a sprint before its code (ADR 0057); 2-week sprints as in Phase 2:

   | Spec | Covers |
   | --- | --- |
   | 021 Treasury and budget | F15.1 (US-138, US-139) |
   | 022 Taxes and policies | F15.2 (US-140, US-141) |
   | 023 The Township: the clinic, the link road, coverage, the old village, the unlock | F15.6 US-148, F15.5 US-146, F09.4 US-103 |
   | 024 Zoning, land value, rentals, upgrades, overlays | F15.3 (with US-261, leasing land out), F15.8 (the pucca house, rent by land value) |
   | 025 Utilities, construction, the map's ring, hills and apples, the cold store | F15.4, F15.5 US-253 and US-147, US-242, US-243 (the brick kiln), the farm's Phase 3 promises US-254 to US-257 (§15) |
   | 026 Town life: festivals, petitions, reputation, crime, inequality | US-149, F15.7 |
   | 027 Events, insurance, forward contracts | F16.1, F16.2 |
   | 028 Business growth: invoices, managers, valuation, exit, investors, Township businesses | F12.5, F12.6, F12.7 (but the kiln), US-258 to US-260 (§15) |
   | 029 Chronicle, Almanac, citizen stories | F16.3, F16.4 |

   Treasury and taxes go first, together: a treasury with nothing to receive has nothing to budget. The Township gate's spec comes second, before zoning, so its pace is measured early (the Village's was measured only in Sprint 17, ADR 0097). Sprints 20–26 build (2 weeks each), Sprint 27 is the 4-week playtest; 243 points with §13–§15 (the plan in `sprints.md`). The first branch is `sprint/20-treasury-and-taxes`.
2. **Who counts in the Township's town (G2).** The gate counts **20 of your own households**, as the Village's does. At the unlock **the old village's families join the town as a cohort** (about 30 households and 150 people, `population.json`): they pay taxes and fees, use the clinic and school, count in population and literacy, and are never individual citizens. The town goes from about 100 to about 250 people, inside the Township's 80–500. From the Township, zoning lets private builders add homes (F15.3). **The map grows a ring at the unlock** (ADR 0052): about 24 × 18 acres (from 16 × 12), with land for zoning, the highway link, hill parcels and more of the village drawn; its size and layout are set in spec 025 and previewed first (ADR 0064). The clinic and the road come before the ring and fit today's map.
3. **The clinic and its doctor (G3, G6).** Built like the school: you build the clinic (about 1,20,000) and a doctor's house (60,000) as a gift to the village, on a village plot. A migrant doctor comes once both stand. **The district pays the doctor's salary**, as it does the teacher's (a rural MBBS doctor costs about 1,80,000–2,25,000 a season, beyond any village's means); what draws the doctor is the house and a **hard-area allowance of about 15%** (about 30,000 a season), which you pay as a gift until the Township and the treasury pays after. A small fee a visit offsets the allowance. The clinic cuts sick days and slows health loss with age; the household card's *Healthcare* row is met. The string `needs.healthCareNotYet` becomes "A clinic is one of the Township's goals" (with US-148).
4. **The road to the highway (G4).** From the Village with 10 households or more, you petition at the council hall; the district builds a **paved link road** (PMGSY-style) on a community share of about 50,000 (a gift), about 4 days; the district road on the map's south edge is drawn paved and the road tier becomes `road` (1.00 a kg: the trader pays 0.50 a kg more, imports cost 0.50 less; potato +7.7%, wheat +2.1% at the farm gate). The `highway` tier waits for Phase 4's bonds. Each trader price's *Why?* names the road.
5. **The rest of the gate and the pace (G5).** **Literacy 60%** of your people aged 7+ (rural India about 68–74%; the old village joins after the gate at about 65%). **3 open businesses, each with at least one profitable season.** **The Township at 9–11 weeks using credit (day 63–77), about 15 saving (day 100–110)**, every order of the goals reachable, never stuck (ADR 0097's rule); eras about doubling (Hamlet 1 week, Village 5, Township 10, Town about 16, City about 24, the GDD's 4–6 months). ADR 0028's Township target is superseded. A miss comes to the owner with its numbers.
6. **The treasury's first day (G6).** Like real town councils, it lives on grants first: a district start-up grant at the unlock (about 1,00,000) and a per-resident grant each season (about 150), with conditions (the clinic staffed). Its own money from day one: the square's pitch rents (your shops still pay them) and the taxes spec 022 sets (a house tax about 1,000 a house a year). It takes over from you at the unlock: the school's upkeep, the clinic's upkeep and the doctor's allowance, the link road's upkeep (its first departments: Education, Health, Roads, the council's office). **The wall between the two wallets:** you may give to the treasury (Gifts to the village); it never pays you, directly or otherwise, in Phase 3 (using public money for your own land or businesses is Phase 4's conflict-of-interest rule). **It never goes below zero in Phase 3** (bonds are Phase 4): at each close departments are paid in budget order, what can't be paid goes unpaid and its service decays, warned a day ahead. Spending follows the confirm cards (ADR 0101 §12); every movement is on the treasury's own itemised statements (spec 021). Amounts are content, measured in spec 021.
7. **Township businesses and Phase 2's leftovers (G7).** A new feature, **F12.7 Township businesses**: the **brick kiln** (Must; construction's bricks, otherwise bought from the trader at the regional price, limited a day), the **tuition centre** (Should), **tempo transport** (Should), the **restaurant** (Could). **Builders** are townspeople with a mason trade on the hiring board, inside F15.4 (no construction firm). **Investors, partner citizens and rival buy-outs** join F12.6 (Should, US-247). To Phase 4: the hardware store, private clinic, tailor, carpenter, a construction company as a firm. **The combine harvester: Won't** in Phase 3 (a harvest is a free tap, ADR 0089).
8. **Rent (G8, ADR 0102 §3 (c)).** The rent was fair to tenants (12% of income, ADR 0071); the house was too cheap for a pucca rent. Now:
   - **Two kinds of house.** Today's family house is the **kutcha house** (60,000), rent about **2,500** a season. From the Township the **pucca house** (about 2,40,000), rent about **7,000**, built by private builders or upgraded from a kutcha house for the difference (F15.8).
   - **Rent = the kind's base × location × the price index.** Location now is road access (by the district road ×1.20, the village lane ×1.00, inner ×0.85); from the Township it is land value (F15.3). Each rent's *Why?* shows the parts.
   - **Yearly leases.** A lease runs 4 seasons at a fixed rent from move-in; at renewal the rent moves toward the market rent, the rise capped at 10% a year; the renewal card offers the market rise (default), a smaller one (the tenant's happiness and loyalty rise) or none; a rent past about 30% of a family's income risks it leaving (existing rules). Rent control (F15.2) later caps rises town-wide.
   - **Houses already built** become kutcha houses at their cost and book value; a signed lease keeps its rent until renewal. A hand's house as part of his pay follows the kutcha rent.
   - Built in Sprint 20 as **US-240** (save schema 18: each lease's start and rent); the bots re-measure the Village's pace and a slip past 5 weeks comes to the owner. The pucca house and land value come with spec 024.
9. **The cold store (G9, spec 013 §3.4).** From the Township, **cold-store space in the mandi town, rented by the tonne for a season** like the warehouse's (ADR 0087), about 1.00 a kg a season, upfront at the close and in *Next season, already due*. Potato and seed potato at about a tenth of their spoilage (Must), fruit (mango, guava, apple) about a quarter (Should), vegetables (Could), milk Won't (the co-op chills it daily). Potato from Spring to the Monsoon nets about +0.40 a kg after two seasons' rent: a thin, risky margin, as in reality. Owning one is Phase 4. **US-242**, in spec 025.
10. **Hands and labourers take part-harvests (G10, ADR 0105).** A hand with a *store* order takes what fits each hour and the rest stands ready under the 72 h grace; each part costs its share of the harvest's hours. Labourers the same; when nothing fits they go home and the field waits. An alert ("Store full: 640 kg of wheat still standing on the north field, taken as room appears", with *Sell some* and *Rent space*) and the away card say what still stands. **US-241**, Sprint 20; no save change (`TakenGrams`, schema 17); bots re-measured, a golden's hash re-pinned only if it moves, said in the commit.
11. **The Phase 3 gate (G11).** As Phase 2's, parts A and B.
    - **No exploits** is ADR 0016's rule built at last (US-099: agri-hub, merchant and education archetypes within 20% on median net worth and happiness; no stress bot above the best archetype by over 20%), measured on 200 seeds to game year 8 (the full 1,000 seeds at year 12 at the Phase 4 gate), plus Phase 3's checks (**US-248**): zero-time loops over every new command pair; no route from the treasury to your wallet; an *insure everything* bot no better on the median than an uninsured one; no riskless forward contract; a found-and-sell bot not over 20% above the competent bot; a town with every tax at 0 loses services and happiness; a landlord raising every rent to the cap no better than a fair one; late payment of your own invoices makes no money; and the earlier checks (never stuck, an exact ledger, bounded prices).
    - **The 4-week playtest:** the Village bot (credit) plays to the close after the Village (about day 40), the town shown to the owner first; a Release build; 4 weeks with the play log and a diary line a day.
    - **Part A:** A1 the bots' pace (decision 5); A2 the owner reaches the Township from the saved town within 4 weeks, or the log and diary show why; A3 a session each day, the diary's reason more than "testing" on most; A4 no blocker (ADR 0047's list, plus a treasury statement that doesn't add up, public money moving unseen, a tax charged unshown); A5 at two closes after the Township the owner explains from the screens why the treasury's balance moved and one budget variance; A6 build health (the Release smoke list, both frame-time p95s and golden replays on the phone, CI green); A7 decision 11's exploit checks.
    - **Part B**, before Launch: fresh players explain public versus personal money unprompted.
    - **A part A miss:** tuning in content with the bots re-run, or a rule change brought for approval; Phase 4's code waits, its spec writing may go on.
    - **The owner's rule (2026-11-13): never defer to the device what the Unity Editor can check.** Each story runs every check the Editor can, by scripted touch, before it's done; only device-only checks wait for the phone (frame-time p95s, golden replays on IL2CPP, OS notifications and quiet hours, haptics, real time away in the background, the phone's number pad, rotation and lock, the real safe area, the Release install). Every checklist marks its device-only rows.
12. **Frame time with the ring and 250 people (G12).** The GPU has about 2 ms of room (Sprint 17: main 5.09 ms, GPU 8.72 ms; limits 8.3 and 11, ADR 0094). Phase 3's **first Development build (Sprint 20)** measures both p95s and the phone's golden replays, closing Phase 2's deferred items; the ring's build measures again. The old village's cohort is drawn as **at most about 40 walkers** (content); ring tiles use the map's chunking and are skipped off screen. Each visible story reads the Editor's rendering statistics (draw calls, batches, sprites) at the most crowded view and flags growth over 10% against the last (**US-101**). Over 10 ms GPU on the phone: cut the edge rings' detail, then walkers, never the farm or the village, and bring the numbers first.

13. **The scope of every Phase 3 story (owner, 2026-11-13: "described without any ambiguity").** Each story in its feature file now says what is in Phase 3, what is not, and what its spec still sets (numbers and wording only). The scope lines that are new decisions, beyond §1–§12:
    1. **Taxes:** property tax on every house (yours and the old village's too), on land value; business tax on profit (your businesses too); fees. **Sales tax waits for Phase 4** (it moves every shop's price).
    2. **Policies:** free school meals, farm input subsidy, small business grant, minimum wage, rent control; a night market as Could; **at most 3 active**; tourism promotion and pollution limits wait for Phase 4 (no hotels, no industry yet).
    3. **The wall, spelled out:** subsidies and grants go to townspeople and neighbours, **never to your own farm or businesses** in Phase 3.
    4. **Zoning:** residential, commercial and agricultural; a zoned acre of yours is offered to builders at its land value; builders also buy the ring's unowned land; industrial and mixed-use zones wait for Phase 4.
    5. **Construction:** bricks, cement and timber, masons and time **from the Township on**; buildings before it keep the Phase 2 rule.
    6. **Utilities' Phase 3 steps:** water tower, diesel generator then the grid, drains, waste collection; plants, recycling and solar wait for Phase 4.
    7. **Civic buildings:** besides the clinic, a watchman's post, a community hall and a playground, built and staffed by the treasury; the school gets a reach.
    8. **Events:** weather, economic, business, workforce, social and opportunity families, and fire and epidemic as disasters; river floods and recessions wait for Phase 4; a minor event while you're away takes its named, safest default.
    9. **The map's ring is its own Must story (US-253, 5 points)**, with gravel lanes the council builds to open zones; hill parcels and apples stay Should (US-147, moved to Sprint 24).
    10. **Overlays:** fertility, land value, coverage, happiness, crime; pollution and traffic wait for Phase 4.
    11. **Invoices** cover every business-to-business sale (the village grocer and tea stall too), not the trader or the supermarket; households pay cash.
14. **A mandi trip with several crops (owner request, 2026-11-13).** The cart already carries any mix (`GoToMandi` takes a list), but the sheet offered each crop only 0, 500, 1,000 or 2,000 kg, ignoring the room the other crops take, and didn't show which crop pays best. Now each crop's load is dragged or typed in kg (the savings sheet's control) up to the room left, the rows are ordered by the mandi's gain over the trader, and *Fill with what pays best* loads the trolley in that order. **US-252**, 3 points, Sprint 20; no save change.

15. **Nothing Phase 3 needs is left to Phase 4 (owner, 2026-11-13).** Every "Not in Phase 3" line was checked against what Phase 3's own stories and the Township need, and every Phase 3 promise in specs 001–019 was traced to a story:
    - **Pulled into Phase 3:** the diesel generator's smoke (it lowers health and land value in its reach, so the grid is worth its cost; the coverage overlay shows it); a fire is softer within the water tower's reach and covered by property insurance (fire is a Phase 3 event, the fire station is the Town's); land can be sold slowly at its land value or at once at 80% (spec 002); shops' location scores from land value (spec 017); the town planner advisor with the treasury (spec 018).
    - **Phase 3 promises made stories:** raising calves into the herd (US-254, Should; spec 012); silage, a chaff cutter and more sheds (US-255, Could; spec 012); your tractor for neighbours (US-256, Could; spec 013); a tube well an acre and solar pumps (US-257, Could; spec 013); benefits and night shifts (US-258, Should; spec 015); supervisors, worker associations, conflicts and accidents (US-259, Could; spec 015); your kirana, a marketing budget and khata (US-260, Should; spec 017); leasing land out (US-261, Should; spec 019).
    - **Rightly later, tied to the GDD's eras:** bonds and public debt, elections and council mood, scandal (no Phase 3 system needs them: the treasury never borrows); sales tax; tourism (needs the Town's hotels); industry, pollution limits and industrial zones; treatment and sewage plants, recycling, a power plant (the City's gate); villas and apartments; traffic; the Town's civic buildings (police, fire station, hospital, secondary school); river floods (need an embankment); educated roles and locally trained professionals (ADR 0011: from the Town); franchises, shares, trucks and hotels. The combine harvester stays Won't (§7).

## New and re-pointed stories

| Story | Feature | Points | Priority |
| --- | --- | --- | --- |
| US-138 Treasury | F15.1 | 5 → 8 | Must |
| US-139 Budget cycle | F15.1 | 3 → 5 | Must |
| US-140 Taxes | F15.2 | 3 → 5 | Must |
| US-141 Policies | F15.2 | 5 → 8 | Must |
| US-142 Zoning | F15.3 | 5 → 8 | Must |
| US-143 Land value | F15.3 | 3 → 5 | Must |
| US-144 Utilities | F15.4 | 5 → 8 | Must |
| US-145 Construction (builders with a mason trade) | F15.4 | 5 → 8 | Must |
| US-146 The link road | F15.5 | 3 → 5 | Must |
| US-147 Hill parcels and apples | F15.5 | 3 → 5 | Should |
| **US-253** The map's ring | F15.5 | 5 | Must |
| US-148 Civic buildings: the clinic first | F15.6 | 5 → 8 | Must |
| US-149 Festivals | F15.6 | 3 | Should |
| US-150 Petitions | F15.7 | 3 | Must |
| US-151 Reputation and fairness | F15.7 | 5 | Must |
| US-152 Building upgrades (kutcha to pucca) | F15.8 | 3 → 5 | Must |
| US-153 Overlays | F15.8 | 3 → 5 | Must |
| US-154 Events | F16.1 | 5 → 8 | Must |
| US-155 Insurance | F16.2 | 3 → 5 | Must |
| US-156 Forward contracts | F16.2 | 3 | Should |
| US-157 Chronicle | F16.3 | 5 | Must |
| US-158 Almanac | F16.3 | 3 | Must |
| US-159 Citizen stories | F16.4 | 5 | Should |
| US-123 Invoices | F12.5 | 5 | Must |
| US-124 Managers | F12.6 | 5 → 8 | Must |
| US-125 Sell or expand | F12.6 | 5 | Should |
| US-103 Township unlock (the old village joins) | F09.4 | 2 → 5 | Must |
| **US-240** Rent: kutcha houses, location, yearly leases | F11.2 | 5 | Must |
| **US-241** Hands and labourers take part-harvests | F06.3 | 3 | Must |
| **US-242** The cold store | F13.3 | 3 | Should |
| **US-243** Brick kiln | F12.7 (new) | 5 | Must |
| **US-244** Tuition centre | F12.7 | 5 | Should |
| **US-245** Tempo transport | F12.7 | 5 | Should |
| **US-246** Restaurant | F12.7 | 3 | Could |
| **US-247** Investors, partners and buy-outs | F12.6 | 5 | Should |
| US-099 Archetype bots | F20.1 | 5 → 8 | Must |
| US-100 Era-gate reachability | F20.1 | 3 | Must |
| US-101 Device performance, and the Editor's rendering statistics | F20.1 | 2 → 3 | Must |
| **US-248** Phase 3 exploit checks | F20.1 | 5 | Must |
| **US-249** The playtest town and the Release build | F10.3 (new) | 3 | Must |
| **US-250** The Phase 3 playtest | F10.3 | 3 | Must |
| **US-251** Tune and the gate decision | F10.3 | 3 | Must |
| **US-252** A mandi trip with several crops | F04.1 | 3 | Must |
| **US-254** Raise calves and grow the herd | F13.1 | 3 | Should |
| **US-255** Silage, a chaff cutter and more sheds | F13.1 | 2 | Could |
| **US-256** Your tractor for neighbours | F13.2 | 3 | Could |
| **US-257** A tube well an acre and solar pumps | F13.2 | 3 | Could |
| **US-258** Benefits and night shifts | F11.4 | 3 | Should |
| **US-259** Supervisors, worker associations, conflicts and accidents | F11.4 | 5 | Could |
| **US-260** Your kirana, marketing and khata | F12.3 | 5 | Should |
| **US-261** Lease land out | F15.3 | 3 | Should |

243 points in all (208 when the plan was approved; US-252 and US-253 under §13–§14; US-254 to US-261 under §15). **If Phase 3 runs over,** cut Could first (the restaurant, vegetables in the cold store, US-255, US-256, US-257, US-259), then Should in this order: US-258, US-260, US-261, US-254, citizen stories, festivals, forward contracts, tempo, tuition, investors and buy-outs, sell or expand, hill apples. **Never cut:** the treasury, taxes, the gate's pieces (clinic, road, unlock), the map's ring, rent realism, the multi-crop mandi trip, the exploit checks.

## Consequences

- The GDD, `epics.md` (a Phase 3 gate in parts A and B; F12.7 and F10.3), the feature files, `sprints.md` (Sprints 20–28) and `STATUS.md` carry these decisions in this commit; CLAUDE.md's table gains this ADR and ADR 0028's row points here.
- The specs written from it amend: spec 014 (rent, US-240), spec 006 (hands' harvests, US-241), spec 013 §3.4 (the cold store, in spec 025), spec 018 (the Township's literacy, in spec 023).
- Save schema 18 in Sprint 20 (leases); later schemas +1 as stories need them, each with a migration and a fixture.
- Every balance number named here is a starting value in content, measured with the bots in its spec; a miss comes to the owner with its numbers.
