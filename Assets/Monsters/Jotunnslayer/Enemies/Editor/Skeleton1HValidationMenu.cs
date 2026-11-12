using MTPSKIT;
using UnityEditor;
using UnityEngine;

namespace Jotunnslayer
{
    internal static class Skeleton1HValidationMenu
    {
        const string SphinxMenu = "Tools/Jotunnslayer/Spawn Sphinx Skeleton";
        const string HybMenu = "Tools/Jotunnslayer/Spawn Hyb Sub Boss Easy";
        const string AlfheimFirstMenu = "Tools/Jotunnslayer/Spawn Alfheim Boss Phase 1";
        const string AlfheimSecondMenu = "Tools/Jotunnslayer/Spawn Alfheim Boss Phase 2";
        const string GhostMenu = "Tools/Jotunnslayer/Spawn Ghost";
        const string BossMenu = "Tools/Jotunnslayer/Spawn Niflheim Boss Easy";
        const string GuardianMenu = "Tools/Jotunnslayer/Spawn Skeleton Guardian";
        const string SpiderMenu = "Tools/Jotunnslayer/Spawn Spider Regular";
        const string BowMenu = "Tools/Jotunnslayer/Spawn Skeleton Bow Regular";
        const string GuardianWaveMenu = "Tools/Jotunnslayer/Start Niflheim Easy W8";
        const string MixedWaveMenu = "Tools/Jotunnslayer/Start Niflheim Easy W7";
        const string WaveMenu = "Tools/Jotunnslayer/Start Niflheim Easy Skeleton Wave";
        const string MenuPath = "Tools/Jotunnslayer/Spawn Skeleton 1H";

        [MenuItem("Tools/Jotunnslayer/Validate Bat Loot Assets")]
        public static void ValidateBatLootAssets()
        {
            const string folder = "Jotunnslayer/BatLoot/";
            var model = Resources.Load<GameObject>(folder + "bat_loot_sur");
            var animator = model ? model.GetComponent<Animator>() : null;
            var skin = model ? model.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            if (!animator || !animator.avatar || !animator.avatar.isValid || animator.avatar.isHuman ||
                !skin || !skin.sharedMesh || skin.sharedMesh.vertexCount == 0 ||
                skin.bones.Length == 0 || skin.bones.Length != skin.sharedMesh.bindposeCount)
                throw new System.InvalidOperationException("Bat Loot requires a Generic Avatar and complete skin bindings.");
            foreach (var bone in skin.bones)
                if (!bone) throw new System.InvalidOperationException("Bat Loot has an unresolved skin bone.");
            foreach (string name in new[] { "bat_loot_dm", "bat_loot_nm", "bat_loot_rm", "bat_loot_sm", "BloodOverlayMap" })
                if (!Resources.Load<Texture2D>(folder + name))
                    throw new System.InvalidOperationException($"Bat Loot texture missing: {name}.");
            var normal = AssetImporter.GetAtPath("Assets/Monsters/Jotunnslayer/Enemies/Resources/" + folder +
                "bat_loot_nm.png") as TextureImporter;
            if (!normal || normal.textureType != TextureImporterType.NormalMap)
                throw new System.InvalidOperationException("Bat Loot normal texture has incorrect import settings.");

            int bindings = 0;
            foreach (string name in new[] { "bat_n_init", "bat_n_stand_40fps", "bat_n_run", "bat_n_death",
                         "bat_n_hit_forward", "bat_n_hit_backward", "bat_n_hit_left", "bat_n_hit_right", "bat_n_skill_scream" })
            {
                var clip = Resources.Load<AnimationClip>(folder + name);
                if (!clip || clip.legacy || clip.humanMotion || clip.length <= 0f)
                    throw new System.InvalidOperationException($"Bat Loot Generic clip missing: {name}.");
                bool bound = false;
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (binding.type != typeof(Transform) || binding.path.Length == 0) continue;
                    if (!animator.transform.Find(binding.path))
                        throw new System.InvalidOperationException($"Bat Loot clip {name} cannot bind {binding.path}.");
                    bound = true;
                    bindings++;
                }
                if (!bound) throw new System.InvalidOperationException($"Bat Loot clip {name} has no bone curves.");
            }
            Debug.Log($"JOTUNN_BAT_LOOT_ASSETS_PASSED bones={skin.bones.Length} bindings={bindings} clips=9 textures=5");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static void ValidateHorrorJotunAndFormalMatch()
        {
            ValidateHorrorJotunAssets();
            HyboreaMatchValidation.RunBatch();
        }

