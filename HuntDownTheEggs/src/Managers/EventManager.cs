using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;

namespace HuntDownTheEggs;

public sealed class EventManager(HuntDownTheEggsPlugin plugin)
{
    private readonly HuntDownTheEggsPlugin _plugin = plugin;

    public void RegisterEvents()
    {
        _plugin.RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        _plugin.RegisterEventHandler<EventRoundStart>(OnRoundStart);
        _plugin.RegisterEventHandler<EventRoundEnd>(OnRoundEnd, HookMode.Pre);
        _plugin.RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        _plugin.RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        _plugin.RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

        _plugin.RegisterListener<Listeners.OnMapStart>(OnMapStart);
        _plugin.RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
        _plugin.RegisterListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);

        _plugin.HookEntityOutput("trigger_multiple", "OnStartTouch", OnTriggerTouch, HookMode.Pre);
        _plugin.RegisterListener<Listeners.OnEntityTakeDamagePre>(OnTakeDamage);
        // Pre-hook: eggs have their own HP, so the engine's damage handling is skipped for them (see OnTakeDamage)
        //VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(OnTakeDamage, HookMode.Pre);
    }



    public void DeregisterEvents()
    {
        //VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Unhook(OnTakeDamage, HookMode.Pre);
    }

    // ---------- game events ----------

    private HookResult OnPlayerDeath(EventPlayerDeath e, GameEventInfo info)
    {
        if (!_plugin.Config.ModesSetup.DeathMode) return HookResult.Continue;

        var victim = e.Userid;
        var attacker = e.Attacker;
        if (victim == null || attacker == null || !victim.IsValid || !attacker.IsValid) return HookResult.Continue;
        if (victim.SteamID == attacker.SteamID) return HookResult.Continue;
        if (attacker.PlayerPawn.Value is not { IsValid: true }) return HookResult.Continue;

        if (_plugin.Config.ModesSetup.SpawnDeathEggOnVictim) _plugin.Eggs.TrySpawnKillEgg(victim, attacker);
        else _plugin.Eggs.TrySpawnKillEgg(attacker, attacker);

        return HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart e, GameEventInfo info)
    {
        _plugin.Eggs.ForgetAllEntities();
        // mp_match_end_restart 0 + same map again: the in-memory list may be empty, so reload from file
        _plugin.Eggs.CheckIfEggsAreThere();

        _plugin.WarmupRunning = _plugin.Helper.IsWarmup();

        if (!_plugin.Config.ModesSetup.SpawnEggsOnWarmup && _plugin.WarmupRunning)
        {
            _plugin.DebugLog("Warmup detected, not spawning eggs on this round.");
            return HookResult.Continue;
        }

        _plugin.Eggs.SpawnAllEggs();
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd e, GameEventInfo info)
    {
        if (_plugin.Config.ModesSetup.RemoveOnFind && _plugin.Config.ModesSetup.SpawnPlacedEggsOnce)
        {
            _plugin.DebugLog("Saving eggs to map file that were taken");
            _plugin.Eggs.SaveIfRemovedEggs();
        }
        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn e, GameEventInfo info)
    {
        var p = e.Userid;
        if (!_plugin.Helper.IsRealPlayer(p)) return HookResult.Continue;
        _plugin.Players.EnsureLoaded(p!.SteamID, p.PlayerName);
        return HookResult.Continue;
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull e, GameEventInfo info)
    {
        var player = e.Userid;
        if (!_plugin.Helper.IsRealPlayer(player)) return HookResult.Continue;

        var steamId = player!.SteamID;
        _plugin.Players.EnsureLoaded(steamId, player.PlayerName);

        _plugin.AddTimer(5f, () =>
        {
            if (!_plugin.Helper.IsRealPlayer(player)) return;
            if (!_plugin.Config.ModesSetup.SearchMode && _plugin.Eggs.EggCount == 0) return;
            _plugin.Helper.Lines(player, "hdte.welcome", _plugin.Eggs.EggCount, _plugin.Config.ModesSetup.NumberOfRandomEggs);
        });

        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect e, GameEventInfo info)
    {
        var player = e.Userid;
        if (!_plugin.Helper.IsRealPlayer(player)) return HookResult.Continue;
        _plugin.Players.SaveAndForget(player!.SteamID, player.PlayerName);
        return HookResult.Continue;
    }

    // ---------- listeners ----------

    private void OnMapStart(string map)
    {
        _plugin.MapName = map;
        _plugin.WarmupRunning = false;

        _plugin.Players.Clear();
        _plugin.Eggs.ClearCache();
        _plugin.Eggs.LoadEggsFromMap();
        _plugin.Players.LoadTopLists(map);

        _plugin.DebugLog($"Map started: {map}");
    }

    private void OnMapEnd()
    {
        _plugin.Players.SaveAllInBackground();
        _plugin.Eggs.ForgetAllEntities();
    }

    private void OnServerPrecacheResources(ResourceManifest manifest)
    {
        manifest.AddResource(_plugin.Config.EggSetup.EggModel);
        if (!string.IsNullOrEmpty(_plugin.Config.EggSetup.TriggerModel))
            manifest.AddResource(_plugin.Config.EggSetup.TriggerModel);
    }

    // ---------- gathering eggs by touching ----------

    private HookResult OnTriggerTouch(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
    {
        if (!_plugin.Config.ModesSetup.PickUpMode) return HookResult.Continue;

        // fast reject: only triggers that belong to an egg
        var egg = _plugin.Eggs.FindByTrigger(caller.Index);
        if (egg == null || egg.Collected) return HookResult.Continue;

        if (activator.DesignerName != "player") return HookResult.Continue;

        var pawn = activator.As<CCSPlayerPawn>();
        if (pawn == null || !pawn.IsValid) return HookResult.Continue;

        var player = pawn.OriginalController?.Value?.As<CCSPlayerController>();
        if (!_plugin.Helper.IsRealPlayer(player)) return HookResult.Continue;
        if (player == null || !player.IsValid) return HookResult.Continue;

        var data = _plugin.Players.Get(player.SteamID);
        if (data == null)
        {
            _plugin.Players.EnsureLoaded(player.SteamID, player.PlayerName);
            return HookResult.Continue;
        }

        var h = _plugin.Helper;

        switch (egg.Kind)
        {
            case EggKind.Kill:
                if (_plugin.Config.EggSetup.KillEggOnlyForKiller && egg.OwnerSteamId != player.SteamID)
                {
                    player.PrintToCenter(h.T(player, "hdte.only_for_killer"));
                    return HookResult.Continue;
                }
                _plugin.Players.IncrementKillEggs(player.SteamID);
                h.Chat(player, "hdte.kill_egg");
                _plugin.Eggs.RemoveEgg(egg);
                _plugin.Eggs.GiveEggPrize(player);
                h.NotifyCollected(player, EggKind.Kill, null);
                return HookResult.Continue;

            case EggKind.Random:
                _plugin.Players.IncrementRandomEggs(player.SteamID);
                h.Chat(player, "hdte.random_egg");
                _plugin.Eggs.RemoveEgg(egg);
                _plugin.Eggs.GiveEggPrize(player);
                h.NotifyCollected(player, EggKind.Random, null);
                return HookResult.Continue;
        }

        // Placed
        if (_plugin.Eggs.PlacingMode) return HookResult.Continue;

        if (data.Eggs.Contains(egg.EggId))
        {
            if (!_plugin.Config.EggSetup.HidePickedEggsForPlayer)
                h.Chat(player, "hdte.already_own");
            return HookResult.Continue;
        }

        _plugin.Players.AddFoundEgg(player.SteamID, egg.EggId);

        if (_plugin.Config.ModesSetup.RemoveOnFind)
        {
            _plugin.Eggs.RemoveEgg(egg);
            if (_plugin.Config.ModesSetup.SpawnPlacedEggsOnce) _plugin.Eggs.RemoveEgg(egg.EggId);
        }

        _plugin.Eggs.GiveEggPrize(player);
        h.NotifyCollected(player, EggKind.Placed, egg.EggId);

        return HookResult.Continue;
    }

    // ---------- gathering eggs by shooting ----------

    private HookResult OnTakeDamage(CBaseEntity entity, CTakeDamageInfo info)
    {
        if (!_plugin.Config.ModesSetup.ShootEggMode) return HookResult.Continue;

        if (entity == null || !entity.IsValid) return HookResult.Continue;

        var egg = _plugin.Eggs.FindByProp(entity.Index);
        if (egg == null || egg.Collected) return HookResult.Continue;

        // Od tego miejsca to jajko: obrażenia silnika są pomijane, HP liczymy ręcznie.
        var attackerEntity = info.Attacker.Value;
        if (attackerEntity == null || !attackerEntity.IsValid || attackerEntity.DesignerName != "player")
            return HookResult.Handled;

        var pawn = attackerEntity.As<CCSPlayerPawn>();
        if (pawn == null || !pawn.IsValid) return HookResult.Handled;

        var player = pawn.OriginalController?.Value?.As<CCSPlayerController>();
        if (!_plugin.Helper.IsRealPlayer(player)) return HookResult.Handled;
        if(player == null || !player.IsValid) return HookResult.Continue;

        var data = _plugin.Players.Get(player.SteamID);
        if (data == null)
        {
            _plugin.Players.EnsureLoaded(player.SteamID, player.PlayerName);
            return HookResult.Handled;
        }

        var h = _plugin.Helper;

        if (egg.Kind == EggKind.Kill && _plugin.Config.EggSetup.KillEggOnlyForKiller && egg.OwnerSteamId != player.SteamID)
        {
            player.PrintToCenter(h.T(player, "hdte.only_for_killer"));
            return HookResult.Handled;
        }

        if (egg.Kind == EggKind.Placed && data.Eggs.Contains(egg.EggId))
        {
            h.Chat(player, "hdte.already_own");
            return HookResult.Handled;
        }

        egg.Prop.Health -= (int)Math.Round(info.Damage);
        Utilities.SetStateChanged(egg.Prop, "CBaseEntity", "m_iHealth");

        if (egg.Prop.Health > 0)
        {
            h.Chat(player, "hdte.egg_shot_hp", egg.Prop.Health);
            return HookResult.Handled;
        }

        // jajko zniszczone
        switch (egg.Kind)
        {
            case EggKind.Kill:
                _plugin.Players.IncrementKillEggs(player.SteamID);
                h.Chat(player, "hdte.kill_egg");
                break;
            case EggKind.Random:
                _plugin.Players.IncrementRandomEggs(player.SteamID);
                h.Chat(player, "hdte.random_egg");
                break;
            default:
                _plugin.Players.AddFoundEgg(player.SteamID, egg.EggId);
                break;
        }

        _plugin.Eggs.RemoveEggDelayed(egg, 0.3f);
        _plugin.Eggs.GiveEggPrize(player);

        h.NotifyCollected(player, egg.Kind, egg.Kind == EggKind.Placed ? egg.EggId : null);

        return HookResult.Handled;
    }
}
