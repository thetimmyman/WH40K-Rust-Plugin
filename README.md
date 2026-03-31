# WH40K — Warhammer 40K PvE Plugin for Rust / Oxide

**v0.4.2** · GPL v3 · [uMod/Oxide](https://umod.org/)

A full Warhammer 40K-themed PvE faction system for [Rust](https://store.steampowered.com/app/252490/Rust/). Three factions — **Orks**, **Tyranids**, and **Imperium** — are automatically mapped to your server's monuments on startup. No manual zone setup required.

---

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Faction Zones](#faction-zones)
- [NPC Archetypes](#npc-archetypes)
- [Quests](#quests)
- [Events](#events)
- [Support & Assault System](#support--assault-system)
- [Configuration](#configuration)
- [Admin Commands](#admin-commands)
- [Player Commands](#player-commands)
- [Roadmap](#roadmap)
- [Contributing](#contributing)
- [License](#license)

---

## Features

- **Monument auto-detection** — scans all map monuments on server start and assigns Ork, Tyranid, or Imperium zones automatically
- **NPC population** — zones auto-fill with faction NPCs on server start; NPCs respawn on a configurable timer after death
- **6 NPC archetypes** — Ork Boy, Ork Nob, Warboss Grakh (Orks), Termagant, Hormagaunt, Carnifex (Tyranids)
- **5 quests** — interactive CUI panel with live progress bars and ACCEPT buttons (`/wh40k quests`)
- **2 faction events** — Tyranid Swarm and Waaagh! that target the nearest imperial zone to any online player
- **Support & assault system** — recruit Guardsmen, Sergeants, and Commissars; buy buffs; launch structured assaults with wave schedules
- **7 buff items** — Emperor's Blessing, Iron Faith, Stimm, Medicae, Orbital Scan, Artillery, Vox Scramble
- **4 difficulty tiers** — recruit / soldier / veteran / champion (controls support caps and wave count)
- **Death flavor text** — faction-specific kill messages per faction, configurable in JSON
- **Map markers** — faction zones shown on the in-game map (G) with color-coded circles
- **Economy hooks** — Economics (Thrones), ServerRewards, and XPerience all wired in; plugin runs without any of them

---

## Requirements

**Required:**
- [Oxide/uMod](https://umod.org/games/rust) on your Rust dedicated server

**Optional (graceful fallback if missing):**

| Plugin | Purpose |
|--------|---------|
| [Economics](https://umod.org/plugins/economics) | Thrones currency — kill rewards, quest rewards, recruitment costs |
| [ServerRewards](https://umod.org/plugins/server-rewards) | RP points from kills and quests |
| [XPerience](https://umod.org/plugins/xperience) | XP from NPC kills |
| [ZoneManager](https://umod.org/plugins/zone-manager) | Safe zone enforcement at imperial monuments |

The plugin compiles and runs with none of these installed. Rewards are simply skipped if the target plugin is absent.

---

## Installation

1. Copy `WH40K.cs` into `oxide/plugins/` on your server
2. Restart the server **or** run `oxide.reload WH40K` in the console
3. Verify zone assignments: `wh40k.zones`

> **First deploy:** delete any existing `oxide/config/WH40K.json` before step 2 so fresh defaults are written with all new config keys.

---

## Faction Zones

On server init, the plugin scans all map monuments and assigns each one to a faction by matching substrings in the monument's internal name (order-sensitive, most specific first).

```
wh40k.zones       — list all detected zones with faction and coordinates
wh40k.monuments   — list raw monument names from this map
wh40k.resetzones  — wipe zone config and re-run detection
```

### Zone Assignments

| Monument | Faction | Radius |
|----------|---------|--------|
| launch_site | Imperium | 250m |
| airfield | Imperium | 220m |
| harbor | Imperium | 200m |
| excavator | Imperium | 200m |
| water_treatment | Imperium | 180m |
| powerplant / power_plant | Imperium | 175m |
| trainyard | Imperium | 175m |
| military_tunnel | Imperium | 175m |
| compound | Imperium | 120m |
| arctic_research | Imperium | 120m |
| satellite_dish | Imperium | 80m |
| junkyard | Orks | 130m |
| bandit | Orks | 120m |
| quarry | Orks | 80m |
| sewer | Orks | 70m |
| oilrig | Orks | 150m |
| sphere_tank | Orks | 80m |
| ferry_terminal | Orks | 80m |
| supermarket | Orks | 60m |
| gas_station | Orks | 50m |
| lighthouse | Tyranids | 60m |
| fishing_village | Tyranids | 80m |
| ranch | Tyranids | 80m |
| large_barn | Tyranids | 80m |
| barn | Tyranids | 70m |
| rad_town | Tyranids | 80m |
| cave | Tyranids | 50m |
| stables | Tyranids | 70m |

Zones populate with NPCs 5 seconds after server init (to allow terrain to fully load). NPCs spawn in ork/tyranid zones; imperial zones are player-safe areas.

---

## NPC Archetypes

### Enemy NPCs

| Archetype ID | Display Name | Faction | Thrones | RP | XP |
|--------------|--------------|---------|---------|-----|-----|
| `ork_boy` | Ork Boy | Orks | 50 | 10 | 25 |
| `ork_nob` | Ork Nob | Orks | 150 | 30 | 75 |
| `warboss_grakh` | Warboss Grakh da Killa | Orks | 500 | 100 | 300 |
| `termagant` | Termagant | Tyranids | 30 | 5 | 15 |
| `hormagaunt` | Hormagaunt | Tyranids | 60 | 12 | 30 |
| `carnifex` | Carnifex | Tyranids | 350 | 70 | 200 |

### Support Units (player-recruited)

| Unit | HP | Recruit Cost |
|------|----|-------------|
| Guardsman | 150 | 25 Thrones |
| Sergeant | 400 | 75 Thrones |
| Commissar | 800 | 150 Thrones |

---

## Quests

Open the quest panel with `/wh40k quests`. The CUI shows all available quests, live progress bars, and clickable ACCEPT buttons. Progress updates in real time while the panel is open.

| Quest ID | Name | Objective | Reward |
|----------|------|-----------|--------|
| `q01_thin_the_herd` | Thin the Herd | Kill 10 Tyranids | 300T + 100 rep |
| `q02_green_tide` | Stem the Green Tide | Kill 8 Orks | 400T + 150 rep |
| `q03_decapitation_strike` | Decapitation Strike | Kill the Carnifex | 800T + 250 rep |
| `q04_warboss_hunt` | Hunt the Warboss | Kill Warboss Grakh | 1500T + 500 rep |
| `q05_cleanse_and_burn` | Cleanse and Burn | Kill 5 Orks + 5 Tyranids | 600T + 200 rep |

T = Thrones (Economics currency). Rep = Imperium faction reputation.

---

## Events

Two events fire automatically on a random timer. Both target the **nearest imperial zone to any online player** (falls back to random if no players are connected).

| Event | Display Name | Waves | NPCs/Wave | Auto Interval |
|-------|-------------|-------|-----------|---------------|
| `tyranid_wave` | Tyranid Biomorphic Swarm | 3 | 6 Termagants | 45–90 min |
| `waaagh` | WAAAGH! Ork Raid | 2 | 5 Ork Boys | 60–120 min |

Events broadcast start/end announcements to all players. The event ends when all spawned NPCs are killed or the final wave timer expires.

**Trigger manually:** `wh40k.event tyranid_wave` / `wh40k.event waaagh`

---

## Support & Assault System

The support system lets players build and deploy a squad of Imperial troops for structured assault missions against Ork zones.

### Step 1 — Recruit

Spend Thrones to add units to your roster before an assault:

```
/wh40k recruit list              — show roster, balance, recruit cap
/wh40k recruit guardsman         — 25 Thrones
/wh40k recruit sergeant          — 75 Thrones
/wh40k recruit commissar         — 150 Thrones
/wh40k recruit dismiss           — dismiss all active units
```

### Step 2 — Buy Buffs (optional)

Purchase consumables and buffs before launching the assault:

```
/wh40k buy <item>
```

| Item | Cost | Effect | Type |
|------|------|--------|------|
| `emperor_blessing` | 60T | +25% player damage for 10 min | Latched |
| `iron_faith` | 75T | +25% damage resistance for 10 min | Latched |
| `orbital_scan` | 50T | Reveal all Orks in zone on map for 5 min | Latched |
| `artillery` | 100T | Deal 30% max HP damage to all Orks in zone | Latched |
| `vox_scramble` | 80T | Reduce Ork max HP by 25% in zone | Latched |
| `stimm` | 30T | Instantly restore stamina, calories, hydration | Instant |
| `medicae` | 40T | Instantly restore full health | Instant |

**Instant** items apply immediately on purchase. **Latched** buffs apply when you start the assault.

### Step 3 — Set Difficulty

Configure `ActiveAssaultDifficulty` in `oxide/config/WH40K.json`:

| Tier | Recruit Cap | Support Waves | Wave Times |
|------|-------------|---------------|-----------|
| `recruit` | 0 | 0 | — |
| `soldier` | 2 | 1 | T+60s |
| `veteran` | 4 | 2 | T+30s, T+180s |
| `champion` | 6 | 3 | T+0s, T+60s, T+180s |

### Step 4 — Launch the Assault

```
/wh40k assault start    — deploy to nearest Ork zone; latched buffs apply; support waves launch per schedule
/wh40k assault end      — recall all support units and clear buff state
```

---

## Configuration

After first load, `oxide/config/WH40K.json` is generated with all defaults. Player data is saved to `oxide/data/WH40K/players.json` and `oxide/data/WH40K/recruits.json`.

Key settings to tune:

| Key | Default | Description |
|-----|---------|-------------|
| `AutoDiscoverZones` | `true` | Auto-assign zones on server start |
| `BaseKillEconomics` | `30` | Thrones for untracked kills (vanilla scientists, animals) |
| `BaseKillServerRewards` | `5` | RP points for untracked kills |
| `ActiveAssaultDifficulty` | `"soldier"` | Assault difficulty tier (recruit/soldier/veteran/champion) |
| `FactionSpawn.orks.MaxPerZone` | `4` | Max Ork NPCs per zone |
| `FactionSpawn.orks.RespawnMinutes` | `12` | Minutes before Orks respawn after death |
| `FactionSpawn.tyranids.MaxPerZone` | `5` | Max Tyranid NPCs per zone |
| `FactionSpawn.tyranids.RespawnMinutes` | `8` | Minutes before Tyranids respawn after death |
| `RecruitCosts.guardsman` | `25` | Thrones to recruit a Guardsman |
| `RecruitCosts.sergeant` | `75` | Thrones to recruit a Sergeant |
| `RecruitCosts.commissar` | `150` | Thrones to recruit a Commissar |

All NPC archetype stats, kill rewards, quest definitions, event configs, buff costs, and death flavor text are also in the same file.

---

## Admin Commands

Run in server console or F1 in-game (requires admin):

| Command | Arguments | Description |
|---------|-----------|-------------|
| `wh40k.zones` | — | List all auto-detected zones with faction and coordinates |
| `wh40k.monuments` | — | List raw monument names found on this map |
| `wh40k.resetzones` | — | Wipe zone config and re-run auto-detection |
| `wh40k.populate` | `[zoneId]` | Repopulate all zones or one specific zone |
| `wh40k.spawn` | `<archetypeId>` | Spawn NPC in front of you (debug tool, in-game F1 only) |
| `wh40k.event` | `<eventId>` | Trigger event immediately |
| `wh40k.events` | — | List all events and their active status |

**Archetype IDs:** `ork_boy` `ork_nob` `warboss_grakh` `termagant` `hormagaunt` `carnifex`

**Event IDs:** `tyranid_wave` `waaagh`

---

## Player Commands

All chat commands:

| Command | Description |
|---------|-------------|
| `/wh40k status` | Faction kill counts, reputation, and active quest progress |
| `/wh40k quests` | Open quest CUI panel (progress bars + ACCEPT buttons) |
| `/wh40k accept <questId>` | Start a quest (alternative to clicking ACCEPT in panel) |
| `/wh40k where` | Show which faction zone you're currently standing in |
| `/wh40k recruit list` | Show recruitment roster, Thrones balance, and recruit cap |
| `/wh40k recruit <unit>` | Recruit a unit — `guardsman`, `sergeant`, or `commissar` |
| `/wh40k recruit dismiss` | Dismiss all active support units |
| `/wh40k buy <item>` | Purchase a buff or consumable (see [Buff Items](#step-2--buy-buffs-optional)) |
| `/wh40k assault start` | Deploy to nearest Ork zone and begin assault |
| `/wh40k assault end` | Recall support units and end the assault |

---

## Roadmap

See [PLAN.md](PLAN.md) for the full phase-by-phase breakdown with detailed tasks.

| Phase | Status | Summary |
|-------|--------|---------|
| 1 — Foundation | ✅ Complete | Zones, NPC spawns, events, quests, kill rewards |
| 2 — World Setup | ✅ Complete | Hub builder, RaidableBases blueprints, support system |
| 3 — Economy | 🔄 In Progress | GUIShop categories, ServerRewards kits, rep tier rewards |
| 4 — Content Expansion | ⬜ Planned | 15+ quests, HumanNPC quest givers, boss respawn, wartrukk event |
| 5 — Polish | ⬜ Planned | Welcome message, death messages, kill feed flavor |

**Backlog ideas** (contributions very welcome): Chaos faction, Aeldari faction, squad difficulty scaling, NPC nameplates, Hive Fleet Alert server-wide event, custom loot tables per archetype.

---

## Contributing

Pull requests welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup, code style, and testing instructions.

Areas where help is most wanted:
- New quests and objective types (Phase 4 — see PLAN.md)
- Balance tuning for kill rewards, NPC health, respawn timers
- New faction zones or monument keyword mappings
- Lore-accurate flavor text and announcements
- Backlog factions (Chaos, Aeldari)

Please open an issue before starting large features so we can align on approach.

---

## License

GPL v3 — see [LICENSE](LICENSE).
