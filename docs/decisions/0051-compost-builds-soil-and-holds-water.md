# 0051. Compost builds the soil and holds water; fertility shown to one decimal (Sprint 10 review)

- Status: Accepted
- Date: 2026-10-21
- Deciders: Keyur Patel
- Amends: spec 003 §3.10, §4 (`FertiliserDef`), §6, §8 (AC-12; AC-39 new); spec 002 §3.2 (step 1); spec 008 §3.7 (the sow sheet)
- Related: ADR 0014 (chemical fertiliser −3 a season, unchanged)
- GDD sections: Farming (Fertiliser)

## Context

The owner reported that sowing with compost didn't improve the field, although it costs more and the sow sheet said "builds up the soil". What the code did:

1. **Compost added less than any crop takes out.** Compost gave +2 fertility a season. Wheat and rice take 4, potato 5, so a composted field still ended each crop lower. For one rice cycle: −0.5 with compost, −1.75 with chemical. Only radish (2) broke even.
2. **The changes were invisible.** Fertility was shown as a whole number on the field sheet, the sow sheet and the *Why?*, so a change of +0.04 or −0.5 showed as no change.
3. **Compost did nothing for water.** The owner expected it to raise moisture.

Realism: about 2 tonnes of compost (farmyard manure) per acre a season roughly replaces the nutrients a crop takes out, and the organic matter lets soil hold water. It does not add water.

## Decisions

1. **Compost +6 fertility per season of growth** (was +2), applied pro rata per cycle as before. With compost, every crop leaves the soil a little better: rice or wheat +0.5 a full season, potato +1.
2. **Compost holds water.** While a crop sown with compost grows, the soil under it dries 20% more slowly: `FertiliserDef.dryingFactor` 0.8 multiplies the drying step of spec 002 §3.2 (`d < 0`). Rain, drainage and the well are unchanged. It helps rain-fed fields through dry spells. Other fertilisers have 1.0 (the field is optional in content and defaults to 1.0).
3. **Fertility is shown to one decimal** on the field sheet, the sow sheet and the *Why?* sheets. The fertility *Why?* rows read "+0.4 points".
4. **The sow sheet shows the real effect.** Each crop row shows "Soil 45.0 → 45.3": the field's fertility after this crop with the chosen fertiliser (the crop's use and the fertiliser's change, each × cycleShare). It is green when the soil holds or rises, amber when it falls. The fertiliser chips no longer say "builds up / wears out the soil": they read "+5% harvest · holds water" (compost) and "+25% harvest" (chemical).
5. **Chemical fertiliser is unchanged** (+25% harvest, −3 a season, ADR 0014).

## Consequences

- The trade-off the game teaches is now visible on every sowing: chemical gives more harvest now and the sheet shows the soil falling; compost costs about the same per sowing, gives less now, and the soil rises.
- **The economy (gate, 100 seeds).** The bots compost tired soil, which no longer runs down:
  - The median farm profit a season in the first year rises from 25,765 to 33,179 coins, and the competent bot's net worth after 2 years from 1,363,039 to 1,443,365 (+5.9%).
  - Every stress bot stays within ADR 0016's 1.20 × competent (the highest is 0.997). The Hamlet still unlocks on day 5.4 (median).
  - The strategic-defaulter bot now earns enough to repay on time in some runs: it defaults in 54 of 100 runs (was 84). Every default is still foreclosed. The gate now checks that directly: at least half the runs default, and every default is foreclosed (`GateTests.DebtBots_UseCredit`).
- Spec 003 AC-12 is restated (compost +6 × cycleShare) and AC-39 is new (`CropTests.Compost_HoldsWater`). The golden replays use frozen content and are unchanged.
- Livestock (Phase 2) will make compost from dung, which gives compost a source on the farm.
