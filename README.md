# WH40K — Warhammer 40K PvE Plugin for Rust / Oxide

A full Warhammer 40K-themed PvE faction system for [Rust](https://store.steampowered.com/app/252490/Rust/) using the [Oxide/uMod](https://umod.org/) framework.

Three factions — **Orks**, **Tyranids**, and **Imperium** — are automatically mapped to your server's monuments on startup. No manual zone setup required.

---

## Features

- **Auto-detection** — scans all map monuments on server init and assigns faction zones by type
- **NPC population** — Orks and Tyranids auto-spawn in their zones and respawn on kill
- **Faction events** — Tyranid Swarm and Waaagh! events that target imperial zones near players
- **Quest system** — 5 quests with a CUI panel (`/wh40k quests`), live progress bars, and Thrones rewards
- **Support system** — recruit Guardsmen, Sergeants, and Commissars to fight alongside you during assaults
- **Assault missions** — structured PvE assaults with difficulty tiers, buff items, and wave schedules
- **Map markers** — faction zones shown on the in-game map (G key) with color-coded circles
- **Economy integration** — Economics, ServerRewards, and XPerience plugin hooks
- **Death flavor text** — faction-specific kill messages

### Factions

| Faction | Monuments | NPCs |
|---------|-----------|------|
| **Orks** | Junkyard, Bandit Camp, Excavator, Quarry, Oil Rig, Trainyard, … | Ork Boy, Ork Nob, Warboss Grakh |
| **Tyranids** | Fishing Village, Ranch, Barn, Rad Town, Cave, … | Termagant, Hormagaunt, Carnifex |
| **Imperium** | Launch Site, Airfield, Harbor, Military Tunnel, … | (Phase 2: HumanNPC quest givers) |

---

## Requirements

- [Oxide/uMod](https://umod.org/) on your Rust server
- Recommended companion plugins (optional but enhance experience):
  - [Economics](https://umod.org/plugins/economics)
  - [ServerRewards](https://umod.org/plugins/server-rewards)
  - [XPerience](https://umod.org/plugins/xperience)
  - [ZoneManager](https://umod.org/plugins/zone-manager)

---

## Installation

1. Copy `WH40K.cs` to `oxide/plugins/` on your server.
2. Restart the server or run `oxide.reload WH40K` in the console.
3. The plugin auto-detects all monuments and populates zones on start.
4. Check zone assignments with `wh40k.zones` in the server console.

> **First deploy:** delete any existing `oxide/config/WH40K.json` so fresh defaults are written.

---

## Configuration

After first load, `oxide/config/WH40K.json` is generated with all defaults. Key settings:

| Setting | Default | Description |
|---------|---------|-------------|
| `AutoDiscoverZones` | `true` | Auto-assign zones from monuments on server start |
| `BaseKillEconomics` | `30` | Thrones awarded for generic kills |
| `BaseKillServerRewards` | `5` | ServerRewards points for generic kills |
| `ActiveAssaultDifficulty` | `"soldier"` | Default assault difficulty tier |
| `FactionSpawn.orks.MaxPerZone` | `4` | Max Ork NPCs per zone |
| `FactionSpawn.orks.RespawnMinutes` | `12` | Minutes before dead Orks respawn |
| `FactionSpawn.tyranids.MaxPerZone` | `5` | Max Tyranid NPCs per zone |
| `FactionSpawn.tyranids.RespawnMinutes` | `8` | Minutes before dead Tyranids respawn |

NPC archetypes, kill rewards, quest definitions, and event configs are all in the same file.

---

## Admin Commands

```
# Zones
wh40k.zones                    list all auto-detected zones + faction assignments
wh40k.monuments                list raw monument names found on this map
wh40k.resetzones               wipe zone config and re-run auto-detection
wh40k.populate                 manually trigger zone population

# Spawning
wh40k.spawn <archetypeId>      spawn NPC in front of you
# archetypes: ork_boy, ork_nob, warboss_grakh, termagant, hormagaunt, carnifex

# Events
wh40k.event <eventId>          trigger an event immediately
wh40k.events                   list events and their active status
# events: tyranid_wave, waaagh
```

## Player Commands

```
/wh40k status                  kill counts + faction rep
/wh40k quests                  open quest CUI panel
/wh40k accept <questId>        start a quest
/wh40k where                   show which faction zone you're in
/wh40k support                 open support/recruitment panel
/wh40k assault <zoneId>        start an assault mission at a zone
/wh40k recall                  recall support units
```

---

## Roadmap

See [PLAN.md](PLAN.md) for the full phase-by-phase roadmap.

| Phase | Status |
|-------|--------|
| Phase 1 — Foundation (zones, spawns, events, quests) | 🔄 In Progress |
| Phase 2 — World Setup (ZoneManager, hub, RaidableBases) | ⬜ Planned |
| Phase 3 — Economy & Progression | ⬜ Planned |
| Phase 4 — Content Expansion (15+ quests, HumanNPC givers, boss respawn) | ⬜ Planned |
| Phase 5 — Polish (welcome experience, map markers, death messages) | ⬜ Planned |

---

## Contributing

Contributions welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

Ideas especially welcome for:
- New faction zones / monument assignments
- Additional quest types and objectives
- Lore-accurate flavor text and announcements
- Balance tuning for kill rewards and NPC health
- New factions from the backlog (Chaos, Aeldari)

---

## License

GPL v3 — see [LICENSE](LICENSE).
