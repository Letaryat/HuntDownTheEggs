/*
*  This is a modified version of https://gist.github.com/21Joakim/6a264623e4ac8ae217e0eb15fc43e3e5#file-navmesh-cs
*  It works without NavPathCost and NavAreaBuildPath to get random positions on the map.
*  Don't even expect me to know what is happening here since I barely understand it myself. It was modified by ClaudeAI till it started to work.
*/

using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CounterStrikeSharp.API.Modules.Utils;

namespace HuntDownTheEggs.Extensions
{
    public class NavMesh
    {
        public static readonly MemoryFunctionWithReturn<nint, bool> CSource2Server_IsValidNavMesh = new("48 8D 05 ?? ?? ?? ?? 48 83 38 00 0F 95 C0 C3");

        public static readonly nint NavMeshPtrAddress = GetNavMeshPtrAddress();

        public static nint GetNavMeshPtrAddress()
        {
            nint functionAddress = Marshal.ReadIntPtr(CSource2Server_IsValidNavMesh.Handle);
            return functionAddress.Rel(3);
        }

        public static nint GetNavMeshAddress() => Marshal.ReadIntPtr(NavMeshPtrAddress);

        public static CNavMesh? GetNavMesh()
        {
            try
            {
                nint navMeshAddress = GetNavMeshAddress();
                Console.WriteLine($"[NavMesh Debug] NavMesh address: 0x{navMeshAddress:X}");
                
                if (navMeshAddress == 0)
                {
                    Console.WriteLine("[NavMesh Debug] NavMesh address is null");
                    return null;
                }

                var navMesh = new CNavMesh(navMeshAddress);
                Console.WriteLine($"[NavMesh Debug] NavMesh created, count: {navMesh.Count}");
                return navMesh;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavMesh Debug] Error getting NavMesh: {ex.Message}");
                return null;
            }
        }

