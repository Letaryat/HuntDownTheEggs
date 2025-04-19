using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API;
using System.Text.Json;


namespace HuntDownTheEggs
{
    public partial class HuntDownTheEggs
    {
        [ConsoleCommand("css_eggplace", "Generate present where you stand")]
        [CommandHelper(usage: "[color]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void PlacePresent(CCSPlayerController controller, CommandInfo info)
        {
            if (!AdminManager.PlayerHasPermissions(controller, Config.EggRootFlag)) return;
            if (controller == null || controller.PlayerPawn.Value == null || !controller.PlayerPawn.IsValid) return;
            
            var arg = info.GetArg(1);

            if(info.ArgCount < 2)
            {
                arg = "default";
            }

            var presentid = presents.Count();

            GeneratePresent(new Vector(controller.PlayerPawn.Value.AbsOrigin!.X, controller.PlayerPawn.Value.AbsOrigin!.Y, controller.PlayerPawn.Value.AbsOrigin!.Z + Config.EggModelHeight), arg, $"{mapName}_${presentid}");
            
            try
            {
                WritePresentCords(controller, arg);
            }
            catch (Exception ex)
            {
                Logger.LogInformation($"{ex}");
            }
            return;
        }

        [ConsoleCommand("css_tpegg", "Teleport to present using ID")]
        [CommandHelper(minArgs: 1, usage: "[id]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void TeleportToPresent(CCSPlayerController controller, CommandInfo info)
        {
            if (!AdminManager.PlayerHasPermissions(controller, Config.EggRootFlag)) return;
            if (controller?.PlayerPawn?.Value == null) return;

            if (info.ArgCount < 2)
            {
                controller.PrintToChat("Usage: !tppresent <id>");
                return;
            }

            if (!int.TryParse(info.GetArg(1), out int id))
            {
                controller.PrintToChat("Cannot find present with that id!");
                return;
            }

            if (!File.Exists(filePath))
            {
                Logger.LogInformation("Cannot find file with presents!");
                return;
            }

            if (presents == null || id < 0 || id >= presents.Count)
            {
                controller.PrintToChat("Cannot find present with that id!.");
                return;
            }

            var pos = new Vector(presents[id].X, presents[id].Y, presents[id].Z);
            controller.PlayerPawn.Value.Teleport(pos);
            return;
        }

        [ConsoleCommand("css_removeegg", "Teleport to present using ID")]
        [CommandHelper(minArgs: 1, usage: "[id]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void RemovePresent(CCSPlayerController controller, CommandInfo info)
        {
            if (!AdminManager.PlayerHasPermissions(controller, Config.EggRootFlag)) return;
            if (controller?.PlayerPawn?.Value == null) return;

            if (info.ArgCount < 2)
            {
                Server.PrintToChatAll("Usage: !removepresent <id>");
                return;
            }

            if (!int.TryParse(info.GetArg(1), out int id))
            {
                Server.PrintToChatAll("Cannot find present with such id!");
                return;
            }

            if (!File.Exists(filePath))
            {
                Logger.LogInformation("Cannot find file with presents! Try restarting the server or changing the map!");
                return;
            }

            if (presents == null || id < 0 || id >= presents.Count)
            {
                Server.PrintToChatAll("Cannot find present with such id!");
                return;
            }
            presents.RemoveAt(id);

            for (int i = 0; i < presents.Count; i++)
            {
                presents[i].Id = i;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(filePath, JsonSerializer.Serialize(presents, options));

            RemovePresents();

            foreach (var present in presents) {
                Server.PrintToChatAll("Generating new presents!");
                GeneratePresent(new Vector(present.X, present.Y, present.Z), present.modelColor!, $"{mapName}_{present.Id}");
            }
            return;
        }

        [ConsoleCommand("css_reloadeggs", "Teleport to present using ID")]
        public void ReloadPresents(CCSPlayerController controller, CommandInfo info)
        {
            if (!AdminManager.PlayerHasPermissions(controller, Config.EggRootFlag)) return;
            RemovePresents();
            SerializeJsonFromMap();
            foreach (var present in presents)
            {
                Server.PrintToChatAll("Generating new presents!");
                GeneratePresent(new Vector(present.X, present.Y, present.Z), present.modelColor!, $"{mapName}_{present.Id}");
            }
            return;
        }

        [ConsoleCommand("css_placingMode", "Mode that turn offs picking up eggs")]
        public void PlacingMode(CCSPlayerController controller, CommandInfo info)
        {
            if (!AdminManager.PlayerHasPermissions(controller, Config.EggRootFlag)) return;
            if (!placingMode)
            {
                placingMode = true;
            }
            else
            {
                placingMode = false;
            }
            Server.PrintToChatAll($"Placing mode: {placingMode}");
            return;
        }

        [ConsoleCommand("css_myeggs", "Show player eggs to player")]
        public void MyEggs(CCSPlayerController controller, CommandInfo info)
        {
            if (controller.PlayerPawn.Value == null || !controller.PlayerPawn.IsValid) return;
            if (Players[controller!.AuthorizedSteamID!.SteamId64] == null)
            {
                controller.PrintToChat($"{Localizer["prefix"]}{Localizer["playerNotFound"]}");
            }
            var myEggsMSG = ReplaceMSG(Localizer["myEggs", Players[controller.AuthorizedSteamID.SteamId64].totalEggs, Players[controller.AuthorizedSteamID.SteamId64].killeggs, Players[controller.AuthorizedSteamID.SteamId64].map]);
            controller.PrintToChat($"{myEggsMSG}");
            return;
        }

        [ConsoleCommand("css_topeggs", "Print top 5 eggs in chat")]
        public void TopEggs(CCSPlayerController controller, CommandInfo info)
        {
            var player = controller;
            if (player == null || player!.PlayerPawn.Value == null) return;
            player.PrintToChat($"{Localizer["topListHeader"]}");
            var i = 0;
            if (_TopEggsCache.Count <= 0)
            {
                player.PrintToChat($"{Localizer["prefix"]}{Localizer["noEggsFound"]}");
                return;
            }
            foreach (var p in _TopEggsCache)
            {
                i++;
                player.PrintToChat($"{Localizer["topListPlayer", i, p.Key, p.Value]}");
            }
            return;
        }
        [ConsoleCommand("css_topkilleggs", "Print top 5 kill eggs in chat")]
        public void TopKillEggs(CCSPlayerController controller, CommandInfo info)
        {
            var player = controller;
            if (player == null || player!.PlayerPawn.Value == null) return;
            player.PrintToChat($"{Localizer["topKillEggsListHeader"]}");
            var i = 0;
            if (_TopKillEggsCache.Count <= 0)
            {
                player.PrintToChat($"{Localizer["prefix"]}{Localizer["noEggsFound"]}");
                return;
            }
            foreach (var p in _TopKillEggsCache)
            {
                i++;
                player.PrintToChat($"{Localizer["topListPlayer", i, p.Key, p.Value]}");
            }
            return;
        }

    }
}
