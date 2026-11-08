# Always Be Searchin'

Gatherable items near you always glow softly, so you can spot resources as you walk. Pressing Z still gives the game's full Resource Finder pulse.

## Settings

Edit `BepInEx/config/solrlabs.aska.alwaysbesearchin.cfg` (created on first launch), section `[General]`.

| Setting | Default | What it does |
| --- | --- | --- |
| `DayStrength` | `0.17` | Strength of the always-on glow in full daylight, 0 to 1. |
| `NightStrength` | `0.08` | Strength of the always-on glow at night, 0 to 1. The glow blends between the two with the game's day and night. Set both to 0 to turn the always-on glow off; Z then works as in the base game. |
| `GlowRadius` | `60` | How far from you items glow, in metres (5 to 200). |
| `GlowColor` | `#FFFF33` | Glow color as `#RRGGBB` (default is yellow). Leave empty to use the Resource Finder color from the game's settings. |
| `ExcludeVillagers` | `true` | Keeps villagers, players and live creatures, and what they carry, out of the glow, including during Z. |
| `ExcludeStructures` | `true` | Keeps items stored in or on buildings (storage, cooking pots, racks) out of the glow. Items on the landscape still glow. |
| `PlantStrength` | `0.5` | Multiplier on the glow of plants (ferns, bushes, grass and similar), which glow much brighter than rocks. 1 leaves them as the game draws them. Also applies during Z. |
| `PlantTypes` | `Fern,Bush,Grass,Reed,Flower,Lupin` | Comma-separated name fragments of the vegetation types that PlantStrength applies to. |
| `BoostTypes` | `Flax` | Comma-separated name fragments of vegetation types whose glow is multiplied by BoostStrength (never dimmed by PlantStrength). |
| `BoostStrength` | `3` | Multiplier on the glow of BoostTypes, for items that are hard to see (1 = as the game draws them, up to 10). |

Changing `ExcludeVillagers` from on to off while playing takes full effect after a restart.

## Other mods

Mods that change night lighting (for example BrighterMoonlight) can make the night glow look stronger or weaker. Adjust `NightStrength` to taste.

## How Z behaves

While Z's pulse is stronger than the always-on glow, the game's own pulse shows untouched, in your finder color. As it fades, the soft glow returns.

## Multiplayer

The glow is purely visual and local. Nothing is sent over the network, other players are unaffected, and they do not need the mod.

## Install

Install through a mod manager such as [Pulsar](https://pulsarmods.com), Thunderstore Mod Manager or r2modman. It also installs the required BepInEx pack.

## Building from source

You need the .NET SDK and the game with BepInEx installed.

- `dotnet build AlwaysBeSearchin.csproj -c Release` builds the plugin. `AskaDir` overrides the game path: `-p:AskaDir=/path/to/ASKA`.
- Copy `Local.props.example` to `Local.props` and set `PulsarLocalMods` to have builds copy into a Pulsar profile.
- `scripts/package.sh` builds the Thunderstore zip.
