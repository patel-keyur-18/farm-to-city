# 0081. US-112 review: renewals at the current wage, children's farming skill, the board's build notes

- Status: Accepted
- Date: 2026-10-28
- Deciders: Keyur Patel
- Amends: spec 015 §3.3, §3.7 (renewal and the raise request), spec 014 §3.1 (a child's skills), spec 006 §3.6 (a hand's renewal)
- Related: ADR 0069 (spec 015), ADR 0063 (spec 014), ADR 0041 (regulars)

## Context

The owner reviewed US-112, the hiring board, on 2026-10-28. Claude raised three things:

1. **Renewals left the raise request with nobody to come from.** Every worker was renewed at each close at the asked wage for their skill: spec 006's rule for hands, carried over to business workers. That cleared a low hire's memory automatically and paid skill growth automatically. So at a close nobody was paid below what they would ask, except a loyal worker who keeps their wage, and spec 015 §3.7's raise request ("a worker paid below their asked wage asks, 50% a close") would almost never happen. "Pay fairly or lose them" was never a choice.
2. **Children grew up with farming skill 0.** Spec 014 drew a farming skill for adults only, and children kept 0. A grown child would show on the board as a Beginner whatever their upbringing. The crafting skill US-112 added is drawn for children too.
3. **US-112's build notes** (spec 015 §10) settled choices the spec left open: townspeople apply at once, each side has its own three places, a let-go worker keeps the season's pay, the farm hand's post keeps its pace, and wages stay where they were in the data.

## Decision

1. **A contract renews at its current wage** (hands and business workers alike, from US-114). When the asked wage for a worker's skill has risen above what they're paid (they grew on the job, finished a course, or were hired low), they may **ask for a raise** at a close (spec 015 §3.7: 50% a close, Pay or Not now). A loyal worker never asks. An ambitious one asks 10% above their asked wage. A low hire's memory clears only when a raise or a renewal pays the asked wage. The family house as housing in kind (spec 014 §3.6) still applies at renewal.
2. **Every citizen's farming skill is drawn at birth**, like the crafting skill: skillMin–skillMax from their id (stream Citizens). A migrant family's children get one too. The schema-11 migration draws one for every living citizen still at 0.
3. **US-112's build notes are accepted** as written in spec 015 §10.

## Consequences

- The raise request becomes a real choice in US-114, with reputation and motivation behind it. Wages stop following skill by themselves, so a player who ignores raises pays less but keeps a less motivated worker who is likelier to be poached.
- Until US-114 lands in the same sprint, renewals keep the old behaviour: US-113's course test reads the asked wage, which holds either way.
- A grown child applies as a hand or worker like any other townsperson. US-219 gets smaller: a townsperson hired as a hand already lives in their family house, and housing in kind already applies at renewal.
- Save schema 11 (not yet shipped) draws the missing farming skills at the migration. No new schema is needed.
