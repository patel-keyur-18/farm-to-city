# 0011. Era gates: literacy, the first school and the first doctor

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0002 (life clock, calendar targets), ADR 0009 (cooperative bank loans)
- GDD sections: Player journey and eras, Community and civic services, Balancing and testing

## Context

Under ADR 0002's calendar, Township is due at weeks 3–4. Three era conditions don't fit:

1. **Township needs literacy above 40%,** but a school's 10–15 life-year payoff takes 40–60 real days.
2. **Village needs a primary school,** but the treasury that would pay for it only starts at Village.
3. **Township needs a clinic,** but a locally educated doctor takes 40–60+ days.

## Decision

1. **Literacy is separate from skilled graduates.**
   - Literacy is the share of residents aged 7+ who can read.
   - A child becomes literate after **4 life-years of primary school, about 16 real days**.
   - A school can run **adult literacy classes**. Adults become literate in about 1 season, and the classes use teacher hours.
   - **Migrants** arrive with an education level drawn from data.
   - The 10–15 life-year pipeline (40–60 real days) still applies to **skilled graduates**, such as doctors and engineers, who gate later eras.
2. **The founder funds the first primary school** from the personal wallet, as a community investment with no direct return. The cooperative bank can lend for it, and it's a Chronicle moment. Later schools are funded by the treasury.
3. **The first doctor is a migrant.**
   - The Township clinic is staffed by a migrant doctor, attracted by a staff house and a wage above market.
   - Locally educated doctors (through scholarships or a college) become possible from the Town era.
   - The same pattern applies to other first professionals, such as the first engineer.

## Consequences

- **The education spec defines:** literacy progression, adult classes, and the education distribution for migrants.
- **New balancer health check:** every era gate is reachable within the calendar targets in ADR 0002, for at least the three town archetypes.
- **Two kinds of education now matter:** the fast payoff (literacy, era unlock) and the slow payoff (skilled workforce, later eras and wages). Both are visible.
