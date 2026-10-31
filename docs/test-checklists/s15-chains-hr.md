# Sprint 15 build: the bakery and the dairy, the hiring board, output and training, HR events

- **Build:** the head of branch `sprint/15-chains-and-hr` (the commit is in the pull request)
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** Keyur, 2026-10-31
- **Result:** the owner passed every check except 14–25. Those need the Village and wait until the owner reaches it by normal play, which becomes possible with the Village gate in Sprint 17 (US-102).
- **How to install:** see `how-to-build-ios.md`. This is **a Development build**, so it has the developer panel. Delete `unity/FarmToCity/Builds/iOS` before building (this avoids the macOS 27 crash).

## Before you install: keep your town safe

1. In the **old** app, go to Menu → Settings → **Export**. A copy of your town goes to the Files app (On My iPhone → Farm to City). Keep it until this build is checked.
2. **Install over the old app. Don't delete the app first**: deleting it deletes your town.
3. This build upgrades your town as it loads (save schema 10 or 11 to 12):
   - Your fields, cash, people, houses, orders, loans, trees, animals and flour mill stay as they were.
   - Your hands and your mill's worker start neutral, and your name as an employer starts at 50.
   - Every townsperson gets a crafting skill, and anyone without a farming skill gets one.
   - Money and loans starts a record of what you sell. What you sold this season before the update shows as one line, **Other sales**; from next season every crop has its own line.
   - The first time it loads, the app also saves your town as it was before the update in the Files app, as `FarmToCity-before-update-schema10.ftc` (or `…schema11.ftc` if you installed the 29 October build).

If anything looks wrong with your town after the update, stop and report it before playing on. Either copy can bring it back.

## What this build changes

