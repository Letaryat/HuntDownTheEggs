using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using CounterStrikeSharp.API.Modules.Utils;

namespace HuntDownTheEggs.Utils
{
    public class NavMesh
    {
        // How to find
        // 1. Search for `NavAreaBuildPath`
        // 
        // Alternative way to find it
        // 1. It makes use of lot of convars, search for any of these and check where they are used
        //  * nav_pathfind_debug_log
        //  * nav_pathfind_draw
        //  * nav_pathfind_draw_blocked
        //  * nav_pathfind_draw_fail
        //  * nav_pathfind_draw_costs
        //  * nav_pathfind_draw_total_costs
        //  * nav_pathfind_inadmissable_heuristic_factor
        public static readonly MemoryFunctionWithReturn<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, float, float, IntPtr, IntPtr> NavAreaBuildPath = new("55 48 89 E5 41 57 41 56 41 55 49 89 CD 41 54 49 89 D4 53 48 89 FB 48 8D 3D");

        // How to find
        // 1. Search for `spawnpoints.2v2`
        // 2. Find where `NavAreaBuildPath` is being called
        // 3. The 5th argument should be the result of `GetNavPathCost`, find where the variable was set
        //
        // Alternative way to find it
        // 1. It makes use of the `nav_avoid_obstacles` convar, search for it and check where it's used
        public static readonly MemoryFunctionWithReturn<IntPtr> NavPathCost = new("0F B6 05 ? ? ? 01 84 C0 74 1D 80 3D ? ? ? 01 00 74 07 C6 05 ? ? ? 01 00");

        // Thanks to _xstage on the CounterStrikeSharp Discord server for a more reliable solution than using an offset to get the navmesh
        public static readonly MemoryFunctionWithReturn<nint, bool> CSource2Server_IsValidNavMesh = new("48 8D 05 ? ? ? ? 48 83 38 00 0F 95 C0");

        public static readonly nint NavMeshPtrAddress = GetNavMeshPtrAddress();

        public static nint GetNavMeshPtrAddress()
        {
            nint functionAddress = Marshal.ReadIntPtr(CSource2Server_IsValidNavMesh.Handle);
            return functionAddress.Rel(3);
        }

        public static nint GetNavMeshAddress() => Marshal.ReadIntPtr(NavMeshPtrAddress);

        public static CNavMesh? GetNavMesh()
        {
            nint navMeshAddress = GetNavMeshAddress();
            if (navMeshAddress == 0)
            {
                return null;
            }

            return new(navMeshAddress);
        }

        public static Vector? GetRandomPosition(int maxAttempts = 10, bool includeOneWayAccessible = false)
        {
            // NOTE: This assumes every spawn point is accessible to every other
            Vector? spawnPoint = GetSpawnPoints().FirstOrDefault()?.AbsOrigin;
            if (spawnPoint == null)
            {
                return null;
            }

            return GetRandomAccessiblePosition(spawnPoint, maxAttempts, includeOneWayAccessible);
        }

        // Depending on your use-case you might want to pre-generate
        // all of the accessible areas and just pick from one of those
        // directly. Just make sure you clear and regenerate the data
        // at the start of a new map. Preferably you only want to generate
        // it once as it can be quite expensive depending on the cirumstances,
        // for instance, on wingman inferno checking for ALL one way accessible
        // areas takes somewhere around 2 seconds while on other maps it's usually
        // a few hundred milliseconds.
        public static Vector? GetRandomAccessiblePosition(Vector startPosition, int maxAttempts = 10, bool includeOneWayAccessible = false)
        {
            CNavMesh? navMesh = GetNavMesh();
            if (navMesh == null)
            {
                return null;
            }

            CNavArea? startNavArea = GetClosestNavArea(startPosition);
            if (startNavArea == null)
            {
                return null;
            }

            for (int i = 0; i < maxAttempts; i++)
            {
                CNavArea navArea = navMesh[Random.Shared.Next(navMesh.Count)];
                // TODO: Filter out nav areas close to blocked areas
                // because they can be problematic, for instance,
                // you can spawn inside objects on wingman maps.
                if (navArea.BlockedTeam != 0)
                {
                    continue;
                }

                if (IsAreaAccessible(startNavArea, navArea))
                {
                    return navArea.Center;
                }

                // NOTE: Usually these are boost spots which are not accessible
                // solo but they can be a bit iffy so use with caution
                if (includeOneWayAccessible)
                {
                    if (IsAreaAccessible(navArea, startNavArea))
                    {
                        return navArea.Center;
                    }
                }
            }

            return null;
        }

        // TODO: Fix this, it's not implemented correctly,
        // if you are already inside a navarea it might not
        // return the navarea you are inside but another one
        // which you are closer to the center to
        /// <summary>
        /// Caution when using this, it's not as performant as the native
        /// implementation of GetNearestNavArea because this does not
        /// make use of the cells.
        /// </summary>
        public static CNavArea? GetClosestNavArea(Vector position, float maximumDistance = -1)
        {
            CNavMesh? navMesh = GetNavMesh();
            if (navMesh == null)
            {
                return null;
            }

            float closestDistance = float.MaxValue;
            CNavArea? closest = null;

            foreach (CNavArea navArea in navMesh)
            {
                float distance = DistanceTo(position, navArea.Center);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = navArea;
                }
            }

            if (maximumDistance > 0 && closestDistance > maximumDistance)
            {
                return null;
            }

            return closest;
        }

        public static unsafe bool IsAreaAccessible(CNavArea startNavArea, CNavArea? goalNavArea = null, Vector? startPosition = null, Vector? goalPosition = null)
        {
            nint navPathCost = NavPathCost.Invoke();

            // TODO: Not sure how many bytes this needs.
            // 
            // It seems to return an address to a CNavArea
            // and a distance, the distance is only non-zero
            // if the goal is in any nav area. I am guessing
            // it returns the CNavArea, along with the distance,
            // of the closest navarea.
            fixed (byte* unk = new byte[32])
            {
                float distance = 0.0f;

                // Doesn't seem to work without the start nav area
                nint startNavAreaPtr = startNavArea.Handle;
                nint goalNavAreaPtr = goalNavArea?.Handle ?? 0;

                nint startPositionPtr = startPosition?.Handle ?? 0;
                nint goalPositionPtr = goalPosition?.Handle ?? 0;

                NavAreaBuildPath.Invoke(startNavAreaPtr, goalNavAreaPtr, startPositionPtr, goalPositionPtr, navPathCost, (nint)unk, -1.0f, -1.0f, (nint)(void*)&distance);
                return distance >= 0;
            }
        }

        private static List<SpawnPoint> GetSpawnPoints()
        {
            // TODO: You probably want to cache this
            CCSGameRules? gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules")
                .FirstOrDefault()?
                .GameRules;

            if (gameRules == null)
            {
                return [];
            }

            List<SpawnPoint> spawnPoints = [];
            spawnPoints.AddRange(GetVectorPtrElements(gameRules.CTSpawnPoints));
            spawnPoints.AddRange(GetVectorPtrElements(gameRules.TerroristSpawnPoints));
            return spawnPoints;
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

    // Thanks to nuko8964 on the CounterStrikeSharp Discord server for the suggestion
    public static class IntPtrExtension
    {
        public static nint Rel(this nint address, int offset)
        {
            int relativeOffset = Marshal.ReadInt32(address + offset);
            return address + relativeOffset + offset + sizeof(int) /* The size of the relative offset */;
        }
    }
}
