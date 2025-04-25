using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
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
            _plugin.RegisterEventHandler<EventRoundOfficiallyEnded>(OnRoundEnd, HookMode.Pre);
            _plugin.RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            _plugin.RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

            // Register listeners
            _plugin.RegisterListener<Listeners.OnMapStart>(OnMapStart);
            _plugin.RegisterListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);
            
            // Hook entity outputs
            _plugin.HookEntityOutput("trigger_multiple", "OnStartTouch", OnTriggerTouch, HookMode.Pre);
        }

        private void OnMapStart(string map)
        {
            _plugin.PlayerManager!.ClearPlayers();

            _plugin.EggManager!._mapName = map;
            _plugin.EggManager!._mapFilePath = Path.Combine(_plugin.ModuleDirectory, "maps", $"{map}.json");

            // Initialize egg manager with the new map
            _plugin.EggManager!.ClearEggs();
            _plugin.EggManager.SpawnAllEggs();
            
            // Load top players
            _ = _plugin.PlayerManager.LoadTopPlayersAsync(map);
            

            _plugin.DebugLog($"Map started: {map}");
        }

        private void OnServerPrecacheResources(ResourceManifest manifest)
        {
            manifest.AddResource(_plugin.Config.EggModel);
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
            // If someone is using mp_match_end_restart 0 after map end and starting a new match on the same map it would not spawn eggs anymore.
            // So it deserialize json again:
            _plugin.EggManager!.CheckIfEggsAreThere();
            // Ensure eggs are spawned at round start
            _plugin.EggManager!.SpawnAllEggs();
            return HookResult.Continue;
        }

        private HookResult OnRoundEnd(EventRoundOfficiallyEnded @event, GameEventInfo info)
        {
            _plugin.DebugLog("Round ended! Clearing egg entities.");
            _plugin.EggManager!.RemoveAllEggEntities();
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
    }
}