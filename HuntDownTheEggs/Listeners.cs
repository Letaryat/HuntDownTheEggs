using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;


namespace HuntDownTheEggs
{
    public partial class HuntDownTheEggs
    {
        public HookResult ListenerChangeLevel(CCSPlayerController? player, CommandInfo info)
        {
            DebugMode("Changing map. Clearing cache!");
            
            Presents.Clear();
            presents.Clear();
            
            return HookResult.Continue;
        }

    }
}