- **The bakery and the dairy (spec 016, US-118, US-119, US-216; ADR 0079, 0080).** Both come with the Village. Start one from Menu → **Businesses**, the same way as the flour mill.
  - The **bakery** bakes pav and rusk from atta (your mill's, if you have one) and sells to the village shops and the trader.
  - The **dairy** makes paneer, curd and ghee from your herd's milk and from neighbours' milk (on by default).
  - In each one's **Settings** you choose what it makes and how much a day. Its **Today** shows each batch made, with a *Why?*.
- **Next era (US-218):** the developer panel moves your town to the next era, so you can reach the Village's businesses now.
- **One hiring board (spec 015, US-112).** People → **Hiring board** shows a tab for each job: farm hand, and miller, baker or dairyman for each business you have.
  - Townspeople apply with their skill and both traits known. People from outside show a skill range and one trait.
  - **Talk** reveals the second trait. **Ask** (about 300 coins) takes 12 hours: their last employer tells you their skill, and how they left.
  - **Offer** lets you pay the asked wage, 10% less, or 10% more. Each choice shows how likely they are to accept.
- **Output and training (US-113).**
  - Each worker's card shows **Output** in words and stars, with a *Why?*: their skill, how keen they are, and how tired.
  - **Long days** (farm hands) means a 10-hour day, with overtime at 1.5×. Long days tire people out.
  - **Send on a course** costs 6,000 and takes the worker away for 1 day. They come back with +10 skill.
  - There are four new traits: creative, frugal, sociable and perfectionist.
- **HR events (US-114, ADR 0081, 0082).** Each event is a card with two choices. If you're away, it waits in the bell until you answer.
  - **A raise request.** Refuse it, and the worker works less keenly and leaves at the season's end.
  - **An offer from the next village** (skill 60+). Match it, or let them go.
  - **Sick leave** (a day off). You choose whether those days are paid.
  - **A family emergency.** They ask for an advance on their wage.
  - **A bad batch** at a business. Throw it away, or sell it cheap.
  - A contract now renews at the wage the worker is already on.
- **The weather on the map (US-222, ADR 0083).** Each weather is drawn over the whole farm, as you approved in the preview: sun, cloudy (drifting cloud shadows), rain, heavy rain (splashes, mist), storm (wind-driven rain, trees leaning in gusts, a soft glow of lightning now and then), heatwave (warm haze) and frost (pale rime). A change of weather fades over 3 seconds. With **Sound** on: a rain loop and distant thunder. With **Reduced motion** on: the rain stands still and there's no glow. The developer panel's **Weather look ▸** shows any weather for checking (look only: the sim's weather doesn't change).
- **Your townspeople as hands (US-219, ADR 0083).** A townsperson aged 18–59 who works outside can be offered work as a hand from their own card. Their family house becomes part of their pay from their first renewal (never unpaid work for housing).
- **Your labourers on the map (US-220).** Townspeople you hire as day labourers walk from their house to your fields at 7:00 and home at 19:00, drawn as themselves. The People sheet names each one. Everyone on the map now keeps the same working day, 7:00–19:00.
- **Money and loans opens on *Your season* (US-221, spec 019 amended).** What you earned (each crop by name, and where it sold), what you spent (each hand by name), your profit, why your cash moved differently from your profit, and what's already due next season. *Last season* shows the season before. Your balance sheet and land are on the *Your wealth* tab.
- **Your name as an employer:** the People sheet shows it, for example "Fair (50)", with a *Why?*. Fair pay raises it, and people then accept your offers more readily.

**Your questions:**
1. Is the hiring board clear: who to pick, and what each offer means?
2. Does the Output *Why?* explain why a worker does well or badly?
3. Are the HR cards clear about what each choice costs?
4. Do the bakery and the dairy look right on their acres?
5. Does the weather look like the preview, and is the rain and thunder mix right on the phone's speaker?
6. Does *Your season* show where every coin went, and is the line *Why your cash moved differently* clear?

## Checks

**The developer panel:** hold the time card for 3 seconds. It has **+1 hour**, **+1 day**, **+1 season**, **Away cap (72 h)**, **Next era**, and **Snapshot** / **Restore**.
- Take a **Snapshot** before check 14. **Restore** puts your town back exactly as it was then.
- **Times after time travel:** "till …" and "until …" times are your phone's clock, while the time card shows the town's. After **+1 hour** they differ by the hours you added (a developer-panel effect only; without time travel they agree).
- Checks 14–25 need the Village. If your town is earlier, **Next era** moves it on: tap it until the panel says "Era: village".

| # | Story / AC | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | Migration (spec 015 AC-10) | Launch with your town | Same cash, fields, people, houses, orders, animals and mill as before. In the Files app: `FarmToCity-before-update-schema10.ftc` | Pass | **Screenshot** |
| 2 | US-114 | People | The row *Your name as an employer*: "Fair (50)". Its **?** lists what moves it | Pass | **Screenshot** |
| 3 | US-112 | People → **Hiring board** | The hiring board, with a tab for each job you have: *Farm hand*, plus *Miller* if you have the mill. Each tab shows the usual wage | Pass | **Screenshot** |
| 4 | US-112 | *Farm hand* → **Send word** | If any townspeople want work, they're listed at once: skill, both traits and "From your town". People from outside come over 4 days | Pass | |
| 5 | US-112 | An applicant from outside → **Talk** | Their second trait shows | Pass | |
| 6 | US-112 | *Ask about …* → **Ask** (about 300 coins) | "Asking their last employer · back in 12 h"; after **+1 day**: their skill and "Left on good terms" or "Was let go" | Pass | |
| 7 | US-112 | **Offer** | Three offers: what they ask, 10% less, 10% more. Each shows how likely they are to accept, and the cost now | Pass | **Screenshot** |
| 8 | US-113 | A hand's card | *Output*: words and stars. Its **?**: skill, keenness, tiredness | Pass | **Screenshot** |
| 9 | US-113 | **Long days** → **Start** | "10 h a day · … overtime a day". **+1 day**: the Output **?** shows them tired. Then **Stop** | Pass | |
| 10 | US-113 | **Send on a course** | "6,000 coins · +10 skill · 1 day away". The card says "Away on a course · back in 1 d" | Pass | |
| 11 | US-113 | **+1 day** | They're back, with 10 more skill on their card | Pass | |
| 12 | US-114 | Hire a hand who isn't *Loyal* (a loyal worker never asks) at **10% less**, then **+1 season** (twice if no card comes) | A card: "… asks for a raise", with what it costs now and from whose cash | Pass | **Screenshot** |
| 13 | US-114, ADR 0082 | **Not now**, then open the hand's card | The *Contract* row: "Leaving at the season's end". Output **?**: −10% for low pay and −10% for the refused raise. **+1 season**: they've gone | Pass | **Screenshot** |
| 14 | US-218 | **Snapshot**. Then **Next era** until "Era: village" | The Village era. Menu → **Businesses** lists *Start a bakery* and *Start a dairy* | Deferred | |
| 15 | US-119 | **Start a bakery** → research → **Make a plan** | The shops' pav and the trader's rusk, the atta's price, a baker's wage, the cost; each with **?** | Deferred | **Screenshot** |
| 16 | US-119 | **Choose the acre** → a lit acre → **Build here**, then **+1 day** | The bakery on its acre; its card says "Closed · nobody works here" until you hire, with **Hire** under *Who works here* | Deferred | **Screenshot** |
| 17 | US-112 | The bakery's card → **Hire** → **Send word**, then **+1 day** and **Offer** | The board on the *Baker* tab. Once hired, the card says *Open now* in its hours | Deferred | |
| 18 | US-118, US-119 | **+1 day**, then the bakery's **Today** in the afternoon | The day's pav and rusk made from atta, each with **?** (the recipe). The shops and the trader collect them at 6:00 next morning: **Books** then shows them sold | Deferred | **Screenshot** |
| 19 | US-119 | **Settings** → *Pav a day* → **Change** | Half as much pav a day; the card stays open | Deferred | |
| 20 | US-216 | **Start a dairy** → plan → acre → **Build here**, then **+1 day** | The dairy on its acre; the plan and the card show *Buy neighbours' milk* (on) | Deferred | **Screenshot** |
| 21 | US-216 | The dairy's **Today**, after a day with a dairyman | Paneer, curd and ghee batches from milk; your herd's milk used first | Deferred | |
| 22 | US-114 | Play on with **+1 day** / **+1 season**. If an HR card comes (sick, emergency, offer, bad batch) | The card says what happened, what *yes* costs now and from whose cash. Answer it: the bell's row goes | Deferred | **Screenshot** if one comes |
| 23 | US-114 | A card you didn't answer: tap the bell | The event's row; **Answer** opens a sheet with the same two choices | Deferred | |
| 24 | US-116 | The bakery's **Books** | Profit and loss, cash and what it owns and owes, each line with **?** | Deferred | |
| 25 | Restore | **Restore** | Your town is back as it was at the Snapshot | Deferred | |
| 26 | Frame time | Pinch out so the whole map shows. Developer panel → **Measure frame time (20 s)**; don't touch the screen | Main thread p95 at or under 8.3 ms, about 60 fps, under 1% dropped. Write the numbers in Notes | Pass | Passed per the owner; numbers not reported |
| 27 | US-222 | Developer panel → **Weather look ▸**, then **Close**. Repeat for each weather (the line under the status names it) | Sun, cloudy, rain, heavy rain, heatwave, frost, storm drawn over the whole map, each fading in over about 3 s. Fields, crops and people readable in every one; the top and bottom bars unchanged | Pass | **Screenshot** of storm and frost |
| 28 | US-222, spec 008 AC-21 | Storm look **at night** (after 21:00; **+1 hour** if needed). Pinch out so the whole map shows. **Measure frame time (20 s)**. Then the same in heavy rain | Each: main thread p95 at or under 8.3 ms, about 60 fps, under 1% dropped. Write both in Notes | Pass | Passed per the owner; numbers not reported |
| 29 | US-222 | Storm look, **Sound** on, watch for a minute | A rain loop; a soft glow every 15–25 s that rises and fades (never a flash); distant thunder 1–3 s after it; trees leaning in the gusts | Pass | Is the mix right? |
| 30 | US-222 | Settings → **Reduced motion** on, storm look | The rain stands still and lighter; no glow, no gusts; thunder still plays with Sound on. Then turn **Reduced motion** back off | Pass | |
| 31 | US-222 | **Weather look ▸** until it says "the sim's own", then leave the app and come back | The map shows the time card's weather again; nothing about your town changed | Pass | |
| 32 | US-219, spec 015 AC-11 | People → the **Town** tab → a household → someone aged 18–59 whose *Work* says "Works outside, about half the days" → **Offer work as a hand** | The offer sheet, each offer with the line "From next season: … coins in cash + the house (worth … coins)". Hire one: the toast "… works your fields now!"; their hand card's *Housing* row says the house is part of the pay from the next renewal | Pass | **Screenshot** |
| 33 | US-219 | A child's (or an elder's) card, and a second adult from the same house | No offer button for the child or elder (the *Work* line says why, for example "Too young to work"). The second adult's offer reads "The house is already part of …'s pay: all of this is cash" | Pass | |
| 34 | US-220, spec 014 AC-17 | With a crop growing, People → hire labourers. Then **+1 hour** until after 7:00 | Your townspeople walk from their houses to the fields, drawn as themselves (not also at home). People names each one, with their house and till when | Pass | **Screenshot** |
| 35 | US-220 | **+1 hour** until after 19:00 | Everyone walks home: your hands, the founder, your labourers to their houses, neighbours' labourers out by the west edge | Pass | |
| 36 | US-221, spec 019 AC-11 | Tap the cash on the top bar | Money and loans opens on *Your season* → *This season*: Earned, Spent, Profit, *Why your cash moved differently*, *Next season, already due*; every line has **?** | Pass | **Screenshot** |
| 37 | US-221, AC-12 | The *Your cash* line ("… at the start → … now") and its **?** | Cash at the start + profit + each line of *Why your cash moved differently* equals the cash now and the cash on the top bar (within a coin or two: each line is rounded to the coin). Each crop sold has its own line, and its **?** says where it sold (after the update, earlier sales show as *Other sales*) | Pass | |
| 38 | US-221, AC-14, AC-15 | *Next season, already due*, then **+1 season** and the *Last season* switch | Due shows only what's committed (loans, living costs, each hand's renewal). *Last season*'s profit equals the season report's | Pass | **Screenshot** |
| 39 | US-221 | The *Your wealth* tab | Your balance sheet, land, Borrow and Repay as before | Pass | |
| 40 | Regression | Play your normal morning: orders, sowing, harvest, selling, the shed, your people, the mill | Everything works as in Sprint 14 | Pass | |

## Report

- **Blockers:** anything that stops play, or anything wrong with your town after the update. Mark it **BLOCKER** and say what you did.
- **Screenshots:** for checks 1, 2, 3, 7, 8, 12, 13, 15, 16, 18, 20, 27, 32, 34, 36 and 38, and for 22 if a card came.
- **Numbers:** the frame-time results from checks 26 and 28.
- **Answers:** to the six questions above.
