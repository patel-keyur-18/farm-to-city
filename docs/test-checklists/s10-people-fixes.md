# Sprint 10 build: the hand works every season, labourers stay till harvest

- **Build:** the head of branch `sprint/10-people-fixes` (the commit is in the pull request)
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. **A Development build** (`--options '["Development"]'`), so the developer panel exists. Delete `unity/FarmToCity/Builds/iOS` before building (the macOS 27 crash).
- **Your town:** this build moves saves to schema 4. **Your Sprint 9 town loads** and is migrated, with nothing lost. Labourers you hired for the day stay until midnight, as they were paid. Please keep playing it.

**What this build changes (ADR 0048):**
- **The hand works every season.** An order's crop is sown when it's in season. In the other seasons, the hand sows the in-season crop that pays best on that field's soil and moisture. The order sheet has a new first row, **Let Manoj choose** (his name), which leaves every season's crop to him.
- **The hand is seen working.** A hand you pay walks round his own fields first, then every other growing field, because his hours care for all of them. He goes home only when nothing grows, or when his wage is unpaid.
- **Labourers stay till the harvest.** Hire them from a **growing field's sheet** with **1 labourer till harvest**, **2 labourers till harvest** and so on, each showing its cost. They are paid upfront up to the hour the crop is ready. They tend it, harvest it into the store, then go home. The People sheet no longer hires anyone: it shows who is free and points you to a growing field.

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
| 8 | US-187, AC-25 | Tap a growing field | "1 labourer till harvest" (and more rows, one per free labourer), each with a cost. The **?** explains: one labourer a day, until the crop is ready, they harvest it and go home | | **Screenshot** |
| 9 | US-187, AC-3 | Hire 1 | The cost leaves your cash. The sheet **stays open** and shows "Labourers on this crop: 1, harvest in …". A labourer walks in from the west to that field first | | |
| 10 | US-187, AC-25 | Hire on a 24-hour crop (potato or rice), then **+1 day** | The labourer is still there after midnight. People shows them as working "till their crops are ready" | | |
| 11 | US-187, AC-25 | Advance until that crop is ready (**+1 hour** steps) | Within the hour it's ready, the labourer harvests it into the store (or to the trader if the store is full) and walks off west | | |
| 12 | US-187 | Open **People** | No hire buttons. "Hire labourers: Tap a growing field: they stay till its harvest" | | |
| 13 | Regression | Sow by hand on a field ordered with a crop | You're asked whether to change Manoj's order. On a "Let Manoj choose" field, you aren't asked: his choice stays | | |

## Report

- Anything that blocks play: **BLOCKER** plus what you did.
- Screenshots for checks 2, 6 and 8.
