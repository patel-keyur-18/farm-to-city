# 0150. US-156 forward contracts as built: offers at the close, delivery by normal selling, a shortfall at the average price

- Status: Proposed (awaiting the owner's review of US-156)
- Date: 2026-11-21
- Deciders: Keyur Patel
- Related: spec 027 §3.7, §7, Preview 13 (approved 2026-11-21), ADR 0141, 0149

## Context

Spec 027 §3.7 sets the trader's fixed-price contracts (G7, closed by the owner). Building them settled five things the spec left open.

## Decisions

1. **The offers.** At each close, after the loans and the other charges, the trader offers a contract for each of your two biggest staples (wheat, rice, maize, mustard, lentils, potato) harvested last season at 300 kg or more: 40% of that harvest, rounded to 100 kg, for delivery in the season after the one that begins. The harvest is a tally kept by the `Harvest` command (`ContractsState.HarvestThis`, rolled to `HarvestLast` at the close; harvests before the Township do not count). The price is the expected price less 3%, never above it; the expected price is the delivery season's floor (base price × price index × that season's factor, less transport), without the day's swing or any event, fixed in coins a kilo.
2. **The limit is counted within a delivery season.** The spec's "at most 2 open, one a crop" would have blocked next season's offer for a crop whose contract is still delivering (offers arrive at the start of the delivery season), so a player could hedge only every other season. A contract still delivering does not count against the offers for the season after. The two rejections stay as guards (the offers' own two crops make them unreachable today).
3. **Delivery** is the selling you already do: `Store.SellToTrader` pays the first kilos of a contracted crop at the contract price up to what is left, every route (the Sell button, a hand's order, held goods, a harvest's overflow), so the player never picks which kilos count. The mandi and the market are not the trader and do not fill a contract. What the contract paid over (or under) the floor is kept on the contract as `Gain`, for the card's "against the market". The Sell buttons (the Store row and the good's sheet) show what the trader will really pay, the contract's price on its first kilos, and the row says "Your contract: the first 500 kg are paid 7.89"; found in the Editor tour, where the button first showed the floor's value.
4. **The shortfall** is the unfilled kilos × what the average of the trader's 6:00 floor prices over the delivery season rose above the contract price (nothing if it fell). It is charged at the close with the other charges (after the loans, before the insurance premium), from cash; what cash cannot pay is carried in `ContractsState.Owed`, paid first at the next close. Unlike unpaid tax it is **not a ledger liability** (it has no account and is not in net worth until paid); a shortfall is small, and a liability meant a net-worth line in six statements. It is posted in the season that begins, like every other charge at the close, as a farm cost *Contract shortfalls* (`ContractSettlement` 5036, schema 22). The close's dues list shows it beforehand (`DueKind.Contract`: the running shortfall as it stands, plus what was carried).
5. **Screens** as Preview 13 drew them, in the Store's trader section under the sale rows: *Trader's offers* (a row a crop with Accept; a one-line hint when there is none), *Your contracts* (delivered, to go, days left, "against the market you gained or lost X") and the last two settled. Accept opens a tap card that says it is binding and shows the quantity, the expected price, the fixed price and the delivery season; nothing is paid now. A bell alert says the trader has offers; the away card says an offer and a settled contract. Every row has a *Why?*.

## Consequences

- Save schema 22 gains `ContractsState`; the fixtures are regenerated. A town that never harvested a staple at the Township hashes as before.
- The spec's tests are `ForwardContractTests` (`ContractTests` is spec 006's hiring contracts); AC-13's names are corrected.
- Whether a contract is worth it is for the balance pass: the 3% discount, the 40% share and the average-price settlement, measured with `GateTests.AcceptEveryContract` (an accept-every-contract bot no better than the median).
