using System.Text.Json.Serialization;

namespace HuntDownTheEggs.Models
{
    public class EggData
    {
        public int Id { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public string? ModelColor { get; set; }
    }

    public class EggTypeConfig
    {
        [JsonPropertyName("chance")]
        public float Chance { get; set; } = 20.0f;

        [JsonPropertyName("color")]
        public string Color { get; set; } = "white";

        [JsonPropertyName("rewards")]
        public Dictionary<string, string> Rewards { get; set; } = [];
    }
}