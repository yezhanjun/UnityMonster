using System;
using UnityEditor;
using UnityEngine;
public static class LilToonPassAudit
{
 public static void RunAndSmoke()
 {
  foreach(var name in new[]{"lilToon","Hidden/lilToonOutline","Hidden/lilToonTransparent","Hidden/lilToonTransparentOutline"})
  {
   var shader=Shader.Find(name);
   if(!shader || !shader.isSupported)throw new InvalidOperationException("Unsupported patched shader: "+name);
   var material=new Material(shader);
   try
   {
    if(material.FindPass("FORWARD")<0 || material.FindPass("SHADOW_CASTER")<0 || material.FindPass("DEPTHONLY")<0)
     throw new InvalidOperationException("Original lilToon passes unavailable: "+name);
    if(name.Contains("Outline") && material.FindPass("FORWARD_OUTLINE")<0)
     throw new InvalidOperationException("Outline pass unavailable: "+name);
    foreach(var message in ShaderUtil.GetShaderMessages(shader))
     if(message.severity.ToString()=="Error")throw new InvalidOperationException(name+": "+message.message);
    Debug.Log($"LILTOON_PASS_AUDIT supported=True shader={name} forward={material.FindPass("FORWARD")} outline={material.FindPass("FORWARD_OUTLINE")} passes={material.passCount}");
   }
   finally{UnityEngine.Object.DestroyImmediate(material);}
  }
  Debug.Log("LILTOON_PASS_AUDIT_PASS primary passes retained, no shader compiler errors");
  Jotunnslayer.Skeleton1HHostSmoke.RunAlfheimBatch();
 }
}
