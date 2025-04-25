namespace HuntDownTheEggs.Models
{
    public class PlayerData
    {
        public ulong SteamId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string Map { get; set; } = string.Empty;
        public List<int> Eggs { get; set; } = [];
        public int KillEggs { get; set; } = 0;
        public int TotalEggs { get; set; } = 0;
    }
}