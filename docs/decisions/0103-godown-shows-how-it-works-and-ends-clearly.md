# 0103. The godown says how goods get there, and the rented space can be ended from the next season in one step

- Status: Accepted (owner, 2026-11-12: agreed to both recommendations; "make it more visible")
- Date: 2026-11-12
- Deciders: Keyur Patel
- Amends: spec 013 §3.4 (the godown's sheet); ADR 0087 (the godown in the mandi town, renting and letting); ADR 0101 (confirm cards)
- Related: ADR 0087, 0101 (every amount due, the close's rent), 0102

## Context

The owner played the godown and wrote two things:

1. "I don't see any option to put it in godown." Crops, wheat and the rest, have no button to move them there. They go there by themselves (`GodownRules.Dry`): your own godown first, then rented space, then the hut, fastest-spoiling first. The rule is right for the average player (no step to forget, no per-good choice, nothing to carry), but the sheet never said so. Its only hint was "Nothing here yet: dry goods come here first", shown once the list was empty. Which goods qualify (and that potatoes don't) was also unwritten.
2. "I want a clear option if I want to stop renting from next season." The rented space renews each season, but nothing said so. The only control was "1 t less / From next season", a one-tonne step. Worse, the close rents what the goods in the space need even when 0 t was booked (`CloseTonnes`), so while grain sits there the rent can never really stop, and nothing told the player.

A third finding came from the Editor pass: "1 t less" (and any reduction) opened a confirm card titled "Rent warehouse space? Cost 0 coins", because `RentWarehouse` is a tap-card command whether it spends or not.

## Options

**Putting goods in.**
- (a) Keep it automatic and say so, in words and in the lists (recommended, chosen).
- (b) A per-good *Keep in town / Keep at home* switch with the godown filled in the player's order. A decision for every crop, and a rule for when space runs out. Worse for the average player (a forgotten switch loses grain to the hut's faster spoilage) and no gain in realism, since a real godown takes whatever you send it.

**Ending the rent.**
- (a) A status line in words, a **Stop renting** button, and **Keep renting** to undo it (recommended, chosen).
- (b) Let Stop end the space even with goods in it and send them back to the hut. That changes the sim (the hut may be full, goods could spill), so it is out: the rules and the money stay as they were.

## Decision

1. **The godown sheet opens with *How it works*:** "Goods come here by themselves": wheat, rice, dal, maize, mustard, atta and seed you harvest or buy go into your godown, then your rented space, then the hut; nothing to carry or press; selling or using takes the hut's share first, so the best-kept grain stays in town; they spoil at a fifth of the hut's rate.
2. **A harvest counts.** A harvested crop is store stock the moment it is collected, so with space it is in the godown at once. The sheet's *What goes here* lists all six dry goods always (seed once there is some), each as "{x} here · {y} in the hut", "{x} here", "{y} in the hut" or "None yet"; potatoes, vegetables, fruit and milk are named as staying in the hut (they need a cold store, Phase 3). With no godown and no space it says what to do.
3. **The Store's rows** carry a line "{kg} of it is in the godown in town" for goods kept there.
4. **The rented space gets a status line** from the close's own figures (`GodownRules.NextCloseTonnes` and `NextCloseRent`, the same worked-out numbers the close uses): *Renews · {rent}* (rented again each season until you stop; the next close takes {rent} for {t} t), *Ends · nothing to pay*, or *Renews anyway* (your goods need {t} t, sell or use them before the close and it ends).
5. **Stop renting** is a button of its own (`RentWarehouse(0)`); while goods stand in the rented space it is greyed with the sim's reason, now "Your goods stored there need that much space. Sell or use some first." After a stop the button becomes **Keep renting {t} t** (`RentWarehouse(RentedTonnes)`). *1 t less* shows only above 1 t.
6. **No card for what costs nothing.** `ConfirmCards.For` returns null for a `RentWarehouse` that changes no cash (stopping, cutting, keeping): everyday play stays one tap (the owner's Hay Day rule). Renting more still asks.

No rule, formula, price or state changes; no save change. `NextCloseTonnes` is read-only and `NextCloseRent` is now `NextCloseTonnes` at the season's rent, so the figures the close prints and the ones the sheet shows cannot differ.

## Consequences

- `GodownViewTests` (new): how it works first and every dry good listed, a harvest in town and its Store row, stop and keep, goods keeping the rent going, stopped-then-harvest shown as *Renews anyway*, no confirm card for a stop or a cut, no "0 kg in the hut".
- The `s18` test checklist gains checks 34–37; Editor captures `docs/art/s18-godown-*.png`.
- Spec 013 §3.4 carries an *As built* line; ADR 0087 is amended by this ADR.
- A cold store (potatoes, vegetables, fruit, milk) stays in the Phase 3 plan; the sheet says only that those goods "need a cold store, which isn't in town yet".
