namespace MTPSKIT.Gameplay.Gamemodes
{
    public static class RoomSetup
    {
        public static RoomProperties Properties;
    }
    public struct RoomProperties
    {
        //p for property
        public Gamemodes P_Gamemode;
        public int P_MaxPlayers;
        public bool P_FillEmptySlotsWithBots;
        public string P_Map;
        public int P_GameDuration;
        public float P_RespawnCooldown;
    }
}
