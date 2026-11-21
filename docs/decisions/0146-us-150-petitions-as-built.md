# 0146. US-150 petitions as built: eight topics, judged at each close, approval moves a close and changes only migration

- Status: Accepted (the owner approved US-150, 2026-11-20; the §9 tuning still to be confirmed with the owner)
- Date: 2026-11-20
- Deciders: Keyur Patel
- Related: spec 026 §3.2, §3.3, §3.7, ADR 0004, 0127, 0145

## Context

Spec 026 §3.2 and §3.3 set petitions and approval. US-151 (ADR 0145) read the town at each close but left approval at 50. US-150 makes approval move and lets households ask for what they lack.

## Decisions

1. **Eight topics, not nine.** The spec's table lists eight (clinic, school, community hall, watchman, playground, water, rent too high, no work) and says nine; `QuietUntil` has eight slots. Everything starts at the Township, because town life does (the "clinic from the Village" row has no effect: no petition exists before the first close from the Township).
2. **A need is a household fact, read at the close.** Clinic, hall, watchman, playground: the household's effect from that building is zero (not built, not staffed, out of reach or full; the playground only for homes with children). School: a child of school age who can't read and isn't enrolled. Water: the home has no water in reach. Rent: rent over 25% of income. No work: a working-age adult in the household and nobody in a town job. The old village's cohort is never counted.
3. **Families needed: 3, or a fifth of your households rounded up, whichever is more.** The same number decides "the need is gone": fewer than that still share it. So a petition ends when it would no longer be raised.
4. **The close's order:** the town is read, the close counter rises, petitions are settled (done, ignored, kept or broken), approval moves by the happiness step plus the endings, then new petitions are raised for topics that are not quiet.
5. **Endings as the spec says, one value tuned (9).** Done +4 (open, need gone), Kept +6 and Broken −8 (judged at the promise's close only, so a promise fixed early waits for its date), Ignored −2 (two closes with no answer; spec −4), Declined −2 at once. After an ending the topic is quiet for one close; after a decline, four.
6. **Approval** moves `(happiness − 70) ÷ 5`, toward zero, at most 4 either way, a whole number, 0–100. Its last six closes are kept for the sheet. It feeds only the pull (§3.7) and changes no tax, policy slot or vote (ADR 0004).
7. **Screens.** Petitions sit on the Town life tab: a row (who and where, how many families), Promise (opens a choice sheet for 1, 2 or 4 closes, each named by its season and the time to it, no card) and Decline (a card: approval −2, raised again after 4 closes). A promised petition shows its date and what keeping or breaking it does. The last three settled are listed. Each open petition rings the bell. Approval's last closes are a line chart (a larger picture than Preview 14's small spark).
8. **Save.** `Closes`, `ApprovalHistory`, `Petitions`, `NextPetitionId` and `QuietUntil` join `TownLifeState` in schema 22 (still the sprint's single bump); the schema 22 fixture is regenerated.

9. **Two numbers tuned from the bots (owner to confirm).** With the spec's values (ignored −4, neutral happiness 70) every hands-off bot Township, which answers no petition, fell to approval 15–27 in 12 towns (happiness 78 gave +1 a close; four or five topics standing cost 3 to 4 a close). Spec 026 §9 asks a town left alone for 50–65 (±10). Measured on the 12 bot towns: ignored −3 gave 25–34; ignored −2 gave 35–43; **ignored −2 with neutral happiness 65 gave 43–51 (the pull 110–114)**. Chosen: `ignored` −2 and `neutralHappiness` 65 in `township.json`. A player who answers keeps the spec's +4, +6, −8 and −2. Alternative if the owner prefers the spec's numbers: cap what petitions can take in a close.

## Consequences

- Approval now moves the pull; the balance pass measures the Township's pace and a town left alone (approval 50–65 by spec 026 §9) on 200 seeds.
- The first close at the Township may raise several petitions at once (no hall, watchman or playground yet); the balance pass watches how many a player meets.
