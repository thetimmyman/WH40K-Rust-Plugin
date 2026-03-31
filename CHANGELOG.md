# Changelog

All notable changes to WH40K.cs are documented here.

## [0.4.2] — Current
- Support system: `/wh40k support` panel for recruiting Guardsmen, Sergeants, Commissars
- Assault missions: `/wh40k assault <zoneId>` with difficulty tiers (recruit/soldier/veteran/champion)
- Buff items: Emperor's Blessing, Iron Faith, Stimm, Medicae, Orbital Scan, Artillery, Vox Scramble
- Support wave schedules per difficulty (0–3 reinforcement waves mid-assault)
- HP multipliers per support archetype
- Map markers for faction zones visible on in-game map (G key)
- ZoneManager integration stub (safe zones at imperial monuments)

## [0.4.1]
- Death flavor text system — faction-specific kill messages configurable in JSON
- DeathFlavor config key per faction id
- Minor stability fixes for event zone targeting

## [0.3.2]
- Fix: `wh40k.populate` command now works reliably after server restart
- Fix: zone radius map entries for `sewer`, `ferry_terminal`, `sphere_tank`
- Fix: CUI panel close button z-order

## [0.3.0] — Major update
- Monument auto-population: Ork/Tyranid zones auto-fill on server start
- Respawn system: NPCs respawn 12 min (orks) / 8 min (tyranids) after death
- CUI quest panel: `/wh40k quests` opens panel with live progress bars and ACCEPT buttons
- Events now target nearest imperial zone to any online player (fallback: random)

## [0.2.3]
- Fix: display name strips `.prefab` extension from zone names
- Fix: faction assignment order (more specific entries first — `large_barn` before `barn`)

## [0.2.0]
- Monument auto-detection: `AutoDiscoverMonumentZones()` runs on `OnServerInitialized`
- Faction assignment via keyword matching on monument name
- No manual `wh40k.setzone` required
- `wh40k.monuments` command to inspect raw monument list

## [0.1.0] — Initial release
- Basic faction system: Orks, Tyranids, Imperium
- Manual zone registration via `wh40k.setzone`
- Kill rewards via Economics and ServerRewards
- Basic quest system (text only, no CUI)
- `tyranid_wave` and `waaagh` events
