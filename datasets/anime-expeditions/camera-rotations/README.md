# Camera rotation fixtures

These 808 by 611 client-area PNGs were sampled from seven continuous right-arrow yaw captures recorded on July 21, 2026. Roblox was fully zoomed out, pitched downward, shift lock was enabled, and the Start Game dialog remained open.

- `Expedition_Map1` through `Expedition_Map3` cover the three Expedition maps.
- `Story_Map1` through `Story_Map4` cover the same maps and UI used by regular Challenges.
- `goal.png` is the starting view.
- `wrong-yaw.png` is a later frame with a clearly different yaw.

The complete rotation archives are intentionally not duplicated here. The curated pairs are sufficient for deterministic automatic-region and wrong-yaw regression tests without adding roughly two gigabytes of redundant frames.

## Runtime projection drift

`RuntimeProjectionDrift` contains three 304 by 192 grayscale composites derived on July 22, 2026 from a reported Deep Debug run at the canonical client size:

- `reference.png` is the saved four-region goal composite from camera-model setup.
- `matching-projection-shift.png` applies the model's saved regions and normal preprocessing to runtime frame 486, where the correct yaw was rendered roughly 8 to 10 pixels higher across the regions.
- `wrong-yaw.png` applies the same process to runtime frame 564, a nearby incorrect yaw from the same alignment scan.

Only world-region composites are retained; fixed controls, player identity, desktop chrome, and unrelated screen content are excluded. The regression requires at least three of the four regions to agree on the expanded vertical registration and keeps the wrong-yaw composite below the saved model's 71.59% success threshold.
