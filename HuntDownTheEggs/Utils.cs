using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Text.Json;
using CounterStrikeSharp.API.Modules.Utils;


namespace HuntDownTheEggs
{
    public partial class HuntDownTheEggs
    {
        public void GenerateFile()
        {
            if(!Config.SearchMode)
            {
                DebugMode("Search mode is turned off! No need to create map file!");
                return;
            }
            

            string path = Path.Combine(ModuleDirectory, "maps");
            mapName ??= Server.MapName;
            string file = Path.Combine(path, $"{mapName}.json");

            try
            {
                if (!Directory.Exists(path))
                {
                    DebugMode("Folder 'maps' not found! Creating one!");
                    Directory.CreateDirectory(path);
                }

                if (!File.Exists(file))
                {
                    DebugMode("File does not exist! Creating one!");
                    File.WriteAllText(file, "[]");
                }
            }
            catch (Exception ex)
            {
                Logger.LogInformation($"Error creating folder: {ex}");
            }
        }

        /*
         * I tried to make some changes to at least create the impression that I did something myself buuut, 
         * it was breaking a lot or even not working at all.
         * So I had mental break-dance and yoinked CreateTrigger & GeneratePresent almost 1:1 from:
         * https://github.com/exkludera/cs2-gift-packages/blob/main/src/Gift.cs#L10
         * But adapted the code for this plugin
         * 
         * ty exkludera :3333
         */

        public void CreateTrigger(CDynamicProp entity, Vector position)
        {
            var trigger = Utilities.CreateEntityByName<CTriggerMultiple>("trigger_multiple")!;

            trigger.Entity!.Name = entity.Entity!.Name + "_trigger";
            trigger.Spawnflags = 1;
            trigger.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= ~(uint)(1 << 2);
            trigger.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
            trigger.Collision.SolidFlags = 0;
            trigger.Collision.CollisionGroup = 14;

            trigger.SetModel("models/chicken/chicken.vmdl");
            
            trigger.DispatchSpawn();
            
            trigger.CBodyComponent.SceneNode.GetSkeletonInstance().Scale = Config.EggModelScale;
            
            trigger.Teleport(new Vector(position.X, position.Y, position.Z));
            trigger.AcceptInput("FollowEntity", entity, trigger, "!activator");
            trigger.AcceptInput("Enable");

            if (Presents.ContainsKey(trigger.Index))
            {
                Presents.Remove(trigger.Index);
            }

            Presents.Add(trigger.Index, entity);
        }
        public void GeneratePresent(Vector cords, string color, string name)
        {
            var entity = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
            if (color == null)
            {
                color = "default";
            }

            if (color != "default")
            {
                if (color == "r")
                {
                    var knownColors = Enum.GetValues(typeof(KnownColor))
                        .Cast<KnownColor>()
                        .Where(c =>
                            c != KnownColor.Transparent &&
                            !Color.FromKnownColor(c).IsSystemColor)
                        .ToList();

                    var random = new Random();
                    var randomColor = knownColors[random.Next(knownColors.Count)];

                    var customColor = Color.FromKnownColor(randomColor);
                    entity!.Render = customColor;
                }
                else if (Enum.TryParse<KnownColor>(color, true, out var knownColor))
                {
                    var customColor = Color.FromKnownColor(knownColor);
                    entity!.Render = customColor;
                }
                else
                {
                    Server.PrintToChatAll("Not correct color!");
                    return;
                }
            }

            entity!.DispatchSpawn();
            entity.SetModel(Config.EggModel);

            entity.Teleport(new Vector(cords.X, cords.Y, cords.Z));

            entity.UseAnimGraph = false;

            if(!string.IsNullOrWhiteSpace(Config.EggAnimation))
            {
                entity.AcceptInput("SetAnimation", value: Config.EggAnimation);
            }

            entity!.CBodyComponent!.SceneNode!.GetSkeletonInstance().Scale = Config.EggModelScale;

            entity.Entity!.Name = $"pack-{name}";

            if (Config.Glowing)
            {
                if (Enum.TryParse<KnownColor>(Config.GlowingColor, true, out var knownColor))
                {
                    Color colorGlow = Color.FromKnownColor(knownColor);
                    SetGlowOnEntity(entity, colorGlow, Config.GlowingRange);
                }
                else
                {
                    SetGlowOnEntity(entity, Color.Green, Config.GlowingRange);
                }
            }
            


            CreateTrigger(entity, new Vector(cords.X, cords.Y, cords.Z));
        }

