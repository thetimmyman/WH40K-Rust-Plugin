# WH40K PvE — Project Execution Plan

**Server:** Your Rust server
**Plugin:** `oxide/plugins/WH40K.cs` · current version: **v0.3.0**
**Theme:** Warhammer 40K PvE — Orks / Tyranids / Imperium

Legend: `[CODE]` = Claude writes it · `[IN-GAME]` = you do it in Rust · `[CONFIG]` = edit a file · `[TEST]` = verify it works

---

## Phase 1 — Foundation · *Get WH40K.cs running and testable*

> Goal: Plugin loads, zones are set, NPCs spawn, events fire, quests work.

### 1.1 Deploy Plugin
- [x] `[CODE]` WH40K.cs written and reviewed (v0.2.3)
- [x] `[IN-GAME]` Deployed: `kubectl cp WH40K.cs rust/<pod>:/steamcmd/rust/oxide/plugins/WH40K.cs`
- [x] `[TEST]` Confirmed load: `oxide.plugins` shows `WH40K 2.2.3` in list
- [x] `[TEST]` Config generated: `oxide/config/WH40K.json` exists

### 1.2 Zone Setup ~~Set Zone Coordinates~~
> **SUPERSEDED by monument auto-detection (v0.2.0+)**
> ~~Stand at each location in-game and run in F1 console.~~

- [x] `[CODE]` Monument auto-discovery implemented (`AutoDiscoverMonumentZones` on `OnServerInitialized`)
- [x] `[CODE]` Faction assignment via keyword matching (order-sensitive `MonumentFactionMap`)
- [x] `[CODE]` Display name fix: `.prefab` extension stripped from zone names
- [x] `[TEST]` `wh40k.zones` — 28 zones auto-detected and assigned:
  - **Imperium (6):** launch_site, airfield, harbor, military_tunnel, water_treatment, powerplant/arctic_research/compound/satellite_dish variants
  - **Orks (11):** junkyard, bandit_camp, excavator, quarry, sewer, oilrig (small+large), sphere_tank, ferry_terminal, supermarket, gas_station, trainyard
  - **Tyranids (11):** lighthouse, fishing_village, ranch, large_barn, barn, rad_town, cave, stables variants

### 1.3 Test NPC Spawns (v0.3.0: auto-population)
> **v0.3.0 change:** Ork/Tyranid zones now auto-populate on server start (4 orks/zone, 5 tyranids/zone).
> NPCs respawn 12 min (orks) / 8 min (tyranids) after death. `wh40k.spawn` is now an admin debug tool.
> **Important:** delete `oxide/config/WH40K.json` before deploying so the new `FactionSpawn` defaults are written.

- [ ] `[IN-GAME]` Travel to a junkyard/bandit camp — confirm ork_boy and ork_nob spawned there automatically
- [ ] `[IN-GAME]` Travel to a fishing_village/ranch — confirm termagants and hormagaunts spawned
- [ ] `[TEST]` Kill an ork_boy — confirm "Ork Boy slain! +50 Thrones"
- [ ] `[TEST]` Kill a termagant — confirm "Termagant slain! +30 Thrones"
- [ ] `[TEST]` Wait ~12 min after clearing ork zone — confirm NPCs respawn
- [ ] `[TEST]` Check Economics balance increased after kills

### 1.4 Test Events
> **v0.3.0 change:** Events now target the imperial zone **nearest to any online player** instead of random.

- [ ] `[IN-GAME]` Stand near any monument, run `wh40k.event tyranid_wave` — confirm NPCs spawn at nearest imperial zone to you
- [ ] `[TEST]` Wave 2 spawns after ~90 seconds
- [ ] `[TEST]` Killing all spawns triggers "swarm repelled" broadcast
- [ ] `[IN-GAME]` `wh40k.event waaagh` — confirm Ork wave fires similarly

### 1.5 Test Quests (v0.3.0: CUI panel)
> **v0.3.0 change:** `/wh40k quests` now opens a CUI panel with progress bars and clickable ACCEPT buttons.

