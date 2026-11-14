# 0110. Rent as built: a kutcha house's market rent by place, a yearly lease, a raise that never exceeds what the family can bear, and settled families that are patient

- Status: Accepted (owner, 2026-11-13: the renewal card approved from its artifact preview; the affordable cap and the settled-family patience approved in the same conversation)
- Date: 2026-11-13
- Deciders: Keyur Patel
- Amends: ADR 0107 §8 (rent: the loose "a rent past about 30% of a family's income risks it leaving" is replaced by §2 below); ADR 0063 §2 and spec 014 §3.4, §3.6, §3.10 (rent, the hand's house, moving out); ADR 0102 §3 (c) (decided in ADR 0107 §8, built here)
- Related ADRs: 0063, 0071, 0083 (the hand's house), 0101 §12 (everyday cards), 0102, 0107, 0109 (rent control caps the rise later)
- GDD sections: Land, farming and livestock (rent); Citizens and workforce (migration)

## Context

ADR 0107 §8 decided the rent's shape (a kutcha house at about 2,500, by place, in yearly leases with rises capped at 10%) and said a rent past about 30% of income "risks the family leaving (existing rules)". The code has no such rule: a family leaves only when it falls two seasons behind on rent after spending its savings, or stays unhappy (below 35) for two closes. The owner pointed out that leaving after a rise is harsh: the family's savings grow and it settles in the village.

## Decisions

1. **The market rent** of a family house is the kind's base (kutcha 2,500) × the place (an acre touching the district road ×1.20, a lane ×1.00, inner ×0.85) × the price index, to a whole coin. Road tiles are content (`population.json` `house.roads`).
2. **A raise never pushes a family out.** At a lease's renewal the market rise is the least of the market rent, the 10% yearly cap and the rent at which the family would pay 30% of its income; it is never below the current rent. A family already over 30% keeps its rent and may be offered a lower one (5% off). Income and savings grow with the years, so the bearable rent grows with them. The card says what limits the raise and no longer says families leave over 30%.
3. **Leases.** A household's rent is fixed for 4 seasons from move-in (or from the day a hand's family stops being his). In the last season a card (and an alert, and the household sheet) asks how to renew: the market rise (the default), a smaller rise (half), keep the rent, or lower it. With no answer the market rise applies at the close; nothing waits on the player. A lease above the market (an old 6,000 rent) renews at the market with no card.
4. **Goodwill.** A smaller rise gives the family +1 happiness for the next lease, and keeping or lowering the rent when a raise was possible +2. Happiness is what the leave rule measures, so a kind landlord's tenants are less likely to leave.
5. **Settled families are patient.** A household that is unhappy leaves after `leaveSeasons` (2) closes plus one for each year it has lived in the town, at most 6. Falling behind on rent stays at 2 seasons.
6. **The hand's house** in his pay is the market rent of his house at his renewal. Houses already built become kutcha houses at their cost and book value; a tenant's present rent is its lease, starting at the migration (save schema 18).
7. **The renewal card** was previewed to the owner as an artifact in the game's own look and approved. As built it has one change from the preview: a *Decide later* button (the market rise then applies at the close, and the alert and household sheet keep the question) and the second line of each button inside the button.
8. **The Village's pace (owner, 2026-11-13).** With rent about 2,500 the competent bot reaches the Village at a median of day 41 with credit (36–45), about 6 weeks, against 32 before. Five households still arrive by day 16; the school opens later because five houses' rent falls from about 30,000 to about 10,600 a season. Measured and rejected: a house at 45,000 or 40,000 (day 39–40; a 4%-a-season yield turns unrealistically high) and a base rent of 3,000–3,500 (day 40–41). The owner accepted the slower pace; the target is 28–45 days (spec 018 AC-9, `GateTests.Village`). The cautious saver slips the same way, median day 56 → 69 (56–72), so its target is 50–75 (about 10 weeks, was about 8); every order still gets there in every run. ADR 0107's "Village 5 weeks" reads as 6 weeks. Passive income (`FinanceGate.Measure`, US-240's balance step): *Passive income covers your living* is now reached around day 20 by the competent Village bot and day 60 by the saver; ADR 0102 §3 (c) is settled by this rent. **Carried risk:** ADR 0107 §5 puts the Township at day 63–77 with credit; the Village a week later leaves about 3–5 weeks for its goals, so US-100 (Sprint 22) measures it first and a miss goes to the owner.

## Consequences

- Spec 014 §3.14 carries the rules; AC-18 to AC-24 name their tests (`RentTests`, `MigrationTests.Schema17_Leases`, `RentViewTests`).
- Save schema 18: each household's lease start, rent, answer, goodwill and the season it settled; a schema-17 town's tenants keep their present rent as a lease from the migration.
- A kutcha house lets for about 2,500 (4% a season on 60,000, against 10% before): passive income and the Village's pace are re-measured in the sprint's balance step, and a slip past 5 weeks with credit goes to the owner with its numbers.
- Spec 022's rent control lowers the 10% cap to 3%; its house-tax guard (15% of the actual rent) reads the lease rent.
