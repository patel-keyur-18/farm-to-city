# Device test checklists

Every build handed to the owner comes with a checklist here (CLAUDE.md, ADR 0017, ADR 0025). A story with something visible on screen isn't done until the owner has confirmed it on the iPhone 13.

**File name:** `sNN-short-name.md`, for example `s01-hello-iphone.md`.

## Template

Copy everything below the line into a new file.

---

# Sprint NN build: short name

- **Build:** commit `abcdef0` on branch `sprint/NN-short-name`
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`

## Checks

| # | Story | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | US-000 | Open the app from a cold start | The title screen appears within 5 s | | |
| 2 | | | | | |

## What to report

- Every check marked **Fail**, with what you saw instead (a screenshot or screen recording helps).
- Anything that felt confusing or slow, even if it passed.
- Crashes: what you did just before, and whether it happens again.
- Paste the results back in the sprint review section of `docs/planning/sprints.md`, or into the chat with Claude.