        [MenuItem("Tools/Jotunnslayer/Validate Horror Alf Assets")]
        public static void ValidateHorrorAlfAssets()
        {
            const string folder = "Jotunnslayer/HorrorAlf/";
            var model = Resources.Load<GameObject>(folder + "horror_expl_alf_sur");
            var animator = model ? model.GetComponent<Animator>() : null;
            var skin = model ? model.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            if (!animator || !animator.avatar || !animator.avatar.isValid || animator.avatar.isHuman ||
                !skin || !skin.sharedMesh || skin.sharedMesh.vertexCount == 0 ||
                skin.bones.Length == 0 || skin.bones.Length != skin.sharedMesh.bindposeCount)
                throw new System.InvalidOperationException("Horror Alf requires a Generic Avatar and complete skin bindings.");
            foreach (var bone in skin.bones)
                if (!bone) throw new System.InvalidOperationException("Horror Alf has an unresolved skin bone.");

            foreach (string suffix in new[] { "ao", "dm", "em", "nm", "sm" })
                if (!Resources.Load<Texture2D>(folder + "horror_expl_alf_" + suffix))
                    throw new System.InvalidOperationException($"Horror Alf texture missing: {suffix}.");
            var normal = AssetImporter.GetAtPath("Assets/Monsters/Jotunnslayer/Enemies/Resources/" + folder +
                "horror_expl_alf_nm.png") as TextureImporter;
            if (!normal || normal.textureType != TextureImporterType.NormalMap)
                throw new System.InvalidOperationException("Horror Alf normal texture has incorrect import settings.");

            int bindings = 0;
            int sampledClips = 0;
            // Validate all recovered source clips, including the source death clip.
            // Resource validation does not establish original runtime action selection.
            foreach (string name in new[] { "attack_stand_01", "attack_stand_death", "horror_suicide",
                         "run_01", "stun_01", "summon" })
            {
                var clip = Resources.Load<AnimationClip>(folder + name);
                if (!clip || clip.legacy || clip.humanMotion || clip.length <= 0f)
                    throw new System.InvalidOperationException($"Horror Alf Generic clip missing: {name}.");
                bool bound = false;
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (binding.type != typeof(Transform) || binding.path.Length == 0) continue;
                    if (!animator.transform.Find(binding.path))
                        throw new System.InvalidOperationException($"Horror Alf clip {name} cannot bind {binding.path}.");
                    bound = true;
                    bindings++;
                }
                if (!bound) throw new System.InvalidOperationException($"Horror Alf clip {name} has no bone curves.");
                ValidateGenericMonsterPose(model, clip);
                sampledClips++;
            }
            var attack = Resources.Load<AnimationClip>(folder + "horror_suicide");
            var events = AnimationUtility.GetAnimationEvents(attack);
            var names = new[] { "OnCastEvent", "OnExplode", "OnCastEndEvent" };
            var times = new[] { .12783404f, 1.285815f, 1.3091004f };
            if (events.Length != names.Length)
                throw new System.InvalidOperationException("Horror Alf attack event count changed.");
            for (int i = 0; i < events.Length; i++)
                if (events[i].functionName != names[i] || Mathf.Abs(events[i].time - times[i]) > .0001f ||
                    events[i].floatParameter != 0f)
                    throw new System.InvalidOperationException($"Horror Alf attack event changed at index {i}.");
            Debug.Log($"JOTUNN_HORROR_ALF_ASSETS_PASSED bones={skin.bones.Length} bindings={bindings} clips=6 textures=5 events=3 sampledClips={sampledClips}");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        [MenuItem("Tools/Jotunnslayer/Validate Existing Exploder And Bat Poses")]
        public static void ValidateExistingExploderAndBatPoses()
        {
            var groups = new[]
            {
                (folder: "Jotunnslayer/HorrorJotun/", model: "horror_expl_jotun_sur",
                    clips: new[] { "jotunheim_exploder_attack1_skill", "jotunheim_exploder_idle",
                        "jotunheim_exploder_init", "jotunheim_exploder_move" }),
                (folder: "Jotunnslayer/BatLoot/", model: "bat_loot_sur",
                    clips: new[] { "bat_n_init", "bat_n_stand_40fps", "bat_n_run", "bat_n_death",
                        "bat_n_hit_forward", "bat_n_hit_backward", "bat_n_hit_left", "bat_n_hit_right",
                        "bat_n_skill_scream" })
            };
            int passed = 0;
            int failed = 0;
            foreach (var group in groups)
            {
                var model = Resources.Load<GameObject>(group.folder + group.model);
                foreach (string name in group.clips)
                {
                    try
                    {
                        var clip = Resources.Load<AnimationClip>(group.folder + name);
                        if (!model || !clip)
                            throw new System.InvalidOperationException("Model or animation missing.");
                        ValidateGenericMonsterPose(model, clip);
                        passed++;
                        Debug.Log($"JOTUNN_GENERIC_POSE_CHECK_PASSED model={group.model} clip={name}");
                    }
                    catch (System.Exception exception)
                    {
                        failed++;
                        Debug.LogError($"JOTUNN_GENERIC_POSE_CHECK_FAILED model={group.model} clip={name}: {exception.Message}");
                    }
                }
            }
            if (failed != 0)
                throw new System.InvalidOperationException($"JOTUNN_GENERIC_POSE_AUDIT_FAILED passed={passed} failed={failed}");
            Debug.Log($"JOTUNN_GENERIC_POSE_AUDIT_PASSED clips={passed}");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static void ValidateGenericMonsterPose(GameObject model, AnimationClip clip)
        {
            GameObject instance = null;
            Mesh first = null;
            Mesh second = null;
            try
            {
                instance = Object.Instantiate(model);
                instance.hideFlags = HideFlags.HideAndDontSave;
                var animator = instance.GetComponent<Animator>();
                animator.runtimeAnimatorController = null;
                animator.enabled = false;
                var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                first = new Mesh();
                second = new Mesh();
                ValidateGenericMonsterTransforms(instance, clip.name + " before sampling");
                clip.SampleAnimation(instance, 0f);
                ValidateGenericMonsterTransforms(instance, clip.name + " at zero");
                skin.BakeMesh(first);
                clip.SampleAnimation(instance, clip.length * .5f);
                ValidateGenericMonsterTransforms(instance, clip.name + " at midpoint");
                skin.BakeMesh(second);
                var start = first.vertices;
                var middle = second.vertices;
                if (start.Length == 0 || start.Length != middle.Length)
                    throw new System.InvalidOperationException($"{model.name} clip {clip.name} has invalid sampled skin.");
                bool changed = false;
                for (int i = 0; i < middle.Length; i++)
                {
                    Vector3 vertex = middle[i];
                    if (float.IsNaN(vertex.x) || float.IsInfinity(vertex.x) ||
                        float.IsNaN(vertex.y) || float.IsInfinity(vertex.y) ||
                        float.IsNaN(vertex.z) || float.IsInfinity(vertex.z))
                        throw new System.InvalidOperationException($"{model.name} clip {clip.name} has nonfinite sampled skin.");
                    if ((vertex - start[i]).sqrMagnitude > .00000001f) changed = true;
                }
                if (!changed)
                    throw new System.InvalidOperationException($"{model.name} clip {clip.name} did not deform the skin.");
                int sampleFrames = Mathf.Max(1, Mathf.CeilToInt(clip.length * 30f));
                for (int frame = 0; frame <= sampleFrames; frame++)
                {
                    float sampleTime = clip.length * frame / sampleFrames;
                    clip.SampleAnimation(instance, sampleTime);
                    ValidateGenericMonsterTransforms(instance, clip.name + " at " + sampleTime);
                }
            }
            finally
            {
                if (first) Object.DestroyImmediate(first);
                if (second) Object.DestroyImmediate(second);
                if (instance) Object.DestroyImmediate(instance);
            }
        }

        static void ValidateGenericMonsterTransforms(GameObject instance, string stage)
        {
            foreach (var bone in instance.GetComponentsInChildren<Transform>(true))
            {
                Matrix4x4 matrix = bone.localToWorldMatrix;
                for (int i = 0; i < 16; i++)
                    if (float.IsNaN(matrix[i]) || float.IsInfinity(matrix[i]))
                        throw new System.InvalidOperationException($"{instance.name} {stage}: invalid matrix at {bone.name}, position={bone.localPosition}, rotation={bone.localRotation}, scale={bone.localScale}.");
            }
        }

        [MenuItem("Tools/Jotunnslayer/Validate Horror Jotun Assets")]
        public static void ValidateHorrorJotunAssets()
        {
            const string folder = "Jotunnslayer/HorrorJotun/";
            var model = Resources.Load<GameObject>(folder + "horror_expl_jotun_sur");
            var animator = model ? model.GetComponent<Animator>() : null;
            var skin = model ? model.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            if (!animator || !animator.avatar || !animator.avatar.isValid || animator.avatar.isHuman ||
                !skin || !skin.sharedMesh || skin.sharedMesh.vertexCount == 0 ||
                skin.bones.Length == 0 || skin.bones.Length != skin.sharedMesh.bindposeCount)
                throw new System.InvalidOperationException("Horror Jotun requires a Generic Avatar and complete skin bindings.");
            foreach (var bone in skin.bones)
                if (!bone) throw new System.InvalidOperationException("Horror Jotun has an unresolved skin bone.");

            foreach (string suffix in new[] { "ao", "dm", "em", "nm", "sm" })
                if (!Resources.Load<Texture2D>(folder + "horror_jotunheim_" + suffix))
                    throw new System.InvalidOperationException($"Horror Jotun texture missing: {suffix}.");
            var normal = AssetImporter.GetAtPath("Assets/Monsters/Jotunnslayer/Enemies/Resources/" + folder +
                "horror_jotunheim_nm.png") as TextureImporter;
            if (!normal || normal.textureType != TextureImporterType.NormalMap)
                throw new System.InvalidOperationException("Horror Jotun normal texture has incorrect import settings.");

            int bindings = 0;
            // Validate every clip actually bound by the adapter. The source death clip has unresolved
            // hashes for a different hierarchy; original deathType=Destroy does not play it here.
            foreach (string name in new[] { "jotunheim_exploder_attack1_skill",
                         "jotunheim_exploder_idle", "jotunheim_exploder_init", "jotunheim_exploder_move" })
            {
                var clip = Resources.Load<AnimationClip>(folder + name);
                if (!clip || clip.legacy || clip.humanMotion || clip.length <= 0f)
                    throw new System.InvalidOperationException($"Horror Jotun Generic clip missing: {name}.");
                bool bound = false;
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (binding.type != typeof(Transform) || binding.path.Length == 0) continue;
                    if (!animator.transform.Find(binding.path))
                        throw new System.InvalidOperationException($"Horror Jotun clip {name} cannot bind {binding.path}.");
                    bound = true;
                    bindings++;
                }
                if (!bound) throw new System.InvalidOperationException($"Horror Jotun clip {name} has no bone curves.");
            }
            var attack = Resources.Load<AnimationClip>(folder + "jotunheim_exploder_attack1_skill");
            var events = AnimationUtility.GetAnimationEvents(attack);
            var names = new[] { "OnCastEvent", "OnExplode", "OnCastEndEvent" };
            var times = new[] { .08489895f, .6666292f, .6873687f };
            if (events.Length != names.Length)
                throw new System.InvalidOperationException("Horror Jotun attack event count changed.");
            for (int i = 0; i < events.Length; i++)
                if (events[i].functionName != names[i] || Mathf.Abs(events[i].time - times[i]) > .0001f ||
                    events[i].floatParameter != 0f)
                    throw new System.InvalidOperationException($"Horror Jotun attack event changed at index {i}.");
            Debug.Log($"JOTUNN_HORROR_JOTUN_ASSETS_PASSED bones={skin.bones.Length} bindings={bindings} clips=4 textures=5 events=3 sourceDeathClipUnrestored=True");
        }

