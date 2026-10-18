# 0036. Fonts under SIL OFL, the low-poly art direction, and the notification ask in the first session

- Status: Accepted
- Date: 2026-10-18
- Deciders: Keyur Patel
- Amends: ADR 0007 (allowed licences: SIL OFL for fonts), spec 007 §3.5 (when the notification prompt first appears)
- Related: ADR 0001 (setting), ADR 0015 (onboarding), ADR 0017 (code-first Unity, Kenney CC0 art), ADR 0024 (localisation), F00.6 (US-024 style check), F19.1 (Hindi), F19.3 (art pipeline)
- GDD sections: Art, audio and feel (Sourcing); UI/UX (Onboarding, Localisation)
- Note: ADR 0035 is reserved for the performance-spike result (US-022, ADR 0034 §9.8), written once the device numbers are in.

## Context

The review of specs 008 and 009 (2026-10-18) raised three decisions that change earlier ADRs or specs:

1. **Fonts.** ADR 0007 allows CC0, CC-BY and the Unity Asset Store licence. Almost every good free font is under the SIL Open Font License (OFL), including Noto Sans, which covers Latin, the ₹ sign and, as Noto Sans Devanagari, Hindi. The game is mostly English, with Hindi supported (F19.1).
2. **Art direction.** The US-024 style check put Kenney's two CC0 isometric packs side by side: the flat, bright low-poly *Isometric Landscape* and the textured, rustic *Isometric Miniature Farm* (Western barns). Mixed, they clash.
3. **The notification prompt.** Spec 007 asks after the first field harvest. In the first session the field crop is potato (24 h), so the first absence had no reminder; ADR 0015 invites the player when the long crop is sown.

## Decision

1. **SIL OFL is allowed for fonts only.** The game uses **Noto Sans** (and Noto Sans Devanagari when Hindi arrives). OFL fonts are bundled in the app, never sold on their own, and logged in `ASSETS_LICENSES.md` and the credits. Other assets keep ADR 0007's list.
2. **The low-poly landscape style is the base.** Kenney's *Isometric Landscape* sets the look (flat colours, crisp edges, Hay Day-like with the code-driven motion of spec 008). Setting-specific assets (crops, huts, the well, people, trees) are made in Blender to match it (F19.3); until then, simple placeholder sprites in the same flat style. The textured miniature-farm pack is not used for the game's look.
3. **The first notification prompt comes in the first session,** in the letter's `comeBack` chapter right after the long crop is sown, with its concrete reason ("Your potato will be ready at 14:05 tomorrow. Want a nudge when it is?"). If declined, spec 007's second ask after the first season close stays; then never.

## Consequences

- **GDD:** Sourcing allows SIL OFL for fonts; the Art section names the low-poly base.
- **Spec 007 §3.5:** the opt-in rule changes as in decision 3.
- **Spec 008 §3.11:** Noto Sans; **spec 009 §3.2:** step 7 carries the prompt.
- **US-024:** the miniature-farm sprites stay only in the throwaway style check (`Phase0/`), removed with it.
