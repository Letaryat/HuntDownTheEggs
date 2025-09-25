using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;
using HuntDownTheEggs.Models;

namespace HuntDownTheEggs.Core
{
    public class PluginConfig : BasePluginConfig
    {
        public DatabaseSetup DBSetup { get; set; } = new();
        public EggSetup EggSetup { get; set; } = new();
        public ModesSetup ModesSetup { get; set; } = new();
        public PrizeSetup PrizeSetup { get; set; } = new();
        public CommandsSetup CommandsSetup { get; set; } = new();

        [JsonPropertyName("Debug")]
        public bool Debug { get; set; } = true;

        [JsonPropertyName("DebugNavMesh")]
        public bool DebugNavMesh { get; set; } = false;
    }
}