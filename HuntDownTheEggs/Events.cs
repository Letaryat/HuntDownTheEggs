using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
namespace HuntDownTheEggs
{
    public partial class HuntDownTheEggs
    {

        public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
        {
            if (Config.DeathMode == false) return HookResult.Continue;
            var victim = @event.Userid;
            var attacker = @event.Attacker;
            if (victim == null) { return HookResult.Continue; }
            if (attacker == null || victim == attacker)
            {
                return HookResult.Continue;
            }
            if (attacker.PlayerPawn.Value == null || !attacker.PlayerPawn.IsValid)
            {
                return HookResult.Continue;
            }

            if(Config.SpawnDeathEggOnVictim)
            {
                ChanceToSpawnEgg(victim);
            }
            else
            {
                ChanceToSpawnEgg(attacker);
            }

            return HookResult.Continue;
        }

        public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            if (!File.Exists(filePath))
            {
                DebugMode("Cannot find .json file with presents. You might want to try to restart the server / change map.");
                return HookResult.Continue;
            }

            string json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json)) return HookResult.Continue;

            if (presents == null || presents.Count == 0)
            {
                try
                {
                    DebugMode("Cannot find any presents. Trying to fetch data again.");
                    SerializeJsonFromMap();

                    // Tutaj warto jeszcze raz sprawdzić, czy po deserializacji coś jest:
                    if (presents == null || presents.Count == 0)
                    {
                        DebugMode("Deserialized presents are still empty.");
                        return HookResult.Continue;
                    }
                }
                catch (Exception e)
                {
                    DebugMode(e.ToString());
                    return HookResult.Continue;
                }
            }

            foreach (var present in presents)
            {
                GeneratePresent(new Vector(present.X, present.Y, present.Z), present!.modelColor!, $"{mapName}_${present.Id}");
            }

            return HookResult.Continue;
        }

        public HookResult OnRoundEnd(EventRoundOfficiallyEnded @event, GameEventInfo info)
        {
            DebugMode("Round has ended! Clearing cache!");

            Presents.Clear();
            return HookResult.Continue;
        }

        public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (player == null) return HookResult.Continue;

            if (Config.SearchMode || presents.Count() != 0) {
                var msgWelcome = Localizer["welcomeMessage", presents.Count()];
                var msg = ReplaceMSG(msgWelcome);
                player.PrintToChat($"{msg}");
            }

            /*
            if (player.AuthorizedSteamID == null)
                return HookResult.Continue;
            var steamid64 = player.AuthorizedSteamID.SteamId64;
            */

            var steamid64 = player.SteamID;

            Task.Run(async () =>
            {
                try
                {
                    await OnClientAuthorizedAsync(steamid64, player);
                }
                catch (Exception ex)
                {
                    Logger.LogInformation($"{ex}");
                }
            });

            return HookResult.Continue;
        }
        /*
        private async Task HandlePlayerAsync(ulong steamid)
        {
            try
            {
                await OnClientAuthorizedAsync(steamid, );
                DebugMode("Player {steamid} loaded successfully.");
                
            }
            catch (Exception ex)
            {
                Logger.LogInformation($"[Player Load] Error loading {steamid}: {ex}");
            }
        }
        */
        public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info) 
        {
            var player = @event.Userid;

            if (player == null) return HookResult.Continue;
            var steamid64 = player.SteamID;
            if (!Players.ContainsKey(steamid64))
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await OnClientAuthorizedAsync(steamid64, player);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogInformation(ex.ToString());
                    }
                });
            }
            return HookResult.Continue; 
        }


        HookResult trigger_multiple(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            var pawn = activator.As<CCSPlayerPawn>();
            if (pawn == null || !pawn.IsValid)
                return HookResult.Continue;

            var player = pawn.OriginalController?.Value?.As<CCSPlayerController>();
            if (player == null || player.IsBot || player.IsHLTV)
                return HookResult.Continue;

            var eggName = Presents[caller.Index].Entity!.Name;
            var steamid = player.AuthorizedSteamID?.SteamId64 ?? 0;
            if (steamid == 0) return HookResult.Continue;

            if (!Players.ContainsKey(steamid))
            {
                var user = GetPlayerEggs(steamid, mapName!);
                if (user?.Result == null)
                {
                    Players[steamid] = new PlayerEggs
                    {
                        steamid = steamid,
                        playername = player.PlayerName,
                        map = mapName!,
                        eggs = new(),
                        killeggs = 0
                    };
                }

                Players[steamid] = new PlayerEggs
                {
                    steamid = user.Result.steamid,
                    playername = player.PlayerName,
                    map = user.Result.map,
                    eggs = user.Result.eggs,
                    killeggs = user.Result.killeggs,
                    totalEggs = user.Result.totalEggs
                };
            }

            if (eggName.Contains("kill"))
            {

                    Players[steamid].killeggs++;

                    player.PrintToChat($"{Localizer["prefix"]}{Localizer["killEgg"]}");

                    if (Presents.ContainsKey(caller.Index))
                    {
                        Presents[caller.Index].Remove();
                        caller.Remove();
                        Presents.Remove(caller.Index);
                    }
                GivePrize(player);
                return HookResult.Continue;
            }
            
            if (placingMode == true) return HookResult.Continue;
            string[] splitEgg = Presents[caller.Index].Entity!.Name.Split("$");
            var eggID = int.Parse(splitEgg[1]);

            if (Players[player.AuthorizedSteamID!.SteamId64].eggs.Contains(eggID))
            {
                player.PrintToChat($"{Localizer["prefix"]}{Localizer["alreadyOwn"]}");
                return HookResult.Continue;
            }
            PickedUpPresent(player, Presents[caller.Index].Entity!.Name);

            if(Presents.ContainsKey(caller.Index))
            {
                if (Config.RemoveOnFind)
                {
                    Presents[caller.Index].Remove();
                    caller.Remove();
                    Presents.Remove(caller.Index);
                    if(Config.SpawnPlacedEggsOnce)
                    {
                        var egg = presents.FirstOrDefault(p => p.Id == eggID);
                        if (egg != null)
                        {
                            presents.Remove(egg);
                        }
                    }
                }
            }
            GivePrize(player);

            return HookResult.Continue;
        }

        public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            try
            {
                var player = @event.Userid;
                if (player == null || player.IsBot || player.IsHLTV) return HookResult.Continue;

                //leaving this commented since idk player!.steamid is okay but commented thing sometimes ended up as NullReferenceException

                //var steamid64 = player!.AuthorizedSteamID!.SteamId64;
                var steamid64 = player!.SteamID;
                var name = player.PlayerName;
                try
                {
                    Task.Run(async () =>
                    {
                        DebugMode($"Saving player data that disconnected!");

                        await SaveEggs(steamid64, name);
                    });
                }
                catch (Exception ex) { Logger.LogInformation(ex.ToString()); }
            }
            catch(Exception ex)
            {
                Logger.LogInformation(ex.ToString());
            }

            return HookResult.Continue;
        }

    }
}
