# 0172. The ad hoc phase before Phase 4 (Phase 3.5): a businesses hub, supply between businesses, UI fixes, two towns and a graphics redesign; time travel dropped

- Status: Accepted (the owner approved the plan, 2026-11-25)
- Date: 2026-11-25
- Deciders: Keyur Patel
- Related ADRs: 0002, 0019, 0023, 0025, 0036, 0043, 0068, 0091, 0093, 0094, 0158, 0170, 0171

## Context

Phase 3 is closed (ADR 0171) and Phase 4 is not started. The owner asked for one ad hoc phase first, tracked under the GitHub milestone `Phase 3.5 · Ad hoc` (#7). The owner's brief: the balance and the finances are right and stay as they are; the work is the game's UX. Seven ideas came in, then two screenshots with broken UI. Each idea was checked against the code at `main` (`5e5a4301`) before planning, and every gap was closed with the owner one per message, each with a recommendation.

## What the code at `main` already does

| Idea | Found |
| --- | --- |
| 1. One business run by you | Not enforced. `RunBusinessYourself` checks only that the business has no worker; `FoundBusiness(runYourself: true)` checks nothing. `BusinessRules.FounderHours` caps your hours at your day, but no business's output depends on them, so two run by you both work at full output: free labour. |
| 2. A businesses button | Menu → Businesses already lists yours with their cash (`BusinessesSheet`); each card has *Take out* with a sheet of amounts (`BusinessCashSheet`, `TakeCash`). No HUD button. |
| 3. Collect all | Always on screen, greyed when nothing is ready (`HudPresenter`, `ReadyCount`). |
| 4. Two towns | ADR 0023 chose one town at a time (it rejected "up to 3 towns" to keep saves from becoming an undo). Saves live in one folder (`GameRoot.Saves`). |
| 5. +1 hour time travel | The dev panel's `DebugAdvance` moves the game clock ahead of the phone's; since ADR 0170 a reload then waits for the phone ("clock ahead"), so a press followed by a restart freezes the game for up to an hour. The same flaw is in the dev panel. ADR 0002 says no speed controls. |
| 6. Graphics | 605 Blender sprites (people 124 frames, icons 76, village 64, crops 55, trees 23, ground 22, ...) plus what is drawn in code; imported uncompressed (max 2048 px); the GPU last measured in Sprint 16 (9.21 ms of 11 ms, ADR 0094). |
| 7. Supply between businesses | Already automatic: the vegetable stall, the kirana and the restaurant take your store's produce first (`ShopRules.FromStore`) and your businesses' products (`FromYours`), the bakery your mill's atta, the dairy and the tea stall your herd's milk (`MilkIn`), the mill your wheat up to its *a day* (`OwnPerDay`); the rest at the market when *buy at the market* is on. Missing: a cap on what goes from you, a view of it, and a hand growing for the stall. |
| Screenshots | The Hiring board's 7 role tabs run past the sheet (`UiKit.MakeButton` sets `minWidth` 260 on every button, `TabStrip` doesn't wrap or scroll). The People badge's text wraps: "11" shows as a "1" in the badge and a white "1" beside the person (a 54-unit circle). |

## Decisions (the owner, one per message, 2026-11-25)