        public static Vector? GetRandomPosition(int maxAttempts = 10, bool includeOneWayAccessible = false)
        {
            Console.WriteLine($"[NavMesh Debug] GetRandomPosition called with maxAttempts: {maxAttempts}");
            
            try
            {
                // Get spawn points first
                var spawnPoints = GetSpawnPoints();
                Console.WriteLine($"[NavMesh Debug] Found {spawnPoints.Count} spawn points");
                
                if (spawnPoints.Count == 0)
                {
                    Console.WriteLine("[NavMesh Debug] No spawn points found");
                    return null;
                }

                Vector? spawnPoint = spawnPoints.FirstOrDefault()?.AbsOrigin;
                if (spawnPoint == null)
                {
                    Console.WriteLine("[NavMesh Debug] First spawn point has null AbsOrigin");
                    return null;
                }

                Console.WriteLine($"[NavMesh Debug] Using spawn point: {spawnPoint.X}, {spawnPoint.Y}, {spawnPoint.Z}");
                return GetRandomAccessiblePosition(spawnPoint, maxAttempts, includeOneWayAccessible);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavMesh Debug] Exception in GetRandomPosition: {ex.Message}");
                Console.WriteLine($"[NavMesh Debug] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        public static Vector? GetRandomAccessiblePosition(Vector startPosition, int maxAttempts = 10, bool includeOneWayAccessible = false)
        {
            Console.WriteLine($"[NavMesh Debug] GetRandomAccessiblePosition called from {startPosition.X}, {startPosition.Y}, {startPosition.Z}");
            
            try
            {
                CNavMesh? navMesh = GetNavMesh();
                if (navMesh == null)
                {
                    Console.WriteLine("[NavMesh Debug] NavMesh is null, trying basic spawn point method");
                    return TryGetBasicRandomPosition();
                }

                Console.WriteLine($"[NavMesh Debug] NavMesh has {navMesh.Count} areas");

                if (navMesh.Count == 0)
                {
                    Console.WriteLine("[NavMesh Debug] NavMesh has no areas");
                    return null;
                }

                // Try to find a good nav area without complex pathfinding
                for (int i = 0; i < maxAttempts; i++)
                {
                    try
                    {
                        CNavArea navArea = navMesh[Random.Shared.Next(navMesh.Count)];
                        Console.WriteLine($"[NavMesh Debug] Attempt {i + 1}: Checking area {navArea.ID}, blocked team: {navArea.BlockedTeam}");
                        /*
                        if (navArea.BlockedTeam != 0)
                        {
                            Console.WriteLine($"[NavMesh Debug] Area {navArea.ID} is blocked for team {navArea.BlockedTeam}");
                            continue;
                        }
                        */
                        // Simple distance check instead of complex pathfinding
                        float distance = DistanceTo(startPosition, navArea.Center);
                        Console.WriteLine($"[NavMesh Debug] Distance to area {navArea.ID}: {distance}");
                        
                        if (distance < 5000.0f) // Reasonable distance check
                        {
                            Console.WriteLine($"[NavMesh Debug] Found valid area {navArea.ID} at {navArea.Center.X}, {navArea.Center.Y}, {navArea.Center.Z}");
                            return navArea.Center;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[NavMesh Debug] Error in attempt {i + 1}: {ex.Message}");
                        continue;
                    }
                }

                Console.WriteLine("[NavMesh Debug] No accessible areas found within distance, trying any non-blocked area");
                
                // Fallback: try any non-blocked area
                for (int i = 0; i < Math.Min(maxAttempts, navMesh.Count); i++)
                {
                    try
                    {
                        CNavArea navArea = navMesh[Random.Shared.Next(navMesh.Count)];
                        if (navArea.BlockedTeam == 0)
                        {
                            Console.WriteLine($"[NavMesh Debug] Using fallback area {navArea.ID} at {navArea.Center.X}, {navArea.Center.Y}, {navArea.Center.Z}");
                            return navArea.Center;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[NavMesh Debug] Error in fallback attempt {i + 1}: {ex.Message}");
                        continue;
                    }
                }

                Console.WriteLine("[NavMesh Debug] All attempts failed");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavMesh Debug] Exception in GetRandomAccessiblePosition: {ex.Message}");
                Console.WriteLine($"[NavMesh Debug] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        // Basic fallback that doesn't rely on navmesh
        private static Vector? TryGetBasicRandomPosition()
        {
            Console.WriteLine("[NavMesh Debug] Trying basic random position from spawn points");
            try
            {
                var spawnPoints = GetSpawnPoints();
                if (spawnPoints.Count == 0)
                {
                    Console.WriteLine("[NavMesh Debug] No spawn points for basic method");
                    return null;
                }

                var randomSpawn = spawnPoints[Random.Shared.Next(spawnPoints.Count)];
                if (randomSpawn?.AbsOrigin != null)
                {
                    // Add some random offset to the spawn point
                    var pos = randomSpawn.AbsOrigin;
                    var randomPos = new Vector(
                        pos.X + Random.Shared.Next(-500, 500),
                        pos.Y + Random.Shared.Next(-500, 500),
                        pos.Z + 50 // Slightly above ground
                    );
                    Console.WriteLine($"[NavMesh Debug] Generated basic random position: {randomPos.X}, {randomPos.Y}, {randomPos.Z}");
                    return randomPos;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavMesh Debug] Error in basic random position: {ex.Message}");
            }
            return null;
        }

        public static CNavArea? GetClosestNavArea(Vector position, float maximumDistance = -1)
        {
            Console.WriteLine($"[NavMesh Debug] GetClosestNavArea called for {position.X}, {position.Y}, {position.Z}");
            
            try
            {
                CNavMesh? navMesh = GetNavMesh();
                if (navMesh == null)
                {
                    Console.WriteLine("[NavMesh Debug] NavMesh is null in GetClosestNavArea");
                    return null;
                }

                float closestDistance = float.MaxValue;
                CNavArea? closest = null;

                int checkedAreas = 0;
                foreach (CNavArea navArea in navMesh)
                {
                    try
                    {
                        float distance = DistanceTo(position, navArea.Center);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closest = navArea;
                        }
                        checkedAreas++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[NavMesh Debug] Error checking nav area: {ex.Message}");
                        continue;
                    }
                }

                Console.WriteLine($"[NavMesh Debug] Checked {checkedAreas} areas, closest distance: {closestDistance}");

                if (maximumDistance > 0 && closestDistance > maximumDistance)
                {
                    Console.WriteLine($"[NavMesh Debug] Closest area is too far: {closestDistance} > {maximumDistance}");
                    return null;
                }

                if (closest != null)
                {
                    Console.WriteLine($"[NavMesh Debug] Found closest area {closest.ID} at distance {closestDistance}");
                }

                return closest;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavMesh Debug] Exception in GetClosestNavArea: {ex.Message}");
                return null;
            }
        }

        // Simple version without NavPathCost
        public static bool IsAreaAccessible(CNavArea startNavArea, CNavArea? goalNavArea = null, Vector? startPosition = null, Vector? goalPosition = null)
        {
            // Simple fallback - assume areas are accessible if they're not blocked and within reasonable distance
            if (goalNavArea == null) return true;
            if (goalNavArea.BlockedTeam != 0) return false;
            
            float distance = DistanceTo(startNavArea.Center, goalNavArea.Center);
            return distance < 3000.0f; // Adjust this distance as needed
        }

        private static List<SpawnPoint> GetSpawnPoints()
        {
            try
            {
                Console.WriteLine("[NavMesh Debug] Getting spawn points...");
                
                var gameRulesProxies = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules");
                Console.WriteLine($"[NavMesh Debug] Found {gameRulesProxies.Count()} game rules proxies");

                CCSGameRules? gameRules = gameRulesProxies.FirstOrDefault()?.GameRules;
                
                if (gameRules == null)
                {
                    Console.WriteLine("[NavMesh Debug] GameRules is null");
                    return [];
                }

                Console.WriteLine("[NavMesh Debug] GameRules found, getting spawn points");

                List<SpawnPoint> spawnPoints = [];
                
                try
                {
                    var ctSpawns = GetVectorPtrElements(gameRules.CTSpawnPoints);
                    var ctCount = ctSpawns.Count();
                    Console.WriteLine($"[NavMesh Debug] Found {ctCount} CT spawn points");
                    spawnPoints.AddRange(ctSpawns);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NavMesh Debug] Error getting CT spawns: {ex.Message}");
                }

                try
                {
                    var tSpawns = GetVectorPtrElements(gameRules.TerroristSpawnPoints);
                    var tCount = tSpawns.Count();
                    Console.WriteLine($"[NavMesh Debug] Found {tCount} T spawn points");
                    spawnPoints.AddRange(tSpawns);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NavMesh Debug] Error getting T spawns: {ex.Message}");
                }

                Console.WriteLine($"[NavMesh Debug] Total spawn points: {spawnPoints.Count}");
                return spawnPoints;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavMesh Debug] Exception in GetSpawnPoints: {ex.Message}");
                return [];
            }
        }

        private static float DistanceTo(Vector a, Vector b) => MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));

        private static IEnumerable<T> GetVectorPtrElements<T>(NetworkedVector<T?> vector)
        {
            if (vector.Count <= 0)
            {
                yield break;
            }

            IntPtr basePtr = NativeAPI.GetNetworkVectorElementAt(vector.Handle, 0);
            for (int i = 0; i < vector.Count; i++)
            {
                T? value = (T?)Activator.CreateInstance(typeof(T), Marshal.ReadIntPtr(basePtr + (i * 8)));
                if (value != null)
                {
                    yield return value;
                }
            }
        }

        private static nint GetBaseAddress()
        {
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
            {
                if (module.ModuleName == "libserver.so" && !module.FileName.Contains("addons"))
                {
                    return module.BaseAddress;
                }
            }

            throw new Exception("Could not get base address");
        }
    }

    public class CNavMesh(nint pointer) : NativeObject(pointer), IReadOnlyCollection<CNavArea>
    {
        public int Count => Marshal.ReadInt32(Handle + 8);

        public CNavArea this[int index]
        {
            get
            {
                nint navAreas = Marshal.ReadIntPtr(Handle + 16);
                return new(Marshal.ReadIntPtr(navAreas + index * 8));
            }
        }

        public IEnumerator<CNavArea> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public class CNavArea(nint pointer) : NativeObject(pointer)
    {
        public Vector Center => new(Handle + 12);
        public Vector Min => new(Handle + 36);
        public Vector Max => new(Handle + 48);

        public uint ID => unchecked((uint)Marshal.ReadInt32(Handle + 84));

        public byte BlockedTeam => Marshal.ReadByte(Handle + 92);
    }

    public static class IntPtrExtension
    {
        public static nint Rel(this nint address, int offset)
        {
            int relativeOffset = Marshal.ReadInt32(address + offset);
            return address + relativeOffset + offset + sizeof(int);
        }
    }
}