# 0076. Sprint 15 plan: two weeks, save schemas by sprint, and a developer era jump

- Status: Accepted
- Date: 2026-10-27
- Deciders: Keyur Patel
- Amends: spec 017 §4 and AC-8, spec 013 §4 and AC-9 (save schema 12, not 11), ADR 0074 and ADR 0075 (their "save schema 11" consequence), spec 001 §3.8 (the developer panel)
- Related: ADR 0025 (sprints), ADR 0041 (one write path, migrations), ADR 0047 (migrations from the RC, a fixture per schema), ADR 0057 (spec order)

## Context

Sprint 15 builds production chains (spec 016's bakery and dairy, F12.2) and full hiring and HR (spec 015, F11.4), and writes specs 018 and 019. Re-pointed with their art, screens and Editor tours, its stories come to 36 points, about Sprint 14's size (36 points in 2 weeks).

Specs 015, 017 and 013 each planned "save schema 11". Spec 015 is built in Sprint 15 and specs 017 and 013 in Sprint 16, each sprint with its own device build. A schema that has shipped to the owner's phone can't be extended again.

The bakery and the dairy are Village-era, but the Village unlock (US-102) comes in Sprint 17. On the device they would stay greyed until then.

## Decision

1. **Sprint 15 runs 2 weeks** (8–21 Mar 2027); Sprints 16–18 move back a week.
2. **Save schemas by sprint:** schema 11 in Sprint 15 (workers, job posts, employer reputation, a crafting skill for every citizen, the bakery's mix and the dairy's settings); schema 12 in Sprint 16 (spec 017's shops and rivals, spec 013's irrigation, machines, orchards and granary). Each sprint's device build migrates once. Only the numbers change in specs 017 and 013; their rules are unchanged. Their tests become `MigrationTests.Schema11_Shops` and `Schema11_Farm`.
3. **The developer panel gets "Next era"** (Development builds and the Editor only; US-218, 1 point): it moves the town to the next era, so a Village business can be tested on the device before Sprint 17. Use it after a Snapshot and Restore afterwards. Release builds never show it.
4. **Re-pointed:** US-119 the bakery 3 → 5, US-216 the dairy 3 → 5, US-112 the hiring board 5 → 8, US-113 output and training 5 → 8, US-114 HR events 3 → 5. For Sprint 16 (rechecked as ADR 0074 and 0075 asked): US-120 customer choice 5 → 8 (the sweet shop's recipes and batches, pitches, the mill's choice), US-127 the irrigation ladder 3 → 5 (the pump on the well, the borewell), US-128 equipment 5 → 8 (bullocks, the tractor bought, leased or hired, the harvester), US-129 the orchard acre 3 → 5, US-130 the granary 2 → 3. Sprint 16 becomes 37 points over 2 weeks.

## Consequences

- `SaveCodec.SchemaVersion` becomes 11 in Sprint 15 with a migration, `saves/schema11` and `EverySchemaLoads` up to 10; Sprint 16 adds 12 the same way.
- F11.4, F12.2, F12.3, F13.2, F13.3 and F08.4 carry the new points; `sprints.md` has the new dates.
