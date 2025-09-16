using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using HuntDownTheEggs.Core;
using Microsoft.Extensions.Logging;


namespace HuntDownTheEggs
{
    public class SpawnsManager(HuntDownTheEggsPlugin plugin)
    {
        private readonly HuntDownTheEggsPlugin _plugin = plugin;
        public List<Vector> DeathmatchSpawns { get; set; } = new();
        public List<Vector> RandomEggSpawns { get; set; } = new();

        public void SetDMSpawns()
        {
            _plugin.DebugLog("Setting mp_randomspawn to 1 to find deathmatch spawns.");
            Server.ExecuteCommand("mp_randomspawn 1");
            var spawns = new List<Vector>();
            var dmSpawns = Utilities.FindAllEntitiesByDesignerName<CInfoDeathmatchSpawn>("info_deathmatch_spawn")
                .Where(s => s?.AbsOrigin != null)
                .Select(s => new Vector(s.AbsOrigin!.X, s.AbsOrigin.Y, s.AbsOrigin.Z));

            spawns.AddRange(dmSpawns);
            _plugin.DebugLog($"Found: {dmSpawns.Count()} deathmatch spawns.");
            DeathmatchSpawns = spawns;

            _plugin.AddTimer(1f, () =>
            {
                Server.ExecuteCommand("mp_randomspawn 0");
            });

            _plugin.DebugLog($"mp_randomspawn set back to 0.");
        }

        public Vector GenerateRandomSpawn()
        {
            if (!DeathmatchSpawns.Any())
            {
                _plugin.DebugLog("Could not find any Deathmatch spawns on this map. Cannot generate random ones");
                return null!;
            }

            var baseSpawn = DeathmatchSpawns[Random.Shared.Next(DeathmatchSpawns.Count)];
            var spawnPos = baseSpawn;

            if (spawnPos == null) return null!;

            float offsetX = (Random.Shared.NextSingle() - 0.5f) * 2 * 250f;
            float offsetY = (Random.Shared.NextSingle() - 0.5f) * 2 * 250f;

            var newPos = new Vector(
                spawnPos!.X + offsetX,
                spawnPos!.Y + offsetY,
                spawnPos!.Z// lekki offset w górę, żeby uniknąć zakopania w ziemi
            );

            return newPos;

        }


    }
}