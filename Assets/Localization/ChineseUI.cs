namespace MTPSKIT
{
    public static class ChineseUI
    {
        public static string GamemodeName(string mode)
        {
            switch (mode)
            {
                case "Battlefield": return "战场模式";
                case "Range": return "靶场训练";
                case "DeathMatch":
                case "Deathmatch": return "个人竞技";
                case "TeamDeathMatch":
                case "TeamDeathmatch": return "团队竞技";
                case "None": return "训练 / 默认";
                default: return mode;
            }
        }
    }
}

