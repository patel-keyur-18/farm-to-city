# Sprint 01 build: hello iPhone

- **Build:** the head of branch `sprint/01-ledger-land-unity` (the commit is in the pull request)
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** Keyur Patel, 2026-10-18
- **How to install:** see `how-to-build-ios.md`

**What this build is:** the first build on the phone. One small card shows the simulation's date, time and weather. Nothing is tappable yet, and there are no saves, so every launch starts a new game (saves arrive in Sprint 6). The point is to prove the whole chain: Unity 6000.3.26f1 → Xcode 27.1 → free signing → iPhone 13, with the simulation running on the device.

## Checks

| # | Story | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | US-020 | Follow `how-to-build-ios.md` from the start | You get the app onto the phone using the guide alone. Note any step that was unclear or wrong | Pass | Guide alone, no issues |
| 2 | US-019 | Open the app from a cold start | Within 5 s: a blue background and a cream card at the top centre showing **Year 1 · Spring · Day 1**, a large time, and one weather word (Sun, Cloudy, Rain, Heavy rain, Heatwave, Frost or Storm) | Pass | Screenshot: landscape, 2532 × 1170 |
| 3 | US-019 | Compare the card's time with the phone's clock | The same hour and minute (24-hour format, e.g. 21:44) | Pass | Showed the current time (08:17) |
| 4 | US-019 | Leave the app open for 2 minutes | The minute changes in step with the phone's clock | Not reported | Re-check with the next build |
| 5 | US-019 | Turn the phone through both landscape directions and to portrait | It stays landscape (flips between the two landscape sides); never portrait | Not reported | Re-check with the next build |
| 6 | US-019 | Check the card against the notch and the rounded corners, in both landscape directions | Nothing is cut off | Pass | Card clear of the notch |
| 7 | US-019 | Press Home, wait 5 minutes, open the app again | The time catches up at once to the phone's clock. No crash | Not reported | Re-check with the next build |
| 8 | US-019 | (Optional) Keep the app open across 00:00, 06:00, 12:00 or 18:00 | The weather may change at that moment (a new 6-hour spell starts). It's fine if it stays the same | Not reported | Re-check with the next build |
| 9 | US-019 | Swipe the app away and open it again | It starts again at Year 1 · Spring · Day 1. **Expected** in this build: there are no saves yet | Not reported | Re-check with the next build |
| 10 | US-019 | Use it for a few minutes | The phone doesn't get warm; nothing stutters | Not reported | Re-check with the next build |

## What to report

- Every check marked **Fail**, with what you saw instead (a screenshot helps).
- Anything in the build guide that was confusing, missing or wrong (check 1).
- Crashes: what you did just before, and whether it happens again. If Xcode is attached, copy the last lines of its console.
- Paste the results back in the sprint review section of `docs/planning/sprints.md`, or into the chat with Claude.
