using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Utils;

namespace HuntDownTheEggs
{
    public partial class HuntDownTheEggs
    {
        public string GetRandomPresentType(Dictionary<string, EggsTypeConfig> presentTypes)
        {
            var rnd = new Random();
            double roll = rnd.NextDouble() * 100.0;
            double cumulative = 0;
            foreach (var kv in presentTypes)
            {
                cumulative += kv.Value.Chance;
                if (roll <= cumulative) return kv.Key;
            }
            return presentTypes.Keys.First();
        }
        public (string ID, string Command) RandomizePrize(string typeName)
        {
            var type = Config.EggsTypes[typeName];
            var commands = type.Rewards;
            var randomEntry = commands.ElementAt(new Random().Next(0, commands.Count));
            return (randomEntry.Key, randomEntry.Value);
        }

        public void GivePrize(CCSPlayerController controller)
        {
            if(controller == null || controller.PlayerPawn.Value == null || !controller.PlayerPawn.IsValid || controller.IsBot || controller.IsHLTV) return;
            var userid = controller.UserId;
            
            if(Config.ReceivePrize == false)
            {
                controller.PrintToChat($"{Localizer["prefix"]}{Localizer["pickedEggNoPrize"]}");
                return;
            }

            var RandomType = GetRandomPresentType(Config.EggsTypes);
            if(RandomType == null) return;
            var RandomPrize = RandomizePrize(RandomType);
            var desc = RandomPrize.ID.Replace(" ", "_");
            controller.PrintToChat($"{Localizer["prefix"]}{{{Config.EggsTypes[RandomType].Color}}}{Localizer["recievedPrize", RandomPrize.ID, RandomType]}".ReplaceColorTags());
            if(!Localizer[$"recievedDesc.{desc}"].ResourceNotFound)
            {
                controller.PrintToChat($"{Localizer["prefix"]}{Localizer[$"recievedDesc.{desc}"]}");
            }
            Server.ExecuteCommand(Replace(RandomPrize.Command, controller));
            return;
        }

        public void ChanceToSpawnEgg(CCSPlayerController controller)
        {
            if (controller == null || !controller.PlayerPawn.IsValid || controller.PlayerPawn.Value == null) return;
            float chance = Config.ChanceToSpawn;
            Random rng = new Random();
            float roll = (float)(rng.NextDouble() * 100);
            if(roll <= chance)
            {
                Vector position = new Vector(controller.PlayerPawn.Value.AbsOrigin!.X, controller.PlayerPawn.Value.AbsOrigin.Y, controller.PlayerPawn.Value.AbsOrigin.Z + Config.EggModelHeight);
                GeneratePresent(position, "default", "$kill");
                return;
            }
            
            return;
        }
    }
}
