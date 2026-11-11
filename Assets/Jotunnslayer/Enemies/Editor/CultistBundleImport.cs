using UnityEditor;
public static class CultistBundleImport
{
    public static void Run()
    {
        const string root="Assets/Jotunnslayer/Enemies/Resources/Jotunnslayer/";
        BeetleBundleImport.ImportProfile("cultist_1h_sur",root+"CultistMelee",new[]{"1h_stand_combat","1h_walk_combat","1h_attack_04","dualwield_ec_appear_summon","1h_hit"});
        BeetleBundleImport.ImportProfile("cultist_bow_sur",root+"CultistBow",new[]{"bow_stand_attack","bow_walk","bow_attack_01","bow_init","bow_hit"});
        EditorApplication.Exit(0);
    }
}
