# 0005. Orientation: landscape only

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- GDD sections: UI/UX and screens (Mobile ergonomics), Open decisions and risks

## Context

The GDD left the orientation open: landscape only, or a landscape map with portrait menus. The real-time check-in loop from ADR 0002 makes quick one-handed sessions more common, so portrait-first was considered as well.

## Options

1. **Landscape only.** One layout to build and test, and it suits a wide isometric town. Reference games (Hay Day, Township) are landscape. The downside is that one-handed play is harder.
2. **A landscape map with portrait menus.** This is good for reading reports, but it roughly doubles the layout and test work, and rotating mid-session is jarring.
3. **Portrait-first.** This is best for one hand, but a growing isometric city is cramped in portrait.

## Decision

Option 1: landscape only, on phones and tablets.

## Consequences

**One-handed play** relies on these existing rules:
- menus open as bottom sheets
- the main actions (collect all, harvest, build) sit in thumb reach in the bottom corners
- touch targets are at least 44 pt

**Layouts must handle:**
- phone aspect ratios from 16:9 to 21:9
- 4:3 tablets
- notch and home-indicator safe areas on both platforms

**Both landscape directions** (rotated left and right) are supported, so the layout follows how the player holds the device.

**Report screens** (statements, workforce) are designed for landscape from the start, with wide tables and side-by-side charts.

**Portrait** could be revisited after launch for specific screens. This decision doesn't block it.
