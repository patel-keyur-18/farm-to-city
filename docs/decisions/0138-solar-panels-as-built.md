# 0138. Solar panels as built: one 60,000 panel for each pumped source, taking 80% off its power, and a payback of 75 seasons against a pump that lasts 40, kept as it is for the balance pass

- Status: Accepted (owner approved the model and Preview 11, and chose to keep US-257 in Sprint 23 at spec 025's numbers, to be balanced later, 2026-11-19)
- Date: 2026-11-19
- Deciders: Keyur Patel
- Amends: spec 025 §3.8 (US-257), ADR 0125 (G7), ADR 0107 §15
- Related: ADR 0075, 0086, 0087, 0136, 0137; spec 013 §3.1

## Context

Spec 025 §3.8 gave "a solar pump that cuts pumping power by 80% (cost 60,000)", and ADR 0125 G7 said it stays only if its payback is under about 20 seasons. The arithmetic from `content/` says otherwise: the pump on the well waters 4 acres at 250 an acre a season power, 1,000 a season, so 80% saves 800; a 60,000 panel pays back in **75 seasons**, and the pump itself lasts 40 (`buildings.json`); one panel for both sources would still be 37.5. Real solar pumps cost ₹2 to 3 lakh and their payback rests on electricity subsidies this game does not model. The owner chose to build it anyway at the spec's numbers and balance it later.

## Decision

1. **A panel for each pumped source** (the pump on the well, the borewell), built once each: `FitSolarPanel(source)`, 60,000 × index (`irrigation.pump` and `borewell` `solarCost`), paid at once, capitalised to Buildings (1600), kept **at cost with no depreciation** like the silage pit. It needs the source built and its era (the pump Hamlet, the borewell Village). **It takes `solarCut` = 80% off that source's power** (250 an acre a season × the acres on its list, paid at the close): `IrrigationRules.PumpPower` is `PowerWithout` less `Saving`. Acres, the moisture floor, drip and the canal are untouched; the "next season, already due" line is the lower sum. One panel for both sources and a panel before the source exists were left out.
2. **The honest sum, in words.** A panel's saving is the cut of the power of the acres the source waters now (`PanelSaving`; with none on its list it saves nothing and says so); `PaybackSeasons` is the cost over the saving, rounded up so it never looks quicker than it is; the *Why?* sets it beside the source's own life (`LifeSeasons`) and a plain pump's price (15,255 at index 1.017): at 4 acres **75 seasons against 40, and 150 with only the well's 2 acres**; the verdict says the panel would not pay for itself before the source wears out and that solar pays where power is dear. It is left like this on purpose, for the balance pass.
3. **Where it is.** A picture button on any field the source waters, beside the water buttons: *Solar panel for the pump / the borewell*, its price and "saves about N a season", a tap confirm card (*Fit a solar panel?*, the source as its subtitle); once fitted the button goes, the field's *Irrigation* row says *The pump (solar)* and a *Solar panel* row says "Fitted · power 80% lower: 208 a season instead of 1,041". The irrigation *Why?* gains the panel's rows. Art: one Blender render, a blue panel on a steel stand with a small controller box (`solar_panel` for the map, `icon_solar_panel` for the button), drawn beside the well and beside the borewell's room.
4. **Selling the land.** A borewell's panel goes with the borewell at what it cost (`Release`): a sold or seized borewell parcel returns it with the land, so the books still reconcile. The well's panel has nothing to go with: the well never leaves.

## Consequences

- Save schema 21: `IrrigationState.PumpSolar` and `BorewellSolar` (the cost paid, 0 for none; written from schema 21, hashed once a panel is fitted); the schema 21 fixtures were regenerated; older saves read unchanged. `FitSolarPanel` is a new command tag (appended), `SolarPanelFitted` a new event.
- Content: `farming.json` `irrigation.pump` and `irrigation.borewell` gain `solarCost` 60,000 and `solarCut` 0.8.
- Tests: `SolarPanelTests` (6: refusals and the cost, 80% off and the close's lower payment, a panel for each source, the 75-against-40 payback and the saving on no acres, the borewell's panel with its land, the save), `SolarPanelViewTests` (4), the irrigation field-card test's button lists, and the confirm and string coverage. Editor tour by scripted touch: `docs/art/s23-257-*.png`. Render statistics: the flags (triangles +32 to +46%) are this Editor town's own irrigation scenery (6 pump outlets, a borewell room), which earlier readings did not have; hiding the two panels takes 1 draw call and 5 triangles each off, so the panels are 2 sprites.
- **Balance to read at the sprint-end pass and later:** the panel's 75 seasons against the pump's 40 (price, the cut, or a power price that rises); whether anyone buys it; PM-KUSUM-style subsidy (the spec's "half subsidised") is not modelled.
