using UnityEditor;
public static class DemonBundleImport
{
    public static void Run()
    {
        BeetleBundleImport.ImportProfile("demon_helheim_sur", "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/DemonHelheim", new[] {"demon_hellheim_idle","demon_hellheim_walk","demon_hellheim_attack_noevent","demon_hellheim_init","demon_hellheim_stun"});
        EditorApplication.Exit(0);
    }
}
