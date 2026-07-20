# Challenge mode

Challenge mode automates the three regular Anime Expeditions Challenges that reset globally at `xx:00` and `xx:30`: Trait, Stat, and Sprite. Their selector order is fixed, while the map rotates among School Grounds, Flower Forest, Rose Kingdom, Fairy King Forest, and King's Tomb.

## Workflow

- Starts from the lobby, post-match preview, game-mode selector, or Challenge selector.
- Tries only the enabled Challenge types and skips an entry when **Select Stage** is unavailable.
- Recognizes the selected map and loads that map's camera model and placement configuration.
- Runs a before-start placement model, starts the match, then optionally runs a second placement model after the configured elapsed time.
- Closes Victory and returns through **Change Gamemode**. Defeat retries are configurable and default to zero.
- Clears per-entry attempts at every global half-hour reset. A no-retry defeat can therefore be attempted again when a new Challenge appears.
- Either waits during cooldown or runs a configured Expeditions preset until the next reset.
- Treats all three entries remaining unavailable across a complete global reset as daily-limit evidence, then waits until midnight UTC.

The optional Discord webhook reports monitoring start, match attempts, Victory or Defeat with a Roblox screenshot, recovery, and reset or daily-limit waiting. It uses the same DPAPI-protected storage and Components V2 payload path as Expeditions.

## Detection and input safety

The workflow recognizes the game-mode selector, active and unavailable Challenge lists, available and cooldown details, both preview layouts, Start Game, Victory, and Defeat. It uses stable panel geometry, headings, dividers, button shapes, and map thumbnails instead of player names, reward rarity colors, or changing background artwork.

Selector rows are clicked through their map artwork, away from reward icons that open item tooltips. Every transition is consumed only from its expected runner state; shared Start Game and Defeat visuals are not sufficient by themselves to begin Challenge automation.

## Regression dataset

The checked-in dataset contains 67 selective Challenge fixtures from multiple players, PCs, Roblox UI scales, all five maps, active and cooldown entries, gameplay, terminal screens, and Expeditions handoff states. Focused diagnostics cover private-party previews, dimmed and unavailable selectors, reward tooltips, hovered controls, and bright game-mode artwork.

Fixtures must remain 808 by 611 Roblox client captures. Do not include the Roblox title bar, desktop, notifications, account names, chat, or webhook data. See `datasets/anime-expeditions/challenges/README.md` for provenance and documented redactions.
