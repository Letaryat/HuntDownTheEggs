using CounterStrikeSharp.API.Core;

namespace HuntDownTheEggs;

public enum EggKind { Placed, Kill, Random }

public sealed class EggData
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public string? ModelColor { get; set; }
}

public sealed class EggInstance
{
    public required CDynamicProp Prop { get; init; }
    public CTriggerMultiple? Trigger { get; set; }
    public CDynamicProp? Glow { get; set; }
    public required EggKind Kind { get; init; }
    public int EggId { get; init; } = -1;      // only for placed eggs
    public ulong OwnerSteamId { get; init; }   // only for kill eggs
    public bool Collected { get; set; }
}

public sealed class PlayerData
{
    public ulong SteamId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string Map { get; set; } = string.Empty;
    public List<int> Eggs { get; set; } = [];
    public int KillEggs { get; set; }
    public int RandomEggs { get; set; }
    public int TotalEggs { get; set; }
}
