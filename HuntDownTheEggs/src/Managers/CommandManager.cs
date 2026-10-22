using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace HuntDownTheEggs;

public sealed class CommandManager(HuntDownTheEggsPlugin plugin)
{
    private readonly HuntDownTheEggsPlugin _plugin = plugin;

    public void RegisterCommands()
    {
        var cfg = _plugin.Config.CommandsSetup;
        if (!cfg.EnableCommands) return;

        Register(cfg.PlaceEggCommands, "Place an egg where you stand", PlaceEgg);
        Register(cfg.TeleportToEggCommands, "Teleport to an egg by ID", TeleportToEgg);
        Register(cfg.RemoveEggCommands, "Remove an egg by ID", RemoveEgg);
        Register(cfg.ReloadEggsCommands, "Reload eggs on the map", ReloadEggs);
        Register(cfg.PlacingModeCommands, "Toggle egg placing mode", TogglePlacingMode);
        Register(cfg.ShowMyEggsCommands, "Show your collected eggs", ShowMyEggs);

        RegisterTop(cfg.ShowTopPlacedEggsPerMapCommands, "hdte.top_placed_map", () => _plugin.Players.TopPlacedEggsPerMap);
        RegisterTop(cfg.ShowTopRandomEggsPerMapCommands, "hdte.top_random_map", () => _plugin.Players.TopRandomEggsPerMap);
        RegisterTop(cfg.ShowTopKillEggsPerMapCommands, "hdte.top_kills_map", () => _plugin.Players.TopKillEggsPerMap);
        RegisterTop(cfg.ShowTopAllEggsPerMapCommands, "hdte.top_all_map", () => _plugin.Players.TopAllEggsPerMap);
        RegisterTop(cfg.ShowTopPlacedEggsCommands, "hdte.top_placed_all", () => _plugin.Players.TopPlacedEggs);
        RegisterTop(cfg.ShowTopRandomEggsCommands, "hdte.top_random_all", () => _plugin.Players.TopRandomEggs);
        RegisterTop(cfg.ShowTopKillEggsCommands, "hdte.top_kills_all", () => _plugin.Players.TopKillEggs);
        RegisterTop(cfg.ShowTopAllEggsCommands, "hdte.top_all_all", () => _plugin.Players.TopAllEggs);

        _plugin.DebugLog("Commands registered.");
    }

    // CounterStrikeSharp unregisters commands added with AddCommand when the plugin unloads.

    private void Register(IEnumerable<string> names, string description, CommandInfo.CommandCallback handler)
    {
        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)))
            _plugin.AddCommand(name.StartsWith("css_", StringComparison.OrdinalIgnoreCase) ? name : "css_" + name, description, handler);
    }

    private void RegisterTop(IEnumerable<string> names, string headerKey, Func<Dictionary<string, int>> getTop) =>
        Register(names, "Show an egg leaderboard", (player, info) => ShowTop(player, headerKey, getTop));

    private bool HasRoot(CCSPlayerController? player) =>
        player == null || AdminManager.PlayerHasPermissions(player, _plugin.Config.EggSetup.EggRootPermission);

    // ---------- admin ----------

    private void PlaceEgg(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid || !HasRoot(player)) return;

        var origin = player.PlayerPawn.Value?.AbsOrigin;
        if (origin == null) return;
        var pos = new Vector(origin.X, origin.Y, origin.Z);

        var h = _plugin.Helper;
        var color = info.ArgCount < 2 ? "default" : info.GetArg(1);
        var id = _plugin.Eggs.EggCount;

        _plugin.Eggs.SpawnEgg(pos, EggKind.Placed, color, id);

        try
        {
            _plugin.Eggs.AddNewEgg(pos, color);
            h.Chat(player, "hdte.placed_egg", $"{pos.X:F0} {pos.Y:F0} {pos.Z:F0}");
        }
        catch (Exception ex)
        {
            _plugin.DebugLog($"Error saving egg: {ex}");
            h.Chat(player, "hdte.error_saving_egg");
        }
    }

    private void TeleportToEgg(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid || !HasRoot(player)) return;
        var h = _plugin.Helper;

        if (info.ArgCount < 2) { h.Chat(player, "hdte.usage_tpegg"); return; }
        if (!int.TryParse(info.GetArg(1), out var id)) { h.Chat(player, "hdte.invalid_id"); return; }

        var egg = _plugin.Eggs.GetEggById(id);
        if (egg == null) { h.Chat(player, "hdte.egg_not_found"); return; }

        player.PlayerPawn.Value?.Teleport(new Vector(egg.X, egg.Y, egg.Z));
        h.Chat(player, "hdte.teleport_to_egg", id);
    }

    private void RemoveEgg(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid || !HasRoot(player)) return;
        var h = _plugin.Helper;

        if (info.ArgCount < 2) { h.Chat(player, "hdte.usage_removeegg"); return; }
        if (!int.TryParse(info.GetArg(1), out var id)) { h.Chat(player, "hdte.invalid_id"); return; }
        if (_plugin.Eggs.GetEggById(id) == null) { h.Chat(player, "hdte.egg_not_found"); return; }

        _plugin.Eggs.RemoveEgg(id);
        _plugin.Eggs.SaveIfRemovedEggs();
        _plugin.Eggs.RemoveAllEggEntities();

        h.Chat(player, "hdte.regenerating_eggs");
        _plugin.Eggs.SpawnAllEggs();
    }

    private void ReloadEggs(CCSPlayerController? player, CommandInfo info)
    {
        if (!HasRoot(player)) return;

        _plugin.Eggs.RemoveAllEggEntities();
        _plugin.Eggs.LoadEggsFromMap();
        _plugin.Eggs.SpawnAllEggs();

        if (player != null && player.IsValid)
            _plugin.Helper.Chat(player, "hdte.eggs_reloaded");
    }

    private void TogglePlacingMode(CCSPlayerController? player, CommandInfo info)
    {
        if (!HasRoot(player)) return;

        _plugin.Eggs.PlacingMode = !_plugin.Eggs.PlacingMode;
        foreach (var p in Utilities.GetPlayers().Where(_plugin.Helper.IsRealPlayer))
            _plugin.Helper.Chat(p, "hdte.placing_mode", _plugin.Eggs.PlacingMode ? "ON" : "OFF");
    }

    // ---------- players ----------

    private void ShowMyEggs(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        var h = _plugin.Helper;

        var data = _plugin.Players.Get(player.SteamID);
        if (data == null)
        {
            h.Chat(player, "hdte.player_not_found");
            return;
        }

        var total = data.TotalEggs + data.KillEggs;
        h.Lines(player, "hdte.my_eggs", data.TotalEggs, data.KillEggs, total, data.Eggs.Count, data.Map, data.RandomEggs);
    }

    // Leaderboards are cached in memory (loaded asynchronously on map start), so no await is needed here
    // and everything stays on the game thread.
    private void ShowTop(CCSPlayerController? player, string headerKey, Func<Dictionary<string, int>> getTop)
    {
        if (player == null || !player.IsValid) return;
        var h = _plugin.Helper;

        var top = getTop();
        h.Lines(player, headerKey);

        if (top.Count == 0)
        {
            h.Chat(player, "hdte.no_eggs_found");
            return;
        }

        var i = 0;
        foreach (var (name, count) in top)
        {
            i++;
            player.PrintToChat(h.T(player, "hdte.top_list_player", i, name, count));
        }
    }
}
