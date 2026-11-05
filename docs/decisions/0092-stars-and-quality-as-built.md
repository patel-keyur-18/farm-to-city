# 0092. Stars and quality as built: freshness without produce ages, the mill's stars, and shut days

- Status: Accepted (owner, 2026-11-05: §1–§3 decided before coding)
- Date: 2026-11-05
- Deciders: Keyur Patel
- Amends: spec 017 §3.5, §3.6, §5
- Related: ADR 0074 (spec 017), 0091 (shops as built), 0073 (the mill's targets)
- GDD sections: Businesses (customer choice: reputation)

## Context

US-121 adds quality and stars (spec 017 §3.5, AC-3). Three gaps came up before coding and were settled with the owner one at a time.

## Decisions

1. **Freshness without produce ages** (owner, 2026-11-05). The spec has a good vegetable stall put out only produce harvested in the last 2 days, and a good sweet shop withdraw sweets older than 2 days. The store keeps one total per good, not lots with ages, and its "harvested today" count (the supermarket's) starts again at 6:00, when the stall stocks.
   - Good quality at the vegetable stall and the sweet shop keeps nothing overnight: at 6:00 yesterday's leftovers are cleared as spoilage at their cost, then the day's goods go out fresh. The cost of good quality there is the waste, which teaches matching the day's amounts to demand.
   - Good sweets also cost 8% more to make (pure ghee and fresh paneer, `good.recipeCost` 0.08), on the batch's inputs and supplies.
   - Good chai takes 0.08 L of milk a cup (0.06 standard) and 1.20 coins more in supplies a cup (fresh snacks). Switching quality scales the stall's milk a day so it serves the same cups.
   - Standard quality keeps leftovers, spoiling at their rate (ADR 0091 §4). No save change for freshness.
2. **The mill earns stars** (owner, 2026-11-05). Spec 017 §3.6 has the area choose between your mill and the chakki by fee, quality, stars and distance, but stars were defined only for shops. The mill's day is scored the same way: the share of the day's area grain it ground of what came to it, standard quality (no choice for a mill), and value from its fee against the chakki's. A mill that keeps up at the chakki's fee climbs from 2.5 towards about 4.4 stars and wins a little more of the area. The business bots recheck ADR 0073's targets in the sprint's balance step.
3. **Shut days, softened** (owner, 2026-11-05). A day a shop or mill never opens (nobody to work it, its worker away) has no customers to score. "Everybody has some personal work or emergency where they have to keep shut a day": the first shut day in a row leaves the stars alone; from the second in a row each shut day scores as if it served nobody (service 0, plus quality and value), about a fifth of a star a day from 4.4. A day open but with no customers counts as all served; shorter hours by choice aren't shut days.
4. **Choices made while coding.**
   - The day's score is taken at midnight for the day just ended: served share × 2 + quality (standard 1, good 1.5) + value (1 at or below the reference, falling to 0 at 30% above), scaled so 4.5 points make 5 stars; stars move a tenth of the way to it (`reputation` in `shops.json`). Rivals keep their fixed stars in Phase 2 (§3.7).
   - `SetShop` carries the quality (1 standard, 2 good) with the price preset. Save schema 14 gains `OpenToday` and `ShutDays` on `Business`.
   - The card's Today shows the stars (shops and the mill) with a *Why?* of today's score so far, part by part; Settings has Quality with what good costs, and its *Why?*.

## Consequences

- Spec 017 §3.5 and §3.6 carry as-built notes; the GDD's reputation line says how stars move.
- Tests: `ShopTests.Stars` (AC-3), `ShopTests.GoodQuality`.
