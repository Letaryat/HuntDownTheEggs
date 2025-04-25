using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using HuntDownTheEggs.Core;
using HuntDownTheEggs.Utils;
using Microsoft.Extensions.Logging;

namespace HuntDownTheEggs
{
    public class CommandManager(HuntDownTheEggsPlugin plugin)
    {
        private readonly HuntDownTheEggsPlugin _plugin = plugin;

        public void RegisterCommands()
        {
            // Register command listeners
            _plugin.AddCommandListener("changelevel", OnChangeLevelCommand, HookMode.Pre);
            _plugin.AddCommandListener("map", OnChangeLevelCommand, HookMode.Pre);
            _plugin.AddCommandListener("host_workshop_map", OnChangeLevelCommand, HookMode.Pre);
            _plugin.AddCommandListener("ds_workshop_changelevel", OnChangeLevelCommand, HookMode.Pre);
            
            // Register Plugin Commandss
            _plugin.AddCommand("css_eggplace", "Generate present where you stand", PlaceEgg);
            _plugin.AddCommand("css_tpegg", "Teleport to present using ID", TeleportToEgg);
            _plugin.AddCommand("css_removeegg", "Teleport to present using ID", RemoveEgg);
            _plugin.AddCommand("css_reloadeggs", "Teleport to present using ID", ReloadEggs);
            _plugin.AddCommand("css_placingMode", "Mode that turn offs picking up eggs", TogglePlacingMode);
            _plugin.AddCommand("css_myeggs", "Show player eggs to player", ShowMyEggs);
            _plugin.AddCommand("css_topeggs", "Print top 5 eggs in chat", ShowTopEggs);
            _plugin.AddCommand("css_topkilleggs", "Print top 5 kill eggs in chat", ShowTopKillEggs);
        }

        private void PlaceEgg(CCSPlayerController? controller, CommandInfo info)
        {
            if (controller == null || !controller.PlayerPawn.IsValid || controller.PlayerPawn.Value == null) return;
            if (!AdminManager.PlayerHasPermissions(controller, _plugin.Config.EggRootFlag)) return;
            
            var colorArg = (info.ArgCount < 2) ? "default" : info.GetArg(1);
            var eggId = _plugin.EggManager!.GetEggCount();
            var position = controller.PlayerPawn.Value!.AbsOrigin;

            if (position == null) return;

            // Spawn the egg entity
            _plugin.EggManager.SpawnEgg(
                new Vector(position.X, position.Y, position.Z + _plugin.Config.EggModelHeight), 
                colorArg, 
                $"{_plugin.EggManager._mapName}_${eggId}"
            );
            
            try
            {
                // Save the egg position
                _plugin.EggManager.AddNewEgg(controller, colorArg);
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogInformation($"Error saving egg: {ex}");
            }
        }

        private void TeleportToEgg(CCSPlayerController? controller, CommandInfo info)
        {
            if (controller == null || !controller.PlayerPawn.IsValid) return;
            if (!AdminManager.PlayerHasPermissions(controller, _plugin.Config.EggRootFlag)) return;

            if (info.ArgCount < 2)
            {
                controller.PrintToChat("Usage: !tpegg <id>");
                return;
            }

            if (!int.TryParse(info.GetArg(1), out int id))
            {
                controller.PrintToChat("Invalid egg ID format!");
                return;
            }

            var egg = _plugin.EggManager!.GetEggById(id);
            if (egg == null)
            {
                controller.PrintToChat("Could not find an egg with that ID!");
                return;
            }

            var position = new Vector(egg.X, egg.Y, egg.Z);
            controller.PlayerPawn.Value!.Teleport(position);
        }

