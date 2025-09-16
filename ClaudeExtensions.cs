using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Numerics;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using CS2TraceRay.Class;
using CS2TraceRay.Struct;
using CS2TraceRay.Enum;

namespace HuntDownTheEggs.Extensions
{
    public static class RandomPositionGenerator
    {
        // Try different trace methods to see which one works
        public static Vector? GetSimpleRandomPosition(int maxAttempts = 20)
        {
            Server.PrintToConsole("=== Starting GetSimpleRandomPosition ===");
            
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    // Generate random position
                    Vector start = new Vector(
                        Random.Shared.NextSingle() * 2000 - 1000, // -1000 to 1000 (smaller range)
                        Random.Shared.NextSingle() * 2000 - 1000,
                        500 // Start lower
                    );

                    Vector end = new Vector(start.X, start.Y, -500);

                    Server.PrintToConsole($"=== Attempt {attempt + 1} ===");
                    Server.PrintToConsole($"Start: ({start.X:F1},{start.Y:F1},{start.Z:F1})");
                    Server.PrintToConsole($"End: ({end.X:F1},{end.Y:F1},{end.Z:F1})");

                    // Try Method 1: TraceHull with direction vector
                    var pos1 = TryTraceHull(start, end);
                    if (pos1 != null)
                    {
                        Server.PrintToConsole($"TraceHull SUCCESS: ({pos1.X:F1},{pos1.Y:F1},{pos1.Z:F1})");
                        return pos1;
                    }

                    // Try Method 2: TraceRay (if available)
                    var pos2 = TryTraceRay(start, end);
                    if (pos2 != null)
                    {
                        Server.PrintToConsole($"TraceRay SUCCESS: ({pos2.X:F1},{pos2.Y:F1},{pos2.Z:F1})");
                        return pos2;
                    }

                    Server.PrintToConsole($"Attempt {attempt + 1} failed - no ground found");
                }
                catch (Exception ex)
                {
                    Server.PrintToConsole($"Attempt {attempt + 1} exception: {ex.Message}");
                }
            }

            Server.PrintToConsole("=== All attempts failed ===");
            return null;
        }

