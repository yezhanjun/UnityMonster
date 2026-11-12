#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MTPSKIT.Gameplay;
using MTPSKIT.Gameplay.Gamemodes;
namespace MTPSKIT.BuildTools
{
    public static class TrainingRangeSetup
    {
        public const string Scene="Assets/Game/Baseline/Scenes/map_training.unity";
        public static MapRepresenter PrepareRain(MapRepresenter training)
        {
            const string scene = "Assets/Game/Baseline/Scenes/map_rain_coop.unity";
            const string asset = "Assets/Resources/RainCoopMap.asset";
            if (!System.IO.File.Exists(scene) && !AssetDatabase.CopyAsset(Scene, scene))
                throw new System.InvalidOperationException("Could not create Rain co-op scene.");
            PrepareChapterOne(scene);
            var map = AssetDatabase.LoadAssetAtPath<MapRepresenter>(asset);
            if (!map)
            {
                map = Object.Instantiate(training);
                map.Scene = scene;
                map.Name = "雨中冒险";
                AssetDatabase.CreateAsset(map, asset);
            }
            if (!EditorBuildSettings.scenes.Any(s => s.path == scene))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(scene, true) }).ToArray();
            return map;
        }

        static void PrepareChapterOne(string path)
        {
            var scene = EditorSceneManager.OpenScene(path);
            var roots = scene.GetRootGameObjects();
            if (roots.Any(root => root.name == "Chapter One")) return;

            foreach (var root in roots)
            {
                if (root.name == "map_range" || root.name == "ItemSpawners" || root.name == "Reflection Probe" ||
                    root.name == "prop_granadeSupply" || root.name == "prop_healthpack")
                    Object.DestroyImmediate(root);
            }

            var arena = new GameObject("Chapter One").transform;
            // ChapterOne's ArenaBuilder grid and cover coordinates from the local Jotunnslayer reference.
            string[] layout = { "001100", "011100", "111110", "001110", "000111", "000110" };
            for (int z = 0; z < layout.Length; z++)
            for (int x = 0; x < layout[z].Length; x++)
            {
                if (layout[z][x] == '0') continue;
                var center = new Vector3((x - 2.5f) * 14f, -1f, (z - 2.5f) * 14f);
                AddBlock(arena, $"Sector {x}-{z}", center, new Vector3(14, 2, 14));
                foreach (var edge in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    int adjacentX = x + edge.x, adjacentZ = z + edge.y;
                    if (adjacentX >= 0 && adjacentX < 6 && adjacentZ >= 0 && adjacentZ < 6 &&
                        layout[adjacentZ][adjacentX] == '1') continue;
                    AddBlock(arena, "Boundary wall", center + new Vector3(edge.x * 7, 2, edge.y * 7),
                        new Vector3(edge.x == 0 ? 14 : .5f, 2, edge.y == 0 ? 14 : .5f));
                }
            }
            foreach (var point in new[] { new Vector3(7,1,-18), new Vector3(18,1,6),
                new Vector3(-22,1,-8), new Vector3(8,1,26), new Vector3(-5,1,-27) })
                AddBlock(arena, "TPS Cover", point, new Vector3(4, 2, 2));

            var players = roots.First(root => root && root.name == "SpawnPoints_Players").GetComponent<SpawnpointsContainer>();
            players.Spawnpoints[0].SetPositionAndRotation(new Vector3(-7, 1, -7), Quaternion.identity);
            players.Spawnpoints[1].SetPositionAndRotation(new Vector3(-10, 1, -7), Quaternion.identity);
            var enemies = roots.First(root => root && root.name == "SpawnPoints_Bots").GetComponent<SpawnpointsContainer>();
            var enemyPoints = new[] { new Vector3(7, 1, -5), new Vector3(-18, 1, -5),
                new Vector3(4, 1, 6), new Vector3(-8, 1, -18), new Vector3(11, 1, -10) };
            for (int i = 0; i < enemies.Spawnpoints.Count; i++)
                enemies.Spawnpoints[i].SetPositionAndRotation(enemyPoints[i % enemyPoints.Length], Quaternion.identity);
            roots.First(root => root && root.name == "DeathTrigger").transform.position = new Vector3(0, -12.5f, 0);

            if (!EditorSceneManager.SaveScene(scene))
                throw new System.InvalidOperationException("Could not save the ChapterOne co-op arena.");
        }

        static void AddBlock(Transform arena, string name, Vector3 position, Vector3 size)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(arena);
            block.transform.localPosition = position;
            block.transform.localScale = size;
        }

        public static MapRepresenter Prepare()
        {
            const string original="Assets/AssetStore/desNetware/Multiplayer TPS KIT/Levels/map_range.unity";
            if(!System.IO.File.Exists(Scene))
            {
                AssetDatabase.CopyAsset(original,Scene);
                var scene=EditorSceneManager.OpenScene(Scene);
                int removed=0;
                foreach(var spawner in Object.FindObjectsByType<ItemSpawner>(FindObjectsSortMode.None))
                    if(spawner.ItemPrefab&&spawner.ItemPrefab.GetComponent<Item>())
                    {Object.DestroyImmediate(spawner.gameObject);removed++;}
                foreach(var item in Object.FindObjectsByType<Item>(FindObjectsSortMode.None))
                    {Object.DestroyImmediate(item.gameObject);removed++;}
                EditorSceneManager.SaveScene(scene);
                Debug.Log("TRAINING_RANGE_REMOVED_WEAPON_OBJECTS="+removed);
            }
            const string asset="Assets/Resources/TrainingMap.asset";
            var map=AssetDatabase.LoadAssetAtPath<MapRepresenter>(asset);
            if(!map)
            {
                var source=AssetDatabase.FindAssets("t:MapRepresenter").Select(g=>AssetDatabase.LoadAssetAtPath<MapRepresenter>(AssetDatabase.GUIDToAssetPath(g))).First(m=>m.Scene==original);
                map=Object.Instantiate(source);map.Scene=RoomCreator.TrainingScenePath;map.Name="训练场";
                AssetDatabase.CreateAsset(map,asset);
            }
            if(!EditorBuildSettings.scenes.Any(s=>s.path==Scene))EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(Scene,true)}).ToArray();
            if (map.Scene != RoomCreator.TrainingScenePath)
            {
                map.Scene = RoomCreator.TrainingScenePath;
                EditorUtility.SetDirty(map);
            }
            if (!EditorBuildSettings.scenes.Any(s => s.path == RoomCreator.TrainingScenePath && s.enabled))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != RoomCreator.TrainingScenePath)
                    .Concat(new[] { new EditorBuildSettingsScene(RoomCreator.TrainingScenePath, true) }).ToArray();
            return map;
        }
    }
}
#endif
