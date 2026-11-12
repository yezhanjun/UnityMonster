using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class ResourceNavigation : EditorWindow
{
    const string BossScene="Assets/Monsters/Boss/BossSkillPreview.unity";
    const string MonsterScene="Assets/Monsters/Jotunnslayer/MonsterPreview/MonsterPreview.unity";
    [MenuItem("资源导航/打开分类导航", false, 0)]
    public static void Open(){GetWindow<ResourceNavigation>("资源导航").Show();}
    static void Folder(string path){var asset=AssetDatabase.LoadMainAssetAtPath(path);Selection.activeObject=asset;EditorGUIUtility.PingObject(asset);EditorUtility.FocusProjectWindow();}
    static void Scene(string path){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(path);}
    void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("UnityMonster 资源分类",EditorStyles.boldLabel);
        GUILayout.Label("按来源找资源，按预览入口查看模型与动作。",EditorStyles.wordWrappedLabel);
        GUILayout.Space(10);
        Category("VRC · 角色、服装与适配","Assets/VRC");
        Category("Monsters · Boss 与怪物","Assets/Monsters");
        if(GUILayout.Button("打开 Boss 技能预览",GUILayout.Height(28)))Scene(BossScene);
        if(GUILayout.Button("打开已提取怪物预览",GUILayout.Height(28)))Scene(MonsterScene);
        GUILayout.Space(8);
        Category("AssetStore · 完整商店资源包","Assets/AssetStore");
        foreach(string name in new[]{"KatanaAnimationPack","Sword and Shield Animation Pack","Paladin_Anim_Set"})
            if(GUILayout.Button(name))Folder("Assets/AssetStore/"+name);
        if(GUILayout.Button("打开 Paladin 原包预览"))Scene("Assets/AssetStore/Paladin_Anim_Set/ScenePreview/Paladin_Anim_Set.unity");
        GUILayout.Space(8);
        Category("Game · 项目场景与逻辑","Assets/Game");
        Category("ThirdParty · 插件","Assets/ThirdParty");
        GUILayout.Space(10);
        EditorGUILayout.HelpBox("Resources、StreamingAssets、TextMesh Pro 保留标准目录。怪物预览进入 Play 后使用；商店资源保留完整原包。",MessageType.Info);
    }
    static void Category(string label,string path){if(GUILayout.Button(label,GUILayout.Height(30)))Folder(path);}
    public static void OpenOrganizedProject(){EditorSceneManager.OpenScene(BossScene);Open();Folder("Assets");}
    public static void ValidateLayout()
    {
        try
        {
            Directory.CreateDirectory("Logs/AssetLayout");
            int count=0;
            foreach(string line in File.ReadAllLines("Docs/Validation/AssetLayout/expected-guids.tsv"))
            {
                string[] parts=line.Split('\t');
                string actual=AssetDatabase.GUIDToAssetPath(parts[0]);
                if(actual!=parts[1])throw new Exception("GUID path mismatch: "+parts[1]+" -> "+actual);
                count++;
            }
            foreach(string scenePath in new[]{BossScene,MonsterScene,"Assets/AssetStore/Paladin_Anim_Set/ScenePreview/Paladin_Anim_Set.unity"})
            {
                var scene=EditorSceneManager.OpenScene(scenePath);
                foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
                    if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing script in "+scenePath+" / "+t.name);
            }
            var gallery=AssetDatabase.LoadAssetAtPath<Jotunnslayer.Preview.MonsterPreviewGallery>("Assets/Monsters/Jotunnslayer/MonsterPreview/MonsterPreviewGallery.asset");
            if(!gallery||gallery.entries.Length!=65)throw new Exception("Monster gallery not intact");
            foreach(var entry in gallery.entries)
            {
                if(!entry.model||!AssetDatabase.IsValidFolder(entry.folder))throw new Exception("Missing monster model/folder: "+entry.id);
                foreach(var clip in entry.clips)if(!clip)throw new Exception("Missing monster clip: "+entry.id);
                foreach(var effect in entry.effects)if(!effect)throw new Exception("Missing monster effect: "+entry.id);
            }
            Debug.Log("ASSET_LAYOUT_PASS GUIDS="+count+" MONSTER_MODELS="+gallery.entries.Length);
            File.WriteAllText("Logs/AssetLayout/unity-validation.txt","PASS: "+count+" GUID paths; preview scenes have no missing scripts; all 65 monster gallery models, clips, effects and folder references resolve.\n");
            BossPreview.Editor.BossImportValidation.Validate();
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
