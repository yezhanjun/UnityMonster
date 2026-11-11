using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
public static class BeetleBundleImport
{
    const string Folder = "Assets/Jotunnslayer/Enemies/Resources/Jotunnslayer/Beetle";
    static readonly string[] Wanted = { "beetle_minion_idle", "beetle_minion_fly", "beetle_veteran_attack", "beetle_minion_stun", "beetle_minion_summon" };
    public static void Run()
    {
        const string source = "E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
        var bundles = new List<AssetBundle>();
        GameObject model = null;
        try
        {
            foreach (string pattern in new[] { "*monoscripts*.bundle", "*unitybuiltinassets*.bundle", "duplicateassetisolation*.bundle", "enemies_assets_beetle_sur_*.bundle" })
                foreach (string file in Directory.GetFiles(source, pattern)) bundles.Add(AssetBundle.LoadFromFile(file));
            var main = bundles.Last();
            var prefab = main.GetAllAssetNames().Select(n => main.LoadAsset<GameObject>(n)).First(p => p && p.name == "beetle_sur");
            var originalAnimator = prefab.GetComponentsInChildren<Animator>(true).First(a => a.runtimeAnimatorController);
            var clips = originalAnimator.runtimeAnimatorController.animationClips.Where(c => c && Wanted.Contains(c.name)).GroupBy(c => c.name).Select(g => g.First()).ToArray();
            if (clips.Length != Wanted.Length) throw new Exception("Missing beetle source clips");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            model = Object.Instantiate(originalAnimator.gameObject); model.name = "beetle_sur";
            foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.fireEvents = false;
            foreach (var clip in clips) Bake(model, clip);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true)) if (script) Object.DestroyImmediate(script);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var r in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(r);
            foreach (var lod in model.GetComponentsInChildren<LODGroup>(true)) Object.DestroyImmediate(lod);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is SkinnedMeshRenderer skin)) { Object.DestroyImmediate(renderer); continue; }
                var mesh = Object.Instantiate(skin.sharedMesh);
                AssetDatabase.CreateAsset(mesh, Folder + "/" + mesh.name + ".asset"); skin.sharedMesh = mesh;
                skin.sharedMaterials = skin.sharedMaterials.Select(Material).ToArray(); skin.updateWhenOffscreen = true;
            }
            foreach (var particle in model.GetComponentsInChildren<ParticleSystem>(true)) Object.DestroyImmediate(particle);
            var actorAnimator = model.GetComponent<Animator>();
            actorAnimator.runtimeAnimatorController = null;
            var avatar = AvatarBuilder.BuildGenericAvatar(model, ""); avatar.name = "BeetleAvatar";
            if (!avatar.isValid || avatar.isHuman) throw new Exception("Invalid beetle generic avatar");
            AssetDatabase.CreateAsset(avatar, Folder + "/BeetleAvatar.asset"); actorAnimator.avatar = avatar;
            actorAnimator.applyRootMotion = false;
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            PrefabUtility.SaveAsPrefabAsset(model, Folder + "/beetle_sur.prefab");
            AssetDatabase.SaveAssets(); Debug.Log("BEETLE_IMPORTED standalone prefab and five sampled clips");
        }
        finally { if (model) Object.DestroyImmediate(model); foreach (var b in bundles) if (b) b.Unload(true); }
        EditorApplication.Exit(0);
    }
    static Material Material(Material original)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.name = original.name;
        var textures = new SerializedObject(original).FindProperty("m_SavedProperties.m_TexEnvs");
        for (int i = 0; i < textures.arraySize; i++)
        {
            var item = textures.GetArrayElementAtIndex(i);
            if (item.FindPropertyRelative("first").stringValue != "_AlbedoMap") continue;
            var texture = item.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture2D;
            if (!texture) throw new Exception("Beetle original albedo missing");
            var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            string file = Folder + "/" + texture.name + ".png";
            try { Graphics.Blit(texture, rt); RenderTexture.active = rt; image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); image.Apply(); File.WriteAllBytes(file,image.EncodeToPNG()); }
            finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image); }
            AssetDatabase.ImportAsset(file); material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(file));
        }
        if (!material.GetTexture("_BaseMap")) throw new Exception("Beetle converted albedo missing");
        AssetDatabase.CreateAsset(material, Folder + "/" + material.name + ".mat"); return material;
    }
    static void Bake(GameObject model, AnimationClip original)
    {
        var bones=model.GetComponentsInChildren<Transform>(true);
        var curves=new AnimationCurve[bones.Length*10];for(int i=0;i<curves.Length;i++)curves[i]=new AnimationCurve();
        string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w","m_LocalScale.x","m_LocalScale.y","m_LocalScale.z"};
        int frames=Mathf.Clamp(Mathf.CeilToInt(original.length*30),2,180);
        for(int frame=0;frame<=frames;frame++) {
            float time=original.length*frame/frames; original.SampleAnimation(model,time);
            for(int i=1;i<bones.Length;i++) {
                var t=bones[i];var p=t.localPosition;var q=t.localRotation;var s=t.localScale;
                float[] values={p.x,p.y,p.z,q.x,q.y,q.z,q.w,s.x,s.y,s.z};
                for(int axis=0;axis<10;axis++) {if(float.IsNaN(values[axis])||float.IsInfinity(values[axis]))throw new Exception("Invalid beetle sample");curves[i*10+axis].AddKey(new Keyframe(time,values[axis],0,0));}
            }
        }
        var clip=new AnimationClip {name=original.name,frameRate=30};
        for(int i=1;i<bones.Length;i++)for(int axis=0;axis<10;axis++) AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bones[i],model.transform),typeof(Transform),properties[axis]),curves[i*10+axis]);
        clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=original.name.EndsWith("idle")||original.name.EndsWith("fly");AnimationUtility.SetAnimationClipSettings(clip,settings);
        AssetDatabase.CreateAsset(clip,Folder+"/"+clip.name+".anim");
        Debug.Log($"BEETLE_BAKED {clip.name} curves={AnimationUtility.GetCurveBindings(clip).Length} duration={clip.length}");
    }
}
