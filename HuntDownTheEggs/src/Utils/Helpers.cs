using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using HuntDownTheEggs.Api;

namespace HuntDownTheEggs;

public sealed class Helpers(HuntDownTheEggsPlugin plugin)
{
    private readonly HuntDownTheEggsPlugin _plugin = plugin;

    public bool IsRealPlayer(CCSPlayerController? p) =>
        p != null && p.IsValid && !p.IsBot && !p.IsHLTV;

    // ---------- localization ----------
    // Uses the player's own language (like GetPlayerLocalizer in Swiftly).
    // If ForPlayer does not exist in your CSS build, swap the body for: _plugin.Localizer[key, args]
    public string T(CCSPlayerController player, string key, params object[] args) =>
        _plugin.Localizer.ForPlayer(player, key, args);

    public string Prefix(CCSPlayerController player) => T(player, "hdte.prefix");

    /// <summary>Prefix + translated message in one chat line.</summary>
    public void Chat(CCSPlayerController player, string key, params object[] args) =>
        player.PrintToChat(Prefix(player) + T(player, key, args));

    /// <summary>Translated text that contains '\n' - printed as one chat line per row.</summary>
    public void Lines(CCSPlayerController player, string key, params object[] args) =>
        SendLines(player, T(player, key, args));

    public void SendLines(CCSPlayerController player, string text)
    {
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            player.PrintToChat(line);
    }

    // ---------- misc ----------

    public void NotifyCollected(CCSPlayerController player, EggKind kind, int? eggId)
    {
        var apiKind = kind switch
        {
            EggKind.Placed => EggCollectionKind.Placed,
            EggKind.Kill => EggCollectionKind.Kill,
            EggKind.Random => EggCollectionKind.Random,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        _plugin.Api.NotifyEggCollected(player.SteamID, player.PlayerName, eggId, _plugin.MapName, apiKind);
    }

    public string ReplacePlayerParameters(string input, CCSPlayerController p)
    {
        var id64 = p.SteamID;
        const ulong baseId = 76561197960265728UL;
        var accountId = id64 - baseId;
        var steam2 = $"STEAM_0:{accountId & 1}:{accountId >> 1}";
        var steam3 = $"[U:1:{accountId}]";

        return input
            .Replace("{USERID}", (p.UserId ?? -1).ToString())
            .Replace("{STEAMID}", steam2)
            .Replace("{STEAMID3}", steam3)
            .Replace("{STEAMID64}", id64.ToString())
            .Replace("{NAME}", p.PlayerName)
            .Replace("{SLOT}", p.Slot.ToString());
    }

    public bool IsWarmup()
    {
        var rules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules")
            .FirstOrDefault()?.GameRules;
        return rules?.WarmupPeriod ?? false;
    }

    public float PlayerPercentage()
    {
        var all = Utilities.GetPlayers().Where(p => p != null && p.IsValid).ToList();
        var humans = all.Count(p => !p.IsBot && !p.IsHLTV);
        var fakes = all.Count(p => p.IsBot || p.IsHLTV);
        var slots = Server.MaxPlayers - fakes;
        return slots <= 0 ? 100f : (float)humans / slots * 100f;
    }
}
