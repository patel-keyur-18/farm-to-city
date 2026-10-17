# 0008. Coin scale, payment timing and starting package

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0001 (Indian-inspired setting), ADR 0002 (4-day seasons). This ADR amends ADR 0002's "wages are paid daily" and "if cash runs out, workers stop".
- GDD sections: Player journey (start), Time (cadence), Businesses (plan, books), Citizens (hiring, wages), Assets (personal finance screen), Offline (away rules), Data model (money units)

## Context

The GDD left the coin scale open: abstract coins, or prices that roughly mirror real rupees. The owner also wants realistic costs. Labour is hired for a fixed contract price paid upfront, and land, houses, construction and materials are all paid for upfront.

Two problems came up. First, the calendar from ADR 0002 has no month, but the GDD used "monthly" in several places. Second, crops that take hours would make realistic per-harvest yields pay many times their real-world income.

## Options

- **Coin scale:** abstract magnitudes; real rupees (with the ₹ symbol); or rupee-scale numbers under a fictional "coins" currency.
- **Pay period:** one season (4 real days, standing for a real quarter); or a game month of ⅓ season (32 real hours). The 32-hour month would drift against the day/night cycle and add a third cycle.

## Decision

### Coin scale

- Prices **roughly match real rupee magnitudes**: wheat is about 25 a kg, a day of farm labour about 400, and a 2-acre plot is worth lakhs.
- The currency is named **coins**, with a coin icon and no ₹ symbol. Inflation will make prices drift from real ones, and a fictional currency keeps the game from being read as a statement about real prices.
- The **smallest unit is 1/100 coin** (like paise), stored as `long`.
- **Number formatting follows the locale:**
  - en-IN and hi-IN use Indian grouping and short forms: 1,00,000 · 12.5 L · 1.2 Cr
  - other locales use Western grouping: 100,000 · 1.25M
- **Anchor prices are data.** The economy model tunes them, and this ADR does not set them.

### Realism is measured per season

- **One season equals one real-world quarter for every money flow.** Wages, rent, interest, upkeep and per-acre yields are realistic in total per season.
- A crop cycle that is shorter than a season yields its share of the season's realistic output. Per-kg prices stay rupee-like.
- The economy model and the balancer's "no money loops" check enforce this rule.

### Pay period: one season

- "Monthly" in the GDD becomes "per season" everywhere: business-plan projections, the passive-income ratio, and the emergency fund (measured in seasons of costs).
- A worker's wage is a **fixed contract price per season**, about 90 days of real wages. For example, a farm hand costs about 36,000 coins per season. The price is renegotiated at renewal, when inflation and the market wage apply.

### Payment timing

| Cost | When it's paid |
| --- | --- |
| Land, buildings, equipment, livestock, materials, seeds | In full, upfront, at purchase |
| Construction | Upfront: materials when ordered, builders' wages when the build starts |
| Wages | Upfront, per season, at contract start or renewal |
| Rent | Upfront, per season |
| Founder's living costs (ADR 0003) | Upfront, per season |
| Utilities, taxes, loan interest and repayments | At season close, in arrears |

- **Accrual accounting.**
  - Prepaid wages and rent are balance-sheet assets, and they are expensed day by day across the season in the P&L.
  - Cash leaves on the day of payment.
  - This is how the game teaches "profit is not cash" and "prepaid expenses".
- **Contracts.**
  - A worker who has been paid for the season keeps working, including while the player is away.
  - If the player can't afford a renewal, the contract lapses and the worker stops.
  - If a worker quits or is let go mid-season, the unearned part of the prepaid wage is refunded, less any severance owed.

### Starting package

The inherited start is sized to allow gradual growth but not to buy growth outright. The economy model sets the exact figures in data (`StartDef`). The principle:

- **Assets:** the inherited 2-acre plot, a hut and a well, all owned outright.
- **Savings cover:**
  - the founder's living costs for about 2 seasons
  - seeds and inputs to sow both acres for the first season
  - a small buffer
- **Savings do not cover:**
  - buying land outright
  - hiring a worker for more than about 1 season

## Consequences

- **Time cadence (amends ADR 0002):**
  - Every real day, wage and rent costs are **expensed** in the P&L, but no cash moves that day.
  - At each season close, contracts renew and the next season's wages, rent and living costs are paid upfront. Arrears items (utilities, taxes, interest) are settled.
- **Away rules (amend ADR 0002):** "If cash runs out, workers stop" is replaced by "workers who are paid for the season keep working, and contracts that can't be renewed lapse". Nothing pushes the player into debt automatically while they are away.
- **New state:**
  - `Contract`: worker, wage per season, start, end, amount prepaid
  - `StartDef`: starting savings, plot, buildings
- **Still open:** borrowing, credit limits and what happens on bankruptcy. These are decided separately.
