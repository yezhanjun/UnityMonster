using System.IO;
using UnityEditor;
using UnityEngine;
public static class GoblinFxAudit
{
    public static void Run()
    {
        const string root="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
        foreach(var pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle","fx_assets_all*.bundle","skill_assets_all*.bundle","custom_assets_all*.bundle","objects_assets_all*.bundle","enemies_assets_goblin_ranged_sur*.bundle"})
        foreach(var file in Directory.GetFiles(root,pattern))
        {
            var b=AssetBundle.LoadFromFile(file);
            if(!b) continue;
            foreach(var hidden in b.LoadAllAssets<GameObject>())
                if(hidden && (hidden.name.ToLowerInvariant().Contains("goblin") || hidden.name.ToLowerInvariant().Contains("thrower"))) Debug.Log("GOBLIN_HIDDEN_PREFAB " + hidden.name + " bundle=" + Path.GetFileName(file));
            foreach(var n in b.GetAllAssetNames())
            {
                Debug.Log("FX_INDEX " + n);
                if(!n.ToLowerInvariant().Contains("goblin") && !n.Contains("fire") && !n.Contains("throw")) continue;
                Debug.Log("GOBLIN_FX_ASSET "+n);
                var prefab=b.LoadAsset<GameObject>(n); if(!prefab) continue;
                foreach(var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var m=ps.main; var r=ps.GetComponent<ParticleSystemRenderer>();
                    Debug.Log($"GOBLIN_FX_PART {n} node={ps.name} size={m.startSize.constant} lifetime={m.startLifetime.constant} material={r.sharedMaterial} texture={(r.sharedMaterial?r.sharedMaterial.mainTexture:null)}");
                }
            }
        }
        EditorApplication.Exit(0);
    }
}
