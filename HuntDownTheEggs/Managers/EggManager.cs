using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Utils;
using HuntDownTheEggs.Core;
using HuntDownTheEggs.Models;
using HuntDownTheEggs.Utils;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Text.Json;

namespace HuntDownTheEggs
{
    public class EggManager
    {
        private readonly HuntDownTheEggsPlugin _plugin;
        private readonly Dictionary<uint, CDynamicProp> _eggEntities = [];
        private readonly List<EggData> _eggs = [];
        public string _mapName;
        public string _mapFilePath;
        private readonly Random _random = new();
        private static readonly object _fileLock = new();
        
        public bool PlacingMode { get; set; } = false;

        public EggManager(HuntDownTheEggsPlugin plugin)
        {
            _plugin = plugin;
            _mapName = Server.MapName;
            _mapFilePath = Path.Combine(_plugin.ModuleDirectory, "maps", $"{_mapName}.json");
            
            // Create necessary directories and files
            GenerateMapFile();
            
            // Load eggs from JSON file
            LoadEggsFromMap();
        }

        public void ClearEggs()
        {
            _eggEntities.Clear();
            _eggs.Clear();
            _plugin.DebugLog("Cleared all eggs from cache");
        }

        public void RemoveAllEggEntities()
        {
            if (_eggEntities == null) return;

            foreach (var egg in _eggEntities)
            {
                if (egg.Value != null && egg.Value.IsValid)
                {
                    egg.Value.Remove();
                }
            }

            _eggEntities.Clear();
            _plugin.DebugLog("Removed all egg entities from map");
        }

        public void SpawnAllEggs()
        {
            foreach (var egg in _eggs)
            {
                SpawnEgg(new Vector(egg.X, egg.Y, egg.Z), egg.ModelColor, $"{_mapName}_${egg.Id}");
            }
        }

