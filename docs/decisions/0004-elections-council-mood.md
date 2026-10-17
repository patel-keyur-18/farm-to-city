# 0004. Elections: the player never loses office; results set the council's mood

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0002 (real-time clock: 1 game year = 16 real days)
- GDD sections: Governance and public finance, Decisions and consequences, Open decisions and risks

## Context

From the Town era there is an election every 5 game years. Under ADR 0002 that is every 80 real days, so a campaign has only 1–2 elections. The GDD left open whether the player can lose office.

## Options

1. **Low approval only limits the player's powers.** The player stays in office.
2. **A full loss.** A rival runs the treasury for a term. That would lock the player out of public finance for about 80 real days, close to 3 months, which conflicts with "mistakes are recoverable" and "calm, fair".

## Decision

Option 1, with a **council mood** design so that elections still matter.

- An election produces a vote share for the player's platform, and that sets the **council mood** for the next term. The mood tiers and their thresholds live in data, with no hard-coded numbers.
- **Supportive council** (high approval):
  - a higher tax ceiling
  - more policy slots
  - eligibility for regional grants
- **Neutral council:** the baseline ceilings and slots.
- **Hostile council** (low approval):
  - a lower tax ceiling
  - fewer policies active at once
  - budget revisions need a council vote, which can fail
  - unanswered petitions escalate into protests that lower happiness
- Approval between elections still works as the GDD describes. It changes how much tax citizens accept and how fast migration flows.
- The mood is reset by the next election, not by any one-off action. Recovery means governing well for a term.

## Consequences

- The governance spec must define:
  - the vote-share formula from approval
  - the mood tiers
  - the council-vote probability for budget revisions
  - the protest rules
- The result screen works as a public report card, and it's a Chronicle moment.
- No "rival term" state is needed, so there is no rival-AI treasury logic.
- A full loss of office, or a challenger, could be added later as a Realistic-difficulty option.