- [ ] `[IN-GAME]` `/wh40k quests` — confirm CUI panel opens
- [ ] `[IN-GAME]` Click ACCEPT on q01_thin_the_herd from the panel
- [ ] `[TEST]` Kill Tyranids — confirm progress bar updates live in the open panel
- [ ] `[TEST]` Complete quest — confirm panel refreshes to show DONE + Thrones message appears
- [ ] `[IN-GAME]` Click ACCEPT on q04_warboss_hunt — confirm `wh40k.spawn warboss_grakh` still works for testing

**Phase 1 complete when:** All zones set ✅, kills reward correctly, events fire, quests complete.

---

## Phase 2 — World Setup · *Make locations feel real*

> Goal: The hub feels like a base, Ork camps feel like camps, nests feel dangerous.
> Note: with 28 auto-detected zones, "the hub" is now any imperial zone (compound / outpost area).

### 2.1 Install ZoneManager
- [ ] `[CODE]` Download ZoneManager from uMod, deploy to server
- [ ] `[CODE]` Wire WH40K.cs to call ZoneManager for safe zone enforcement at imperial zones
  - Players cannot be damaged inside imperial zone radius
  - Notify on entry: "You have entered Firebase Tertius. Safe zone active."
  - Notify on exit: "Leaving Firebase Tertius. Stay alert, soldier."
- [ ] `[TEST]` Take damage inside hub → confirm blocked
- [ ] `[TEST]` Leave hub → confirm damage re-enabled

