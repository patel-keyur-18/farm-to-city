# Sprint 18 build: your statements, Your wealth, the savings deposit, milestones and confirm cards

- **Build:** the head of branch `sprint/18-statements-and-playtest`, a Development build made from the open Editor on 2026-11-11 (Unity: Build Succeeded; the one console error is the CLI's 5 s eval timeout; the unsigned Xcode Release compile: BUILD SUCCEEDED). Rebuild from the merged `main` if you prefer, with the guide in `how-to-build-ios.md`.
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. This is **a Development build**, so it has the developer panel. Delete `unity/FarmToCity/Builds/iOS` before building (this avoids the macOS 27 crash).
- **Run in the Editor first (2026-11-11, scripted touch, on a copy of your town):** see the Notes column. Captures are in `docs/art/s18-check-*.png`, and each story's own tour is in `docs/art/s18-135-*`, `s18-136-*`, `s18-137-*` and `s18-235-*`. Set up by script, in the session only: test coins, 30,000 coins put into the deposit, dev time travel. **Only a device can check:** 1 (your own town's update on the phone), 27 (reduced motion), 28 (frame time), 29 (notifications), 30 (your normal morning), 31 (Snapshot and Restore) and 33 (rotation).

## Before you install: keep your town safe

1. In the **old** app, go to Menu → Settings → **Export**. A copy of your town goes to the Files app (On My iPhone → Farm to City). Keep it until this build is checked.
2. **Install over the old app. Don't delete the app first**: deleting it deletes your town.
3. This build upgrades your town as it loads (save schema 15 to 16):
   - Your fields, cash, people, houses, animals, loans, businesses, shops and Codex cards stay as they were.
   - Seasons already closed keep what they showed: their cash flow reads "not itemised then" where the old save didn't record the rows. The next close is itemised.
   - No savings deposit yet; no milestones yet. At your first close after the update, each milestone your kept seasons show was reached is recorded with its real season, in one toast.
   - The first time it loads, the app also saves your town as it was before the update in the Files app, as `FarmToCity-before-update-schema15.ftc`.

If anything looks wrong with your town after the update, stop and report it before playing on. Either copy can bring it back.

## What this build changes