        public void HandleEggPickup(CCSPlayerController controller, string eggName)
        {
            try
            {
                Server.NextFrameAsync(() =>
                {
                    try
                    {
                        ulong steamId = controller.SteamID;
                        if (steamId == 0)
                        {
                            _plugin.DebugLog("Player with invalid steamId!");
                            return;
                        }

                        string[] parts = eggName.Split("$");
                        if (parts.Length < 2)
                        {
                            _plugin.DebugLog($"Issue with egg name format: {eggName}");
                            return;
                        }

                        int eggId = int.Parse(parts[1]);

                        // Ensure player exists in player manager
                        _ = _plugin.PlayerManager!.EnsurePlayerExists(steamId, controller);

                        // Add egg to player's collection if not already collected
                        var playerData = _plugin.PlayerManager.GetPlayerData(steamId);
                        if (!playerData!.Eggs.Contains(eggId))
                        {
                            playerData.Eggs.Add(eggId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _plugin.Logger.LogInformation(ex.ToString());
                    }
                });
            }
            catch (Exception e)
            {
                _plugin.Logger.LogInformation($"{e}");
            }
        }

        public void SpawnEgg(Vector position, string? color, string name)
        {
            var entity = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
            
            if (color == null)
            {
                color = "default";
            }

            // Set egg color
            if (color != "default")
            {
                if (color == "r") // Random color
                {
                    var knownColors = Enum.GetValues(typeof(KnownColor))
                        .Cast<KnownColor>()
                        .Where(c =>
                            c != KnownColor.Transparent &&
                            !Color.FromKnownColor(c).IsSystemColor)
                        .ToList();

                    var randomColor = knownColors[_random.Next(knownColors.Count)];
                    entity!.Render = Color.FromKnownColor(randomColor);
                }
                else if (Enum.TryParse<KnownColor>(color, true, out var knownColor))
                {
                    entity!.Render = Color.FromKnownColor(knownColor);
                }
                else
                {
                    Server.PrintToChatAll("Invalid color specified!");
                    return;
                }
            }

            // Configure egg entity
            entity!.DispatchSpawn();
            entity.SetModel(_plugin.Config.EggModel);
            entity.Teleport(position);
            entity.UseAnimGraph = false;

            // Set animation if configured
            if (!string.IsNullOrWhiteSpace(_plugin.Config.EggAnimation))
            {
                entity.AcceptInput("SetAnimation", value: _plugin.Config.EggAnimation);
            }

            // Scale the egg
            entity!.CBodyComponent!.SceneNode!.GetSkeletonInstance().Scale = _plugin.Config.EggModelScale;
            entity.Entity!.Name = $"pack-{name}";

            // Apply glow if enabled
            if (_plugin.Config.Glowing)
            {
                if (Enum.TryParse<KnownColor>(_plugin.Config.GlowingColor, true, out var knownColor))
                {
                    Color colorGlow = Color.FromKnownColor(knownColor);
                    PluginUtilities.SetGlowOnEntity(entity, colorGlow, _plugin.Config.GlowingRange);
                }
                else
                {
                    PluginUtilities.SetGlowOnEntity(entity, Color.Green, _plugin.Config.GlowingRange);
                }
            }

            // Create trigger for egg
            CreateEggTrigger(entity, position);
        }
        
        public string GetRandomEggType()
        {
            double roll = _random.NextDouble() * 100.0;
            double cumulative = 0;
            
            foreach (var kv in _plugin.Config.EggsTypes)
            {
                cumulative += kv.Value.Chance;
                if (roll <= cumulative) return kv.Key;
            }
            
            return _plugin.Config.EggsTypes.Keys.First();
        }
        
        public (string ID, string Command) GetRandomPrize(string typeName)
        {
            var type = _plugin.Config.EggsTypes[typeName];
            var commands = type.Rewards;
            var randomEntry = commands.ElementAt(_random.Next(0, commands.Count));
            return (randomEntry.Key, randomEntry.Value);
        }
        
        public void GiveEggPrize(CCSPlayerController controller)
        {
            if (controller == null || controller.PlayerPawn.Value == null || !controller.PlayerPawn.IsValid || controller.IsBot || controller.IsHLTV) 
                return;
            
            if (_plugin.Config.ReceivePrize == false || _plugin.Config.EggsTypes.Count() == 0)
            {
                controller.PrintToChat($"{_plugin.Localizer["prefix"]}{_plugin.Localizer["pickedEggNoPrize"]}");
                return;
            }

            var randomType = GetRandomEggType();
            if (randomType == null) return;
            
            var randomPrize = GetRandomPrize(randomType);
            var desc = randomPrize.ID.Replace(" ", "_");
            
            controller.PrintToChat($"{_plugin.Localizer["prefix"]}{{{_plugin.Config.EggsTypes[randomType].Color}}}{_plugin.Localizer["recievedPrize", randomPrize.ID, randomType]}".ReplaceColorTags());
            
            if (!_plugin.Localizer[$"recievedDesc.{desc}"].ResourceNotFound)
            {
                controller.PrintToChat($"{_plugin.Localizer["prefix"]}{_plugin.Localizer[$"recievedDesc.{desc}"]}");
            }
            
            Server.ExecuteCommand(PluginUtilities.ReplacePlayerParameters(randomPrize.Command, controller));
        }
        
        public void TrySpawnDeathEgg(CCSPlayerController controller)
        {
            if (controller == null || !controller.PlayerPawn.IsValid || controller.PlayerPawn.Value == null) 
                return;
                
            float chance = _plugin.Config.ChanceToSpawn;
            float roll = (float)(_random.NextDouble() * 100);
            
            if (roll <= chance)
            {
                Vector position = new Vector(
                    controller.PlayerPawn.Value.AbsOrigin!.X, 
                    controller.PlayerPawn.Value.AbsOrigin.Y, 
                    controller.PlayerPawn.Value.AbsOrigin.Z + _plugin.Config.EggModelHeight
                );
                
                SpawnEgg(position, "default", "$kill");
            }
        }
        
        public void AddNewEgg(CCSPlayerController controller, string modelColor)
        {
            if (!_plugin.Config.SearchMode)
            {
                _plugin.DebugLog("Search mode is disabled! No need to save egg locations.");
                return;
            }
            
            if (controller?.PlayerPawn?.Value == null) return;
            
            lock (_fileLock)
            {
                var pos = controller.PlayerPawn.Value.AbsOrigin;
                if (pos == null) return;

                int newId = _eggs.Count;
                
                // Add egg to list
                _eggs.Add(new EggData
                {
                    Id = newId,
                    X = pos.X,
                    Y = pos.Y,
                    Z = pos.Z + _plugin.Config.EggModelHeight,
                    ModelColor = modelColor ?? "default"
                });

                // Save to file
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(_mapFilePath, JsonSerializer.Serialize(_eggs, options));

                // Reload eggs from file
                LoadEggsFromMap();
                
                _plugin.DebugLog($"Saved egg with ID: {newId} at position: {pos}");
            }
        }

        public void RemoveEgg(int id)
        {
            if (id < 0 || id >= _eggs.Count) return;
            
            _eggs.RemoveAt(id);

            // Re-index eggs
            for (int i = 0; i < _eggs.Count; i++)
            {
                _eggs[i].Id = i;
            }

            // Save updated eggs list
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_mapFilePath, JsonSerializer.Serialize(_eggs, options));
        }
        
        public bool HasEgg(uint triggerIndex)
        {
            return _eggEntities.ContainsKey(triggerIndex);
        }
        
        public CDynamicProp GetEgg(uint triggerIndex)
        {
            return _eggEntities[triggerIndex];
        }
        
        public void RemoveEggEntity(uint triggerIndex, CTriggerMultiple trigger)
        {
            if (_eggEntities.TryGetValue(triggerIndex, out var eggEntity))
            {
                eggEntity.Remove();
                trigger.Remove();
                _eggEntities.Remove(triggerIndex);
            }
        }
        
        public int GetEggCount()
        {
            return _eggs.Count;
        }
        
        public EggData? GetEggById(int id)
        {
            return id >= 0 && id < _eggs.Count ? _eggs[id] : null;
        }

        private void GenerateMapFile()
        {
            if (!_plugin.Config.SearchMode)
            {
                _plugin.DebugLog("Search mode is disabled! No need to create map file.");
                return;
            }

            string directoryPath = Path.Combine(_plugin.ModuleDirectory, "maps");

            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    _plugin.DebugLog("Folder 'maps' not found! Creating one!");
                    Directory.CreateDirectory(directoryPath);
                }

                if (!File.Exists(_mapFilePath))
                {
                    _plugin.DebugLog("Map file does not exist! Creating one!");
                    File.WriteAllText(_mapFilePath, "[]");
                }
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogInformation($"Error creating folder or file: {ex}");
            }
        }

