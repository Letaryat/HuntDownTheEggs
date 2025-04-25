using CounterStrikeSharp.API.Core;
using HuntDownTheEggs.Core;
using HuntDownTheEggs.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace HuntDownTheEggs
{
    public class PlayerManager(HuntDownTheEggsPlugin plugin)
    {
        private readonly HuntDownTheEggsPlugin _plugin = plugin;
        private readonly ConcurrentDictionary<ulong, PlayerData> _players = new();
        private readonly Dictionary<string, int> _topEggsCache = [];
        private readonly Dictionary<string, int> _topKillEggsCache = [];

        public void ClearPlayers()
        {
            _players.Clear();
        }

        public PlayerData? GetPlayerData(ulong steamId)
        {
            return _players.TryGetValue(steamId, out var playerData) ? playerData : null;
        }

        public async Task AddPlayerAsync(ulong steamId, string playerName)
        {
            _plugin.DebugLog($"Client authorization: {steamId}");
            
            var existingData = await _plugin.DatabaseManager!.GetPlayerEggsAsync(steamId, _plugin.EggManager!._mapName);
            
            if (existingData == null)
            {
                _players[steamId] = new PlayerData
                {
                    SteamId = steamId,
                    PlayerName = playerName,
                    Map = _plugin.EggManager._mapName,
                    Eggs = [],
                    KillEggs = 0
                };
            }
            else
            {
                _players[steamId] = new PlayerData
                {
                    SteamId = existingData.SteamId,
                    PlayerName = playerName,
                    Map = existingData.Map,
                    Eggs = existingData.Eggs,
                    KillEggs = existingData.KillEggs,
                    TotalEggs = existingData.TotalEggs
                };
            }
        }

        public void IncrementKillEggs(ulong steamId)
        {
            if (_players.TryGetValue(steamId, out var playerData))
            {
                playerData.KillEggs++;
            }
        }

        public async Task EnsurePlayerExists(ulong steamId, CCSPlayerController controller)
        {
            if (!_players.ContainsKey(steamId))
            {
                var userData = await _plugin.DatabaseManager!.GetPlayerEggsAsync(steamId, _plugin.EggManager!._mapName);
                if (userData == null) return;

                _players[steamId] = new PlayerData
                {
                    SteamId = userData.SteamId,
                    PlayerName = controller.PlayerName,
                    Map = userData.Map,
                    Eggs = userData.Eggs,
                    KillEggs = userData.KillEggs
                };
            }
        }

        public async Task SavePlayerAsync(ulong steamId, string playerName)
        {
            if (_players.TryGetValue(steamId, out var playerData))
            {
                playerData.PlayerName = playerName;
                
                try 
                {
                    // Just Debug bullshit (just in case)
                    _plugin.DebugLog($"Saving player {playerName} (SteamID: {steamId}) with {playerData.KillEggs} kill eggs and {playerData.Eggs.Count} regular eggs on map {playerData.Map}");
                    
                    await _plugin.DatabaseManager!.SavePlayerEggsAsync(playerData);
                    
                    _players.TryRemove(steamId, out _);
                }
                catch (Exception ex)
                {
                    _plugin.Logger.LogInformation($"Error saving player data: {ex}");
                }
            }
            else
            {
                _plugin.DebugLog($"No player data to save for {playerName} (SteamID: {steamId})");
            }
        }

        public async Task SaveAllPlayersAsync()
        {
            _plugin.DebugLog("Saving all players to database");
            
            try
            {
                foreach (var player in _players)
                {
                    await _plugin.DatabaseManager!.SavePlayerEggsAsync(player.Value);
                }
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogInformation($"Error saving players: {ex}");
            }
        }

        public async Task LoadTopPlayersAsync(string map)
        {
            try
            {
                var topEggs = await _plugin.DatabaseManager!.GetTopEggsAsync();
                if (topEggs != null)
                {
                    _topEggsCache.Clear();
                    foreach (var entry in topEggs)
                    {
                        _topEggsCache[entry.Key] = entry.Value;
                    }
                }

                var topKillEggs = await _plugin.DatabaseManager.GetTopKillEggsAsync(map);
                if (topKillEggs != null)
                {
                    _topKillEggsCache.Clear();
                    foreach (var entry in topKillEggs)
                    {
                        _topKillEggsCache[entry.Key] = entry.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogInformation($"Error loading top players: {ex}");
            }
        }

        public Dictionary<string, int> GetTopEggs()
        {
            return _topEggsCache;
        }

        public Dictionary<string, int> GetTopKillEggs()
        {
            return _topKillEggsCache;
        }
    }
}