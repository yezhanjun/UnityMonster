using UnityEditor;
public static class DraugBundleImport
{
    public static void Run()
    {
        const string root = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/";
        BeetleBundleImport.ImportProfile("draug_sur", root + "Draug", new[] { "stand", "move", "attack_01", "summon_01" });
        BeetleBundleImport.ImportProfile("draug_crawler_sur", root + "DraugCrawler", new[] { "crawler_stand", "crawler_walk", "crawler_attack_01", "crawler_init", "crawler_stun" });
        BeetleBundleImport.ImportProfile("draug_thrower_sur", root + "DraugThrower", new[] { "stand", "move", "skill_throw", "summon_01" });
        EditorApplication.Exit(0);
    }
}
