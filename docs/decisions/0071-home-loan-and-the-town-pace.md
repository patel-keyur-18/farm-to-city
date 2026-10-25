# 0071. The home loan, and how fast a town of 5 households grows

- Status: Accepted
- Date: 2026-10-25
- Deciders: Keyur Patel
- Amends: spec 014 §3.4, §5, §8, §9; spec 005 (the bank's limit counts family houses); ADR 0028's Village pacing is kept for a player who borrows, and spec 018 sets the unlock from these measurements
- Related: ADR 0028 (pacing), ADR 0060 (the dairy loan), ADR 0030 (inherited acres are never seized), ADR 0063 (spec 014)

## Context

US-211 made the balancer's bots build family houses after the Hamlet and measured spec 014 §9. Five households need 4 family houses (2,50,000) beside the hand's hut. Paid from a Hamlet farm's cash, with the dairy first, the competent bot reached 5 households on day 46 and the cautious saver mostly not within 48 days, against the Village's 2–3 weeks (ADR 0028, days 14–21). The random bot never reaches the Hamlet, so it can never build. Building nothing but houses first, the competent bot still needed day 28.

## Decision

1. **A home loan.** The co-op bank counts family houses at `houseLtv` 0.75 of their book value (the one being built included) in its limit, as the dairy loan counts animals (`lenders.json`). `BuildFamilyHouse(tile, financing)` borrows up to the house's cost, the rest paid down. A house on inherited land is never seized (ADR 0030): the bank carries that risk. There is no money loop: a house costs 60,000 in cash to raise at most 45,000 of credit. The placement bar offers "With a home loan", whose sheet has a row a term (the dairy loan's layout). Family houses leave the land collateral's building sum (counted once, here).
2. **Measured with the loan** (20 seeds, two years; the competent bot builds homes first, says "Not now" when a hand asks to bring his family (the hand keeps the hut and a renting family takes the house, one household more for the same money), and borrows 0.75 of a buffalo): 5 households on **day 16 (15–22)**, 16 people at day 21 (10–20), town happiness 69 (68–70), nobody leaving, rent 12% of income (at most 14%). Two buffalo after two years, as before. The cautious saver, which never borrows: day 33 (29–43).
3. **The Village's pacing stays 2–3 weeks for a player who uses credit**; a saver takes 4–6 weeks. Spec 018 sets the Village's unlock from these figures; this gate checks 5 households by day 21 for the competent bot and by day 48 for the cautious saver.
4. **Town happiness 65–85** (spec 014 §9 said 70–85): a renting family whose adults work outside scores about 68 by §3.8 (AC-5's example) until businesses (spec 016) bring town jobs.
5. **The random bot isn't measured for the town**: it never reaches the Hamlet in Phase 1 either.

## Consequences

- Credit becomes the lever of a fast town, which is the lesson the game wants to teach (rent at 10% of the cost a season against the bank's rate). A saver still gets there, later.
- Tests: `HouseTests.HomeLoan`, `PeopleViewTests.HomeLoan_Sheet`, `GateTests.Town`. Golden replays unchanged (their content has no houses).
- US-211 grows by the home loan (3 → 6 points).
