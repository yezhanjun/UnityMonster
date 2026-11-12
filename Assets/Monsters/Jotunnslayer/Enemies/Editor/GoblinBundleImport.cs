using UnityEditor;
public static class GoblinBundleImport
{
    public static void Run()
    {
        BeetleBundleImport.ImportProfile("goblin_melee_sur","Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/GoblinMelee",new[]{"goblin_melee_idle","goblin_melee_run","goblin_melee_attack","goblin_melee_summon","goblin_melee_stun"});
        BeetleBundleImport.ImportProfile("goblin_ranged_sur","Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/GoblinRanged",new[]{"goblin_ranged_idle","goblin_ranged_run","goblin_ranged_attack","goblin_ranged_summon","goblin_ranged_stun"});
        EditorApplication.Exit(0);
    }
}
