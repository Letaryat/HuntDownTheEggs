using System.Drawing;
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

            if (_plugin.Config.CommandsSetup.EnableCommands)
            {
                foreach (var cmd in _plugin.Config.CommandsSetup.PlaceEggCommands)
                {
                    _plugin.AddCommand(cmd, "Generate present where you stand", PlaceEgg);
                }
                foreach (var cmd in _plugin.Config.CommandsSetup.TeleportToEggCommands)
                {
                    _plugin.AddCommand(cmd, "Teleport to present using ID", TeleportToEgg);
                }
                foreach (var cmd in _plugin.Config.CommandsSetup.RemoveEggCommands)
                {
                    _plugin.AddCommand(cmd, "Remove egg via ID", RemoveEgg);
                }
                foreach (var cmd in _plugin.Config.CommandsSetup.ReloadEggsCommands)
                {
                    _plugin.AddCommand(cmd, "Reload eggs on map", ReloadEggs);
                }
                foreach (var cmd in _plugin.Config.CommandsSetup.PlacingModeCommands)
                {
                    _plugin.AddCommand(cmd, "Mode that turn offs picking up eggs", TogglePlacingMode);
                }
                foreach (var cmd in _plugin.Config.CommandsSetup.ShowMyEggsCommands)
                {
                    _plugin.AddCommand(cmd, "Show player eggs to player", ShowMyEggs);
                }
                foreach (var cmd in _plugin.Config.CommandsSetup.ShowTopEggsCommands)
                {
                    _plugin.AddCommand(cmd, "Print top 5 eggs in chat", ShowTopEggs);
                }
                foreach (var cmd in _plugin.Config.CommandsSetup.ShowTopKillEggsCommands)
                {
                    _plugin.AddCommand(cmd, "Print top 5 kill eggs in chat", ShowTopKillEggs);
                }
                foreach (var cmd in _plugin.Config.CommandsSetup.ShowTopRandomEggsCommands)
                {
                    _plugin.AddCommand(cmd, "Print top 5 kill eggs in chat", ShowTopRandomEggs);
                }
                _plugin.DebugLog("Commands registered.");
            }

        }

        private void PlaceEgg(CCSPlayerController? controller, CommandInfo info)
        {
            if (controller == null || !controller.PlayerPawn.IsValid || controller.PlayerPawn.Value == null) return;
            if (!AdminManager.PlayerHasPermissions(controller, _plugin.Config.EggSetup.EggRootFlag)) return;

            var colorArg = (info.ArgCount < 2) ? "default" : info.GetArg(1);
            var eggId = _plugin.EggManager!.GetEggCount();
            var position = controller.PlayerPawn.Value!.AbsOrigin;

            if (position == null) return;

            // Spawn the egg entity
            _plugin.EggManager.SpawnEgg(
                new Vector(position.X, position.Y, position.Z + _plugin.Config.EggSetup.EggModelHeight),
                colorArg,
                $"{_plugin.EggManager._mapName}_${eggId}"
            );

            try
            {
                // Save the egg position
                _plugin.EggManager.AddNewEgg(controller, colorArg);
                controller.PrintToChat($"{_plugin.Localizer["prefix"]}{_plugin.Localizer["placedEgg", position]}");
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogInformation($"Error saving egg: {ex}");
                controller.PrintToChat($"{_plugin.Localizer["prefix"]} Something went wrong while saving the egg!");

            }
        }

        private void TeleportToEgg(CCSPlayerController? controller, CommandInfo info)
        {
            if (controller == null || !controller.PlayerPawn.IsValid) return;
            if (!AdminManager.PlayerHasPermissions(controller, _plugin.Config.EggSetup.EggRootFlag)) return;

            if (info.ArgCount < 2)
            {
                controller.PrintToChat($"{_plugin.Localizer["prefix"]}Usage: !tpegg <id>");
                return;
            }

            if (!int.TryParse(info.GetArg(1), out int id))
            {
                controller.PrintToChat($"{_plugin.Localizer["prefix"]}Invalid egg ID format!");
                return;
            }

            var egg = _plugin.EggManager!.GetEggById(id);
            if (egg == null)
            {
                controller.PrintToChat($"{_plugin.Localizer["prefix"]}Could not find an egg with that ID!");
                return;
            }

            var position = new Vector(egg.X, egg.Y, egg.Z);
            controller.PlayerPawn.Value!.Teleport(position);
            controller.PrintToChat($"{_plugin.Localizer["prefix"]}{_plugin.Localizer["teleportToEgg", id]}");
        }

        private void RemoveEgg(CCSPlayerController? controller, CommandInfo info)
        {
            if (controller == null || !controller.PlayerPawn.IsValid) return;
            if (!AdminManager.PlayerHasPermissions(controller, _plugin.Config.EggSetup.EggRootFlag)) return;

            if (info.ArgCount < 2)
            {
                controller.PrintToChat($"{_plugin.Localizer["prefix"]}Usage: !removeegg <id>");
                return;
            }

            if (!int.TryParse(info.GetArg(1), out int id))
            {
                controller.PrintToChat($"{_plugin.Localizer["prefix"]}Invalid egg ID format!");
                return;
            }

            var egg = _plugin.EggManager!.GetEggById(id);
            if (egg == null)
            {
                controller.PrintToChat($"{_plugin.Localizer["prefix"]}Could not find an egg with that ID!");
                return;
            }

            _plugin.EggManager.RemoveEgg(id);
            _plugin.EggManager.SaveIfRemovedEggs();
            _plugin.EggManager.RemoveAllEggEntities();

            // Respawn all eggs
            controller.PrintToChat($"{_plugin.Localizer["prefix"]}Regenerating eggs after removal...");
            _plugin.EggManager.SpawnAllEggs();
        }

        private void ReloadEggs(CCSPlayerController? controller, CommandInfo info)
        {
            if (controller == null || !controller.PlayerPawn.IsValid) return;
            if (controller != null && !AdminManager.PlayerHasPermissions(controller, _plugin.Config.EggSetup.EggRootFlag)) return;

            _plugin.EggManager!.RemoveAllEggEntities();
            _plugin.EggManager.SpawnAllEggs();

            controller!.PrintToChat($"{_plugin.Localizer["prefix"]}All eggs have been reloaded!");
        }

        private void TogglePlacingMode(CCSPlayerController? controller, CommandInfo info)
        {
            if (controller != null && !AdminManager.PlayerHasPermissions(controller, _plugin.Config.EggSetup.EggRootFlag)) return;

            _plugin.EggManager!.PlacingMode = !_plugin.EggManager.PlacingMode;
            Server.PrintToChatAll($"{_plugin.Localizer["prefix"]}Egg placement mode: {_plugin.EggManager.PlacingMode}");
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

        private void ShowTopRandomEggs(CCSPlayerController? controller, CommandInfo info)
        {
            if (controller == null || !controller.PlayerPawn.IsValid) return;

            controller.PrintToChat($"{_plugin.Localizer["topRandomEggsListHeader"]}");

            var topKillEggs = _plugin.PlayerManager!.GetTopRandomEggs();
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
            //_plugin.EventManager!.DeregisterEvents();
            return HookResult.Continue;
        }
    }
}