using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MTPSKIT.Gameplay;

[InitializeOnLoad]
static class SuperBotSetup
{
    const string Root = "Assets/Game/CharacterSelection/SuperBotPackVol1/";
    const string Version = "TPSBattle.SuperBot.Humanoid.v1";
    static SuperBotSetup() { EditorApplication.delayCall += AutoInstall; }
    static void AutoInstall()
    {
        foreach (string name in RangeRobotVisual.SuperBotNames)
            if (!File.Exists("Assets/Resources/SuperBotPackVol1/" + name + ".prefab")) { Install(); return; }
    }
    [MenuItem("TPSBattle/训练场/安装 SuperBot 人形角色")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (!Directory.Exists(Root)) return;
        var report = new List<string>();
        try
        {
            foreach (string name in RangeRobotVisual.SuperBotNames)
            {
                string output = "Assets/Resources/SuperBotPackVol1/" + name + ".prefab";
                string[] files = Directory.GetFiles(Root + name + "/BaseMesh", "*.fbx");
                if (files.Length == 0) throw new Exception(name + " unexpected base mesh count " + files.Length);
                Array.Sort(files,StringComparer.Ordinal);
                Animator modelAnimator = null;
                foreach (string file in files)
                {
                    var mapped = ConfigureModel(name,file);
                    if (!modelAnimator) modelAnimator = mapped;
                }
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(output))
                {
                    string[] candidates = Directory.GetFiles(Root + name,"*.prefab",SearchOption.AllDirectories);
                    Array.Sort(candidates,StringComparer.Ordinal);
                    if(candidates.Length==0)throw new Exception(name+" source prefab missing");
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(candidates[0].Replace('\\','/'));
                    var instance=UnityEngine.Object.Instantiate(prefab);
                    try
                    {
                        instance.name=name+" Unarmed";
                        var animator=instance.GetComponent<Animator>();
                        animator.runtimeAnimatorController=null;animator.avatar=modelAnimator.avatar;animator.applyRootMotion=false;
                        foreach(var t in instance.GetComponentsInChildren<Transform>(true))
                        {
                            string n=t.name.ToLowerInvariant();
                            if(n.Contains("weapon")||n.Contains("gun")||n.Contains("plasma"))t.gameObject.SetActive(false);
                        }
                        foreach(var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        {
                            foreach(var material in skin.sharedMaterials)
                                if(material && (material.name.ToLowerInvariant().Contains("gun")||material.name.ToLowerInvariant().Contains("weapon")||material.name.ToLowerInvariant().Contains("plasma")))
                                    skin.enabled=false;
                            skin.updateWhenOffscreen=true;
                        }
                        using(var pose=new HumanPoseHandler(animator.avatar,animator.transform))
                        {var value=new HumanPose();pose.GetHumanPose(ref value);}
                        Directory.CreateDirectory("Assets/Resources/SuperBotPackVol1");AssetDatabase.Refresh();
                        PrefabUtility.SaveAsPrefabAsset(instance,output);
                    }
                    finally {UnityEngine.Object.DestroyImmediate(instance);}
                }
                var result=AssetDatabase.LoadAssetAtPath<GameObject>(output).GetComponent<Animator>();
                report.Add(name+" PASS valid Humanoid; source GUID preserved; unarmed prefab "+output);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllLines("Temp/superbot-install-result",report);
            Debug.Log("SUPERBOT_INSTALL_PASS 10 Humanoid unarmed characters");
        }
        catch(Exception e){File.WriteAllText("Temp/superbot-install-result",string.Join("\n",report)+"\nFAIL "+e);Debug.LogException(e);}
    }
    static Animator ConfigureModel(string name,string file)
    {
        string modelPath = file.Replace('\\','/');
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (!importer || !model) throw new Exception(name + " model not imported");
        if (importer.userData != Version)
        {
            var transforms = model.GetComponentsInChildren<Transform>(true);
            var human = new List<HumanBone>();
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;
                string suffix = BoneName(bone);
                if (suffix == null) continue;
                var target = Array.Find(transforms, t => t.name == "Character1_" + suffix);
                if (!target) continue;
                human.Add(new HumanBone { humanName = HumanTrait.BoneName[(int)bone], boneName = target.name,
                    limit = new HumanLimit { useDefaultValues = true } });
            }
            var skeleton = new SkeletonBone[transforms.Length];
            for (int i=0;i<transforms.Length;i++) skeleton[i] = new SkeletonBone { name=transforms[i].name,
                position=transforms[i].localPosition, rotation=transforms[i].localRotation, scale=transforms[i].localScale };
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.isReadable = true; importer.importAnimation = false;
            importer.humanDescription = new HumanDescription { human=human.ToArray(), skeleton=skeleton,
                upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f };
            importer.userData = Version; importer.SaveAndReimport();
            model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        }
        var modelAnimator = model.GetComponent<Animator>();
        if (!modelAnimator || !modelAnimator.avatar || !modelAnimator.avatar.isHuman || !modelAnimator.avatar.isValid)
            throw new Exception(name + " invalid Humanoid Avatar");
        return modelAnimator;
    }
    static string BoneName(HumanBodyBones bone)
    {
        switch(bone)
        {
            case HumanBodyBones.Hips:return "Hips";case HumanBodyBones.Spine:return "Spine";
            case HumanBodyBones.Chest:return "Spine1";case HumanBodyBones.UpperChest:return "Spine2";
            case HumanBodyBones.Neck:return "Neck";case HumanBodyBones.Head:return "Head";
            case HumanBodyBones.LeftShoulder:return "LeftShoulder";case HumanBodyBones.RightShoulder:return "RightShoulder";
            case HumanBodyBones.LeftUpperArm:return "LeftArm";case HumanBodyBones.RightUpperArm:return "RightArm";
            case HumanBodyBones.LeftLowerArm:return "LeftForeArm";case HumanBodyBones.RightLowerArm:return "RightForeArm";
            case HumanBodyBones.LeftHand:return "LeftHand";case HumanBodyBones.RightHand:return "RightHand";
            case HumanBodyBones.LeftUpperLeg:return "LeftUpLeg";case HumanBodyBones.RightUpperLeg:return "RightUpLeg";
            case HumanBodyBones.LeftLowerLeg:return "LeftLeg";case HumanBodyBones.RightLowerLeg:return "RightLeg";
            case HumanBodyBones.LeftFoot:return "LeftFoot";case HumanBodyBones.RightFoot:return "RightFoot";
            case HumanBodyBones.LeftToes:return "LeftToeBase";case HumanBodyBones.RightToes:return "RightToeBase";
        }
        string value=bone.ToString();string side=value.StartsWith("Left")?"Left":value.StartsWith("Right")?"Right":null;
        if(side==null)return null;
        foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Little"})
            if(value.Contains(finger))return side+"Hand"+(finger=="Little"?"Pinky":finger)+(value.EndsWith("Proximal")?"1":value.EndsWith("Intermediate")?"2":"3");
        return null;
    }
}
