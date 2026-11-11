using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
public static class BeetleBundleImport
{
    static string Folder = "Assets/Jotunnslayer/Enemies/Resources/Jotunnslayer/Beetle";
    static string Profile = "beetle_sur";
    static Dictionary<Material, Material> MaterialCopies;
    static string[] Wanted = { "beetle_minion_idle", "beetle_minion_fly", "beetle_veteran_attack", "beetle_minion_stun", "beetle_minion_summon" };
    public static void RunVariants() {
        foreach (string id in new[] { "beetle_big_sur", "beetle_desert_sur" }) {
            Profile = id; Folder = "Assets/Jotunnslayer/Enemies/Resources/Jotunnslayer/" + (id == "beetle_big_sur" ? "BeetleBig" : "BeetleDesert");
            Wanted = id == "beetle_big_sur" ? new[] { "beetle_veteran_idle", "beetle_veteran_flying", "beetle_veteran_attack" } : new[] { "beetle_minion_idle", "beetle_minion_fly", "beetle_veteran_attack", "beetle_minion_stun", "beetle_minion_summon" };
            Import();
        }
        EditorApplication.Exit(0);
    }
    public static void RunMoreVariants() {
        foreach (string id in new[] { "beetle_desert_big_sur", "beetle_portal_sur" }) {
            Profile=id; bool big=id=="beetle_desert_big_sur";
            Folder="Assets/Jotunnslayer/Enemies/Resources/Jotunnslayer/"+(big ? "BeetleDesertBig" : "BeetlePortal");
            Wanted=big ? new[] { "beetle_veteran_idle", "beetle_veteran_flying", "beetle_veteran_attack" } : new[] { "beetle_minion_idle", "beetle_minion_fly", "beetle_veteran_attack", "beetle_minion_stun", "beetle_minion_summon" };
            Import();
        }
        EditorApplication.Exit(0);
    }
    public static void RunExplosiveVariants() {
        foreach (string id in new[] { "beetle_explo_sur", "beetle_big_explo_sur" }) {
            Profile=id; bool big=id=="beetle_big_explo_sur";
            Folder="Assets/Jotunnslayer/Enemies/Resources/Jotunnslayer/"+(big ? "BeetleBigExplosive" : "BeetleExplosive");
            Wanted=big ? new[] { "beetle_veteran_idle", "beetle_veteran_flying", "beetle_veteran_attack" } : new[] { "beetle_minion_idle", "beetle_minion_fly", "beetle_minion_suicide", "beetle_minion_stun", "beetle_minion_summon" };
            Import();
        }
        EditorApplication.Exit(0);
    }
    public static void Run() { Import(); EditorApplication.Exit(0); }
    public static void ImportProfile(string profile, string folder, string[] wanted)
    {
        Profile = profile; Folder = folder; Wanted = wanted; Import();
    }
    static void Import()
    {
        MaterialCopies = new Dictionary<Material, Material>();
        const string source = "E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
        var bundles = new List<AssetBundle>();
        GameObject model = null;
        try
        {
            AssetBundle main = null;
            foreach (string pattern in new[] { "*monoscripts*.bundle", "*unitybuiltinassets*.bundle", "duplicateassetisolation*.bundle", "enemies_assets_" + Profile + "_*.bundle" })
                foreach (string file in Directory.GetFiles(source, pattern))
                {
                    bool actorBundle = pattern.StartsWith("enemies_assets_");
                    if (actorBundle && !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(file), "^enemies_assets_" + System.Text.RegularExpressions.Regex.Escape(Profile) + @"_[a-f0-9]{32}\.bundle$")) continue;
                    var loaded = AssetBundle.LoadFromFile(file); bundles.Add(loaded);
                    if (actorBundle) { if (main) throw new Exception("Ambiguous actor bundle: " + Profile); main = loaded; }
                }
            if (!main) throw new Exception("Actor bundle not loaded: " + Profile);
            var prefab = main.GetAllAssetNames().Select(n => main.LoadAsset<GameObject>(n)).First(p => p && p.name == Profile);
            var originalAnimator = prefab.GetComponentsInChildren<Animator>(true).First(a => a.runtimeAnimatorController);
            var clips = originalAnimator.runtimeAnimatorController.animationClips.Where(c => c && Wanted.Contains(c.name)).GroupBy(c => c.name).Select(g => g.First()).ToArray();
            if (clips.Length != Wanted.Length) throw new Exception("Missing beetle source clips");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            model = Object.Instantiate(originalAnimator.gameObject); model.name = Profile;
            foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.fireEvents = false;
            foreach (var clip in clips) Bake(model, clip);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true)) if (script) Object.DestroyImmediate(script);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var r in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(r);
            foreach (var lod in model.GetComponentsInChildren<LODGroup>(true)) Object.DestroyImmediate(lod);
            int staticMeshIndex = 0;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is SkinnedMeshRenderer skin))
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (Profile.StartsWith("cultist_") && renderer is MeshRenderer && filter && filter.sharedMesh)
                    {
                        var staticMesh = Object.Instantiate(filter.sharedMesh);
                        AssetDatabase.CreateAsset(staticMesh, Folder + "/StaticMesh_" + staticMeshIndex++ + ".asset");
                        filter.sharedMesh = staticMesh;
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(Material).ToArray();
                    }
                    else Object.DestroyImmediate(renderer);
                    continue;
                }
                var mesh = Object.Instantiate(skin.sharedMesh);
                AssetDatabase.CreateAsset(mesh, Folder + "/" + mesh.name + ".asset"); skin.sharedMesh = mesh;
                skin.sharedMaterials = skin.sharedMaterials.Select(Material).ToArray(); skin.updateWhenOffscreen = true;
            }
            foreach (var particle in model.GetComponentsInChildren<ParticleSystem>(true)) Object.DestroyImmediate(particle);
            var actorAnimator = model.GetComponent<Animator>();
            foreach (var nested in model.GetComponentsInChildren<Animator>(true))
                if (nested != actorAnimator) Object.DestroyImmediate(nested);
            actorAnimator.runtimeAnimatorController = null;
            var avatar = AvatarBuilder.BuildGenericAvatar(model, ""); avatar.name = "BeetleAvatar";
            if (!avatar.isValid || avatar.isHuman) throw new Exception("Invalid beetle generic avatar");
            AssetDatabase.CreateAsset(avatar, Folder + "/BeetleAvatar.asset"); actorAnimator.avatar = avatar;
            actorAnimator.applyRootMotion = false;
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            PrefabUtility.SaveAsPrefabAsset(model, Folder + "/" + Profile + ".prefab");
            AssetDatabase.SaveAssets(); Debug.Log($"BEETLE_IMPORTED {Profile} standalone prefab and {clips.Length} sampled clips");
        }
        finally { if (model) Object.DestroyImmediate(model); foreach (var b in bundles) if (b) b.Unload(true); }
    }
    public static void RepairVariants()
    {
        foreach (var pair in new[] { ("BeetleBig", "beetle_big_sur"), ("BeetleDesert", "beetle_desert_sur") })
        {
            string folder = "Assets/Jotunnslayer/Enemies/Resources/Jotunnslayer/" + pair.Item1;
            string old = folder + "/beetle_sur.prefab", destination = folder + "/" + pair.Item2 + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(old);
            try
            {
                var animator = root.GetComponent<Animator>();
                foreach (var nested in root.GetComponentsInChildren<Animator>(true))
                    if (nested != animator) Object.DestroyImmediate(nested);
                if (!animator || !animator.avatar || !animator.avatar.isValid) throw new Exception("Invalid variant avatar " + pair.Item2);
                PrefabUtility.SaveAsPrefabAsset(root, old);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            string error = AssetDatabase.MoveAsset(old, destination);
            if (!string.IsNullOrEmpty(error)) throw new Exception(error);
            Debug.Log("BEETLE_VARIANT_REPAIRED " + pair.Item2);
        }
        AssetDatabase.SaveAssets(); EditorApplication.Exit(0);
    }
    static Material Material(Material original)
    {
        if (MaterialCopies.TryGetValue(original, out var cached)) return cached;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.name = original.name;
        var textures = new SerializedObject(original).FindProperty("m_SavedProperties.m_TexEnvs");
        for (int i = 0; i < textures.arraySize; i++)
        {
            var item = textures.GetArrayElementAtIndex(i);
            if (item.FindPropertyRelative("first").stringValue != "_AlbedoMap") continue;
            var texture = item.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture2D;
            if (!texture) continue;
            var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            string file = Folder + "/" + texture.name + ".png";
            try { Graphics.Blit(texture, rt); RenderTexture.active = rt; image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); image.Apply(); File.WriteAllBytes(file,image.EncodeToPNG()); }
            finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image); }
            AssetDatabase.ImportAsset(file); material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(file));
        }
        if (!material.GetTexture("_BaseMap"))
        {
            // Some original prototype actors intentionally use an untextured material.
            var colors = new SerializedObject(original).FindProperty("m_SavedProperties.m_Colors");
            for (int i = 0; i < colors.arraySize; i++)
            {
                var item = colors.GetArrayElementAtIndex(i);
                string key = item.FindPropertyRelative("first").stringValue;
                if (key == "_Color" || key == "_BaseColor" || key == "_AlbedoColor")
                    material.SetColor("_BaseColor", item.FindPropertyRelative("second").colorValue);
            }
            Debug.Log("IMPORTED_SOLID_MATERIAL " + Profile + " " + original.name);
            if (Profile == "bat") material.SetColor("_BaseColor", new Color(.7f, .75f, .8f));
        }
        AssetDatabase.CreateAsset(material, Folder + "/" + material.name + ".mat"); MaterialCopies.Add(original, material); return material;
    }
    static void Bake(GameObject model, AnimationClip original)
    {
        var bones=model.GetComponentsInChildren<Transform>(true);
        var curves=new AnimationCurve[bones.Length*10];for(int i=0;i<curves.Length;i++)curves[i]=new AnimationCurve();
        string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w","m_LocalScale.x","m_LocalScale.y","m_LocalScale.z"};
        int frames=Mathf.Clamp(Mathf.CeilToInt(original.length*30),2,180);
        var initial = new Quaternion[bones.Length]; float maxAngle = 0f;
        for(int frame=0;frame<=frames;frame++) {
            float time=original.length*frame/frames; original.SampleAnimation(model,time);
            for(int i=1;i<bones.Length;i++) {
                var t=bones[i];var p=t.localPosition;var q=t.localRotation;var s=t.localScale;
                if(frame==0) initial[i]=q; else maxAngle=Mathf.Max(maxAngle,Quaternion.Angle(initial[i],q));
                float[] values={p.x,p.y,p.z,q.x,q.y,q.z,q.w,s.x,s.y,s.z};
                for(int axis=0;axis<10;axis++) {if(float.IsNaN(values[axis])||float.IsInfinity(values[axis]))throw new Exception("Invalid beetle sample");curves[i*10+axis].AddKey(new Keyframe(time,values[axis],0,0));}
            }
        }
        Debug.Log($"IMPORTED_POSE {Profile} {original.name} maxAngle={maxAngle}");
        if(Profile.StartsWith("cultist_") && (original.name.Contains("attack") || original.name.Contains("walk")) && maxAngle<.1f) throw new Exception("Cultist source sampling has no verified motion: "+original.name);
        var clip=new AnimationClip {name=original.name,frameRate=30};
        for(int i=1;i<bones.Length;i++)for(int axis=0;axis<10;axis++) AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bones[i],model.transform),typeof(Transform),properties[axis]),curves[i*10+axis]);
        clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=original.name.EndsWith("idle")||original.name.Contains("_stand")||original.name.Contains("_walk")||original.name.EndsWith("run")||(original.name.EndsWith("fly") || original.name.EndsWith("flying"));AnimationUtility.SetAnimationClipSettings(clip,settings);
        AssetDatabase.CreateAsset(clip,Folder+"/"+clip.name+".anim");
        Debug.Log($"BEETLE_BAKED {clip.name} curves={AnimationUtility.GetCurveBindings(clip).Length} duration={clip.length}");
    }
}
