# NNNN. A crop ladder: new crops in every season

> **Draft.** The number is given when the ad hoc phase starts, after Phase 2's last ADR (0072 is the latest on `sprint/14-flows-and-mill` today). Move this file to `docs/decisions/NNNN-crop-ladder.md` on approval.

- Status: Proposed
- Date: 2026-10-26
- Deciders: Keyur Patel
- Related: ADR 0021 (crop calendar), 0008 (per-season realism), 0014 (yield), 0016 (balance rule), 0031 (seasonal input prices), 0033 (land payback), 0048 and 0050 (the hand's choice), 0055 and 0058 (rice and potato in 15 h; the long crop ≥ 12 h)
- Spec: the crop ladder spec (draft: `spec-crop-ladder.md` beside this file)
- GDD sections: Time and world simulation (Task clock, Seasons); Land, farming and livestock (Crops); Roadmap

## Context

1. **The start feels slow** (owner, 2026-10-26). A new game starts in Spring, and Spring's only field crops are radish (2 h) and potato (15 h). Nothing in Spring takes between 2 and 15 hours. The first session ends with "your potato will be ready tomorrow".
2. **Other seasons have gaps too.** The Monsoon has radish (2 h) and rice (15 h) only. Summer has 2, 8 and 15 h. Winter is the fullest: 2, 6, 8 and 15 h.
3. The brainstorm (`docs/planning/proposals/first-day-pacing.md`) offered 12 ideas. The owner chose Idea 2 (new crops in every season) and set the others aside.
4. The owner's limits: it must not break existing calculations or the game's realism, and an average player must find it sensible and reachable.

## Options

1. **New crops, content only** (chosen). Add real Indian crops to each season's gaps. Every crop number is per acre per season (ADR 0008), so a crop's length changes how often an acre is harvested, not what it earns in a season. The yield, Care, water, pest, seed and fertility formulas stay unchanged.
2. **Shorter timers for existing crops.** Rejected: it reverses ADR 0055 and 0058, and every season's harvest sizes would change.
3. **Wait for the later crops the GDD lists** (maize, onion, cotton, sugarcane). Rejected for now: they come at different phases and don't fill the gaps on their own.

## Decision

1. **Six new field crops**, all available from the Homestead, with real sowing windows:

   | Crop | Sown in | Game time | Real basis |
   | --- | --- | --- | --- |
   | Fenugreek (methi) | Spring, Winter | 1 h | Leaves cut 25–35 days after sowing; sown Sep–Nov and Feb–Mar |
   | Amaranth (chaulai) | Summer, Monsoon | 1 h | Leaves cut 25–30 days after sowing; sown Mar–Jul; stands the heat |
   | Okra (bhindi) | Spring, Summer, Monsoon | 4 h | First picking 45–50 days after sowing; sown Feb–Mar and Jun–Jul |
   | Green peas (matar) | Winter | 4 h | Green pods picked 60–75 days after sowing; sown Oct–Nov |
   | Maize (makka) | Spring, Monsoon | 6 h | 90–110 days; spring maize sown Jan–Feb, kharif maize Jun–Jul |
   | Mustard (sarson) | Winter | 8 h | 110–140 days; sown Oct–Nov; the valley's neighbours already grow it (ADR 0062) |

2. **The ladder.** Every season then offers a crop on each rung: about **1 h, 2 h, 4 h, 6–8 h and 15 h**, so a player can pick a crop that is ready when they'll be back.
3. **No formula changes.** Spec 003's rules apply to the new crops as they stand. Only content, the sow sheet's order and two lines of wording change.
4. **The balance rule.** At the regional price with every factor at 1, no new crop beats the best existing crop of any season it's sown in. This applies both to the gross margin (output less seed) and to the net after labour at the day-labour wage. Potato stays the leader in Spring and Winter, and rice in the Monsoon. Each new crop wins only where it really fits: a quick return, a dry acre, a light-labour season or soil to rebuild.
5. **Realism rules for the content.**
   - Game times follow real growing days by band: quick greens 1 h, vegetables picked at 45–75 days 4 h, grains and oilseeds 6–8 h. Rice and potato keep their 15 h (ADR 0055).
   - Season yields are at or below real yields, as with radish.
   - Prices are farm-gate prices.
   - Seed costs 10% more in its sowing seasons (ADR 0031's pattern).
   - Produce is cheaper in its harvest season.
6. **Legumes feed the soil.** Peas and methi restore fertility like lentils. The fertility reason `Lentils` is shown as "Legumes fed the soil" (the saved value is unchanged).
7. **The sow sheet** lists this season's crops in order of growing time, each with its ready time ("ready at 16:40", "ready tomorrow 07:10"), then the crops of other seasons.
8. **Out:** onion (with the granary, F13.3), tomato, sugarcane and cotton (later phases); new kitchen-garden crops; maize straw and fodder; milling maize or pressing mustard.

## Consequences

- **Content:** `crops.json`, `goods.json`, `region.json`, `farming.json` (pest risk) and `strings/en.json` gain the six crops and their seed. There's no new definition field (mustard uses the existing `edible: false`).
- **Sim:** no new rule. The hand's choice (ADR 0048, 0050), supermarket orders (crops of 18 h or less in season), eating from the store, notifications and *Why?* pick the new crops up from content.
- **Saves:** content is matched by id (spec 007), so an old save loads with the new goods at their regional price. A migration test proves it; no schema change is expected.
- **Balance:** two new balancer bots: an active player who sows the quickest crops, and a diversifier who spreads over every crop's separate demand. Both must stay within 1.20 × the competent bot (ADR 0016). Every existing gate keeps its threshold.
- **Art:** 6 crops × 4 growth stages plus a produce icon, rendered in Blender (mustard's middle stage exists).
- **Hard-coded crop lists** in the presentation and the bots move to content or stop listing crops by name.
- **GDD:** the seasons table, the task-clock table and the roadmap's crop row are updated when this ADR is accepted.
- **Onboarding is unchanged.** The first session still sows a long crop of at least 12 h (ADR 0045, 0058). The new crops show in the sow sheet and the season hints.
