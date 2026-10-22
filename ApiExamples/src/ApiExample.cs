using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using HuntDownTheEggs.Api;
using Microsoft.Extensions.Logging;

namespace HuntDownTheEggs.ApiExample;

/// <summary>
/// Minimal consumer of the HuntDownTheEggs API:
///  - listens to EggCollected
///  - registers an extra prize in the "common" present type (handled by this plugin, not by a console command)
/// </summary>
public class ApiExamplePlugin : BasePlugin
{
    public override string ModuleName => "HuntDownTheEggs ApiExample";
    public override string ModuleVersion => "1.0.0";

    private IHuntDownTheEggsApi? _api;
    private Guid _registration;

    // OnAllPluginsLoaded: the capability is only available once HuntDownTheEggs_CSS itself has loaded.
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = HuntDownTheEggsCapability.Api.Get();
        if (_api == null)
        {
            Logger.LogWarning("HuntDownTheEggs API not found - is HuntDownTheEggs_CSS installed?");
            return;
        }

        _api.EggCollected += OnEggCollected;

        // "common" must exist under PrizeSetup.PresentTypes in the HuntDownTheEggs config.
        _registration = _api.RegisterPrizeProvider(
            "common",
            "ApiExample",
            new Dictionary<string, string> { ["Bonus from API"] = "100" },
            GiveReward);
    }

    public override void Unload(bool hotReload)
    {
        if (_api == null) return;
        _api.EggCollected -= OnEggCollected;
        _api.UnregisterPrizeProvider(_registration);
    }

    private void OnEggCollected(EggCollectedEvent e)
    {
        Logger.LogInformation("{Name} collected a {Kind} egg on {Map} (eggs found: {Count})",
            e.PlayerName, e.Kind, e.Map, _api?.GetEggCount(e.SteamId));
    }

    private bool GiveReward(ulong steamId, string playerName, string rewardName, string rewardValue)
    {
        // Runs on the game thread (the event/prize path is raised from game hooks) - safe to touch players.
        var player = Utilities.GetPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == steamId);
        if (player == null) return false;

        player.PrintToChat($"[ApiExample] {rewardName}: +{rewardValue}");
        return true;
    }
}
