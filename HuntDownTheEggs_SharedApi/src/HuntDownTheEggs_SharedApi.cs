using CounterStrikeSharp.API.Core.Capabilities;

namespace HuntDownTheEggs.Api;

public enum EggCollectionKind
{
    Placed,
    Kill,
    Random
}

public sealed record EggCollectedEvent(
    ulong SteamId,
    string PlayerName,
    int? EggId,
    string Map,
    EggCollectionKind Kind
);

public delegate bool EggPrizeRewardHandler(
    ulong steamId,
    string playerName,
    string rewardName,
    string rewardValue
);

public sealed record RegisteredPrizeReward(
    Guid RegistrationId,
    string PresentType,
    string ProviderId,
    string RewardName,
    string RewardValue
);

public interface IHuntDownTheEggsApi
{
    int GetEggCount(ulong steamId);

    bool HasFoundEgg(ulong steamId, int eggId);

    int GetKillEggCount(ulong steamId);

    int GetRandomEggCount(ulong steamId);

    bool AllowToSpawnEggs(bool allow);

    event Action<EggCollectedEvent>? EggCollected;

    Guid RegisterPrizeProvider(string presentType, string providerId, IReadOnlyDictionary<string, string> rewards, EggPrizeRewardHandler handler);

    void UnregisterPrizeProvider(Guid registrationId);

    IReadOnlyList<RegisteredPrizeReward> GetPrizeRewards(string presentType);

    bool TryGivePrize(RegisteredPrizeReward reward, ulong steamId, string playerName);
}
public static class HuntDownTheEggsCapability
{
    public static PluginCapability<IHuntDownTheEggsApi> Api { get; } = new("huntdowntheeggs:api.v1");
}
