using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;

namespace HuntDownTheEggs.Core
{
    public class HuntDownTheEggsPlugin : BasePlugin, IPluginConfig<PluginConfig>
    {
        public override string ModuleName => "Hunt Down The Eggs";
        public override string ModuleAuthor => "Letaryat & Mesharsky";
        public override string ModuleDescription => "https://github.com/Letaryat/ & https://github.com/Mesharsky/";
        public override string ModuleVersion => "1.2";

        // Managers
        public EggManager? EggManager { get; private set; }
        public PlayerManager? PlayerManager { get; private set; }
        public DatabaseManager? DatabaseManager { get; private set; }
        public new CommandManager? CommandManager { get; private set; }
        public EventManager? EventManager { get; private set; }

        // Configuration
        public required PluginConfig Config { get; set; }
        
        // Plugin instance for global access
        public static HuntDownTheEggsPlugin? Instance { get; private set; }

        public override void Load(bool hotReload)
        {
            Instance = this;
            
            // Initialize managers
            DatabaseManager = new DatabaseManager(this);
            PlayerManager = new PlayerManager(this);
            EggManager = new EggManager(this);
            CommandManager = new CommandManager(this);
            EventManager = new EventManager(this);
            
            // Register event handlers through the event manager
            EventManager.RegisterEvents();
            
            // Register commands through the command manager
            CommandManager.RegisterCommands();

            // Initialize database connection
            DatabaseManager?.InitializeConnection();
            
            Logger.LogInformation($"HuntDownTheEggs v{ModuleVersion} has been loaded!");
        }

        public override void Unload(bool hotReload)
        {
            // Save all player egg data before unloading
            Task.Run(PlayerManager!.SaveAllPlayersAsync).Wait();
            
            Logger.LogInformation("HuntDownTheEggs has been unloaded!");
        }

        public void OnConfigParsed(PluginConfig config)
        {
            Config = config;
        }
        
        public void DebugLog(string message)
        {
            if (Config.Debug)
            {
                Logger.LogInformation(message);
            }
        }
    }
}