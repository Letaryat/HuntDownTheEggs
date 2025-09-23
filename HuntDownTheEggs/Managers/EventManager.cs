using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;
using HuntDownTheEggs.Core;
using HuntDownTheEggs.Utils;
using Microsoft.Extensions.Logging;

namespace HuntDownTheEggs
{
    public class EventManager(HuntDownTheEggsPlugin plugin)
    {
        private readonly HuntDownTheEggsPlugin _plugin = plugin;

        public void RegisterEvents()
        {
            // Register game events
            _plugin.RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
            _plugin.RegisterEventHandler<EventRoundStart>(OnRoundStart);
            _plugin.RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
            _plugin.RegisterEventHandler<EventRoundEnd>(OnRoundEnd, HookMode.Pre);
            _plugin.RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            _plugin.RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

            // Register listeners
            _plugin.RegisterListener<Listeners.OnMapStart>(OnMapStart);
            _plugin.RegisterListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);
            _plugin.RegisterListener<Listeners.CheckTransmit>(OnCheckTransmit);

            // Hook entity outputs
            _plugin.HookEntityOutput("trigger_multiple", "OnStartTouch", OnTriggerTouch, HookMode.Pre);

            // Kill entities
            VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(OnTakeDamage, HookMode.Post);
        }

        private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
        {
            if (!_plugin.Config.DeathMode) return HookResult.Continue;

            var victim = @event.Userid;
            var attacker = @event.Attacker;

            if (victim == null || attacker == null || victim == attacker)
                return HookResult.Continue;

            if (attacker.PlayerPawn.Value == null || !attacker.PlayerPawn.IsValid)
                return HookResult.Continue;

            // Spawn egg based on config setting
            if (_plugin.Config.SpawnDeathEggOnVictim)
            {
                _plugin.EggManager!.TrySpawnDeathEgg(victim);
            }
            else
            {
                _plugin.EggManager!.TrySpawnDeathEgg(attacker);
            }

            return HookResult.Continue;
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {

            //_plugin.SpawnManager!.SetDMSpawns();
            // If someone is using mp_match_end_restart 0 after map end and starting a new match on the same map it would not spawn eggs anymore.
            // So it deserialize json again:
            _plugin.EggManager!.CheckIfEggsAreThere();

            // Ensure eggs are spawned at round start
            _plugin.EggManager!.SpawnAllEggs();

            return HookResult.Continue;
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            if (_plugin.Config.RemoveOnFind && _plugin.Config.SpawnPlacedEggsOnce)
            {
                _plugin.DebugLog("Saving eggs to map file that were taken");
                _plugin.EggManager!.SaveIfRemovedEggs();
            }
            return HookResult.Continue;
        }

        private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (player == null) return HookResult.Continue;

            // Show welcome message with egg count
            var eggCount = _plugin.EggManager!.GetEggCount();
            if (_plugin.Config.SearchMode || eggCount > 0)
            {
                var welcomeMessage = _plugin.Localizer["welcomeMessage", eggCount];
                var formattedMessage = PluginUtilities.ReplaceMessageNewlines(welcomeMessage);
                player.PrintToChat($"{formattedMessage}");
            }

            // Load player data from database
            var steamId = player.SteamID;
            var playerName = player.PlayerName;

            Task.Run(async () =>
            {
                try
                {
                    await _plugin.PlayerManager!.AddPlayerAsync(steamId, playerName);
                }
                catch (Exception ex)
                {
                    _plugin.Logger.LogInformation($"Error loading player data: {ex}");
                }
            });

            return HookResult.Continue;
        }

        private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (player == null || player.IsBot || player.IsHLTV) return HookResult.Continue;

            var steamId = player.SteamID;
            var playerName = player.PlayerName;

