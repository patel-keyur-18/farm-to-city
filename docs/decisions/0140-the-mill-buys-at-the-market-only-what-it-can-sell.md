# 0140. The mill buys wheat at the market only while its unsold atta and bran stay under two days of what the trader takes

- Status: Accepted (owner, 2026-11-20: "Yes to the cap", at 2 days)
- Date: 2026-11-20
- Deciders: Keyur Patel
- Amends: spec 016 §3.5c ("the market's wheat, if allowed")
- Related: ADR 0054 (the trader's truck), 0068, 0073 (the mill's targets), 0139 (a pass that throws)

## Context

The owner's flour mill showed a season profit of 38,959 and **0 cash**, with **184,475 coins of stock** and 263,569 of the owner's capital put in. The switch *Buy wheat at the market* was on. The mill then bought wheat at the import price every open hour and ground all it could (about 3,800 kg a day). Who buys atta from the mill? Only your feed room (bran, for cattle), your other businesses (a bakery's atta) and the trader's truck, which takes **400 kg of atta and 100 kg of bran a day** at his floor price (regional less transport), inside the truck's 4,000 kg a day. The village's people do not buy from the mill (they pay it a grinding fee to grind their own wheat; the valley's atta comes from the market's supply). So the mill turned its cash into stock about 2,500 kg a day faster than it sold, earned about 3.3 coins a kg on what sold, and paid power on all of it. The plan never showed this leg.

The same starved state exposed a second bug: the purchase was sized with the price and the power rounded apart from the amount, so at 0 cash and a used overdraft it overshot by a few paise and the books refused `ledger.power` ("would make Cash negative"). That error, thrown inside the hourly pass, is what ended the owner's clock and the HUD (ADR 0139).

## Decision

1. **The cap (content `businesses.json` `area.marketStockDays` = 2).** `BusinessRules.MarketRoom`: the mill buys market wheat only while each output's unsold stock is under `marketStockDays` × what the trader takes a day (each output's room divided by its yield; the least rules). Over the line it buys nothing and sells down. Your own wheat (`Your wheat a day`) is not capped.
2. **The purchase is sized to the cent.** `Affordable` reduces the amount until the exact cost of the wheat plus its power fits the cash and overdraft, as the entries will post it. Nothing changes for a mill that can pay.
3. **The *Why?* of *Buy wheat at the market* says it** (the trader's daily amounts, that the village does not buy from the mill, the 2 days).

## Consequences

- A mill that buys at the market keeps about 800 kg of atta and 200 kg of bran unsold at most, so about 500 kg of wheat a day is bought; its cash is not buried in stock. Its margin on that leg is about 3.3 coins a kg (about 1,600 a day).
- A town that already holds more stock than the cap sells it down to the trader at 400 kg of atta a day and buys no more wheat until it is under the line.
- The recipe businesses (bakery, dairy, shops) make toward a daily mix and the kiln's yard is capped, so none can bury its cash like this; each is tested starved (`MillCashTests`).
- No save schema change; `Sim.Core` fixtures and goldens are unchanged (the cap and the sizing only act on a mill that buys at the market).
