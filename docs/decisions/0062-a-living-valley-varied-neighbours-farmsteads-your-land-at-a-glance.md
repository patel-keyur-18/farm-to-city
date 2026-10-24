# 0062. A living valley: varied neighbours' land, farmsteads, your land at a glance

- Status: Accepted
- Date: 2026-10-22
- Deciders: Keyur Patel
- Amends: spec 010 §3.7 (neighbours' land and the owned-land boundary, presentation only)
- Related: ADR 0053 (neighbours, for sale or not for sale yet), ADR 0061 (the village redrawn), spec 010 G6 (no desaturation of unowned land)
- GDD sections: Land, farming and livestock (the village around the farm); Art, audio and feel

## Context

Looking at the Sprint 11 map, the owner found the valley unlike a real village: every neighbour's acre sown, the fields running together, no villagers' homes among them, and the player's own land hard to tell apart without tapping.

## Decision

1. **Neighbours' land varies** (presentation only; neighbours' land has no sim state). Each neighbour's acre takes a look fixed by its soil, fertility and a seeded draw: poor or sandy acres mostly scrub or fallow, good loam mostly sown; ploughed fields waiting, mustard yellow in Winter, sugarcane, a mango grove or two, haystacks and a tube-well hut.
2. **Farmsteads among the fields**, only on acres that are not for sale yet and at the map's edge (a house on a buyable acre would need rules for buying it). They are neighbours' homes, not the player's citizens (ADR 0061).
3. **Your land at a glance**, keeping spec 010's no-desaturation: a continuous low mud wall with a green hedge round everything the player owns, with a gate where the lane comes in, redrawn on every purchase and sale; corner stones on each owned acre; fresh dark bunds on the player's fields against neighbours' worn, grassy ones; for-sale signs as now.
4. **In Sprint 12** as US-207 (5 points), after the cattle stories (the owner approved the order and keeping everything in Sprint 12).

## Consequences

- Sprint 12 grows to 49 points.
- Blender work: scrub, fallow, mustard, sugarcane, a grove, farmsteads, haystacks, a tube-well hut, the hedge-and-wall boundary and corner stones.
