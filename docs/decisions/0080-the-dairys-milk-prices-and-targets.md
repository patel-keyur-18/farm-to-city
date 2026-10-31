# 0080. The dairy: neighbours' milk, paneer for the town, and targets from the bots

- Status: Accepted
- Date: 2026-10-28
- Deciders: Keyur Patel
- Amends: spec 016 §3.8, §3.9b, §3.9c, §9; content `businesses.json` (the dairy's milk, its recipe order), `goods.json` (paneer), `region.json` (the trader's paneer)
- Related: ADR 0068 (spec 016), ADR 0079 (the bakery), ADR 0060 (milk)

## Context

US-216's business bot (`BusinessBot`, `Gate.Business`, the balancer's `dairy` mode: 12 seeds, 16 seasons, the dairy unlocked with the Hamlet because the Village gate is spec 018's, a buffalo and a cow) measured the dairy as spec 016 set it: hired, about −17,000 a season; run by you, about +6,500 with the farm foreclosed in 10 of 12 runs; on your own herd's milk alone, a loss either way. Nobody sensible would found it. Three causes:

- **Thin margins:** paneer at 320 a kilo barely covers its 5 litres of milk (287.50) and fuel; in real life wholesale paneer is about six times a litre of milk (ours was 5.6 times).
- **Ghee from whole milk always loses** (15 litres at 59.50 is 892 against 900 a kilo); real dairies make it from cream left over from other products.
- **Little to sell beyond the village:** the trader took only ghee.

Two engine faults also showed: at 6:00 the dairy took your milk only if it could pay for all of it (so short of cash it took none), and founding funded a season's wage or fuel but not a day's milk, paid at 6:00 and recovered only at the next morning's round.

## Decision

1. **Paneer 380 a kilo** (year-0) and **the trader takes up to 60 kg of paneer a day**: it keeps three days in the freezer, so it travels to town on the truck.
2. **The starting mix (and the plan) leave out what loses at today's prices** (`BusinessRules.Pays`): ghee from whole milk, priced at the dairy's next litre (neighbours' milk). It can still be made; its *Why?* shows what it costs and fetches. The trader still takes 20 kg of ghee a day.
3. **Neighbours' milk is on by default**, at the co-op's price and 2 a litre: a village dairy runs on the village's milk. The dairy's milk a day starts at what its mix needs; your herd's milk comes first.
4. **Recipes in the order spec 016 §3.9c gives:** buffalo milk to paneer, then ghee; cow milk to curd first; then the other milk.
5. **Engine:** your milk is kept at 6:00 as far as the dairy's cash goes (the co-op takes the rest). A recipe business's cash to put in adds a day's inputs and fuel as working capital (the bakery's day of atta too).
6. **Targets** (`GateTests.DairyBusiness`, 8 seeds, 16 seasons, on the median run):

| Dairy | Target | Measured (8 seeds; 12 seeds) |
| --- | --- | --- |
| Hired dairyman, neighbours' milk | 10,000–20,000 a season, payback 5–9 seasons | 14,700, payback 6.6; 14,700 |
| Run by you, neighbours' milk | 35,000–50,000, payback 2–4 seasons | 41,000, payback 3.7; 41,000, payback 2.5 |
| Your own herd's milk only | a loss (the lesson) | −17,000 hired |
| Never stuck | every run plays on with cash; overdrawn at most 3 closes in a row; hired never foreclosed; run by you at most half the runs | 0; 4 of 8 run by you |

The dairy sits a little below the bakery (hired 15,000–25,000, run by you 45,000–60,000, ADR 0079): less money in, thinner margins.

## Consequences

- **Flag (owner):** run by you, the farm was foreclosed in 4 of 8 runs (3 of 12 with the bot drawing cash when low). Those founders had 14,000–35,000 in hand; ten hours a day at the dairy on top of a herd and 6 acres starved the farm of Care, and loan payments slipped. The bakery run by you: 0 of 12. The plan's *Your hours there* line already shows what the hours are worth; a warning when they're more than the farm can spare is a candidate for a later story.
- The bakery's numbers are unchanged by these (ADR 0079 holds: hired 19,700, run by you 53,700, foreclosed 0 of 12 run by you, from 2 of 8, since the working capital).
- Spec 017's supermarket orders (Sprint 16) can ask for paneer and ghee too.