        [MenuItem("Tools/Jotunnslayer/Validate Milipede Assets")]
        public static void ValidateMilipedeAssets()
        {
            const string folder = "Jotunnslayer/Milipede/";
            var model = Resources.Load<GameObject>(folder + "milipede_sur");
            var animator = model ? model.GetComponent<Animator>() : null;
            var avatar = animator ? animator.avatar : null;
            if (!avatar || !avatar.isValid || avatar.isHuman ||
                !model.GetComponentInChildren<SkinnedMeshRenderer>())
                throw new System.InvalidOperationException("Milipede model or Generic Avatar is unavailable.");

            foreach (string name in new[] { "necropede_negro_dm", "necropede_negro_nm", "necropede_negro_em",
                         "necropede_negro_sm", "necropede_rm" })
                if (!Resources.Load<Texture2D>(folder + name))
                    throw new System.InvalidOperationException($"Milipede Texture2D missing: {name}.");

            foreach (string name in new[] { "appear_summon", "stand_attack", "run", "hit", "attack_melee_01", "spit" })
            {
                var clip = Resources.Load<AnimationClip>(folder + "mili_gr_" + name);
                if (!clip || clip.humanMotion)
                    throw new System.InvalidOperationException($"Milipede Generic clip missing: {name}.");
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if (binding.path.Length > 0 && !animator.transform.Find(binding.path))
                    {
                        var paths = System.Array.ConvertAll(model.GetComponentsInChildren<Transform>(true),
                            node => AnimationUtility.CalculateTransformPath(node, animator.transform));
                        Debug.Log($"JOTUNN_MILIPEDE_HIERARCHY {string.Join(", ", paths)}");
                        throw new System.InvalidOperationException($"Milipede clip {name} cannot bind {binding.path}.");
                    }
            }

            Debug.Log("JOTUNN_MILIPEDE_ASSETS_PASSED");
        }

