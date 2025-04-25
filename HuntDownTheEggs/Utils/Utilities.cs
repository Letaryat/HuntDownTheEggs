using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using System.Drawing;

namespace HuntDownTheEggs.Utils
{
    public static class PluginUtilities
    {
        /// <summary>
        /// Replaces player-specific parameters in a command string
        /// </summary>
        public static string ReplacePlayerParameters(string input, CCSPlayerController controller)
        {
            return input
                .Replace("{USERID}", controller.UserId.ToString())
                .Replace("{STEAMID}", controller.AuthorizedSteamID!.SteamId2.ToString())
                .Replace("{STEAMID3}", controller.AuthorizedSteamID!.SteamId3.ToString())
                .Replace("{STEAMID64}", controller.AuthorizedSteamID!.SteamId64.ToString())
                .Replace("{NAME}", controller.PlayerName)
                .Replace("{SLOT}", controller.Slot.ToString());
        }

        /// <summary>
        /// Replaces newlines for chat messages
        /// </summary>
        public static string ReplaceMessageNewlines(string input)
        {
            return input.Replace("\n", "\u2029");
        }

        /// <summary>
        /// Applies a glow effect to an entity
        /// </summary>
        public static void SetGlowOnEntity(CBaseEntity? entity, Color glowColor, int range)
        {
            if (entity == null || !entity.IsValid)
                return;

            CDynamicProp glow = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic")!;
            glow.Spawnflags = 256;
            glow.Render = Color.Transparent;
            glow.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags = (uint)(glow.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags & ~(1 << 2));
            glow.SetModel(entity.CBodyComponent!.SceneNode!.GetSkeletonInstance().ModelState.ModelName);
            glow.DispatchSpawn();
            
            glow.Glow.GlowColorOverride = glowColor;
            glow.Glow.GlowRange = range;
            glow.Glow.GlowRangeMin = 0;
            glow.Glow.GlowTeam = -1; // -1 = Both, 2 = T, 3 = CT
            glow.Glow.GlowType = 3;

            glow.Teleport(entity.AbsOrigin, entity.AbsRotation, entity.AbsVelocity);
            glow.AcceptInput("SetParent", entity, glow, "!activator");
        }
    }
}