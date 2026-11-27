# 0181. Sprint 29 independent review (Opus 5.5): five major and nine minor findings, fixed or flagged

- Status: Accepted
- Date: 2026-11-27
- Deciders: Keyur Patel
- Related ADRs: 0178, 0179, 0180, 0177

## Context

CLAUDE.md's rule: once a sprint's stories are built and approved, and before the pull request, an independent agent (Opus 5.5, high effort, read-only) reviews the whole sprint against its plan. It read all seven commits of `sprint/29-two-towns` against `sprints.md`, the feature files, specs 031 and 032, ADRs 0172, 0023 and 0178 to 0180, the test checklist and the code, and ran the test projects. No blocker.

## Findings and what was done

| # | Finding | Done |
| --- | --- | --- |
| M1 | The other town's notes could go out twice: a kept note the platform had delivered while the app was closed looked "past due" at the next backgrounding and was sent again; with notifications off, past-due notes piled up and fired days late. | **Fixed.** `KeptNotes.Merge` takes `sinceWallUtc` (when the app last came to the foreground, `notify.foreground`); only notes due after it count as past due. `NotificationMergeTests.DeliveredWhileClosed_NotSentAgain`. ADR 0179 §7. |
| M2 | The time card lost "Town 2 ·" after New game, Import or a number-format change (breaks spec 031 AC-6): three places built a `ViewContext` without the town's name. | **Fixed.** One `NewContext()` builds the context and the name; checked in the Editor (the name survives the number-format rebuild). |
| M3 | Spec 032 said a building's shadow has "the same frame and pivot" as its sprite, which cannot hold with AC-1 (the realistic house is 316 px wide with its shadow, 252 without). | **Fixed in the spec:** a shadow has its own frame and its pivot at the same world anchor; AC-1 applies to the building sprite. |
| M4 | Spec 031 AC-1 named a test that did not exist (`TownSlotTests.StartSecond`). | **Fixed.** The test is written (the save side of starting a second town), with `UnreadableTargetStaysPut` for the owner's gap-1 rule (new AC-12). |
| M5 | The F21.3 feature file contradicted the sprint ("awaiting review", "soonest 64") and ticked a device check that has not happened. | **Fixed.** Done and approved; the merge's rule corrected; T4 split into the Editor part (done) and the device part (open, for the phase's build). |
| m1 | An exception part way through a switch could leave the new slot paired with the old town's sim (the next autosave would overwrite the other town). | **Fixed.** `GameRoot.Move` changes slot, service, play log and sim together or restores all; the sim is built before the old town is left. |
| m2 | In Development builds the developer snapshot survived a switch and a Restore would load one town's state into the other's slot. | **Fixed.** `EnterTown` clears the snapshot (checked in the Editor). |
| m3 | Stale ADR statuses and notes (0178, 0179 "awaits review"; 0023 still "Accepted" with its table row; 0172 §8). | **Fixed.** |
| m4 | The GDD's notifications paragraph did not cover two towns. | **Fixed.** |
| m5 | Render statistics did not match the claims, and the triangle growth was unresolved. | **Fixed: cause found.** An A/B in the same Editor town on `main`'s Unity code and on this branch reads the same numbers (farm 9,296 and 9,297; the map alone 8,589 on both): nothing of this sprint adds triangles; the Editor town itself had grown since the US-269 reading. New baseline `Sprint 29 end (review)` in `render-stats.json`; the three texts corrected. |
| m6 | Spec 032 named test locations that would not work (`ContentTests` in Sim.Core for `ui.json`; `IsoGrid.MinHalfHeight`; the default view living in Unity code). | **Fixed in the spec** (`UiContentTests` in Sim.View.Tests, `MapCamera`, the default moves into `MapCamera`). |
| m7 | Spec 032's draw-call estimate for shadows was not backed by a count. | **Flagged, evidence to come:** the estimate is withdrawn from the spec (stated as not yet counted); US-283 counts the shadow-casting sprites with `RenderStats` and restates it. |
| m8 | `PlayLogPerTown` and `SwitchRoundTrip` do not run `GameRoot`'s switch, so the owner's "target loaded first" order had no automated test. | **Partly fixed, partly flagged:** the save side now has `UnreadableTargetStaysPut`; the order inside `GameRoot.SwitchTown` and the play-log sequence remain verified by the Editor tour (said plainly in spec 031's AC table). |
| m9 | Stale STATUS row, CLAUDE.md layout and test rule (a third test project), US-271's priority. | **Fixed.** |

## Flagged, not changed

- **A note "a minute after you leave" is lost if you come back inside that minute** (the return cancels it and moves the foreground time forward). Accepted: a minute is the owner's rule, and the town's own cards show on return.
- **The real iOS tap round trip** and the frame-time baseline remain device-only, for the phase's Development build.
- **Balance** (`Category=Balance`) was run after the fixes: 69 of 69 passed (29 minutes).

## Consequences

No sim rule, save or golden changed by the fixes. Tests after them: `Sim.Core.Tests` 975 (not Balance, not Perf), Perf 6, `Sim.View.Tests` 460, `Game.Services.Tests` 9.
