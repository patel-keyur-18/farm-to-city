# Sprint 25 build: business growth (invoices, managers, selling and investors, benefits and night shifts, the tuition centre, the tempo and the restaurant)

- **Build:** a Development build from the head of branch `sprint/25-business-growth` (made at the end of the sprint; see the pull request).
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. This is **a Development build**, so it has the developer panel. Delete `unity/FarmToCity/Builds/iOS` before building (this avoids the macOS 27 crash). **Quit Xcode before building.**
- **Run in the Editor first (scripted touch, on a copy of your town):** every row below that is not marked **Device only**. Captures are in `docs/art/s25-*.png`. **Only a device can check:** the rows marked **Device only**. This list **carries the device-only rows of the Sprint 23 and Sprint 24 checklists** (rows 28 to 36) so one pass on the phone covers all three sprints.

## Before you install: keep your town safe

1. In the **old** app, go to Menu → Settings → **Export**. Keep that copy until this build is checked.
2. **Install over the old app. Don't delete the app first**: deleting it deletes your town.
3. This build upgrades your town as it loads (save schema 22 to 23): nothing in your town moves. A Hamlet or Village town is unchanged; the new parts start at the Township. **A town saved by this build cannot go back to an older build.**

## What this build changes (story by story)

- **Invoices (US-123).** From the Township, a sale from one of your businesses to another, or to the village's grocer and tea stall, is an invoice on a term you set (cash, half a season or a season). A buyer that cannot pay lets it age; a seller stops delivering to a buyer more than a term late. Profit is not cash, and the books show it.
- **Managers (US-124, reworked at the balance pass, ADR 0164).** A manager **works the business himself** (it has no other worker), sets its price, quality and stock in his style, is paid about 60% of what a worker of the trade earns plus a tenth of the profit, and reports at each close. A business with a worker must let the worker go first. A managed business earns more than a hired one and less than one you run yourself.
- **Selling and branches (US-125).** *Worth now* with its parts under a **?**; list a business for 3 to 6 days or sell in a day at 85%; a second and third business of a kind are branches.
- **Investors, partners and buy-outs (US-247).** Offers at the close; an investor's share of equity and profit; a partner from your own households; a rival's buy-out card. **Buying an investor back costs his share of the worth now, and never less than he put in** (ADR 0164).
- **Benefits and night shifts (US-258).** Four benefits per employer (a meal, health cover, a festival bonus, paid leave); a night shift on the grid for the mill, bakery, dairy and sweet shop when the day shift falls behind.
- **Tuition centre (US-244).** Your households' children and the old village's come for lessons; fees by the day; stars set the take-up; pupils gain skill.
- **Tempo (US-245).** A vehicle with legs for the mandi; the cheapest ride is chosen; idle legs are let out to neighbours at midnight.
- **Restaurant (US-246).** A kitchen that cooks to order from the store, your own businesses and the market; diners from the households' leisure money; the town dhaba is the rival; a switch for each dish.
- **Without any of it** a Hamlet or Village town is as before.

## Checks

