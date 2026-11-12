using UnityEditor;
public static class ElementalWarriorBundleImport
{
    public static void RunVariants()
    {
        var clips=new[]{"ele_warrior_init","ele_warrior_stand","ele_warrior_walk","elem_mb_melee_summon_plc"};
        BeetleBundleImport.ImportProfile("ele_warrior_green_sur","Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/ElementalWarriorGreen",clips);
        BeetleBundleImport.ImportProfile("ele_warrior_hel_sur","Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/ElementalWarriorHel",clips);
        EditorApplication.Exit(0);
    }
    public static void Run()
    {
        var clips=new[]{"ele_warrior_init","ele_warrior_stand","ele_warrior_walk","elem_mb_melee_summon_plc"};
        BeetleBundleImport.ImportProfile("ele_warrior_sur","Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/ElementalWarrior",clips);
        BeetleBundleImport.ImportProfile("ele_warrior_runner_sur","Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/ElementalRunner",clips);
        EditorApplication.Exit(0);
    }
}
