using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mirror;
using MTPSKIT;
using MTPSKIT.Battlefield;
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
public static class DesertBattlefieldSetup
{
    const string Root="Assets/Game/Maps/WAVE_ONEWAY",Generated="Assets/Game/Battlefield/Generated";
    public const string ScenePath=Root+"/Scenes/map_desert_battlefield.unity";
    const string Hub=DNNetworkManager.MainMenuScene;
    [MenuItem("TPSBattle/Battlefield/Open Desert")]
    public static void Open(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()){EditorSceneManager.OpenScene(ScenePath);SceneView.lastActiveSceneView?.LookAt(new Vector3(-8,-40,0),Quaternion.Euler(45,30,0),170);}}
    static Vector3 Ground(Vector3 p)
    {
        if(!Physics.Raycast(p+Vector3.up*2,Vector3.down,out var hit,35,1,QueryTriggerInteraction.Ignore))throw new InvalidOperationException("Ground missing: "+p);
        return new Vector3(p.x,hit.point.y+.06f,p.z);
    }
    [MenuItem("TPSBattle/Battlefield/Create Desert")]
    public static void CreateAndBuild()
    {
        if(File.Exists(ScenePath))throw new InvalidOperationException("Desert scene exists; use BuildExisting to preserve edits.");
        AssetDatabase.Refresh();
        foreach(var path in Directory.GetFiles(Root+"/Generated","*.mat"))
        {
            string yaml=File.ReadAllText(path);var match=Regex.Match(yaml,@"m_Texture: \{fileID: 2800000, guid: (\w+)");
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(match.Groups[1].Value));
            if(!texture)throw new InvalidDataException("Texture missing: "+path);
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);bool clip=Regex.IsMatch(yaml,@"_AlphaClip: 1");mat.shader=Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap",texture);mat.SetTexture("_MainTex",texture);mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Smoothness",.02f);mat.SetFloat("_Cull",0);mat.SetFloat("_AlphaClip",clip?1:0);mat.SetFloat("_Cutoff",.4f);if(clip)mat.EnableKeyword("_ALPHATEST_ON");else mat.DisableKeyword("_ALPHATEST_ON");mat.renderQueue=clip?2450:2000;mat.doubleSidedGI=true;EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(Hub);var sky=RenderSettings.skybox;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var geometry=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Generated/WAVE_ONEWAY.prefab"));geometry.name="Desert_Map_OriginalScale_001";
        int meshes=0;foreach(var f in geometry.GetComponentsInChildren<MeshFilter>()){f.gameObject.layer=0;f.gameObject.AddComponent<MeshCollider>().sharedMesh=f.sharedMesh;meshes++;}
        Physics.SyncTransforms();
        var nav=geometry.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;nav.layerMask=1;nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;nav.overrideVoxelSize=true;nav.voxelSize=.16f;nav.BuildNavMesh();
        if(!nav.navMeshData)throw new InvalidOperationException("Navigation failed");AssetDatabase.CreateAsset(nav.navMeshData,Generated+"/DesertNavMesh.asset");
        var configPath=Root+"/Data/battlefield-runtime.json";var map=JsonUtility.FromJson<BattlefieldMapData>(File.ReadAllText(configPath));
        var names=new HashSet<string>(map.nodes.Select(n=>n.id));
        foreach(var n in map.nodes)
        {
            foreach(var next in new[]{n.next1,n.next2})if(!string.IsNullOrEmpty(next)&&!names.Contains(next))throw new InvalidDataException("Dangling edge "+n.id+" -> "+next);
            var ground=Ground(n.position);
            if(!NavMesh.SamplePosition(ground,out var hit,2,NavMesh.AllAreas))throw new InvalidOperationException("Node outside NavMesh: "+n.id+" "+ground);
            n.position=hit.position;
        }
        foreach(var spawn in map.spawns){spawn.position=Ground(spawn.position);if(spawn.period!=15||spawn.count!=4)throw new InvalidDataException("Unexpected source wave parameters");}
        foreach(var building in map.buildings)building.position=Ground(building.position);
        foreach(var player in map.playerSpawns)player.position=Ground(player.position);
        int validEdges=0;
        foreach(var n in map.nodes)foreach(var next in new[]{n.next1,n.next2})if(!string.IsNullOrEmpty(next))
        {
            var other=map.nodes.First(x=>x.id==next);var path=new NavMeshPath();
            if(!NavMesh.CalculatePath(n.position,other.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new InvalidOperationException("Unwalkable source edge: "+n.id+" -> "+next);
            validEdges++;
        }
        File.WriteAllText(configPath,JsonUtility.ToJson(map,true));AssetDatabase.Refresh();
        var settings=ScriptableObject.CreateInstance<BattlefieldSettings>();settings.UIFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Localization/LocalSimHei.ttf");settings.MapJson=AssetDatabase.LoadAssetAtPath<TextAsset>(configPath);
        settings.PlayerPrefab=MakeCharacter("BattlefieldPlayer",BattlefieldKind.Player,500);
        settings.MinionPrefab=MakeCharacter("BattlefieldMinion",BattlefieldKind.Minion,100);
        settings.TowerPrefab=MakeBuilding("BattlefieldTower",false);settings.BasePrefab=MakeBuilding("BattlefieldBase",true);
        AssetDatabase.CreateAsset(settings,Generated+"/DesertBattlefieldSettings.asset");
        var room=new GameObject("ROOMMANAGER_DesertBattlefield");room.AddComponent<NetworkIdentity>();room.AddComponent<RoomManager>();var mode=room.AddComponent<BattlefieldGamemode>();mode.Settings=settings;
        var spawns=new GameObject("PlayerSpawns_FromOriginalGameStartPoints");mode.PlayerSpawns=new SpawnpointsContainer[2];
        for(int team=0;team<2;team++){var g=new GameObject(team==0?"BluePlayerSpawns":"OrangePlayerSpawns");g.transform.SetParent(spawns.transform);var container=g.AddComponent<SpawnpointsContainer>();container.Spawnpoints=new List<Transform>();mode.PlayerSpawns[team]=container;}
        foreach(var p in map.playerSpawns){var t=new GameObject(p.id).transform;t.SetParent(mode.PlayerSpawns[p.team].transform);t.position=p.position;t.rotation=Quaternion.Euler(0,p.yaw,0);mode.PlayerSpawns[p.team].Spawnpoints.Add(t);}
        var sun=new GameObject("Desert_Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.2f;sun.color=new Color(1,.94f,.82f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(45,-25,0);sun.gameObject.AddComponent<UniversalAdditionalLightData>();RenderSettings.sun=sun;RenderSettings.skybox=sky;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.65f,.68f,.72f);RenderSettings.ambientEquatorColor=new Color(.5f,.48f,.43f);RenderSettings.ambientGroundColor=new Color(.35f,.32f,.28f);RenderSettings.fog=false;
        var kill=new GameObject("OutOfMap_DeathZone");kill.layer=(int)GameLayers.trigger;kill.transform.position=new Vector3(0,-85,0);var box=kill.AddComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(600,10,700);kill.AddComponent<NetworkIdentity>();kill.AddComponent<DeathZone>();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath);
        var rep=ScriptableObject.CreateInstance<MapRepresenter>();rep.Name="沙漠风暴 · 战场";rep.Scene=ScenePath;rep.AvailableGamemodes=new[]{Gamemodes.Battlefield};rep.MaxPlayersPresets=new[]{2,4,8};AssetDatabase.CreateAsset(rep,Generated+"/DesertMap.asset");
        EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
        var hub=EditorSceneManager.OpenScene(Hub);var creator=UnityEngine.Object.FindFirstObjectByType<RoomCreator>();creator.Maps=creator.Maps.Concat(new[]{rep}).ToArray();
        var manager=UnityEngine.Object.FindFirstObjectByType<DNNetworkManager>();foreach(var prefab in new[]{settings.PlayerPrefab,settings.MinionPrefab,settings.TowerPrefab,settings.BasePrefab})manager.spawnPrefabs.Add(prefab);
        EditorUtility.SetDirty(creator);EditorUtility.SetDirty(manager);EditorSceneManager.MarkSceneDirty(hub);EditorSceneManager.SaveScene(hub);AssetDatabase.SaveAssets();
        Debug.Log($"DESERT_BATTLEFIELD_CREATED meshes={meshes} nodes={map.nodes.Length} edges={validEdges} spawners={map.spawns.Length} buildings={map.buildings.Length} playerSpawns={map.playerSpawns.Length}");
        BuildExisting();
    }
    static GameObject MakeCharacter(string name,BattlefieldKind kind,int hp)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetStore/desNetware/Multiplayer TPS KIT/Levels/Prefabs/Gameplay/Character.prefab");
        var root=(GameObject)PrefabUtility.InstantiatePrefab(source);PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);root.name=name;
        var e=root.AddComponent<BattlefieldEntity>();e.Kind=kind;var health=root.GetComponent<Health>();health.MaxHealth=health.CurrentHealth=hp;
        if(kind==BattlefieldKind.Minion){root.GetComponent<CharacterMotor>().enabled=false;var agent=root.AddComponent<NavMeshAgent>();agent.radius=.35f;agent.height=1.8f;agent.enabled=false;}
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Generated+"/"+name+".prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
    }
    static GameObject MakeBuilding(string name,bool isBase)
    {
        var root=new GameObject(name);root.AddComponent<NetworkIdentity>();var health=root.AddComponent<Health>();health.CharacterName=isBase?"基地":"防御塔";health.centerPosition=Vector3.up*3;
        var e=root.AddComponent<BattlefieldEntity>();e.Kind=isBase?BattlefieldKind.Base:BattlefieldKind.Tower;
        var visual=new GameObject("Visual");visual.transform.SetParent(root.transform,false);e.VisualRoot=visual.transform;
        if(isBase)LoadModel("Onewaybase",visual.transform);else{LoadModel("OneWayTurret_Body",visual.transform);e.Turret=LoadModel("OneWayTurret_Gun",visual.transform).transform;}
        var renderers=visual.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);visual.transform.localPosition-=Vector3.up*bounds.min.y;
        var hit=new GameObject("BuildingHitbox");hit.layer=(int)GameLayers.hitbox;hit.transform.SetParent(root.transform,false);var box=hit.AddComponent<BoxCollider>();box.size=new Vector3(Mathf.Max(3,bounds.size.x),Mathf.Max(5,bounds.size.y),Mathf.Max(3,bounds.size.z));box.center=Vector3.up*(box.size.y*.5f);var hb=hit.AddComponent<HitBox>();hb._health=health;hb.part=CharacterPart.body;
        var obstacle=root.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=box.size;obstacle.center=box.center;obstacle.carving=true;
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Generated+"/"+name+".prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
    }
    static GameObject LoadModel(string id,Transform parent)
    {
        var root=new GameObject(id);root.transform.SetParent(parent,false);root.transform.localScale=Vector3.one*.01f;
        var matPath=Generated+"/BuildingSteel.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",new Color(.42f,.46f,.49f));mat.SetFloat("_Smoothness",.25f);AssetDatabase.CreateAsset(mat,matPath);}
        using(var reader=new BinaryReader(File.OpenRead("Assets/Game/Battlefield/SourceModels/"+id+".bytes")))
        {
            int count=reader.ReadInt32();for(int i=0;i<count;i++)
            {
                string label=reader.ReadString();reader.ReadString();int n=reader.ReadInt32();var vertices=new Vector3[n];var uv=new Vector2[n];for(int j=0;j<n;j++){vertices[j]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());uv[j]=new Vector2(reader.ReadSingle(),reader.ReadSingle());}int c=reader.ReadInt32();var triangles=new int[c];for(int j=0;j<c;j++)triangles[j]=reader.ReadInt32();
                var mesh=new Mesh{name=label,indexFormat=IndexFormat.UInt32};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Generated+"/"+id+"_"+i+".asset");
                var go=new GameObject(label);go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
            }
        }
        return root;
    }
    public static void BuildExisting()
    {
        var settings=AssetDatabase.LoadAssetAtPath<BattlefieldSettings>(Generated+"/DesertBattlefieldSettings.asset");
        if(!settings.TracerMaterial)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material,Generated+"/BattlefieldTracer.mat");settings.TracerMaterial=material;EditorUtility.SetDirty(settings);
        }
        AssetDatabase.SaveAssets();var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
        var report=BuildPipeline.BuildPlayer(scenes,"Builds/Windows/TPSShoot.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
        Debug.Log("DESERT_BATTLEFIELD_BUILD: "+report.summary.result);EditorApplication.Exit(report.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded?0:1);
    }
}