### 2.2 Build the Imperial Hub
- [ ] `[IN-GAME]` Pick the best imperial monument as the "main" Firebase Tertius (Compound recommended — it's a safe zone by default in vanilla Rust)
- [ ] `[IN-GAME]` Place sleeping bags / respawn point near compound
- [ ] `[IN-GAME]` `/sethome hub` for quick return teleport
- [ ] `[CONFIG]` Note: compound is already a vanilla safe zone — may not need ZoneManager for it

### 2.3 Ork Territory — Field Test
- [ ] `[IN-GAME]` Walk through each Ork zone, test `wh40k.spawn ork_boy` inside each
- [ ] `[IN-GAME]` Note any terrain issues (spawn underground, ocean clips, etc.)
- [ ] `[CODE]` Adjust `MonumentRadiusMap` values for problem zones

### 2.4 Build Ork Warboss Stronghold (Warboss-specific base)
- [ ] `[IN-GAME]` Pick one Ork zone (excavator or junkyard recommended) for warboss encounter
- [ ] `[IN-GAME]` Build a crude fortification at that monument
- [ ] `[TEST]` Full clear: ork_boys → ork_nobs → warboss_grakh

### 2.5 RaidableBases — Themed Ork Raids
- [ ] `[IN-GAME]` Build a simple "Ork bunker" structure (4×4 or 6×6, defended)
- [ ] `[IN-GAME]` `/copy orkbunker_easy` (CopyPaste command)
- [ ] `[CONFIG]` Add `orkbunker_easy` to RaidableBases `Buildings` config as difficulty "Easy"
- [ ] `[IN-GAME]` Build a harder structure → `/copy orkfortress_hard`
- [ ] `[CONFIG]` Add to RaidableBases as difficulty "Hard"
- [ ] `[TEST]` `rb.toggle easy` — confirm ork base spawns on map
- [ ] `[TEST]` Raid and clear a spawned base

### 2.6 Tyranid Nest Field Test
- [ ] `[IN-GAME]` Walk through each Tyranid zone, confirm terrain suitability
- [ ] `[TEST]` Spawn termagants and hormagaunts in each nest zone
- [ ] `[IN-GAME]` Mark dangerous nests (signs / landmine clusters as visual indicator)

### 2.7 Carnifex Lair Designation
- [ ] `[IN-GAME]` Pick one Tyranid zone as the Carnifex encounter zone (open area — Carnifex needs room to charge)
- [ ] `[TEST]` Full nest clear: termagants → hormagaunts → carnifex

**Phase 2 complete when:** Ork and Tyranid zones play well, RaidableBases has 2 Ork blueprints, hub has a home point. ✓

---

## Phase 3 — Economy & Progression · *Give players a reason to grind*

> Goal: Thrones earned through play → spent at shop. XPerience levels reward play style.

### 3.1 Tune Kill Rewards
- [ ] `[CONFIG]` Review WH40K.json kill reward values after Phase 1 play-testing
  - Termagant: 30T feels right? Or adjust to 20T?
  - Warboss: 500T — does it feel earned?
- [ ] `[CONFIG]` Adjust `BaseKillEconomics` for vanilla kills (untracked animals/scientists)

### 3.2 Configure GUIShop with WH40K Theme
- [ ] `[CODE]` Rename GUIShop categories to WH40K flavor:
  - "Resources" → "Imperial Supply Depot"
  - "Weapons" → "Armory"
  - "Medical" → "Medicae Bay"
  - "Ammunition" → "Munitions"
  - "Building" → "Field Engineering"
- [ ] `[CONFIG]` Set item prices relative to Throne kill rewards:
  - Basic weapon (SMG): ~500T (~10 ork kills)
  - AK: ~1500T (~30 ork kills)
  - Medical syringe: ~100T each
  - Explosive: ~800T
- [ ] `[TEST]` Open `/shop` — confirm categories renamed and prices feel balanced

### 3.3 Configure ServerRewards Earn Rates
- [ ] `[CONFIG]` Set ServerRewards to award points for:
  - Killing WH40K NPCs (already in WH40K.cs)
  - Gathering resources (ServerRewards config: `GatherRate`)
  - Surviving a wave event (add to WH40K.cs EndEvent handler)
- [ ] `[CODE]` Add `ServerRewards` point grant in `EndEvent(cleared:true)` for nearby players
- [ ] `[TEST]` Confirm ServerRewards balance grows from play

### 3.4 Configure XPerience Skill Tree
- [ ] `[CONFIG]` Review XPerience.json — enable skills relevant to PvE:
  - WoodCutter / Miner / Forager (gathering bonuses) ✓
  - Medic (healing bonuses) ✓
  - Weaponsmith (crafting speed) ✓
  - Disable or reduce PvP-focused skills
- [ ] `[CONFIG]` Set XP earn rates from WH40K kills (already wired)
- [ ] `[TEST]` `/xp stats` — confirm XP increases after kills

### 3.5 Add Faction Rep Tier Rewards
- [ ] `[CODE]` Add `FactionRepTiers` to WH40K config:
  ```json
  "FactionRepTiers": {
    "imperium": [
      { "Rep": 100, "Reward": "kit:guardsman",   "Message": "Rank: Guardsman" },
      { "Rep": 500, "Reward": "kit:veteran",     "Message": "Rank: Veteran" },
      { "Rep": 1000,"Reward": "kit:stormtrooper","Message": "Rank: Stormtrooper" }
    ]
  }
  ```
- [ ] `[CODE]` Add tier-check to `CompleteQuest` and `OnFactionKill`
- [ ] `[IN-GAME]` Create Kits: `guardsman`, `veteran`, `stormtrooper` via `/kit` admin UI
- [ ] `[TEST]` Complete quests until rep reaches 100 → confirm kit reward fires

**Phase 3 complete when:** Economy loop works (kill → Thrones → shop), XP levels up, rep tiers unlock. ✓

---

## Phase 4 — Content Expansion · *More to do, more reasons to return*

> Goal: 15+ quests, HumanNPC quest givers, boss respawn cycle, second event type.

### 4.1 Expand Quest List (5 → 15)
- [ ] `[CODE]` Add 10 more quests to WH40K.json:
  - Wave survival: "Survive the Tyranid Swarm event" (objective type: `event_survived`)
  - Hunting: "Kill 3 Hormagaunts"
  - Escalating: "Kill 20 Orks" (grindy, high reward)
  - Boss chain: "Kill the Carnifex AND the Warboss" (multi-objective)
  - Exploration: "Visit all 5 zone types" (objective type: `zone_visited`)
  - Daily-style: "Kill 5 of anything" (resets, low reward)
- [ ] `[CODE]` Add `zone_visited` objective type to WH40K.cs (proximity check on `OnPlayerTick`)
- [ ] `[CODE]` Add `event_survived` objective type — grant on `EndEvent(cleared:true)` to nearby players
- [ ] `[TEST]` All 15 quests completable without bugs

### 4.2 HumanNPC Quest Givers at Hub
- [ ] `[IN-GAME]` Install HumanNPC: already in oxide/plugins, verify `oxide.plugins` shows it
- [ ] `[IN-GAME]` At hub, run: `/npc_add` → creates NPC at your feet
- [ ] `[IN-GAME]` `/npc_name <id> "Commissar Dorn"` — name it
- [ ] `[IN-GAME]` `/npc_kit <id> militarysuit` — give it an outfit
- [ ] `[IN-GAME]` Repeat for: Sergeant Valdis, Supply Master Vex, Tech-Adept Thorn
- [ ] `[CODE]` Add chat-proximity trigger: when player within 5m of NPC, send quest list message
  - MVP: just `/npc_message <id> "Use /wh40k quests to see available missions, soldier."`
- [ ] `[TEST]` Walk up to Commissar Dorn → receive quest list

### 4.3 Boss Respawn System
- [ ] `[CODE]` Add `BossRespawnMinutes` to NpcArchetype config
- [ ] `[CODE]` In `OnWH40KKill`: if archetype is a boss, schedule respawn timer
  - Announce: "Warboss Grakh has been defeated! He will return in 2 hours..."
  - On respawn: "Warboss Grakh has returned to his stronghold. Hunt him again."
- [ ] `[CONFIG]` Set warboss_grakh respawn to 120 min, carnifex to 60 min
- [ ] `[TEST]` Kill warboss → wait → confirm respawn announcement fires

### 4.4 Add Third Event: Bradley / Ork Wartrukk
- [ ] `[CODE]` Add `wartrukk` event to WH40K.json:
  - Spawns a Bradley APC (prefab: `assets/prefabs/npc/m2bradley/bradleyapc.prefab`)
  - Theme: "Ork Wartrukk spotted heading for Firebase Tertius!"
- [ ] `[CODE]` Add Bradley to `SpawnNpc` — Bradley uses different kill hook (`OnEntityDeath` on BradleyAPC)
- [ ] `[TEST]` `wh40k.event wartrukk` — confirm Bradley spawns

### 4.5 Improve Zone Spawning
- [ ] `[CODE]` Add `wh40k.repopulate <zoneId>` admin command — respawns all zone NPCs
- [ ] `[CODE]` Add `ZoneRespawnMinutes` to ZoneDef — auto-repopulate zones on a timer
- [ ] `[CONFIG]` Set ork camps to repopulate every 30 min, nests every 20 min
- [ ] `[TEST]` Clear a zone, wait → confirm NPCs respawn

**Phase 4 complete when:** 15 quests, 4 named NPCs at hub, bosses respawn, zones repopulate. ✓

---

## Phase 5 — Polish · *Immersion and feel*

> Goal: First-time player experience is coherent. The WH40K theme is visible and consistent.

### 5.1 Welcome Experience
- [ ] `[CODE]` Add `WelcomeMessage` to config — multi-line lore intro on first join
  ```
  ╔══ IMPERIAL DEPLOYMENT ORDERS ══════════════════════╗
  ║ Soldier. You have been assigned to Firebase Tertius.║
  ║ The xenos threaten our perimeter on all fronts.    ║
  ║ Report to Commissar Dorn for your first orders.    ║
  ║                                                    ║
  ║ /wh40k quests  — mission briefings                 ║
  ║ /shop          — Imperial supply depot             ║
  ║ /home hub      — return to Firebase Tertius        ║
  ╚════════════════════════════════════════════════════╝
  ```
- [ ] `[CODE]` Add `FirstJoinOnly` flag — show once, then compact reminder on subsequent joins
- [ ] `[TEST]` Fresh character join → confirm welcome fires

### 5.2 Map Markers
- [ ] `[CODE]` Install `MapMarkerGenericRadius` via WH40K.cs on `OnServerInitialized`:
  - Imperial zones: gold circles labeled "Firebase [MonumentName]"
  - Ork zones: green circles labeled "Ork Territory — [MonumentName]"
  - Tyranid zones: purple circles labeled "Tyranid Nest — [MonumentName]"
- [ ] `[TEST]` Open in-game map (G) — confirm faction zones visible

### 5.3 Death Messages
- [ ] `[CODE]` Add `OnPlayerDeath` hook — WH40K-flavored death messages:
  - Killed by ork: "The xenos claim another. The Emperor weeps."
  - Killed by tyranid: "Consumed by the Swarm. Your genes now feed the hive."
  - Killed by environment: "Hostile environment claims another guardsman."
- [ ] `[TEST]` Die to each faction → confirm correct message

### 5.4 Announcement Tuning
- [ ] `[CONFIG]` Review all event start/end announcements for tone consistency
- [ ] `[CODE]` Add pre-event warning 5 min before event fires in `ScheduleEvent`
  - "⚠ Auspex contact. Expect Tyranid contact in 5 minutes. Prepare defenses."

### 5.5 Kill Feed Flavor
- [ ] `[CODE]` Add random kill flavor lines per faction (list in config):
  ```json
  "OrkKillFlavor": ["WAAAGH redirected.", "One less greenskin.", "For the Emperor."],
  "TyranidKillFlavor": ["The Swarm retreats... temporarily.", "Biomass neutralized.", "The Hivemind takes note."]
  ```
- [ ] `[TEST]` Kill 5 each → confirm flavor lines vary

### 5.6 Server Name & MOTD
- [ ] `[CONFIG]` Update `SERVER_NAME` in [rust.yaml](../rust.yaml): `"YourServer — WH40K PvE"`
- [ ] `[CONFIG]` Update `SERVER_DESCRIPTION`: `"Private WH40K-themed PvE. /wh40k to begin."`
- [ ] Redeploy: `kubectl apply -f k8s/rust/rust.yaml`

**Phase 5 complete when:** New player lands, sees the theme immediately, knows what to do. ✓

---

## Backlog — Future Phases

> Not planned yet. Ideas for after Phase 5 is stable.

- [ ] Death Corps of Krieg starter kit — get DLC skin ID from user; add to `guardsman` kit
- [ ] Chaos faction (Chaos Space Marines as fourth faction — elite, rare, dangerous)
- [ ] Aeldari faction (fragile but fast, tricky encounters)
- [ ] Faction reputation gating: Imperium rep 500+ required to access Armory tier 2
- [ ] Custom loot tables per NPC archetype (ork drops scrap/gunpowder; tyranid drops chitin/bio-matter)
- [ ] Bio-matter currency for Tyranid kills → separate shop category
- [ ] "Hive Fleet Alert" — server-wide event, all Tyranid nests simultaneously active
- [ ] Squad mechanics: if 2+ players in same zone, event difficulty scales up
- [ ] Leash system for Tyranid NPCs (keep wolves in zone radius)
- [ ] NPC name plates (FloatingText or MapNote above spawned entities)
- [ ] Custom Oxide UI panel (player faction stats, active events, top kills)
- [ ] Wipe ceremony: clear all zones, announce "The Imperium reassigns to new territory"

---

## Quick Reference — Admin Commands

```bash
# Zones (auto-detected on server start — no manual setup needed)
wh40k.zones                      # list all 28 auto-detected zones + faction assignments
wh40k.monuments                  # list raw monument names discovered on this map
wh40k.resetzones                 # wipe zone config + re-run auto-detection

# Spawning
wh40k.spawn <archetypeId>        # spawn NPC in front of you
# archetypes: ork_boy, ork_nob, warboss_grakh, termagant, hormagaunt, carnifex

# Events
wh40k.event <eventId>            # trigger event now
wh40k.events                     # list events + active status
# events: tyranid_wave, waaagh

# Player commands
/wh40k status                    # kill counts + rep
/wh40k quests                    # quest list + progress
/wh40k accept <questId>          # start a quest
/wh40k where                     # show which zone you're currently in

# Connect
client.connect <your-server-ip>:28015
```

---

## Status Tracking

| Phase | Status | Notes |
|---|---|---|
| Phase 1 — Foundation | 🔄 In Progress | v0.3.0: zone auto-pop + nearest-player events + CUI quests deployed; in-game testing (1.3–1.5) pending |
| Phase 2 — World Setup | ⬜ Blocked on Phase 1 testing | Zone auto-detection changes scope — see updated tasks |
| Phase 3 — Economy | ⬜ Blocked on Phase 2 | |
| Phase 4 — Content | ⬜ Blocked on Phase 3 | |
| Phase 5 — Polish | ⬜ Blocked on Phase 4 | 5.6 server rename ready to do anytime |
