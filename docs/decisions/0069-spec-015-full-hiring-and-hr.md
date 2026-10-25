# 0069. Spec 015: one hiring board, fair pay, output from skill, motivation and fatigue

- Status: Accepted
- Date: 2026-10-25
- Deciders: Keyur Patel
- Amends: spec 006 (one board for every role, references, negotiation, output, four more traits, employer reputation), spec 016 §3.6 (its worker stopgap ends in Sprint 15), spec 014 (a citizen has a crafting skill beside farming; the household's happiness feeds a worker's motivation)
- Related: ADR 0002 (decisions wait for the player), ADR 0008 (season contracts paid upfront), ADR 0022 (hiring from Homestead), ADR 0039 §7 (skill in stars and words), ADR 0041 (regulars always accept), ADR 0068 (businesses)

## Context

Spec 015 was written in Sprint 13 (ADR 0057), and its nine gaps were closed with the owner one per message. Phase 2's places each have one worker: your hands on the fields, plus a miller, baker or dairyman from spec 016. Hiring has to feel real without making Phase 2 a staffing game, and it must leave existing hands and towns exactly as they are.

## Decision

1. **One hiring board (G1).** It covers the farm hand (30,000), miller (27,000), baker (30,000) and dairyman (27,000), all at skill 50. Your townspeople without a town job apply with their skill and traits known. Migrants apply as in spec 006; a migrant business worker commutes and isn't a citizen. Regulars apply as now. Spec 016's stopgap ends in Sprint 15.
2. **Knowing a candidate (G2).**
   - The skill shows as a range (±15) with one trait.
   - A free interview reveals the second trait.
   - A reference check (300 coins, 12 h) narrows the skill to ±5. It also says how they left their last job: "was let go" when a trait is lazy or careless.
3. **Negotiation (G3).** Offer at the asked wage (accepted 0.85), 10% less (0.40, then one counter at the asked wage) or 10% more (0.95). A low hire is remembered in motivation and reputation. Townspeople always accept at the asked wage or above. Regulars always accept.
4. **Output (G4).** Output = skill × motivation × (1 − fatigue).
   - **Motivation** runs from 0.7 to 1.2: +0.05 hired above the asked wage, −0.10 hired below it until paid in full, lower when the family's happiness is under 60, and moved by the HR events.
   - **Fatigue:** long days run up to 12 h, with the extra hours paid at 1.5×. They add fatigue, and normal days rest it off.
   - **Two skills:** citizens get a crafting skill beside farming, and each role reads its own.
   - **Neutral by default:** a fair hire in a content home works exactly as before.
5. **Training (G5).** On the job, +3 skill a season. A course adds +10 skill for 6,000 coins and 2 paid days away. A trained worker asks more at renewal and draws more offers from elsewhere.
6. **Four more traits (G6).**
   - Creative: a business's goods sell for 5% more.
   - Frugal: 10% lower running costs, and the household saves more.
   - Sociable: +0.05 motivation for colleagues.
   - Perfectionist: no bad batch, but 5% slower.
   - No contradictory pairs are drawn, and all ten traits are drawn evenly.
7. **HR events (G7).** A raise request, an offer from elsewhere, sick leave, a family emergency and a bad batch. A worker gets at most one a season. Each waits for the player while the worker carries on. Expect about 0–2 a season for 3 workers.
8. **Employer reputation (G8).** A score 0–100, starting at 50, shown with its *Why?*. Fair pay and fair treatment raise it, and unfair treatment lowers it. At 80 or above, candidates accept more often and above-market posts bring better applicants. At 20 or below, they accept less often and come more slowly.
9. **Left for later (G9).** Supervisors, benefits, minimum wage, managers and night shifts come in Phase 3. Education bonds come with the school (spec 018).

## Consequences

- Save schema 11 (Sprint 15) adds workers, job posts, reputation and a crafting skill drawn for every existing citizen.
- The golden replays are regenerated once, because new applicants draw from ten traits. Existing hands keep their traits and their output.
- Code: F11.4 (US-112–US-114) in Sprint 15, with spec 016's bakery and dairy.
