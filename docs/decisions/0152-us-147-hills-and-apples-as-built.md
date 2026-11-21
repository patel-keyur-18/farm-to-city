# 0152. US-147 hills and apples as built: the hills open with the Township, apple orchards on hill land only, a Monsoon harvest of 3,000 kg an acre that keeps in the cold store

- Status: Accepted (the owner approved Preview 15 and the build, 2026-11-21)
- Date: 2026-11-21
- Deciders: Keyur Patel
- Related: spec 025 §3.5, §3.6, ADR 0021, ADR 0090, ADR 0125, ADR 0134, ADR 0144 §6

## Context

Spec 025 §3.5's first figures did not hold up (150 kg a tree at real density is about 24 t an acre against India's 3 to 4; 8 coins a kg is far below mango's 50; apples spoil in days, not hours). ADR 0144 asked for four gaps to be closed with the owner before code. They were, one per message, on 2026-11-21, and Preview 15 showed the screens.

## Decisions

1. **Yield and price (G1).** 100 trees an acre, 30 kg a tree a season once bearing (3,000 kg an acre), in the Monsoon only. Base price 60 a kg, elasticity 1.2.
2. **Sapling and years (G2).** Sapling 250 (25,000 an acre with planting), 8 seasons (32 days) to a full first batch, young-row intercrops at half yield (ADR 0090), mango's moisture band 25 to 70, labour 1.0 young and 2.0 bearing.
3. **Keeping (G3).** Seasonal factors Spring 1.15, Summer 1.25, Monsoon 0.85, Winter 1.00. **Hut spoilage 4% a day**, raised from G1's 0.8% (a candid correction: at 0.8% the hut loses about 3% a season, so the cold store would save almost nothing and storing apples would be pointless). The cold store keeps apples at a quarter of the hut's rate, the same share as mango and guava. The trader takes 300 kg a day.
4. **How the hills open (G4).** With the Township and the ring (`LandRules.Locked` is the ring's rule for every parcel with `opens`), bought like other land at `locationFactor` 0.75. For farming and orchards only: `LandRules.Tillable` (farm or hill) is used by sowing, orchards, the fallow and season passes, a hand's orders and the field lists; houses, shops, zoning, builders and livestock still need a *farm* acre, so a hill acre is excluded without a new rule. No lane up the hills.
5. **Hill land only.** `TreeDef.HillOnly` (`terrain: "hill"`) refuses a valley acre with `RejectReason.HillOnly` ("Hill land only."), and a yard slot or field-edge slot with `TerrainNotAllowed`. Other trees grow on either. The planting sheet lists the hill tree first and shows it greyed on a valley acre; it is hidden until the Township opens the hills.
6. **Screens.** An owned hill acre is a field on the map (`TileKind.Farm`); one for sale is hill ground with a mound on one acre in three. Your land › For sale reads "Hills · 2 acres" with "Apples grow here · no lane yet" (or "Not next to your land yet"), and its price's ? names the distance. Art: `tree_apple_*` and `icon_good_apple` rendered in Blender.

7. **Two small differences from Preview 15, found in the Editor.** The orchard's *Pays for itself* counts the trees' cost only, like every orchard (the Cost row just above it): a bearing apple acre says "around the 1st harvest" (a tree that fruits once a year names the harvest, not the season), while the preview's "2nd harvest" also counted the land and the care in the Why?. A hill acre shows no irrigation rows (it is rain-fed: the pump offered on the well made no sense up a hill). Strings "A Apple orchard" and "A Apple takes" lost their "A".

## Consequences

- No new save field. Content only (a tree, two goods, region rows, a cold-store share). The goods list grows by two, so the schema 22 fixture's hash changed and was regenerated; older schemas load unchanged.
- **Measured:** the cold store's worked hold for apple is *Monsoon to Summer, +12.69 a kg, about 24% on the money kept in one season*. That is well above mango's few percent. It stays bounded by the orchard (3,000 kg an acre, 20 t of space needs about 7 acres) and is a candidate for the balance pass: the exploit bots check that no stock-and-hold loop pays.
- `AppleTests` (4) and `AppleViewTests` (5); three stale tests updated (apple was an unknown tree).
