# 0077. Spec 018: the school and literacy, the Codex, advisors and the Village unlock

- Status: Accepted
- Date: 2026-10-27
- Deciders: Keyur Patel
- Amends: GDD Player journey and eras (the Village's gate and what it opens; the treasury from the Township), Community and civic services (the first school, adult classes, literacy's premium), Learning layer (Phase 2's Codex and advisors), Roadmap (Phase 2's exit gate); spec 014 (the growth need's school row)
- Related: ADR 0011 (literacy versus skilled graduates; the founder funds the first school), ADR 0028 and 0071 (Village pacing), ADR 0061 (the school plot), ADR 0073 (the mill's targets), ADR 0076 (schemas by sprint)

## Context

Spec 018 was written in Sprint 15 (ADR 0057, 0076) and its eight gaps closed with the owner one per message. Two conflicts noted in Sprint 11 were settled with it: the GDD gave the Village "the treasury" though the treasury is Phase 3, and Phase 2's exit gate said "a week of play" though ADR 0028 puts the Village at 2–3 weeks. The owner asked, approving, that everything stay realistic, teach real lessons, and be achievable and clear for an average player.

## Decision

1. **The Village gate (G1):** 5 households, a business with 2 profitable seasons (not necessarily in a row), and the primary school built and open. The market stall is dropped (stalls come with the Village). The school can be built from the Hamlet. The Hamlet's celebration and the progress view name the three goals and what to do for each.
2. **The school (G2):** on the village's school plot, 1,05,000 (building 90,000, benches and books 15,000), 48 h, from cash or a co-op loan within the bank's existing room. The founder gives it to the district, which posts and pays the teacher (a government primary school); the founder pays 2,000 a season upkeep. Children aged 6–13 who can't read attend free and are literate after four life-years.
3. **Adult classes (G3):** the founder starts an evening class of up to 15 people aged 14+ for a season, 3,000; all are literate at the close.
4. **Literacy's payoff (G4):** a literate adult's outside work earns 10% more (about 1,900 a season today). No skill change.
5. **The Codex (G5):** about 24 Phase 2 cards in six groups, each found after its result; a toast and Menu → Codex; never a sheet over play or in the first session.
6. **Advisors (G6):** three people of the village with Blender portraits (an old farmer neighbour, the co-op bank's secretary, a retired headmaster); one line on the season report and on alerts they understand; at most one a sheet and a topic a game day; never in the first session; they never act.
7. **The treasury (G7):** with the Township (Phase 3). The Village opens the co-op bank's larger loans (25,00,000 cap), the bakery and the dairy, shops on the pitches, drip, the borewell and the tractor.
8. **The Phase 2 exit gate (G8):** the competent bot reaches the Village in 14–21 days using credit, the cautious saver in 28–42; the owner's playtest plays from the Hamlet to the Village; players can explain profit versus cash.

## Consequences

- Built in Sprint 17 (F14.1, F14.2, US-102) with save schema 13 (ADR 0076).
- The school plot's line and the "No school yet" row change in Sprint 17, when the school can be built (until then they describe the current build).
- New art: the school in four facings and its building stage, three advisor portraits, the Codex's icons.
- The balancer adds the school and the Village gate to the bots and `Gate.Village`; contradictions with the 14–21 day target are flagged to the owner.
