# Always Be Searchin' changelog

## 0.1.1

- The version in the load message now always matches the plugin's version.
- The plugin's ID is now `solrlabs.aska.alwaysbesearchin`, so settings live in `BepInEx/config/solrlabs.aska.alwaysbesearchin.cfg`.
- The Thunderstore package is named Always_Be_Searchin, shown as "Always Be Searchin".

## 0.1.0

- First release.
- Gatherable items near the player always glow softly.
- Pressing Z still gives the game's full Resource Finder pulse; the soft glow returns as it fades.
- Glow strength follows the game's day and night: settings for day strength (default 0.17), night strength (default 0.08), radius and color (default yellow, `#FFFF33`).
- Villagers, players and live creatures, and what they carry, can be kept out of the glow, including during Z.
- Items stored in or on buildings (storage, cooking pots, racks) can also be kept out of the glow (`ExcludeStructures`, default on).
- Plants (ferns, bushes, grass and similar) glow much brighter than rocks, so a plant multiplier (`PlantStrength`, default 0.5, for the types in `PlantTypes`) turns them down, including during Z. Types in `BoostTypes` (default `Flax`), which are hard to see, are instead multiplied by `BoostStrength` (default 3) and never dimmed.
