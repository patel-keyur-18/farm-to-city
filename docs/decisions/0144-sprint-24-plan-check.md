# 0144. Sprint 24 plan check: 35 points, the mill's atta to the local market, no "Pay me every day", US-247 belongs to spec 028

- Status: Accepted (owner, 2026-11-20: plan approved as proposed; the two Sprint 23 leftovers decided below)
- Date: 2026-11-20
- Deciders: Keyur Patel
- Amends: spec 027 §2 (the investor's offer's spec); spec 016 §3.5c (the atta note); `sprints.md` (the Sprint 24 row and section)
- Related: ADR 0107, 0126, 0127, 0132, 0139, 0141, 0142, 0143

## Context

Sprint 24 builds spec 026 (town life) and spec 027 (events, insurance and contracts) and opens the hills (US-147), and writes spec 028 (business growth) for Sprint 25. Spec 025's calves, silage, your tractor and solar were built in Sprint 23 (ADR 0126), so the sprint's own table is the 32 points of seven stories. The plan was checked against the repository at `main` (`9e9e69d`), Sprint 23's as-built notes and ADRs 0139 to 0143.

## Decisions

1. **The order.** US-151 reputation, approval, crime and inequality (5; spec 027's event weights read it) → US-150 petitions (3) → US-149 festivals (3) → US-154 events and disasters (8) → US-155 insurance (5; needs fire and *event health*) → US-156 forward contracts (3) → **US-264** the mill's atta at the local market (3, new, decision 3) → US-147 hill parcels and apples (5, after its gaps). **35 points.** Spec 028 is written in parallel, its gaps closed one per message. Save schema 22 is bumped once and ships at the end, as schema 21 did.
2. **Previews.** 12 (events) and 13 (insurance and contracts) as spec 027 §7 defines them; **14** (Town life: approval, petitions, reputation, crime, inequality, and the festival card) and **15** (hills and apples) are new, because spec 026 §7 named "Sprint 24 Preview 1" and nothing for the hills. Each is an exact artifact approved before its UI code.
3. **The mill's atta goes to the local market** (owner's decision on the open item of ADR 0140): spec 016 §3.5c said atta also sells at the local market; the code sold it to the bakery and the trader only. The code changes to the spec, as **US-264** (3 points, Should). The mill's targets (ADR 0073, 0140: hired 5,000–10,000, run by you 30,000–40,000, `marketStockDays` 2) are re-measured, and a gap on how the mill's atta shares the local demand with the founder's own offers is closed with the owner before code.
4. **"Pay me every day" is not built** (owner's decision). Drawings stay a command on the business sheet.
5. **US-147 is blocked until its numbers are redone.** Spec 025 §3.5's apple figures are marked placeholders (150 kg a tree is about 24 t an acre against India's 3–4; 8 coins a kg against mango's 50; apples spoil fast). Four gaps are closed with the owner, one per message, before any US-147 code: yield and price, sapling and years to fruit, keeping time and cold store, how the hills open. The hill parcels already exist in `map.json` (`opens: "hills"`, location factor 0.75), so the map does not change.
6. **US-247's spec is 028.** Spec 027 and ADR 0141 said the investor's offer card comes with spec 029, but US-247 is built in Sprint 25 under F12.6 and spec 029 is Sprint 26's Chronicle and Almanac. Spec 028 owns the card and plugs it into spec 027's engine. Spec 027 §2 is corrected.
7. **Spec 028's scope** is the ten stories whose feature files cite it: US-123, US-124, US-125, US-247, US-244, US-245, US-246 and US-258 (Sprint 25, 39 points) and US-259 and US-260 (Sprint 26).
8. **Risks recorded.** (a) `Crop` gains `EventHealth`, `Insured`, `SumInsured` and `Premium`; if its written form changes for every game the goldens move and are re-pinned once, said in the commit. (b) The new systems run inside the per-system guard (ADR 0143), keep the fixed system order, and keep the 72 h catch-up guards (ADR 0132); a daily roll never loops while away. (c) Two fixtures (`schema22-townlife.ftc`, `schema22-risk.ftc`) from one schema.
9. **The cut order:** spec 027 §11 first, then US-156, then US-264, then US-147, then spec 026 §11. **Never cut:** the engine, its mercy rules and the away rules, crop insurance, fire with property insurance, every *Why?*, the schema 22 fixtures and the exploit checks.

## Consequences

- Sprint 24 is 35 points (the table's 32 and US-264); the Sprint 24 row and section say so.
- The balance pass also runs the 200-seed hands-off Township on the ring (ADR 0142) and the mill's re-measure.
- Spec 016 §3.5c's note is closed by US-264; spec 027 §2 now names spec 028.
