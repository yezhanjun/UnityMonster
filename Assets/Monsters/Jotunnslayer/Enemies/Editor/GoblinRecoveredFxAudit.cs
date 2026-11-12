using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class GoblinRecoveredFxAudit
{
 public static void Run()
 {
  var bundles=new List<AssetBundle>();
  try
  {
   const string source="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
   foreach(var pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle"})
    foreach(var file in Directory.GetFiles(source,pattern)) bundles.Add(AssetBundle.LoadFromFile(file));
   var main=AssetBundle.LoadFromFile("Logs/goblin-recovered.bundle");bundles.Add(main);
   foreach(var kind in new[]{"cast","muzzle","projectile","banish","hit"})
   {
    var go=main.LoadAsset<GameObject>("assets/recovered/goblin_"+kind+".prefab");
    if(!go)throw new Exception("Missing recovered FX "+kind);
    Debug.Log($"RECOVERED_FX {kind} name={go.name} particles={go.GetComponentsInChildren<ParticleSystem>(true).Length} renderers={go.GetComponentsInChildren<Renderer>(true).Length}");
    foreach(var r in go.GetComponentsInChildren<Renderer>(true))
    {
     Debug.Log($"RECOVERED_RENDERER {kind}/{r.name} type={r.GetType().Name}");
     foreach(var mat in r.sharedMaterials)
     {
      if(!mat)continue;
      Debug.Log($"RECOVERED_MAT {mat.name} shader={mat.shader.name}");
      var so=new SerializedObject(mat);
      foreach(var key in new[]{"m_TexEnvs","m_Floats","m_Colors"})
      {
       var props=so.FindProperty("m_SavedProperties."+key);
       for(int i=0;props!=null && i<props.arraySize;i++)
       {
        var item=props.GetArrayElementAtIndex(i);var value=item.FindPropertyRelative("second");
        string data=key=="m_TexEnvs"?value.FindPropertyRelative("m_Texture").objectReferenceValue?.name:key=="m_Floats"?value.floatValue.ToString():value.colorValue.ToString();
        Debug.Log($"RECOVERED_PROP {mat.name} {item.FindPropertyRelative("first").stringValue}={data}");
       }
      }
     }
    }
   }
   Debug.Log("RECOVERED_FX_AUDIT_PASS");
  }
  finally {foreach(var b in bundles)if(b)b.Unload(true);}
  EditorApplication.Exit(0);
 }
}
