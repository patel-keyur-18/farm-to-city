# 0007. Zero-budget production: art sourcing, reviews and personal distribution

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0001 (Indian-inspired setting), ADR 0006 (free with a one-time unlock)
- GDD sections: Art, audio and feel, Learning layer (Accuracy), Offline play and saves, Technical architecture, Roadmap, Open decisions and risks

## Context

The owner will not spend money on development. The GDD's art options (asset packs then a commissioned artist, or an artist from day one) both assumed a budget, and so did its "warm, hand-painted 2D" direction, its expert reviews and its store-based platform features (iCloud, Play Games saves, in-app purchases).

## Options for art

1. **Paid packs, then a commissioned artist.** Ruled out: it costs money.
2. **The owner hand-paints everything.** This costs nothing, but hand-painting is the hardest style to keep consistent, and the owner is not an artist.
3. **AI-generated art as shipped assets.** The style isn't consistent across hundreds of assets, purely AI-generated images may not be protected by copyright, and the stores' rules are still changing.
4. **CC0 base packs plus the owner's own low-poly Blender models, rendered to isometric sprites.** Free and consistent, because a fixed render rig sets the style. It suits an engineer.

## Decision

### Art: option 4

- **Base set.** CC0 packs such as Kenney.nl isometric tiles, buildings, characters and UI, plus free Unity Asset Store items under the standard Asset Store licence.
- **Setting-specific assets** (huts, wells, paddy fields, tempos, clothing) are modelled low-poly in Blender and rendered to isometric sprites with a fixed camera and light rig. Seasonal palettes and condition states (new, worn, cracked) are rendered as automatic variants.
- **Art direction** changes from "warm, hand-painted 2D" to **"clean, stylised isometric"**, matched to the base packs.
- **Licence rule.** Only CC0, CC-BY (credited on a credits screen) or the Unity Asset Store licence. Never CC-BY-SA, non-commercial or "personal use only". Every third-party asset is logged in `ASSETS_LICENSES.md` with its source and licence.
- **AI-generated images** are allowed for concepts and reference only, never as shipped assets.
- **Audio.** CC0 sound effects (Kenney, freesound.org with the CC0 filter), and CC0 or CC-BY music.
- **A revenue-share artist** may be considered later. Nothing is paid upfront.

### Reviews

- The finance-accuracy and cultural-accuracy reviews are done by the owner's friends and relatives who have the expertise, not by paid experts.

### Distribution: personal builds only, for now

- **iOS:** a free Apple developer account. Builds are re-signed from Xcode every 7 days, within the free account's 3-app limit. The bundle ID and signing team stay fixed, so reinstalling keeps the save data.
- **Android:** sideloaded APKs signed with the owner's own key.
- **No store submission is planned.** Store fees ($99 a year for Apple, $25 once for Google) and store-only features are deferred until the owner decides to publish.

## Consequences

- **Platform services sit behind interfaces**, with personal-build implementations:

  | Service | Personal build | Later (if published) |
  | --- | --- | --- |
  | Cloud backup | Manual export and import of the save file | iCloud, Play Games Saved Games |
  | Purchases (ADR 0006) | **None.** `IEntitlementService` has an always-unlocked implementation: the full game is open and no unlock offer is shown | StoreKit 2 and Play Billing implementations of the same interface |
  | Local notifications | Fully supported | Same |

- The Phase 0 **art test** becomes a **style-pipeline test**: render one hut, one field and one citizen through the Blender rig, and place them beside Kenney tiles to check that they match.
- A Unity iOS build needs a Mac with Xcode, which the owner provides.
- **In-app purchases are not built now.** The provision is the `IEntitlementService` port, which the era-unlock check reads. Adding real purchases later means one new platform implementation plus the unlock-offer screen, with no simulation changes.
- When an iOS build expires, the app won't launch until it is re-signed. The save is unaffected, and the 72-hour away cap (ADR 0002) bounds what the town does in the meantime.
