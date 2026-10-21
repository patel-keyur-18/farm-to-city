# Asset licences

Every third-party asset in the game (art, audio, fonts) is listed here with its source and licence (ADR 0007). The project uses CC0 assets and its own Blender sprites only, so most rows read "CC0" or "Original".

Add a row in the same commit that adds the asset.

| Asset (path) | Source (URL or "Original") | Author | Licence | Notes |
| --- | --- | --- | --- | --- |
| `unity/FarmToCity/Assets/Resources/Sounds/harvest_pop.ogg`, `ready.ogg`, `urgent.ogg` | https://kenney.nl/assets/interface-sounds (`drop_002`, `confirmation_001`, `bong_001`) | Kenney (www.kenney.nl) | CC0 1.0 | Feedback sounds, US-087 (ADR 0043 §10) |
| `unity/FarmToCity/Assets/Resources/Sounds/harvest_rustle.ogg`, `coins_in.ogg` | https://kenney.nl/assets/rpg-audio (`cloth2`, `handleCoins`) | Kenney (www.kenney.nl) | CC0 1.0 | Feedback sounds, US-087 |
| `unity/FarmToCity/Assets/Resources/Sounds/coins_out.ogg` | https://kenney.nl/assets/casino-audio (`chip-lay-1`) | Kenney (www.kenney.nl) | CC0 1.0 | Feedback sounds, US-087 |
| `unity/FarmToCity/Assets/Resources/Art/Kenney/IsometricLandscape/landscapeTiles_059.png`, `_066`, `_067`, `_083` | https://kenney.nl/assets/isometric-tiles-landscape | Kenney (www.kenney.nl) | CC0 1.0 | Sand, water, grass and soil tiles; US-024 style check. Removed from the game in Sprint 6 (the ground is a Blender render, ADR 0041); the screenshots below still show them |
| `unity/FarmToCity/Assets/Resources/Fonts/Baloo2-Bold.ttf` | https://github.com/google/fonts/tree/main/ofl/baloo2 (variable font `Baloo2[wght].ttf`, the Bold instance made with fontTools `varLib.instancer wght=700`) | The Baloo 2 Project Authors (Ekta Type) | SIL OFL 1.1 | Headings, buttons, numbers (ADR 0036, 0038). `Baloo2-OFL.txt` alongside. Bundled in the app, not sold separately |
| `unity/FarmToCity/Assets/Resources/Fonts/NotoSans-Regular.ttf`, `NotoSans-Bold.ttf` | https://github.com/googlefonts/noto-fonts (hinted/ttf/NotoSans) | The Noto Project Authors | SIL OFL 1.1 | Body text (ADR 0036). `NotoSans-OFL.txt` alongside |
| `docs/art/s03-style-spring.png`, `docs/art/s03-style-monsoon.png` | Original (screenshots of the style check) | Claude | Project | Built from the Kenney CC0 tiles above |
| `unity/FarmToCity/Assets/Resources/Art/Blender/*.png`, `sprites.json` | Original: modelled in code and rendered by `tools/blender/render.py` (Blender 5.2 LTS, GPL tool; its output is ours) | Claude | Project | Every map sprite (ground, crops, beds, trees, props, buildings, markers), people with work and walk frames, and every icon (ADR 0039, ADR 0041, US-183, US-171); the village, roads, river bank, commons, bunds and the valley edge (spec 010, US-191) |
