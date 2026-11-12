using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class CultistBundleAudit
{
    public static void Run()
    {
        const string source="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
        var dependencies=new List<AssetBundle>();
        try {
            foreach(string pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle"})
                foreach(string file in Directory.GetFiles(source,pattern))dependencies.Add(AssetBundle.LoadFromFile(file));
            foreach(string id in new[]{"cultist_1h_sur","cultist_bow_sur","cultist_miniboss_sur","cultist_miniboss_v02_sur","cultist_priest_sur"}) {
                var bundle=AssetBundle.LoadFromFile(Directory.GetFiles(source,"enemies_assets_"+id+"_*.bundle").Single());
                try {
                    var prefab=bundle.GetAllAssetNames().Select(n=>bundle.LoadAsset<GameObject>(n)).First(p=>p&&p.name==id);
                    var animator=prefab.GetComponentsInChildren<Animator>(true).First(a=>a.runtimeAnimatorController);
                    Debug.Log($"CULTIST_MODEL {id} animator={animator.name} human={animator.avatar&&animator.avatar.isHuman} skins={prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length}");
                    foreach(var clip in animator.runtimeAnimatorController.animationClips.Where(c=>c).GroupBy(c=>c.name).Select(g=>g.First()))
                        Debug.Log($"CULTIST_CLIP {id} {clip.name} duration={clip.length} human={clip.humanMotion}");
                } finally {bundle.Unload(true);}
            }
        }finally{foreach(var b in dependencies)if(b)b.Unload(true);}
        EditorApplication.Exit(0);
    }
}