1. **One business run by you.** A second is refused ("You already run the mill: hire someone"). A town saved with two or more run by you gets a card at load to pick the one you keep running; the others go to *Nobody yet* until you hire. The card has no close button.
2. **The Businesses button.** A fourth round button left of Orders, the same look, shown from your first business, with a ! badge when a business needs you. Menu → Businesses stays.
3. **The hub's rows.** At the top: the total profit this season so far and the total cash across your businesses. Each row: picture, name, status, who runs it, profit this season so far, cash, stars where it has them, a ! when it needs you; *Take out* (the existing amounts sheet, Back returns to the hub) and *See* (its card). Every number with a *Why?*.
4. **Two towns: one player, two strategies.** Settings are shared; notifications come from both towns, each naming its town; each town has its own save, clock, play log and away card.
5. **The town picker.** Settings → *Your towns*: a row per town (name, era, day, net worth), *Switch* on the other one behind a confirm card, *Start a second town* in an empty slot. Names *Town 1* and *Town 2*. New game, Export and Import act on the open town. This replaces ADR 0023's "one town at a time"; its rule that no save point is ever an undo stands.
6. **Time travel (idea 5) is dropped for now.** It comes back with the time-travel points idea, and the clock question (an offset kept in the save, or not) is settled then.
7. **Graphics direction.** Three samples (a family house, a wheat field, a person) rendered both realistic and rich stylised in Blender, shown to the owner, who picks; then the redesign, one category per story with a before/after preview.
8. **Frame time.** No device baseline first (the owner). The bar: about 60 fps, no dropped frames, no heating. My calls: sprites keep today's pixel sizes, no new shaders or particles, a `RenderStats` reading in every art story (growth over 10% reported), the 60 fps cap kept, and the phase's Development build reads both frame-time p95s on the iPhone before the pull request.
9. **Supply.** Visibility (today's supply of each business: from you or bought, by good) and one hand order, *Grow for the vegetable stall*, sown in turn.
10. **Manual flow.** Each business that takes goods from you gets a Supply section: *Automatic* (today's rule, the default) or *Manual* with a daily amount from you for each good (none, a quarter, half, all it needs, or stepped kg). What you don't send stays in your store; the rest is bought at the market if the business's switch allows.
11. **Long tab rows scroll sideways** with a fade at the edge (every sheet).
12. **Badges grow to fit and never wrap** (People, Orders, alerts), capped at "99+". The HUD under an open sheet stays as it is (the owner).

**Spec 031 gaps (the owner, 2026-11-26):**

13. **A notification's tap opens its town** (switching at launch if it isn't the open one).
14. **Town 2 starts with the same first session** as any new town; the notification ask isn't repeated.
15. **The town's name on the time card** once a second town exists ("Town 2 · Monsoon · …").
16. **No removing a town:** New game replaces the open one.
17. **Two towns in two stories:** US-270 (slots, Your towns, the switch, 5 points) and US-272 (notifications from both, a play log each, 3 points).

## The plan

| Sprint | Branch | Scope | Specs |
| --- | --- | --- | --- |
| 28 | `sprint/28-business-hub` | US-265 UI fixes, US-266 one business run by you, US-267 the Businesses hub, US-268 supply (today's supply, Manual), US-269 *Grow for the vegetable stall* | 030 (written and reviewed first), 031 (written and reviewed in the sprint for Sprint 29) |
| 29 | `sprint/29-two-towns` | US-270 and US-272 two towns; US-271 the three samples | 032 (graphics) |
| 30 | `sprint/30-art-land` | US-273 ground and terrain, US-274 crops, US-275 trees (13 points) | 032 |
| 31 | `sprint/31-art-people-farm` | US-276 people, US-277 animals and machines, US-278 your farm (16 points) | 032 |
| 32 | `sprint/32-art-town` | US-279 businesses, US-280 village and town, US-281 icons, US-282 effects (16 points) | 032 |

- **One exception to ADR 0019:** spec 032 is written in Sprint 29, not in the phase's first sprint, because its style can't be fixed before the owner picks from the samples.
- **Save schema 25** ships once, in Sprint 28 (the supply caps, the day's supply counters, the stall order's business). Sprint 29's two towns need no sim state.
- **Goldens must not move.** The new rule only refuses; the new state is empty by default; no bot gives the new order or sets Manual. A golden that moves comes back to the owner.
- Each story ends at the owner's review; every screen is previewed as an exact-looking artifact before its UI code.

**The art inventory (2026-11-26, [docs/art/inventory.md](../art/inventory.md)):** 605 Blender sprites in ten categories (ground 48, crops 69, trees 22, people 137, animals and machines 18, your farm 50, businesses 93, village 85, icons 76, effects 7) and eight kinds drawn in code (sky, weather, shadow, ring, lit acre, guide arrow, overlays, the UI). The redesign is ten stories, about 45 points over Sprints 30–32, sized again after the samples.

## Consequences

- ADR 0023's "one town at a time" is superseded by decision 5; its no-undo rule stays.
- ADR 0002's "no speed controls" stands (time travel dropped).
- Phase 4 starts after this phase's last pull request is merged.
