using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;
using static HuntDownTheEggs.HuntDownTheEggs;


namespace HuntDownTheEggs
{
    public class PresentsConfig : BasePluginConfig
    {
        [JsonPropertyName("EggRootFlag")] public string EggRootFlag { get; set; } = "@egg/root";
        [JsonPropertyName("EggModel")] public string EggModel { get; set; } = "models/chicken/chicken.vmdl";
        [JsonPropertyName("EggAnimation")] public string EggAnimation { get; set; } = "challenge_coin_idle";
        [JsonPropertyName("EggModelHeight")] public float EggModelHeight { get; set; } = 0;
        [JsonPropertyName("EggModelScale")] public float EggModelScale { get; set; } = 1;
        [JsonPropertyName("DBHost")] public string DBHost { get; set; } = "localhost";
        [JsonPropertyName("DBPort")] public uint DBPort { get; set; } = 3306;
        [JsonPropertyName("DBUsername")] public string DBUsername { get; set; } = "root";
        [JsonPropertyName("DBName")] public string DBName { get; set; } = "db_";
        [JsonPropertyName("DBPassword")] public string DBPassword { get; set; } = "123";
        [JsonPropertyName("DeathMode")] public bool DeathMode { get; set; } = true;
        [JsonPropertyName("SearchMode")] public bool SearchMode { get; set; } = true;
        [JsonPropertyName("SpawnDeathEggOnVictim")] public bool SpawnDeathEggOnVictim { get; set; } = true;
        [JsonPropertyName("ChanceToSpawn")] public float ChanceToSpawn { get; set; } = 100.0f;
        [JsonPropertyName("RemoveOnFind")] public bool RemoveOnFind { get; set; } = true;
        [JsonPropertyName("SpawnPlacedEggsOnce")] public bool SpawnPlacedEggsOnce { get; set; } = true;
        [JsonPropertyName("ReceivePrize")] public bool ReceivePrize { get; set; } = true;

        [JsonPropertyName("PresentTypes")]
        public Dictionary<string, EggsTypeConfig> EggsTypes { get; set; } = new();

        [JsonPropertyName("Glowing")] public bool Glowing { get; set; } = false;
        [JsonPropertyName("GlowingColor")] public string GlowingColor { get; set; } = "Green";
        [JsonPropertyName("GlowingRange")] public int GlowingRange { get; set; } = 1500;

        [JsonPropertyName("Debug")] public bool Debug { get; set; } = true;
    }
}
