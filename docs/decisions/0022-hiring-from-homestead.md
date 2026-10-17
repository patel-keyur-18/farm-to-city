# 0022. Hiring starts in Homestead: day labourers and resident hands

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0008 (season contracts), ADR 0010 (founder labour), ADR 0017 (Phase 1 scope)
- GDD sections: Player journey and eras, Citizens and workforce (Hiring), Roadmap (Phase 1 scope)

## Context

The era table listed hiring as a Hamlet system. However:
- the founder's labour runs out at about 3 acres (ADR 0010), which is in Homestead
- the Hamlet unlock itself requires a house for a hired hand

The town also has no citizens in Homestead, so there was no source of applicants.

## Decision

Hiring starts in Homestead, with two kinds of farm labour:

| | Day labourer | Resident farm hand |
| --- | --- | --- |
| Source | Neighbouring villages, commuting daily | A migrant who moves onto the founder's land |
| Contract | One day at a time, paid upfront | Season contract, paid upfront (ADR 0008) |
| Cost per day | Higher (premium in data, about 25%) | Lower. Gains skill with experience |
| Housing | None needed | A hand's hut on the founder's land is required |
| Availability | Limited daily pool (data). Scarcer at harvest time | Applicants arrive over a few days |
| Standing orders | No. Does the task assigned for the day | Yes |
| Population | Not counted | Counted. Becomes a citizen, and may bring a family |

**Regulars become neighbours.** A day labourer the founder hires often (threshold in data) can ask to move to the village. They then become a resident-hand applicant whose traits are already known. Over time, migrants settle, form households and grow family trees in the town. Births and the family tree are part of the citizens system (Phase 2).

**Phase 1 hiring of a resident hand:**
- applicants show skills as ranges and one trait
- an interview reveals one more trait
- the founder makes an offer at the asked wage, which the applicant accepts or declines

Negotiation, reference checks and the full hiring board come in Phase 2.

## Consequences

- **Era table:** Homestead gains day labourers and the first resident hand. Hamlet keeps more roles, storage and first processing.
- **What this teaches:**
  - casual labour as a variable cost vs permanent staff as a fixed cost
  - housing is part of the cost of hiring
  - trust built over repeated work reduces hiring uncertainty
- **New definitions:**
  - `LabourPoolDef` (daily pool size by season, premium, regular threshold)
  - `BuildingDef` for the hand's hut
- **Spec 006** (people: hiring and contracts) covers this in full.
