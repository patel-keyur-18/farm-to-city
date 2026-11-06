# Sprint 16 build: the farm's capital (water, machines, orchards, the godown) and the village's shops

- **Build:** the head of branch `sprint/16-shops-and-farm`, a Development build made at `923f0b7` (Unity: Build Succeeded; the unsigned Xcode Release compile: no errors)
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. This is **a Development build**, so it has the developer panel. Delete `unity/FarmToCity/Builds/iOS` before building (this avoids the macOS 27 crash).
- **Run in the Editor first (2026-11-06, scripted touch):** checks 2–43 passed in the Unity Editor on the test town, except where Notes say otherwise. Captures are in `docs/art/s16-check-*.png` and `docs/art/s16-balance-*.png`. Set up by script: test coins, **Next era**, and dev time travel. The device-only checks are 1 (your own town's update), 44 (frame time), 45 (your normal morning) and 46 (the developer panel's Snapshot and Restore).

## Before you install: keep your town safe

1. In the **old** app, go to Menu → Settings → **Export**. A copy of your town goes to the Files app (On My iPhone → Farm to City). Keep it until this build is checked.
2. **Install over the old app. Don't delete the app first**: deleting it deletes your town.
3. This build upgrades your town as it loads (save schema 13 to 14):
   - Your fields, cash, people, houses, animals, loans and businesses stay as they were.
   - Your fields start rain-fed (the well acres keep their buckets), with hand tools, no orchards, no godown and no shops.
   - The first time it loads, the app also saves your town as it was before the update in the Files app, as `FarmToCity-before-update-schema13.ftc`.

If anything looks wrong with your town after the update, stop and report it before playing on. Either copy can bring it back.

## What this build changes

- **Grain storage in the mandi town (US-130, ADR 0087).** Store → **Godown in town**:
  - Buy your own godown (45,000, 10 t), or rent warehouse space by the tonne (0.30 a kg a season).
  - Grain, dal, atta and seed keep there at a fifth of the hut's spoilage. Potatoes need a cold store, so they stay in the hut.
  - An empty godown can be let to a grain trader for a season (passive income).
  - Its *Why?* says what gains by waiting: rice and maize from the Monsoon, mustard from Winter. Wheat is dearest at its Winter harvest, so sell it then (ADR 0093).
- **The irrigation ladder (US-127, ADR 0086, 0087).** A field's card has an **Irrigation** row, with picture buttons for the next step:
  - **Pump on the well** (Hamlet): it waters 4 fields you choose at moisture 40, with no buckets, for power each season.
  - **Dig a canal channel** (Hamlet): on a field beside the minor canal, or beside a channelled field. It keeps the field at 45, except in Summer when the canal is shut, for a fee each season. It's a small, steady gain: it mostly saves dry Springs (ADR 0093).
  - **Lay drip** (Village), on a pumped field. It holds the crop at its best moisture and saves half an hour of Care a day. Fertiliser through it gives +15%. Half its price is paid by the government on your first 2 acres.
  - **Drill a borewell** (Village): it waters 4 more fields you choose.
- **A second cattle shed (US-234, ADR 0088).** Build it on any empty field you own, once the first shed stands: 8 animals over both sheds.
- **Machines (US-128, ADR 0088, 0089).** Menu → **Equipment**:
  - The **ox plough** with a pair of bullocks (Hamlet; the bullocks live in the second shed).
  - The **tractor** (Village): buy it (on a bank loan if you like), lease it by the season, or hire it for a day.
  - A field's card shows **Worked by** with a *Why?* (labour saved, the Tools factor). Machines are drawn at work on your fields by day, and the tractor parks on the lane at night.
- **Orchards (US-129, ADR 0090).** A field's card → **Plant an orchard** (Hamlet): mango, guava, banana or papaya, at real densities.
  - Young trees can have a short crop between the rows, at half its yield.
  - When the fruit is ripe, **Pick what can go today** (what the store and the trader take), or **Sell it all on the trees** to a fruit contractor at 70% of the trader's price.
