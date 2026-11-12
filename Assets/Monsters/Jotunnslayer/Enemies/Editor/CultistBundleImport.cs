using UnityEditor;
public static class CultistBundleImport
{
    public static void RunRemaining()
    {
        const string root="Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/";
        var elite=new[]{"1h_stand_combat","1h_walk","1h_attack_04_nofire","skeleton_summon_ground","1h_hit","whirlwind_loop"};
        BeetleBundleImport.ImportProfile("cultist_miniboss_sur",root+"CultistElite",elite);
        BeetleBundleImport.ImportProfile("cultist_miniboss_v02_sur",root+"CultistEliteV2",elite);
        BeetleBundleImport.ImportProfile("cultist_priest_sur",root+"CultistPriest",new[]{"staff_stand_attack","staff_walk","staff_attack_01","staff_init","staff_hit"});
        EditorApplication.Exit(0);
    }
    public static void Run()
    {
        const string root="Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/";
        BeetleBundleImport.ImportProfile("cultist_1h_sur",root+"CultistMelee",new[]{"1h_stand_combat","1h_walk_combat","1h_attack_04","dualwield_ec_appear_summon","1h_hit"});
        BeetleBundleImport.ImportProfile("cultist_bow_sur",root+"CultistBow",new[]{"bow_stand_attack","bow_walk","bow_attack_01","bow_init","bow_hit"});
        EditorApplication.Exit(0);
    }
}
