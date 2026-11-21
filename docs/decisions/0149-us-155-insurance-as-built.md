# 0149. US-155 insurance as built: luck covered and neglect not, three policies, claims through the books

- Status: Accepted (the owner approved US-155, 2026-11-21)
- Date: 2026-11-21
- Deciders: Keyur Patel
- Related: spec 027 §3.5–§3.6, §7, Preview 13 (approved 2026-11-21), ADR 0141, 0148

## Context

Spec 027 sets crop, property and business insurance. Preview 13 drew six screens and the owner approved it. Building it found three things the spec and the preview did not settle, and one gap in US-154.

## Decisions

1. **Crop insurance** is one switch. A field crop sown while it is on is insured at that sowing (`Crop.Insured`, `SumInsured`, `Premium`); fodder and orchards are not. The sum insured is the sow sheet's harvest value at the trader's floor. The premium is `sum × (pest part + 1.5%) × 1.30`, where the pest part is the crop's pest risk at sowing (its repeats, the season's usual wet share from `insurance.json`) × 0.23. It is charged from cash at once; cash short, the crop goes uninsured and the away card says so. At readiness the insured index is `Weather × CropHealth` (health already holds pests and events), and under 0.80 the claim is `sum × (0.80 − index)`. Soil, water, care, seed and fertiliser are never in the index. A claim says what hurt the crop most: an event (the crop remembers the last event that hit it, `Crop.HitBy`), pests, or the season's weather.
2. **Property and business insurance** are season policies charged at the close. Switched on mid-season, they start at the next close; switched off, they end at once with no refund. A policy that cannot be paid at the close ends and is never a debt. Cover is kept as `Since`, the time it has run unbroken; a fire pays only when the cover began no later than its warning (`EventsState.FireInsured` is read at the strike). The property premium is 0.2% of the book value of the buildings on your own books; a business pays 0.2% of its building and equipment plus 3% of its last season's profit, from its own cash.
3. **Claims are paid when the repair is paid.** A fire pays 90% of what was actually paid (half the bill if repaired slowly), so a slow repair can never earn a profit. A business also gets 80% of the profit of the days it stays shut, into its own books; a supplier strike's *Pause the line* pays the same for its 3 days. The worked example in spec 027 divided a season's profit by 16 days; a season is 4 days, so it now divides by 4 (spec corrected).
4. **Found in US-154 and fixed here: a business's building never burned.** The fire chose only from the buildings on your own books, and a business's building lives in its own books, so the fire's "a business building shuts 2 days" could not happen and business insurance could never pay. A fire now draws from open businesses too, by their book value (`DisasterTarget2` = 1), shuts the business for the downtime, and its repair card carries the business.
5. **The books.** Four new accounts (schema 22): *Insurance claims* (4045, farm income), *Crop insurance premiums* (5035, farm cost), *Insurance claims received* (4355, other income) and *Insurance premiums* (5287, other costs). Insurance is never passive income. Also fixed from US-154: the *Your season* page had no words for *Regional grants*, *Disaster loss* and *What you paid for events*; they have them now.
6. **Screens.** Preview 13 as drawn, with one change: its "close card" with keep or drop switches became the same switches on the Money page, next to *Next season, already due*, which lists the property premium and, beside, each business's own. There is no new card to stop the close, and the switches work all season. The sow sheet shows the premium under each crop (or that the cash won't cover it) with a ?, a business's sheet has its row, and the repair card says what the insurance pays back; its confirm card shows the bill, the refund and what you pay.
7. **Names.** The events are `InsuranceStarted` and `InsuranceLapsed` (`PolicyStarted` is the treasury's), `PremiumCharged`, `ClaimPaid` and `CropNotInsured`. Commands: `SetCropInsurance`, `SetPropertyInsurance`, `SetBusinessInsurance` (everyday, no confirm card; a switch costs nothing). Reject reason `NoBuildings`.

## Consequences

- Save schema 22 gains `InsuranceState` and the crop's insurance fields; the fixture is regenerated. A town with nothing switched on hashes as before.
- Solar panels, listed in spec 027 among the buildings, are not buildings in the sim and are not covered or burned.
- `eventPart` (1.5%), the loadings and the 0.2% rates are placeholders for the balance pass, with the *insure everything* bot (`GateTests.InsureEverything`).
