using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;


namespace HuntDownTheEggs
{
    public partial class HuntDownTheEggs
    {
        public async void ConnectionDB()
        {

            if (Config.DBHost.Length < 1 || Config.DBName.Length < 1 || Config.DBPassword.Length < 1 || Config.DBUsername.Length < 1)
            {
                Logger.LogInformation($"You need to setup a mysql database!");
                return;
            }

            MySqlConnectionStringBuilder builder = new()
            {
                Server = Config.DBHost,
                UserID = Config.DBUsername,
                Port = Config.DBPort,
                Password = Config.DBPassword,
                Database = Config.DBName,
            };
            DBConnection = builder.ConnectionString;

            try
            {
                var connection = new MySqlConnection(builder.ConnectionString);
                await connection.OpenAsync();
                Logger.LogInformation("Connected to mysql DB");
                //var sqlcmd = connection.CreateCommand();
                string createTable = @"
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
                );
                ";
                await connection.QueryFirstOrDefaultAsync(createTable);
            }
            catch (Exception ex)
            {
                Logger.LogInformation($"Error while trying to connect to database: {ex}");
                return;
            }
        }


        public async Task<PlayerEggs?> GetPlayerEggs(ulong SteamID, string mapName)
        {
            await using var connection = new MySqlConnection(DBConnection);
            try
            {
                await connection.OpenAsync();
                var eggIds = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT EggID FROM EggHunt WHERE SteamID = @steamid AND Map = @mapName",
                new { steamid = SteamID, mapName });
                var eggKill = await connection.QueryFirstOrDefaultAsync<int>(
                "SELECT EggsPicked FROM EggKills WHERE SteamID = @steamid AND Map = @mapName",
                new { steamid = SteamID, mapName });

                if (eggIds == null) return null;

                var existingEggs = eggIds?
                    .Split(",", StringSplitOptions.RemoveEmptyEntries)
                    .Select(int.Parse)
                    .ToList() ?? new List<int>();

                return new PlayerEggs
                {
                    steamid = SteamID,
                    map = mapName,
                    eggs = existingEggs,
                    killeggs = eggKill
                };
            }
            catch (Exception ex)
            {
                Logger.LogInformation($"Database Error: {ex}");
                return null;
            }
        }
        public async Task<bool> UserExist(ulong SteamID, string mapName)
        {
            try
            {
                using var connection = new MySqlConnection( DBConnection );
                await connection.OpenAsync();
                string sqlExist = "SELECT COUNT(1) FROM `EggHunt` WHERE SteamID = @SteamID AND Map = @mapName";
                var exists = await connection.ExecuteScalarAsync<bool>(sqlExist, new {SteamID, mapName});
                return exists;
            }
            catch (Exception ex)
            {
                Logger.LogInformation(ex.ToString());
            }
            Logger.LogInformation("Player does not exist!");
            return false;
        }

        public async Task<bool> UserExistKill(ulong SteamID)
        {
            try
            {
                using var connection = new MySqlConnection(DBConnection);
                await connection.OpenAsync();
                string sqlExist = "SELECT COUNT(1) FROM `EggKills` WHERE SteamID = @SteamID AND Map = @mapName";
                var exists = await connection.ExecuteScalarAsync<bool>(sqlExist, new { SteamID, mapName });
                return exists;
            }
            catch (Exception ex)
            {
                Logger.LogInformation(ex.ToString());
            }
            Logger.LogInformation("Player does not exist - EggKill Table");
            return false;
        }

        public async Task SaveEggs(ulong SteamID, string name)
        {
            if(!Players.ContainsKey(SteamID)) return;
            try
            {
                await using var connection = new MySqlConnection(DBConnection);
                var playerData = Players[SteamID];
                var map = playerData.map;
                var eggsToSave = playerData.eggs;
                await connection.OpenAsync();

                var userExist = await UserExist(SteamID, map);
                var userExistKill = await UserExistKill(SteamID);

                await Task.Run(() => userExist);
                await Task.Run(() => userExistKill);

                if (userExistKill)
                {
                    await connection.ExecuteAsync(
                    "UPDATE EggKills SET EggsPicked = @eggs WHERE SteamID = @steamid AND Map = @map AND Name = @name",
                    new { steamid = SteamID, eggs = playerData.killeggs, map, name});
                }
                else
                {
                    await connection.ExecuteAsync(
                    "INSERT INTO EggKills (SteamID, Map, EggsPicked, Name) VALUES (@steamid, @map, @eggs, @name)",
                    new { steamid = SteamID, map, eggs = playerData.killeggs, name });
                }

                if (eggsToSave == null || eggsToSave.Count == 0) return;

                var existingEggsStr = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT EggID FROM EggHunt WHERE SteamID = @steamid AND Map = @map",
                new { steamid = SteamID, map });

                var existingEggs = existingEggsStr?
                    .Split(",", StringSplitOptions.RemoveEmptyEntries)
                    .Select(int.Parse)
                    .ToHashSet() ?? new HashSet<int>();

                var newEggs = eggsToSave.Where(egg => !existingEggs.Contains(egg)).ToList();
                if (newEggs.Count == 0) return;

                var combinedEggs = existingEggs
                    .Union(newEggs)
                    .OrderBy(x => x)
                    .ToList();

                var combinedEggsStr = string.Join(",", combinedEggs);

                if (userExist)
                {
                        Logger.LogInformation("Updating existing row!");
                        await connection.ExecuteAsync(
                        "UPDATE EggHunt SET EggID = @eggsString WHERE SteamID = @SteamID AND Map = @map AND Name = @name", 
                            new { eggsString = combinedEggsStr, SteamID, map, name});
                }
                else
                {
                        await connection.ExecuteAsync(
                        "INSERT INTO EggHunt (SteamID, Map, EggID, Name) VALUES (@steamid, @map, @eggsString, @name)",
                            new { steamid = SteamID, map, eggsString = combinedEggsStr, name});
                }
            }
            catch (Exception ex)
            {
                Logger.LogInformation($"{ex}");
            }
            finally
            {
                Players.Remove(SteamID, out var _);
            }
        }

        public async Task SaveAllEggs()
        {
            Logger.LogInformation("Saving all players into DB");
            try
            {
                await using var connection = new MySqlConnection(DBConnection);
                await connection.OpenAsync();
                foreach (var p in Players) {
                    var sid = p.Key;
                    var player = p.Value;
                    var map = p.Value.map;
                    var eggs = p.Value.eggs;

                    if (player == null) continue;

                    var combinedEggs = string.Join(",", eggs);
                    var userExist = await UserExist(sid, map);
                    var userExistKill = await UserExistKill(sid);

                    if (userExist) {
                        await connection.ExecuteAsync(
                        "UPDATE EggHunt SET EggID = @eggsString WHERE SteamID = @sid AND Map = @map",
                            new { eggsString = combinedEggs, sid, map });
                    }
                    else
                    {
                        await connection.ExecuteAsync(
                        "INSERT INTO EggHunt (SteamID, Map, EggID, Name) VALUES (@sid, @map, @eggsString)",
                            new { sid, map, eggsString = combinedEggs });
                    }

                    if (userExistKill)
                    {
                        await connection.ExecuteAsync(
                        "UPDATE EggKills SET EggsPicked = @eggs WHERE SteamID = @sid",
                            new { sid, eggs = p.Value.killeggs });
                    }
                    else
                    {
                        await connection.ExecuteAsync(
                        "INSERT INTO EggKills (SteamID, EggsPicked) VALUES (@sid, eggs)",
                            new { sid, eggs = p.Value.killeggs });
                    }

                }
            }
            catch (Exception ex)
            {
                Logger.LogInformation(ex.ToString());
            }
            return;
        }

        public async Task GetTop(string map)
        {
            try
            {
                await using var connection = new MySqlConnection(DBConnection);
                await connection.OpenAsync();

                var topEggsResult = await connection.QueryAsync<(string Name, int TotalEggs)>(
                    @"SELECT `Name`, 
                     SUM(LENGTH(EggID) - LENGTH(REPLACE(EggID, ',', '')) + 1) AS TotalEggs 
              FROM `EggHunt` 
              WHERE `EggID` IS NOT NULL AND `EggID` <> '' 
              GROUP BY `SteamID`, `Name` 
              ORDER BY TotalEggs DESC 
              LIMIT 5;"
                );

                if (topEggsResult != null)
                {
                    _TopEggsCache = topEggsResult.ToDictionary(row => row.Name, row => row.TotalEggs);
                }

                var topKillEggsResult = await connection.QueryAsync<(string Name, int EggsPicked)>(
                    @"SELECT `Name`, `EggsPicked` 
              FROM `EggKills` 
              WHERE `Map` = @map 
              ORDER BY `EggsPicked` DESC 
              LIMIT 5;",
                    new { map }
                );
                
                if(topKillEggsResult != null)
                {
                    _TopKillEggsCache = topKillEggsResult.ToDictionary(row => row.Name, row => row.EggsPicked);
                }

            }
            catch (Exception ex)
            {
                Logger.LogInformation(ex.ToString());
            }
        }


    }
}
