// WH40K.cs — Warhammer 40K PvE plugin for Rust / Oxide
// Warhammer 40K-inspired PvE faction system for Rust / Oxide
//
// ARCHITECTURE
// ─────────────────────────────────────────────────────────────────────────────
// v0.2.0: Monument auto-detection
//   On server init, scans all map monuments and assigns faction zones by type:
//   • Orks     → junkyard, bandit camp, excavator, quarry
//   • Imperium → harbor, airfield, military tunnel, launch site, power plant,
//                train yard, water treatment, oil rig, outpost, arctic base
//   • Tyranids → fishing village, ranch, barn, rad town, cave, stables,
//                supermarket, gas station
//
// Zone assignment is fully automatic — no manual wh40k.setzone needed.
// Run 'wh40k.monuments' in-game to see what was detected and assigned.
//
// Faction mapping (vanilla entities)
//   orks      → vanilla NPCPlayer (scientists, heavy scientists)
//   tyranids  → vanilla BaseAnimalNPC (wolf, bear, boar)
//   imperium  → friendly, no vanilla equivalent yet (Phase 2: HumanNPC)
//
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("WH40K", "TimmyMan", "0.4.2")]
    [Description("Warhammer 40K PvE faction system — monument auto-zones, hub builder, support system")]
    class WH40K : RustPlugin
    {
        // ─── Monument → Faction / Radius mappings ────────────────────────────
        // Matched as substring of monument.name.ToLower(). Order matters: more
        // specific entries first so "large_barn" matches before "barn".

        static readonly List<KeyValuePair<string,string>> MonumentFactionMap =
            new List<KeyValuePair<string,string>>
        {
            // === ORKS — industrial wasteland, scrap, offshore platforms ===
            new KeyValuePair<string,string>("junkyard",        "orks"),
            new KeyValuePair<string,string>("bandit",          "orks"),
            new KeyValuePair<string,string>("excavator",       "orks"),
            new KeyValuePair<string,string>("quarry",          "orks"),
            new KeyValuePair<string,string>("sewer",           "orks"),
            new KeyValuePair<string,string>("oilrig",          "orks"),   // scrap platforms, not military
            new KeyValuePair<string,string>("sphere_tank",     "orks"),   // industrial waste dome
            new KeyValuePair<string,string>("ferry_terminal",  "orks"),   // industrial dock
            new KeyValuePair<string,string>("supermarket",     "orks"),   // looted supply depot
            new KeyValuePair<string,string>("gas_station",     "orks"),   // scrap fuel stop
            new KeyValuePair<string,string>("trainyard",       "orks"),   // scrap/rail chaos

            // === IMPERIUM — military, organized, high-tech ===
            new KeyValuePair<string,string>("launch_site",     "imperium"),
            new KeyValuePair<string,string>("airfield",        "imperium"),
            new KeyValuePair<string,string>("harbor",          "imperium"),
            new KeyValuePair<string,string>("military_tunnel", "imperium"),
            new KeyValuePair<string,string>("water_treatment", "imperium"),
            new KeyValuePair<string,string>("powerplant",      "imperium"),
            new KeyValuePair<string,string>("power_plant",     "imperium"),
            new KeyValuePair<string,string>("compound",        "imperium"),  // outpost
            new KeyValuePair<string,string>("arctic_research", "imperium"),
            new KeyValuePair<string,string>("satellite_dish",  "imperium"),

            // === TYRANIDS — organic, isolated, rural, underground ===
            new KeyValuePair<string,string>("lighthouse",      "tyranids"),  // isolated coastal
            new KeyValuePair<string,string>("fishing_village", "tyranids"),
            new KeyValuePair<string,string>("ranch",           "tyranids"),
            new KeyValuePair<string,string>("large_barn",      "tyranids"),
            new KeyValuePair<string,string>("barn",            "tyranids"),
            new KeyValuePair<string,string>("rad_town",        "tyranids"),
            new KeyValuePair<string,string>("cave",            "tyranids"),
            new KeyValuePair<string,string>("stables",         "tyranids"),
        };

        static readonly List<KeyValuePair<string,float>> MonumentRadiusMap =
            new List<KeyValuePair<string,float>>
        {
            new KeyValuePair<string,float>("launch_site",     250f),
            new KeyValuePair<string,float>("airfield",        220f),
            new KeyValuePair<string,float>("excavator",       200f),
            new KeyValuePair<string,float>("harbor",          200f),
            new KeyValuePair<string,float>("water_treatment", 180f),
            new KeyValuePair<string,float>("powerplant",      175f),
            new KeyValuePair<string,float>("power_plant",     175f),
            new KeyValuePair<string,float>("trainyard",       175f),
            new KeyValuePair<string,float>("military_tunnel", 175f),
            new KeyValuePair<string,float>("oilrig",          150f),
            new KeyValuePair<string,float>("compound",        120f),
            new KeyValuePair<string,float>("arctic_research", 120f),
            new KeyValuePair<string,float>("junkyard",        130f),
            new KeyValuePair<string,float>("bandit",          120f),
            new KeyValuePair<string,float>("quarry",           80f),
            new KeyValuePair<string,float>("satellite_dish",   80f),
            new KeyValuePair<string,float>("sphere_tank",      80f),
            new KeyValuePair<string,float>("lighthouse",       60f),
            new KeyValuePair<string,float>("fishing_village",  80f),
            new KeyValuePair<string,float>("ranch",            80f),
            new KeyValuePair<string,float>("large_barn",       80f),
            new KeyValuePair<string,float>("barn",             70f),
            new KeyValuePair<string,float>("rad_town",         80f),
            new KeyValuePair<string,float>("cave",             50f),
            new KeyValuePair<string,float>("stables",          70f),
            new KeyValuePair<string,float>("supermarket",      60f),
            new KeyValuePair<string,float>("gas_station",      50f),
            new KeyValuePair<string,float>("sewer",            70f),
        };

        // ─── Config ───────────────────────────────────────────────────────────

        class PluginConfig
        {
            [JsonProperty("AutoDiscoverZones")]
            public bool AutoDiscoverZones = true;

            [JsonProperty("Factions")]
            public Dictionary<string, FactionDef> Factions = new Dictionary<string, FactionDef>();

            [JsonProperty("NpcArchetypes")]
            public Dictionary<string, NpcArchetype> NpcArchetypes = new Dictionary<string, NpcArchetype>();

            [JsonProperty("Zones")]
            public Dictionary<string, ZoneDef> Zones = new Dictionary<string, ZoneDef>();

            [JsonProperty("Events")]
            public Dictionary<string, EventDef> Events = new Dictionary<string, EventDef>();

            [JsonProperty("Quests")]
            public Dictionary<string, QuestDef> Quests = new Dictionary<string, QuestDef>();

            // Which archetypes spawn in each faction's zones, and how many
            [JsonProperty("FactionSpawn")]
            public Dictionary<string, FactionSpawnConfig> FactionSpawn = new Dictionary<string, FactionSpawnConfig>();

            [JsonProperty("BaseKillEconomics")]
            public int BaseKillEconomics = 30;

            [JsonProperty("BaseKillServerRewards")]
            public int BaseKillServerRewards = 5;

            // Death flavor text, keyed by faction id
            [JsonProperty("DeathFlavor")]
            public Dictionary<string, List<string>> DeathFlavor = new Dictionary<string, List<string>>();

            // ── Support system ────────────────────────────────────────────────
            // Thrones cost to recruit each unit type
            [JsonProperty("RecruitCosts")]
            public Dictionary<string, int> RecruitCosts = new Dictionary<string, int>
            {
                ["guardsman"]  = 25,
                ["sergeant"]   = 75,
                ["commissar"]  = 150,
            };

            // Max total units player can recruit per assault mission, keyed by difficulty tier
            [JsonProperty("DifficultyRecruitCaps")]
            public Dictionary<string, int> DifficultyRecruitCaps = new Dictionary<string, int>
            {
                ["recruit"]  = 0,
                ["soldier"]  = 2,
                ["veteran"]  = 4,
                ["champion"] = 6,
            };

            // Thrones cost for consumable buffs / debuffs
            [JsonProperty("BuyCosts")]
            public Dictionary<string, int> BuyCosts = new Dictionary<string, int>
            {
                ["emperor_blessing"] = 60,
                ["iron_faith"]       = 75,
                ["stimm"]            = 30,
                ["medicae"]          = 40,
                ["orbital_scan"]     = 50,
                ["artillery"]        = 100,
                ["vox_scramble"]     = 80,
            };

            // Assault wave schedule (seconds after assault start) per difficulty tier
            [JsonProperty("AssaultWaveSchedules")]
            public Dictionary<string, List<float>> AssaultWaveSchedules = new Dictionary<string, List<float>>
            {
                ["recruit"]  = new List<float> { },            // no support waves
                ["soldier"]  = new List<float> { 60f },        // 1 wave at T+60s
                ["veteran"]  = new List<float> { 30f, 180f },  // 2 waves
                ["champion"] = new List<float> { 0f, 60f, 180f }, // 3 waves
            };

            // NPC prefab for support troops: guardsman, sergeant, commissar
            [JsonProperty("SupportArchetypePrefabs")]
            public Dictionary<string, string> SupportArchetypePrefabs = new Dictionary<string, string>
            {
                ["guardsman"]  = "assets/prefabs/npc/murderer/murderer.prefab",
                ["sergeant"]   = "assets/prefabs/npc/heavyscientist/heavyscientist.prefab",
                ["commissar"]  = "assets/prefabs/npc/heavyscientist/heavyscientist.prefab",
            };

            // HP multiplier for each support archetype relative to base
            [JsonProperty("SupportArchetypeHP")]
            public Dictionary<string, float> SupportArchetypeHP = new Dictionary<string, float>
            {
                ["guardsman"]  = 150f,
                ["sergeant"]   = 400f,
                ["commissar"]  = 800f,
            };

            // Active assault difficulty (affects recruit cap & wave schedule)
            [JsonProperty("ActiveAssaultDifficulty")]
            public string ActiveAssaultDifficulty = "soldier";
        }

        class FactionDef
        {
            [JsonProperty("DisplayName")]     public string DisplayName;
            [JsonProperty("Color")]           public string Color = "#FFFFFF";
            [JsonProperty("HostileToPlayer")] public bool HostileToPlayer;
        }

        class NpcArchetype
        {
            [JsonProperty("DisplayName")]       public string DisplayName;
            [JsonProperty("Faction")]           public string Faction;
            [JsonProperty("Prefab")]            public string Prefab;
            [JsonProperty("HealthMultiplier")]  public float HealthMultiplier = 1f;
            [JsonProperty("KillEconomics")]     public int KillEconomics;
            [JsonProperty("KillServerRewards")] public int KillServerRewards;
            [JsonProperty("KillXp")]            public int KillXp;
        }

        class FactionSpawnConfig
        {
            [JsonProperty("Archetypes")]     public List<string> Archetypes = new List<string>();
            [JsonProperty("MaxPerZone")]     public int MaxPerZone = 3;
            [JsonProperty("RespawnMinutes")] public float RespawnMinutes = 10f;
        }

        class ZoneDef
        {
            [JsonProperty("DisplayName")] public string DisplayName;
            [JsonProperty("Faction")]     public string Faction;
            [JsonProperty("Center")]      public float[] Center = { 0f, 0f, 0f };
            [JsonProperty("Radius")]      public float Radius = 100f;
            [JsonProperty("IsSafeZone")]  public bool IsSafeZone;
            [JsonProperty("Monument")]    public string Monument;  // source monument name
        }

        class EventDef
        {
            [JsonProperty("DisplayName")]          public string DisplayName;
            [JsonProperty("Faction")]              public string Faction;
            // ZoneId: specific zone to target, or "" to pick a random imperial zone each wave
            [JsonProperty("ZoneId")]               public string ZoneId = "";
            [JsonProperty("WaveCount")]            public int WaveCount = 3;
            [JsonProperty("NpcsPerWave")]          public int NpcsPerWave = 5;
            [JsonProperty("Archetype")]            public string Archetype;
            [JsonProperty("WaveIntervalSeconds")]  public float WaveIntervalSeconds = 90f;
            [JsonProperty("StartAnnouncement")]    public string StartAnnouncement;
            [JsonProperty("EndAnnouncement")]      public string EndAnnouncement;
            [JsonProperty("MinIntervalMinutes")]   public float MinIntervalMinutes = 60f;
            [JsonProperty("MaxIntervalMinutes")]   public float MaxIntervalMinutes = 120f;
        }

        class QuestDef
        {
            [JsonProperty("DisplayName")]          public string DisplayName;
            [JsonProperty("Description")]          public string Description;
            [JsonProperty("Objectives")]           public List<QuestObjective> Objectives = new List<QuestObjective>();
            [JsonProperty("EconomicsReward")]      public int EconomicsReward;
            [JsonProperty("ServerRewardsReward")]  public int ServerRewardsReward;
            [JsonProperty("FactionRep")]           public string FactionRep;
            [JsonProperty("FactionRepGain")]       public int FactionRepGain;
        }

        class QuestObjective
        {
            // Type:   "kill_faction" | "kill_npc"
            // Target: faction id    |  npc archetype id
            [JsonProperty("Type")]   public string Type;
            [JsonProperty("Target")] public string Target;
            [JsonProperty("Count")]  public int Count;
        }

        // ─── Player Data ──────────────────────────────────────────────────────

        class PlayerData
        {
            [JsonProperty("FactionRep")]
            public Dictionary<string, int> FactionRep = new Dictionary<string, int>();

            [JsonProperty("FactionKills")]
            public Dictionary<string, int> FactionKills = new Dictionary<string, int>();

            [JsonProperty("ActiveQuests")]
            public Dictionary<string, QuestProgress> ActiveQuests = new Dictionary<string, QuestProgress>();

            [JsonProperty("CompletedQuests")]
            public List<string> CompletedQuests = new List<string>();
        }

        class QuestProgress
        {
            [JsonProperty("Progress")]
            public Dictionary<int, int> Progress = new Dictionary<int, int>();
        }

        class RecruitedUnit
        {
            [JsonProperty("UnitType")] public string UnitType;
            [JsonProperty("Count")]    public int Count;
        }

        class AssaultBuff
        {
            [JsonProperty("EmperorBlessing")] public bool EmperorBlessing;
            [JsonProperty("IronFaith")]       public bool IronFaith;
            [JsonProperty("Stimm")]           public bool Stimm;
            [JsonProperty("OrbitalScan")]     public bool OrbitalScan;
            [JsonProperty("Artillery")]       public bool Artillery;
            [JsonProperty("VoxScramble")]     public bool VoxScramble;
        }

        // ─── Fields ───────────────────────────────────────────────────────────

        PluginConfig _cfg;
        Dictionary<ulong, PlayerData>   _playerData   = new Dictionary<ulong, PlayerData>();
        Dictionary<ulong, string>       _spawnedNpcs  = new Dictionary<ulong, string>();
        Dictionary<string, List<ulong>> _activeEvents = new Dictionary<string, List<ulong>>();
        Dictionary<string, Timer>       _eventTimers  = new Dictionary<string, Timer>();
        // Zone population tracking
        Dictionary<string, List<ulong>> _zoneNpcs = new Dictionary<string, List<ulong>>();
        Dictionary<ulong, string>       _npcZone  = new Dictionary<ulong, string>();
        // Quest UI tracking (which players have the panel open)
        HashSet<ulong> _questUiOpen = new HashSet<ulong>();
        // Map markers for faction zone visualization
        List<MapMarkerGenericRadius> _mapMarkers = new List<MapMarkerGenericRadius>();

        // ── Hub entities ──────────────────────────────────────────────────────
        List<ulong> _hubEntities = new List<ulong>();

        // ── Support system ────────────────────────────────────────────────────
        // netIds of all currently deployed support NPCs (used by OnNpcTarget)
        HashSet<ulong> _supportNpcIds = new HashSet<ulong>();
        // per-player spawned support netIds (for recall)
        Dictionary<ulong, List<ulong>> _supportEntities = new Dictionary<ulong, List<ulong>>();
        // per-player recruitment roster (persisted)
        Dictionary<ulong, List<RecruitedUnit>> _playerRecruits = new Dictionary<ulong, List<RecruitedUnit>>();
        // per-player active assault buffs (cleared per mission)
        Dictionary<ulong, AssaultBuff> _playerBuffs = new Dictionary<ulong, AssaultBuff>();
        // per-player active assault zone ID (for wave targeting)
        Dictionary<ulong, string> _playerAssaultZone = new Dictionary<ulong, string>();
        // per-player damage/resistance multipliers (from buffs)
        Dictionary<ulong, float> _playerDamageMultiplier = new Dictionary<ulong, float>();
        Dictionary<ulong, float> _playerResistMultiplier = new Dictionary<ulong, float>();
        // per-player buff expiry timers
        Dictionary<ulong, Timer> _playerBuffTimers = new Dictionary<ulong, Timer>();

        [PluginReference] Oxide.Core.Plugins.Plugin Economics;
        [PluginReference] Oxide.Core.Plugins.Plugin ServerRewards;
        [PluginReference] Oxide.Core.Plugins.Plugin XPerience;
        [PluginReference] Oxide.Core.Plugins.Plugin ZoneManager;

        // ─── Oxide Lifecycle ─────────────────────────────────────────────────

        protected override void LoadDefaultConfig() => Config.WriteObject(BuildDefaultConfig(), true);

        void OnServerInitialized()
        {
            _cfg = Config.ReadObject<PluginConfig>();
            if (_cfg == null) { _cfg = BuildDefaultConfig(); Config.WriteObject(_cfg, true); }

            var stored = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>("WH40K/players");
            if (stored != null) _playerData = stored;

            var storedRecruits = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<RecruitedUnit>>>("WH40K/recruits");
            if (storedRecruits != null) _playerRecruits = storedRecruits;

            // Migrate: clear any stale hardcoded ZoneIds from old config (imperial_hub, etc.)
            bool migrated = false;
            foreach (var evt in _cfg.Events.Values)
            {
                if (!string.IsNullOrEmpty(evt.ZoneId) && !_cfg.Zones.ContainsKey(evt.ZoneId))
                {
                    evt.ZoneId = "";
                    migrated = true;
                }
            }
            if (migrated) Puts("[WH40K] Migrated stale event ZoneIds to dynamic zone selection.");

            if (_cfg.AutoDiscoverZones)
                AutoDiscoverMonumentZones();

            ScheduleAllEvents();
            timer.Once(5f, PopulateAllZones);   // terrain must be fully loaded
            timer.Once(6f, () => { PlaceMapMarkers(); SetupZoneManagerZones(); });

            int imperial = _cfg.Zones.Values.Count(z => z.Faction == "imperium");
            int ork      = _cfg.Zones.Values.Count(z => z.Faction == "orks");
            int tyranid  = _cfg.Zones.Values.Count(z => z.Faction == "tyranids");
            Puts($"[WH40K] v0.4.2 loaded — {_cfg.Factions.Count} factions, " +
                 $"{_cfg.Zones.Count} zones ({imperial} imperial, {ork} ork, {tyranid} tyranid), " +
                 $"{_cfg.Events.Count} events, {_cfg.Quests.Count} quests");
        }

        void Unload()
        {
            foreach (var t in _eventTimers.Values) t?.Destroy();
            foreach (var t in _playerBuffTimers.Values) t?.Destroy();
            // Close quest UI for any players who have it open
            foreach (var uid in _questUiOpen)
            {
                var p = BasePlayer.FindByID(uid);
                if (p != null) CuiHelper.DestroyUi(p, "WH40K.Quest");
            }
            // Despawn all zone-population NPCs so they don't persist as orphans
            foreach (var netId in _npcZone.Keys.ToList())
            {
                var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as BaseEntity;
                entity?.Kill();
            }
            // Despawn hub entities
            foreach (var netId in _hubEntities)
            {
                var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as BaseEntity;
                entity?.Kill();
            }
            _hubEntities.Clear();
            // Despawn all support NPCs
            foreach (var netId in _supportNpcIds.ToList())
            {
                var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as BaseEntity;
                entity?.Kill();
            }
            _supportNpcIds.Clear();
            RemoveMapMarkers();
            RemoveZoneManagerZones();
            SaveAllData();
        }

        // ─── Monument Auto-Discovery ───────────────────────────────────────────

        void AutoDiscoverMonumentZones()
        {
            var monuments = TerrainMeta.Path?.Monuments;
            if (monuments == null || monuments.Count == 0)
            {
                Puts("[WH40K] No monuments found — zone discovery skipped.");
                return;
            }

            _cfg.Zones.Clear();
            int discovered = 0;

            foreach (var monument in monuments)
            {
                if (monument == null) continue;
                string rawName = monument.name ?? "";
                string name = rawName.ToLower()
                    .Replace("(clone)", "")
                    .Replace("assets/bundled/prefabs/autospawn/monument/", "")
                    .Trim()
                    .TrimEnd('/');
                if (string.IsNullOrEmpty(name)) continue;

                string faction = GetMonumentFaction(name);
                if (faction == null) continue;

                float radius = GetMonumentRadius(name);
                var pos = monument.transform.position;

                // Stable zone ID from last path segment
                string baseId = name.Contains("/")
                    ? name.Substring(name.LastIndexOf('/') + 1)
                    : name;
                baseId = baseId.Replace(" ", "_").Replace("-", "_");

                string zoneId = baseId;
                int suffix = 2;
                while (_cfg.Zones.ContainsKey(zoneId))
                    zoneId = baseId + "_" + (suffix++);

                _cfg.Zones[zoneId] = new ZoneDef
                {
                    DisplayName = GetMonumentDisplayName(name, faction),
                    Faction     = faction,
                    Center      = new float[] { pos.x, pos.y, pos.z },
                    Radius      = radius,
                    IsSafeZone  = (faction == "imperium"),
                    Monument    = name
                };
                discovered++;
            }

            Config.WriteObject(_cfg, true);

            int imp = _cfg.Zones.Values.Count(z => z.Faction == "imperium");
            int ork = _cfg.Zones.Values.Count(z => z.Faction == "orks");
            int tyr = _cfg.Zones.Values.Count(z => z.Faction == "tyranids");
            Puts($"[WH40K] Monument discovery: {discovered} zones ({imp} imperial, {ork} ork, {tyr} tyranid)");

            // Refresh map markers and ZoneManager zones
            RemoveMapMarkers();
            RemoveZoneManagerZones();
            PlaceMapMarkers();
            SetupZoneManagerZones();
        }

        string GetMonumentFaction(string name)
        {
            foreach (var kvp in MonumentFactionMap)
                if (name.Contains(kvp.Key)) return kvp.Value;
            return null;
        }

        float GetMonumentRadius(string name)
        {
            foreach (var kvp in MonumentRadiusMap)
                if (name.Contains(kvp.Key)) return kvp.Value;
            return 100f;
        }

        string GetMonumentDisplayName(string name, string faction)
        {
            // Extract display-friendly name from last path segment
            string clean = name.Contains("/") ? name.Substring(name.LastIndexOf('/') + 1) : name;
            // Strip .prefab extension
            clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\.prefab$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // Strip trailing numbers and variant letters: harbor_1 → harbor, fishing_village_a → fishing_village
            clean = System.Text.RegularExpressions.Regex.Replace(clean, @"[_\-][0-9]+$", "");
            clean = System.Text.RegularExpressions.Regex.Replace(clean, @"[_\-][a-c]$", "");
            // Underscores → spaces, title case
            clean = clean.Replace("_", " ").Trim();
            clean = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(clean);

            switch (faction)
            {
                case "orks":     return "Ork Territory: " + clean;
                case "tyranids": return "Tyranid Zone: "  + clean;
                case "imperium": return "Imperial Sector: " + clean;
                default:         return clean;
            }
        }

        // ─── Kill Hooks ───────────────────────────────────────────────────────

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;

            // Player was killed — check if by a WH40K NPC for death flavor
            if (entity is BasePlayer deadPlayer && !(deadPlayer is NPCPlayer))
            {
                var initiatorId = info.Initiator?.net?.ID.Value ?? 0;
                if (initiatorId > 0 && _spawnedNpcs.TryGetValue(initiatorId, out var killerArch))
                {
                    if (_cfg.NpcArchetypes.TryGetValue(killerArch, out var archetypeDef))
                    {
                        var flavor = PickDeathFlavor(archetypeDef.Faction);
                        if (!string.IsNullOrEmpty(flavor))
                            Broadcast($"<color=#FFD700>[WH40K]</color> <color=#888888>{deadPlayer.displayName} — {flavor}</color>");
                    }
                }
                return;  // don't apply faction-kill tracking for player deaths
            }

            var killer = info.InitiatorPlayer;
            if (killer == null || !killer.IsConnected) return;

            var netId = entity.net?.ID.Value ?? 0;

            if (netId > 0 && _spawnedNpcs.TryGetValue(netId, out var archetypeId))
            {
                OnWH40KKill(killer, archetypeId, netId);
                _spawnedNpcs.Remove(netId);
                return;
            }

            // Don't reward kills of our own support NPCs
            if (_supportNpcIds.Contains(netId)) { _supportNpcIds.Remove(netId); return; }

            var faction = VanillaEntityFaction(entity);
            if (faction != null)
                OnFactionKill(killer, faction);
        }

        void OnWH40KKill(BasePlayer killer, string archetypeId, ulong netId)
        {
            if (!_cfg.NpcArchetypes.TryGetValue(archetypeId, out var arch)) return;
            var data = GetOrCreate(killer.userID);

            Tally(data.FactionKills, arch.Faction);

            if (arch.KillEconomics > 0)
                Economics?.Call("Deposit", killer.UserIDString, (double)arch.KillEconomics);
            if (arch.KillServerRewards > 0)
                ServerRewards?.Call("AddPoints", killer.userID, arch.KillServerRewards);
            if (arch.KillXp > 0)
                XPerience?.Call("GainXp", killer, arch.KillXp);

            var color = _cfg.Factions.TryGetValue(arch.Faction, out var f) ? f.Color : "#FFFFFF";
            SendReply(killer,
                $"<color=#FFD700>[WH40K]</color> <color={color}>{arch.DisplayName}</color> slain! " +
                $"<color=#4CAF50>+{arch.KillEconomics} Thrones</color>");

            foreach (var evtId in new List<string>(_activeEvents.Keys))
            {
                _activeEvents[evtId].Remove(netId);
                if (_activeEvents[evtId].Count == 0)
                    EndEvent(evtId, cleared: true);
            }

            // Zone repopulation: schedule respawn if this was a zone-population NPC
            if (_npcZone.TryGetValue(netId, out var deadZoneId))
            {
                _npcZone.Remove(netId);
                if (_zoneNpcs.TryGetValue(deadZoneId, out var zoneList)) zoneList.Remove(netId);
                if (_cfg.Zones.TryGetValue(deadZoneId, out var dz) &&
                    _cfg.FactionSpawn.TryGetValue(dz.Faction, out var sc) && sc.RespawnMinutes > 0)
                {
                    float delay = sc.RespawnMinutes * 60f;
                    timer.Once(delay, () =>
                    {
                        if (_cfg.Zones.TryGetValue(deadZoneId, out var dz2))
                            PopulateZone(deadZoneId, dz2);
                    });
                }
            }

            UpdateQuestProgress(killer, data, "kill_faction", arch.Faction);
            UpdateQuestProgress(killer, data, "kill_npc", archetypeId);
        }

        void OnFactionKill(BasePlayer killer, string faction)
        {
            var data = GetOrCreate(killer.userID);
            Tally(data.FactionKills, faction);

            if (_cfg.BaseKillEconomics > 0)
                Economics?.Call("Deposit", killer.UserIDString, (double)_cfg.BaseKillEconomics);
            if (_cfg.BaseKillServerRewards > 0)
                ServerRewards?.Call("AddPoints", killer.userID, _cfg.BaseKillServerRewards);

            var color = _cfg.Factions.TryGetValue(faction, out var f) ? f.Color : "#AAAAAA";
            var fname = f?.DisplayName ?? faction;
            SendReply(killer,
                $"<color=#FFD700>[WH40K]</color> <color={color}>{fname}</color> kill. " +
                $"<color=#4CAF50>+{_cfg.BaseKillEconomics} Thrones</color>");

            UpdateQuestProgress(killer, data, "kill_faction", faction);
        }

        string VanillaEntityFaction(BaseCombatEntity entity)
        {
            if (entity is BaseAnimalNPC) return "tyranids";
            if (entity is NPCPlayer)     return "orks";
            return null;
        }

        // ─── Zone System ──────────────────────────────────────────────────────

        ZoneDef ZoneAt(Vector3 pos)
        {
            foreach (var z in _cfg.Zones.Values)
            {
                var c = new Vector3(z.Center[0], z.Center[1], z.Center[2]);
                if (Vector3.Distance(pos, c) <= z.Radius) return z;
            }
            return null;
        }

        // Returns a random zone belonging to targetFaction, or null
        ZoneDef RandomZoneOfFaction(string targetFaction)
        {
            var candidates = _cfg.Zones.Values.Where(z => z.Faction == targetFaction).ToList();
            if (candidates.Count == 0) return null;
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        // Returns the imperial zone nearest to any online player (falls back to random)
        ZoneDef NearestImperialZoneToAnyPlayer()
        {
            var zones = _cfg.Zones.Values.Where(z => z.Faction == "imperium").ToList();
            if (zones.Count == 0) { Puts("[WH40K] NearestImperial: no imperial zones found!"); return null; }
            var players = BasePlayer.activePlayerList;
            Puts($"[WH40K] NearestImperial: {zones.Count} imperial zones, {players.Count} players online");
            if (players.Count == 0) { Puts("[WH40K] NearestImperial: no players online — picking random zone"); return zones[UnityEngine.Random.Range(0, zones.Count)]; }
            ZoneDef best = null;
            float bestDist = float.MaxValue;
            foreach (var z in zones)
            {
                var zc = new Vector3(z.Center[0], z.Center[1], z.Center[2]);
                foreach (var p in players)
                {
                    float d = Vector3.Distance(p.transform.position, zc);
                    if (d < bestDist) { bestDist = d; best = z; }
                }
            }
            if (best != null) Puts($"[WH40K] NearestImperial: selected '{best.DisplayName}' (nearest player dist={bestDist:F0}m)");
            return best ?? zones[UnityEngine.Random.Range(0, zones.Count)];
        }

        // ─── Zone Population ──────────────────────────────────────────────────

        void PopulateAllZones()
        {
            if (_cfg.FactionSpawn == null || _cfg.FactionSpawn.Count == 0)
            {
                Puts("[WH40K] FactionSpawn config is empty — skipping zone population. Add FactionSpawn to WH40K.json.");
                return;
            }
            foreach (var kvp in _cfg.Zones)
                PopulateZone(kvp.Key, kvp.Value);
            int total = _zoneNpcs.Values.Sum(l => l.Count);
            Puts($"[WH40K] Zone population complete: {total} NPCs across {_zoneNpcs.Count} zones");
        }

        void PopulateZone(string zoneId, ZoneDef zone)
        {
            if (!_cfg.FactionSpawn.TryGetValue(zone.Faction, out var sc)) return;
            if (sc.Archetypes == null || sc.Archetypes.Count == 0) return;

            if (!_zoneNpcs.ContainsKey(zoneId)) _zoneNpcs[zoneId] = new List<ulong>();
            var list = _zoneNpcs[zoneId];
            // Prune dead entries
            list.RemoveAll(id => !_spawnedNpcs.ContainsKey(id));

            int toSpawn = sc.MaxPerZone - list.Count;
            for (int i = 0; i < toSpawn; i++)
            {
                string arch = sc.Archetypes[UnityEngine.Random.Range(0, sc.Archetypes.Count)];
                var pos = RandomInZone(zone);
                var netId = SpawnNpc(arch, pos, zoneId);
                if (netId > 0) list.Add(netId);
            }
        }

        // ─── Event System ─────────────────────────────────────────────────────

        void ScheduleAllEvents()
        {
            foreach (var id in _cfg.Events.Keys) ScheduleEvent(id);
        }

        void ScheduleEvent(string eventId)
        {
            if (!_cfg.Events.TryGetValue(eventId, out var evt)) return;
            _eventTimers.TryGetValue(eventId, out var old); old?.Destroy();
            float delay = UnityEngine.Random.Range(evt.MinIntervalMinutes, evt.MaxIntervalMinutes) * 60f;

            // 5-minute auspex warning (only when event is still more than 6 min away)
            if (delay > 360f)
            {
                string warnFaction = evt.Faction.ToUpper();
                string warnName    = evt.DisplayName;
                timer.Once(delay - 300f, () =>
                {
                    if (!_activeEvents.ContainsKey(eventId))
                        Broadcast($"<color=#FFD700>[WH40K]</color> <color=#FF8C00>⚠ Auspex contact. {warnFaction} signatures detected. Expect {warnName} in 5 minutes. Prepare defenses.</color>");
                });
            }

            _eventTimers[eventId] = timer.Once(delay, () => StartEvent(eventId));
            Puts($"[WH40K] Event '{eventId}' scheduled in {delay / 60f:F0} min");
        }

        void StartEvent(string eventId)
        {
            if (!_cfg.Events.TryGetValue(eventId, out var evt)) return;

            if (_activeEvents.ContainsKey(eventId))
            {
                Puts($"[WH40K] Event '{eventId}' already active, skipping");
                ScheduleEvent(eventId);
                return;
            }

            _activeEvents[eventId] = new List<ulong>();
            Broadcast($"<color=#FFD700>[WH40K]</color> <color=#FF4444>{evt.StartAnnouncement}</color>");
            SpawnWave(eventId, evt, 1);
        }

        void SpawnWave(string eventId, EventDef evt, int waveNum)
        {
            if (!_activeEvents.ContainsKey(eventId)) return;

            if (waveNum > evt.WaveCount)
            {
                timer.Once(evt.WaveIntervalSeconds * 2f, () => EndEvent(eventId, cleared: false));
                return;
            }

            // Resolve zone: specific override, or nearest imperial zone to any online player
            ZoneDef zone = null;
            if (!string.IsNullOrEmpty(evt.ZoneId))
                _cfg.Zones.TryGetValue(evt.ZoneId, out zone);
            if (zone == null)
                zone = NearestImperialZoneToAnyPlayer();

            if (zone == null)
            {
                Puts($"[WH40K] Event '{eventId}': no imperial zone found to attack");
                EndEvent(eventId, cleared: false);
                return;
            }

            Broadcast($"<color=#FFD700>[WH40K]</color> <color=#FF4444>Wave {waveNum}/{evt.WaveCount} — attacking <color=white>{zone.DisplayName}</color>!</color>");

            for (int i = 0; i < evt.NpcsPerWave; i++)
            {
                var pos = RandomInZone(zone);
                var netId = SpawnNpc(evt.Archetype, pos);
                if (netId > 0 && _activeEvents.ContainsKey(eventId))
                    _activeEvents[eventId].Add(netId);
            }

            timer.Once(evt.WaveIntervalSeconds, () => SpawnWave(eventId, evt, waveNum + 1));
        }

        void EndEvent(string eventId, bool cleared)
        {
            if (!_activeEvents.ContainsKey(eventId)) return;

            foreach (var netId in _activeEvents[eventId])
                _spawnedNpcs.Remove(netId);
            _activeEvents.Remove(eventId);

            if (_cfg.Events.TryGetValue(eventId, out var evt))
            {
                string msg = cleared ? evt.EndAnnouncement : $"The {evt.DisplayName} has subsided... for now.";
                Broadcast($"<color=#FFD700>[WH40K]</color> <color={(cleared ? "#4CAF50" : "#AAAAAA")}>{msg}</color>");
            }

            ScheduleEvent(eventId);
        }

        // ─── NPC Spawning ─────────────────────────────────────────────────────

        ulong SpawnNpc(string archetypeId, Vector3 pos, string zoneId = null)
        {
            if (!_cfg.NpcArchetypes.TryGetValue(archetypeId, out var arch)) { Puts($"[WH40K] SpawnNpc: archetype '{archetypeId}' not in config"); return 0; }
            if (string.IsNullOrEmpty(arch.Prefab)) { Puts($"[WH40K] SpawnNpc: '{archetypeId}' has no Prefab set"); return 0; }

            // Snap to terrain height so scientists don't spawn underground or floating
            var spawnPos = pos;
            spawnPos.y = TerrainMeta.HeightMap?.GetHeight(pos) ?? pos.y;

            var entity = GameManager.server.CreateEntity(arch.Prefab, spawnPos, Quaternion.identity);
            if (entity == null) { Puts($"[WH40K] SpawnNpc: CreateEntity returned null for prefab '{arch.Prefab}' — check prefab path"); return 0; }

            entity.Spawn();

            if (arch.HealthMultiplier != 1f && entity is BaseCombatEntity bce)
            {
                float hp = bce.startHealth * arch.HealthMultiplier;
                bce.InitializeHealth(hp, hp);
            }

            var netId = entity.net?.ID.Value ?? 0;
            if (netId == 0) Puts($"[WH40K] SpawnNpc: '{archetypeId}' entity spawned but net.ID is 0 (entity may have been destroyed immediately)");
            if (netId > 0)
            {
                _spawnedNpcs[netId] = archetypeId;
                if (zoneId != null) _npcZone[netId] = zoneId;
            }
            return netId;
        }

        Vector3 RandomInZone(ZoneDef z)
        {
            var center = new Vector3(z.Center[0], z.Center[1], z.Center[2]);
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float dist  = UnityEngine.Random.Range(5f, z.Radius * 0.75f);
            var pos = center + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            pos.y = TerrainMeta.HeightMap?.GetHeight(pos) ?? pos.y;
            return pos;
        }

        // ─── Quest Engine ─────────────────────────────────────────────────────

        void UpdateQuestProgress(BasePlayer player, PlayerData data, string objType, string target)
        {
            bool changed = false;
            foreach (var questId in new List<string>(data.ActiveQuests.Keys))
            {
                if (!_cfg.Quests.TryGetValue(questId, out var quest)) continue;
                var prog = data.ActiveQuests[questId];
                bool advanced = false;

                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    var obj = quest.Objectives[i];
                    if (obj.Type != objType || obj.Target != target) continue;
                    if (!prog.Progress.ContainsKey(i)) prog.Progress[i] = 0;
                    if (prog.Progress[i] < obj.Count)
                    {
                        prog.Progress[i]++;
                        advanced = true;
                        changed  = true;
                    }
                }

                if (!advanced) continue;

                bool done = true;
                for (int i = 0; i < quest.Objectives.Count; i++)
                    if (!prog.Progress.TryGetValue(i, out int v) || v < quest.Objectives[i].Count)
                    { done = false; break; }

                if (done) CompleteQuest(player, data, questId, quest);
            }

            if (changed)
            {
                SaveAllData();
                // Refresh open quest UI so progress bars update
                if (_questUiOpen.Contains(player.userID))
                    timer.Once(0.1f, () => { if (player.IsConnected) OpenQuestUI(player); });
            }
        }

        void CompleteQuest(BasePlayer player, PlayerData data, string questId, QuestDef quest)
        {
            data.ActiveQuests.Remove(questId);
            if (!data.CompletedQuests.Contains(questId)) data.CompletedQuests.Add(questId);

            if (quest.EconomicsReward > 0)
                Economics?.Call("Deposit", player.UserIDString, (double)quest.EconomicsReward);
            if (quest.ServerRewardsReward > 0)
                ServerRewards?.Call("AddPoints", player.userID, quest.ServerRewardsReward);
            if (!string.IsNullOrEmpty(quest.FactionRep))
                Tally(data.FactionRep, quest.FactionRep, quest.FactionRepGain);

            SendReply(player,
                $"\n<color=#FFD700>╔══ QUEST COMPLETE ══════════════════╗</color>" +
                $"\n<color=#FFD700>║</color> <color=white>{quest.DisplayName}</color>" +
                $"\n<color=#FFD700>║</color> <color=#4CAF50>+{quest.EconomicsReward} Thrones</color>" +
                (quest.FactionRepGain > 0 ? $"  <color=#87CEEB>+{quest.FactionRepGain} Imperium Rep</color>" : "") +
                $"\n<color=#FFD700>╚════════════════════════════════════╝</color>");

            // Refresh quest panel if open
            if (_questUiOpen.Contains(player.userID))
                timer.Once(0.1f, () => { if (player.IsConnected) OpenQuestUI(player); });
        }

        // ─── Chat Commands ────────────────────────────────────────────────────

        [ChatCommand("wh40k")]
        void CmdWH40K(BasePlayer player, string cmd, string[] args)
        {
            string sub = args.Length > 0 ? args[0].ToLower() : "help";
            switch (sub)
            {
                case "status":
                case "stats":  ShowStatus(player); break;
                case "quests": OpenQuestUI(player); break;
                case "where":  ShowZoneInfo(player); break;
                case "accept":
                    if (args.Length > 1) AcceptQuest(player, args[1]);
                    else SendReply(player, "Usage: /wh40k accept <questId>");
                    break;

                case "recruit":
                {
                    string unitArg = args.Length > 1 ? args[1].ToLower() : "list";
                    if (unitArg == "list") ShowRecruitList(player);
                    else if (unitArg == "dismiss")
                    {
                        GetRecruits(player.userID).Clear();
                        SaveAllData();
                        SendReply(player, "<color=#FFD700>[WH40K]</color> Roster dismissed. No refund.");
                    }
                    else DoRecruit(player, unitArg);
                    break;
                }

                case "buy":
                {
                    string itemArg = args.Length > 1 ? args[1].ToLower() : "";
                    if (string.IsNullOrEmpty(itemArg))
                        SendReply(player, "Usage: /wh40k buy <item>  Items: emperor_blessing iron_faith stimm medicae orbital_scan artillery vox_scramble");
                    else DoBuy(player, itemArg);
                    break;
                }

                case "assault":
                {
                    string assaultSub = args.Length > 1 ? args[1].ToLower() : "start";
                    if (assaultSub == "start")
                        StartAssault(player);
                    else if (assaultSub == "end")
                    {
                        RecallSupport(player.userID);
                        SendReply(player, "<color=#FFD700>[WH40K]</color> Assault ended. Support recalled.");
                    }
                    else
                        SendReply(player, "Usage: /wh40k assault start|end");
                    break;
                }

                default:
                    SendReply(player,
                        "<color=#FFD700>[WH40K]</color> Commands:\n" +
                        "  <color=white>/wh40k status</color>   — faction kills and rep\n" +
                        "  <color=white>/wh40k quests</color>   — available quests\n" +
                        "  <color=white>/wh40k accept <id></color>  — start a quest\n" +
                        "  <color=white>/wh40k where</color>    — your current faction zone\n" +
                        "  <color=white>/wh40k recruit list|guardsman|sergeant|commissar|dismiss</color>\n" +
                        "  <color=white>/wh40k buy emperor_blessing|iron_faith|stimm|medicae|orbital_scan|artillery|vox_scramble</color>\n" +
                        "  <color=white>/wh40k assault start|end</color>  — deploy/recall support");
                    break;
            }
        }

        void ShowStatus(BasePlayer player)
        {
            var data = GetOrCreate(player.userID);
            var sb = new StringBuilder();
            sb.AppendLine("<color=#FFD700>╔══ WH40K STATUS ════════════════════╗</color>");
            sb.AppendLine("<color=#FFD700>║</color> <color=#AAAAAA>Faction Kills:</color>");
            foreach (var kvp in data.FactionKills)
            {
                var fname = _cfg.Factions.TryGetValue(kvp.Key, out var f) ? f.DisplayName : kvp.Key;
                sb.AppendLine($"<color=#FFD700>║</color>   <color={f?.Color ?? "#AAAAAA"}>{fname}</color>: <color=white>{kvp.Value}</color>");
            }
            if (data.FactionKills.Count == 0)
                sb.AppendLine("<color=#FFD700>║</color>   <color=#555555>None yet.</color>");
            sb.AppendLine("<color=#FFD700>║</color> <color=#AAAAAA>Reputation:</color>");
            foreach (var kvp in data.FactionRep)
            {
                var fname = _cfg.Factions.TryGetValue(kvp.Key, out var f2) ? f2.DisplayName : kvp.Key;
                sb.AppendLine($"<color=#FFD700>║</color>   <color=#87CEEB>{fname}</color>: <color=white>{kvp.Value}</color>");
            }
            sb.AppendLine($"<color=#FFD700>║</color> Quests: <color=white>{data.ActiveQuests.Count}</color> active, <color=white>{data.CompletedQuests.Count}</color> done");
            sb.AppendLine("<color=#FFD700>╚════════════════════════════════════╝</color>");
            SendReply(player, sb.ToString());
        }

        void ShowZoneInfo(BasePlayer player)
        {
            var zone = ZoneAt(player.transform.position);
            if (zone == null)
            {
                SendReply(player, "<color=#FFD700>[WH40K]</color> You are in unclaimed territory.");
                return;
            }
            var color = _cfg.Factions.TryGetValue(zone.Faction, out var f) ? f.Color : "#FFFFFF";
            string safety = zone.IsSafeZone ? " <color=#4CAF50>[SAFE ZONE]</color>" : " <color=#FF4444>[HOSTILE]</color>";
            SendReply(player, $"<color=#FFD700>[WH40K]</color> <color={color}>{zone.DisplayName}</color>{safety}");
        }

        void OpenQuestUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "WH40K.Quest");
            _questUiOpen.Add(player.userID);

            var data = GetOrCreate(player.userID);
            var cu   = new CuiElementContainer();

            // ── Root panel ──────────────────────────────────────────────────
            cu.Add(new CuiPanel {
                Image           = { Color = "0.07 0.07 0.07 0.97" },
                RectTransform   = { AnchorMin = "0.2 0.05", AnchorMax = "0.8 0.95" },
                CursorEnabled   = true
            }, "Hud", "WH40K.Quest");

            // ── Header bar ──────────────────────────────────────────────────
            cu.Add(new CuiPanel {
                Image         = { Color = "0.14 0.09 0.01 1" },
                RectTransform = { AnchorMin = "0 0.92", AnchorMax = "1 1" }
            }, "WH40K.Quest", "WH40K.Quest.Hdr");

            cu.Add(new CuiLabel {
                Text          = { Text = "⚔  IMPERIAL MISSION BRIEFINGS", FontSize = 16,
                                  Align = TextAnchor.MiddleCenter, Color = "1 0.84 0 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.9 1" }
            }, "WH40K.Quest.Hdr");

            cu.Add(new CuiButton {
                Button        = { Command = "wh40k.ui close", Color = "0.55 0.1 0.1 0.9" },
                RectTransform = { AnchorMin = "0.92 0.1", AnchorMax = "0.99 0.9" },
                Text          = { Text = "✕", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, "WH40K.Quest.Hdr");

            // ── Quest rows ──────────────────────────────────────────────────
            var quests  = _cfg.Quests.ToList();
            float rowH  = 0.155f;
            float gap   = 0.012f;
            float startY = 0.905f;

            for (int qi = 0; qi < quests.Count; qi++)
            {
                string questId = quests[qi].Key;
                var    quest   = quests[qi].Value;
                bool active    = data.ActiveQuests.ContainsKey(questId);
                bool done      = data.CompletedQuests.Contains(questId);

                float y2 = startY - qi * (rowH + gap);
                float y1 = y2 - rowH;
                if (y1 < 0.02f) break;

                string row = $"WH40K.Quest.Row{qi}";
                string bg  = done   ? "0.12 0.12 0.12 0.75"
                           : active ? "0.04 0.18 0.04 0.85"
                           :          "0.11 0.11 0.16 0.85";

                cu.Add(new CuiPanel {
                    Image         = { Color = bg },
                    RectTransform = { AnchorMin = $"0.02 {y1:F3}", AnchorMax = $"0.98 {y2:F3}" }
                }, "WH40K.Quest", row);

                // Status badge
                string badge     = done ? "DONE" : active ? "ACTIVE" : "AVAIL";
                string badgeCol  = done ? "0.4 0.4 0.4 1" : active ? "0.25 0.9 0.25 1" : "0.75 0.75 0.75 1";
                cu.Add(new CuiLabel {
                    Text          = { Text = badge, FontSize = 8, Align = TextAnchor.MiddleCenter, Color = badgeCol },
                    RectTransform = { AnchorMin = "0.01 0.55", AnchorMax = "0.12 1" }
                }, row);

                // Quest name
                string nameCol = done ? "0.45 0.45 0.45 1" : "1 0.88 0.5 1";
                cu.Add(new CuiLabel {
                    Text          = { Text = quest.DisplayName, FontSize = 12,
                                      Align = TextAnchor.MiddleLeft, Color = nameCol },
                    RectTransform = { AnchorMin = "0.13 0.52", AnchorMax = "0.72 1" }
                }, row);

                if (!done)
                {
                    // Description
                    cu.Add(new CuiLabel {
                        Text          = { Text = quest.Description, FontSize = 8,
                                          Align = TextAnchor.UpperLeft, Color = "0.6 0.6 0.6 1" },
                        RectTransform = { AnchorMin = "0.13 0.02", AnchorMax = "0.72 0.5" }
                    }, row);

                    if (active && quest.Objectives.Count > 0)
                    {
                        // Progress bar (shows first objective; enough for most quests)
                        var  obj  = quest.Objectives[0];
                        var  prog = data.ActiveQuests[questId];
                        int  cur  = prog.Progress.TryGetValue(0, out int pv) ? pv : 0;
                        float pct = Mathf.Clamp01((float)cur / Mathf.Max(1, obj.Count));

                        string barBg = $"{row}.Bar";
                        cu.Add(new CuiPanel {
                            Image         = { Color = "0.2 0.2 0.2 0.9" },
                            RectTransform = { AnchorMin = "0.73 0.3", AnchorMax = "0.98 0.65" }
                        }, row, barBg);

                        if (pct > 0.01f)
                            cu.Add(new CuiPanel {
                                Image         = { Color = "0.18 0.78 0.18 0.9" },
                                RectTransform = { AnchorMin = "0 0", AnchorMax = $"{pct:F2} 1" }
                            }, barBg);

                        cu.Add(new CuiLabel {
                            Text          = { Text = $"{cur}/{obj.Count}", FontSize = 9,
                                              Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }, barBg);

                        cu.Add(new CuiLabel {
                            Text          = { Text = $"+{quest.EconomicsReward}T", FontSize = 9,
                                              Align = TextAnchor.UpperCenter, Color = "0.35 0.85 0.35 1" },
                            RectTransform = { AnchorMin = "0.73 0.67", AnchorMax = "0.98 1" }
                        }, row);
                    }
                    else if (!active)
                    {
                        cu.Add(new CuiLabel {
                            Text          = { Text = $"+{quest.EconomicsReward}T", FontSize = 9,
                                              Align = TextAnchor.MiddleCenter, Color = "0.55 0.88 0.28 1" },
                            RectTransform = { AnchorMin = "0.73 0.55", AnchorMax = "0.98 1" }
                        }, row);

                        cu.Add(new CuiButton {
                            Button        = { Command = $"wh40k.ui accept {questId}", Color = "0.08 0.42 0.08 0.95" },
                            RectTransform = { AnchorMin = "0.73 0.06", AnchorMax = "0.98 0.5" },
                            Text          = { Text = "ACCEPT", FontSize = 11,
                                              Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                        }, row);
                    }
                }
                else
                {
                    cu.Add(new CuiLabel {
                        Text          = { Text = "✓ Complete", FontSize = 10,
                                          Align = TextAnchor.MiddleCenter, Color = "0.35 0.35 0.35 1" },
                        RectTransform = { AnchorMin = "0.73 0.1", AnchorMax = "0.98 0.9" }
                    }, row);
                }
            }

            CuiHelper.AddUi(player, cu);
        }

        [ConsoleCommand("wh40k.ui")]
        void CmdQuestUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            switch (arg.GetString(0).ToLower())
            {
                case "close":
                    _questUiOpen.Remove(player.userID);
                    CuiHelper.DestroyUi(player, "WH40K.Quest");
                    break;
                case "accept":
                    string qid = arg.GetString(1);
                    if (!string.IsNullOrEmpty(qid))
                    {
                        AcceptQuest(player, qid);
                        OpenQuestUI(player); // refresh panel
                    }
                    break;
            }
        }

        void AcceptQuest(BasePlayer player, string questId)
        {
            var data = GetOrCreate(player.userID);
            if (data.CompletedQuests.Contains(questId))
            { SendReply(player, "<color=#FFD700>[WH40K]</color> Already completed."); return; }
            if (data.ActiveQuests.ContainsKey(questId))
            { SendReply(player, "<color=#FFD700>[WH40K]</color> Already active."); return; }
            if (!_cfg.Quests.TryGetValue(questId, out var quest))
            { SendReply(player, "<color=#FFD700>[WH40K]</color> Unknown quest ID."); return; }

            data.ActiveQuests[questId] = new QuestProgress();
            SaveAllData();
            SendReply(player,
                $"<color=#FFD700>[WH40K]</color> <color=#4CAF50>Quest accepted: {quest.DisplayName}</color>\n" +
                $"<color=#AAAAAA>{quest.Description}</color>");
        }

        // ─── Player Connections ───────────────────────────────────────────────

        void OnPlayerConnected(BasePlayer player)
        {
            bool isNew = !_playerData.ContainsKey(player.userID);
            if (isNew) _playerData[player.userID] = new PlayerData();

            timer.Once(4f, () =>
            {
                if (player == null || !player.IsConnected) return;
                if (isNew)
                {
                    SendReply(player,
                        "\n<color=#FFD700>╔══ IMPERIAL DEPLOYMENT ORDERS ═════════════════════╗</color>" +
                        "\n<color=#FFD700>║</color> <color=white>Soldier. You are assigned to Firebase Tertius.</color>" +
                        "\n<color=#FFD700>║</color> <color=#AAAAAA>The xenos threaten our perimeter on all fronts.</color>" +
                        "\n<color=#FFD700>║</color> <color=#AAAAAA>Report to Commissar Dorn for your first orders.</color>" +
                        "\n<color=#FFD700>║</color>" +
                        "\n<color=#FFD700>║</color>  <color=white>/wh40k quests</color>  <color=#555555>—</color> <color=#AAAAAA>mission briefings</color>" +
                        "\n<color=#FFD700>║</color>  <color=white>/shop</color>          <color=#555555>—</color> <color=#AAAAAA>Imperial supply depot</color>" +
                        "\n<color=#FFD700>║</color>  <color=white>/wh40k where</color>  <color=#555555>—</color> <color=#AAAAAA>identify your current zone</color>" +
                        "\n<color=#FFD700>╚════════════════════════════════════════════════════╝</color>");
                }
                else
                {
                    SendReply(player,
                        "<color=#FFD700>[WH40K]</color> <color=#AAAAAA>Welcome back, soldier. For the Emperor.</color>  " +
                        "<color=white>/wh40k quests</color> to check your missions.");
                }
            });
        }

        // ─── Console / RCON Commands (Admin) ─────────────────────────────────

        [ConsoleCommand("wh40k.event")]
        void CmdEvent(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin && !arg.IsServerside) return;
            var id = arg.GetString(0);
            if (string.IsNullOrEmpty(id)) { arg.ReplyWith("Usage: wh40k.event <eventId>"); return; }
            if (!_cfg.Events.ContainsKey(id))
            { arg.ReplyWith($"Unknown event: {id}\nAvailable: {string.Join(", ", _cfg.Events.Keys)}"); return; }
            StartEvent(id);
            arg.ReplyWith($"Event '{id}' started.");
        }

        [ConsoleCommand("wh40k.spawn")]
        void CmdSpawn(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin && !arg.IsServerside) return;
            var archetypeId = arg.GetString(0);
            var player = arg.Player();
            if (string.IsNullOrEmpty(archetypeId) || player == null)
            { arg.ReplyWith("Usage: wh40k.spawn <archetypeId>  (run in-game)"); return; }

            var spawnPos = player.transform.position + player.eyes.BodyForward() * 4f;
            var netId = SpawnNpc(archetypeId, spawnPos);
            arg.ReplyWith(netId > 0
                ? $"Spawned '{archetypeId}' (netId {netId})"
                : $"Failed. Available: {string.Join(", ", _cfg.NpcArchetypes.Keys)}");
        }

        [ConsoleCommand("wh40k.zones")]
        void CmdZones(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin && !arg.IsServerside) return;
            var sb = new StringBuilder($"WH40K Zones ({_cfg.Zones.Count} total):\n");
            foreach (var kvp in _cfg.Zones.OrderBy(k => k.Value.Faction))
            {
                var z = kvp.Value;
                sb.AppendLine($"  [{z.Faction.ToUpper()}] {kvp.Key}: {z.DisplayName} " +
                              $"r={z.Radius} @ ({z.Center[0]:F0},{z.Center[1]:F0},{z.Center[2]:F0})" +
                              (z.IsSafeZone ? " [safe]" : ""));
            }
            arg.ReplyWith(sb.ToString());
        }

        [ConsoleCommand("wh40k.monuments")]
        void CmdMonuments(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin && !arg.IsServerside) return;
            var monuments = TerrainMeta.Path?.Monuments;
            if (monuments == null) { arg.ReplyWith("No monuments found."); return; }

            var sb = new StringBuilder($"All monuments ({monuments.Count}):\n");
            foreach (var m in monuments)
            {
                if (m == null) continue;
                string name = (m.name ?? "").ToLower().Replace("(clone)", "").Trim();
                string faction = GetMonumentFaction(name) ?? "(unassigned)";
                sb.AppendLine($"  [{faction}] {name}  @ ({m.transform.position.x:F0},{m.transform.position.y:F0},{m.transform.position.z:F0})");
            }
            arg.ReplyWith(sb.ToString());
        }

        [ConsoleCommand("wh40k.events")]
        void CmdEvents(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin && !arg.IsServerside) return;
            var sb = new StringBuilder("WH40K Events:\n");
            foreach (var kvp in _cfg.Events)
            {
                bool active = _activeEvents.ContainsKey(kvp.Key);
                sb.AppendLine($"  {kvp.Key}: {kvp.Value.DisplayName} [{(active ? "ACTIVE" : "scheduled")}] " +
                              $"waves={kvp.Value.WaveCount}x{kvp.Value.NpcsPerWave}");
            }
            arg.ReplyWith(sb.ToString());
        }

        [ConsoleCommand("wh40k.resetzones")]
        void CmdResetZones(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin && !arg.IsServerside) return;
            AutoDiscoverMonumentZones();
            int imp = _cfg.Zones.Values.Count(z => z.Faction == "imperium");
            int ork = _cfg.Zones.Values.Count(z => z.Faction == "orks");
            int tyr = _cfg.Zones.Values.Count(z => z.Faction == "tyranids");
            arg.ReplyWith($"Zones re-discovered: {_cfg.Zones.Count} total ({imp} imperial, {ork} ork, {tyr} tyranid)");
            timer.Once(2f, PopulateAllZones);
        }

        [ConsoleCommand("wh40k.populate")]
        void CmdPopulate(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin && !arg.IsServerside) return;
            var zoneId = arg.GetString(0);
            if (!string.IsNullOrEmpty(zoneId))
            {
                if (!_cfg.Zones.TryGetValue(zoneId, out var z))
                { arg.ReplyWith($"Unknown zone: {zoneId}"); return; }
                PopulateZone(zoneId, z);
                arg.ReplyWith($"Populated zone '{zoneId}'.");
            }
            else
            {
                PopulateAllZones();
                arg.ReplyWith("All zones populated.");
            }
        }

        // ─── Map Markers ──────────────────────────────────────────────────────

        void PlaceMapMarkers()
        {
            RemoveMapMarkers();
            float worldSize = TerrainMeta.Size.x;
            if (worldSize <= 0) worldSize = 3000f;

            foreach (var kvp in _cfg.Zones)
            {
                var zone = kvp.Value;
                if (!_cfg.Factions.TryGetValue(zone.Faction, out var factionDef)) continue;

                var pos = new Vector3(zone.Center[0], zone.Center[1] + 1f, zone.Center[2]);
                var marker = GameManager.server.CreateEntity(
                    "assets/prefabs/tools/map/genericradiusmarker.prefab", pos
                ) as MapMarkerGenericRadius;
                if (marker == null) continue;

                marker.alpha   = 0.35f;
                marker.color1  = ParseHexColor(factionDef.Color);
                marker.radius  = Mathf.Clamp(zone.Radius * 2f / worldSize, 0.01f, 0.3f);
                marker.name    = zone.DisplayName;
                marker.Spawn();
                marker.SendUpdate();
                _mapMarkers.Add(marker);
            }
            Puts($"[WH40K] Placed {_mapMarkers.Count} map markers");
        }

        void RemoveMapMarkers()
        {
            foreach (var m in _mapMarkers)
                if (m != null && !m.IsDestroyed) m.Kill();
            _mapMarkers.Clear();
        }

        Color ParseHexColor(string hex)
        {
            hex = hex?.TrimStart('#') ?? "FFFFFF";
            if (hex.Length < 6) return Color.white;
            float r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
            float g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
            float b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
            return new Color(r, g, b);
        }

        // ─── ZoneManager Integration (optional) ───────────────────────────────

        void SetupZoneManagerZones()
        {
            if (ZoneManager == null) return;
            foreach (var kvp in _cfg.Zones)
            {
                var zone = kvp.Value;
                if (zone.Faction != "imperium") continue;  // only imperial zones are safe
                var center = new Vector3(zone.Center[0], zone.Center[1], zone.Center[2]);
                ZoneManager.Call("CreateOrUpdateZone", "wh40k_" + kvp.Key, new string[]
                {
                    "name",          zone.DisplayName,
                    "radius",        zone.Radius.ToString(),
                    "nodamage",      "true",
                    "enter_message", $"You have entered {zone.DisplayName}. Safe zone active.",
                    "leave_message", $"Leaving {zone.DisplayName}. Stay alert, soldier."
                }, center);
            }
            int count = _cfg.Zones.Values.Count(z => z.Faction == "imperium");
            Puts($"[WH40K] ZoneManager: registered {count} imperial safe zones");
        }

        void RemoveZoneManagerZones()
        {
            if (ZoneManager == null) return;
            foreach (var kvp in _cfg.Zones)
                if (kvp.Value.Faction == "imperium")
                    ZoneManager.Call("EraseZone", "wh40k_" + kvp.Key);
        }

        // ─── Death Flavor ─────────────────────────────────────────────────────

        string PickDeathFlavor(string faction)
        {
            if (!_cfg.DeathFlavor.TryGetValue(faction, out var lines) || lines.Count == 0) return null;
            return lines[UnityEngine.Random.Range(0, lines.Count)];
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        void Broadcast(string msg) => Server.Broadcast(msg);

        PlayerData GetOrCreate(ulong uid)
        {
            if (!_playerData.TryGetValue(uid, out var d))
                _playerData[uid] = d = new PlayerData();
            return d;
        }

        void Tally(Dictionary<string, int> dict, string key, int amount = 1)
        {
            if (!dict.ContainsKey(key)) dict[key] = 0;
            dict[key] += amount;
        }

        void SaveAllData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("WH40K/players",  _playerData);
            Interface.Oxide.DataFileSystem.WriteObject("WH40K/recruits", _playerRecruits);
        }

        // ─── OnNpcTarget / Damage hooks ──────────────────────────────────────

        // Prevent support NPCs from targeting the player — they fight orks only.
        object OnNpcTarget(NPCPlayer npc, BaseEntity target)
        {
            if (_supportNpcIds.Contains(npc?.net?.ID.Value ?? 0) && target is BasePlayer)
                return true;
            return null;
        }

        // Apply player buff multipliers (damage boost / damage resistance).
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;

            // Player taking damage — apply resistance
            if (entity is BasePlayer victim && !(entity is NPCPlayer))
            {
                if (_playerResistMultiplier.TryGetValue(victim.userID, out float resist) && resist > 0f)
                    info.damageTypes.ScaleAll(1f - resist);
            }

            // Player dealing damage — apply damage boost
            var attacker = info.InitiatorPlayer;
            if (attacker != null && !(attacker is NPCPlayer))
            {
                if (_playerDamageMultiplier.TryGetValue(attacker.userID, out float boost) && boost > 0f)
                    info.damageTypes.ScaleAll(1f + boost);
            }
        }

        // ─── Hub Builder ─────────────────────────────────────────────────────

        ulong PlaceBlock(string prefab, Vector3 pos, Quaternion rot, BuildingGrade.Enum grade, uint buildingId)
        {
            var entity = GameManager.server.CreateEntity(prefab, pos, rot);
            if (entity == null) { Puts($"[WH40K] PlaceBlock null: {prefab}"); return 0; }
            var block = entity as BuildingBlock;
            if (block != null)
            {
                block.grade = grade;
                block.grounded = true;
                block.buildingID = buildingId;
                block.Spawn();
                block.SetHealthToMax();
            }
            else
            {
                entity.Spawn();
            }
            var netId = entity.net?.ID.Value ?? 0;
            if (netId > 0) _hubEntities.Add(netId);
            return netId;
        }

        ulong PlaceDeployable(string prefab, Vector3 pos, Quaternion rot)
        {
            var entity = GameManager.server.CreateEntity(prefab, pos, rot);
            if (entity == null) { Puts($"[WH40K] PlaceDeployable null: {prefab}"); return 0; }
            entity.Spawn();
            var netId = entity.net?.ID.Value ?? 0;
            if (netId > 0) _hubEntities.Add(netId);
            return netId;
        }

        void BuildFirebaseTertius(Vector3 center)
        {
            float baseY = TerrainMeta.HeightMap?.GetHeight(center) ?? center.y;
            center.y = baseY;

            uint bid = BuildingManager.server.NewBuildingID();
            var g = BuildingGrade.Enum.Metal;
            int placed = 0;

            // 4×4 foundation grid, centered at `center`
            // xo = zo = (4-1)*3/2 = 4.5
            float xo = 4.5f, zo = 4.5f;
            for (int c = 0; c < 4; c++)
            {
                for (int r = 0; r < 4; r++)
                {
                    float x = c * 3 - xo;
                    float z = r * 3 - zo;
                    PlaceBlock("assets/prefabs/building core/foundation/foundation.prefab",
                        center + new Vector3(x, 0, z), Quaternion.identity, g, bid);
                    placed++;
                }
            }

            // Perimeter walls + doorway (south face, col 1)
            // South face z = -zo - 1.5 = -6.0
            float sz = -zo - 1.5f, nz = zo + 1.5f, wx = -xo - 1.5f, ex = xo + 1.5f;
            for (int c = 0; c < 4; c++)
            {
                float x = c * 3 - xo;
                string wallPrefab = (c == 1)
                    ? "assets/prefabs/building core/wall.doorway/wall.doorway.prefab"
                    : "assets/prefabs/building core/wall/wall.prefab";
                PlaceBlock(wallPrefab, center + new Vector3(x, 1.5f, sz), Quaternion.Euler(0, 180, 0), g, bid);
                placed++;
                PlaceBlock("assets/prefabs/building core/wall/wall.prefab",
                    center + new Vector3(x, 1.5f, nz), Quaternion.Euler(0, 0, 0), g, bid);
                placed++;
            }
            for (int r = 0; r < 4; r++)
            {
                float z = r * 3 - zo;
                PlaceBlock("assets/prefabs/building core/wall/wall.prefab",
                    center + new Vector3(wx, 1.5f, z), Quaternion.Euler(0, 270, 0), g, bid);
                PlaceBlock("assets/prefabs/building core/wall/wall.prefab",
                    center + new Vector3(ex, 1.5f, z), Quaternion.Euler(0, 90, 0), g, bid);
                placed += 2;
            }

            // 2nd-floor watchtower platforms: NW (col 0, row 3) and NE (col 3, row 3)
            PlaceBlock("assets/prefabs/building core/floor/floor.prefab",
                center + new Vector3(-xo, 3.0f, zo), Quaternion.identity, g, bid);
            PlaceBlock("assets/prefabs/building core/floor/floor.prefab",
                center + new Vector3(xo, 3.0f, zo), Quaternion.identity, g, bid);
            placed += 2;

            // Watchtower parapet walls (N + outer faces of each platform)
            // NW tower: north + west
            PlaceBlock("assets/prefabs/building core/wall/wall.prefab",
                center + new Vector3(-xo, 4.5f, nz), Quaternion.Euler(0, 0, 0), g, bid);
            PlaceBlock("assets/prefabs/building core/wall/wall.prefab",
                center + new Vector3(wx, 4.5f, zo), Quaternion.Euler(0, 270, 0), g, bid);
            // NE tower
            PlaceBlock("assets/prefabs/building core/wall/wall.prefab",
                center + new Vector3(xo, 4.5f, nz), Quaternion.Euler(0, 0, 0), g, bid);
            PlaceBlock("assets/prefabs/building core/wall/wall.prefab",
                center + new Vector3(ex, 4.5f, zo), Quaternion.Euler(0, 90, 0), g, bid);
            placed += 4;

            // Interior deployables
            // Tool cupboard: NW interior corner
            PlaceDeployable("assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab",
                center + new Vector3(-xo + 0.5f, baseY + 0.3f, zo - 0.5f), Quaternion.identity);
            // 2 sleeping bags
            PlaceDeployable("assets/prefabs/deployable/sleeping bag/sleepingbag_leather_deployed.prefab",
                center + new Vector3(-xo + 0.5f, baseY + 0.3f, zo - 1.5f), Quaternion.Euler(0, 180, 0));
            PlaceDeployable("assets/prefabs/deployable/sleeping bag/sleepingbag_leather_deployed.prefab",
                center + new Vector3(-xo + 1.5f, baseY + 0.3f, zo - 0.5f), Quaternion.Euler(0, 90, 0));
            // Workbench T1 prefab not available server-side — skipped
            placed += 4;

            Puts($"[WH40K] Firebase Tertius built: {placed} blocks at ({center.x:F0},{center.y:F0},{center.z:F0})");
        }

        [ConsoleCommand("wh40k.buildhub")]
        void CmdBuildHub(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin && !arg.IsServerside) return;
            // Kill existing hub first
            foreach (var netId in _hubEntities)
            {
                var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as BaseEntity;
                entity?.Kill();
            }
            _hubEntities.Clear();

            var zone = NearestImperialZoneToAnyPlayer();
            if (zone == null) { arg.ReplyWith("[WH40K] No imperial zone found to place hub."); return; }

            var center = new Vector3(zone.Center[0], zone.Center[1], zone.Center[2]);
            // Offset slightly to avoid overlapping monument geometry
            center += new Vector3(50f, 0, 50f);
            BuildFirebaseTertius(center);
            arg.ReplyWith($"[WH40K] Firebase Tertius placed near '{zone.DisplayName}'");
        }

        [ConsoleCommand("wh40k.clearhub")]
        void CmdClearHub(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin && !arg.IsServerside) return;
            int killed = 0;
            foreach (var netId in _hubEntities)
            {
                var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as BaseEntity;
                if (entity != null) { entity.Kill(); killed++; }
            }
            _hubEntities.Clear();
            arg.ReplyWith($"[WH40K] Cleared {killed} hub entities.");
        }

        // ─── Support System ───────────────────────────────────────────────────

        List<RecruitedUnit> GetRecruits(ulong uid)
        {
            if (!_playerRecruits.TryGetValue(uid, out var list))
                _playerRecruits[uid] = list = new List<RecruitedUnit>();
            return list;
        }

        int TotalRecruits(ulong uid) => GetRecruits(uid).Sum(u => u.Count);

        int RecruitCapForDifficulty()
        {
            string diff = _cfg.ActiveAssaultDifficulty?.ToLower() ?? "soldier";
            _cfg.DifficultyRecruitCaps.TryGetValue(diff, out int cap);
            return cap;
        }

        void DoRecruit(BasePlayer player, string unitType)
        {
            if (!_cfg.RecruitCosts.TryGetValue(unitType, out int cost))
            {
                SendReply(player, $"<color=#FFD700>[WH40K]</color> Unknown unit type '{unitType}'. Valid: guardsman, sergeant, commissar");
                return;
            }

            int cap = RecruitCapForDifficulty();
            if (cap == 0)
            {
                SendReply(player, $"<color=#FFD700>[WH40K]</color> Difficulty '{_cfg.ActiveAssaultDifficulty}' allows no support — solo mission.");
                return;
            }

            int current = TotalRecruits(player.userID);
            if (current >= cap)
            {
                SendReply(player, $"<color=#FFD700>[WH40K]</color> Recruit cap reached ({current}/{cap}). Dismiss units first.");
                return;
            }

            double balance = Economics != null ? (double)Economics.Call("Balance", player.UserIDString) : 0;
            if (balance < cost)
            {
                SendReply(player, $"<color=#FFD700>[WH40K]</color> Insufficient Thrones ({(int)balance}/{cost}).");
                return;
            }

            Economics?.Call("Withdraw", player.UserIDString, (double)cost);

            var roster = GetRecruits(player.userID);
            var existing = roster.FirstOrDefault(u => u.UnitType == unitType);
            if (existing != null) existing.Count++;
            else roster.Add(new RecruitedUnit { UnitType = unitType, Count = 1 });

            SaveAllData();
            SendReply(player,
                $"<color=#FFD700>[WH40K]</color> <color=#4CAF50>{unitType} recruited.</color> " +
                $"Roster: {TotalRecruits(player.userID)}/{cap}. Cost: {cost}T");
        }

        void ShowRecruitList(BasePlayer player)
        {
            int cap = RecruitCapForDifficulty();
            var roster = GetRecruits(player.userID);
            double balance = Economics != null ? (double)Economics.Call("Balance", player.UserIDString) : 0;
            var buff = _playerBuffs.TryGetValue(player.userID, out var b) ? b : null;

            var sb = new StringBuilder();
            sb.AppendLine("<color=#FFD700>╔══ IMPERIAL SUPPORT ROSTER ═══════════════╗</color>");
            sb.AppendLine($"<color=#FFD700>║</color> Difficulty: <color=white>{_cfg.ActiveAssaultDifficulty.ToUpper()}</color>  Cap: {TotalRecruits(player.userID)}/{cap}  Thrones: <color=#4CAF50>{(int)balance}T</color>");
            sb.AppendLine("<color=#FFD700>║</color> <color=#AAAAAA>Units:</color>");
            if (roster.Count == 0) sb.AppendLine("<color=#FFD700>║</color>   <color=#555555>None recruited.</color>");
            foreach (var u in roster)
            {
                int unitCost; _cfg.RecruitCosts.TryGetValue(u.UnitType, out unitCost);
                sb.AppendLine($"<color=#FFD700>║</color>   <color=white>{u.UnitType}</color> ×{u.Count}  <color=#4CAF50>({unitCost}T ea)</color>");
            }
            sb.AppendLine("<color=#FFD700>║</color> <color=#AAAAAA>Buffs purchased:</color>");
            if (buff == null || (!buff.EmperorBlessing && !buff.IronFaith && !buff.Stimm && !buff.OrbitalScan && !buff.Artillery && !buff.VoxScramble))
                sb.AppendLine("<color=#FFD700>║</color>   <color=#555555>None.</color>");
            else
            {
                if (buff.EmperorBlessing) sb.AppendLine("<color=#FFD700>║</color>   <color=#FFD700>Emperor's Blessing</color> (+25% dmg)");
                if (buff.IronFaith)       sb.AppendLine("<color=#FFD700>║</color>   <color=#87CEEB>Iron Faith</color> (+25% resist)");
                if (buff.Stimm)           sb.AppendLine("<color=#FFD700>║</color>   <color=#4CAF50>Stimm Pack</color> (+speed)");
                if (buff.OrbitalScan)     sb.AppendLine("<color=#FFD700>║</color>   <color=#FFA500>Orbital Scan</color> (reveal orks)");
                if (buff.Artillery)       sb.AppendLine("<color=#FFD700>║</color>   <color=#FF4444>Artillery Strike</color> (-30% ork HP)");
                if (buff.VoxScramble)     sb.AppendLine("<color=#FFD700>║</color>   <color=#9C27B0>Vox Scramble</color> (-25% ork HP)");
            }
            sb.AppendLine("<color=#FFD700>║</color> <color=#AAAAAA>/wh40k recruit guardsman|sergeant|commissar</color>");
            sb.AppendLine("<color=#FFD700>║</color> <color=#AAAAAA>/wh40k buy emperor_blessing|iron_faith|stimm|medicae</color>");
            sb.AppendLine("<color=#FFD700>║</color> <color=#AAAAAA>/wh40k buy orbital_scan|artillery|vox_scramble</color>");
            sb.AppendLine("<color=#FFD700>║</color> <color=#AAAAAA>/wh40k assault start — deploy to nearest ork zone</color>");
            sb.AppendLine("<color=#FFD700>╚═══════════════════════════════════════════╝</color>");
            SendReply(player, sb.ToString());
        }

        void DoBuy(BasePlayer player, string item)
        {
            if (!_cfg.BuyCosts.TryGetValue(item, out int cost))
            {
                SendReply(player, $"<color=#FFD700>[WH40K]</color> Unknown item '{item}'.");
                return;
            }

            double balance = Economics != null ? (double)Economics.Call("Balance", player.UserIDString) : 0;
            if (balance < cost)
            {
                SendReply(player, $"<color=#FFD700>[WH40K]</color> Insufficient Thrones ({(int)balance}/{cost}T).");
                return;
            }

            if (!_playerBuffs.ContainsKey(player.userID))
                _playerBuffs[player.userID] = new AssaultBuff();
            var buff = _playerBuffs[player.userID];

            // Instant-use items
            if (item == "medicae")
            {
                Economics?.Call("Withdraw", player.UserIDString, (double)cost);
                player.SetHealth(player.MaxHealth());
                SendReply(player, "<color=#FFD700>[WH40K]</color> <color=#4CAF50>Medicae applied — health restored.</color>");
                return;
            }
            if (item == "stimm")
            {
                Economics?.Call("Withdraw", player.UserIDString, (double)cost);
                buff.Stimm = true;
                // Max calories + hydration for the stamina boost effect
                player.metabolism.calories.SetValue(player.metabolism.calories.max);
                player.metabolism.hydration.SetValue(player.metabolism.hydration.max);
                SendReply(player, "<color=#FFD700>[WH40K]</color> <color=#4CAF50>Stimm Pack administered. Stamina maxed.</color>");
                return;
            }

            // Latched buffs (applied at assault start)
            if (item == "emperor_blessing") buff.EmperorBlessing = true;
            else if (item == "iron_faith")  buff.IronFaith       = true;
            else if (item == "orbital_scan") buff.OrbitalScan    = true;
            else if (item == "artillery")   buff.Artillery       = true;
            else if (item == "vox_scramble") buff.VoxScramble    = true;

            Economics?.Call("Withdraw", player.UserIDString, (double)cost);
            SendReply(player, $"<color=#FFD700>[WH40K]</color> <color=#4CAF50>{item.Replace('_', ' ')} purchased.</color> Active at next assault start.");
        }

        void StartAssault(BasePlayer player)
        {
            // Find nearest ork zone to player
            var orkZones = _cfg.Zones.Where(kvp => kvp.Value.Faction == "orks").ToList();
            if (orkZones.Count == 0) { SendReply(player, "<color=#FFD700>[WH40K]</color> No ork zones found."); return; }

            KeyValuePair<string, ZoneDef> nearest = default;
            float nearestDist = float.MaxValue;
            foreach (var kvp in orkZones)
            {
                var c = new Vector3(kvp.Value.Center[0], kvp.Value.Center[1], kvp.Value.Center[2]);
                float d = Vector3.Distance(player.transform.position, c);
                if (d < nearestDist) { nearestDist = d; nearest = kvp; }
            }

            _playerAssaultZone[player.userID] = nearest.Key;
            Puts($"[WH40K] Assault started: {player.displayName} → {nearest.Value.DisplayName} ({nearestDist:F0}m)");

            // Apply latched buffs
            var buff = _playerBuffs.TryGetValue(player.userID, out var b) ? b : null;
            if (buff != null)
            {
                if (buff.EmperorBlessing)
                {
                    _playerDamageMultiplier[player.userID] = 0.25f;
                    // expire after 10 min
                    _playerBuffTimers[player.userID] = timer.Once(600f, () => _playerDamageMultiplier.Remove(player.userID));
                    SendReply(player, "<color=#FFD700>[WH40K]</color> <color=#FFD700>Emperor's Blessing active — +25% damage for 10 min.</color>");
                }
                if (buff.IronFaith)
                {
                    _playerResistMultiplier[player.userID] = 0.25f;
                    timer.Once(600f, () => _playerResistMultiplier.Remove(player.userID));
                    SendReply(player, "<color=#FFD700>[WH40K]</color> <color=#87CEEB>Iron Faith active — +25% damage resistance for 10 min.</color>");
                }
                if (buff.OrbitalScan)
                {
                    OrbitalScan(player, nearest.Value);
                }
                if (buff.Artillery)
                {
                    ArtilleryStrike(player, nearest.Value, 0.30f);
                    SendReply(player, "<color=#FFD700>[WH40K]</color> <color=#FF4444>Artillery strike inbound — 30% damage to all orks in zone.</color>");
                }
                if (buff.VoxScramble)
                {
                    VoxScramble(nearest.Value, 0.25f);
                    SendReply(player, "<color=#FFD700>[WH40K]</color> <color=#9C27B0>Vox scramble activated — ork HP reduced 25%.</color>");
                }
                _playerBuffs.Remove(player.userID);
            }

            // Schedule support waves
            string diff = _cfg.ActiveAssaultDifficulty?.ToLower() ?? "soldier";
            if (!_cfg.AssaultWaveSchedules.TryGetValue(diff, out var schedule) || schedule.Count == 0)
            {
                SendReply(player, "<color=#FFD700>[WH40K]</color> No support waves for difficulty '" + diff + "'. Assault is solo.");
                return;
            }

            var roster = GetRecruits(player.userID);
            if (roster.Count == 0)
            {
                SendReply(player, "<color=#FFD700>[WH40K]</color> No units recruited. Assault launched solo.");
                return;
            }

            Broadcast($"<color=#FFD700>[WH40K]</color> <color=#4CAF50>Imperial assault inbound on {nearest.Value.DisplayName}. " +
                      $"{TotalRecruits(player.userID)} support units deploying in waves.</color>");

            int waveIdx = 0;
            foreach (float delay in schedule)
            {
                int wi = waveIdx++;
                timer.Once(delay, () =>
                {
                    if (!_playerAssaultZone.ContainsKey(player.userID)) return;
                    if (!_cfg.Zones.TryGetValue(nearest.Key, out var zone)) return;
                    SpawnSupportWave(player, zone);
                });
            }
        }

        void SpawnSupportWave(BasePlayer player, ZoneDef zone)
        {
            var roster = GetRecruits(player.userID);
            if (roster.Count == 0) return;

            int spawned = 0;
            foreach (var unit in roster)
            {
                for (int i = 0; i < unit.Count; i++)
                {
                    if (!_cfg.SupportArchetypePrefabs.TryGetValue(unit.UnitType, out var prefab)) continue;
                    float hp = _cfg.SupportArchetypeHP.TryGetValue(unit.UnitType, out float h) ? h : 150f;

                    var pos = RandomInZone(zone);
                    var entity = GameManager.server.CreateEntity(prefab, pos, Quaternion.identity);
                    if (entity == null) continue;
                    entity.Spawn();
                    if (entity is BaseCombatEntity bce)
                        bce.InitializeHealth(hp, hp);

                    var netId = entity.net?.ID.Value ?? 0;
                    if (netId > 0)
                    {
                        _supportNpcIds.Add(netId);
                        if (!_supportEntities.ContainsKey(player.userID))
                            _supportEntities[player.userID] = new List<ulong>();
                        _supportEntities[player.userID].Add(netId);
                    }
                    spawned++;
                }
            }
            SendReply(player, $"<color=#FFD700>[WH40K]</color> <color=#4CAF50>{spawned} support troops deployed to {zone.DisplayName}.</color>");
        }

        void RecallSupport(ulong playerId)
        {
            _playerAssaultZone.Remove(playerId);
            if (!_supportEntities.TryGetValue(playerId, out var list)) return;
            int killed = 0;
            foreach (var netId in list)
            {
                _supportNpcIds.Remove(netId);
                var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as BaseEntity;
                if (entity != null) { entity.Kill(); killed++; }
            }
            _supportEntities.Remove(playerId);
            Puts($"[WH40K] Support recalled for {playerId}: {killed} units despawned");
        }

        void OrbitalScan(BasePlayer player, ZoneDef zone)
        {
            var center = new Vector3(zone.Center[0], zone.Center[1], zone.Center[2]);
            float worldSize = TerrainMeta.Size.x;
            if (worldSize <= 0) worldSize = 3000f;
            int marked = 0;
            foreach (var ent in BaseNetworkable.serverEntities.OfType<NPCPlayer>())
            {
                if (_supportNpcIds.Contains(ent.net?.ID.Value ?? 0)) continue;
                if (Vector3.Distance(ent.transform.position, center) > zone.Radius) continue;
                var marker = GameManager.server.CreateEntity(
                    "assets/prefabs/tools/map/genericradiusmarker.prefab",
                    ent.transform.position) as MapMarkerGenericRadius;
                if (marker == null) continue;
                marker.alpha  = 0.7f;
                marker.color1 = new Color(1f, 0.2f, 0.2f);
                marker.radius = 0.01f;
                marker.Spawn(); marker.SendUpdate();
                _mapMarkers.Add(marker);
                // auto-remove after 5 min
                ulong markNetId = marker.net?.ID.Value ?? 0;
                timer.Once(300f, () =>
                {
                    var m = BaseNetworkable.serverEntities.Find(new NetworkableId(markNetId)) as MapMarkerGenericRadius;
                    if (m != null) { _mapMarkers.Remove(m); m.Kill(); }
                });
                marked++;
            }
            SendReply(player, $"<color=#FFD700>[WH40K]</color> <color=#FFA500>Orbital scan: {marked} ork contacts marked for 5 minutes.</color>");
        }

        void ArtilleryStrike(BasePlayer player, ZoneDef zone, float damageFraction)
        {
            var center = new Vector3(zone.Center[0], zone.Center[1], zone.Center[2]);
            int hit = 0;
            foreach (var ent in BaseNetworkable.serverEntities.OfType<NPCPlayer>().ToList())
            {
                if (_supportNpcIds.Contains(ent.net?.ID.Value ?? 0)) continue;
                if (Vector3.Distance(ent.transform.position, center) > zone.Radius) continue;
                float dmg = ent.MaxHealth() * damageFraction;
                ent.Hurt(dmg, Rust.DamageType.Explosion, null);
                hit++;
            }
            SendReply(player, $"<color=#FFD700>[WH40K]</color> <color=#FF4444>Artillery: {hit} orks hit ({(int)(damageFraction * 100)}% HP damage).</color>");
        }

        void VoxScramble(ZoneDef zone, float hpReductionFraction)
        {
            var center = new Vector3(zone.Center[0], zone.Center[1], zone.Center[2]);
            foreach (var ent in BaseNetworkable.serverEntities.OfType<NPCPlayer>().ToList())
            {
                if (_supportNpcIds.Contains(ent.net?.ID.Value ?? 0)) continue;
                if (Vector3.Distance(ent.transform.position, center) > zone.Radius) continue;
                float newMax = ent.MaxHealth() * (1f - hpReductionFraction);
                if (newMax < 10f) newMax = 10f;
                float newHp  = Mathf.Min(ent.Health(), newMax);
                ent.InitializeHealth(newHp, newMax);
            }
        }

        // ─── Default Config ───────────────────────────────────────────────────

        PluginConfig BuildDefaultConfig() => new PluginConfig
        {
            AutoDiscoverZones     = true,
            BaseKillEconomics     = 30,
            BaseKillServerRewards = 5,

            Factions = new Dictionary<string, FactionDef>
            {
                ["imperium"] = new FactionDef { DisplayName = "Imperium of Man",  Color = "#FFD700", HostileToPlayer = false },
                ["orks"]     = new FactionDef { DisplayName = "Ork Warband",      Color = "#4CAF50", HostileToPlayer = true  },
                ["tyranids"] = new FactionDef { DisplayName = "Tyranid Swarm",    Color = "#9C27B0", HostileToPlayer = true  }
            },

            NpcArchetypes = new Dictionary<string, NpcArchetype>
            {
                ["ork_boy"] = new NpcArchetype
                {
                    DisplayName = "Ork Boy", Faction = "orks",
                    Prefab = "assets/prefabs/npc/murderer/murderer.prefab",
                    HealthMultiplier = 1.2f, KillEconomics = 50, KillServerRewards = 10, KillXp = 25
                },
                ["ork_nob"] = new NpcArchetype
                {
                    DisplayName = "Ork Nob", Faction = "orks",
                    Prefab = "assets/prefabs/npc/murderer/murderer.prefab",
                    HealthMultiplier = 2.5f, KillEconomics = 150, KillServerRewards = 30, KillXp = 75
                },
                ["warboss_grakh"] = new NpcArchetype
                {
                    DisplayName = "Warboss Grakh da Killa", Faction = "orks",
                    Prefab = "assets/prefabs/npc/heavyscientist/heavyscientist.prefab",
                    HealthMultiplier = 5.0f, KillEconomics = 500, KillServerRewards = 100, KillXp = 300
                },
                ["termagant"] = new NpcArchetype
                {
                    DisplayName = "Termagant", Faction = "tyranids",
                    Prefab = "assets/rust.ai/agents/wolf/wolf.prefab",
                    HealthMultiplier = 1.0f, KillEconomics = 30, KillServerRewards = 5, KillXp = 15
                },
                ["hormagaunt"] = new NpcArchetype
                {
                    DisplayName = "Hormagaunt", Faction = "tyranids",
                    Prefab = "assets/rust.ai/agents/boar/boar.prefab",
                    HealthMultiplier = 1.5f, KillEconomics = 60, KillServerRewards = 12, KillXp = 30
                },
                ["carnifex"] = new NpcArchetype
                {
                    DisplayName = "Carnifex", Faction = "tyranids",
                    Prefab = "assets/rust.ai/agents/bear/bear.prefab",
                    HealthMultiplier = 3.0f, KillEconomics = 350, KillServerRewards = 70, KillXp = 200
                }
            },

            DeathFlavor = new Dictionary<string, List<string>>
            {
                ["orks"] = new List<string>
                {
                    "Dat one wuz soft! WAAAGH!",
                    "Another skull for da pile!",
                    "WAAAGH claims another Imperial!",
                    "Weak humie. Grakh is disappoint.",
                    "Didn't even put up a good fight!"
                },
                ["tyranids"] = new List<string>
                {
                    "Consumed by the Swarm. Your genes now feed the Hive.",
                    "The Hivemind absorbs your biomass. You are one with the Swarm.",
                    "Devoured. The Hive Fleet grows stronger.",
                    "The Great Devourer takes what it needs.",
                    "Biomass acquired. The Swarm advances."
                }
            },

            // Zones are auto-populated at runtime by AutoDiscoverMonumentZones()
            Zones = new Dictionary<string, ZoneDef>(),

            FactionSpawn = new Dictionary<string, FactionSpawnConfig>
            {
                ["orks"] = new FactionSpawnConfig
                {
                    Archetypes    = new List<string> { "ork_boy", "ork_nob" },
                    MaxPerZone    = 4,
                    RespawnMinutes = 12f
                },
                ["tyranids"] = new FactionSpawnConfig
                {
                    Archetypes    = new List<string> { "termagant", "hormagaunt" },
                    MaxPerZone    = 5,
                    RespawnMinutes = 8f
                }
                // no "imperium" entry — imperial zones are safe zones, no ambient hostiles
            },

            Events = new Dictionary<string, EventDef>
            {
                ["tyranid_wave"] = new EventDef
                {
                    DisplayName = "Tyranid Biomorphic Swarm", Faction = "tyranids",
                    ZoneId = "",  // picks a random imperial zone each wave
                    WaveCount = 3, NpcsPerWave = 6, Archetype = "termagant",
                    WaveIntervalSeconds = 90f,
                    StartAnnouncement = "⚠ TYRANID SWARM DETECTED — Imperial sectors under attack! ALL HANDS DEFEND!",
                    EndAnnouncement   = "✓ Tyranid swarm repelled. The Imperium holds. Well done, soldier.",
                    MinIntervalMinutes = 45f, MaxIntervalMinutes = 90f
                },
                ["waaagh"] = new EventDef
                {
                    DisplayName = "WAAAGH! Ork Raid", Faction = "orks",
                    ZoneId = "",  // picks a random imperial zone each wave
                    WaveCount = 2, NpcsPerWave = 5, Archetype = "ork_boy",
                    WaveIntervalSeconds = 120f,
                    StartAnnouncement = "⚠ WAAAGH INCOMING — Ork warband raiding Imperial positions! HOLD THE LINE!",
                    EndAnnouncement   = "✓ WAAAGH repelled. For the Emperor! The line holds.",
                    MinIntervalMinutes = 60f, MaxIntervalMinutes = 120f
                }
            },

            Quests = new Dictionary<string, QuestDef>
            {
                ["q01_thin_the_herd"] = new QuestDef
                {
                    DisplayName = "Thin the Herd",
                    Description = "Commissar Dorn: 'The Tyranids multiply without end. Kill 10 before they overrun our flanks.'",
                    Objectives  = new List<QuestObjective>
                        { new QuestObjective { Type = "kill_faction", Target = "tyranids", Count = 10 } },
                    EconomicsReward = 300, ServerRewardsReward = 50, FactionRep = "imperium", FactionRepGain = 100
                },
                ["q02_green_tide"] = new QuestDef
                {
                    DisplayName = "Stem the Green Tide",
                    Description = "Sergeant Valdis: 'Ork Boys massing at the scrap fields. Kill 8 and reduce the pressure.'",
                    Objectives  = new List<QuestObjective>
                        { new QuestObjective { Type = "kill_faction", Target = "orks", Count = 8 } },
                    EconomicsReward = 400, ServerRewardsReward = 60, FactionRep = "imperium", FactionRepGain = 150
                },
                ["q03_decapitation_strike"] = new QuestDef
                {
                    DisplayName = "Decapitation Strike",
                    Description = "A Carnifex commands the second nest. Eliminate it. That thing doesn't fall easily.",
                    Objectives  = new List<QuestObjective>
                        { new QuestObjective { Type = "kill_npc", Target = "carnifex", Count = 1 } },
                    EconomicsReward = 800, ServerRewardsReward = 150, FactionRep = "imperium", FactionRepGain = 250
                },
                ["q04_warboss_hunt"] = new QuestDef
                {
                    DisplayName = "Hunt the Warboss",
                    Description = "Warboss Grakh da Killa leads the warband. Kill him and their cohesion collapses.",
                    Objectives  = new List<QuestObjective>
                        { new QuestObjective { Type = "kill_npc", Target = "warboss_grakh", Count = 1 } },
                    EconomicsReward = 1500, ServerRewardsReward = 300, FactionRep = "imperium", FactionRepGain = 500
                },
                ["q05_cleanse_and_burn"] = new QuestDef
                {
                    DisplayName = "Cleanse and Burn",
                    Description = "The Emperor demands both threats addressed. Kill 5 Orks and 5 Tyranids.",
                    Objectives  = new List<QuestObjective>
                    {
                        new QuestObjective { Type = "kill_faction", Target = "orks",     Count = 5 },
                        new QuestObjective { Type = "kill_faction", Target = "tyranids", Count = 5 }
                    },
                    EconomicsReward = 600, ServerRewardsReward = 100, FactionRep = "imperium", FactionRepGain = 200
                }
            }
        };
    }
}