        [MenuItem(MenuPath, validate = true)]
        [MenuItem(BowMenu, validate = true)]
        [MenuItem(SpiderMenu, validate = true)]
        [MenuItem(GuardianMenu, validate = true)]
        [MenuItem(BossMenu, validate = true)]
        [MenuItem(GhostMenu, validate = true)]
        [MenuItem(AlfheimFirstMenu, validate = true)]
        [MenuItem(HybMenu, validate = true)]
        [MenuItem(SphinxMenu, validate = true)]
        [MenuItem(AlfheimSecondMenu, validate = true)]
        static bool CanSpawn() => EditorApplication.isPlaying && GameManager.myPlayerInstance;

        [MenuItem(WaveMenu, validate = true)]
        [MenuItem(MixedWaveMenu, validate = true)]
        [MenuItem(GuardianWaveMenu, validate = true)]
        static bool CanStartWave() => CanSpawn();

        [MenuItem(WaveMenu)]
        static void StartWave() => StartWave(4);

        [MenuItem(MixedWaveMenu)]
        static void StartMixedWave() => StartWave(7);

        [MenuItem(GuardianWaveMenu)]
        static void StartGuardianWave() => StartWave(8);

        static void StartWave(int index)
        {
            var player = GameManager.myPlayerInstance;
            if (player && NiflheimEasySkeletonWave.TryStart(player.MyCharacter, Time.time, index))
                Debug.Log($"Niflheim W{index} scheduled in {NiflheimEasySkeletonWave.GetStartTime(index)} seconds.");
            else
                Debug.LogWarning("Niflheim wave needs a living local host and can only be started once per actor.");
        }

