using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Unity.Mathematics;
using MagicaCloth2;
using VertexAttribute = MagicaCloth2.VertexAttribute;

namespace CyberSamurai.Editor
{
    public static class SamuraiKatanaSetup
    {
        public const string Root = "Assets/Monsters/Boss/cyber-samurai";
        public const string Generated = Root + "/KatanaPreview";
        public const string Model = Root + "/model/CyberSamurai_Humanoid_Corrected.fbx";
        public const string Scene = Generated + "/CyberSamurai_KatanaPreview.unity";
        public static readonly string[] ClipNames = { "Idle", "Walk_Loop_F_0", "Run_Loop_F_0", "Idle_To_Idle_Combat", "Idle_Combat", "Combo_Attack_01_All", "Combo_Attack_02_All", "Combo_Attack_03_All", "Combo_Attack_04_All", "Combo_Attack_05_All", "Idle_Combat_To_Idle" };

        [MenuItem("Tools/Cyber Samurai/Build Katana and Cloth Preview")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Generated + "/Materials");
            Directory.CreateDirectory(Generated + "/Animations");
            Directory.CreateDirectory(Generated + "/Meshes");
            AssetDatabase.Refresh();
            var imp = (ModelImporter)AssetImporter.GetAtPath(Model);
            if (imp == null) throw new Exception("Corrected FBX is missing");
            imp.animationType = ModelImporterAnimationType.Human; imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.isReadable = true; imp.optimizeBones = false; imp.optimizeGameObjects = false;
            imp.importAnimation = false; imp.importCameras = false; imp.importLights = false;
            imp.SaveAndReimport();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "CyberSamurai_KatanaBoss";
            var animator = go.GetComponent<Animator>();
            if (!animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid) throw new Exception("Humanoid Avatar is invalid");
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.updateWhenOffscreen = true; smr.localBounds = new Bounds(new Vector3(0, 1.2f, 0), new Vector3(5, 5, 5));
                smr.sharedMaterials = smr.sharedMaterials.Select(m => MaterialFor(m.name)).ToArray();
            }
            var robe = go.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.name == "CyberSamurai_Robe");
            var cloth = new GameObject("Robe_MagicaCloth").AddComponent<MagicaCloth>(); cloth.transform.SetParent(go.transform, false);
            ConfigureCloth(cloth, robe, animator);
            var boot = go.AddComponent<SamuraiClothBootstrap>(); boot.animator = animator; boot.cloth = cloth;
            var clothParts=new List<MagicaCloth>{cloth};
            foreach(var trousers in go.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.name.StartsWith("CyberSamurai_Trousers_")))
            {
                var part=new GameObject(trousers.name+"_MagicaCloth").AddComponent<MagicaCloth>();part.transform.SetParent(go.transform,false);
                ConfigureCloth(part,trousers,animator);clothParts.Add(part);
            }
            boot.cloths=clothParts.ToArray();
            var remap = AttachKatana(go, animator);
            string controllerPath = Generated + "/CyberSamurai_Katana.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var sm = controller.layers[0].stateMachine;
            foreach (var s in sm.states) sm.RemoveState(s.state);
            var all = AssetDatabase.FindAssets("t:AnimationClip", new[]{"Assets/AssetStore/KatanaAnimationPack/Animations/Humanoid"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            for (int i=0; i<ClipNames.Length; i++)
            {
                var matches = all.Where(p => Path.GetFileNameWithoutExtension(p) == ClipNames[i]).OrderBy(p => p).ToArray();
                if (ClipNames[i].StartsWith("Combo_")) matches = matches.Where(p => p.Contains("/02_Attack/01_Vertion_01/")).ToArray();
                if (matches.Length == 0) throw new Exception("Missing clip " + ClipNames[i]);
                var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(matches[0]);
                if (!source.isHumanMotion) throw new Exception("Clip is not Humanoid: " + matches[0]);
                var copy = UnityEngine.Object.Instantiate(source); copy.name = ClipNames[i];
                foreach (var binding in AnimationUtility.GetCurveBindings(copy))
                {
                    if (binding.type != typeof(Transform)) continue;
                    string leaf = binding.path.Split('/').Last();
                    var curve = AnimationUtility.GetEditorCurve(copy,binding);
                    AnimationUtility.SetEditorCurve(copy,binding,null);
                    if (remap.TryGetValue(leaf,out string path))
                    { var mapped = binding; mapped.path = path; AnimationUtility.SetEditorCurve(copy,mapped,curve); }
                }
                AnimationUtility.SetAnimationEvents(copy, Array.Empty<AnimationEvent>());
                BakeSheathing(source, copy, go.GetComponent<SamuraiWeaponSheathing>());
                var settings = AnimationUtility.GetAnimationClipSettings(copy);
                settings.loopTime = i<3 || ClipNames[i]=="Idle_Combat";
                AnimationUtility.SetAnimationClipSettings(copy, settings);
                string clipPath = Generated + "/Animations/" + copy.name + ".anim";
                SaveAsset(copy, clipPath);
                var state = sm.AddState(ClipNames[i], new Vector3(240*(i%3),80*(i/3),0));
                state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath); state.iKOnFeet = false;
                if(i==0)sm.defaultState=state;
            }
            animator.runtimeAnimatorController = controller;
            PrefabUtility.SaveAsPrefabAsset(go, Generated + "/CyberSamurai_KatanaBoss.prefab");
            var ui = new GameObject("Preview Controls").AddComponent<SamuraiKatanaPreview>();
            ui.animator=animator;ui.cloth=cloth;ui.bootstrap=boot;ui.states=ClipNames;ui.autoCycle=false;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.name="Ground";floor.transform.localScale=Vector3.one*1.5f;
            var floorMat=new Material(Shader.Find("Universal Render Pipeline/Lit"));floorMat.name="Ground";floorMat.SetColor("_BaseColor",new Color(.12f,.15f,.19f));SaveAsset(floorMat,Generated+"/Materials/Ground.mat");floor.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Generated+"/Materials/Ground.mat");
            var light = new GameObject("Key Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(35,-35,0);light.shadows=LightShadows.Soft;
            var fill = new GameObject("Fill Light").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.8f;fill.transform.rotation=Quaternion.Euler(15,150,0);
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(3,2.1f,4.7f);camera.transform.LookAt(new Vector3(0,1.25f,0));camera.fieldOfView=36;camera.nearClipPlane=.05f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.065f,.08f,.11f);camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.45f,.45f,.45f);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Scene);
            AssetDatabase.SaveAssets();
            Debug.Log("CYBER_SAMURAI_SETUP_OK " + animator.avatar.isValid + " colliders="+cloth.SerializeData.colliderCollisionConstraint.colliderList.Count+" painted="+cloth.GetSerializeData2().selectionData.Count);
        }

        static void ConfigureCloth(MagicaCloth cloth,SkinnedMeshRenderer robe,Animator animator)
        {
            var d=cloth.SerializeData;d.clothType=ClothProcess.ClothType.MeshCloth;d.sourceRenderers.Add(robe);
            // Distance-only merging can weld the two trouser tubes together.
            d.reductionSetting.simpleDistance=0;d.reductionSetting.shapeDistance=.015f;
            d.gravity=3;d.damping=new CurveSerializeData(.18f);d.radius=new CurveSerializeData(.009f);
            d.animationPoseRatio=1;d.stablizationTimeAfterReset=.3f;
            d.motionConstraint.useMaxDistance=true;d.motionConstraint.maxDistance=new CurveSerializeData(.07f,.05f,1);
            d.inertiaConstraint.worldInertia=.15f;d.inertiaConstraint.localInertia=.3f;
            d.inertiaConstraint.teleportMode=InertiaConstraint.TeleportMode.Reset;d.inertiaConstraint.teleportDistance=1;d.inertiaConstraint.teleportRotation=100;
            d.colliderCollisionConstraint.mode=ColliderCollisionConstraint.Mode.Edge;d.colliderCollisionConstraint.friction=.08f;
            d.cullingSettings.cameraCullingMode=CullingSettings.CameraCullingMode.Off;
            var verts=robe.sharedMesh.vertices;var selection=new SelectionData(verts.Length){userEdit=true,maxConnectionDistance=.06f};
            float waist=animator.GetBoneTransform(HumanBodyBones.Hips).position.y;
            float shoulder=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position.y;
            for(int i=0;i<verts.Length;i++)
            {
                Vector3 p=cloth.transform.InverseTransformPoint(robe.transform.TransformPoint(verts[i]));selection.positions[i]=p;
                bool trousers=p.y<waist-.12f;
                bool sleeve=Mathf.Abs(p.x)>.33f && p.y<shoulder-.035f;
                selection.attributes[i]=trousers||sleeve?VertexAttribute.Move:VertexAttribute.Fixed;
            }
            cloth.GetSerializeData2().selectionData=selection;
            AddCapsule(cloth,animator,HumanBodyBones.Hips,HumanBodyBones.Chest,.16f);
            AddCapsule(cloth,animator,HumanBodyBones.Chest,HumanBodyBones.Neck,.19f);
            foreach(bool left in new[]{true,false})
            {
                AddCapsule(cloth,animator,left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg,left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg,.105f);
                AddCapsule(cloth,animator,left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg,left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot,.085f);
                AddCapsule(cloth,animator,left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm,left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm,.065f);
                AddCapsule(cloth,animator,left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm,left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand,.06f);
            }
        }

        static void AddCapsule(MagicaCloth cloth,Animator a,HumanBodyBones from,HumanBodyBones to,float radius)
        {
            var b=a.GetBoneTransform(from);var end=a.GetBoneTransform(to);var obj=new GameObject("ClothCollider_"+from);
            obj.transform.SetParent(b,false);obj.transform.position=(b.position+end.position)*.5f;obj.transform.rotation=Quaternion.FromToRotation(Vector3.up,end.position-b.position);
            var c=obj.AddComponent<MagicaCapsuleCollider>();c.direction=MagicaCapsuleCollider.Direction.Y;c.SetSize(radius,radius,Vector3.Distance(b.position,end.position)+radius);
            cloth.SerializeData.colliderCollisionConstraint.colliderList.Add(c);
        }

        static Dictionary<string,string> AttachKatana(GameObject target,Animator animator)
        {
            var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetStore/KatanaAnimationPack/Model/9CG_Katana.fbx"));
            var sa=source.GetComponent<Animator>();sa.cullingMode=AnimatorCullingMode.AlwaysAnimate;var map=new Dictionary<Transform,Transform>();var paths=new Dictionary<string,string>();
            var sourceFrames=GetHumanHandFrames(sa);var targetFrames=GetHumanHandFrames(animator);
            foreach(var side in new[]{HumanBodyBones.LeftHand,HumanBodyBones.RightHand})
            {
                var sh=sa.GetBoneTransform(side);var th=animator.GetBoneTransform(side);
                var anchor=new GameObject(side+"_KatanaSocket").transform;anchor.SetParent(th,false);
                // Align in the two avatars' canonical hand frames, retaining source bone units.
                anchor.localRotation=Quaternion.Inverse(targetFrames[side])*sourceFrames[side];
                anchor.localScale=sh.lossyScale;
                foreach(Transform child in sh)
                {
                    if(!child.name.StartsWith("Katana_"))continue;
                    var clone=UnityEngine.Object.Instantiate(child.gameObject,anchor,false).transform;clone.name=child.name;
                    var old=child.GetComponentsInChildren<Transform>();var fresh=clone.GetComponentsInChildren<Transform>();
                    for(int i=0;i<old.Length;i++){map[old[i]]=fresh[i];paths[old[i].name]=AnimationUtility.CalculateTransformPath(fresh[i],target.transform);}
                    foreach (var renderer in clone.GetComponentsInChildren<MeshRenderer>(true))
                        ConfigureRigidWeapon(renderer);
                }
            }
            foreach(var sr in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(!sr.name.StartsWith("Katana_"))continue;
                var mesh=UnityEngine.Object.Instantiate(sr.sharedMesh);mesh.name=sr.name+"_Retargeted";
                if(sr.bones.Any(b=>!map.ContainsKey(b)))throw new Exception("Unmapped weapon skin bone on "+sr.name);
                var dest=new GameObject(sr.name);dest.transform.SetParent(target.transform,false);var dr=dest.AddComponent<SkinnedMeshRenderer>();
                var newBones=sr.bones.Select(b=>map[b]).ToArray();
                var vv=mesh.vertices;var nn=mesh.normals;var weights=mesh.boneWeights;
                // Weapons are rigidly skinned to their own extra bones. Rebase each vertex
                // from source bind space to the corresponding target bind space.
                var mats=sr.bones.Select((b,i)=>target.transform.worldToLocalMatrix*newBones[i].localToWorldMatrix*b.worldToLocalMatrix*sr.transform.localToWorldMatrix).ToArray();
                for(int i=0;i<vv.Length;i++){var m=mats[weights[i].boneIndex0];vv[i]=m.MultiplyPoint3x4(vv[i]);if(nn.Length==vv.Length)nn[i]=m.MultiplyVector(nn[i]).normalized;}
                mesh.vertices=vv;mesh.normals=nn;mesh.bindposes=newBones.Select(b=>b.worldToLocalMatrix*target.transform.localToWorldMatrix).ToArray();mesh.RecalculateBounds();mesh.RecalculateTangents();
                SaveAsset(mesh,Generated+"/Meshes/"+mesh.name+".asset");dr.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Generated+"/Meshes/"+mesh.name+".asset");dr.bones=newBones;dr.rootBone=newBones[0];dr.updateWhenOffscreen=true;
                dr.sharedMaterials=sr.sharedMaterials.Select(m=>MaterialFor("Katana")).ToArray();
            }
            var docking=target.AddComponent<SamuraiWeaponSheathing>();
            docking.blade=target.GetComponentsInChildren<Transform>().Single(t=>t.name=="Katana_weapon_01");
            docking.sheath=target.GetComponentsInChildren<Transform>().Single(t=>t.name=="Katana_sheath_01");
            UnityEngine.Object.DestroyImmediate(source);return paths;
        }

        static void BakeSheathing(AnimationClip clip, AnimationClip copy, SamuraiWeaponSheathing docking)
        {
            var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetStore/KatanaAnimationPack/Model/9CG_Katana.fbx"));
            var animator=source.GetComponent<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;
            var transforms=source.GetComponentsInChildren<Transform>();
            var blade=transforms.Single(t=>t.name=="Katana_weapon_01");var sheath=transforms.Single(t=>t.name=="Katana_sheath_01");
            var graph=PlayableGraph.Create("Sample source sheath alignment");
            try
            {
                var playable=AnimationClipPlayable.Create(graph,AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/AssetStore/KatanaAnimationPack/Animations/Humanoid/01_Idle/Idle.anim"));
                var output=AnimationPlayableOutput.Create(graph,"Source",animator);output.SetSourcePlayable(playable);graph.Play();graph.Evaluate(0);
                docking.dockedPosition=sheath.InverseTransformPoint(blade.position);
                docking.dockedRotation=Quaternion.Inverse(sheath.rotation)*blade.rotation;
                playable.Destroy();playable=AnimationClipPlayable.Create(graph,clip);output.SetSourcePlayable(playable);
                var curve=new AnimationCurve();int count=Mathf.CeilToInt(clip.length*60);
                for(int i=0;i<=count;i++)
                {
                    float time=Mathf.Min(i/60f,clip.length-.0001f);playable.SetTime(time);graph.Evaluate(0);
                    float separation=Vector3.Distance(blade.position,sheath.TransformPoint(docking.dockedPosition));
                    float angle=Quaternion.Angle(blade.rotation,sheath.rotation*docking.dockedRotation);
                    float weight=1-Mathf.SmoothStep(0,1,Mathf.Max(separation/.18f,angle/35));
                    curve.AddKey(i==count?clip.length:time,weight);
                }
                for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}
                AnimationUtility.SetEditorCurve(copy,EditorCurveBinding.FloatCurve("",typeof(SamuraiWeaponSheathing),"sheathWeight"),curve);
            }
            finally {graph.Destroy();UnityEngine.Object.DestroyImmediate(source);}
        }

        static void ConfigureRigidWeapon(MeshRenderer renderer)
        {
            bool sheath=renderer.name.Contains("sheath");
            if(sheath)
            {
                renderer.sharedMaterial=WeaponMaterial("Katana_Lacquer",new Color(.018f,.025f,.035f),.15f,.65f);
                return;
            }
            var filter=renderer.GetComponent<MeshFilter>();var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);
            var vertices=mesh.vertices;var triangles=mesh.triangles;
            var groups=new[]{new List<int>(),new List<int>(),new List<int>()};
            for(int i=0;i<triangles.Length;i+=3)
            {
                float z=(vertices[triangles[i]].z+vertices[triangles[i+1]].z+vertices[triangles[i+2]].z)/3;
                int group=z<.025f?1:z<.15f?2:0;
                groups[group].Add(triangles[i]);groups[group].Add(triangles[i+1]);groups[group].Add(triangles[i+2]);
            }
            mesh.subMeshCount=3;for(int i=0;i<3;i++)mesh.SetTriangles(groups[i],i);
            string path=Generated+"/Meshes/Katana_sword_PBR.asset";SaveAsset(mesh,path);filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            renderer.sharedMaterials=new[]{WeaponMaterial("Katana_Steel",new Color(.65f,.7f,.74f),1,.82f),WeaponMaterial("Katana_Grip",new Color(.025f,.035f,.045f),0,.25f),WeaponMaterial("Katana_Guard",new Color(.42f,.28f,.1f),.85f,.55f)};
        }

        static Material WeaponMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path=Generated+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",smoothness);
            EditorUtility.SetDirty(material);return material;
        }

        static Dictionary<HumanBodyBones,Quaternion> GetHumanHandFrames(Animator animator)
        {
            var transforms=animator.GetComponentsInChildren<Transform>();
            var positions=transforms.Select(t=>t.localPosition).ToArray();var rotations=transforms.Select(t=>t.localRotation).ToArray();var scales=transforms.Select(t=>t.localScale).ToArray();
            var result=new Dictionary<HumanBodyBones,Quaternion>();
            animator.Rebind();animator.Update(0);
            using(var handler=new HumanPoseHandler(animator.avatar,animator.transform))
            {
                var pose=new HumanPose();handler.GetHumanPose(ref pose);Array.Clear(pose.muscles,0,pose.muscles.Length);handler.SetHumanPose(ref pose);
                foreach(var hand in new[]{HumanBodyBones.LeftHand,HumanBodyBones.RightHand})result[hand]=animator.GetBoneTransform(hand).rotation;
            }
            for(int i=0;i<transforms.Length;i++){transforms[i].localPosition=positions[i];transforms[i].localRotation=rotations[i];transforms[i].localScale=scales[i];}
            return result;
        }

        static Material MaterialFor(string name)
        {
            name=name.Replace(" (Instance)","");string path=Generated+"/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);bool isNew=!m;
            if(isNew)m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};
            bool hard=name.StartsWith("LowSet");bool robe=name=="Robe";
            string baseName=hard?name+"_Base_Color.png":robe?"fabric13_basecolor.jpg":name=="Robe2"?"circleknitfabric_basecolor.jpg":null;
            if(baseName!=null)
            {
                m.SetColor("_BaseColor",Color.white);m.SetTexture("_BaseMap",LoadTexture(baseName,false));
                string normal=hard?name+"_Normal_OpenGL.png":robe?"robe_normals.png":"robe_norma1ls.jpg";
                m.SetTexture("_BumpMap",LoadTexture(normal,true));m.SetFloat("_BumpScale",1);m.EnableKeyword("_NORMALMAP");
                string rough=hard?name+"_Roughness.png":robe?"fabric13_roughnessf.jpg":"circleknitfabric_roughness.jpg";
                m.SetTexture("_MetallicGlossMap",PackSurface(name,rough,hard?name+"_Metallic.png":null));
                m.SetFloat("_Metallic",hard?1:0);m.SetFloat("_Smoothness",1);m.SetFloat("_SmoothnessTextureChannel",0);m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_Cull",0); // Author's cullFace=DISABLE on all five materials.
                bool emits=name=="LowSet1"||name=="LowSet2";
                if(emits){m.SetTexture("_EmissionMap",LoadTexture(name+"_Emissive.png",false));m.SetColor("_EmissionColor",Color.white*(name=="LowSet1"?4.4143113f:1));m.EnableKeyword("_EMISSION");}
                else {m.SetColor("_EmissionColor",Color.black);m.DisableKeyword("_EMISSION");}
            }
            if(name=="Katana"){m.SetColor("_BaseColor",new Color(.5f,.55f,.6f));m.SetFloat("_Metallic",.85f);m.SetFloat("_Smoothness",.7f);}
            if(isNew)AssetDatabase.CreateAsset(m,path);else EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D LoadTexture(string file,bool normal)
        {
            string path=Root+"/textures/"+file;var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            var type=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            if(importer.textureType!=type || (!normal&&!importer.sRGBTexture) || (normal&&importer.flipGreenChannel))
            {importer.textureType=type;importer.sRGBTexture=!normal;importer.flipGreenChannel=false;importer.SaveAndReimport();}
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D PackSurface(string name,string roughness,string metallic)
        {
            string path=Generated+"/Materials/"+name+"_MetallicSmoothness.png";
            if(!File.Exists(path))
            {
                var rough=new Texture2D(2,2,TextureFormat.RGBA32,false,true);rough.LoadImage(File.ReadAllBytes(Root+"/textures/"+roughness));
                var rp=rough.GetPixels32();Color32[] mp=null;Texture2D metal=null;
                if(metallic!=null){metal=new Texture2D(2,2,TextureFormat.RGBA32,false,true);metal.LoadImage(File.ReadAllBytes(Root+"/textures/"+metallic));if(metal.width!=rough.width||metal.height!=rough.height)throw new Exception("Surface texture size mismatch");mp=metal.GetPixels32();}
                for(int i=0;i<rp.Length;i++)rp[i]=new Color32(mp==null?(byte)0:mp[i].r,0,0,(byte)(255-rp[i].r));
                var packed=new Texture2D(rough.width,rough.height,TextureFormat.RGBA32,false,true);packed.SetPixels32(rp);packed.Apply();File.WriteAllBytes(path,packed.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(rough);if(metal)UnityEngine.Object.DestroyImmediate(metal);UnityEngine.Object.DestroyImmediate(packed);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            }
            var ti=(TextureImporter)AssetImporter.GetAtPath(path);
            if(ti.sRGBTexture||ti.alphaSource!=TextureImporterAlphaSource.FromInput||ti.maxTextureSize!=4096)
            {ti.sRGBTexture=false;ti.alphaSource=TextureImporterAlphaSource.FromInput;ti.alphaIsTransparency=false;ti.maxTextureSize=4096;ti.SaveAndReimport();}
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void SaveAsset(UnityEngine.Object obj,string path)
        {
            var old=AssetDatabase.LoadMainAssetAtPath(path);
            if(old){EditorUtility.CopySerialized(obj,old);UnityEngine.Object.DestroyImmediate(obj);EditorUtility.SetDirty(old);}
            else AssetDatabase.CreateAsset(obj,path);
        }

        public static void BatchBuild(){try{Build();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
    }
}