        private void LoadEggsFromMap()
        {
            if (!_plugin.Config.SearchMode)
            {
                _plugin.DebugLog("Search mode is disabled! No need to load map file.");
                return;
            }
            
            if (File.Exists(_mapFilePath))
            {
                string json = File.ReadAllText(_mapFilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    _eggs.Clear();
                    var loadedEggs = JsonSerializer.Deserialize<List<EggData>>(json) ?? [];
                    _eggs.AddRange(loadedEggs);
                }
            }
        }

        private void CreateEggTrigger(CDynamicProp entity, Vector position)
        {
            var trigger = Utilities.CreateEntityByName<CTriggerMultiple>("trigger_multiple")!;

            trigger.Entity!.Name = entity.Entity!.Name + "_trigger";
            trigger.Spawnflags = 1;
            trigger.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= ~(uint)(1 << 2);
            trigger.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
            trigger.Collision.SolidFlags = 0;
            trigger.Collision.CollisionGroup = 14;

            trigger.SetModel(_plugin.Config.EggModel);
            
            trigger.DispatchSpawn();
            
            trigger.CBodyComponent.SceneNode.GetSkeletonInstance().Scale = _plugin.Config.EggModelScale;
            
            trigger.Teleport(position);
            trigger.AcceptInput("FollowEntity", entity, trigger, "!activator");
            trigger.AcceptInput("Enable");

            if (_eggEntities.ContainsKey(trigger.Index))
            {
                _eggEntities.Remove(trigger.Index);
            }

            _eggEntities.Add(trigger.Index, entity);
        }
    }
}