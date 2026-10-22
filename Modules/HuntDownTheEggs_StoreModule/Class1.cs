using System.Globalization;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using HuntDownTheEggs.Api;
using Microsoft.Extensions.Logging;
using StoreApi;

namespace HuntDownTheEggs_StorePoints;

public sealed class HuntDownTheEggs_StorePoints : BasePlugin
{
    public override string ModuleName => "HuntDownTheEggs - CS2 Store Points";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "Letaryat";
    public override string ModuleDescription =>
        "Adds CS2 Store credits as HuntDownTheEggs prizes.";

    private IStoreApi? _storeApi;
    private IHuntDownTheEggsApi? _eggApi;

    private ShopPointsConfig _config = new();
    private readonly List<Guid> _registrations = new();

    // MUSI odpowiadać identyfikatorowi capability z głównego pluginu.
    private const string EggApiCapabilityName = "HuntDownTheEggs.Api.v1";

    public override void Load(bool hotReload)
    {
        LoadConfig();
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _storeApi = IStoreApi.Capability.Get();

        // Jeśli Twój interfejs IHuntDownTheEggsApi udostępnia
        // statyczne Capability, możesz zamiast tego użyć:
        // _eggApi = IHuntDownTheEggsApi.Capability.Get();

        var capability = new PluginCapability<IHuntDownTheEggsApi>(
            EggApiCapabilityName);

        _eggApi = capability.Get();

        if (!_config.Enabled)
        {
            Logger.LogInformation(
                "HuntDownTheEggs StorePoints integration is disabled.");
            return;
        }

        if (_storeApi is null)
        {
            Logger.LogError(
                "CS2 Store API not found. Make sure CS2 Store is loaded.");
            return;
        }

        if (_eggApi is null)
        {
            Logger.LogError(
                "HuntDownTheEggs API not found. Check the capability name.");
            return;
        }

        foreach (var (presentType, rewards) in _config.Rewards)
        {
            if (rewards.Count == 0)
                continue;

            try
            {
                var registrationId = _eggApi.RegisterPrizeProvider(
                    presentType,
                    _config.ProviderId,
                    rewards,
                    GiveReward);

                _registrations.Add(registrationId);

                Logger.LogInformation(
                    "Registered {Count} CS2 Store rewards for present type '{Type}'.",
                    rewards.Count,
                    presentType);
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    ex,
                    "Failed to register CS2 Store rewards for '{Type}'.",
                    presentType);
            }
        }
    }

    private bool GiveReward(
        ulong steamId,
        string playerName,
        string rewardName,
        string rewardValue)
    {
        if (_storeApi is null)
            return false;

        if (!int.TryParse(
                rewardValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var credits)
            || credits <= 0)
        {
            Logger.LogWarning(
                "Invalid credit amount '{Value}' for reward '{Reward}'.",
                rewardValue,
                rewardName);

            return false;
        }

        var player = Utilities.GetPlayers().FirstOrDefault(p =>
            p.IsValid && p.SteamID == steamId);

        if (player is null)
        {
            Logger.LogWarning(
                "Could not give reward '{Reward}' to {Player}: player is not online.",
                rewardName,
                playerName);

            return false;
        }

        _storeApi.GivePlayerCredits(player, credits);

        Logger.LogInformation(
            "Gave {Credits} CS2 Store credits to {Player} for reward '{Reward}'.",
            credits,
            playerName,
            rewardName);

        return true;
    }

    public override void Unload(bool hotReload)
    {
        if (_eggApi is not null)
        {
            foreach (var registrationId in _registrations)
                _eggApi.UnregisterPrizeProvider(registrationId);
        }

        _registrations.Clear();
    }

    private void LoadConfig()
    {
        var configPath = Path.Combine(ModuleDirectory, "ShopPoints.json");

        try
        {
            if (!File.Exists(configPath))
            {
                _config = new ShopPointsConfig();

                var json = JsonSerializer.Serialize(
                    _config,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(configPath, json);
                Logger.LogInformation("Created default config: {Path}", configPath);
                return;
            }

            var contents = File.ReadAllText(configPath);

            _config = JsonSerializer.Deserialize<ShopPointsConfig>(
                contents,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new ShopPointsConfig();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load config: {Path}", configPath);
            _config = new ShopPointsConfig();
        }
    }
}

public sealed class ShopPointsConfig
{
    public bool Enabled { get; set; } = true;

    public string ProviderId { get; set; } = "StorePoints";

    public Dictionary<string, Dictionary<string, string>> Rewards { get; set; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["rare"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["100 Store Credits"] = "100",
                ["500 Store Credits"] = "500"
            },
            ["common"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["1000 Store Credits"] = "1000"
            }
        };
}