        public void SerializeJsonFromMap()
        {
            if (!Config.SearchMode)
            {
                DebugMode("Search mode is turned off! No need to serialize map file!");
                return;
            }
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    presents = JsonSerializer.Deserialize<List<EggsData>>(json) ?? new();
                }
            }
        }
        public void WritePresentCords(CCSPlayerController controller, string modelColor)
        {
            if (!Config.SearchMode)
            {
                DebugMode("Search mode is turned off! No need to write cords into file!");
                return;
            }
            if (controller?.PlayerPawn?.Value == null) return;
            if (filePath == null)
            {
                DebugMode("filePath variable is empty!");
                return;
            }

            if (modelColor == null)
            {
                modelColor = "default";
            }

            lock(fileLock)
            {
                var pos = controller.PlayerPawn.Value.AbsOrigin;

                int newId = presents.Count;

                presents.Add(new EggsData
                {
                    Id = newId,
                    X = pos!.X,
                    Y = pos.Y,
                    Z = pos.Z + Config.EggModelHeight,
                    modelColor = modelColor
                });

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(filePath, JsonSerializer.Serialize(presents, options));

                SerializeJsonFromMap();
                DebugMode($"Saved present with ID: {newId} = {pos}\"");
                    
                return;
            }
        }

        public void RemovePresents()
        {
            if (Presents == null) return;

            foreach (var present in Presents)
            {
                if (present.Value != null && present.Value.IsValid)
                {
                    present.Value.Remove();
                }
            }

            Presents.Clear();
            DebugMode("RemovePresents - Removed all presents from map");
                

        }

        public void PickedUpPresent(CCSPlayerController controller, string eggName)
        {
            try
            {
                Server.NextFrameAsync(() =>
                {
                    try
                    {
                        var steamid = controller.SteamID;
                        if (steamid == 0)
                        {
                            DebugMode("Player with wrong steamid!");
                            return;
                        }

                        string[] parts = eggName.Split("$");
                        if (parts.Length < 2)
                        {
                            DebugMode($"Issue with egg: {eggName}");
                            return;
                        }

                        var eggid = int.Parse(parts[1]);

                        CheckIfPlayer(steamid);

                        if (!Players[steamid].eggs.Contains(eggid))
                        {
                            Players[steamid].eggs.Add(eggid);
                        }
                    }
                    catch (Exception ex) {
                        Logger.LogInformation(ex.ToString());
                    }


                });

            }
            catch (Exception e)
            {
                Logger.LogInformation($"{e}");
            }
        }
        public async Task CheckIfPlayer(ulong SteamID)
        {
            if (!Players.ContainsKey(SteamID))
            {
                var user = await GetPlayerEggs(SteamID, mapName!);
                if (user == null) return;

                Players[SteamID] = new PlayerEggs
                {
                    steamid = user!.steamid,
                    map = user.map,
                    eggs = user.eggs,
                    killeggs = user.killeggs
                };
            }
            return;
        }

        public static string Replace(string input, CCSPlayerController controller)
        {
            return input
                .Replace("{USERID}", controller.UserId.ToString())
                .Replace("{STEAMID}", controller.AuthorizedSteamID!.SteamId2.ToString())
                .Replace("{STEAMID3}", controller.AuthorizedSteamID!.SteamId3.ToString())
                .Replace("{STEAMID64}", controller.AuthorizedSteamID!.SteamId64.ToString())
                .Replace("{NAME}", controller.PlayerName)
                .Replace("{SLOT}", controller.Slot.ToString());
        }

        public static string ReplaceMSG(string input)
        {
            return input.Replace("\n", "\u2029");
        }

        /*
         * Not sure who made this method. Was sent by my friend but I found this on CS# Discord:
         * https://discord.com/channels/1160907911501991946/1311638450881167523/1311973008122052618
         * So much thanks Dliix66 for sharing this method.
         */
        public static void SetGlowOnEntity(CBaseEntity? entity, Color GlowColor, int Range)
        {
            if (entity == null || !entity.IsValid)
                return;

            CDynamicProp Glow = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic")!;
            Glow.Spawnflags = 256;
            Glow.Render = Color.Transparent;
            Glow.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags = (uint)(Glow.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags & ~(1 << 2));
            Glow.SetModel(entity.CBodyComponent!.SceneNode!.GetSkeletonInstance().ModelState.ModelName);
            Glow.DispatchSpawn();
            
            Glow.Glow.GlowColorOverride = GlowColor;
            Glow.Glow.GlowRange = Range;
            Glow.Glow.GlowRangeMin = 0;
            Glow.Glow.GlowTeam = -1; // -1 = Both, 2 = T, 3 = CT
            Glow.Glow.GlowType = 3;

            Glow.Teleport(entity.AbsOrigin, entity.AbsRotation, entity.AbsVelocity);
            Glow.AcceptInput("SetParent", entity, Glow, "!activator");
        }

        private void DebugMode(string input)
        {
            if(Config.Debug)
            {
                Logger.LogInformation(input);
            }
            return;
        }

    }
}
