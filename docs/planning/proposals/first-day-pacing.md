# Proposal: a faster, livelier first day

- Status: **Brainstorm, for the owner to choose from.** No code, no spec change, no ADR yet.
- Date: 2026-10-26
- Author: Claude
- Reviewer: Keyur Patel
- Touches (if chosen): spec 009 (onboarding), spec 003 (crops), `content/start.json`, `content/crops.json`, `content/onboarding.json`; ADRs 0015, 0045, 0058

## 1. The problem

The owner's observation: a new player sows, then waits. Some crops take 15 hours, and the start feels slow and boring. Later the game is full (businesses, people, the town), but the first day loses players before they get there.

## 2. Why it feels slow (what the repository says)

What a new player sees today in their first 24 hours (a new game starts in **Spring** at the device's local time, spec 001):

| When | What happens |
| --- | --- |
| 0–20 min | Dadi's letter, sow the garden (radish 10 min, spinach 15, coriander 5), the household sheet, the first pick, the first sale, then sow a **long crop of at least 12 h** (spec 009 step 6, ADR 0045 and 0058): in Spring that means potato, **15 h** |
| End of session 1 | `comeBack` says "Your potato will be ready at 14:05 **tomorrow**" |
| The next 15 h | The garden still loops and field radish (2 h) exists, but nothing points to them, and radish pays 7 a kg |
| Day 2 | The potato, the first *While you were away* card, the first field harvest |

Four causes:

1. **Spring has a gap in its timers.** Spring's field crops are radish (2 h) and potato (15 h). Wheat (6 h) and lentils (8 h) are Summer and Winter crops. So in the season **every** new game starts in, nothing sits between 2 h and 15 h.
2. **The first session ends by pointing at tomorrow.** The game itself tells the player the next real thing is a day away.
3. **One timer at a time.** Hay Day's crops are not all fast. It feels busy because many timers run at once and finish at staggered times, so something is always nearly ready. Our first day has one or two timers.
4. **Nothing to do that isn't a timer.** Between harvests there's no small chore, request or goal to pick up.

The mid-term pace is fine: the Hamlet comes at a median of day 5.4 (bots, Sprint 8). The problem is the first 24–48 hours.

## 3. A target to design against

> **In the first 48 hours, while the player is awake (07:00–23:00 local), something worth tapping is never more than about 2 hours away, and the first session ends with a reason to return within 1–3 hours, not tomorrow.**

It can be measured like the other gates: a balancer bot that plays the first two days at waking hours and reports the longest gap between meaningful events (a harvest, a pick, a request, a goal met). This keeps whichever ideas we choose honest.

## 4. Guardrails (what any idea must keep)

- **No speed controls, no skip-the-timer** (ADR 0002). Timers stay real.
- **Nothing to buy** (ADR 0006, 0007): no gems, no paid speed-ups.
- **No energy bar** (ADR 0010): chores use the founder's labour hours, never a stamina meter, and never block a tap.
- **No timed decisions** and no penalties for being away (crops wait 72 h before spoiling, ADR 0034).
- **Realism** (the owner's standing preference): crops and timings that a real Indian farm would recognise.
- **Determinism and data:** every new number in `content/`, every new rule in a spec with tests.

## 5. Ideas

Cost: **S** = a few points, mostly content and onboarding; **M** = a story or two with sim, view and art; **L** = a feature.

### Idea 1. Dadi's fields are still growing (a farm in motion) · S

Start with 3–4 acres already sown by Dadi last Winter, as a real farm would have in Spring (the rabi harvest comes in March and April). The neighbours kept them watered. They finish at staggered times:

| Acre | Crop (sown last Winter) | Ready after the start |
| --- | --- | --- |
| A well acre | Wheat | about 15 min (inside the first session) |
| A rain-fed acre | Lentils | about 3 h |
| A rain-fed acre | Potato | about 7 h |
| A well acre | Potato | the next morning |

- **Why it works:** the first field harvest and a real sale happen in session 1, and three more harvests are already lined up for the day without any new rule. It also shows the player what a full farm looks like.
- **Fits:** a start-state change (`start.json` gains "crops in the ground" with time left and a Care level), plus a step in spec 009. Wheat and lentils as a harvest (not a sowing) in Spring are realistic.
- **Watch:** the GDD calls the farm "neglected". This makes it *half*-neglected (Dadi's last sowing), which pairs well with Idea 4. Golden replays need a new start state.

### Idea 2. Fill Spring's gap with real zaid and late-rabi crops · M

Add field crops that farmers really sow in Feb–April (and some in other seasons), so every season has a ladder of timers: **about 1 h, 2 h, 4–6 h, 12–15 h**.

| Crop | Seasons (suggested) | Growth (placeholder) | Note |
| --- | --- | --- | --- |
| Methi (fenugreek) | Spring, Winter | 1 h | Leafy, cheap seed, sells well fresh, spoils fast |
| Peas (matar) | Spring, Winter | 4 h | A pulse: gives back to the soil like lentils |
| Okra (bhindi) | Spring, Summer | 5 h | Classic zaid vegetable |
| Onion | Spring, Winter | 6 h | The GDD already lists onion at 4–8 h "later" |

- **Why it works:** it fixes cause 1 at the root, for every season, not only the first day. The hand's crop choice (ADR 0048, 0050) picks them up with no new logic.
- **Fits:** content plus a spec 003 amendment (the crop calendar), Blender sprites for each crop, a balancer retune.
- **Watch:** more crops means more art and more balance work; two crops (methi and peas) may be enough to close the gap.

### Idea 3. The first session ends with a short return, chosen by the player · S

Replace step 6's "a long crop of at least 12 h" with Dadi asking: **"When can you look in again?"** *In an hour · In a few hours · Tomorrow morning.* She then suggests a crop that is ready by then (radish 2 h, a 4–6 h crop, or potato), and `comeBack` names that time: "Your radish is ready at 16:40."

- **Why it works:** it fixes cause 2. The player's own answer sets the first return, and the notification ask is about a time they chose.
- **Fits:** `onboarding.json` (the step's goal) and spec 009 §3.2. Works best with Idea 2 (a real "in a few hours" crop in Spring).
- **Watch:** it reverses ADR 0045 and 0058 (the ≥ 12 h long crop), so it needs a new ADR. The long crop is then taught at the first evening or the first return instead.

### Idea 4. Clear the neglected farm (chores that aren't timers) · M

The inherited acres start with things to put right, each a tap and a short job for the founder (5–30 minutes of real time, using the founder's labour hours):

- Pull weeds from a rain-fed acre (fertility back; fodder for later).
- Pick stones from an acre (the stones cut the cost of the hand's hut plinth).
- Mend the fence and the gate (the boundary of ADR 0062 drawn whole).
- Clean the well channel (the second well acre starts watering).
- Cut the dead branches of Dadi's old mango (firewood to sell or use).

- **Why it works:** it fixes cause 4 the way Hay Day's trees and rocks do: small, satisfying, visible changes on the map, each with a reward, while the timers run.
- **Fits:** the "neglected farm" story, the founder's labour model (ADR 0010), the art pipeline.
- **Watch:** must stay a handful (8–12 chores), finite, never repeating; new tile states and sprites.

### Idea 5. Village requests from day 1 · S–M

Before the supermarket orders arrive with the Hamlet (ADR 0055), the village asks for small amounts, 2–3 requests at a time, refreshed every few hours by day, never a penalty:

> "Lata's dhaba needs 3 kg of coriander by 19:00. She'll pay 90 a kg."

- **Why it works:** a short goal with a deadline in hours, paid above the trader's floor. It gives the garden a purpose after the first pick and teaches demand and price.
- **Fits:** a small version of the supermarket orders' machinery (one row per order, the Delivered tag of ADR 0059), the village's people (spec 014).
- **Watch:** keep the extra income small so the Hamlet pace (day 5.4) doesn't drop much; the bots will tell us.

### Idea 6. Dadi's list: a ladder of first goals with small gifts · S–M

A short list of 8–12 tiny goals for the first two days, each paying a gift from **Dadi's tin box** (a few hundred coins, a bag of compost, a sapling, a little seed):

- Pick the garden 5 times. Harvest your first acre. Sell with a minimum price at the market. Open a *Why?*. Sow a crop on a rain-fed acre. Fill a village request. Hire a day labourer for an hour.

- **Why it works:** a steady sense of progress (Hay Day's level-ups) between the timers, and it walks the player into the systems the first week teaches. It leads naturally into the existing *Becoming a Hamlet* progress view.
- **Fits:** the letter and the nudge pill (ADR 0045) already show one step at a time; this is a visible list.
- **Watch:** the gifts are money into the economy: keep them small and in content.

### Idea 7. Dadi's hens (or a goat) from day one · M

Five desi hens in a coop by the hut lay through the day (eggs to collect every 2–3 h in daylight), or a goat gives milk morning and evening.

- **Why it works:** a short repeating loop that needs no sowing, and a living thing on the farm on day 1. It previews the cattle of the Hamlet (ADR 0056).
- **Fits:** a real Indian homestead; the livestock code exists.
- **Watch:** livestock is a Hamlet feature in the GDD and poultry is a new species: a GDD change, a new good and sprites.

### Idea 8. A head start: chitted seed and a nursery · M

Dadi's seed potatoes are already sprouted (chitted), so the first potato takes about 8 h, not 15. Later, rice can be raised in a nursery bed and transplanted, which shortens its time in the field.

- **Why it works:** shorter first timers with a real farming reason, and a lesson (inputs that buy time).
- **Watch:** it changes growth durations, a rule that stood since spec 003 (weather never changes duration either). It weakens the 15 h realism the owner chose for rice and potato (ADR 0055, 0058). I'd keep this one for later, if at all.

### Idea 9. A prologue: Dadi's last season, as a memory · L

Before the letter, 8–10 minutes as Dadi's helper in her last season, on an accelerated clock (the GDD allows scenarios on their own save and clock): sow, watch it grow in seconds, harvest, sell, see the season report. Drawn in warm sepia, clearly a memory. Then the letter opens and real time starts.

- **Why it works:** the player sees the whole loop, and the payoff of waiting, in minutes. The long timers then feel known, not empty.
- **Watch:** scenario mode isn't built (it's in the Learning layer, later). Two clocks may confuse; it must look plainly like a memory.

### Idea 10. The village lends a hand on day 1 · S

For the first day, neighbours come to help Kamla's grandchild: free day labourers until the first evening.

- **Why it works:** the player can sow all 6 acres with several crops on day 1, so many timers run at once (cause 3), and hiring is met early.
- **Watch:** it eases the Hamlet gate's Care ≥ 0.9 in the first season; check with the bots.

### Idea 11. Dadi asks: small predictions while you wait · S–M

Now and then Dadi asks a question about the farm: "Which acre will give more potato, the well acre or the rain-fed one?" The player picks, and the answer comes at the harvest with its *Why?*.

- **Why it works:** a tap-sized thing to do that isn't a timer, and it serves the learning pillar (every number explained).
- **Watch:** a few, spaced out, never a quiz wall.

### Idea 12. "What's next" on leaving and on return · S

When the player leaves (and on the away card), show the next three things and their times: "Radish 16:40 · Lata's order by 19:00 · Dadi's potato 07:10 tomorrow". The first-day notifications follow the same list.

- **Why it works:** the game never says "nothing until tomorrow" while things are lined up.
- **Fits:** mostly presentation in `Sim.View`; works with any of the ideas above.

## 6. What the first day could look like

With Ideas 1, 2, 3, 5 and 12 together, a player who installs at 09:00:

| Time | What happens |
| --- | --- |
| 09:00–09:25 | Letter, garden, first pick, **Dadi's wheat acre ready: the first field harvest** and a real sale. Dadi asks when they'll be back: "In a few hours". They sow peas (4 h) |
| 09:25 | Leaving: "Lentils 12:00 · Peas 13:25 · Lata's coriander by 19:00" |
| 12:00 | Dadi's lentils; sow methi (1 h) on that acre |
| 13:25 | Peas in; fill Lata's order from the garden |
| 16:00 | Dadi's rain-fed potato; the long crop (potato, 15 h) sown in the evening |
| Next morning | The potato, the first *While you were away* card, the first season's plan |

## 7. My recommendation

**First (one sprint, mostly S):** Idea 1 (Dadi's fields), Idea 3 (the player picks the return), Idea 12 (what's next), and the target in §3 as a bot check. These fix the first session's ending and the first day's emptiness with little new code and no new art.

**Next:** Idea 2 (two or three new crops, starting with methi and peas) to close Spring's gap for good, and Idea 5 (village requests) or Idea 6 (Dadi's list) for something to do between timers.

**Later or not at all:** Ideas 4 and 7 are good but cost art; Idea 9 waits for scenario mode; Idea 8 cuts against the realism you chose.

## 8. Questions for the owner

1. Which ideas (or bundle) do you want to take forward?
2. Is a *half*-neglected farm (Dadi's last crops still in the ground) all right with the story, or should it stay fully neglected?
3. Is the target in §3 (never more than about 2 hours between things to do while awake, in the first 48 hours) the right bar?
4. Where should this land: before Sprint 14's planned scope, inside it, or as a Phase 2 enhancement sprint like Sprint 11's (ADR 0057)?

Once you choose, the next step is an ADR for the decision and an amendment to spec 009 (and spec 003 if new crops are in), for your review before any code.
