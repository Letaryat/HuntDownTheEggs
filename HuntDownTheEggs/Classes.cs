using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace HuntDownTheEggs
{
    public partial class HuntDownTheEggs
    {
        public class EggsData
        {
            public int Id { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public string? modelColor { get; set; }
        }

        public class PlayerEggs
        {
            public required ulong steamid { get; set; }
            public string map {  get; set; }
            public List<int> eggs { get; set; }
            public int killeggs { get; set; }
            public int totalEggs { get; set; }
        }
        public class EggsTypeConfig
        {
            [JsonPropertyName("chance")]
            public float Chance { get; set; } = 20.0f;

            [JsonPropertyName("color")]
            public string Color { get; set; } = "white";

            [JsonPropertyName("rewards")]
            public Dictionary<string, string> Rewards { get; set; } = new();
        }


    }
}