        private void RemoveEgg(CCSPlayerController? controller, CommandInfo info)
        {
            if (controller == null || !controller.PlayerPawn.IsValid) return;
            if (!AdminManager.PlayerHasPermissions(controller, _plugin.Config.EggRootFlag)) return;

            if (info.ArgCount < 2)
            {
                Server.PrintToChatAll("Usage: !removeegg <id>");
                return;
            }

            if (!int.TryParse(info.GetArg(1), out int id))
            {
                Server.PrintToChatAll("Invalid egg ID format!");
                return;
            }

            var egg = _plugin.EggManager!.GetEggById(id);
            if (egg == null)
            {
                Server.PrintToChatAll("Could not find an egg with that ID!");
                return;
            }

            _plugin.EggManager.RemoveEgg(id);
            _plugin.EggManager.RemoveAllEggEntities();

            // Respawn all eggs
            Server.PrintToChatAll("Regenerating eggs after removal...");
            _plugin.EggManager.SpawnAllEggs();
        }

        private void ReloadEggs(CCSPlayerController? controller, CommandInfo info)
        {
            if (controller != null && !AdminManager.PlayerHasPermissions(controller, _plugin.Config.EggRootFlag)) return;
            
            _plugin.EggManager!.RemoveAllEggEntities();
            _plugin.EggManager.SpawnAllEggs();
            
            Server.PrintToChatAll("All eggs have been reloaded!");
        }

        private void TogglePlacingMode(CCSPlayerController? controller, CommandInfo info)
        {
            if (controller != null && !AdminManager.PlayerHasPermissions(controller, _plugin.Config.EggRootFlag)) return;
            
            _plugin.EggManager!.PlacingMode = !_plugin.EggManager.PlacingMode;
            Server.PrintToChatAll($"Egg placement mode: {_plugin.EggManager.PlacingMode}");
        }

        private void ShowMyEggs(CCSPlayerController? controller, CommandInfo info)
        {
            if (controller == null || !controller.PlayerPawn.IsValid) return;
            
            var steamId = controller.AuthorizedSteamID?.SteamId64 ?? 0;
            if (steamId == 0) return;
            
            var playerData = _plugin.PlayerManager!.GetPlayerData(steamId);
            if (playerData == null)
            {
                controller.PrintToChat($"{_plugin.Localizer["prefix"]}{_plugin.Localizer["playerNotFound"]}");
                return;
            }

            var totalEggs = playerData.TotalEggs + playerData.KillEggs;
            var myEggsMessage = PluginUtilities.ReplaceMessageNewlines(
                _plugin.Localizer["myEggs", 
                playerData.TotalEggs, 
                playerData.KillEggs, 
                totalEggs, 
                playerData.Eggs.Count, 
                playerData.Map]
            );
            
            controller.PrintToChat($"{myEggsMessage}");
        }

        private void ShowTopEggs(CCSPlayerController? controller, CommandInfo info)
        {
            if (controller == null || !controller.PlayerPawn.IsValid) return;
            
            controller.PrintToChat($"{_plugin.Localizer["topListHeader"]}");
            
            var topEggs = _plugin.PlayerManager!.GetTopEggs();
            if (topEggs.Count <= 0)
            {
                controller.PrintToChat($"{_plugin.Localizer["prefix"]}{_plugin.Localizer["noEggsFound"]}");
                return;
            }
            
            int i = 0;
            foreach (var entry in topEggs)
            {
                i++;
                controller.PrintToChat($"{_plugin.Localizer["topListPlayer", i, entry.Key, entry.Value]}");
            }
        }

        private void ShowTopKillEggs(CCSPlayerController? controller, CommandInfo info)
        {
            if (controller == null || !controller.PlayerPawn.IsValid) return;
            
            controller.PrintToChat($"{_plugin.Localizer["topKillEggsListHeader"]}");
            
            var topKillEggs = _plugin.PlayerManager!.GetTopKillEggs();
            if (topKillEggs.Count <= 0)
            {
                controller.PrintToChat($"{_plugin.Localizer["prefix"]}{_plugin.Localizer["noEggsFound"]}");
                return;
            }
            
            int i = 0;
            foreach (var entry in topKillEggs)
            {
                i++;
                controller.PrintToChat($"{_plugin.Localizer["topListPlayer", i, entry.Key, entry.Value]}");
            }
        }

        private HookResult OnChangeLevelCommand(CCSPlayerController? player, CommandInfo info)
        {
            _plugin.DebugLog("Changing map. Clearing egg cache!");
            _plugin.EggManager!.ClearEggs();
            return HookResult.Continue;
        }
    }
}