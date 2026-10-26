using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using HuntDownTheEggs.Api;
using HuntDownTheEggs.Extensions;
using Microsoft.Extensions.Logging;

namespace HuntDownTheEggs;

public sealed class HuntDownTheEggsPlugin : BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "Hunt Down The Eggs";
    public override string ModuleAuthor => "Letaryat";
    public override string ModuleDescription => "Spawn collectables that players gather.";
    public override string ModuleVersion => "1.4.1";

    public PluginConfig Config { get; set; } = new();

    public EventManager EventManager { get; private set; } = null!;
    public DatabaseManager Database { get; private set; } = null!;
    public PlayerManager Players { get; private set; } = null!;
    public EggManager Eggs { get; private set; } = null!;
    public new CommandManager CommandManager { get; private set; } = null!;
    public Helpers Helper { get; private set; } = null!;
    public HuntDownTheEggsApi Api { get; private set; } = null!;

    public string MapName { get; set; } = string.Empty;
    public bool WarmupRunning { get; set; }
    public bool AllowToSpawnEggs { get; set; } = true;

    public void OnConfigParsed(PluginConfig config) => Config = config;

    public override void Load(bool hotReload)
    {
        MapName = Server.MapName ?? string.Empty;

        Api = new HuntDownTheEggsApi(this);
        Capabilities.RegisterPluginCapability(HuntDownTheEggsCapability.Api, () => Api);

        Helper = new Helpers(this);
        Database = new DatabaseManager(this);
        Players = new PlayerManager(this);
        Eggs = new EggManager(this);
        EventManager = new EventManager(this);
        CommandManager = new CommandManager(this);

        Database.Initialize();
        EventManager.RegisterEvents();
        CommandManager.RegisterCommands();

        NavMesh.Init(this);

        if (hotReload && !string.IsNullOrEmpty(MapName))
        {
            Eggs.LoadEggsFromMap();
            Players.LoadTopLists(MapName);
            foreach (var p in Utilities.GetPlayers().Where(Helper.IsRealPlayer))
                Players.EnsureLoaded(p.SteamID, p.PlayerName);
        }

        Logger.LogInformation("HuntDownTheEggs {Version} (CounterStrikeSharp) loaded", ModuleVersion);
    }

    public override void Unload(bool hotReload)
    {
        try { Players.SaveAllBlocking(); }
        catch (Exception ex) { Logger.LogError(ex, "HuntDownTheEggs: error while saving players on unload"); }

        Logger.LogInformation("HuntDownTheEggs unloaded");
    }

    public void DebugLog(string message)
    {
        if (Config.Debug) Logger.LogInformation("[HDTE] {Message}", message);
    }
}
