# 0065. The village's places stand where their fronts face the camera

- Status: Accepted
- Date: 2026-10-23
- Deciders: Keyur Patel
- Amends: ADR 0061 §4 (the dairy co-op's acre), spec 010 §3.3 (which acre each place stands on)
- Related: ADR 0061 (the village redrawn), ADR 0064 (map layouts previewed to the owner)

## Context

Redrawing the village (US-206), every building faces its street. The map's camera sees only each building's south (-y) and east (+x) sides, so a shop facing north or west shows a blank back: in the first preview the chemist, the council hall and the dairy couldn't be told from houses.

## Decision

1. **Shops and public buildings stand where their fronts face the camera**: along the main street's north side the council hall, the grocer and the tea stall; along the side lane's west side the dealer, the chemist and the dairy co-op; the temple faces the pond. Houses keep a mix of facings for a natural look.
2. **The dairy co-op moves** from the seventh house's acre (110, off any street) to the side lane by the lane from the farm (108), where milk carts pass, as real collection centres stand.
3. The 16 village acres, their tiles and the place count are unchanged; places are content only (no save state), so no migration.

## Consequences

- `content/map.json` places reassigned (house_7 gone, `dairy` on 108); spec 010 §3.3 updated.
- The layout is presentation code (`MapPresenter.Village`), previewed and approved by the owner (`docs/art/s12-village-preview-*.png`).
