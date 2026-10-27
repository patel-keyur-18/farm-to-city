# 0070. Marriage is patrilocal: a son's bride moves in, a daughter marries away

- Status: Accepted
- Date: 2026-10-25
- Deciders: Keyur Patel
- Amends: spec 014 §3.3 (the departed), §3.9 (marriage), AC-13
- Related: ADR 0063 (spec 014), ADR 0001 (an Indian-inspired valley)

## Context

Spec 014 §3.9 had "the spouse moves in from another village" for any adult child aged 22–30 who marries. In rural India marriage is mostly patrilocal: a bride goes to live with her husband's family, and a groom moving into his wife's parents' home (*ghar jamai*) is rare. Building US-109, Claude asked the owner which rule to use; the owner chose realism.

## Decision

1. **A son's bride moves in** from another village (age 0–4 years younger, at least 18; skill, traits and schooling drawn as for an applicant). If a family house stands empty (and isn't offered to a hand), the couple moves into it as a new household; otherwise they live with his parents.
2. **A daughter marries away** to her husband's village (drawn from the home villages): she leaves the town, worded gently ("Asha married and moved to her husband's village, Rampur"), and her card reads "Married, lives in Rampur".
3. **She is kept in the family tree while a parent lives** (§3.3 adds the parent link for a daughter who married away). The tree stays about two generations deep: AC-13's bound (under 1 departed a living citizen over 20 years) still applies.
4. Chances unchanged: `marriageChance` 0.25 a season from 22 to 30, living with a parent, unmarried, not your hand.

## Consequences

- Fewer people stay than under the old rule: daughters leave and sons stay. Births make it up; US-211 measures population against spec 014 §9 (12–25 at the Village) with this rule.
- A son's marriage is the town's other way to fill an empty house besides families moving in (US-110).
- Tests: `PopulationTests.Flows` (sons' brides in, a free house taken, daughters away and kept), `PeopleViewTests.Flows_InWords`.
