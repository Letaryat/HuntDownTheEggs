using System.Text.Json.Serialization;

namespace HuntDownTheEggs.Models
{
    public class EggSetup
    {
        [JsonPropertyName("EggRootFlag")]
        public string EggRootFlag { get; set; } = "@egg/root";

        [JsonPropertyName("EggModel")]
        public string EggModel { get; set; } = "models/chicken/chicken.vmdl";

        [JsonPropertyName("TriggerModel")]
        public string TriggerModel { get; set; } = "models/chicken/chicken.vmdl";

        [JsonPropertyName("EggAnimation")]
        public string EggAnimation { get; set; } = "challenge_coin_idle";

        [JsonPropertyName("EggCollectAnimation")]
        public string EggCollectAnimation { get; set; } = "challenge_coin_collect";

        [JsonPropertyName("EggModelHeight")]
        public float EggModelHeight { get; set; } = 0;

        [JsonPropertyName("EggModelScale")]
        public float EggModelScale { get; set; } = 1;

        [JsonPropertyName("Glowing")]
        public bool Glowing { get; set; } = true;

        [JsonPropertyName("GlowingColor")]
        public string GlowingColor { get; set; } = "Red";

        [JsonPropertyName("GlowingRange")]
        public int GlowingRange { get; set; } = 1500;

        [JsonPropertyName("HidePickedEggsForPlayer")]
        public bool HidePickedEggsPlayer { get; set; } = false;

        [JsonPropertyName("KillEggOnlyForKiller")]
        public bool ShowKillEggOnlyForKiller { get; set; } = false;


    }

    public class DatabaseSetup
    {
        [JsonPropertyName("DBHost")]
        public string DBHost { get; set; } = "localhost";

        [JsonPropertyName("DBPort")]
        public uint DBPort { get; set; } = 3306;

        [JsonPropertyName("DBUsername")]
        public string DBUsername { get; set; } = "root";

        [JsonPropertyName("DBName")]
        public string DBName { get; set; } = "db_";

        [JsonPropertyName("DBPassword")]
        public string DBPassword { get; set; } = "123";
    }

    public class ModesSetup
    {
        [JsonPropertyName("SpawnEggsOnWarmup")]
        public bool SpawnEggsOnWarmup { get; set; } = false;

        [JsonPropertyName("DeathMode")]
        public bool DeathMode { get; set; } = true;

        [JsonPropertyName("SearchMode")]
        public bool SearchMode { get; set; } = true;

        [JsonPropertyName("ShootEggMode")]
        public bool ShootEggMode { get; set; } = true;

        [JsonPropertyName("ShootEggHealth")]
        public int ShootEggHealth { get; set; } = 100;

        [JsonPropertyName("SpawnDeathEggOnVictim")]
        public bool SpawnDeathEggOnVictim { get; set; } = true;

        [JsonPropertyName("ChanceToSpawn")]
        public float ChanceToSpawn { get; set; } = 100.0f;

        [JsonPropertyName("RemoveOnFind")]
        public bool RemoveOnFind { get; set; } = false;

        [JsonPropertyName("SpawnPlacedEggsOnce")]
        public bool SpawnPlacedEggsOnce { get; set; } = false;

        [JsonPropertyName("SpawnRandomEggs")]
        public bool SpawnRandomEggs { get; set; } = false;
        [JsonPropertyName("SpawnRandomEggsOnWarmup")]
        public bool SpawnRandomEggsOnWarmup { get; set; } = false;

        [JsonPropertyName("MinPlayersToSpawnRandomEggs")]
        public int MinPlayersToSpawnRandomEggs { get; set; } = 1;

        [JsonPropertyName("NumberOfRandomEggs")]
        public int NumberOfRandomEggs { get; set; } = 20;

        [JsonPropertyName("SpawnRandomEggsByPercentageOfPlayers")]
        public bool SpawnRandomEggsByPercentageOfPlayers { get; set; } = true;
    }

    public class PrizeSetup
    {
        [JsonPropertyName("ReceivePrize")]
        public bool ReceivePrize { get; set; } = true;

        [JsonPropertyName("PresentTypes")]
        public Dictionary<string, EggTypeConfig> EggsTypes { get; set; } = [];
    }

    public class CommandsSetup
    {
        public bool EnableCommands { get; set; } = true;

        [JsonPropertyName("PlaceEggCommands")]
        public List<string> PlaceEggCommands { get; set; } = ["css_eggplace"];
        [JsonPropertyName("TeleportToEggCommands")]
        public List<string> TeleportToEggCommands { get; set; } = ["css_tpegg"];
        [JsonPropertyName("RemoveEggCommands")]
        public List<string> RemoveEggCommands { get; set; } = ["css_removeegg"];
        [JsonPropertyName("ReloadEggsCommands")]
        public List<string> ReloadEggsCommands { get; set; } = ["css_reloadeggs"];
        [JsonPropertyName("PlacingModeCommands")]
        public List<string> PlacingModeCommands { get; set; } = ["css_placingMode"];
        [JsonPropertyName("ShowMyEggsCommands")]
        public List<string> ShowMyEggsCommands { get; set; } = ["css_myeggs"];
        [JsonPropertyName("ShowTopEggsCommands")]
        public List<string> ShowTopEggsCommands { get; set; } = ["css_topeggs"];
        [JsonPropertyName("ShowTopKillEggsCommands")]
        public List<string> ShowTopKillEggsCommands { get; set; } = ["css_topkilleggs"];
        [JsonPropertyName("ShowTopRandomEggsCommands")]
        public List<string> ShowTopRandomEggsCommands { get; set; } = ["css_toprandomeggs"];
    }


}