- **Shops (US-120, US-121, ADR 0091, 0092, 0093), from the Village.** Menu → **Businesses** → a vegetable stall, a tea stall or a sweet shop:
  - Put it on a rented pitch round the banyan in the square (about half the village's customers), or on your own acre (no rent, but few pass by: the place sheet shows about 5–10%).
  - Customers choose between your shop and the village's grocer, tea stall or the town's halwai by price, quality, stars, distance and waiting.
  - The card's **Customers** tab shows each shop's share, each with a *Why?*.
  - **Stars** move a tenth of the way to each day's score at midnight. One day shut is forgiven.
  - **Settings:** the price (four presets), the quality (good costs more), your goods a day, and buying at the market.
  - The tea stall takes your herd's milk first. The vegetable stall sells your produce first and buys what your store lacks at the local market.
- **Balance (ADR 0093):**

  | What | Value now |
  | --- | --- |
  | A cup of chai | 12 coins |
  | Rasgulla, a kg | 300 |
  | Atta laddoo, a kg | 360 |
  | The vegetable stall's usual price | the market's + 15% |
  | Drip | no power of its own; fertilising through it +15% |

**Your questions:**
1. Is it clear which irrigation step to take next, and what each one costs and gains?
2. Is buy, lease or hire clear for the tractor?
3. Does an orchard's card make the long wait and the payback clear?
4. Is the pitch-or-acre choice clear before you build a shop?
5. Do the shops round the banyan, the customers and the stall-holders look right on the phone?
6. Do the stars and their *Why?* tell you how to earn more customers?

## Checks

**The developer panel:** hold the time card for 3 seconds. It has **+1 hour**, **+1 day**, **+1 season**, **Away cap (72 h)**, **Next era**, and **Snapshot** / **Restore**.
- Take a **Snapshot** after check 1. **Restore** at check 46 puts your town back.
- Checks 7–9 and 24–25 need the Hamlet; checks 14–16, 19–23 and 31–43 need the Village. If your town is earlier, **Next era** moves it on.
- Several checks spend money. If cash runs short, play a day on (**+1 day**) or skip that check and say so.

| # | Story / AC | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | Migration (spec 013 AC-9) | Launch with your town | Same cash, fields, people, houses, animals, loans and businesses. In the Files app: `FarmToCity-before-update-schema13.ftc` | | **Screenshot** |
| 2 | US-130 | Store → **Godown in town** | *Grain storage in the mandi town*: your own godown (Buy · about 45,000: prices follow the index), rented space, what's kept; each with **?** | | **Screenshot** |
| 3 | US-130, ADR 0093 | The godown's **?** | 10,000 kg; grain spoils at 20% of the hut's rate; the goods it keeps; *What gains by waiting* (rice, maize, mustard; wheat sold at harvest) | | |
| 4 | US-130, AC-11 | **Rent 1 t more** | Paid now for the days left in this season; *Warehouse space 1 t* | | |
| 5 | US-130, AC-7 | **Buy** | The godown is yours; the hut store's grain moves to it (the Store's bar drops) | | |
| 6 | US-130, AC-12 | **Let it out**, then **+1 season** | At the close a trader takes it (most seasons): the away card or the sheet shows the rent. If your grain needs the room, it isn't let and the row says why | | |
| 7 | US-127, AC-1 | A well field's card → **Pump on the well** | The *Irrigation* row says *The pump*; the pump is drawn on the well. Another field's card: **Water from the pump** (up to 4 fields) | | **Screenshot** |
| 8 | US-127 | A field beside the canal (the farm's row next to the canal line) → **Dig a canal channel** | The channel and its gate drawn at the field's edge; *Irrigation: Canal channel* | | |
| 9 | US-127, ADR 0093 | That field's *Irrigation* **?** | The canal keeps it at 45; dry in Summer; "the rain mostly keeps this field wet enough… a small, steady gain for its fee"; the fee each season | | **Screenshot** |
| 10 | US-127 | A field neither pumped nor channelled | *Irrigation: Rain-fed*; the buttons say what each step needs, for example "Only beside the canal, or beside a field of yours with a channel" | | |
| 11 | US-234 | With the first shed built: the cattle acre → *A second shed* → **Choose the field** → a lit empty field → **Build here** | The lit fields you own; the shed's ghost; after a day the second shed on that field, which isn't sown or sold while it stands | | **Screenshot** |
| 12 | US-234 | The cattle acre after it's built | *Your cattle sheds · 0 of 8 stalls* (or how many you have); the feed room and straw stack add up | | |
| 13 | US-128 | Menu → **Equipment** | The ox plough and the tractor, each with what it does; Buy, Lease, Hire a day as picture buttons; refusals in words (for example "Comes once you're a Village") | | **Screenshot** |
| 14 | US-128, AC-3 | **Hire a day** on the tractor (Village) | "Hired today · Working your fields until midnight"; a growing field's card: *Worked by · Tractor* and its **?** (labour saved, Tools) | | |
| 15 | US-128 | **+1 hour** to 10:00, then look at your fields | The tractor at work on a growing field; at 22:00, parked with its trolley on the lane | | **Screenshot** |
| 16 | US-128, AC-5 | **Buy with a loan** on the tractor, then the machine's **?** | The bank lends against the tractor; price, fields, work saved, diesel, upkeep, breakdowns, life. Then **Sell**: the bank is repaid first | | |
| 17 | US-128 | Ox plough → **Buy** (needs the second shed with 2 free places) | The plough and its pair (*Working … fields*); the bullocks in the second shed; the plough team at work by day. With a tractor too, the tractor takes the fields first | | |
| 18 | US-128, AC-4 | The tractor's *Maintenance at each close* → **Skip**, then **Pay it** (the plough has no maintenance) | "Skipped: it breaks down more often"; the tractor's **?** shows 0.5% a day maintained, 4% skipped | | |
| 19 | US-127, AC-2 | A pumped field (Village) → **Lay drip** | "Half paid by the government"; drip lines drawn; *Irrigation: Drip from the pump* | | |
| 20 | US-127, ADR 0093 | That field's *Irrigation* **?** | Drip holds the crop at its lowest good moisture; Care saved 0.5 h; fertiliser through the drip +15%; upkeep 1% of its cost (no power of its own); pays back in about 3 years | | **Screenshot** |
| 21 | US-127, AC-10 | Any field you own → **Drill a borewell here** (Village) | The borewell room on the field; **Water from the borewell** on up to 4 fields not on the pump's list | | |
| 22 | US-127 | A drip field's card | **Off the pump** greyed: "Drip needs pumped water: this acre has to stay on." | | |
| 23 | US-127 | **+1 season** to Summer, a channelled field's card | *Canal channel (dry this season)*; the canal and channels drawn dry | | |
| 24 | US-129 | An empty field → **Plant an orchard** | Four trees, each with trees an acre, when it fruits, when it bears, price and **?** | | **Screenshot** |
| 25 | US-129, AC-13 | Plant **banana**; the card → **Sow between the rows** | Short crops only (methi, radish…), at half the harvest and the hours | | |
| 26 | US-129, AC-6 | **+1 day** until the trees bear (banana about 7 days) | A ripe marker on the acre; the card: ripe fruit, *You can pick today* and its **?** (store + trader) | | **Screenshot** |
| 27 | US-129 | **Pick what can go today**, then **Sell the rest on the trees** | Picked up to what the store and trader take; the rest sold to the contractor at 70% | | |
| 28 | US-129 | **Clear the orchard** | Asked first ("Keep it" / clear); cleared, the field is a field again | | |
| 29 | Map | Pinch out over the farm and the village | Canal, channels, drip, pump, borewell, sheds, machines, orchard rows and the shops all drawn where they should be; nothing overlapping a road or a house | | **Screenshot** |
| 30 | US-120 | **Snapshot** if you haven't. **Next era** until "Era: village", then Menu → **Businesses** | Six picture buttons, three a row: mill, bakery, dairy, vegetable stall, tea stall, sweet shop ("On a pitch, from …") | | |
| 31 | US-120, ADR 0093 | **Start a tea stall** → research | The village's people, cups a day, the usual price (12 coins at year-0 prices, so 13 or so now), the village tea stall's terms, your share on a pitch, the pitch's rent, the wage; each with **?** | | **Screenshot** |
| 32 | US-120 | **Make a plan** → scroll | Cups, chai's milk (your herd's, then neighbours'), snacks, rent, wage, profit, payback; each with **?** | | |
| 33 | US-120, ADR 0093 | **Choose the place** | Three pitches, each "About …% of the village's customers" with its rent; *Any acre you own*: "No rent; fewer pass by" and "Your nearest: about …%" (much lower) | | **Screenshot** |
| 34 | US-120 | **Build here** on Pitch I, then **+1 hour** past 8:00 | The tea stall at the banyan; a tea maker sent for (or **Run it yourself**); customers at the counter while it's open | | **Screenshot** |
| 35 | US-120, AC-1 | The card → **Customers** | Your share and the village tea stall's, lost customers; each share's **?** names price, quality, stars, distance and wait | | **Screenshot** |
| 36 | US-120, AC-2 | **Settings** → *Price* → **Change** until it says *10% above the usual price* (each tap moves to the next preset); **+1 hour** | The share falls (Customers); the price **?** says why | | |
| 37 | US-121, AC-3 | **Settings** → *Quality* → **Change** to Good | The note "Good: full-cream milk"; *Milk a day* goes up by a third; the **?** says 80 ml a cup against 60 | | |
| 38 | US-121 | **+1 day**, the card's **Today** → *Stars* and its **?** | Stars moved from 2.5 a tenth of the way to the day's score; the **?** shows served, quality and value parts | | **Screenshot** |
| 39 | US-120, ADR 0093 | **Start a vegetable stall** → research → plan | The usual price "the market's price + 15%"; the produce line "yours at the trader's price, the rest at the market" | | |
| 40 | US-120, ADR 0093 | Build it on Pitch II → the card → **Settings** | *Buy at the market · Yes*; its **?** explains buying at the local market each morning | | |
| 41 | US-120 | Hire for it or **Run it yourself**, then **+1 day**, the vegetable stall's **Today** | Your produce and goods bought at the market (mango, pav, paneer, curd…) on the stall, "… left of … a day" going down through the day. With nobody working it, it stays *Closed · nobody works here* | | |
| 42 | US-120 | **Start a sweet shop** → research and plan | Rasgulla and atta laddoo at 300 and 360 a kg at year-0 prices (a little more now); the town's halwai 1,500 m away; the plan's paneer, ghee, atta and sugar | | |
| 43 | US-120 | Tap the square (the banyan) | *The square's pitches*: your shops and free pitches; **Open a shop** | | |
| 44 | Frame time | Pinch out so the whole village and farm show, with shops open. Developer panel → **Measure frame time (20 s)**; don't touch the screen | Main thread p95 at or under 8.3 ms, about 60 fps, under 1% dropped. Write the numbers in Notes | | |
| 45 | Regression | Play your normal morning: orders, sowing, harvest, selling, the shed, your people, the mill | Everything works as in Sprint 15b | | |
| 46 | Restore | **Restore** | Your town is back as it was at the Snapshot | | |

## Report

- **Blockers:** anything that stops play, or anything wrong with your town after the update. Mark it **BLOCKER** and say what you did.
- **Screenshots:** for checks 1, 2, 7, 9, 11, 13, 15, 20, 24, 26, 29, 31, 33, 34, 35 and 38.
- **Numbers:** the frame-time results from check 44.
- **Answers:** to the six questions above.
