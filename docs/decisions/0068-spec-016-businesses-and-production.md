# 0068. Spec 016: businesses with their own books; the flour mill, the bakery and the dairy

- Status: Accepted
- Date: 2026-10-25
- Deciders: Keyur Patel
- Amends: GDD Businesses (the Phase 2 catalogue, the first business), spec 005 (each business has its own ledger; account 1800 Investment in businesses; net worth), spec 012 (milk can go to your own dairy instead of the co-op), spec 014 (a business job is a town job)
- Related: ADR 0009 (never stuck), ADR 0034 (prices follow the index), ADR 0054 (the trader's truck), ADR 0056 and 0060 (milk and the co-op), ADR 0057 (spec order), ADR 0063 (households)

## Context

Spec 016 was written in Sprint 13 (ADR 0057) and its ten gaps closed with the owner one per message. Two findings shaped it:
- None of the six inherited acres touches a road, so a "beside the road" rule would force a land purchase before a first business.
- A real chakki lives on grinding for every hamlet around it. The village alone (about 35 neighbours plus your people) brings too little grain to keep a miller, and wheat grows only in Winter, at 1,200 kg an acre.

The final review also found that the rates had to use the game's time compression (a real day is 22.8 days of life, spec 012 G1).

## Decision

1. **Phase 2 businesses (G1):** the flour mill (atta chakki) from the Hamlet; the bakery and the **dairy** (paneer, curd, ghee from your own milk) from the Village. The owner asked for the dairy in this phase because no later phase plans it. The farm and market stalls come with spec 017's shops.
2. **Own books (G2):** each business has its own ledger, owner's capital and drawings. Your books show 1800 Investment in businesses at cost. Net worth shows each business at its equity, never below 0. Your own wheat or milk goes to a business at today's market price: a sale on the farm, a cost on the business.
3. **Who works (G3):** until spec 015, the best-skilled adult in your town without a town job (else a daily worker from a neighbouring village), at a season wage paid upfront. Or you run it yourself, at no wage, with the hours coming off your farm work. A business job is a town job.
4. **Where (G4):** any acre you own with nothing growing, one business an acre. The acre is out of farming and can't be sold while the business stands. Each business's layout is previewed to the owner first. Foreclosure takes a business on bought land; one on the inherited acres is protected.
5. **The mill (G5, G6):**
   - It grinds the area's grain (your town, the village and the hamlets around, about 300 people) at 4 coins a kg, about 10,900 kg a season.
   - It mills your own wheat into atta (0.97) and bran (0.03); market wheat is optional.
   - It grinds 380 kg a real hour while open (a real chakki's 400 kg day); power costs 1.20 a kg.
   - Atta goes to the trader (400 kg a day), the market and supermarket orders. Bran goes to your feed room first.
6. **Funding (G7):** your cash as capital, plus the co-op bank's small-business loan, up to 60% of the building and equipment over up to 12 seasons, with your guarantee. You put in at least the rest plus a season's running cash.
7. **Research and the plan (G8):** a free research sheet; a plan with the projected season, cash needed and payback; plan vs actual after 4 seasons.
8. **Trouble (G9):**
   - An overdraft covers shortfalls up to half a season's running costs, at 4% a season.
   - Past that limit, inputs stop and the unpaid worker leaves at the close.
   - Closing sells stock to the trader and the building and equipment at 60% of book value. What's still owed becomes your co-op bank loan.
9. **The bakery and the dairy (G10):** both use realistic recipes and rates. The village grocer and tea stall buy each morning at wholesale for the area; your town buys from its food and leisure shares. What keeps (rusk, ghee) also goes to the trader and supermarket orders.

## Consequences

- Expected results: a mill with a hired miller makes 5,000–10,000 a season and pays back in 6–12 seasons; run by you, it makes about 35,000 but takes your farm time. Processing pays most on your own harvest. The balancer tunes these figures in Sprint 14.
- The Village unlock's "a business profitable for 2 seasons" (spec 018) is reachable: the mill run by you profits from its first season.
- Code: F12.1 (US-115–US-117) in Sprint 14 with save schema 10; F12.2 (US-118, US-119, US-216 the dairy) in Sprint 15.
