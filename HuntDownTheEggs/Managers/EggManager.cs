using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using HuntDownTheEggs.Core;
using HuntDownTheEggs.Extensions;
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
        public readonly Dictionary<uint, CDynamicProp> _eggEntities = [];
        public List<CDynamicProp> _testEggsEntities = [];
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
            var e = 0; 
            if (_plugin.Config.ModesSetup.SpawnRandomEggs)
            {
                _plugin.Logger.LogInformation("Spawning random eggs");
                for (int i = 0; i < _plugin.Config.ModesSetup.NumberOfRandomEggs; i++)
                {
                    Vector? randomPos = NavMesh.GetRandomPosition();

                    if (randomPos == null)
                    {
                        _plugin.DebugLog($"Failed to get {randomPos}. Times: {i}");
                        continue;
                    }
                    e++;
                    SpawnEgg(new Vector(randomPos.X, randomPos.Y, randomPos.Z), "default", "$random_egg");
                    _plugin.DebugLog($"Spawned egg on: {randomPos.X} {randomPos.Y} {randomPos.Z} | Number: {e}");
                    
                }
            }

            foreach (var egg in _eggs)
            {
                SpawnEgg(new Vector(egg.X, egg.Y, egg.Z), egg.ModelColor, $"letegg-{_mapName}_${egg.Id}");
            }
        }

        public void CheckIfEggsAreThere()
        {
            if (_eggs.Count == 0 || _eggs == null)
            {
                _plugin.DebugLog("Cannot find any eggs! Trying to fetch data again!");
                try
                {
                    _plugin.DebugLog($"Deserializng {_mapName}.json file again!");
                    LoadEggsFromMap();
                    if (_eggs!.Count == 0 || _eggs == null)
                    {
                        _plugin.DebugLog("Still cannot find any eggs!");
                    }
                }
                catch (Exception ex)
                {
                    _plugin.DebugLog(ex.ToString());
                }
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
            var entity = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic_override");

            if (_plugin.Config.ModesSetup.ShootEggMode)
            {
                entity!.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_WEAPON;
                entity.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
                entity!.TakesDamage = true;
            }


            if (color == null)
            {
                color = "default";
            }

            position = new Vector(position.X, position.Y, position.Z + _plugin.Config.EggSetup.EggModelHeight);

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

            entity!.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2));

            // Configure egg entity
            entity.SetModel(_plugin.Config.EggSetup.EggModel);

            entity!.DispatchSpawn();

            if (_plugin.Config.ModesSetup.ShootEggMode)
            {
                entity.Health = _plugin.Config.ModesSetup.ShootEggHealth;
                entity.MaxHealth = _plugin.Config.ModesSetup.ShootEggHealth;
                Utilities.SetStateChanged(entity, "CBaseEntity", "m_iHealth");
            }

            entity.Teleport(position);

            entity.UseAnimGraph = false;

            // Set animation if configured
            if (!string.IsNullOrWhiteSpace(_plugin.Config.EggSetup.EggAnimation))
            {
                entity.AcceptInput("SetAnimation", value: _plugin.Config.EggSetup.EggAnimation);
            }

            entity.AcceptInput("Enable");

            // Scale the egg
            entity!.CBodyComponent!.SceneNode!.GetSkeletonInstance().Scale = _plugin.Config.EggSetup.EggModelScale;
            entity.Entity!.Name = $"pack-{name}";

            // Apply glow if enabled

            if (_plugin.Config.EggSetup.Glowing)
            {
                if (Enum.TryParse<KnownColor>(_plugin.Config.EggSetup.GlowingColor, true, out var knownColor))
                {
                    Color colorGlow = Color.FromKnownColor(knownColor);
                    PluginUtilities.SetGlowOnEntity(entity, colorGlow, _plugin.Config.EggSetup.GlowingRange);
                }
                else
                {
                    PluginUtilities.SetGlowOnEntity(entity, Color.Green, _plugin.Config.EggSetup.GlowingRange);
                }
            }

            if (_plugin.Config.ModesSetup.ShootEggMode)
            {
                _testEggsEntities.Add(entity);
                return;
            }

            // Create trigger for egg
            CreateEggTrigger(entity, position);
        }

        public string GetRandomEggType()
        {
            double roll = _random.NextDouble() * 100.0;
            double cumulative = 0;

            foreach (var kv in _plugin.Config.PrizeSetup.EggsTypes)
            {
                cumulative += kv.Value.Chance;
                if (roll <= cumulative) return kv.Key;
            }

            return _plugin.Config.PrizeSetup.EggsTypes.Keys.First();
        }

        public (string ID, string Command) GetRandomPrize(string typeName)
        {
            var type = _plugin.Config.PrizeSetup.EggsTypes[typeName];
            var commands = type.Rewards;
            var randomEntry = commands.ElementAt(_random.Next(0, commands.Count));
            return (randomEntry.Key, randomEntry.Value);
        }

        public void GiveEggPrize(CCSPlayerController controller)
        {
            if (controller == null || controller.PlayerPawn.Value == null || !controller.PlayerPawn.IsValid || controller.IsBot || controller.IsHLTV)
                return;

            if (_plugin.Config.PrizeSetup.ReceivePrize == false || _plugin.Config.PrizeSetup.EggsTypes.Count() == 0)
            {
                controller.PrintToChat($"{_plugin.Localizer["prefix"]}{_plugin.Localizer["pickedEggNoPrize"]}");
                return;
            }

            var randomType = GetRandomEggType();
            if (randomType == null) return;

            var randomPrize = GetRandomPrize(randomType);
            var desc = randomPrize.ID.Replace(" ", "_");

            controller.PrintToChat($"{_plugin.Localizer["prefix"]}{{{_plugin.Config.PrizeSetup.EggsTypes[randomType].Color}}}{_plugin.Localizer["recievedPrize", randomPrize.ID, randomType]}".ReplaceColorTags());

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

            float chance = _plugin.Config.ModesSetup.ChanceToSpawn;
            float roll = (float)(_random.NextDouble() * 100);

            if (roll <= chance)
            {
                Vector position = new Vector(
                    controller.PlayerPawn.Value.AbsOrigin!.X,
                    controller.PlayerPawn.Value.AbsOrigin.Y,
                    controller.PlayerPawn.Value.AbsOrigin.Z + _plugin.Config.EggSetup.EggModelHeight
                );

                SpawnEgg(position, "default", $"$kill_{controller.UserId}");
            }
        }

        public void AddNewEgg(CCSPlayerController controller, string modelColor)
        {
            if (!_plugin.Config.ModesSetup.SearchMode)
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
                    Z = pos.Z + _plugin.Config.EggSetup.EggModelHeight,
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
            var eggToRemove = _eggs.FirstOrDefault(e => e.Id == id);
            if (eggToRemove == null) return;

            _eggs.Remove(eggToRemove);

            _plugin.Logger.LogInformation($"Usuwam jajko o Id={id}");
        }

        public void SaveIfRemovedEggs()
        {
            lock (_fileLock)
            {
                for (int i = 0; i < _eggs.Count; i++)
                {
                    _eggs[i].Id = i;
                }
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(_mapFilePath, JsonSerializer.Serialize(_eggs, options));
            }
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

        public void RemoveEggEntityOnShoot(CDynamicProp entity)
        {
            entity.Remove();

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
            if (!_plugin.Config.ModesSetup.SearchMode)
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
            if (!_plugin.Config.ModesSetup.SearchMode)
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
                    _plugin.DebugLog($"Loaded {_eggs.Count} eggs from {_mapName}.json");
                }
            }
        }

        private void CreateEggTrigger(CDynamicProp entity, Vector position)
        {
            var trigger = Utilities.CreateEntityByName<CTriggerMultiple>("trigger_multiple")!;

            if (trigger == null)
            {
                _plugin.DebugLog("Failed to create trigger entity for egg!");
                return;
            }

            trigger.Entity!.Name = entity.Entity!.Name + "_trigger";
            trigger.Spawnflags = (1 << 0) | (1 << 1); // Touch and Use
            trigger.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= ~(uint)(1 << 2);
            trigger.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
            trigger.Collision.SolidFlags = 0;
            trigger.Collision.CollisionGroup = 14;

            trigger.DispatchSpawn();

            if (_plugin.Config.EggSetup.TriggerModel != _plugin.Config.EggSetup.EggModel || !string.IsNullOrEmpty(_plugin.Config.EggSetup.TriggerModel))
            {
                trigger.SetModel(_plugin.Config.EggSetup.TriggerModel);
            }
            else
            {
                trigger.SetModel(_plugin.Config.EggSetup.EggModel);
            }

            trigger.CBodyComponent.SceneNode.GetSkeletonInstance().Scale = _plugin.Config.EggSetup.EggModelScale;
            trigger.Teleport(position);
            trigger.AcceptInput("SetParent", entity, trigger, "!activator");
            trigger.AcceptInput("Enable");


            if (_eggEntities.ContainsKey(trigger.Index))
            {
                _eggEntities.Remove(trigger.Index);
            }


            _eggEntities.Add(trigger.Index, entity);
        }

        public void RemoveEntityOnShoot(CDynamicProp dynamicprop, CEntityInstance entities, CCSPlayerController player, int eggType)
        {
            // eggtype: 0 - Kill, 1 - Placed by admin, 2- Random spawned eggs
            
            var steamId = player.AuthorizedSteamID?.SteamId64 ?? player.SteamID;
            if (dynamicprop.Health <= 0)
            {
                _testEggsEntities.Remove(dynamicprop);

                entities.Remove();
                entities.AcceptInput("kill");

                if (eggType == 0)
                {
                    _plugin.PlayerManager!.IncrementKillEggs(steamId);
                    player.PrintToChat($"{_plugin.Localizer["prefix"]}{_plugin.Localizer["killEgg"]}");
                }
                else if (eggType == 1)
                {
                    HandleEggPickup(player, dynamicprop.Entity!.Name);
                }
                else if (eggType == 2)
                {
                    _plugin.PlayerManager!.IncrementRandomEggs(steamId);
                    player.PrintToChat($"{_plugin.Localizer["prefix"]}{_plugin.Localizer["randomEgg"]}");
                }

                GiveEggPrize(player);
            }

        }


    }
}