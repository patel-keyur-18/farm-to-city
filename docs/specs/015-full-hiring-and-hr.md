# 015. Full hiring and HR

- Status: Approved 2026-10-25 (ADR 0069); every gap closed with the owner
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Citizens and workforce (Hiring flow, Wages, Productivity, Hours and shifts, Training, HR events, Letting people go)
- Amends: spec 006 (one hiring board for every role; references; negotiation; motivation and fatigue in a worker's output; four more traits; employer reputation), spec 016 §3.6 (the business's worker is hired on the board from Sprint 15), spec 014 (a worker's household happiness feeds motivation)
- Related ADRs: 0008 (season contracts paid upfront), 0022 (hiring from Homestead), 0039 (skill as stars and words), 0041 (regulars always accept), 0063 (households), 0068 (businesses)
- Depends on specs: 005, 006, 014, 016

## 1. Goal

Hiring and keeping people feels real: the player posts a job, weighs candidates whose skills are only partly known, negotiates a wage, and then sees how fair pay, sensible hours and training make a worker better. Raise requests and offers from elsewhere ask the player to choose.

**Lesson:** hiring under uncertainty (interviews and references); a fair wage keeps good people (paying below the market costs more in the end); output is skill × motivation × fitness, not hours alone; training raises a worker's worth to you and to others.

## 2. Scope

**In:**
- One hiring board for every role: farm hand, miller, baker, dairyman.
- Candidates: your own townspeople without a town job, migrants, and Phase 1's regulars.
- Reference checks, negotiation, probation.
- Motivation, fatigue and overtime in a worker's output.
- Training: on the job and paid courses.
- Four more traits: creative, frugal, sociable, perfectionist.
- HR events with choices; employer reputation.

**Out** (and where it goes instead):
- Supervisors and span of control, benefits (meals, health cover, bonuses, paid leave), minimum wage policy, worker associations, petitions: Phase 3.
- Managers for businesses: Phase 3 (F12.6).
- Sponsoring a citizen's education for a work bond: spec 018 (the school).
- Night shifts: Phase 3 (no business runs at night in Phase 2).

## 3. Rules and formulas

Every coin amount is a year-0 price that follows the price index (ADR 0034). **Neutral by default:** a worker at the market wage, normal hours and a content household has motivation 1 and no fatigue, so today's hands work exactly as in spec 006. New applicants draw from ten traits, so the golden replays are regenerated once (a content change); existing hands keep theirs.

### 3.1 Roles and the hiring board (§10 G1)

| Role | Works at | Market wage a season (skill 50) | Skill used | Housing |
| --- | --- | --- | --- | --- |
| Farm hand | Your fields (spec 006) | 30,000 | Farming | A hand's hut, or a family house |
| Miller | The flour mill | 27,000 | Crafting | Lives in town or commutes |
| Baker | The bakery | 30,000 | Crafting | Lives in town or commutes |
| Dairyman | The dairy | 27,000 | Farming | Lives in town or commutes |

- **Post a job** (People → Hire → Post a job): the role, the wage (market, or above it as spec 006's above-market post), and for a business its hours. Costs spec 006's post fee (200). A post lasts 4 days or until filled.
- **Who applies:**
  - Your townspeople of working age without a town job. You know them, so their skill and both traits are shown exactly.
  - Migrants, as spec 006's applicants: the skill as a range and one trait. A migrant farm hand needs an empty hut or family house. A business worker from outside commutes daily from a neighbouring village and isn't a citizen.
  - Regulars (spec 006 §3.3), as now.
- **Pace:** applicants come every 12 ± 6 h while a post is open (twice as fast above the market). Walk-ins for a farm hand still come while a hut stands empty (spec 006).
- **Spec 016's stopgap ends:** from Sprint 15 a business's worker is hired here, or you run it yourself.

### 3.2 Knowing a candidate (§10 G2)

| Step | What it shows | Cost |
| --- | --- | --- |
| The application | A migrant's skill as a range (±15) and the first trait | Free |
| Interview | The second trait (spec 006) | Free, once |
| Reference check | The skill narrowed to ±5, and how they left their last job: "was let go" when either trait is lazy or careless, else "left on good terms" (so it hints at a hidden trait) | 300 coins and 12 hours' wait; once |

### 3.3 The offer and negotiation (§10 G3)

| Rule | Behaviour | Default |
| --- | --- | --- |
| Asked wage | Spec 006's formula by role: the market wage × (0.80 + 0.40 × skill ÷ 100) × the price index | |
| Offer | At the asked wage, 10% below it, or 10% above it | |
| Answer | Accepts with `acceptChance` (0.85) at the asked wage, 0.95 above it, 0.40 below it. A refusal of a low offer becomes a counter at the asked wage, once; any other refusal ends it (the applicant leaves). Regulars always accept (ADR 0041) | Seeded, stream Applicants |
| Townspeople | Always accept at the asked wage or above (they live here); 0.6 below it | |
| Wage memory | A worker hired below their asked wage remembers it: motivation is lower (§3.4) until a raise or a renewal pays the asked wage | |
| Renewal (ADR 0081) | A contract renews at its current wage. When the asked wage for the worker's skill has risen above it (on the job, a course, or hired low), they may ask for a raise (§3.7); a loyal worker never asks | |

### 3.4 Output: skill, motivation, fatigue (§10 G4)

```
Output = Skill factor × Motivation × (1 − Fatigue)
```

- **Skill factor:** for a farm hand, spec 006's `productivityBase + productivityPerSkill × skill ÷ 100` times the trait factors. For a business worker, the same form on its rate.
- **Two skills:** from Phase 2 a citizen has a farming skill and a crafting skill, each drawn as spec 006 draws farming (existing citizens get crafting drawn at the save migration). A role reads its own skill (§3.1), so nobody works in the wrong skill and RoleFit is 1; it returns with Phase 3's educated roles.
- **Tools:** 1 in Phase 2; they come with spec 013's equipment.

| Factor | Formula | Bounds |
| --- | --- | --- |
| Motivation | 1 + fairness + home + the HR events' effects (§3.7). Fairness: +0.05 hired 10% above the asked wage; −0.10 hired 10% below it, until a renewal pays the asked wage; 0 otherwise (a loyal hand keeping his wage is content). Home: −0.003 for each point the household's happiness is below 60, else 0 | 0.7–1.2 |
| Fatigue | Rises 0.05 a day worked over 8 hours a day (overtime), falls 0.10 a day at 8 or fewer; never above 0.5 | 0–0.5 |
| Overtime | A business can stay open up to 12 hours a day: the hours over 8 are paid 1.5 × the hourly wage, and fatigue builds. A farm hand can be set to "long days" (10 h, same rule) | |

A fair hire with a household at 60 or above has motivation exactly 1, so today's hands (alone in a hut, about 84) work exactly as before.

### 3.5 Training (§10 G5)

| Way | Effect | Cost |
| --- | --- | --- |
| On the job | +3 skill a season (spec 006), ×1.5 ambitious | Nothing |
| A course | +10 skill, in the worker's role skill | 6,000 coins; the worker is away 1 day (no work, wage still paid; ADR 0082) |
| Worth more | A trained worker asks more at the next renewal (the asked wage follows skill) and draws more poaching offers (§3.7) | |

### 3.6 Four more traits (§10 G6)

| Trait | Effect |
| --- | --- |
| Creative | A business's batches sell for 5% more (a better loaf, a finer atta); for a farm hand, no effect in Phase 2 (like steady) |
| Frugal | A business's power, fuel and supplies cost 10% less; the household saves more (its leisure share half saved) |
| Sociable | Every colleague at the same place +0.05 motivation; spec 017's shops sell more later |
| Perfectionist | No spoiled batch (§3.7's bad batch) and 5% slower |

They join spec 006's six, drawn evenly from all ten. A pair never contradicts: never hardworking with lazy, or careless with perfectionist (the second draw skips the first's opposite).

### 3.7 HR events (§10 G7)

Events that need a decision wait for the player (ADR 0002): an alert, a card with choices, and no deadline while away; until it's answered the worker carries on as they are (an offer from elsewhere waits too). At most one event a worker a season, seeded (stream People).

| Event | When | Choices | Consequences |
| --- | --- | --- | --- |
| Raise request | At a close, a worker paid below their asked wage (skill grew, or hired low) asks, 50% a close | Pay the asked wage from now / Not now | Yes: wage and motivation up. No: motivation −0.1 for the rest of the season, and still underpaid at the next close they leave instead of renewing, whatever their skill (ADR 0082) |
| Poaching offer | A worker of skill 60+ gets an offer from the next village (5% a season, ×2 if under-paid) | Match it (+15% wage) / Let them go | Match: they stay, motivation +0.05. Let go: they leave at the close, no severance, the job opens on the board |
| Sick leave | 2% a worker a season (×2 if fatigue is over 0.3) | Paid leave (1 day off, wage as usual; ADR 0082) / Unpaid | Paid: reputation +2, motivation +0.05. Unpaid: the days' wage refunded, motivation −0.1 |
| Family emergency | 2% a worker a season | An advance of a quarter's wage (paid back from the next renewal) / No | Yes: reputation +3, motivation +0.1 for a season. No: motivation −0.1 |
| A bad batch | 3% a business a season (never with a perfectionist) | Throw it away / Sell it cheap | Throw: the batch's inputs lost. Sell: half its value, reputation −2 |

Conflicts between workers, accidents and petitions come in Phase 3, when places have several workers.

### 3.8 Employer reputation (§10 G8)

A score 0–100 (start 50) shown on the People sheet with its *Why?*.
- **Up:** paying the asked wage or above at renewal (+1 a worker a season); paid sick leave and advances (+2, +3); severance paid when letting go after probation (+1).
- **Down:** a hire below the asked wage (−2); a refused raise (−2); letting go in a worker's first season (−1); a lapse (−3).
- **Effects:** at 80 or above, acceptance +10% and above-market posts bring skill +5 more; at 20 or below, acceptance −10% and applicants come more slowly (×0.75 pace).

### 3.9 Letting go (§10 G9)

As spec 006: no severance in probation (the first season), then 10% of the season wage. Colleagues at the same place lose 0.05 motivation for a season; reputation as §3.8. A business left without a worker stops until one is hired, or you run it.

### 3.10 Processing order

- Hourly, system Labour: applicants arrive for open posts (live only), reference checks finish, then work as spec 006 and 016 with each worker's output.
- Daily, system Labour: fatigue moves.
- Seasonal, inside spec 005's settlement step 6: renewals (asked wages, raise requests, poaching drawn), training finished, motivation set for the season.

## 4. Data

**`people.json`:** roles (id, wage, skill, housing), the reference check (cost, hours, range), the offer factors and chances, the motivation weights and bounds, fatigue rates and cap, overtime, courses (cost, days, gain), the four traits' effects and the incompatible pairs, event chances and effects, reputation steps and effects.

**State (save schema 11):**
- `Citizen`: a crafting skill (drawn for existing citizens at the migration, stream Citizens).
- `Worker`: the role and place, the wage memory, motivation, fatigue, course end, the season's event.
- `Applicant`: role, reference result, offer state.
- `JobPost`: role and place.
- `PeopleState`: employer reputation.

## 5. Commands and events

| Command | Parameters | Validation | Effect |
| --- | --- | --- | --- |
| `PostJob` | role, place, aboveMarket | `NoHousing` (farm hand), `UnknownBusiness`, `AlreadyPosted` | §3.1 |
| `CheckReference` | applicant | `AlreadyChecked`, `InsufficientFunds` | §3.2 |
| `OfferContract` | applicant, offer (asked / low / high) | spec 006's | §3.3 |
| `SendOnCourse` | worker | `Busy`, `InsufficientFunds` | §3.5 |
| `SetLongDays` | worker or business, on | | §3.4 |
| `AnswerHrEvent` | event, choice | `UnknownEvent` | §3.7 |

| Event | Payload | Consumers |
| --- | --- | --- |
| `ReferenceChecked`, `CounterOffer` | applicant | People sheet |
| `RaiseRequested`, `PoachingOffer`, `SickLeave`, `FamilyEmergency`, `BadBatch` | worker | Alerts, a card, notifications |
| `CourseFinished` | worker, skill | Away card |
| `ReputationChanged` | score | People sheet |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects | Reversibility |
| --- | --- | --- | --- | --- |
| Hire below the asked wage | Saves 10% | Lower motivation, a raise request, poaching | Reputation −2 | Pay the asked wage at renewal |
| Check a reference | 300, 12 h | A surer hire | | — |
| Send on a course | 6,000, 1 day away | +10 skill: more output | Asks more at renewal; poaching likelier | — |
| Long days | 1.5 × wage for the extra hours | More output now | Fatigue; sick leave likelier | Turn off |
| Match a poaching offer | +15% wage | Keeps a skilled worker | | — |

## 7. UI

```
People → Hire
| Post a job        [Farm hand] [Miller] [Baker] [Dairyman]             |
| Applicants for Miller (post open 3 d)                                   |
|  Usha (35) · from your town · Capable ★★★ · Steady, Frugal   [Offer ▾]  |
|  Vijay · migrant · Beginner to Skilled ★★☆☆ · Hardworking · ?  [Interview] [Reference 300] [Offer ▾] |
Offer ▾:  At 27,000 (asked) · 24,300 (10% less: may refuse) · 29,700 (10% more: surer)

A worker's card: Output "Works well" ★★★★ [?]: skill, motivation (fair pay, a happy home), fatigue
Employer reputation 62 [?] on the People sheet
HR event card: "Manoj asks for a raise to 30,000 (you pay 27,000)."  [Pay 30,000 from now] [Not now]
```

Output is shown in words and stars, never as a factor (ADR 0039 §7); every number has a *Why?*.

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | A post brings applicants for its role every 12 ± 6 h; townspeople without a town job apply with known skill and traits | `HiringTests.Board_Applicants` |
| AC-2 | A reference check costs 300, finishes after 12 h and narrows the skill to ±5 | `HiringTests.Reference` |
| AC-3 | A low offer is accepted at 0.40, else a counter at the asked wage once; a high offer at 0.95 | `HiringTests.Negotiation` |
| AC-4 | Output = skill factor × motivation × (1 − fatigue); a fair hire in a content home has motivation exactly 1, so an existing hand works as before; the golden replays are unchanged (their content has no people) | `WorkerTests.Output_Neutral`, `ReplayTests.*` |
| AC-5 | Long days raise output and fatigue; fatigue falls at normal hours; overtime pays 1.5× | `WorkerTests.Fatigue_Overtime` |
| AC-6 | A course adds 10 skill after a day away (ADR 0082), paid; the next renewal asks more | `WorkerTests.Course` |
| AC-7 | The four traits act as §3.6; no contradictory pairs are drawn | `WorkerTests.Traits` |
| AC-8 | Each HR event follows its chance, waits for the player, and applies its choice's effects | `HrEventTests.Choices` |
| AC-9 | Reputation moves by §3.8's steps and changes acceptance and applicants as stated | `HrEventTests.Reputation` |
| AC-10 | A schema-10 save loads into 11 (workers neutral, reputation 50, a crafting skill drawn for each citizen) and round-trips | `MigrationTests.Schema10_Workers` |

## 9. Balancing hooks

| Metric | Expected | Tolerance |
| --- | --- | --- |
| A competent bot paying fair wages | Motivation 1.0–1.1, output as Phase 1 | No farm-profit change over 2%. `GateTests.Hr` (16 seeds, 12 seasons, ADR 0082): motivation 1.000, farm profit 5,61,247 vs Phase 1's output 5,64,114 (−0.5%) |
| A bot hiring everyone 10% low | Saves 10% of wages and loses more than that in output and quits within 2 seasons | The lesson holds. `GateTests.Hr`: wages −21%, farm profit −3.7% against the fair bot, hands gone after 2.6 seasons (median) |
| HR events | 0–2 a season for a town of 3 workers | Not a nag. `GateTests.Hr`: 0.19 a worker a season (0.56 for 3) |

## 10. Open questions

- **G1. Roles and the board.** **Closed (owner, 2026-10-25):** one board for farm hand, miller, baker and dairyman (30,000, 27,000, 30,000, 27,000 at skill 50); your townspeople without a town job apply with skill and traits known, migrants as now, regulars as now; a business worker from outside commutes and isn't a citizen; spec 016's stopgap ends in Sprint 15 (§3.1).
- **G2. Knowing a candidate.** **Closed (owner, 2026-10-25):** the application shows the skill ±15 and one trait; a free interview the second trait; a reference check (300, 12 h) narrows the skill to ±5 and says how they left their last job ("was let go" when a trait is lazy or careless); townspeople are known (§3.2).
- **G3. Negotiation.** **Closed (owner, 2026-10-25):** offer at the asked wage (0.85), 10% less (0.40, then one counter at the asked wage; a low hire is remembered in motivation and reputation) or 10% more (0.95); townspeople always accept at the asked wage or above, 0.6 below; regulars always (§3.3).
- **G4. Output.** **Closed (owner, 2026-10-25):** skill × motivation (0.7–1.2: +0.05 hired above, −0.10 hired below until paid the asked wage, lower when the family is under 60, the events' effects) × (1 − fatigue: +0.05 a long day, −0.10 a normal one, at most 0.5); long days to 12 h with the extra hours at 1.5×; tools with spec 013; exactly neutral for a fair hire in a content home (§3.4). Review (Claude, same day): citizens get a crafting skill beside farming and each role reads its own, so the "wrong skill at 0.8" never arises in Phase 2.
- **G5. Training.** **Closed (owner, 2026-10-25):** on the job +3 a season (×1.5 ambitious); a course +10 skill in the role for 6,000, 2 days away and paid (1 day since ADR 0082); a trained worker asks more at renewal and draws more poaching offers. Example: a miller from skill 40 to 50 works about 5% faster and asks about 1,100 more a season (the owner was told about 900; the formula gives 25,920 → 27,000) (§3.5).
- **G6. Four traits.** **Closed (owner, 2026-10-25):** creative (a business's goods sell 5% higher; no farm effect yet), frugal (−10% power, fuel and supplies; the household saves half its leisure), sociable (+0.05 motivation to colleagues; shops later), perfectionist (no bad batch, 5% slower); never hardworking with lazy or careless with perfectionist; all ten drawn evenly (§3.6).
- **G7. HR events.** **Closed (owner, 2026-10-25):** raise request, an offer from elsewhere (skill 60+, match +15% or let go), sick leave (paid or unpaid), family emergency (an advance of a quarter's wage), a bad batch (throw away or sell at half); at most one a worker a season; each waits for the player, the worker carrying on meanwhile; about 0–2 a season for 3 workers; conflicts, accidents and petitions in Phase 3 (§3.7).
- **G8. Employer reputation.** **Closed (owner, 2026-10-25):** 0–100 (start 50) shown with its *Why?*; up with the asked wage or more at renewal (+1 a worker), paid sick leave (+2), an advance (+3), severance paid (+1); down with a low hire (−2), a refused raise (−2), letting go in probation (−1), a lapse (−3); at 80+ acceptance +10% and better above-market applicants, at 20− acceptance −10% and applicants slower (§3.8).
- **G9. What stays out.** **Closed (owner, 2026-10-25):** supervisors and span of control, benefits and minimum wage policy (with the treasury), managers, night shifts in Phase 3; education bonds with the school (spec 018); a family house as part of a hand's pay already exists (§2).

### US-112 build notes (2026-10-28, for the owner's review)

Choices made while building where the spec was silent, each to keep it realistic and clear on screen:
- **When townspeople apply:** the moment you send word (they live here), the most skilled in the job's skill first, listed above migrants. Townspeople and people from outside each have up to `maxApplicants` (3) waiting, so neither crowds the other out. Townspeople answering an above-usual post are offered its wage.
- **Pace:** a business's post brings a migrant every 12 ± 6 h at the usual wage, twice as fast above it (AC-1); a farm hand's post keeps spec 006's pace (walk-ins every 12 ± 6 h, a post twice as often).
- **Roles' data:** the farm hand's wage stays in `people.json` (`hand.marketWage`) and each business role's wage and skill in `businesses.json` (`worker`, `workerSkill`, `wage`), so no wage is written twice; `people.json` gains `hiring` (the reference, offer factors and chances, reputation's start).
- **A migrant farm hand** still needs an empty hut; your townspeople need none (their family house counts).
- **Letting a business's worker go** (§3.9 named it, no command did): `LetWorkerGo` from the business's card, with a confirm; the season's wage was paid upfront from the business and isn't refunded; severance 10% after the first season. With nobody working, **Run it** (`RunBusinessYourself`) or hire again.
- **Renewal:** a worker was renewed at each close at the asked wage for their skill, so §3.7's raise request had almost nobody to come from. **Settled (owner, 2026-10-28, ADR 0081):** a contract renews at its current wage, and a worker whose asked wage has risen asks for a raise (US-114).
- **Not yet in play:** reputation is stored (50) but moves with US-114; motivation and output with US-113.

**Accepted (owner, 2026-10-28, ADR 0081).** Also from that review: every citizen's farming skill is drawn at birth (children no longer grow up at 0).

### US-113 build notes (2026-10-28, for the owner's review)

- **A business worker's skill factor is measured against a skill-50 worker** (the market wage's skill): output ÷ (0.70 + 0.50 × 0.5). So a typical hire works at the business's rate, and the rates, the plan and ADR 0073/0079/0080's targets stay a typical hire's; skill 70 works 5% faster, skill 30 5% slower; tiredness and motivation come on top. The literal form (no division) would have made every typical hire 5% slower than the rate and moved all three businesses' targets. A farm hand's hours are spec 006's exactly, as before.
- **A business's long day is its open hours, not a separate switch.** Spec 016 already lets you set the hours (8–18, 8–14, or a long day). Hours past the village's usual 8:00–18:00 are overtime: each paid at time and a half of the worker's hourly wage (the season's wage over 4 days × 10 usual hours) as it's worked, from the business's cash; past its overdraft, the hour isn't worked. The worker tires on such days. The long-day choice is now **7–19** (12 hours, §3.4's cap; it was 6–20, 14 hours), and `SetBusiness` refuses more than 12. A farm hand's long days are a switch on their card (10 hours), each day's overtime paid each morning upfront; short of cash, they stop with a note on the away card.
- **The business's usual day is 10 hours, not §3.4's 8:** the village's mill, bakery and dairy keep 8:00–18:00 (with a meal break), and their wages are for that day. Counting overtime from 8 hours would have made every business tire its worker from day one, against "neutral by default".
- **Courses:** paid by whoever employs them (you for a hand, the business for its worker), refused while away on one. While away, a hand's orders wait and a business stands idle, wages still paid.
- **The trait sentences are in words** ("a little keener", "a tenth less"): the numbers live in the output's *Why?*, which shows each part (skill and traits, fair pay, their home, colleagues, motivation, tiredness) against a typical worker.
- **Frugal at home:** a household with a frugal adult saves half its leisure money.
- **The golden replays didn't change:** their frozen content has no people, so drawing from ten traits touches none of them. AC-4's `ReplayTests.*` pass unchanged.
- **Not yet in play:** HR events' effects on motivation (US-114); the bad batch the perfectionist avoids (US-114).

**Accepted (owner, 2026-10-29).**

### US-114 build notes (2026-10-29, for the owner's review)

- **Renewal (ADR 0081) is in:** a contract renews at its current wage; an advance is repaid from it; a worker who took an offer from elsewhere leaves at the close instead.
- **"Underpaid" means more than 5% below what they'd ask** (`hrEvents.raiseGap`). A season's skill growth alone (+3, about 1.2% on the wage) doesn't make a raise request or count against a fair renewal; a course, a few seasons' growth, inflation or a low hire does. Without this, nearly every worker asked for a small raise nearly every close (against §9's "not a nag"), and a fair employer never earned the +1 for a fair renewal.
- **An offer from elsewhere is 15% above what their skill is worth** (the asked wage), and only comes when that's above what you pay. Matching it pays it. Taken literally as "+15% on your wage", a matched worker got another +15% at the next offer, compounding without limit (seen in a bot run: three matches in six seasons).
- **Sick leave starts at once** (a day off since ADR 0082, no work, the business idle); the choice is only whether those days are paid. "The worker carries on" can't hold for someone who's ill.
- **A bad batch comes with a business's first batch of the day** (the mill's first grinding of your wheat, a recipe's first batch), before anything is sold: the mill sells its atta to the truck every hour, so at any other moment there would rarely be a batch to spoil. It sets aside up to a day's mix (an hour's grinding at the mill) of the product it holds most of.
- **A dairy takes no milk on a day nobody can work it** (its dairyman sick or on a course): the co-op takes it. Before, a sick dairyman's two days of neighbours' milk spoiled (a bot season lost 1,09,014).
- **Each event shows as a card when it happens and as an alert until answered** (the alert opens a sheet with the two choices); the card says what yes costs now and from whose cash; a notification while away ("Geeta needs a word with you").
- **Reputation** shows on the People sheet in words and its score ("Fair (52)") with its *Why?*. A lapse is a hand's unpaid renewal or a business's missed wages. Selling a bad batch cheap counts −2 as §3.7 says.
- **The bots answer as a fair employer:** yes to everything they can pay (ADR 0082).
- **Random draws use their own stream** (`HrEvents`), not People, so no existing draw moves.
- **Balance flags (settled, ADR 0082):** on 16 seeds the bakery is inside ADR 0079's targets (hired 18,240 a season, payback 7.1; on a loan 18,849); the 13,500 seen at US-114 was 8 seeds and the bot refusing raises.
- **Settled at the balance step (owner, 2026-10-29, ADR 0082):** sick leave and a course take 1 day, not 2 (half a season was out of proportion); a refused raise loses the worker at the next close; the bots answer as a fair employer; the bakery holds ADR 0079's targets and the dairy's are widened.

## 11. Cut first

1. Reference checks (interview only).
2. A bad batch.
3. Employer reputation (events' effects on motivation only).
4. Long days.
