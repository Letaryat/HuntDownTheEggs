using Dapper;
using HuntDownTheEggs.Core;
using HuntDownTheEggs.Models;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace HuntDownTheEggs
{
    public class DatabaseManager(HuntDownTheEggsPlugin plugin)
    {
        private readonly HuntDownTheEggsPlugin _plugin = plugin;
        private string _connectionString = string.Empty;

        public void InitializeConnection()
        {
            var config = _plugin.Config;
            
            if (string.IsNullOrEmpty(config.DBHost) || 
                string.IsNullOrEmpty(config.DBName) || 
                string.IsNullOrEmpty(config.DBPassword) || 
                string.IsNullOrEmpty(config.DBUsername))
            {
                _plugin.Logger.LogInformation("MySQL database configuration is incomplete!");
                return;
            }

            MySqlConnectionStringBuilder builder = new()
            {
                Server = config.DBHost,
                UserID = config.DBUsername,
                Port = config.DBPort,
                Password = config.DBPassword,
                Database = config.DBName,
            };
            
            _connectionString = builder.ConnectionString;

            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                _plugin.Logger.LogInformation("Connected to MySQL database");
                
                // Create required tables if they don't exist
                string createTablesQuery = @"
                CREATE TABLE IF NOT EXISTS EggHunt(
                    SteamID VARCHAR(255),
                    Name VARCHAR(255),
                    Map VARCHAR(255),
                    EggID VARCHAR(255)
                );
                CREATE TABLE IF NOT EXISTS EggKills(
                    SteamID VARCHAR(255),
                    Name VARCHAR(255),
                    Map VARCHAR(255),
                    EggsPicked INT
                );";
                
                connection.Execute(createTablesQuery);
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogInformation($"Error connecting to database: {ex}");
            }
        }

        public async Task<PlayerData?> GetPlayerEggsAsync(ulong steamId, string mapName)
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                _plugin.Logger.LogInformation("Database connection string is not set!");
                return null;
            }

            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                
                var eggIds = await connection.QueryFirstOrDefaultAsync<string>(
                    "SELECT EggID FROM EggHunt WHERE SteamID = @steamId AND Map = @mapName",
                    new { steamId, mapName });

                var totalEggs = await connection.QueryFirstOrDefaultAsync<int>(
                    "SELECT COALESCE(SUM(LENGTH(eh.EggID) - LENGTH(REPLACE(eh.EggID, ',', '')) + 1), 0) AS TotalEggs " +
                    "FROM EggHunt eh WHERE eh.SteamID = @steamId",
                    new { steamId });
                
                var killEggs = await connection.QueryFirstOrDefaultAsync<int>(
                    "SELECT COALESCE(EggsPicked, 0) FROM EggKills WHERE SteamID = @steamId AND Map = @mapName",
                    new { steamId, mapName });

                if (eggIds == null) 
                {
                    return new PlayerData
                    {
                        SteamId = steamId,
                        Map = mapName,
                        Eggs = [],
                        KillEggs = killEggs,
                        TotalEggs = totalEggs
                    };
                }

                var eggs = eggIds
                    .Split(",", StringSplitOptions.RemoveEmptyEntries)
                    .Select(int.Parse)
                    .ToList() ?? [];

                return new PlayerData
                {
                    SteamId = steamId,
                    Map = mapName,
                    Eggs = eggs,
                    KillEggs = killEggs,
                    TotalEggs = totalEggs
                };
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogInformation($"Database error: {ex}");
                return null;
            }
        }

        public async Task SavePlayerEggsAsync(PlayerData playerData)
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                _plugin.Logger.LogInformation("Database connection string is not set!");
                return;
            }

            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                // Save kill eggs data
                var killEggsExist = await CheckKillEggsExistAsync(connection, playerData.SteamId, playerData.Map);

                if (killEggsExist)
                {
                    await connection.ExecuteAsync(
                        "UPDATE EggKills SET EggsPicked = @eggs, Name = @name WHERE SteamID = @steamId AND Map = @map",
                        new { 
                            steamId = playerData.SteamId, 
                            eggs = playerData.KillEggs, 
                            map = playerData.Map, 
                            name = playerData.PlayerName 
                        });
                }
                else
                {
                    await connection.ExecuteAsync(
                        "INSERT INTO EggKills (SteamID, Map, EggsPicked, Name) VALUES (@steamId, @map, @eggs, @name)",
                        new { 
                            steamId = playerData.SteamId, 
                            map = playerData.Map, 
                            eggs = playerData.KillEggs,
                            name = playerData.PlayerName 
                        });
                }

                // If there are no collected eggs, we don't need to update the EggHunt table
                if (playerData.Eggs == null || playerData.Eggs.Count == 0) return;

                // Get existing eggs for this player on this map
                var existingEggsStr = await connection.QueryFirstOrDefaultAsync<string>(
                    "SELECT EggID FROM EggHunt WHERE SteamID = @steamId AND Map = @map",
                    new { steamId = playerData.SteamId, map = playerData.Map });

                // Parse existing eggs
                var existingEggs = existingEggsStr?
                    .Split(",", StringSplitOptions.RemoveEmptyEntries)
                    .Select(int.Parse)
                    .ToHashSet() ?? [];

                // Find new eggs that weren't already saved
                var newEggs = playerData.Eggs.Where(egg => !existingEggs.Contains(egg)).ToList();
                if (newEggs.Count == 0) return;

                // Combine existing and new eggs, sort, and convert back to string
                var combinedEggs = existingEggs
                    .Union(newEggs)
                    .OrderBy(x => x)
                    .ToList();

                var combinedEggsStr = string.Join(",", combinedEggs);

                // Check if player already has an entry in the EggHunt table
                var playerExists = await CheckPlayerExistsAsync(connection, playerData.SteamId, playerData.Map);

                if (playerExists)
                {
                    await connection.ExecuteAsync(
                        "UPDATE EggHunt SET EggID = @eggsString, Name = @name WHERE SteamID = @steamId AND Map = @map",
                        new { 
                            eggsString = combinedEggsStr, 
                            steamId = playerData.SteamId, 
                            map = playerData.Map,
                            name = playerData.PlayerName 
                        });
                }
                else
                {
                    await connection.ExecuteAsync(
                        "INSERT INTO EggHunt (SteamID, Map, EggID, Name) VALUES (@steamId, @map, @eggsString, @name)",
                        new { 
                            steamId = playerData.SteamId, 
                            map = playerData.Map, 
                            eggsString = combinedEggsStr,
                            name = playerData.PlayerName 
                        });
                }
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogInformation($"Error saving player data: {ex}");
            }
        }

        public async Task<Dictionary<string, int>> GetTopEggsAsync()
        {
            if (string.IsNullOrEmpty(_connectionString)) return [];

            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var results = await connection.QueryAsync<(string Name, int TotalEggs)>(
                    @"SELECT `Name`, 
                      SUM(LENGTH(EggID) - LENGTH(REPLACE(EggID, ',', '')) + 1) AS TotalEggs 
                      FROM `EggHunt` 
                      WHERE `EggID` IS NOT NULL AND `EggID` <> '' 
                      GROUP BY `SteamID`, `Name` 
                      ORDER BY TotalEggs DESC 
                      LIMIT 5;");

                return results?.ToDictionary(row => row.Name, row => row.TotalEggs) 
                       ?? [];
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogInformation($"Error getting top eggs: {ex}");
                return [];
            }
        }

        public async Task<Dictionary<string, int>> GetTopKillEggsAsync(string map)
        {
            if (string.IsNullOrEmpty(_connectionString)) return [];

            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var results = await connection.QueryAsync<(string Name, int EggsPicked)>(
                    @"SELECT `Name`, `EggsPicked` 
                      FROM `EggKills` 
                      WHERE `Map` = @map 
                      ORDER BY `EggsPicked` DESC 
                      LIMIT 5;",
                    new { map });

                return results?.ToDictionary(row => row.Name, row => row.EggsPicked) 
                       ?? [];
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogInformation($"Error getting top kill eggs: {ex}");
                return [];
            }
        }

        private async Task<bool> CheckPlayerExistsAsync(MySqlConnection connection, ulong steamId, string mapName)
        {
            try
            {
                string sql = "SELECT COUNT(1) FROM `EggHunt` WHERE SteamID = @steamId AND Map = @mapName";
                var exists = await connection.ExecuteScalarAsync<bool>(sql, new { steamId, mapName });
                return exists;
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogInformation($"Error checking player existence: {ex}");
                return false;
            }
        }

        private async Task<bool> CheckKillEggsExistAsync(MySqlConnection connection, ulong steamId, string mapName)
        {
            try
            {
                string sql = "SELECT COUNT(1) FROM `EggKills` WHERE SteamID = @steamId AND Map = @mapName";
                var exists = await connection.ExecuteScalarAsync<bool>(sql, new { steamId, mapName });
                return exists;
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogInformation($"Error checking kill eggs existence: {ex}");
                return false;
            }
        }
    }
}