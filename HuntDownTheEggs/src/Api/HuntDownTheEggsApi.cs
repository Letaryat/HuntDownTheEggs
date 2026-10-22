using HuntDownTheEggs.Api;

namespace HuntDownTheEggs;

public sealed class HuntDownTheEggsApi : IHuntDownTheEggsApi
{
    private readonly HuntDownTheEggsPlugin _plugin;

    public HuntDownTheEggsApi(HuntDownTheEggsPlugin plugin) => _plugin = plugin;

    public int GetEggCount(ulong steamId) => _plugin.Players.Get(steamId)?.Eggs.Count ?? 0;

    public bool HasFoundEgg(ulong steamId, int eggId) => _plugin.Players.Get(steamId)?.Eggs.Contains(eggId) ?? false;

    public int GetKillEggCount(ulong steamId) => _plugin.Players.Get(steamId)?.KillEggs ?? 0;

    public int GetRandomEggCount(ulong steamId) => _plugin.Players.Get(steamId)?.RandomEggs ?? 0;

    public bool AllowToSpawnEggs(bool allow) => _plugin.AllowToSpawnEggs = allow;

    public event Action<EggCollectedEvent>? EggCollected;

    public void NotifyEggCollected(ulong steamId, string playerName, int? eggId, string map, EggCollectionKind kind)
    {
        var args = new EggCollectedEvent(steamId, playerName, eggId, map, kind);

        var handlers = EggCollected;
        if (handlers is null) return;

        foreach (Action<EggCollectedEvent> handler in handlers.GetInvocationList())
        {
            try { handler(args); }
            catch (Exception ex) { _plugin.DebugLog($"EggCollected subscriber failed: {ex.Message}"); }
        }
    }

    private sealed record PrizeProvider(
        Guid Id,
        string PresentType,
        string ProviderId,
        IReadOnlyDictionary<string, string> Rewards,
        EggPrizeRewardHandler Handler);

    private readonly Dictionary<Guid, PrizeProvider> _providers = new();

    public Guid RegisterPrizeProvider(string presentType, string providerId, IReadOnlyDictionary<string, string> rewards, EggPrizeRewardHandler handler)
    {
        if (string.IsNullOrWhiteSpace(presentType))
            throw new ArgumentException("Present type cannot be empty.");
        if (string.IsNullOrWhiteSpace(providerId))
            throw new ArgumentException("Provider ID cannot be empty.");
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));
        if (!_plugin.Config.PrizeSetup.PresentTypes.ContainsKey(presentType))
            throw new ArgumentException($"Present type '{presentType}' does not exist.");

        var id = Guid.NewGuid();

        _providers[id] = new PrizeProvider(
            id,
            presentType,
            providerId,
            new Dictionary<string, string>(rewards, StringComparer.OrdinalIgnoreCase),
            handler);

        _plugin.DebugLog($"Prize provider '{providerId}' registered for '{presentType}'.");
        return id;
    }

    public void UnregisterPrizeProvider(Guid registrationId)
    {
        if (_providers.Remove(registrationId, out var provider))
            _plugin.DebugLog($"Prize provider '{provider.ProviderId}' unregistered.");
    }

    public IReadOnlyList<RegisteredPrizeReward> GetPrizeRewards(string presentType)
    {
        return _providers.Values
            .Where(p => string.Equals(p.PresentType, presentType, StringComparison.OrdinalIgnoreCase))
            .SelectMany(p => p.Rewards.Select(r =>
                new RegisteredPrizeReward(p.Id, p.PresentType, p.ProviderId, r.Key, r.Value)))
            .ToList();
    }

    public bool TryGivePrize(RegisteredPrizeReward reward, ulong steamId, string playerName)
    {
        if (!_providers.TryGetValue(reward.RegistrationId, out var provider))
            return false;

        if (!string.Equals(provider.PresentType, reward.PresentType, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!provider.Rewards.TryGetValue(reward.RewardName, out var registeredValue)
            || registeredValue != reward.RewardValue)
            return false;

        try
        {
            return provider.Handler(steamId, playerName, reward.RewardName, reward.RewardValue);
        }
        catch (Exception ex)
        {
            _plugin.DebugLog($"Prize provider '{provider.ProviderId}' failed: {ex}");
            return false;
        }
    }
}