- **Every amount due (US-235).** Money and loans → *Your season* → *Next season, already due* now lists everything the close takes from your cash, in the order it takes it: loan payments, your family's living, the hands' wages, warehouse space, water, the school, machines. Your businesses' own charges are listed below it, outside your total.
- **Confirm cards, as Hay Day's (US-235).** Everyday play is one tap. **Buying land, building (a house, a hut, the cattle shed, the school), starting a business, a loan:** a card with the numbers (cost, loan, what you pay now, first payment, cash after); *Not now* or the green button. **Selling land, closing a business, letting go of someone:** a card you **hold for one second** ("This can't be undone"): the red button fills as you hold.
- **Menu → Your land (US-235).** What you own (*Yours*) and land for sale (*For sale*). **Sell** is only here: the field sheet no longer has it.
- **Your season's books, as a business keeps them (US-137).** Tapping a closed season opens one page with three tabs: **Profit and loss**, **Cash flow** and **Balance sheet**, each line beside the same season last year (or "not itemised then"). The cash flow adds up to the coin, every row has a **?**, and there's never an *Other* row. *Your people* (wages paid against what each asks, stars, your name as an employer) sits under the profit. Money and loans → *Your wealth* → **Your books** lists the last 8 closed seasons. After a Winter close, *Your year*.
- **Your wealth (US-135).** Net worth's trend in **today's coins** (so rising prices don't look like growth), what it is made of, debt to assets with its band, the emergency fund (seasons of bills your cash and savings cover), passive income against your living. **Borrow** is its own sheet of lenders.
- **The savings deposit (US-236).** Your wealth → **Savings deposit**: put in and take out (5,000 / 10,000 / 25,000 / 50,000 / all), 1.5% a season added at each close, never collateral. **Nothing leaves it unless you say yes.** On the last day of a season, if the close would take more than your cash, the bell shows **Short at the close by …** with four buttons: **Pay from my savings**, **Sell from the store**, **People**, **Borrow**. *Pay from my savings* covers that close only (**Take it back** undoes it); the sheet's standing choice **Always pay from my savings** does it without asking, and the away card says so. The phone sends the same warning if you're away.
- **Wealth milestones (US-136).** The end of Your wealth: *First lakh saved*, *Twice what you inherited* (in today's coins), *First rent*, *Debt-free* and *Passive income covers your living*. Reached ones show their season; the rest are dimmed with where you stand. A toast says so after the close. Nothing is given for a milestone.
- **Balance (ADR 0102).** Debt to assets at the Village is 6–8% for the bots; borrowing to deposit loses in every seed; passive income covers living on day 20–40 at the Village (kept; the rent's realism goes to Phase 3).

**Your questions:**
1. Can you read the season page and say, in your own words, why your cash moved differently from your profit?
2. Is the warning on the day before a short close clear, and are the four buttons the ones you'd reach for?
3. Is it clear that nothing comes out of your savings unless you say yes?
4. Do the confirm cards feel right: not nagging for small things, firm for the ones you can't undo?
5. Do the milestones feel like progress, or like a to-do list?

## Checks

**The developer panel:** hold the time card for 3 seconds. It has **+1 hour**, **+1 day**, **+1 season**, **Away cap (72 h)**, **Next era**, and **Snapshot** / **Restore**.
- Take a **Snapshot** after check 1. **Restore** at check 31 puts your town back.
- Several checks spend money. If cash runs short, play a day on (**+1 day**) or skip that check and say so.
- Checks 17–19 need the last day of a season: **+1 day** until the time card says "last day".

| # | Story / AC | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | Migration (schema 17) | Launch with your town | Same cash, fields, people, houses, animals, loans, businesses; Menu → Codex as before. In the Files app: `FarmToCity-before-update-schema15.ftc` | | **Screenshot**. Editor: the owner's town copy loaded; `MigrationTests.Schema15_Statements` covers a schema-15 save |
| 2 | US-235, AC-1 | Money and loans (tap your cash) | Opens on *Your season*; *Next season, already due* lists loans, living, wages, water, the school… each with its **?**; the total is what the close takes | | Editor: the due block with the pump and the school (`s18-235-due-water-school.png`) |
| 3 | US-235 | Tap land for sale → **Buy this land** (or **Buy with a bank loan** → a term → **Buy this land**) | A card: *Buy this land?* with cost, bank loan, you pay now, first payment, **cash after**; *Not now* / *Buy for …* | | **Screenshot**. Editor (`s18-check-card-loan.png`): 2,06,590 − 1,07,490 = 99,100; *Not now* left cash and loans as they were |
| 4 | US-235 | *Not now*, then the same again and **Buy** | *Not now* changes nothing; *Buy* takes exactly the card's amount | | Editor (US-235's tour): cash down by exactly the card's cost |
| 5 | US-235 | Businesses → *Start a flour mill* → *Make a plan* → *Choose the acre* → **Build here** | *Start this business?*: cost, cash after | | **Screenshot**. Editor (`s18-check-card-business.png`): 1,34,136, cash after 72,454; *Not now* left cash and no business |
| 6 | US-235 | Tap your cattle yard → **Build the shed** | *Build a cattle shed?*: cost, cash after; the green button builds | | Editor (`s18-check-card-shed.png`): 42,248; cash fell by 42,247.58 |
| 7 | US-235 | People → **Build a hut** (when you have room); Town → a family house | A card with the cost and cash after | | Editor: the house card in US-235's second pass; the hut was greyed here (2 of 2 huts), `ConfirmTests` cover it |
| 8 | US-235 | Menu → **Your land** → *Yours* | Each parcel with what's on it; **Sell** beside it; *For sale* is the other tab | | **Screenshot**. Editor (`s18-235-your-land.png`) |
| 9 | US-235 | **Sell** on a bought acre: tap, then hold | A short tap sends nothing; holding fills the red button; at 1 s: *Sold*, cash up by the card's amount | | Editor (US-235's tour): a short hold sent nothing, a full hold sold |
| 10 | US-235 | Tap one of your fields | The field sheet has no **Sell** | | Editor |
| 11 | US-137, AC-1–3 | Money and loans → *Last season* → **See the report** (or after a close) | *Profit and loss | Cash flow | Balance sheet* tabs; each line with *Last Spring: …* beside it; the advisor's line on top | | **Screenshot**. Editor (`s18-check-season-pl.png`) |
| 12 | US-137 | **Cash flow** tab; scroll to the end | Rows by who the money moved with; *Cash moved by*; *Cash at the end: Adds up to the coin*; no *Other* row | | **Screenshot**. Editor (`s18-check-cash-flow.png`) |
| 13 | US-137 | A row's **?** | The entries behind it, adding to the row | | Editor (`s18-137-cash-why.png`) |
| 14 | US-137 | **Balance sheet** tab | What you own and owe, each line beside last year; *Savings deposit* a line | | Editor (`s18-check-balance-sheet.png`) |
| 15 | US-137 | **Profit and loss** → scroll to *Your people* | Each worker: stars, *Paid … · asks …*; *Your name as an employer, n out of 100, up/down* | | **Screenshot**. Editor (`s18-check-your-people.png`) |
| 16 | US-137 | Money and loans → *Your wealth* → **Your books** | The last 8 closed seasons: profit, cash, net worth; each **Open** shows its page; no "this season so far" | | **Screenshot**. Editor (`s18-check-your-books.png`) |
| 17 | US-137 | **+1 season** until a Winter closes | After the report: *Your year* (profit against last year, best and worst season, the biggest cost line, the accountant's line) | | Editor (US-137's tour `s18-137-your-year.png`) |
| 18 | US-135, AC-1–4 | Money and loans → **Your wealth** | At a glance (cash, net worth, what you owe), the buttons *Borrow* / *Savings deposit* / *Your books*, the trend, the pie, debt to assets, emergency fund, passive income, each with **?**; then your loans and *Milestones* | | **Screenshot**. Editor (`s18-check-wealth-top.png`, `s18-135-*`) |
| 19 | US-135 | **Borrow** | The lenders with their limit and rate; a back arrow returns to Your wealth | | Editor (`s18-135-lenders.png`) |
| 20 | US-236, AC-1 | **Savings deposit** | In the bank; interest and what the next close adds; prices beside it; *Put in* steps, *Take out* steps and *All*; the standing choice *Ask me / Always pay from my savings* | | **Screenshot**. Editor (`s18-check-savings-sheet.png`): put in, take out, the standing choice, each **?** |
| 21 | US-236, AC-2 | On the last day, tap the bell when the close would be short | *Short at the close by … : {what would stop}. It takes … ; you have …*, with **Pay from my savings** (naming how much of your deposit), **Sell from the store**, **People**, **Borrow** | | **Screenshot**. Editor (`s18-check-alert-short.png`, `s18-check-alert-levers.png`): a really short town; People, Store and Borrow each opened with a back arrow |
| 22 | US-236, AC-2 | **Pay from my savings** | *Your savings will cover … at the close. You said yes.* and **Take it back**; nothing leaves the deposit today | | **Screenshot**. Editor (`s18-check-alert-yes.png`) |
| 23 | US-236, AC-3 | Play on through the close (**+1 day**) | Cash isn't taken from the deposit beyond what the close needs (it took less, or nothing, if you earned in the day); the deposit shows its interest; the season page's cash flow names *From your savings, at your yes* and *Interest on savings*; **Always** with a thin deposit shows *Your savings paid the close* on the away card | | Editor: yes survived; the deposit grew by its interest (30,000 → 30,450) and the cover wasn't needed after the day's sales. US-236's pass: cash 0 to the coin after a covered close |
| 24 | US-136, AC-7 | Your wealth → scroll to **Milestones** | *Milestones · n of 5*: reached ones with their season and **?**, the rest dimmed with where you stand | | **Screenshot**. Editor (`s18-136-milestones-*.png`) |
| 25 | US-136 | Play on a season close | A toast above the bottom buttons ("First lakh saved, First rent") for 6 seconds; it waits for letters and the Codex toast; a tap opens Your wealth | | Editor (`s18-136-milestone-toast.png`): fed by script after it timed out; the real one came after the Dadi letter and the Codex toast |
| 26 | Regression | Settings → Text size **Large** and back to **Normal** | Your wealth, the Savings sheet, the alert and the cards wrap and stay readable | | Editor (`s18-check-large-wealth.png`, `s18-check-large-milestones.png`); set back to Normal |
| 27 | Regression | Settings → **Reduced motion** on | The toast just appears and goes; no slide | | Check on the phone |
| 28 | Frame time | Pinch out so the whole village and farm show. Developer panel → **Measure frame time (20 s)**; don't touch the screen | Main thread p95 at or under 8.3 ms, GPU p95 at or under 11 ms (ADR 0094), about 60 fps, under 1% dropped. Write both p95s in Notes | | |
| 29 | Notifications | Close the app on the last day of a season with the close short and no yes | A notification a day before: "Your close is short" (no amount in it), never at night | | Unit-tested only (`NotificationTests`); the Editor only logs it |
| 30 | Regression | Play your normal morning on the phone: Collect all, sow, harvest, sell to the trader, hire | As in Sprint 17 | | Editor: Collect all · 0 |
| 31 | Restore | **Restore** | Your town is back as it was at the Snapshot | | |
| 32 | Regression | The away card after a long absence (**Away cap**) | Lines as before; *Interest on savings* when you have a deposit | | Editor (US-236's pass): *Interest on savings 1,137* |
| 33 | Regression | Rotate or lock the phone | Landscape only (ADR 0005) | | |
| 34 | ADR 0103 | Store → **Godown in town** | *How it works* first: goods come here by themselves, nothing to press; then *What goes here* lists wheat, rice, lentils, atta, maize, mustard (even at "None yet") and says potatoes, vegetables, fruit and milk stay in the hut | | Editor (`s18-godown-how.png`, `s18-godown-kept.png`) |
| 35 | ADR 0103 | Rent 1 t, then **Collect all** on a ripe field | The rice, wheat or dal is "x kg here" on the godown sheet and "x kg of it is in the godown in town" under its row in the Store | | Editor (`s18-godown-store-row.png`) |
| 36 | ADR 0103 | With space rented and nothing in it: **Stop renting** | One tap, no card. *Ends · nothing to pay*, **Keep renting n t** appears; Keep brings *Renews* back | | Editor (`s18-godown-stop.png`) |
| 37 | ADR 0103 | With grain in the rented space | **Stop renting** is greyed: *Your goods stored there need that much space. Sell or use some first.* After a stop and a harvest the status says *Renews anyway* in amber with the rent the close will take | | Editor (`s18-godown-forced.png`) |
| 38 | ADR 0104 | Store → **Mandi trip**: load something, **Go to the mandi**, then open the Store again | Under *Supermarket pays you* a line *Mandi trip pays you · about N coins at hh:mm* ("tomorrow" if it's back after midnight); its **?** lists the cart's loads. With no cart away there is no such line; once the cart is back it goes and the cash is in | | Editor (`s18-store-mandi-trip.png`, `s18-store-mandi-trip-why.png`, `s18-store-mandi-trip-back.png`) |
| 39 | ADR 0104 | Menu → **Your land** → **For sale** | Only land that is for sale: the village's land and neighbours who aren't selling are not listed. A parcel that isn't next to your land says *Not next to your land yet* | | Editor (`s18-lands-for-sale.png`) |
| 40 | ADR 0105 | Store, while the truck is here (6:00 to midnight) | Under *Trader's truck*: *The trader still takes today*, a row for each crop sown this season (not the others) with "N kg more" or "Full for today"; at night the rows are gone. Sell some to the trader: that crop's number drops | | Editor (`s18-truck-room.png`) |
| 41 | ADR 0105 | A ready field with the store nearly full: open the field | **Harvest** says "40 kg of 1,056 kg" (what fits of the whole); *Store what fits, sell the rest* beside it | | Editor (`s18-harvest-part-before.png`) |
| 42 | ADR 0105 | Tap **Harvest** | The store goes to 100%; the field stays ready with *Already in your store · 40 kg* and its yield smaller; **Harvest** greyed "The store is full." Sell something, then Harvest again: the rest comes in and the field is free | | Editor (`s18-harvest-part-after.png`) |
| 43 | ADR 0105 | **Collect all** with a nearly full store | Each ready field gives what fits; what's left stays ready; the card offers to sell the rest to the trader | | Check on the phone |

## Report

- **Blockers:** anything that stops play, or anything wrong with your town after the update. Mark it **BLOCKER** and say what you did.
- **Screenshots:** for checks 1, 2, 3, 5, 11, 12, 15, 16, 18, 20, 21, 22, 24, 25, 34, 36, 38, 39, 40 and 42.
- **Numbers:** both frame-time p95s from check 28.
- **Answers:** to the five questions above.
