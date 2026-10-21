# Sprint 10 build: the hand works every season, labourers stay till harvest

- **Build:** the head of branch `sprint/10-people-fixes` (the commit is in the pull request)
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. **A Development build** (`--options '["Development"]'`), so the developer panel exists. Delete `unity/FarmToCity/Builds/iOS` before building (the macOS 27 crash).
- **Your town:** this build moves saves to schema 4. **Your Sprint 9 town loads** and is migrated, with nothing lost. Labourers you hired for the day stay until midnight, as they were paid. Please keep playing it.

**What this build changes (ADR 0048):**
- **The hand works every season.** An order's crop is sown when it's in season. In the other seasons, the hand sows the in-season crop that pays best on that field's soil and moisture. The order sheet has a new first row, **Let Manoj choose** (his name), which leaves every season's crop to him.
- **The hand is seen working.** A hand you pay walks round his own fields first, then every other growing field, because his hours care for all of them. He goes home only when nothing grows, or when his wage is unpaid.
- **Labourers stay till the crops are in (ADR 0049).** Hire them in **People** → **N labourers till harvest**; each row shows the cost. They are paid upfront until the last crop growing now is ready. They tend every field and bring in each field that has no order from Manoj as it ripens, then go home. Fields no longer offer hiring.
- **Orders on several fields work at once (ADR 0049).** Give Manoj orders on two fields one after the other: both are sown straight away.
- **People stand on top of the crops**, even at a field's edges.

**Your questions:** (1) does the hand now run his fields with no help from you? (2) are his crop choices sensible for each season? (3) is hiring from the field clear, and does the cost feel fair?

## Checks

Use the developer panel (hold the time card for 3 seconds) for time: **+1 hour**, **+1 day**, **+1 season**, **Away cap (72 h)**.

| # | Story / AC | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | ADR 0048 §4 | Launch with your Sprint 9 town | It loads, with the same cash, fields, hand and orders as before. No "A new town has started" card | | |
| 2 | US-186, AC-24 | Tap an empty field → **Give an order** | The first row is **Let Manoj choose**, with "Best here now: …". A crop that can't be sown this season says "Sown in …; Manoj picks the best crop in between" | | **Screenshot** |
| 3 | US-186, AC-13 | Choose a crop that **can't** be sown now (e.g. wheat outside Winter) → **Every season** → **Keep in store** | Within a few seconds, Manoj walks out of his hut to that field and it is sown with this season's best crop. It does **not** stay empty | | |
| 4 | US-186, AC-24 | Another empty field → **Let Manoj choose** → **Every season** → **Keep in store** | It is sown at once. The field's order line reads "Let Manoj choose · …". The sheet shows no minimum-price rows for this order | | |
| 4b | US-186 | People → Manoj → his orders | A field holding another crop than its order names reads e.g. "Keep Lentils (Rice now)" | | |
| 5 | US-186 | **+1 season** twice, watching his fields | At each new season, every ordered field is harvested and re-sown with a crop of that season. No field sits empty for long | | |
| 6 | US-186, ADR 0048 §2 | Watch Manoj while crops grow, including fields with no order | He walks between fields and hoes. He never stands idle at his hut while anything is growing | | **Screenshot** |
| 7 | US-186 | **Away cap (72 h)** | The away card lists what Manoj harvested, sold and sowed. Every ordered field is busy | | |
| 8 | US-187, AC-25 | With crops growing, open **People** | "1 labourer till harvest", "2 labourers till harvest", and so on, each with a cost. The **?** says they stay until the last crop growing now is ready, harvest each field without an order, then go home. A field's own sheet has **no** hire rows | | **Screenshot** |
| 9 | US-187, AC-3 | Hire 2 | The cost leaves your cash. People shows "Working today: 2, till the crops are in". Two labourers walk in from the west to the fields | | |
| 10 | US-187, AC-25 | **+1 day** | The labourers are still there after midnight if a crop is still growing | | |
| 11 | US-187, AC-25 | Advance in **+1 hour** steps until the crops ripen | Each field **without** an order from Manoj is harvested into the store within the hour it ripens. Manoj's fields are harvested by Manoj. After the last crop, the labourers walk off west | | |
| 12 | ADR 0049 | Give Manoj orders on two empty fields, one right after the other | Both fields are sown at once, and he walks between them | | |
| 12b | ADR 0049 | Watch people near the edges of fields of tall rice | Everyone is drawn on top of the crops, never under them | | **Screenshot** |
| 13 | Regression | Sow by hand on a field ordered with a crop | You're asked whether to change Manoj's order. On a "Let Manoj choose" field, you aren't asked: his choice stays | | |

## Report

- Anything that blocks play: **BLOCKER** plus what you did.
- Screenshots for checks 2, 6 and 8.
