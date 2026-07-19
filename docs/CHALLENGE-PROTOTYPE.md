# Challenge mode prototype

The local `prototype/challenge-mode` branch establishes the configuration, persistence, UI, timing policy, and first structural detectors for regular Challenges. It deliberately does not run gameplay input yet.

## Implemented

- A Challenges workspace page with local presets and fixed Trait, Stat, and Sprite ordering.
- Five map profiles: School Grounds, Flower Forest, Rose Kingdom, Fairy King Forest, and King's Tomb.
- Per-map camera, before-start placement, after-start placement, and elapsed-seconds configuration.
- Wait-until-reset or run-Expeditions cooldown behavior.
- Global half-hour reset calculation for `xx:00` and `xx:30`.
- Structural recognition for the game-mode selector, Challenge list, available Challenge details, ready and post-match party previews, Start Game, Challenge victory, and Challenge defeat.
- Action geometry tied to recognized buttons, including hover-tooltip fallbacks for Select Stage.

The game-mode selector, Start Game dialog, and defeat screen are shared with Expeditions. A future Challenge runner must consume those detections only from the expected transition state. They are not sufficient evidence that a Challenge run is active by themselves.

## Dataset gate

The Start button remains disabled until map identity and the complete loop have cross-state coverage. The next dataset should contain canonical 808 by 611 Roblox client captures for:

- all five maps in the list, detail, preview, prestart, active-gameplay, victory, and defeat states;
- each regular Challenge selector entry available and on cooldown;
- the bottom-left Play button before and after hover, followed by the party preview and Change Gamemode transition;
- ordinary active gameplay around both placement phases;
- negative frames from lobby, Expeditions, rewards, disconnect, and AFK recovery;
- a complete manual run including every transition and cursor hover state.

Do not include the Roblox title bar, desktop, notifications, account names, chat, or webhook data. Keep the original 808 by 611 pixels and the capture manifest.
