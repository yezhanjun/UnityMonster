using UnityEditor;
public static class BatBundleImport
{
    public static void BrightenPrototypeAndRun()
    {
        var material = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Jotunnslayer/Enemies/Resources/Jotunnslayer/BatPrototype/dummy_enemy_mat.mat");
        material.SetColor("_BaseColor", new UnityEngine.Color(.7f, .75f, .8f));
        EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
        Jotunnslayer.Skeleton1HHostSmoke.RunAlfheimBatch();
    }
    public static void Run()
    {
        foreach (string id in new[] { "bat", "bat_sur", "bat_large_sur" })
        {
            bool large = id == "bat_large_sur";
            string prefix = large ? "bat_large_" : "bat_simple_";
            string folder = "Assets/Jotunnslayer/Enemies/Resources/Jotunnslayer/" + (large ? "BatLarge" : id == "bat" ? "BatPrototype" : "BatCombat");
            BeetleBundleImport.ImportProfile(id, folder, new[] { prefix + "stand", prefix + "run", prefix + "attack_03", prefix + "init", prefix + (large ? "stun" : "hit_forward") });
        }
        EditorApplication.Exit(0);
    }
}
