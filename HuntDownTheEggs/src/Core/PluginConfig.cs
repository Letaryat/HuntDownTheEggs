using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace HuntDownTheEggs;

public sealed class PluginConfig : BasePluginConfig
{
    public DatabaseSetup DBSetup { get; set; } = new();
    public EggSetup EggSetup { get; set; } = new();
    public ModesSetup ModesSetup { get; set; } = new();
    public PrizeSetup PrizeSetup { get; set; } = new();
    public CommandsSetup CommandsSetup { get; set; } = new();

    [JsonPropertyName("DebugNavMesh")]
    public bool DebugNavMesh { get; set; } = true;

    [JsonPropertyName("Debug")]
    public bool Debug { get; set; } = true;
}

public sealed class DatabaseSetup
{
    public string DBHost { get; set; } = "localhost";
    public uint DBPort { get; set; } = 3306;
    public string DBUsername { get; set; } = "root";
    public string DBName { get; set; } = "db_";
    public string DBPassword { get; set; } = "123";
}

public sealed class EggSetup
{
    // CounterStrikeSharp admin flag / permission, e.g. "@css/root" or "@egg/root"
    public string EggRootPermission { get; set; } = "@egg/root";
    public string EggModel { get; set; } = "models/huntdowntheeggs/pumpkin.vmdl";
    public string TriggerModel { get; set; } = "models/huntdowntheeggs/pumpkin.vmdl";
    public string EggAnimation { get; set; } = "huntdowntheEggs_rotateAction";
    public string EggCollectAnimation { get; set; } = "huntdowntheEggs_collect";
    public float EggModelHeight { get; set; } = 0;
    public float EggModelScale { get; set; } = 1;
    public bool Glowing { get; set; } = true;
    public string GlowingColor { get; set; } = "Red";
    public int GlowingRange { get; set; } = 1500;
    public bool HidePickedEggsForPlayer { get; set; } = false;
    public bool KillEggOnlyForKiller { get; set; } = false;
}

public sealed class ModesSetup
{
    public bool SpawnEggsOnWarmup { get; set; } = false;
    public bool DeathMode { get; set; } = true;
    public bool SearchMode { get; set; } = true;
    public bool PickUpMode { get; set; } = true;
    public bool ShootEggMode { get; set; } = true;
    public int ShootEggHealth { get; set; } = 100;
    public bool SpawnDeathEggOnVictim { get; set; } = true;
    public float ChanceToSpawn { get; set; } = 100.0f;
    public bool RemoveOnFind { get; set; } = false;
    public bool SpawnPlacedEggsOnce { get; set; } = false;
    public bool SpawnRandomEggs { get; set; } = false;
    public bool SpawnRandomEggsOnWarmup { get; set; } = false;
    public int MinPlayersToSpawnEggs {get; set; } = 1;
    public int MinPlayersToSpawnRandomEggs { get; set; } = 1;
    public int NumberOfRandomEggs { get; set; } = 20;
    public bool SpawnRandomEggsByPercentageOfPlayers { get; set; } = true;
}

public sealed class PrizeSetup
{
    public bool ReceivePrize { get; set; } = true;
    public Dictionary<string, EggTypeConfig> PresentTypes { get; set; } = new()
    {
        ["common"] = new EggTypeConfig
        {
            Chance = 30,
            Color = "green",
            Rewards = new() { ["Example reward"] = "say Common prize for {NAME}" }
        },
        ["rare"] = new EggTypeConfig
        {
            Chance = 70,
            Color = "red",
            Rewards = new() { ["Rare example reward"] = "say Rare prize for {NAME}" }
        }
    };
}

public sealed class EggTypeConfig
{
    public float Chance { get; set; } = 20.0f;
    public string Color { get; set; } = "white";
    public Dictionary<string, string> Rewards { get; set; } = new();
}

// Command names may be written with or without the "css_" prefix - the plugin adds it when missing.
public sealed class CommandsSetup
{
    public bool EnableCommands { get; set; } = true;
    public List<string> PlaceEggCommands { get; set; } = ["eggplace"];
    public List<string> TeleportToEggCommands { get; set; } = ["tpegg"];
    public List<string> RemoveEggCommands { get; set; } = ["removeegg"];
    public List<string> ReloadEggsCommands { get; set; } = ["reloadeggs"];
    public List<string> PlacingModeCommands { get; set; } = ["placingmode"];
    public List<string> ShowMyEggsCommands { get; set; } = ["myeggs"];
    public List<string> ShowTopPlacedEggsPerMapCommands { get; set; } = ["topplaced"];
    public List<string> ShowTopRandomEggsPerMapCommands { get; set; } = ["toprandom"];
    public List<string> ShowTopKillEggsPerMapCommands { get; set; } = ["topkills"];
    public List<string> ShowTopAllEggsPerMapCommands { get; set; } = ["topeggsmap"];
    public List<string> ShowTopPlacedEggsCommands { get; set; } = ["topplacedall"];
    public List<string> ShowTopRandomEggsCommands { get; set; } = ["toprandomall"];
    public List<string> ShowTopKillEggsCommands { get; set; } = ["topkillsall"];
    public List<string> ShowTopAllEggsCommands { get; set; } = ["topeggsall"];
}
