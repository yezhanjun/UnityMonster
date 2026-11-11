using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using MTPSKIT.Gameplay.Gamemodes;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class TransportshipSetup
{
    const string Root = "Assets/Maps/TRANSPORTSHIP";
    public const string PlayableScene = Root + "/Scenes/map_transportship.unity";
    const string Hub = DNNetworkManager.MainMenuScene;
    [Serializable] public class SourcePoints { public SourcePoint[] points; public float scale; }
    [Serializable] public class SourcePoint { public string Name; public int index, team; public float[] Pos, Rotation; }
    [Serializable] public class SpawnResult { public string sourceName; public int sourceIndex, team; public Vector3 originalScaledPosition, playablePosition; public float yawDegrees, floorOffset; public bool onNavMesh, pathToOtherTeam; }
    [Serializable] public class Report { public string scene=PlayableScene; public int meshes, materials, blueSpawns, orangeSpawns, navTriangles; public string teamMapping="Inferred integration assignment: negative X = blue; positive X = orange. Source has no team identifier."; public List<SpawnResult> points = new List<SpawnResult>(); }

    [MenuItem("TPSBattle/Maps/Create Transportship")]
    public static void Create()
    {
        if (File.Exists(PlayableScene)) throw new InvalidOperationException("Playable map already exists; refusing to overwrite manual edits.");
        AssetDatabase.Refresh();
        var report = new Report();
        foreach (var path in Directory.GetFiles(Root + "/Generated", "*.mat"))
        {
            string yaml = File.ReadAllText(path);
            var textureGuid = Regex.Match(yaml, @"m_Texture: \{fileID: 2800000, guid: (\w+)").Groups[1].Value;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(textureGuid));
            if (!texture) throw new InvalidDataException("Missing source texture: " + path);
            bool clip = Regex.IsMatch(yaml, @"_AlphaClip: 1");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", texture); mat.SetTexture("_MainTex", texture);
            mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Smoothness", .05f);
            mat.SetFloat("_Metallic", 0); mat.SetFloat("_Cull", 0);
            mat.SetFloat("_AlphaClip", clip ? 1 : 0); mat.SetFloat("_Cutoff", .4f);
            if (clip) mat.EnableKeyword("_ALPHATEST_ON"); else mat.DisableKeyword("_ALPHATEST_ON");
            mat.renderQueue = clip ? 2450 : 2000;
            mat.SetOverrideTag("RenderType", clip ? "TransparentCutout" : "Opaque");
            mat.doubleSidedGI = true; EditorUtility.SetDirty(mat); report.materials++;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/desNetware/Multiplayer TPS KIT/Levels/map_arena.unity");
        var sky = RenderSettings.skybox;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var geometry = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Generated/TRANSPORTSHIP.prefab"));
        geometry.name = "Transportship_Geometry_OriginalScale_001";
        foreach (var mf in geometry.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.sharedMesh || mf.sharedMesh.vertexCount == 0) continue;
            mf.gameObject.layer = 0;
            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            report.meshes++;
        }
        Physics.SyncTransforms();
        var source = JsonUtility.FromJson<SourcePoints>(File.ReadAllText(Root + "/Data/spawn-integration.json"));
        var spawnRoot = new GameObject("SpawnPoints_FromOriginalGameStartPoints");
        var groups = new SpawnpointsContainer[2];
        for (int team=0; team<2; team++)
        {
            var g = new GameObject(team==0 ? "Blue_OriginalNegativeX" : "Orange_OriginalPositiveX");
            g.transform.SetParent(spawnRoot.transform); groups[team]=g.AddComponent<SpawnpointsContainer>();
            groups[team].Spawnpoints=new List<Transform>();
        }
        foreach (var p in source.points.OrderBy(p=>p.index))
        {
            Vector3 pos = new Vector3(p.Pos[0], p.Pos[1], p.Pos[2]) * source.scale;
            if (!Physics.Raycast(pos + Vector3.up*.25f, Vector3.down, out var floor, 4f, 1, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("No floor below original spawn: " + p.Name + " at " + pos);
            var point = new GameObject(p.Name).transform;
            point.SetParent(groups[p.team].transform);
            point.position = new Vector3(pos.x, floor.point.y + .06f, pos.z);
            point.rotation = Quaternion.Euler(0, p.Rotation[1]*Mathf.Rad2Deg, 0);
            groups[p.team].Spawnpoints.Add(point);
            // Source object origin represents the old engine's character origin; this controller uses feet.
            report.points.Add(new SpawnResult { sourceName=p.Name, sourceIndex=p.index, team=p.team, originalScaledPosition=pos, playablePosition=point.position, yawDegrees=point.eulerAngles.y, floorOffset=point.position.y-pos.y });
        }
        report.blueSpawns=groups[0].Spawnpoints.Count; report.orangeSpawns=groups[1].Spawnpoints.Count;
        if (report.blueSpawns!=8 || report.orangeSpawns!=8) throw new InvalidDataException("Expected 8 + 8 original spawn points");
        var room = new GameObject("ROOMMANAGER");
        room.AddComponent<NetworkIdentity>(); room.AddComponent<RoomManager>();
        var mode=room.AddComponent<TeamDeathmatch>(); mode.ScoreToWin=3000;
        var so=new SerializedObject(mode);
        so.FindProperty("_spawnpointsTeamA").objectReferenceValue=groups[0];
        so.FindProperty("_spawnpointsTeamB").objectReferenceValue=groups[1]; so.ApplyModifiedPropertiesWithoutUndo();
        var sun = new GameObject("Transportship_Sun").AddComponent<Light>();
        sun.type=LightType.Directional; sun.intensity=1.3f; sun.shadows=LightShadows.Soft;
        sun.transform.rotation=Quaternion.Euler(48,-35,0); sun.gameObject.AddComponent<UniversalAdditionalLightData>();
        RenderSettings.sun=sun; RenderSettings.skybox=sky; RenderSettings.fog=false;
        RenderSettings.ambientMode=AmbientMode.Trilight; RenderSettings.ambientSkyColor=new Color(.6f,.65f,.72f);
        RenderSettings.ambientEquatorColor=new Color(.43f,.46f,.5f); RenderSettings.ambientGroundColor=new Color(.3f,.3f,.32f);
        var kill = new GameObject("OutOfMap_DeathZone"); kill.layer=(int)GameLayers.trigger;
        kill.transform.position=new Vector3(-6,-38,0); var box=kill.AddComponent<BoxCollider>(); box.isTrigger=true;box.size=new Vector3(500,8,500);
        kill.AddComponent<NetworkIdentity>();kill.AddComponent<DeathZone>();
        var nav=geometry.AddComponent<NavMeshSurface>(); nav.collectObjects=CollectObjects.Children;
        nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders; nav.layerMask=1;
        nav.overrideVoxelSize=true;nav.voxelSize=.12f; nav.BuildNavMesh();
        if(!nav.navMeshData)throw new InvalidOperationException("NavMesh generation failed");
        AssetDatabase.CreateAsset(nav.navMeshData,Root+"/Generated/Transportship_NavMesh.asset");
        report.navTriangles=NavMesh.CalculateTriangulation().indices.Length/3;
        foreach(var p in report.points)
        {
            p.onNavMesh=NavMesh.SamplePosition(p.playablePosition,out var hit,1f,NavMesh.AllAreas);
            var other=report.points.First(x=>x.team!=p.team);
            if(p.onNavMesh && NavMesh.SamplePosition(other.playablePosition,out var target,1f,NavMesh.AllAreas))
            {
                var path=new NavMeshPath(); p.pathToOtherTeam=NavMesh.CalculatePath(hit.position,target.position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete;
            }
        }
        EditorSceneManager.SaveScene(scene,PlayableScene); AssetDatabase.SaveAssets();
        File.WriteAllText(Root+"/Data/playable-validation.json",JsonUtility.ToJson(report,true));
        if(report.points.Any(p=>!p.onNavMesh || !p.pathToOtherTeam)) throw new InvalidOperationException("Some original spawn points lack connected navigation; inspect playable-validation.json");
        var rep=ScriptableObject.CreateInstance<MapRepresenter>();rep.Name="运输船";rep.Scene=PlayableScene;
        rep.AvailableGamemodes=new[]{Gamemodes.TeamDeathmatch};rep.MaxPlayersPresets=new[]{4,8,16};
        AssetDatabase.CreateAsset(rep,Root+"/Data/TransportshipMap.asset");
        EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(PlayableScene,true)}).ToArray();
        var hub=EditorSceneManager.OpenScene(Hub);
        var creator=UnityEngine.Object.FindFirstObjectByType<RoomCreator>();
        if(!creator)throw new InvalidOperationException("Hub RoomCreator missing");
        creator.Maps=creator.Maps.Concat(new[]{rep}).ToArray();EditorUtility.SetDirty(creator);EditorSceneManager.MarkSceneDirty(hub);EditorSceneManager.SaveScene(hub);
        AssetDatabase.SaveAssets();
        Debug.Log($"TRANSPORTSHIP_READY meshes={report.meshes} materials={report.materials} blue={report.blueSpawns} orange={report.orangeSpawns} navTriangles={report.navTriangles}");
    }
    [MenuItem("TPSBattle/Maps/Open Transportship")]
    public static void OpenPlayable()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(PlayableScene);
        EditorApplication.delayCall += () =>
        {
            if (SceneView.lastActiveSceneView)
                SceneView.lastActiveSceneView.LookAt(new Vector3(-6,-12,0),Quaternion.Euler(45,35,0),65);
        };
    }
    public static void CreateAndBuild()
    {
        Create();
        BuildExisting();
    }
    public static void BuildExisting()
    {
        var scene=EditorSceneManager.OpenScene(PlayableScene);
        Physics.SyncTransforms();
        var containers=UnityEngine.Object.FindObjectsByType<SpawnpointsContainer>(FindObjectsSortMode.None);
        int points=0;
        foreach(var container in containers)foreach(var point in container.Spawnpoints)
        {
            for(int sample=0;sample<9;sample++)
            {
                Vector3 offset=sample==0?Vector3.zero:Quaternion.Euler(0,(sample-1)*45,0)*Vector3.forward*.4f;
                Vector3 foot=point.position+offset;
                if(Physics.CheckCapsule(foot+Vector3.up*.55f,foot+Vector3.up*1.5f,.48f,1,QueryTriggerInteraction.Ignore))
                    throw new InvalidOperationException("Spawn capsule blocked (including template random offset): "+point.name+" sample="+sample);
            }
            points++;
        }
        Debug.Log("TRANSPORTSHIP_CAPSULES_CLEAR points="+points+" samples="+(points*9));
        var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
        var result=BuildPipeline.BuildPlayer(scenes,"Builds/Windows/TPSShoot.exe",BuildTarget.StandaloneWindows64,UnityEditor.BuildOptions.Development);
        Debug.Log("TRANSPORTSHIP_BUILD: "+result.summary.result);
        EditorApplication.Exit(result.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded?0:1);
    }
}
