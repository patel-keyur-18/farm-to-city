# CLAUDE.md

## Project

Farm to City is an offline, single-player farming, township and city simulation for iOS and Android, built in Unity (C#).
- **The player** is an ageless founder who inherits 2 acres in a fictional, Indian-inspired river valley, and grows it through six eras (Homestead to City) by farming, hiring, borrowing, founding businesses and running the town.
- **The world** runs on a real-time clock like Hay Day's: work continues while the app is closed, up to 72 hours.
- **Every number** can be explained with a *Why?* breakdown, and the game teaches real business and finance concepts through play.
- **`docs/GDD.md`** is the master spec and the source of truth.
- **`docs/decisions/`** holds the ADRs, which record why the GDD says what it says.
- **Roles:** Claude writes all code and docs. The owner, Keyur, reviews specs and tests builds on devices. Keyur is a senior Java/Spring Boot developer who is new to Unity, so explain Unity concepts briefly and map them to backend equivalents.

## Session routine (ADR 0019): read this first

Every session starts with no memory. The repository is the only memory.

**At session start:**
1. Read this file.
2. Read `docs/planning/STATUS.md`: the current phase, sprint, next story, open questions and branch.
3. Read the current sprint file named in `STATUS.md` (`docs/planning/sprints.md`) and the feature file(s) for the next story (`docs/planning/features/`).
4. Read the spec(s) and ADR(s) those stories name.
5. Continue from "Next up" in `STATUS.md`. If anything in it is unclear or contradicts the repository, ask the owner before acting.

**At session end, or after each finished story:**
1. Tick the story and task checkboxes in the feature file. Update the sprint file.
2. Update `docs/planning/STATUS.md`: last done, next up, open questions, branch, anything half-done.
3. Commit and push.
4. At the end of a session or sprint, open a pull request to `main` and ask the owner to merge it. The next session clones `main`, so unmerged work is invisible to it.
5. **Every pull request gets labels and a milestone** (owner rule, 2026-10-17). Use only labels and milestones that already exist in the repository:
   - **Milestone:** the phase the sprint belongs to (`Phase 0 · Pre-production`, `Phase 1 · Homestead slice`, `Phase 2 · Hamlet & Village`, `Phase 3 · Township`, `Phase 4 · Town & City`, `Phase 5 · Launch`).
   - **`type:` labels**, one for each kind of change in the pull request: `type:feature`, `type:spec`, `type:adr`, `type:bug`, `type:balance`.
   - **`area:` labels**, one for each area touched: `area:sim`, `area:content`, `area:tooling`, `area:ui`, `area:art`.
   - **`documentation`** when docs change.
   - Re-check the labels whenever later pushes change what the pull request contains.

## Workflow (spec-driven)

- **No game code until its feature spec is written and approved.** Specs follow `docs/specs/_TEMPLATE.md` and live in `docs/specs/NNN-name.md`.
- **Specs are written rolling-wave** (ADR 0019): all of a phase's specs are written and approved in that phase's first sprint, before its coding starts.
- **Specs fix formulas, bounds and placeholder values** (ADR 0027). The economy model tunes values in `content/`, and spec defaults are then updated without re-approval. A formula or rule change needs re-approval.
- **Every decision of lasting importance becomes an ADR** in `docs/decisions/NNNN-title.md` (context, options, decision, consequences). ADR numbers are never reused. To change a decision, write a new ADR that supersedes the old one.
- **When a spec or ADR changes the GDD,** update `docs/GDD.md` in the same commit.
- **Sprints (ADR 0025)** are 1 week long, with at most 2 specs to review and 1 device build per sprint. Hierarchy: Epic → Feature → User story → Tasks. Story points, MoSCoW priority, and the Definition of Ready and Definition of Done in ADR 0025.
- **Work on one branch per sprint** (`sprint/NN-short-name`), unless the session assigns a branch. Use small conventional commits (`docs:`, `feat:`, `test:`, `fix:`, `chore:`). One pull request per sprint, merged by the owner.
- **Stop for the owner's review at the end of each task.** For anything ambiguous, give a recommendation and ask. Push back when something doesn't make sense.
- **Every build handed to the owner comes with a test checklist** in `docs/test-checklists/`: what to try, what to expect, what to report.
- **Test in the Unity Editor before calling a story done** (owner rule, 2026-10-21). Any story that changes what the player sees or taps: recompile, play, drive it with **scripted touch** (`Game.Editor.TouchDriver`, never the mouse), capture the Game view, look at every capture, and fix what renders or responds wrongly before reporting. Dotnet tests alone are not enough. How to drive the Editor is in `docs/planning/STATUS.md` (Environment notes).
- **This file and `docs/` are the project's memory across sessions.** Keep them current.

## Engineering rules (non-negotiable)

- **`Sim.Core` is pure C#** with zero `UnityEngine` references. The compiler enforces this through the `.asmdef` setting `noEngineReferences: true` (ADR 0018).
- **Determinism:** the same seed plus the same timestamped commands always gives the same state.
  - Sim time is a `long` count of seconds, separate from wall-clock time.
  - A fixed processing order: time, then system order, then entity id.
  - No floats in sim state or rules. Use the `Fixed` fixed-point type for rates and factors.
  - Our own seeded RNG, never `System.Random`.
  - Never take an order from iterating a `Dictionary` or `HashSet`.
  - IL2CPP-safe code: no `dynamic`, no `Reflection.Emit`, no reflection-based serialisation.
- **State changes only through commands.** The simulation emits domain events. The UI only reads state.
- **Money** is `long` in 1/100 coin, never floats. **Quantities** are `long` in each good's smallest unit.
- **All balancing numbers live in data** (`content/*.json`), never hard-coded.
- **Every acceptance criterion in a spec maps to an automated test**, named in the spec.
- **The sim's language level is C# 9.0 at netstandard2.1**, matching Unity 6000.3.26f1 (ADR 0029). No file-scoped namespaces, global usings or record structs inside `sim/`.

## Repository layout

```
docs/GDD.md                 master game design spec (source of truth)
docs/specs/                 feature specs (NNN-name.md) and _TEMPLATE.md
docs/decisions/             ADRs (NNNN-title.md)
docs/test-checklists/       per-build device test checklists
docs/planning/              STATUS.md (session handoff), epics.md, features/, sprints.md
FarmToCity.sln              solution for the dotnet projects
Directory.Build.props       LangVersion 9.0, warnings as errors,
                            output to /artifacts
sim/                        Unity package com.farmtocity.sim (package.json)
                            + dotnet projects
  Sim.Core/                 pure C# simulation: .csproj (netstandard2.1)
                            + .asmdef (noEngineReferences)
  Sim.Content/              JSON loader + validation (definition classes
                            live in Sim.Core, ADR 0020)
tests/Sim.Core.Tests/       NUnit 4 tests (net10.0)
tools/Tools.Balancer/       headless balancer on a fake clock (net10.0)
content/                    JSON definitions (balancing data)
unity/FarmToCity/           Unity project (Unity 6000.3.26f1, ADR 0029)
  Assets/Scenes/Bootstrap   the only scene; GameRoot builds everything else in C#
  Assets/Game/Presentation  Game.Presentation asmdef: composition root, UI (UI Toolkit)
  Assets/Game/Services      Game.Services asmdef: saves, platform ports, IEntitlementService
  Assets/Game/Editor        Game.Editor asmdef: copies content/*.json into the build
```

**Dependency direction:** Presentation and Services depend on Sim. The Sim never depends on Unity.

**Unity is code-first** (ADR 0017): one bootstrap scene, with everything else created by C# at runtime and UI in UI Toolkit (UXML and USS). Avoid hand-editing scene and prefab YAML.

## Build and test

The .NET 10 SDK (LTS) is required (ADR 0026). In cloud sessions the SessionStart hook (`.claude/settings.json` → `.claude/hooks/session-start.sh`) installs `dotnet-sdk-10.0` when it's missing and restores NuGet packages, so `dotnet test` works straight away. If the hook ever fails, install by hand with `apt-get install -y dotnet-sdk-10.0`.

```
dotnet build FarmToCity.sln
dotnet test
dotnet run --project tools/Tools.Balancer
```

Build output goes to `/artifacts/` (never committed). CI (GitHub Actions) runs `dotnet build` and `dotnet test` on every push. Unity builds are made by the owner on a MacBook Air M2 with Xcode 27.1, and tested on an iPhone 13 (ADR 0026).

## Key decisions (see `docs/decisions/` for full text)

| ADR | Decision |
| --- | --- |
| 0001 | Setting: Indian-inspired, fictional river valley. Seasons are Spring, Summer, Monsoon, Winter |
| 0002 | Real-time three clocks. Task clock in real time; 1 season = 4 real days; 1 year = 16 days; citizens age 1 life-year per season. No speed controls. 72 h away cap. A scheduler drives the sim |
| 0003 | The player is an ageless founder household (amended by 0010) |
| 0004 | The player never loses an election. The result sets the council mood |
| 0005 | Landscape only |
| 0006 | Free with one-time unlock (implementation deferred) |
| 0007 | Zero budget: CC0 art plus Blender sprites, personal builds only, no purchases (`IEntitlementService` is always unlocked) |
| 0008 | Rupee-scale "coins" (1/100 unit). Realism per season. Season contracts, costs paid upfront. Starting package (superseded by 0030) |
| 0009 | Moneylender and bank from day one. Bankruptcy is foreclosure, and the inherited plot is protected. The founder can do day labour |
| 0010 | The founder is the first worker: labour = Hours × Nutrition × (1 − Fatigue) × Health. Never an energy bar |
| 0011 | Literacy is separate from skilled graduates. The founder funds the first school. The first doctor is a migrant |
| 0012 | Price bounds come from regional trade. Inflation indexes the regional price |
| 0013 | Market clearing: demand curve, supply includes the trader, damped hourly adjustment, services are separate |
| 0014 | Yield formula adds Fertiliser and CropHealth, capped at 1.6× base |
| 0015 | Onboarding: a first session plus a guided first week |
| 0016 | Balance rule: archetype bots within 20% of each other; stress bots as exploit detectors |
| 0017 | Phase 1 runs from Homestead to the Hamlet unlock (hiring included). Code-first Unity. Dev time-travel panel |
| 0018 | `Sim.Core` is a netstandard2.1 library tested with `dotnet test` and consumed by Unity as a local package |
| 0019 | Rolling-wave specs (a phase's specs are written in its first sprint). The repository is the only memory: the session routine, `STATUS.md`, and a pull request merged to `main` after each session |
| 0020 | Definition classes live in `Sim.Core`. `Sim.Content` is only the JSON loader and validator |
| 0021 | Crop calendar follows Indian cropping seasons (radish all year). Fruit trees in yard and field-edge slots; apple on hills only, later |
| 0022 | Hiring from Homestead: day labourers from neighbouring villages, and a resident hand who needs a hut. Regulars can move in |
| 0023 | One town at a time. Save points are a crash-safety guard only, never an undo |
| 0024 | Phase 1 adds the hut store and spoilage, string tables and Indian number formatting, and an opt-in local play log |
| 0025 | Sprint mechanics: 1-week sprints, story points, MoSCoW, Definition of Ready and Done, a pull request per sprint |
| 0026 | Test device: iPhone 13 (half-frame-budget headroom). Mac: M2 Air with Xcode 27.1. Android untested for now. Latest LTS Unity, .NET 10 for tests and tools |
| 0027 | Audit corrections: specs 002 and 005 first, the ledger core in Sprint 1, Unity and a hello-iPhone build in Sprint 1, on-device determinism in the Phase 0 gate, the Hamlet-unlock feature, and later-phase coverage |
| 0028 | Hamlet at the end of the first week (Village 2–3 weeks, Township 4–6 weeks). Land priced at 8–15 seasons of an owner-operated acre's net income (ADR 0033). Bank loans can be secured by the land they buy. No leasing in Phase 1 |
| 0029 | Unity pinned to 6000.3.26f1 (6.3 LTS). Unity still compiles with `-langversion:9.0`, so the sim stays on C# 9. No upgrades mid-phase. `com.unity.pipeline` is an Editor-only dev tool |
| 0030 | Inherit 6 protected acres and the yard, 20,000 coins and a store of seed and grain. Hamlet gate: 6 acres harvested in a season at Care ≥ 0.9, a hand's house, a profitable season. 8 × 6 map. Unowned land keeps its fertility |
| 0031 | Phase 1 adds the kitchen garden, the mandi trip, buying inputs ahead (seasonal input prices; fertiliser price-controlled), minimum prices on offers, warm clothing and medicine |
| 0032 | The well waters 2 well acres (the rest rain-fed). The household eats from the store daily, credited as home use. Founder nutrition and day labour are time-weighted. P&L grouping; the Hamlet gate needs farm profit > 0. Inherited seed age 2.0 |
| 0033 | Land payback measured on an owner-operated acre (1,20,000 stays; hired acres net 2,000–5,000). Saplings priced with planting: banana 60, papaya 60, guava 180, mango 350 |
| 0034 | Sprint 3 review: mandi trip costs 0.5 h of labour; a glut drains hour by hour; every coin amount follows the price index (not loans or fertiliser); inputs in store are an asset (1200); ready crops and fruit spoil after 72 h; subsistence automatic only; Phase 0 gate checks and the spike defined |
| 0035 | Performance spike passed on the iPhone 13 (main thread p95 ≤ 7.6 ms, GPU ≤ 2.8 ms, about 60 fps, under 1% dropped frames); golden replays match on IL2CPP; the Phase 0 gate is passed |
| 0036 | Fonts may be SIL OFL (Noto Sans, Devanagari for Hindi); the low-poly Kenney landscape style is the art base; the first notification ask comes in the first session after the long crop is sown. (0035 is reserved for the spike result) |
| 0037 | Mandi cart is a tractor-trolley (2,000 kg, 1,000 hire); shade per tree (mango, guava 0.08; banana, papaya 0.03; floor 0.80); first-season pacing accepted (the first loan before hiring) |
| 0038 | Credit loopholes closed: foreclosure also seizes stored produce, selling collateral repays the bank first, a 24-live-hour final notice for every default; moneylender 6% a season; minimum loans; first playable scope (minimal HUD and Store sheet, painted placeholder sprites, Baloo 2 headings, dev builds, Phase 0 spike deleted, Blender pipeline in Sprint 6) |
| 0039 | Sprint 5 review: hands' huts on the edge of an inherited acre (at most 2); regulars counted by time hired; back pay when renewing after a lapse; orders never undo the player's crop, one action per hand per hour; stars and words for skill; a People button and a people-only away card; four people stress bots; a Blender slice in Sprint 5, every sprite rendered by the end of Sprint 6; the hand's wage 30,000 a season (day 9,375) |
| 0040 | Device review: a 2-acre yard (tile 29) and the labour quarters (tile 37) for hands' huts (supersedes 0039 §2); an order starts at once (one action per hand per hour); people go round their fields, hoe, and go home when idle; yard props and fence |
| 0041 | Sprint 6 review: a save holds everything hashed (content by id, one write path); old saves refused and kept until the Sprint 9 playtest build, migrations after; notifications from a simulated absence, ranked, no daily cap, quiet hours, hands' work per 4 h; a gear and Settings sheet; import from a list; regulars always accept; every sprite, the ground, icons and walk cycles in Blender now |
| 0042 | Weather in 1-hour spells with per-weather persistence: calm weather lasts (sun 1–2 days), severe passes (heavy rain, storm 1–2 h); seasonal shares unchanged; the forecast as runs (Sprint 7, US-184) |
| 0043 | Sprint 7 review: run lengths per season (Monsoon sun 6 h breaks, dry-season rain 4–6 h); the forecast rolled per run; *Why?* in words on every number (a ? on rows, a coverage test); the Menu (gear gone, People shortcut, ! alerts); Money and loans, founder, good, Buy inputs, mandi panels; the full away card only when something happened; text size, reduced motion, sound, haptics; 7 Kenney CC0 sounds; 25 points |
| 0044 | Sprint 7 device review: a back button on sheets opened from another sheet; sounds only for town events (no tap or sheet sounds); a *Why?* opens only from its ?; text size 80/100/125%; loan payments priced at the press; net worth kept, with the land on its own row |
| 0045 | Sprint 8 review: a one-line nudge pill per first-session step, the tour during the garden wait; a long crop on any acre; `comeBack` asks (Yes / Not now, never at night), the card as fallback; AC-11 to AC-16; Phase 1 health checks (cautious saver, random, never stuck with credit refused, recovery after foreclosure, exact ledger, bounded prices) with thresholds measured then set realistically; Sprint 8's build feature-complete Development |
| 0046 | Sprint 8 device review: veils over the whole screen (past the safe area); the *While you were away* card only after 8 h away or at the cap (`awayCardMinHours`); the sale nudge only with something the trader would buy |
| 0047 | Sprint 9 review: the owner is the only playtester; play log adds `tz`, `fromNote` and first-session steps; migrations from the RC (`minSchema` frozen at 3, a fixture save per schema); a Development build for regression and frame time, a Release build for the playtest; the Phase 1 gate split (A: solo week; B: fresh players before Launch); Sprint 9 runs 2 weeks, 10 points |
| 0048 | Playtest findings: an every-season order sows the hand's best in-season pick (profit per season on the field's soil and moisture) when its crop is out of season, or always with "Let him choose"; a paid hand is seen tending every growing field; labourers are hired from a growing field until its harvest, which they bring in; save schema 4 |
| 0049 | Sprint 10 review: every order acts at once (`orderActionsPerHour` 6; Care limits what a hand manages); labourers hired from People till the crops growing now are in, harvesting ripe fields with no hand's order; crops drawn below people |
| 0050 | The hand sows the best crop he can pay for: his choice skips crops whose seed cash can't cover, and an order's own crop falls back to his best affordable pick; he waits only when nothing in season is affordable |
| 0051 | Compost +6 fertility a season (more than any crop takes) and the soil under a composted crop dries 20% more slowly; fertility shown to one decimal; the sow sheet shows the soil before and after each crop; chemical unchanged |
| 0052 | The map grows by era: 16 × 12 acres in Phase 2 with the 8 × 6 farm unchanged at columns 2–9, rows 3–8 (tile ids remapped by a save migration); about 60–80 acres buyable; natural terrain past the edge; the camera opens on the farm |
| 0053 | The village layout (north = river): the centre east of the farm, the commons north-west, neighbours for sale or not for sale yet; 27 road acres (district road, village lane, farm lane taking `s5`), never for sale |
| 0054 | The trader's 4 t truck from 6:00 to midnight (or till full); its price and per-crop limits set at 6:00; a total load; selling refused while away; the stall paused midnight to 6:00 |
| 0055 | Two supermarket orders at 6:00 (regional + 15–30%, by midnight, no penalty), fresh today only, the hand fills them first and counts their price; rice and potato in 15 h |
| 0056 | Buffalo and cows in a 4-animal shed: co-op milk by quality; green fodder cut and carried, straw stack, feed room; fed automatically, no deaths from hunger in Phase 2; dung to compost; lactation, vaccination, 1.5 h a day an animal |
| 0057 | Phase 2 specs (010–019) reviewed two per sprint, each a sprint before its code; the enhancements first (010 and 011 coded in Sprint 11 with a device build, 012 in Sprint 12) |
| 0058 | Sprint 11 balance: an order is 0.5–1 acre's sowing (orders 14.8% of sales); the 15-hour crops' selling gain accepted (season farm profit median 46k); the long crop is ≥ 12 h; the bots play after the first session; the truckAway page also opens on a Store visit with the truck away |
| 0059 | Sprint 11 device review (passed; frame time p95 5.37 ms): one row per supermarket order with a Delivered tag; the Store in sections with picture buttons; a hand's field order starts with the sow sheet (fertiliser choice and every figure) |
| 0060 | Spec 012 gaps: a game day is 22.8 real days for animals (182 L a buffalo); cattle from the Hamlet; berseem and sorghum cut daily, spare fodder sold standing, shortfall bought; straw stack, feed room, compost pit as places; co-op pays by fat at 6:00; automatic vaccination and vet; animals at cost, dairy loan at 0.75; nothing dies |
| 0061 | The village is the town: its places serve the player's citizens, its old families aren't counted ("Your people"); the centre redrawn on the same acres (a middle street, buildings from all sides facing their street, clusters, square, pond and temple); the dairy co-op gets a building |
| 0062 | A living valley: neighbours' acres vary by soil (scrub, fallow, mustard, sugarcane, groves), farmsteads only on not-for-sale acres and the edge; a hedge-and-wall boundary with a gate and corner stones round everything you own (no desaturation) |
| 0063 | Spec 014: your people live on your land (huts, family houses from the Hamlet at 60,000, rent 6,000); a life-year a season, only elders die, gently; Engel budgets; needs layers 40/30/20/10; families fill empty houses, a hand's family's house is part of his pay; outside work at the day-labour rate on half the days; a bounded family tree; the town's food adds to local demand; every path achievable |
| 0064 | The cattle acre: the founder inherits one barren acre beside the yard (tile 104, `home_d`), never farmed, sold or seized; the shed goes there from the Hamlet, drawn spread out like a village baada; map layouts never cramped, previewed to the owner first; save schema 8 |
| 0065 | The village's shops and public buildings stand where their fronts face the camera (main street: council hall, grocer, tea stall; side lane: dealer, chemist, dairy co-op; the temple faces the pond); the dairy co-op on acre 108 by the farm's lane |
| 0066 | Fodder stands never keep labourers hired (they're cut each morning with the animals' work; labourers come for a harvest); the People sheet in the Store's layout (picture buttons to hire, build a hut, send word; pictured rows; Hire and Send word sheets) |
| 0067 | Two family houses an acre (was 4: cramped on the map), sharing a wall at the back of the acre with open ground in front; townspeople, children and elders with walk cycles, card portraits, population and happiness icons |
| 0068 | Spec 016: each business keeps its own books (capital, drawings, equity in net worth; own wheat and milk at market price); the flour mill (Hamlet) grinds the area's grain (about 300 people, 4 a kg) and mills your wheat; the bakery and a dairy (paneer, curd, ghee) from the Village; a town hire or you run it; any owned acre; cash plus a guaranteed bank loan; research, plan, plan vs actual; overdraft, closing at 60% |
| 0069 | Spec 015: one hiring board (farm hand, miller, baker, dairyman; townspeople known, migrants as ranges); interview and a 300-coin reference check; offer at the asked wage, 10% less or 10% more; output = skill × motivation × (1 − fatigue), neutral by default; long days at 1.5×; a crafting skill; courses +10 for 6,000; four more traits; five HR events; employer reputation |
| 0070 | Marriage is patrilocal: a son's bride moves in (into a free family house if one stands empty), a daughter marries into her husband's village and is kept in the tree while a parent lives |
| 0071 | The home loan: the bank counts family houses at 0.75 of their book value; measured, the competent bot has 5 households by day 16 on it (the saver day 33); spec 018 sets the Village from these; town happiness 65–85 until businesses; the random bot isn't measured for the town |
| 0072 | The flour mill's targets kept realistic: a hired miller leaves 0–8,000 a season (more with more of your own wheat); run by you 25,000–35,000, payback 3–6 seasons; the plan shows both |
| 0073 | The mill's catchment 350 people plus your town (from US-117's bots: at 260 a hired mill lost money and running it yourself paid less than day labour): hired 5,000–10,000 a season, run by you 30,000–40,000, payback 3–4; the plan values your hours (supersedes 0072's ranges) |
| 0074 | Spec 017: a vegetable stall, a tea stall and a sweet shop from the Village, on a rented square pitch (3,000 a season) or your own acre; customers share demand by an additive score (price, quality, stars, distance, wait); stars 0–5 moving a tenth a day; the village's grocer and tea stall never close; newcomers on a gap or rich prices; the mill chooses against the chakki the same way |
| 0075 | Spec 013: the irrigation ladder (buckets, a pump on the well for 4 acres, canal channels dry in Summer, drip half subsidised on 2 acres, a borewell for 6 more); an ox plough with bullocks in the shed (Hamlet) and a tractor (Village) to buy, lease or hire, a harvester hired by the acre; breakdowns 0.5% or 4% a day; whole-acre orchards at real densities with half-yield intercrops; a dry-goods granary (10 t, a fifth of the spoilage) |
| 0076 | Sprint 15 plan: 2 weeks (36 points + Next era); save schema 11 in Sprint 15 (workers, posts, reputation, crafting skill, the bakery's and dairy's settings), 12 in Sprint 16 (shops, farm); a developer-panel Next era (Development builds only); Sprint 16 re-pointed to 37 |
| 0077 | Spec 018: the Village gate is 5 households, a business with 2 profitable seasons and the school (no market stall); the school on its plot from the Hamlet (1,05,000; the district pays the teacher, 2,000 upkeep); adult classes 3,000 (14+); literate adults earn 10% more outside; ~24 Codex cards by toast; three village advisors, capped; the treasury with the Township; Phase 2 gate: the Village in 2–3 weeks |
| 0078 | Spec 019: your season report as three statements like the mill's Books, each line beside the same season last year; the hub inside Money and loans redesigned in the Store/People layout (trend, pie, debt to assets, emergency fund, passive income); a co-op savings deposit at 1.5% a season, never collateral; five milestones, no rewards; Your year after Winter; Your people on the report |
| 0079 | The bakery: the shops buy 0.01 kg of pav a person a day (about a pav every 3 days); the starting mix and the plan add the trader's rusk; the plan prices atta as it would really cost; targets hired 15,000–25,000 a season (payback 5–8), run by you 45,000–60,000 |
| 0080 | The dairy: neighbours' milk on by default (co-op price + 2); paneer 380 and the trader takes 60 kg a day; the starting mix leaves out what loses (ghee from whole milk); a day's inputs as working capital; targets hired 10,000–20,000 a season, run by you 35,000–50,000 |
| 0081 | US-112 review: a contract renews at its current wage and a worker whose skill has grown asks for a raise (US-114); every citizen's farming skill drawn at birth; the hiring board's build notes accepted |

## Current status

See `docs/planning/STATUS.md`. It is the single source of truth for where the work stands.