private static Vector? TryTraceHull(Vector start, Vector end)
{
    try
    {
        Server.PrintToConsole("Trying TraceHull method...");

        CTraceFilter filter = new CTraceFilter();

        // Ustaw kierunek i hull
        var direction = new Vector3(end.X - start.X, end.Y - start.Y, end.Z - start.Z);
        Ray ray = new Ray(
            new Vector3(start.X, start.Y, start.Z),
            direction,
            16f // hull size as a float (adjust as needed)
        );

        // maska kolizji (solid world geometry)
        CGameTrace trace = TraceRay.TraceHull(start, end, filter, ray);

        Server.PrintToConsole($"TraceHull - DidHit: {trace.DidHit()}, Fraction: {trace.Fraction:F3}, AllSolid: {trace.AllSolid}");

        if (trace.DidHit() && !trace.AllSolid && trace.Fraction < 1.0f)
        {
            // Oblicz punkt trafienia
            var hitPos = new Vector(
                start.X + (end.X - start.X) * trace.Fraction,
                start.Y + (end.Y - start.Y) * trace.Fraction,
                start.Z + (end.Z - start.Z) * trace.Fraction
            );

            // Lekki offset w górę, żeby nie zakopało
            hitPos.Z += 5.0f;
            return hitPos;
        }
    }
    catch (Exception ex)
    {
        Server.PrintToConsole($"TryTraceHull error: {ex.Message}");
    }

    return null;
}

        private static Vector? TryTraceRay(Vector start, Vector end)
        {
            try
            {
                Server.PrintToConsole("Trying TraceRay method...");

                // Check if TraceRay has a TraceRay method (not TraceHull)
                CTraceFilter filter = new CTraceFilter();

                // Try to use TraceRay if it exists
                // You might need to replace this with the correct method name
                // For example: TraceRay.TraceLine, TraceRay.TraceRay, etc.
                
                // This is just an example - adjust based on your CS2TraceRay library
                // CGameTrace trace = TraceRay.TraceLine(start, end, filter);
                
                Server.PrintToConsole("TraceRay method not implemented in this example");
                return null;
            }
            catch (Exception ex)
            {
                Server.PrintToConsole($"TryTraceRay error: {ex.Message}");
            }

            return null;
        }

        // Alternative: Use player positions as reference
        public static Vector? GetRandomNearPlayers(float radius = 800.0f)
        {
            Server.PrintToConsole("=== Trying GetRandomNearPlayers ===");
            
            try
            {
                var players = Utilities.GetPlayers()
                    .Where(p => p != null && p.IsValid && p.PawnIsAlive && p.PlayerPawn?.Value?.AbsOrigin != null)
                    .ToList();

                Server.PrintToConsole($"Found {players.Count} valid players");

                if (!players.Any())
                {
                    Server.PrintToConsole("No valid players found");
                    return null;
                }

                for (int attempt = 0; attempt < 10; attempt++)
                {
                    var randomPlayer = players[Random.Shared.Next(players.Count)];
                    var playerPos = randomPlayer.PlayerPawn!.Value!.AbsOrigin!;

                    float angle = Random.Shared.NextSingle() * 2.0f * MathF.PI;
                    float distance = Random.Shared.NextSingle() * radius;

                    Vector candidatePos = new Vector(
                        playerPos.X + MathF.Cos(angle) * distance,
                        playerPos.Y + MathF.Sin(angle) * distance,
                        playerPos.Z + 100
                    );

                    Vector endPos = new Vector(candidatePos.X, candidatePos.Y, playerPos.Z - 200);

                    Server.PrintToConsole($"Player-based attempt {attempt + 1}: ({candidatePos.X:F1},{candidatePos.Y:F1},{candidatePos.Z:F1})");

                    var groundPos = TryTraceHull(candidatePos, endPos);
                    if (groundPos != null)
                    {
                        Server.PrintToConsole($"Player-based SUCCESS: ({groundPos!.X:F1},{groundPos!.Y:F1},{groundPos!.Z:F1})");
                        return groundPos;
                    }
                }
            }
            catch (Exception ex)
            {
                Server.PrintToConsole($"GetRandomNearPlayers error: {ex.Message}");
            }

            return null;
        }

        // Fallback: Use spawn points directly with small offsets
        public static Vector? GetRandomNearSpawns()
        {
            Server.PrintToConsole("=== Trying GetRandomNearSpawns ===");
            
            try
            {
                var spawns = new List<Vector>();

                var tSpawns = Utilities.FindAllEntitiesByDesignerName<CInfoPlayerStart>("info_player_terrorist");
                var ctSpawns = Utilities.FindAllEntitiesByDesignerName<CInfoPlayerStart>("info_player_start");

                if (tSpawns != null)
                {
                    foreach (var spawn in tSpawns)
                    {
                        if (spawn?.AbsOrigin != null)
                        {
                            spawns.Add(new Vector(spawn.AbsOrigin.X, spawn.AbsOrigin.Y, spawn.AbsOrigin.Z));
                        }
                    }
                }

                if (ctSpawns != null)
                {
                    foreach (var spawn in ctSpawns)
                    {
                        if (spawn?.AbsOrigin != null)
                        {
                            spawns.Add(new Vector(spawn.AbsOrigin.X, spawn.AbsOrigin.Y, spawn.AbsOrigin.Z));
                        }
                    }
                }

                Server.PrintToConsole($"Found {spawns.Count} spawn points");

                if (spawns.Count == 0)
                {
                    Server.PrintToConsole("No spawn points found");
                    return null;
                }

                // Just use a spawn point with small random offset (no tracing)
                var randomSpawn = spawns[Random.Shared.Next(spawns.Count)];
                
                Vector offsetPos = new Vector(
                    randomSpawn.X + (Random.Shared.NextSingle() - 0.5f) * 200, // ±100 units
                    randomSpawn.Y + (Random.Shared.NextSingle() - 0.5f) * 200,
                    randomSpawn.Z + 20 // Slightly above spawn
                );

                Server.PrintToConsole($"Spawn-based position: ({offsetPos.X:F1},{offsetPos.Y:F1},{offsetPos.Z:F1})");
                return offsetPos;
            }
            catch (Exception ex)
            {
                Server.PrintToConsole($"GetRandomNearSpawns error: {ex.Message}");
            }

            return null;
        }

        // Ultimate fallback - just return known safe positions
        public static Vector GetFallbackPosition()
        {
            var fallbackPositions = new[]
            {
                new Vector(0, 0, 100),
                new Vector(200, 200, 100),
                new Vector(-200, -200, 100),
                new Vector(200, -200, 100),
                new Vector(-200, 200, 100)
            };

            var pos = fallbackPositions[Random.Shared.Next(fallbackPositions.Length)];
            Server.PrintToConsole($"Using fallback position: ({pos.X:F1},{pos.Y:F1},{pos.Z:F1})");
            return pos;
        }

        // Main method that tries all approaches
        public static Vector GetBestRandomPosition()
        {
            Vector? result = null;

            // Try 1: Simple trace method
            result = GetSimpleRandomPosition(10);
            if (result != null) return result;

            // Try 2: Near players
            result = GetRandomNearPlayers(600);
            if (result != null) return result;

            // Try 3: Near spawns (no tracing)
            result = GetRandomNearSpawns();
            if (result != null) return result;

            // Try 4: Fallback
            return GetFallbackPosition();
        }
    }
}