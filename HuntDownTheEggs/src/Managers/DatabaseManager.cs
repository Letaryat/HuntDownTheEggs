using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace HuntDownTheEggs;

// Same schema and queries as the Swiftly version (tables EggHunt / EggKills / EggRandom), so an existing
// database can be reused as is. The difference: the connection comes from DBSetup in this plugin's config
// (Swiftly provided it through core.Database).
public sealed class DatabaseManager(HuntDownTheEggsPlugin plugin)
{
    private readonly HuntDownTheEggsPlugin _plugin = plugin;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private string _connectionString = string.Empty;

    private const string PlacedCount =
        "CASE WHEN EggID IS NULL OR EggID = '' THEN 0 " +
        "ELSE LENGTH(EggID) - LENGTH(REPLACE(EggID, ',', '')) + 1 END";

    private MySqlConnection Open() => new(_connectionString);

    public void Initialize()
    {
        var db = _plugin.Config.DBSetup;

        if (string.IsNullOrEmpty(db.DBHost) || string.IsNullOrEmpty(db.DBName) ||
            string.IsNullOrEmpty(db.DBPassword) || string.IsNullOrEmpty(db.DBUsername))
        {
            _plugin.Logger.LogError("HuntDownTheEggs: MySQL database configuration is incomplete!");
            return;
        }

        _connectionString = new MySqlConnectionStringBuilder
        {
            Server = db.DBHost,
            UserID = db.DBUsername,
            Port = db.DBPort,
            Password = db.DBPassword,
            Database = db.DBName,
        }.ConnectionString;

        try
        {
            using var conn = Open();
            conn.Open();
            conn.Execute("CREATE TABLE IF NOT EXISTS EggHunt(SteamID VARCHAR(255), Name VARCHAR(255), Map VARCHAR(255), EggID VARCHAR(255))");
            conn.Execute("CREATE TABLE IF NOT EXISTS EggKills(SteamID VARCHAR(255), Name VARCHAR(255), Map VARCHAR(255), EggsPicked INT)");
            conn.Execute("CREATE TABLE IF NOT EXISTS EggRandom(SteamID VARCHAR(255), Name VARCHAR(255), Map VARCHAR(255), EggsPicked INT)");
            _plugin.Logger.LogInformation("HuntDownTheEggs: database ready ({Host}/{Db})", db.DBHost, db.DBName);
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError(ex, "HuntDownTheEggs: error while initializing database");
        }
    }

