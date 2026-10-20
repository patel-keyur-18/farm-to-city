# 0045. Sprint 8: nudges and the tour, the long crop and the ask, six new ACs, the Phase 1 health checks, a feature-complete build

- Status: Accepted
- Date: 2026-10-20
- Deciders: Keyur Patel
- Amends: spec 009 §3.2 (step 6's goal, the nudges, the tour), §7 (the `comeBack` text and buttons), §8 (AC-11 to AC-16), §10; spec 007 §10 note 6 and ADR 0041 §6 (the plain card stays as a fallback); the GDD (Balancing and testing: the Phase 1 health checks); F09.1, F20.1 and Sprint 8 in `sprints.md`
- Related: ADR 0015 (onboarding), ADR 0016 (balance rule), ADR 0036 (the ask in the first session), ADR 0041 (no daily cap, the first ask as a card), ADR 0030 (the Hamlet gate), ADR 0025 (the sprint)
- GDD sections: UI/UX and screens (Onboarding), Balancing and testing

## Context

The Sprint 8 planning review (2026-10-20) checked every story and task of F09.1, F09.2, F09.3 and F20.1 against spec 009 and the GDD. It found five gaps:

1. US-090.T2 (advisor nudges) had no rule and no AC. Spec 009 says "one-line nudges" but defines no text, place or behaviour, and its "short farm tour" during the garden wait is undefined.
2. Step 6 needed a *well* acre, so a potato on a rain-fed acre never completed it and the nudge nagged. The `comeBack` text said "At most twice a day", which contradicts ADR 0041's "no daily cap". `comeBack` had no yes or no. And nothing asked about notifications if the first session was skipped.
3. Four tasks had no AC: the letter sheet (US-089.T1), reopening chapters (US-092.T2), the Hamlet's pace (§9) and the first sale within 15 minutes (§9). Gaps 1 and 2 added two more behaviours that need tests.
4. US-098's acceptance names four checks, but the GDD defines "never stuck" only as "within a set number of seasons", and "recoverable bankruptcy" by the Village era, which Phase 1 doesn't have.
5. Sprint 8's device build was called a "release candidate", but US-095 (Sprint 9) is also called "Release candidate".

## Decision

1. **Nudges and the tour.**
   - While a first-session step is current and its chapter is closed, a one-line nudge shows as a pill at the top of the map, for example "Tap a garden bed and sow radish".
   - Tapping the pill replays the step's highlight. The pill goes once the step is done or the session is skipped.
   - **The farm tour is the wait.** While the first bed grows, the pill rotates through three tour lines (the store and its inherited seed, the well acres against the rain-fed acres, the founder's hours). Each line opens its sheet. There are no extra chapters.
   - Strings `nudge.<step>` and `tour.<n>`.
2. **The long crop and the ask.**
   - **Step 6's goal** (a rule change, re-approved): a field crop with growth of at least 12 h sown on **any** owned acre. The highlight and the nudge still point at well acre 20.
   - **`comeBack`'s text** ends "Want a nudge when it's ready? Never at night." (no daily cap, ADR 0041).
   - **`comeBack`'s buttons** are **Yes, nudge me** and **Not now**. iOS's permission dialog follows only a Yes. If notifications are already on, the question is left out and the button is **Continue**.
   - **The plain card stays as the fallback.** It asks at the first field crop of at least a day, but only if `comeBack` hasn't asked (the session was skipped, or the player got past that point another way). Either path counts as the first ask. The second ask after the first season close is unchanged, and the game never asks after that.
3. **Six new ACs** in spec 009 §8: AC-11 the letter sheet, AC-12 reopening chapters, AC-13 the nudges, AC-14 the ask, AC-15 the Hamlet's pace on the real sim rule (the balancer's own copy of the rule is deleted), AC-16 the first sale within 15 minutes. The Hamlet vignette (tint and banner) stays on the device checklist only: it is presentation, and the `hamlet` chapter it opens is AC-4's.
4. **The Phase 1 health checks (US-098):**

   | Check | Rule |
   | --- | --- |
   | Bots | ADR 0016's stress set: debt-fuelled expansion (the existing leverage bot); a **cautious saver** (never borrows, hires only when cash covers the hut, a season's wage and a season's living costs); a **random** bot (a seeded random player command each hour) |
   | No money loops | The existing structural, zero-time and stress checks; the cautious saver at most 1.20 × competent; random below competent |
   | Never stuck | No cash, an empty store, and **credit refused**: farm profit above 0 in some season within N seasons |
   | Recoverable bankruptcy | A bot that defaults until foreclosed, then plays competently: farm profit above 0 within N seasons of the foreclosure, and the Hamlet's conditions met again within the GDD's window of 3 game years |
   | Exact ledger | Every bot, random included, closes at least 8 seasons on 100 seeds without a reconciliation failure, and the closes are counted |
   | Bounded prices | The 0.5–2× rail is hit in under 0.1% of good-hours over the competent runs (from the GDD's every-build list) |

   **Feasible, not arbitrary (owner).** Each threshold (the seasons N, the pass share, the window) is first measured on the real sim. It is then set at what competent play actually reaches, with a margin, but tight enough that a regression fails it. Where a GDD target proves unreachable, the measured numbers go to the owner with a realistic threshold. Content is never tuned just to meet an arbitrary number. The proposed starting values are N = 2 seasons and 99% of seeds. The median farm profit (26,111 after the hourly weather) is reported against spec 003 §9's band (24,000–45,600) and tuned only if a check fails.
5. **Sprint 8's build is a feature-complete Development build** (with the developer panel, for time travel to the season close, the chapters and the Hamlet). After it come only fixes and tuning. Its checklist has a row for every story's acceptance line. US-095 in Sprint 9 makes the real release candidate: a full regression, frame time, a Release build for the testers, and save migrations from that build on (ADR 0041 §2). Sprint 8 raises the save schema to 3 (Guidance and Era), so Sprint 7 saves are refused and kept.

## Consequences

- **Spec 009:** §3.2 gets the step 6 goal, the nudges and the tour; §7 gets `comeBack`'s text and buttons; §8 gets AC-11 to AC-16; §10 records this review.
- **Spec 007 §10 note 6:** the plain card is the fallback for the first ask, not retired.
- **GDD:** Balancing and testing describes the Phase 1 health checks.
- **Planning:** US-090's tasks name the nudges and the ask; US-098's tasks name the bots and the checks. Sprint 8's device build is "a feature-complete Development build". Points are unchanged at 21: the nudges were already US-090.T2, and the checks were already US-098.T2.
- **Goldens:** the Guidance and Era state is hashed, so the golden hashes change. They are re-pinned in their own commit.
