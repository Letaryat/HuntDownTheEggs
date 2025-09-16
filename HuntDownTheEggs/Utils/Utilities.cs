using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;
using CS2TraceRay.Class;
using CS2TraceRay.Enum;
using CS2TraceRay.Struct;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using System.Numerics;

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


        public static Vector Test()
        {
            // 1. Pobierz wszystkie dostępne spawny
            var spawns = new List<Vector>();

            var dmSpawns = Utilities.FindAllEntitiesByDesignerName<CInfoDeathmatchSpawn>("info_deathmatch_spawn")
                .Where(s => s?.AbsOrigin != null)
                .Select(s => new Vector(s.AbsOrigin!.X, s.AbsOrigin.Y, s.AbsOrigin.Z));

            var tSpawns = Utilities.FindAllEntitiesByDesignerName<CInfoPlayerStart>("info_player_terrorist")
                .Where(s => s?.AbsOrigin != null)
                .Select(s => new Vector(s.AbsOrigin!.X, s.AbsOrigin.Y, s.AbsOrigin.Z));

            var ctSpawns = Utilities.FindAllEntitiesByDesignerName<CInfoPlayerStart>("info_player_counterterrorist")
                .Where(s => s?.AbsOrigin != null)
                .Select(s => new Vector(s.AbsOrigin!.X, s.AbsOrigin.Y, s.AbsOrigin.Z));

            spawns.AddRange(dmSpawns);
            spawns.AddRange(tSpawns);
            spawns.AddRange(ctSpawns);

            if (spawns.Count == 0)
            {
                // fallback
                return new Vector(0, 0, 64);
            }

            // 2. Oblicz bounding box
            float minX = spawns.Min(v => v.X);
            float maxX = spawns.Max(v => v.X);
            float minY = spawns.Min(v => v.Y);
            float maxY = spawns.Max(v => v.Y);
            float maxZ = spawns.Max(v => v.Z) + 500; // start raytrace trochę nad najwyższym spawnem

            // 3. Losuj kandydatów
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var candidate = new Vector(
                    Random.Shared.NextSingle() * (maxX - minX) + minX,
                    Random.Shared.NextSingle() * (maxY - minY) + minY,
                    maxZ
                );

                var end = new Vector(candidate.X, candidate.Y, -10000); // raytrace w dół

                var ray = new Ray(
                    new Vector3(candidate.X, candidate.Y, candidate.Z),
                    new Vector3(end.X - candidate.X, end.Y - candidate.Y, end.Z - candidate.Z)
                );

                var filter = new CTraceFilter();
                var trace = TraceRay.TraceHull(candidate, end, filter, ray);

                if (trace.DidHit() && !trace.AllSolid)
                {
                    var groundPos = candidate + (end - candidate) * trace.Fraction;
                    return new Vector(groundPos.X, groundPos.Y, groundPos.Z + 5); // lekki offset
                }
            }

            Server.PrintToChatAll($"TEst: {spawns[Random.Shared.Next(spawns.Count)]}");

            // fallback
            return spawns[Random.Shared.Next(spawns.Count)];
        }


        /*
            public static Vector Test()
            {
                //var dmSpawns = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("info_deathmatch_spawn")
                //    .Where(s => s?.AbsOrigin != null)
                //    .ToList();


                Vector min = new(-2000, -2000, -100);
                Vector max = new(2000, 2000, 500);

                Vector randomPos = Vector.Zero;

                for (int attempt = 0; attempt < 20; attempt++)
                {
                    Vector candidate = new Vector(
                        Random.Shared.NextSingle() * (max.X - min.X) + min.X,
                        Random.Shared.NextSingle() * (max.Y - min.Y) + min.Y,
                        500 // start od góry mapy
                    );

                    //var spawn = dmSpawns[Random.Shared.Next(dmSpawns.Count)];

                    Vector end = new Vector(candidate.X, candidate.Y, -1000); // rzut w dół

                    Ray ray = new Ray(new Vector3(candidate.X, candidate.Y, candidate.Z), new Vector3(end.X, end.Y, end.Z));
                    CTraceFilter filter = new CTraceFilter();

                    CGameTrace trace = TraceRay.TraceHull(candidate, end, filter, ray);

                    if (!trace.AllSolid)
                    {
                        // Oblicz dokładny punkt trafienia
                        randomPos = candidate + (end - candidate) * trace.Fraction;
                        //randomPos.Z = spawn.AbsOrigin!.Z; // mały offset nad ziemią
                        break; // koniec pętli
                    }
                }

                return randomPos;
            }
            */

        public static Vector FindValidSpawnPosition()
        {
            Vector min = new Vector(-2000, -2000, -100);
            Vector max = new Vector(2000, 2000, 500);

            for (int attempt = 0; attempt < 50; attempt++) // Increased attempts
            {
                Vector candidate = new Vector(
                    Random.Shared.NextSingle() * (max.X - min.X) + min.X,
                    Random.Shared.NextSingle() * (max.Y - min.Y) + min.Y,
                    max.Z // Start from top and trace downward
                );

                // Check if position is valid
                if (IsPositionValid(candidate, max))
                {
                    return candidate;
                }
            }

            // Fallback: return center or handle failure
            return new Vector(0, 0, 100);
        }

        private static bool IsPositionValid(Vector position, Vector max)
        {
            // 1. Check for ground below
            Vector groundCheckStart = new Vector(position.X, position.Y, position.Z);
            Vector groundCheckEnd = new Vector(position.X, position.Y, position.Z);
            Ray groundRay = new Ray(new Vector3(groundCheckStart.X, groundCheckStart.Y, groundCheckStart.Z),
                                   new Vector3(groundCheckEnd.X, groundCheckEnd.Y, groundCheckEnd.Z));

            CTraceFilter filter = new CTraceFilter();
            CGameTrace groundTrace = TraceRay.TraceHull(groundCheckStart, groundCheckEnd, filter, groundRay);

            if (!groundTrace.DidHit())
            {
                return false; // No ground found
            }

            // 2. Adjust position to be on ground
            Vector groundPosition = new Vector(
                position.X,
                position.Y,
                groundTrace.EndPos.Z + 5.0f // Small offset above ground
            );

            // 3. Check for collisions at the spawn position (player-sized hull)
            Ray ray = new Ray(new Vector3(-16, -16, -0), new Vector3(16, 16, 72));

            Vector traceStart = new Vector(groundPosition.X, groundPosition.Y, groundPosition.Z);
            Vector traceEnd = new Vector(groundPosition.X, groundPosition.Y, groundPosition.Z);

            CTraceFilter collisionFilter = new CTraceFilter();
            CGameTrace collisionTrace = TraceRay.TraceHull(traceStart, traceEnd, collisionFilter, ray);

            if (collisionTrace.AllSolid)
            {
                return false; // Position is blocked
            }

            // 4. Optional: Check if position is in navmesh/playable area
            if (!IsInPlayableArea(groundPosition, max))
            {
                return false;
            }

            return true;
        }

        private static bool IsInPlayableArea(Vector position, Vector max)
        {
            // Implement navigation mesh check or area validation
            // This depends on your game's navigation system
            // For CS2, you might use NavMesh.GetNearestNavMeshPoint()

            // Temporary simple distance check from edges
            float safeMargin = 500f;
            if (Math.Abs(position.X) > max.X - safeMargin ||
                Math.Abs(position.Y) > max.Y - safeMargin)
            {
                return false;
            }

            return true;
        }




    }
}