    public async Task<PlayerData?> GetPlayerEggsAsync(ulong steamId, string mapName)
    {
        if (_connectionString.Length == 0) return null;
        try
        {
            await using var conn = Open();
            await conn.OpenAsync();
            var sid = steamId.ToString();

            var eggIds = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT EggID FROM EggHunt WHERE SteamID = @sid AND Map = @mapName",
                new { sid, mapName });

            var totalEggs = await conn.QueryFirstOrDefaultAsync<int>(
                $@"SELECT
                   (SELECT COALESCE(SUM({PlacedCount}), 0) FROM EggHunt WHERE SteamID = @sid)
                   +
                   (SELECT COALESCE(SUM(EggsPicked), 0) FROM EggKills WHERE SteamID = @sid)
                   +
                   (SELECT COALESCE(SUM(EggsPicked), 0) FROM EggRandom WHERE SteamID = @sid)
                   AS TotalEggs",
                new { sid });

            var killEggs = await conn.QueryFirstOrDefaultAsync<int>(
                "SELECT COALESCE(EggsPicked, 0) FROM EggKills WHERE SteamID = @sid AND Map = @mapName",
                new { sid, mapName });

            var randomEggs = await conn.QueryFirstOrDefaultAsync<int>(
                "SELECT COALESCE(EggsPicked, 0) FROM EggRandom WHERE SteamID = @sid AND Map = @mapName",
                new { sid, mapName });

            var eggs = eggIds?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToList() ?? [];

            return new PlayerData
            {
                SteamId = steamId,
                Map = mapName,
                Eggs = eggs,
                KillEggs = killEggs,
                RandomEggs = randomEggs,
                TotalEggs = totalEggs
            };
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError(ex, "HuntDownTheEggs: database error while loading player {SteamId}", steamId);
            return null;
        }
    }

    public async Task SavePlayerEggsAsync(PlayerData d)
    {
        if (_connectionString.Length == 0) return;

        await _writeLock.WaitAsync();
        try
        {
            await using var conn = Open();
            await conn.OpenAsync();
            var sid = d.SteamId.ToString();

            // Kill eggs
            if (await ExistsAsync(conn, "EggKills", sid, d.Map))
                await conn.ExecuteAsync("UPDATE EggKills SET EggsPicked = @eggs, Name = @name WHERE SteamID = @sid AND Map = @map",
                    new { sid, eggs = d.KillEggs, map = d.Map, name = d.PlayerName });
            else
                await conn.ExecuteAsync("INSERT INTO EggKills (SteamID, Map, EggsPicked, Name) VALUES (@sid, @map, @eggs, @name)",
                    new { sid, map = d.Map, eggs = d.KillEggs, name = d.PlayerName });

            // Random eggs
            if (await ExistsAsync(conn, "EggRandom", sid, d.Map))
                await conn.ExecuteAsync("UPDATE EggRandom SET EggsPicked = @eggs, Name = @name WHERE SteamID = @sid AND Map = @map",
                    new { sid, eggs = d.RandomEggs, map = d.Map, name = d.PlayerName });
            else
                await conn.ExecuteAsync("INSERT INTO EggRandom (SteamID, Map, EggsPicked, Name) VALUES (@sid, @map, @eggs, @name)",
                    new { sid, map = d.Map, eggs = d.RandomEggs, name = d.PlayerName });

            // Placed eggs
            if (d.Eggs.Count == 0) return;

            var existingStr = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT EggID FROM EggHunt WHERE SteamID = @sid AND Map = @map", new { sid, map = d.Map });
            var existing = existingStr?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToHashSet() ?? [];

            if (!d.Eggs.Any(e => !existing.Contains(e))) return;

            var combined = string.Join(",", existing.Union(d.Eggs).OrderBy(x => x));

            if (await ExistsAsync(conn, "EggHunt", sid, d.Map))
                await conn.ExecuteAsync("UPDATE EggHunt SET EggID = @combined, Name = @name WHERE SteamID = @sid AND Map = @map",
                    new { combined, sid, map = d.Map, name = d.PlayerName });
            else
                await conn.ExecuteAsync("INSERT INTO EggHunt (SteamID, Map, EggID, Name) VALUES (@sid, @map, @combined, @name)",
                    new { sid, map = d.Map, combined, name = d.PlayerName });
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError(ex, "HuntDownTheEggs: error while saving player {SteamId}", d.SteamId);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ---------- leaderboards (TOP 5; mapName == null -> all maps) ----------

    private static string MapFilter(string? mapName) => mapName == null ? "" : "WHERE Map = @mapName";

    public async Task<Dictionary<string, int>> GetTopPlacedEggsAsync(string? mapName)
    {
        if (_connectionString.Length == 0) return [];
        try
        {
            await using var conn = Open();
            await conn.OpenAsync();

            var rows = await conn.QueryAsync<TopRow>(
                $@"SELECT Name, Cnt FROM (
                       SELECT MAX(Name) AS Name, SUM({PlacedCount}) AS Cnt
                       FROM EggHunt
                       {MapFilter(mapName)}
                       GROUP BY SteamID
                   ) t
                   WHERE Cnt > 0
                   ORDER BY Cnt DESC
                   LIMIT 5",
                new { mapName });

            return ToDict(rows);
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError(ex, "Error loading placed egg leaderboard");
            return [];
        }
    }

    public async Task<Dictionary<string, int>> GetTopCountEggsAsync(string table, string? mapName)
    {
        // table name goes into the SQL text, so it is whitelisted
        if (_connectionString.Length == 0 || (table != "EggKills" && table != "EggRandom"))
            return [];

        try
        {
            await using var conn = Open();
            await conn.OpenAsync();

            var rows = await conn.QueryAsync<TopRow>(
                $@"SELECT Name, Cnt FROM (
                       SELECT MAX(Name) AS Name, SUM(COALESCE(EggsPicked, 0)) AS Cnt
                       FROM {table}
                       {MapFilter(mapName)}
                       GROUP BY SteamID
                   ) t
                   WHERE Cnt > 0
                   ORDER BY Cnt DESC
                   LIMIT 5",
                new { mapName });

            return ToDict(rows);
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError(ex, "Error loading leaderboard from {Table}", table);
            return [];
        }
    }

    public async Task<Dictionary<string, int>> GetTopCombinedEggsAsync(string? mapName)
    {
        if (_connectionString.Length == 0) return [];
        try
        {
            await using var conn = Open();
            await conn.OpenAsync();
            var filter = MapFilter(mapName);

            var rows = await conn.QueryAsync<TopRow>(
                $@"SELECT Name, Cnt FROM (
                       SELECT MAX(Name) AS Name, SUM(EggCount) AS Cnt
                       FROM (
                           SELECT SteamID, MAX(Name) AS Name, SUM({PlacedCount}) AS EggCount
                           FROM EggHunt
                           {filter}
                           GROUP BY SteamID

                           UNION ALL

                           SELECT SteamID, MAX(Name) AS Name, SUM(COALESCE(EggsPicked, 0)) AS EggCount
                           FROM EggKills
                           {filter}
                           GROUP BY SteamID

                           UNION ALL

                           SELECT SteamID, MAX(Name) AS Name, SUM(COALESCE(EggsPicked, 0)) AS EggCount
                           FROM EggRandom
                           {filter}
                           GROUP BY SteamID
                       ) AS EggStats
                       GROUP BY SteamID
                   ) t
                   WHERE Cnt > 0
                   ORDER BY Cnt DESC
                   LIMIT 5",
                new { mapName });

            return ToDict(rows);
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError(ex, "Error loading combined egg leaderboard");
            return [];
        }
    }

    private static async Task<bool> ExistsAsync(MySqlConnection conn, string table, string sid, string map) =>
        await conn.QueryFirstOrDefaultAsync<int>(
            $"SELECT COUNT(1) FROM {table} WHERE SteamID = @sid AND Map = @map", new { sid, map }) > 0;

    private sealed class TopRow
    {
        public string Name { get; set; } = string.Empty;
        public decimal Cnt { get; set; }
    }

    private static Dictionary<string, int> ToDict(IEnumerable<TopRow> rows)
    {
        var d = new Dictionary<string, int>();
        foreach (var r in rows) d[r.Name] = (int)r.Cnt;
        return d;
    }
}