            // Ensure player data is loaded
            if (_plugin.PlayerManager!.GetPlayerData(steamId) == null)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await _plugin.PlayerManager.AddPlayerAsync(steamId, playerName);
                    }
                    catch (Exception ex)
                    {
                        _plugin.Logger.LogInformation($"Error loading player data: {ex}");
                    }
                });
            }

            return HookResult.Continue;
        }

        private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            try
            {
                var player = @event.Userid;
                if (player == null || player.IsBot || player.IsHLTV) return HookResult.Continue;

                var steamId = player.SteamID;
                var playerName = player.PlayerName;

                Task.Run(async () =>
                {
                    _plugin.DebugLog($"Saving data for disconnected player: {playerName}");

                    if (_plugin.PlayerManager!.GetPlayerData(steamId) != null)
                    {
                        await _plugin.PlayerManager.SavePlayerAsync(steamId, playerName);
                        _plugin.DebugLog($"Player data saved successfully for {playerName}");
                    }
                    else
                    {
                        _plugin.DebugLog($"No player data found for {playerName} (SteamID: {steamId})");
                    }
                }).Wait();
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogInformation($"Error handling player disconnect: {ex}");
            }

            return HookResult.Continue;
        }

        private HookResult OnTriggerTouch(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if (_plugin.Config.ShootEggMode) return HookResult.Continue;
            // Check if the activator is a valid player
            var pawn = activator.As<CCSPlayerPawn>();
            if (pawn == null || !pawn.IsValid)
                return HookResult.Continue;

            var player = pawn.OriginalController?.Value?.As<CCSPlayerController>();
            if (player == null || player.IsBot || player.IsHLTV)
                return HookResult.Continue;

            // Check if this trigger has an associated egg
            uint triggerIndex = caller.Index;
            if (!_plugin.EggManager!.HasEgg(triggerIndex))
                return HookResult.Continue;

            // Get egg entity and steamId
            var eggEntity = _plugin.EggManager.GetEgg(triggerIndex);
            var steamId = player.AuthorizedSteamID?.SteamId64 ?? player.SteamID;

            if (steamId == 0) return HookResult.Continue;

            // Ensure player data is loaded
            var playerData = _plugin.PlayerManager!.GetPlayerData(steamId);
            if (playerData == null)
            {
                Task.Run(async () =>
                {
                    await _plugin.PlayerManager.AddPlayerAsync(steamId, player.PlayerName);
                });
                return HookResult.Continue;
            }

            // Handle kill-type eggs
            string eggName = eggEntity.Entity!.Name;
            if (eggName.Contains("kill"))
            {
                _plugin.PlayerManager.IncrementKillEggs(steamId);
                player.PrintToChat($"{_plugin.Localizer["prefix"]}{_plugin.Localizer["killEgg"]}");

                // Remove the egg entity
                _plugin.EggManager.RemoveEggEntity(triggerIndex, caller.As<CTriggerMultiple>());

                // Give prize to player
                _plugin.EggManager.GiveEggPrize(player);
                return HookResult.Continue;
            }

            // If in placing mode, don't allow picking up eggs
            if (_plugin.EggManager.PlacingMode) return HookResult.Continue;



            // Parse egg ID
            string[] eggParts = eggEntity.Entity!.Name.Split("$");
            if (eggParts.Length < 2) return HookResult.Continue;

            if (!int.TryParse(eggParts[1], out int eggId))
                return HookResult.Continue;

            // Check if player already owns this egg
            if (playerData.Eggs.Contains(eggId))
            {
                if (_plugin.Config.HidePickedEggsPlayer) return HookResult.Continue;
                player.PrintToChat($"{_plugin.Localizer["prefix"]}{_plugin.Localizer["alreadyOwn"]}");
                return HookResult.Continue;
            }

            // Add egg to player's collection
            _plugin.EggManager.HandleEggPickup(player, eggEntity.Entity!.Name);

            // Handle egg entity removal if configured
            if (_plugin.Config.RemoveOnFind)
            {
                _plugin.EggManager.RemoveEggEntity(triggerIndex, caller.As<CTriggerMultiple>());

                // Remove egg from map file if configured to spawn eggs only once
                if (_plugin.Config.SpawnPlacedEggsOnce)
                {
                    _plugin.EggManager.RemoveEgg(eggId);
                }
            }



            // Give prize to player
            _plugin.EggManager.GiveEggPrize(player);

            return HookResult.Continue;
        }


        // Listeners:
        private void OnMapStart(string map)
        {
            _plugin.PlayerManager!.ClearPlayers();

            _plugin.EggManager!._mapName = map;
            _plugin.EggManager!._mapFilePath = Path.Combine(_plugin.ModuleDirectory, "maps", $"{map}.json");

            // Initialize egg manager with the new map
            //_plugin.EggManager!.ClearEggs();

            _plugin.EggManager!.CheckIfEggsAreThere();

            //_plugin.EggManager.SpawnAllEggs();

            // Load top players
            _ = _plugin.PlayerManager.LoadTopPlayersAsync(map);

            _plugin.DebugLog($"Map started: {map}");

        }

        private void OnServerPrecacheResources(ResourceManifest manifest)
        {
            manifest.AddResource(_plugin.Config.EggModel);
            if (_plugin.Config.TriggerModel != _plugin.Config.EggModel || !string.IsNullOrEmpty(_plugin.Config.TriggerModel))
            {
                manifest.AddResource(_plugin.Config.TriggerModel);
            }

        }
        private void OnCheckTransmit(CCheckTransmitInfoList infoList)
        {
            var allEggs = Utilities.FindAllEntitiesByDesignerName<CDynamicProp>("prop_dynamic");
            if (allEggs == null || !allEggs.Any()) return;
            try
            {
                foreach (var entry in infoList)
                {
                    CCheckTransmitInfo info;
                    CCSPlayerController? player;

                    try
                    {
                        (info, player) = ((CCheckTransmitInfo, CCSPlayerController))entry;
                    }
                    catch
                    {
                        continue;
                    }

                    if (player == null) continue;
                    var steamId = player.AuthorizedSteamID?.SteamId64 ?? player.SteamID;
                    var playerData = _plugin.PlayerManager!.GetPlayerData(steamId);
                    if (playerData == null) continue;


                    foreach (var egg in allEggs)
                    {
                        //if (ad?.Entity?.Name != null && ad.Entity.Name.Contains("advert"))
                        if (egg.Entity!.Name == null) continue;

                        //Hide already picked up eggs:
                        if (_plugin.Config.HidePickedEggsPlayer)
                        {
                            if (egg.Entity.Name.StartsWith("pack-letegg"))
                            {
                                var eggParts = egg.Entity.Name.Split('$');
                                if (eggParts.Length < 2) continue;
                                if (int.TryParse(eggParts[1], out int eggId))
                                {
                                    if (playerData.Eggs.Contains(eggId))
                                    {
                                        info.TransmitEntities.Remove(egg);
                                    }
                                }
                            }
                        }
                        //Hide kill eggs for non-killers:
                        if (_plugin.Config.HidePickedEggsPlayer)
                        {
                            if (egg.Entity.Name.StartsWith("pack-$kill"))
                            {
                                var killer = egg.Entity.Name.Split('_');
                                if (killer.Length < 2) continue;
                                if (int.TryParse(killer[1], out int eggId))
                                {
                                    if (player.UserId != eggId)
                                    {
                                        info.TransmitEntities.Remove(egg);
                                    }
                                }
                            }
                        }

                    }
                }
            }
            catch (Exception error)
            {
                _plugin.DebugLog($"[HDTE] [Checktransmit] error: {error}");
            }
        }
        private HookResult OnTakeDamage(DynamicHook hook)
        {
            if (!_plugin.Config.ShootEggMode) return HookResult.Continue;

            var entities = hook.GetParam<CEntityInstance>(0);
            if (entities == null || !entities.IsValid || entities.Entity == null) return HookResult.Continue;

            var damageinfo = hook.GetParam<CTakeDamageInfo>(1);
            if (damageinfo == null)
            {
                return HookResult.Continue;
            }

            var attackerHandle = damageinfo.Attacker?.Value;
            if (attackerHandle == null || !attackerHandle.IsValid)
            {
                return HookResult.Continue;
            }

            var playerPawn = attackerHandle.As<CCSPlayerPawn>();
            if (playerPawn == null || !playerPawn.IsValid || playerPawn.Controller == null)
            {
                return HookResult.Continue;
            }

            var player = Utilities.GetPlayerFromIndex((int)playerPawn.Controller.Index);
            if (player == null || !player.IsValid)
            {
                return HookResult.Continue;
            }

            // Additional safety check for entity name
            string entityName = entities.Entity.Name;
            if (string.IsNullOrEmpty(entityName))
            {
                return HookResult.Continue;
            }

            // Server.PrintToChatAll($"Strzelasz!0 {entityName}");
            // entityName.Contains("kill_egg") || entityName.Contains("letegg")
            // Check if the damaged entity is an egg

            if (_plugin.EggManager!._testEggsEntities.Contains(entities.As<CDynamicProp>()))
            {
                var dynamicProp = entities.As<CDynamicProp>();

                if (dynamicProp == null || !dynamicProp.IsValid) return HookResult.Continue;

                var steamId = player.AuthorizedSteamID?.SteamId64 ?? player.SteamID;
                if (steamId == 0) return HookResult.Continue;

                // Ensure EggManager is not null
                if (_plugin.EggManager == null)
                {
                    return HookResult.Continue;
                }

                // Ensure PlayerManager is not null
                if (_plugin.PlayerManager == null)
                {
                    return HookResult.Continue;
                }

                // Ensure player data is loaded
                var playerData = _plugin.PlayerManager.GetPlayerData(steamId);
                if (playerData == null)
                {
                    Task.Run(async () =>
                    {
                        await _plugin.PlayerManager.AddPlayerAsync(steamId, player.PlayerName);
                    });
                    return HookResult.Continue;
                }

                // Handle kill-type eggs
                if (entityName.Contains("kill"))
                {
                    dynamicProp.Health -= (int)Math.Round(damageinfo.Damage);

                    if (dynamicProp.Health > 0)
                    {
                        player.PrintToChat($"{_plugin.Localizer["prefix"]}{_plugin.Localizer["eggShotHP", dynamicProp.Health]}");
                    }


                    _plugin.EggManager!.RemoveEntityOnShoot(dynamicProp, entities, player, 0);
                    return HookResult.Continue;
                }

                // Parse egg ID
                string[] eggParts = entityName.Split("$");
                if (eggParts.Length < 2) return HookResult.Continue;

                if (!int.TryParse(eggParts[1], out int eggId))
                    return HookResult.Continue;

                // Check if player already owns this egg
                if (playerData.Eggs.Contains(eggId))
                {
                    player.PrintToChat($"{_plugin.Localizer["prefix"]}{_plugin.Localizer["alreadyOwn"]}");
                    return HookResult.Continue;
                }

                dynamicProp.Health -= (int)Math.Round(damageinfo.Damage);

                if (dynamicProp.Health > 0)
                {
                    player.PrintToChat($"{_plugin.Localizer["prefix"]}{_plugin.Localizer["eggShotHP", dynamicProp.Health]}");
                }
                _plugin.EggManager.RemoveEntityOnShoot(dynamicProp, entities, player, 1);

            }


            return HookResult.Continue;
        }




    }
}