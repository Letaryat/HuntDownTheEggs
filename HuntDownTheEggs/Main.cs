using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;


namespace HuntDownTheEggs;
public partial class HuntDownTheEggs : BasePlugin, IPluginConfig<PresentsConfig>
{
    public override string ModuleName => "Hunt Down The Eggs";
    public override string ModuleAuthor => "Letaryat";
    public override string ModuleDescription => "https://github.com/Letaryat/";
    public override string ModuleVersion => "1.0.0";
    public Dictionary<uint, CDynamicProp> Presents = new Dictionary<uint, CDynamicProp>();
    public string? mapName;
    public static HuntDownTheEggs Instance = new();
    List<EggsData> presents = new();
    public string? filePath;

    public bool placingMode = false;

    public readonly ConcurrentDictionary<ulong, PlayerEggs> Players = new();
    public PresentsConfig Config { get; set; }
    public string DBConnection = string.Empty;
    private static readonly object fileLock = new();

    public Dictionary<string, int> _TopEggsCache = new Dictionary<string, int>();
    public Dictionary<string, int> _TopKillEggsCache = new Dictionary<string, int>();

    public override void Load(bool hotReload)
    {
        filePath = Path.Combine(ModuleDirectory, "maps", $"{Server.MapName}.json");
        mapName = Server.MapName;

        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventRoundOfficiallyEnded>(OnRoundEnd, HookMode.Pre);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

        RegisterListener<Listeners.OnMapStart>((map) =>
        {
            Players.Clear();
            mapName = Server.MapName;
            filePath = Path.Combine(ModuleDirectory, "maps", $"{Server.MapName}.json");
            GenerateFile();
            SerializeJsonFromMap();
            _ = GetTop(map);
            DebugMode($"On map start: {mapName}");

        });
        RegisterListener<Listeners.OnServerPrecacheResources>((manifest) =>
        {
            manifest.AddResource(Config.EggModel);
        });

        AddCommandListener("changelevel", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("map", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("host_workshop_map", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("ds_workshop_changelevel", ListenerChangeLevel, HookMode.Pre);
        HookEntityOutput("trigger_multiple", "OnStartTouch", trigger_multiple, HookMode.Pre);

        Logger.LogInformation($"HuntDownTheEggs has been loaded! - FilePath: {filePath}");
    }
    public override void Unload(bool hotReload)
    {
        Logger.LogInformation("HuntDownTheEggs has been unloaded!");
        _ = SaveAllEggs();
    }
    public void OnConfigParsed(PresentsConfig config)
    {
        Config = config;
        ConnectionDB();
    }

    public async Task OnClientAuthorizedAsync(ulong steamid)
    {
        DebugMode($"Client authorization: {steamid}");
        var user = await GetPlayerEggs(steamid, mapName!);
        if (user == null)
        {
            Players[steamid] = new PlayerEggs
            {
                steamid = steamid,
                map = mapName!,
                eggs = new(),
                killeggs = 0
            };
        }
        else
        {
            Players[steamid] = new PlayerEggs
            {
                steamid = user!.steamid,
                map = user.map,
                eggs = user.eggs,
                killeggs = user.killeggs,
                totalEggs = user.totalEggs
            };
        }

    }

}