        [MenuItem(MenuPath)]
        static void Spawn() => Spawn(Skeleton1HSpawn.Kind.Melee);

        [MenuItem(BowMenu)]
        static void SpawnBow() => Spawn(Skeleton1HSpawn.Kind.Bow);

        [MenuItem(SpiderMenu)]
        static void SpawnSpider() => Spawn(Skeleton1HSpawn.Kind.Spider);

        [MenuItem(GuardianMenu)]
        static void SpawnGuardian() => Spawn(Skeleton1HSpawn.Kind.Guardian);

        [MenuItem(BossMenu)]
        static void SpawnBoss() => Spawn(Skeleton1HSpawn.Kind.NiflheimBossEasy);

        [MenuItem(GhostMenu)]
        static void SpawnGhost() => Spawn(Skeleton1HSpawn.Kind.Ghost);

        [MenuItem(AlfheimFirstMenu)]
        static void SpawnAlfheimFirst() => Spawn(Skeleton1HSpawn.Kind.AlfheimPhase1);

        [MenuItem(AlfheimSecondMenu)]
        static void SpawnAlfheimSecond() => Spawn(Skeleton1HSpawn.Kind.AlfheimPhase2);

        [MenuItem(HybMenu)]
        static void SpawnHyb() => Spawn(Skeleton1HSpawn.Kind.HybSubBossEasy);

        [MenuItem(SphinxMenu)]
        static void SpawnSphinx() => Spawn(Skeleton1HSpawn.Kind.SphinxSkeleton);

        static void Spawn(Skeleton1HSpawn.Kind kind)
        {
            var player = GameManager.myPlayerInstance;
            if (!player || !Skeleton1HSpawn.TrySpawnInFrontOf(player.MyCharacter, kind))
            {
                Debug.LogWarning("Skeleton 1H spawn failed. Check local host, active character, ground and imported assets.");
                return;
            }

            Debug.Log("Skeleton spawned in the local host match.");
        }
    }
}
