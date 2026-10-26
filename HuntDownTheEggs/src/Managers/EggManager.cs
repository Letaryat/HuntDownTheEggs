using System.Drawing;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using HuntDownTheEggs.Api;
using HuntDownTheEggs.Extensions;

namespace HuntDownTheEggs;

public sealed class EggManager(HuntDownTheEggsPlugin plugin)
{
    private readonly HuntDownTheEggsPlugin _plugin = plugin;
    private readonly Random _random = new();
    private static readonly object FileLock = new();

    private readonly List<EggData> _eggs = [];
    private readonly Dictionary<uint, EggInstance> _byProp = new();
    private readonly Dictionary<uint, EggInstance> _byTrigger = new();

    public bool PlacingMode { get; set; }

    // ---------- map files ----------

    private string MapFilePath()
    {
        var dir = Path.Combine(_plugin.ModuleDirectory, "maps");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{_plugin.MapName}.json");
    }

    public void LoadEggsFromMap()
    {
        if (!_plugin.Config.ModesSetup.SearchMode) return;
        var path = MapFilePath();
        if (!File.Exists(path)) { File.WriteAllText(path, "[]"); return; }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json)) return;
        _eggs.Clear();
        _eggs.AddRange(JsonSerializer.Deserialize<List<EggData>>(json) ?? []);
        _plugin.DebugLog($"Loaded {_eggs.Count} eggs from {_plugin.MapName}.json");
    }

    public void CheckIfEggsAreThere()
    {
        if (_eggs.Count > 0) return;
        try { LoadEggsFromMap(); }
        catch (Exception ex) { _plugin.DebugLog(ex.ToString()); }
    }

    private void SaveEggsFile() =>
        File.WriteAllText(MapFilePath(), JsonSerializer.Serialize(_eggs, new JsonSerializerOptions { WriteIndented = true }));

    public int EggCount => _eggs.Count;
    public EggData? GetEggById(int id) => id >= 0 && id < _eggs.Count ? _eggs[id] : null;

    public void ClearCache()
    {
        _eggs.Clear();
        _byProp.Clear();
        _byTrigger.Clear();
    }

    public void AddNewEgg(Vector pos, string color)
    {
        if (!_plugin.Config.ModesSetup.SearchMode) return;
        lock (FileLock)
        {
            _eggs.Add(new EggData { Id = _eggs.Count, X = pos.X, Y = pos.Y, Z = pos.Z, ModelColor = color });
            SaveEggsFile();
        }
    }

    public void RemoveEgg(int id)
    {
        var egg = _eggs.FirstOrDefault(e => e.Id == id);
        if (egg != null) _eggs.Remove(egg);
    }

    public void SaveIfRemovedEggs()
    {
        lock (FileLock)
        {
            for (var i = 0; i < _eggs.Count; i++) _eggs[i].Id = i;
            SaveEggsFile();
        }
    }

    // ---------- spawn ----------

    public void SpawnAllEggs()
    {
        var cfg = _plugin.Config.ModesSetup;

        if (!_plugin.AllowToSpawnEggs) return;

        if (_plugin.Helper.GetNumPlayers() < cfg.MinPlayersToSpawnEggs)
        {
            _plugin.DebugLog($"Not enough players to spawn eggs. Current: {_plugin.Helper.GetNumPlayers()}, Required: {cfg.MinPlayersToSpawnEggs}");
            return;
        }

        foreach (var egg in _eggs)
            SpawnEgg(new Vector(egg.X, egg.Y, egg.Z), EggKind.Placed, egg.ModelColor, egg.Id);

        if (!cfg.SpawnRandomEggs) return;

        if (!cfg.SpawnRandomEggsOnWarmup && _plugin.WarmupRunning)
        {
            _plugin.DebugLog("Warmup detected, not spawning random eggs on this round.");
            return;
        }

        var humans = Utilities.GetPlayers().Count(_plugin.Helper.IsRealPlayer);
        if (humans < cfg.MinPlayersToSpawnRandomEggs)
        {
            _plugin.DebugLog($"Not enough players to spawn random eggs. Current: {humans}, Required: {cfg.MinPlayersToSpawnRandomEggs}");
            return;
        }

        var toSpawn = cfg.NumberOfRandomEggs;
        if (cfg.SpawnRandomEggsByPercentageOfPlayers)
            toSpawn = (int)MathF.Ceiling(cfg.NumberOfRandomEggs * (_plugin.Helper.PlayerPercentage() / 100f));

        var spawned = 0;
        for (var i = 0; i < toSpawn; i++)
        {
            var pos = NavMesh.GetRandomPosition();
            if (pos == null)
            {
                _plugin.DebugLog($"No random point available (attempt {i}).");
                break;
            }
            if (SpawnEgg(pos, EggKind.Random, "default") != null) spawned++;
        }

        foreach (var p in Utilities.GetPlayers().Where(_plugin.Helper.IsRealPlayer))
            _plugin.Helper.Chat(p, "hdte.new_round_random_eggs", spawned);
    }

    public EggInstance? SpawnEgg(Vector position, EggKind kind, string? color, int eggId = -1, ulong ownerSteamId = 0)
    {
        var cfg = _plugin.Config;
        var shoot = cfg.ModesSetup.ShootEggMode;

        var prop = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic_override");
        if (prop == null || !prop.IsValid)
        {
            _plugin.DebugLog("Failed to create egg prop!");
            return null;
        }

        prop.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_WEAPON;
        prop.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
        // The Swiftly version has the TakesDamage block commented out for non-shoot mode and then sets it
        // unconditionally. Here it is only enabled when shooting is on: the damage hook is a no-op otherwise,
        // so a damageable prop with no managed HP could be destroyed by the engine.
        if (shoot) prop.TakesDamage = true;

        var render = ResolveColor(color);
        if (render.HasValue) prop.Render = render.Value;

        prop.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2));

        prop.SetModel(cfg.EggSetup.EggModel);
        prop.DispatchSpawn();

        if (shoot)
        {
            prop.Health = cfg.ModesSetup.ShootEggHealth;
            prop.MaxHealth = cfg.ModesSetup.ShootEggHealth;
            Utilities.SetStateChanged(prop, "CBaseEntity", "m_iHealth");
            Utilities.SetStateChanged(prop, "CBaseEntity", "m_iMaxHealth");
        }

        var pos = new Vector(position.X, position.Y, position.Z + cfg.EggSetup.EggModelHeight);
        var rot = kind == EggKind.Random ? new QAngle(0, _random.Next(0, 360), 0) : new QAngle(0, 0, 0);
        prop.Teleport(pos, rot);

        prop.UseAnimGraph = false;

        if (!string.IsNullOrWhiteSpace(cfg.EggSetup.EggAnimation))
            prop.AcceptInput("SetAnimation", value: cfg.EggSetup.EggAnimation);
        prop.AcceptInput("Enable");

        prop.CBodyComponent!.SceneNode!.GetSkeletonInstance().Scale = cfg.EggSetup.EggModelScale;

        prop.Entity!.Name = $"pack-hdte-{kind}-{eggId}";

        var inst = new EggInstance { Prop = prop, Kind = kind, EggId = eggId, OwnerSteamId = ownerSteamId };

        if (cfg.EggSetup.Glowing) inst.Glow = CreateGlow(prop, pos);

        _byProp[prop.Index] = inst;

        if (cfg.ModesSetup.PickUpMode) CreateTrigger(inst, pos);

        return inst;
    }

    private CDynamicProp? CreateGlow(CDynamicProp parent, Vector pos)
    {
        var cfg = _plugin.Config.EggSetup;
        var glow = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
        if (glow == null || !glow.IsValid) return null;

        glow.Spawnflags = 0;
        glow.Render = Color.Transparent;
        glow.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2));
        glow.SetModel(cfg.EggModel);
        glow.DispatchSpawn();

        glow.Glow.GlowColorOverride = ResolveColor(cfg.GlowingColor) ?? Color.Green;
        glow.Glow.GlowRange = cfg.GlowingRange;
        glow.Glow.GlowRangeMin = 0;
        glow.Glow.GlowTeam = -1; // -1 = both teams
        glow.Glow.GlowType = 3;

        glow.Teleport(pos, new QAngle(0, 0, 0));
        glow.AcceptInput("SetParent", parent, glow, "!activator");
        return glow;
    }

    private void CreateTrigger(EggInstance inst, Vector pos)
    {
        var cfg = _plugin.Config.EggSetup;
        var trigger = Utilities.CreateEntityByName<CTriggerMultiple>("trigger_multiple");
        if (trigger == null || !trigger.IsValid)
        {
            _plugin.DebugLog("Failed to create trigger entity for egg!");
            return;
        }

        trigger.Spawnflags = (1 << 0) | (1 << 1);
        trigger.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2));
        trigger.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
        trigger.Collision.SolidFlags = 0;
        trigger.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_WEAPON;

        trigger.DispatchSpawn();
        trigger.SetModel(string.IsNullOrEmpty(cfg.TriggerModel) ? cfg.EggModel : cfg.TriggerModel);

        trigger.CBodyComponent.SceneNode.GetSkeletonInstance().Scale = cfg.EggModelScale;
        trigger.Teleport(pos, new QAngle(0, 0, 0));
        trigger.AcceptInput("SetParent", inst.Prop, trigger, "!activator");
        trigger.AcceptInput("Enable");

        inst.Trigger = trigger;
        _byTrigger[trigger.Index] = inst;
    }

    private Color? ResolveColor(string? color)
    {
        if (string.IsNullOrEmpty(color) || color == "default")
            return null;

        if (color == "r")
        {
            var known = Enum.GetValues<KnownColor>()
                .Where(c => c != KnownColor.Transparent && !Color.FromKnownColor(c).IsSystemColor)
                .ToList();
            return Color.FromKnownColor(known[_random.Next(known.Count)]);
        }

        if (Enum.TryParse<KnownColor>(color, true, out var knownColor))
            return Color.FromKnownColor(knownColor);

        _plugin.DebugLog($"Invalid egg color '{color}', using default.");
        return null;
    }

    // ---------- lookup / delete ----------

    public EggInstance? FindByTrigger(uint triggerIndex) => _byTrigger.GetValueOrDefault(triggerIndex);
    public EggInstance? FindByProp(uint propIndex) => _byProp.GetValueOrDefault(propIndex);

    private static void KillEntities(EggInstance egg)
    {
        if (egg.Trigger is { IsValid: true }) egg.Trigger.Remove();
        if (egg.Glow is { IsValid: true }) egg.Glow.Remove();
        if (egg.Prop.IsValid) egg.Prop.Remove();
    }

    public void RemoveEgg(EggInstance egg)
    {
        egg.Collected = true;
        _byProp.Remove(egg.Prop.Index);
        if (egg.Trigger != null) _byTrigger.Remove(egg.Trigger.Index);
        KillEntities(egg);
    }

    public void RemoveEggDelayed(EggInstance egg, float seconds)
    {
        egg.Collected = true;
        _byProp.Remove(egg.Prop.Index);
        if (egg.Trigger != null) _byTrigger.Remove(egg.Trigger.Index);

        if (!string.IsNullOrWhiteSpace(_plugin.Config.EggSetup.EggCollectAnimation) && egg.Prop.IsValid)
            egg.Prop.AcceptInput("SetAnimation", value: _plugin.Config.EggSetup.EggCollectAnimation);

        _plugin.AddTimer(seconds, () => KillEntities(egg));
    }

    public void RemoveAllEggEntities()
    {
        foreach (var egg in _byProp.Values.ToList())
            KillEntities(egg);
        _byProp.Clear();
        _byTrigger.Clear();
    }

    public void ForgetAllEntities()
    {
        _byProp.Clear();
        _byTrigger.Clear();
    }

    // ---------- kill egg ----------

    public void TrySpawnKillEgg(CCSPlayerController positionSource, CCSPlayerController killer)
    {
        var pawn = positionSource.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return;
        if ((float)(_random.NextDouble() * 100) > _plugin.Config.ModesSetup.ChanceToSpawn) return;

        var origin = pawn.AbsOrigin;
        if (origin == null) return;

        SpawnEgg(new Vector(origin.X, origin.Y, origin.Z), EggKind.Kill, "default", ownerSteamId: killer.SteamID);
    }

    // ---------- prizes ----------

    public string? GetRandomEggType()
    {
        var types = _plugin.Config.PrizeSetup.PresentTypes;
        if (types.Count == 0) return null;
        var roll = _random.NextDouble() * 100.0;
        double cumulative = 0;
        foreach (var kv in types)
        {
            cumulative += kv.Value.Chance;
            if (roll <= cumulative) return kv.Key;
        }
        return types.Keys.First();
    }

    public void GiveEggPrize(CCSPlayerController player)
    {
        if (!_plugin.Helper.IsRealPlayer(player)) return;

        var h = _plugin.Helper;
        var prizeCfg = _plugin.Config.PrizeSetup;

        if (!prizeCfg.ReceivePrize || prizeCfg.PresentTypes.Count == 0)
        {
            var d = _plugin.Players.Get(player.SteamID);
            var total = (d?.RandomEggs ?? 0) + (d?.KillEggs ?? 0);
            h.Chat(player, "hdte.picked_egg_no_prize", total);
            return;
        }

        var type = GetRandomEggType();
        if (type == null) return;

        var typeCfg = prizeCfg.PresentTypes[type];

        var candidates = typeCfg.Rewards.Select(r => new PrizeCandidate(r.Key, r.Value, null)).ToList();
        candidates.AddRange(_plugin.Api.GetPrizeRewards(type).Select(r => new PrizeCandidate(r.RewardName, r.RewardValue, r)));

        if (candidates.Count == 0) return;

        // exactly one reward from the combined pool (config + registered providers)
        var reward = candidates[_random.Next(candidates.Count)];

        bool success;
        if (reward.ModuleReward is { } moduleReward)
        {
            success = _plugin.Api.TryGivePrize(moduleReward, player.SteamID, player.PlayerName);
        }
        else
        {
            Server.ExecuteCommand(h.ReplacePlayerParameters(reward.Value, player));
            success = true;
        }

        if (!success)
        {
            _plugin.DebugLog($"Failed to give prize '{reward.Name}' from type '{type}'.");
            return;
        }

        // {color} comes from config, so tags are resolved explicitly (same as the old CSS version)
        player.PrintToChat(
            $"{h.Prefix(player)}{h.T(player, "hdte.received_prize", reward.Name, type, typeCfg.Color)}"
                .ReplaceColorTags()
        );

        var descKey = "hdte.received_desc." + reward.Name.Replace(" ", "_");
        if (!_plugin.Localizer[descKey].ResourceNotFound)
            player.PrintToChat(h.Prefix(player) + h.T(player, descKey));
    }

    private sealed record PrizeCandidate(string Name, string Value, RegisteredPrizeReward? ModuleReward);
}
