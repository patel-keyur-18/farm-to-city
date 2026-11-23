# 0162. US-245 the tempo as built: a vehicle of legs, rescaled to a four-day season, idle legs let out at midnight

- Status: Accepted (the owner closed gaps 18 to 20 before code, 2026-11-23; the story is for the owner's review)
- Date: 2026-11-23
- Deciders: Keyur Patel
- Related ADRs: 0075, 0088, 0137, 0153, 0155, 0161

## Context

US-245 (spec 028 §3.6, Preview 18): a tempo from the Township that carries goods to the mandi for less than the hired trolley and can be let out to neighbours. Spec §3.6 set the numbers for a 90-day season (price 3,50,000, lease 18,000, "+36,000 a season with neighbours' trips, payback about 10 seasons"), but a season here is **4 real days** and the mandi allows one trip a day: a tempo makes at most 8 legs a season, so the spec's numbers could never pay back (about 8,000 a season at best against an 18,000 lease). Three gaps were closed with the owner before code.

## Decisions (as built)

1. **Gap 18, rescale the price and lease, keep the trip numbers.** Per-trip numbers stay as approved: a leg carries 1,000 kg, takes 4 hours, costs 300 (diesel 200, wear 100); neighbours pay 600 a leg; the hired trolley is 1,000 a trip. The machine is rescaled to what 8 legs a season earn: **price 40,000, lease 2,000 a season, upkeep 1.5%, a 48-season life, a bank loan allowed**. A leg you use saves 700 against the trolley, so a season with every leg yours leaves about 8 × 700 = 5,600 less about 1,400 to stand there; payback about 10 seasons, and *Does it pay?* says so in your numbers. Let out alone it leaves less than it costs (2,400 − 1,400), and the sheet says that too.
2. **The tempo is a machine** (`content/farming.json`, `tempo`, era Township) with a `trip` block (`TripDef`); it works no field (acres 0, no labour cut), is never hired for a day (`HireMachine` rejects it), is bought, leased (never let out), sold and repaired like the tractor through the same commands (`BuyMachine`, `LeaseMachine`, `SellMachine`, `SetHireOut`: no `BuyTempo` or `LeaseTempo` of its own), one of a kind, and breaks by the machine rules (the let-out doubles its chance as the tractor's). `HiredUntil` keeps only the first two kinds in the save and hash (`HiredKinds`), so adding a machine changed no earlier hash.
3. **Gap 19, a send is one or two legs.** One *Go to the mandi* is still one trip: up to 1,000 kg is one leg (4 h, 300), 1,001 to 2,000 kg two legs back to back (8 h, 600, one sale at the end; your time is half an hour a leg). The tempo has two legs a day; a send that uses the legs of the day may be followed by another the same day only while legs are left (otherwise *already went today* as before).
4. **The cheapest ride carries a load**, chosen by `TempoRules.Choose` for the command and the sheet alike: your own cart or tractor-trolley (free), then the tempo (a broken tempo, one with no leg left or a load over two legs falls through), then the hired trolley. The mandi sheet says *Carried by* and the cost of the ride; without a tempo the sheet is as it was.
5. **Gap 20, neighbours get the idle legs.** *Let its idle legs out* (`SetHireOut`, the tractor's switch; needs a hired hand to drive and a tempo you bought): at **midnight** the legs you did not use that day (0, 1 or 2) carry neighbours' goods, 600 each, diesel and wear yours (`ledger.tempoLetOut`: income on the machine-hire account, so farm income, never passive); the next daily pass doubles its chance of breaking. Your own sends are never refused because of it. *This turns round the spec's "neighbours' trips come before yours": with two legs a day it would have left you none.* No cash for the diesel: not driven.
6. **Save schema 23** (hashed only when used): a tempo's legs today and this season (yours and neighbours'), the day it last ran for neighbours, and a trip's legs; one machine-kind difference in the save is padded on load. Fixtures regenerated; the growth fixture holds a let-out tempo that carried two legs.
7. **Screens** (Preview 18): the Equipment sheet's Tempo block (Buy, Lease, loan; no hire a day), its legs today, *Let its idle legs out* with the legs this season, *Does it pay?*; the mandi sheet's *Carried by*; the away card's *Your tempo* row; every number has a *Why?* (`tempo`). The tempo is a blue three-wheeler drawn in Blender, parked on the farm lane beside the tractor and away while a trip is on.

## Consequences

- Balance pass: a leg you use saves 700; the let-out nets 300 a leg, which with the standing cost (price over its life plus upkeep) never pays on its own: the let-out is a way to earn from idle legs, not a business.
- US-246's restaurant and the balance pass's mandi checks read `TempoRules` for the trolley comparison.
