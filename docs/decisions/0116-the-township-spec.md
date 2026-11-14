# 0116. The Township's spec: a lighter clinic and road, a rented doctor's quarters with no allowance from you, one coverage rule, appointments for public posts, and an aggregate old village

- Status: Accepted (owner, 2026-11-14, G1–G10 closed one per message)
- Date: 2026-11-14
- Deciders: Keyur Patel
- Amends: ADR 0107 §3 (who builds and pays for the clinic and the doctor), §4 (the road's share), §13 (who staffs civic buildings); ADR 0108 §6 (the Health department's first lines)
- Related ADRs: 0107, 0108, 0114, 0115, 0063, 0110, 0077, 0097, 0064

## Context

ADR 0107 planned the Township: a clinic built like the school (1,20,000) with a doctor's house (60,000), a doctor's allowance paid by you until the Township, a link road for a 50,000 share, and civic buildings staffed from the hiring board. Writing spec 023 the owner asked, twice, whether this was too much burden on an average player for money that is "just gone". Checking showed it was: about 1,80,000 plus 34,000 a season for a clinic whose benefit in coins is small, and a road that paid back in 17–30 seasons.

## Decisions

1. **The district builds the clinic; you pay a 40% community share, 48,000** (a gift). That is how real health sub-centres work: the village gives land, quarters and a share.
2. **The doctor's quarters are an asset, not a gift.** A larger house of yours, 90,000, reserved for the doctor, who pays 3,750 a season (the same 4.17% a season yield as the 60,000 house at 2,500, so the yield rule of US-240 holds). His household counts as one of your 20 and in literacy.
3. **No allowance from you, ever.** He has a salary; the house is what draws him (as a hand's house is part of his pay). Before the Township the district pays his pay, his allowance and the clinic's upkeep. From the Township the treasury pays the allowance (30,000) and the upkeep (3,000), which is its job (spec 021).
4. **The road's share is 25,000** (was 50,000), a gift, with the gain shown on the season page and in each trader price's *Why?*.
5. **The clinic's effects are four modest rates and one need** (§3.2), scaled by the Health department's condition, reach and capacity. No visit fee. The benefit is shown as exact counts ("sick days avoided") so it is not invisible.
6. **The gate's three businesses must be open at the check**, each with a profitable season; the doctor's family counts for literacy; the pace target stays day 63–77 with a flag to the owner if the median falls under 55 or over 77 (cheaper costs may make it faster, and the response is to tighten a goal, not to loosen the economy).
7. **The old village is one aggregate record** (30 households, 150 people), never citizens: a tax and grant base, school pupils, a literacy contribution, a happiness that follows the town's real coverage, and a household that leaves after two closes under 35.
8. **One coverage rule for every civic building, the school included:** a home is covered within the building's reach in metres (an acre is 100 m, Manhattan); capacity scales the benefit; no staff, no coverage. The reaches are 1,500 m (school) and 2,000 m (clinic), 400 people for the clinic. Today's map is inside both; the ring brings the bite.
9. **Three civic buildings on fixed public plots** (watchman 30,000, hall 80,000, playground 20,000), commissioned by the treasury through a card with a **budget guard** row that says whether the town would be short. A *Safety* part joins the security layer from the Township.
10. **Public posts are appointments, not hires from the board:** one tap posts the job, a villager from the old village takes it within 24 h at a fixed pay scale, paid as a Safety line and the cohort's income, leaving after two unpaid closes. (Amends ADR 0107 §13's "from the hiring board".)
11. **The default Township must break even within two years** at default taxes and no civic buildings; the levers, in order, are the doctor's allowance (toward 24,000) and the clinic's upkeep, never the grants or the default taxes.
12. **The doctor leaves at the third unpaid close and returns at 70% service with the cash for his allowance**; his quarters stay reserved; the Township is never lost.

## Consequences

- Save schema 20 (the clinic, road, cohort, posts and gate state), after Sprint 21's 19; hashed only once started, so earlier towns and the goldens are unchanged.
- The Township's pace will be measured by `TownshipBot` and reported to the owner if it falls outside day 55–77.
- Specs 021 and 022 need no change: the cohort, the clinic's staffing and the Health lines they already name are filled in here.