| # | What to try | What to expect | Device only | Result |
| --- | --- | --- | --- | --- |
| 1 | Open the app with your town | The HUD draws, nothing is lost, the console shows no errors | | Editor ✓ |
| 2 | Township town: a mill and a bakery open; a business's **Orders** tab | Each customer with its term; invoices with their age; a **?** on each | | Editor ✓ (`s25-123-orders.png`, `s25-123-invoices.png`, `s25-123-why-owed.png`) |
| 3 | Set the mill's term for the bakery to **Season**; wait; let the bakery run low on cash | An overdue invoice stops the mill's atta; a warning line and a *Put in cash* button | | Editor ✓ (`s25-123-stopped.png`, `s25-123-put-in.png`, `s25-123-alert.png`) |
| 4 | The bakery's **Books** | Receivable and payable lines; cash flow says why profit and cash differ | | Editor ✓ (`s25-123-books-bridge.png`) |
| 5 | A business with a worker → **Settings** → Manager → **Hire** → Hiring board → **Manager** → **Send word** | The post says "about 60% of what a worker of that trade earns a season, and a tenth of the profit" | | Editor ✓ (`s25-balance-manager-board.png`) |
| 6 | **Offer** a manager the job of a business that has a worker | The business's line says *A worker works there: let them go first, he works it himself*; every offer is greyed with the same reason | | Editor ✓ (`s25-balance-manager-offer-staffed.png`) |
| 7 | Let the worker go, then **Offer** at the asked wage | A confirm card; the manager is hired; the business sheet says *Who works here: …, the manager*; the business keeps working | | Editor ✓ (`s25-balance-manager-offer-free.png`, `s25-balance-manager-works.png`) |
| 8 | The manager's pay **?** | The base is a share of a worker's wage in that trade; his tenth of the profit; "he works the business himself, so there is no other worker to pay" | | Editor ✓ (`s25-balance-manager-why.png`) |
| 9 | Close a season with a manager; open his **Report** | Target met or missed, what he changed and why, days he mis-set | | Editor ✓ (`s25-124-report.png`, `s25-124-why-miss.png`) |
| 10 | A business → **Settings** → **Worth** | *Worth now* with a **?** for the parts; *Sell or expand*; a rushed sale shows 85% | | Editor ✓ (`s25-125-settings.png`, `s25-125-sell.png`) |
| 11 | List a business; wait for its buyer | A receipt: price, tax, what came to you; the acre is yours again | | Editor ✓ (`s25-125-listed.png`, `s25-125-receipt.png`) |
| 12 | A second business of the same kind | It is a branch of the first; the first's research carries over | | Editor ✓ (`s25-125-branch-research.png`) |
| 13 | An investor's offer card (bell) | The cash, the share, the profit share; **Accept** shows a confirm card; the share appears under **Owners** | | Editor ✓ (`s25-247-investor-card.png`, `s25-247-owners.png`) |
| 14 | **Owners** → **Buy back** after four seasons | The price is the worth now times his share and **never less than he put in**; its **?** says so | | `InvestorTests.BuyBack_Floor`, `GateTests.InvestorDilution` (the price line itself is unchanged on screen) |
| 15 | A rival's buy-out card | 90 to 110% of the worth; **Accept** is a sale; **Refuse** costs nothing | | Editor ✓ (`s25-247-buyout-card.png`) |
| 16 | A business → **Settings** → Benefits; switch **A meal** on | The cost for the rest of the season, its motivation, a **?** with "Does it pay?" | | Editor ✓ (`s25-258-benefits.png`, `s25-258-benefit-why.png`) |
| 17 | With the grid built and a mill that falls behind | A night shift is offered with its wage and what it adds; switching it on hires a night worker | | Editor ✓ (`s25-258-night-offered.png`, `s25-258-night-on.png`) |
| 18 | Menu → Businesses → **Start a tuition centre** (an acre) | Research, plan, place; pupils from your households' children; fees at the end of each batch | | Editor ✓ (`s25-244-research.png`, `s25-244-today.png`) |
| 19 | A child's card | *Lessons at the tuition centre* and skill gained | | Editor ✓ (`s25-244-citizen.png`) |
| 20 | Buy a tempo (Menu → Store → Equipment) | Price, lease, trips; *Let it out for neighbours*; the mandi sheet says who carries the load | | Editor ✓ (`s25-245-equipment-offer.png`, `s25-245-mandi.png`) |
| 21 | Menu → Businesses → **Start a restaurant** (pitch or acre) | Research with the town dhaba as rival; a plan; the board's cook | | Editor ✓ (`s25-246-research.png`, `s25-246-plan.png`, `s25-246-board.png`) |
| 22 | The restaurant's **Today**, **Settings** | Diners, portions, the sittings; a switch for each dish (the last one stays on) | | Editor ✓ (`s25-246-today.png`, `s25-246-menu-switch.png`) |
| 23 | Customers at the restaurant on the map | People at the counter at lunch and in the evening | | Editor ✓ (`s25-246-customers.png`, `s25-246-pitch-map.png`) |
| 24 | Every number's **?** on the new sheets | A *Why?* in words, never a raw factor | | `WhyTests`, view tests |
| 25 | Text size 125% on a business sheet and the offer card | Nothing cut off | | Editor ✓ |
| 26 | A Hamlet town loads and plays as before | Nothing new shows before the Township | | `MigrationTests` |
| 27 | An older save (schema 22) | Loads, upgrades, plays | | `MigrationTests.Schema23_Growth`, `EverySchemaLoads` |
| 28 | Tap targets on the new cards (offer, investor, buy-out, confirm) | Every button easy to hit with a thumb | **Device only** | |
| 29 | Pan and zoom a Township town with a restaurant, a tuition centre and a tempo for a minute | Smooth | **Device only** | |
| 30 | Developer panel → run the frame check on the Township map zoomed out and panned for a minute | Main-thread p95 ≤ 8.3 ms and GPU p95 ≤ 11 ms (ADR 0094); about 60 fps; under 1% of frames over 25 ms. Write both p95s down | **Device only** | |
| 31 | Developer panel → **Golden replays** | ALL PASS | **Device only** | |
| 32 | Developer panel → **Away cap (72 h)** with a Township open and invoices running | It finishes inside the budget shown (2,000 ms) with no hitch; the away card lists what happened | **Device only** | |
| 33 | *(carried from Sprint 24)* Close the app for a few hours, open it | The away card lists any default taken; no disaster struck while you were away | **Device only** | |
| 34 | *(carried from Sprint 24)* Tap targets on the event, petition, festival and confirm cards | Every button easy to hit with a thumb | **Device only** | |
| 35 | *(carried from Sprint 24)* Pan and zoom a Township town with an orchard for a minute | Smooth | **Device only** | |
| 36 | *(carried from Sprint 23)* Pan and zoom the biggest town you have for a minute; tap targets on the lane, build and overlay cards | Smooth, with the ring open; every button easy to hit | **Device only** | |

## Render statistics (Editor, 2026-11-23, a Township town in a storm with 73 people, a mill and a bakery)

| View | Draw calls | Triangles | Visible sprites |
| --- | --- | --- | --- |
| The whole map | 1,064 | 23,775 | 1,310 |
| The village | 475 | 10,489 | 560 |
| The farm | 338 | 7,474 | 375 |

No metric grew over 10% for the whole map or the village. The farm's triangles read +12% against the restaurant reading (6,666 to 7,474): the reading was taken in a rainstorm with two businesses on the farm acres; no map code changed in this change.

## What to report

Anything that looks wrong, a tap that does nothing, a number without a **?**, a red line on the console (send its top line), or a town that no longer loads.
