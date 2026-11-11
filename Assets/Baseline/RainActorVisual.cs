using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace MTPSKIT.Gameplay
{
    public sealed class RainActorVisual : MonoBehaviour
    {
        GameObject model;
        Animator animator;
        PlayableGraph graph;
        AnimationClipPlayable playing;
        AnimationLayerMixerPlayable clayLayer;
        AnimationClipPlayable clayBase;
        AnimationPlayableOutput output;
        AnimationClip idle,move,attack,current;
        AnimationClip beamLoop, beamEnd, guardSlam, guardSunder;
        AnimationClip spawn, death;
        float spawnDuration;
        AnimationClip queenCharge, queenSpit, queenSummon, queenWards;
        AnimationClip bruiserBreathPrep, bruiserBreath, bruiserBreathExit, bruiserCharge, bruiserFire;
        AnimationClip wispCharge, wispFire, jellyfishNova;
        AnimationClip greaterWispCharge, greaterWispFire;
        AnimationClip impSlash, impBlink, impBlinkExit;
        AnimationClip bisonHeadbutt, bisonChargePrep, bisonCharge, bisonStun;
        AnimationClip verminBite1, verminBite2, verminBite3;
        AnimationClip flyingVerminSpit, flyingVerminDeathLoop;
        AnimationClip hermitBurrowIn, hermitBurrowed, hermitBurrowOut, hermitFire;
        AnimationClip acidLarvaLeapStart, acidLarvaLeapLoop, acidLarvaLanding;
        AnimationClip lemurianBite, lemurianCharge, lemurianFire;
        AudioSource attackAudio;
        internal AudioSource AttackAudio => attackAudio;
        AudioClip primaryAttackAudio, secondaryAttackAudio;
        int primaryAudioState, secondaryAudioState;
        readonly List<Renderer> hidden=new List<Renderer>();
        readonly List<Renderer> blinkHiddenRenderers = new List<Renderer>(4);
        internal bool ModelRenderingHidden { get; private set; }
        internal bool AttackAudioPlaying => attackAudio && attackAudio.isPlaying;
        internal AudioClip SelectedAttackSound => attackAudio ? attackAudio.clip : null;
        readonly List<Material> materials=new List<Material>();
        static GameObject[] prefabs;
        static AnimationClip[] clips;
        static Material[] sourceMaterials;
        internal Transform FindModelBone(string boneName) => model
            ? Array.Find(model.GetComponentsInChildren<Transform>(), bone => bone.name == boneName)
            : null;
        internal void SetModelVisible(bool visible)
        {
            if (model) model.SetActive(visible);
        }

        internal void SetModelRenderingVisible(bool visible)
        {
            if (!model || ModelRenderingHidden == !visible) return;
            ModelRenderingHidden = !visible;
            if (!visible)
            {
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    if (renderer.enabled)
                    {
                        blinkHiddenRenderers.Add(renderer);
                        renderer.enabled = false;
                    }
            }
            else
            {
                foreach (var renderer in blinkHiddenRenderers)
                    if (renderer) renderer.enabled = true;
                blinkHiddenRenderers.Clear();
            }
        }

        internal void SetVerminBitePaused(bool paused)
        {
            if (playing.IsValid() && current && (current == verminBite1 || current == verminBite2 || current == verminBite3))
                playing.SetSpeed(paused ? 0f : current.length);
        }

        internal Bounds GetModelBounds()
        {
            var bounds = new Bounds(transform.position + Vector3.up * .5f, Vector3.one);
            if (!model) return bounds;
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return bounds;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        internal const string GupResources = "RainActors/RoR2/DLC1/Gup/";

        public bool Setup(string name,string prefix,float height,string skin=null)
        {
            if(prefabs==null)prefabs=Resources.LoadAll<GameObject>("RainActors");
            if(clips==null)clips=Resources.LoadAll<AnimationClip>("RainActors");
            if(sourceMaterials==null)sourceMaterials=Resources.LoadAll<Material>("RainActors");
            var prefab=Array.Find(prefabs,p=>p.name==name);if(!prefab)return false;
            foreach(var renderer in GetComponentsInChildren<Renderer>())if(renderer.enabled){hidden.Add(renderer);renderer.enabled=false;}
            model=Instantiate(prefab,transform);model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;
            model.name = name;
            string variantFolder = name switch
            {
                "mdlSnowyGolem" => "SnowyGolem",
                "mdlNatureGolem" => "NatureGolem",
                "mdlGolem" when skin == RangeResearchEnemy.SandyGolemVariant => "SandyGolem",
                _ => null
            };
            var variantTexture = variantFolder == null ? null : Resources.Load<Texture2D>(
                $"RainActors/RoR2/Base/Characters/Golem/{variantFolder}/tex{variantFolder}Diffuse");
            const string titanFolder = "RainActors/RoR2/Base/Characters/Titan/";
            bool goldTitan = skin == RangeResearchEnemy.GoldTitanVariant;
            var goldMesh = goldTitan ? Resources.Load<Mesh>(titanFolder + RangeResearchEnemy.GoldTitanVariant) : null;
            if (goldTitan) variantTexture = Resources.Load<Texture2D>(titanFolder + "texTitanAO");
            bool gup = prefix == "GupArmature|";
            var gupColor = gup ? Resources.Load<Texture2D>(GupResources + "tex" + name.Substring(3) + "_Diffuse") : null;
            var gupNormal = gup ? Resources.Load<Texture2D>(GupResources + "texGup_Normal") : null;
            var gupEmission = gup ? Resources.Load<Texture2D>(GupResources + "texGup_Emission") : null;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer) continue;
                if (goldMesh && renderer is SkinnedMeshRenderer skinned && skinned.bones.Length == goldMesh.bindposeCount)
                    skinned.sharedMesh = goldMesh;
                var source=renderer.sharedMaterials;var mapped=new Material[source.Length];
                string materialName = variantFolder == null ? name.Replace("mdl","mat") : "mat" + variantFolder;
                var named = name == "mdlLunarExploder" ? null : Array.Find(sourceMaterials, m => m.name == materialName);
                for(int i=0;i<source.Length;i++)
                {
                    var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)shader=Shader.Find("Standard");
                    var original = named ? named : source[i];
                    var mat = (name is "mdlHermitCrab" or "mdlAcidLarva" or "mdlLunarExploder" or "mdlParent") && original
                        ? new Material(original) : new Material(shader);
                    if (original)
                    {
                        var texture = original.mainTexture;
                        if (!texture && original.HasProperty("_MainTex")) texture = original.GetTexture("_MainTex");
                        mat.mainTexture = texture;
                        if (original.HasProperty("_BaseColor")) mat.color = original.GetColor("_BaseColor");
                        else if (original.HasProperty("_Color")) mat.color = original.GetColor("_Color");
                    }
                    if (variantTexture)
                    {
                        mat.mainTexture = variantTexture;
                        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", variantTexture);
                    }
                    if (goldTitan)
                    {
                        mat.color = new Color(1f, .72f, .22f);
                        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", .65f);
                    }
                    if (gup && renderer is SkinnedMeshRenderer gupSkin && gupSkin.sharedMesh &&
                        !gupSkin.sharedMesh.name.Contains("Eyes"))
                    {
                        mat.mainTexture = gupColor;
                        mat.SetTexture("_BaseMap", gupColor);
                        mat.SetTexture("_BumpMap", gupNormal);
                        mat.SetTexture("_EmissionMap", gupEmission);
                        mat.SetColor("_EmissionColor", Color.white);
                        mat.EnableKeyword("_NORMALMAP");
                        mat.EnableKeyword("_EMISSION");
                    }
                    materials.Add(mat);mapped[i]=mat;
                }
                renderer.sharedMaterials=mapped;
            }
            var renderers=model.GetComponentsInChildren<Renderer>();
            float modelHeight = GetModelBounds().size.y;
            // The golem animation overwrites the variant prefab's imported 100x bone scale.
            if (name is "mdlSnowyGolem" or "mdlNatureGolem") modelHeight *= .01f;
            if (height > 0f && modelHeight > .01f) model.transform.localScale *= height / modelHeight;

            if (prefix is "PendingMini_" or "PendingBell_")
            {
                var pivot = new GameObject("Pending model pivot").transform;
                pivot.SetParent(transform, false);
                model.transform.SetParent(pivot, true);
                pivot.localPosition = Vector3.up * (transform.position.y - GetModelBounds().min.y);
            }
            if (goldTitan && renderers.Length > 0) AttachGoldSword(titanFolder, renderers[0].sharedMaterial);
            animator=model.GetComponentInChildren<Animator>();
            if(!animator)animator=model.AddComponent<Animator>();animator.applyRootMotion=false;
            if (gup)
            {
                // The bundle's animator and animation paths are relative to the nested model.
                var gupRoot = model.transform.Find("ModelBase/" + name);
                if (gupRoot && animator.transform != gupRoot)
                {
                    animator.enabled = false;
                    animator = gupRoot.gameObject.GetComponent<Animator>();
                    if (!animator) animator = gupRoot.gameObject.AddComponent<Animator>();
                    animator.applyRootMotion = false;
                }
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.fireEvents = false;
            }
            // Server hit queries read animated head bones even when the model is off camera.
            if ((prefix is "LemurianArm_" or "LemurianBruiserArmature_" or "LunarExploderArmature_" or "ParentArmature_") &&
                TryGetComponent<CharacterInstance>(out var body) && body.isServer)
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            idle=Find(prefix,"IdleLoop")??Find(prefix,"Idle");move=Find(prefix,"WalkForward")??Find(prefix,"RunForward")??idle;
            attack=Find(prefix,"Headbutt")??Find(prefix,"FireLaser")??Find(prefix,"FireFist")??Find(prefix,"Attack")??idle;
            if (prefix == "Child_")
            {
                idle = Find(prefix, "Idle_Single"); move = Find(prefix, "Run_F"); attack = Find(prefix, "Spark_Launch");
                animator.fireEvents = false;
                BindAttackAudio(2, "RainActors/RoR2/Base/Characters/BeetleGroup/BeetleGuard/OriginalSlam");
            }
            if (prefix == "PendingGrenadier_")
            {
                idle = Find(prefix, "Idle"); move = Find(prefix, "Walk"); attack = Find(prefix, "Attack");
                animator.fireEvents = false;
                BindAttackAudio(2, "RainActors/RoR2/Base/Characters/BeetleGroup/BeetleGuard/OriginalSlam");
            }
            if (prefix == "ClayBruiserArmature_")
            {
                idle = Find(prefix, "Idle__Additive_Loop") ?? Find(prefix, "Idle__Single"); move = Find(prefix, "WalkForward"); attack = Find(prefix, "FireGatling");
                animator.fireEvents = false;
                BindAttackAudio(2, "RainActors/RoR2/Base/Characters/BeetleGroup/BeetleGuard/OriginalSlam");
            }
            if (prefix is "PendingMini_" or "PendingBell_")
            {
                idle = prefix == "PendingMini_" ? Find(prefix, "Idle") : Find(prefix, "Idle__Additive") ?? Find(prefix, "Idle__Single") ?? Find(prefix, "Idle, Single");
                move = Find(prefix, prefix == "PendingMini_" ? "RunForward" : "GasForward");
                attack = Find(prefix, prefix == "PendingMini_" ? "SporeRelease" : "GasBackwards");
                animator.fireEvents = false;
                if (!idle || !move || !attack) Debug.LogError("PENDING_VISUAL_CLIP_MISSING " + prefix);
                else Debug.Log($"PENDING_VISUAL_CLIPS {prefix} idle={idle.name} move={move.name} attack={attack.name}");
                BindAttackAudio(2, "RainActors/RoR2/Base/Characters/BeetleGroup/BeetleGuard/OriginalSlam");
            }
            if (gup)
            {
                idle = Find(prefix, "IdleSingle");
                move = Find(prefix, "RunForward");
                attack = Find(prefix, "SpikeAttack");
                spawn = Find(prefix, "Spawn");
                death = Find(prefix, "Death");
                spawnDuration = 1f;
                BindAttackAudio(2, GupResources + "OriginalGupCharge");
            }
            if (prefix == "ParentArmature_")
            {
                idle = Find(prefix, "Idle, Loop");
                move = Find(prefix, "WalkForward");
                attack = Find(prefix, "Slam");
                spawn = Find(prefix, "Spawn");
                death = Find(prefix, "Death");
                spawnDuration = RangeResearchEnemy.ParentSpawnDuration;
                BindAttackAudio(RangeResearchEnemy.ParentSlamAnimation, "RainActors/RoR2/Base/Characters/BeetleGroup/BeetleGuard/OriginalSlam");
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            if (prefix == "LunarExploderArmature_")
            {
                idle = Find(prefix, "Idle, Additive Loop");
                move = Find(prefix, "RunForward");
                attack = Find(prefix, "FireExploderShards");
                spawn = Find(prefix, "Spawn");
                death = Find(prefix, "Death");
                spawnDuration = RangeResearchEnemy.LunarExploderSpawnDuration;
                BindAttackAudio(2, "RainImported/WispItemExplosion");
            }
            if (prefix == "AcidLarva_")
            {
                idle = Array.Find(clips, clip => clip.name == "AcidLarva_Idle");
                move = Find(prefix, "Sprint");
                spawn = Find(prefix, "SpawnFaster");
                death = Find(prefix, "Death");
                spawnDuration = RangeResearchEnemy.AcidLarvaSpawnDuration;
                acidLarvaLeapStart = Find(prefix, "JumpAttackStartup");
                acidLarvaLeapLoop = Find(prefix, "JumpAttackLoop");
                acidLarvaLanding = Find(prefix, "Landing");
                BindAttackAudio(RangeResearchEnemy.AcidLarvaLandingAnimation, "RainImported/WispItemExplosion");
            }
            if (prefix == "HermitCrabArmature_")
            {
                idle = Find(prefix, "Idle_ AdditiveLoop");
                spawn = Find(prefix, "Spawn");
                death = Find(prefix, "Death");
                spawnDuration = RangeResearchEnemy.HermitCrabSpawnDuration;
                hermitBurrowIn = Find(prefix, "BurrowIn");
                hermitBurrowed = Find(prefix, "Burrowed");
                hermitBurrowOut = Find(prefix, "BurrowOut");
                hermitFire = Find(prefix, "BurrowFire");
                BindAttackAudio(RangeResearchEnemy.HermitFireAnimation, "RainActors/RoR2/Base/Characters/HermitCrab/OriginalAttack");
            }
            if (prefix == "FlyingVerminArmature_")
            {
                idle = move = Find(prefix, "FlyLoop");
                spawn = Find(prefix, "Spawn");
                death = Array.Find(clips, clip => clip.name == "FlyingVerminArmature_Death");
                flyingVerminDeathLoop = Find(prefix, "DeathLoop");
                flyingVerminSpit = Find(prefix, "FireSpit");
                BindAttackAudio(RangeResearchEnemy.FlyingVerminSpitAnimation, "RainActors/RoR2/Base/Characters/BeetleGroup/BeetleQueen/OriginalAcidSpit");
                spawnDuration = RangeResearchEnemy.FlyingVerminSpawnDuration;
            }
            if (prefix == "VerminArmature_")
            {
                idle = Find(prefix, "Idle_ Single");
                move = Find(prefix, "RunForward");
                spawn = Array.Find(clips, clip => clip.name == "VerminArmature_Spawn");
                death = Array.Find(clips, clip => clip.name == "VerminArmature_Death");
                verminBite1 = Find(prefix, "Bite1");
                verminBite2 = Find(prefix, "Bite2");
                verminBite3 = Find(prefix, "Bite3");
                BindAttackAudio(RangeResearchEnemy.VerminFirstBiteAnimation, "RainActors/RoR2/Base/Characters/Lemurian/OriginalBite");
                spawnDuration = RangeResearchEnemy.VerminSpawnDuration;
            }
            if (prefix == "BisonArmature_")
            {
                idle = Find(prefix, "Idle_ Single");
                move = Find(prefix, "WalkForward");
                spawn = Find(prefix, "Spawn");
                death = Find(prefix, "DeathCharge");
                bisonHeadbutt = Find(prefix, "Headbutt");
                bisonChargePrep = Find(prefix, "PrepCharge");
                bisonCharge = Find(prefix, "SprintForward");
                bisonStun = Find(prefix, "Hurt1");
                BindAttackAudio(RangeResearchEnemy.BisonHeadbuttAnimation, "RainActors/RoR2/Base/Characters/BeetleGroup/BeetleGuard/OriginalSlam",
                    RangeResearchEnemy.BisonChargeAnimation, "RainActors/RoR2/Base/Characters/BeetleGroup/BeetleGuard/OriginalSlam");
                spawnDuration = RangeResearchEnemy.BisonSpawnDuration;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            if (prefix == "ImpArmature_")
            {
                idle = Find(prefix, "Idle_ Single");
                spawn = Find(prefix, "Spawn");
                death = Find(prefix, "Death");
                impSlash = Find(prefix, "DoubleSlash_ Override");
                impBlink = Find(prefix, "Blink_ Override");
                impBlinkExit = Find(prefix, "BlinkEnd_ Additive");
                BindAttackAudio(26, "Jotunnslayer/SpiderRegular/SpiderStrikePlaceholder");
                spawnDuration = RangeResearchEnemy.ImpSpawnDuration;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            if (prefix == "BeetleQueenArmature_")
            {
                idle = Find(prefix, "Idle_ Single");
                move = Find(prefix, "walkForward");
                spawn = Array.Find(clips, clip => clip.name == "BeetleQueenArmature_spawn");
                death = Find(prefix, "_Death");
                spawnDuration = RangeResearchEnemy.QueenSpawnDuration;
                queenCharge = Find(prefix, "_chargeSpit");
                queenSpit = Find(prefix, "_fireSpit");
                queenSummon = Find(prefix, "_warcry");
                queenWards = Find(prefix, "SpawnWards");
                BindAttackAudio(20, "RainActors/RoR2/Base/Characters/BeetleGroup/BeetleQueen/OriginalAcidSpit",
                    22, "RainActors/RoR2/Base/Characters/BeetleGroup/BeetleQueen/OriginalSummonEggs");
            }
            if (prefix == "LemurianBruiserArmature_")
            {
                idle = Find(prefix, "Idle_ Single");
                spawn = Find(prefix, "_Spawn");
                death = Find(prefix, "_Death");
                spawnDuration = RangeResearchEnemy.BruiserSpawnDuration;
                bruiserBreathPrep = Find(prefix, "_PrepFlamebreath");
                bruiserBreath = Find(prefix, "_Flamebreath");
                bruiserBreathExit = Find(prefix, "_ExitFlamebreath");
                bruiserCharge = Find(prefix, "_ChargeMegaFireball");
                bruiserFire = Find(prefix, "_FireMegaFireball");
                BindAttackAudio(16, "RainActors/RoR2/Base/Characters/LemurianBruiser/OriginalBreath",
                    19, "RainActors/RoR2/Base/Characters/LemurianBruiser/OriginalFireball");
            }
            if (prefix == "JellyfishArmature_")
            {
                idle = Find(prefix, "Idle_ Single");
                move = Find(prefix, "Swim");
                spawn = Find(prefix, "SpawnTwirl");
                spawnDuration = RangeResearchEnemy.JellyfishSpawnDuration;
                jellyfishNova = Find(prefix, "Detonate");
                BindAttackAudio(14, "RainImported/WispItemExplosion");
            }
            if (prefix == "GreaterWispArmature_")
            {
                idle = Find(prefix, "Idle");
                spawn = Find(prefix, "Spawn2");
                death = Find(prefix, "Death2");
                spawnDuration = RangeResearchEnemy.GreaterWispSpawnDuration;
                greaterWispCharge = Find(prefix, "ChargeAttack");
                greaterWispFire = Find(prefix, "FireAttack");
                BindAttackAudio(25, "RainActors/RoR2/Base/Characters/GreaterWisp/OriginalAttack");
            }
            if (prefix == "WispArmature_")
            {
                idle = Find(prefix, "idle_Walk");
                spawn = Find(prefix, "Spawn2");
                spawnDuration = 1f;
                wispCharge = Find(prefix, "chargeEmbers");
                wispFire = Find(prefix, "FireEmbers");
                BindAttackAudio(13, "RainActors/RoR2/Base/Characters/GreaterWisp/OriginalAttack");
            }
            if (prefix == "LemurianArm_")
            {
                idle = Find(prefix, "Idle_ Single");
                lemurianBite = Find(prefix, "_bite");
                lemurianCharge = Find(prefix, "ChargeFireball");
                lemurianFire = Find(prefix, "FireFireball");
                spawn = Find(prefix, "Spawn");
                death = Find(prefix, "Death2");
                spawnDuration = RangeResearchEnemy.LemurianSpawnDuration;
                BindAttackAudio(9, "RainActors/RoR2/Base/Characters/Lemurian/OriginalBite",
                    11, "RainActors/RoR2/Base/Characters/Lemurian/OriginalFireball");
            }
            if (prefix == "BeetleGuard")
            {
                idle = Find(prefix, "IdleSingle");
                guardSlam = Find(prefix, "GroundSmash");
                guardSunder = Find(prefix, "FireSunder");
                spawn = Find(prefix, "Spawn");
                death = Find(prefix, "Death2");
                spawnDuration = RangeResearchEnemy.GuardSpawnDuration;
                BindAttackAudio(5, "RainActors/RoR2/Base/Characters/BeetleGroup/BeetleGuard/OriginalSlam",
                    6, "RainActors/RoR2/Base/Characters/BeetleGroup/BeetleGuard/OriginalSunder");
            }
            if (prefix == "Titan")
            {
                attack = Find(prefix,"FireFist") ?? attack;
                beamLoop = Find(prefix,"FireLaserLoop");
                beamEnd = Find(prefix,"FireLaserEnd");
                BindAttackAudio(3, "RainActors/RoR2/Base/Characters/Titan/OriginalLaserShot");
            }
            if (prefix == "Golem")
                BindAttackAudio(2, "RainActors/RoR2/Base/Characters/Golem/OriginalLaserCharge");
            if (prefix == "Beetle")
                BindAttackAudio(2, "Jotunnslayer/SpiderRegular/SpiderStrikePlaceholder");
            graph=PlayableGraph.Create(name+" animations");graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            output=AnimationPlayableOutput.Create(graph,"Actor",animator);graph.Play();SetState(0);return true;
        }
        internal void EvaluateCurrentPose(float elapsed)
        {
            if (!graph.IsValid() || !playing.IsValid()) return;
            playing.SetTime(elapsed * playing.GetSpeed());
            graph.Evaluate(0f);
        }
        void AttachGoldSword(string folder, Material material)
        {
            var mesh = Resources.Load<Mesh>(folder + "TitanGoldSwordMesh");
            if (!mesh) return;
            var hand = Array.Find(model.GetComponentsInChildren<Transform>(), bone => bone.name == "hand.r");
            if (!hand) return;
            var sword = new GameObject("Titan gold sword", typeof(MeshFilter), typeof(MeshRenderer));
            sword.transform.SetParent(hand, false);
            // Local pose of the sword in the exported titan gold props prefab.
            sword.transform.SetLocalPositionAndRotation(
                new Vector3(-.38950253f, 3.1437573f, .58857846f),
                new Quaternion(.000011591f, .67559063f, -.0000125915f, .737277f));
            sword.GetComponent<MeshFilter>().sharedMesh = mesh;
            sword.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        static AnimationClip Find(string prefix,string token)=>Array.Find(clips,c=>c.name.IndexOf(prefix,StringComparison.OrdinalIgnoreCase)>=0&&c.name.IndexOf(token,StringComparison.OrdinalIgnoreCase)>=0);

        void BindAttackAudio(int primaryState, string primaryPath, int secondaryState = -1, string secondaryPath = null)
        {
            primaryAudioState = primaryState;
            secondaryAudioState = secondaryState;
            primaryAttackAudio = Resources.Load<AudioClip>(primaryPath);
            secondaryAttackAudio = secondaryPath == null ? null : Resources.Load<AudioClip>(secondaryPath);
            if (!primaryAttackAudio && !secondaryAttackAudio) return;
            attackAudio = model.AddComponent<AudioSource>();
            attackAudio.playOnAwake = false;
            attackAudio.spatialBlend = 1f;
            attackAudio.maxDistance = 35f;
        }

        public void SetState(int state, double elapsed = 0d, float speed = 1f, bool restart = false)
        {
            var clip = state switch
            {
                1 => move,
                2 => attack,
                RangeResearchEnemy.ParentSlamAnimation => attack,
                3 => beamLoop ?? attack,
                4 => beamEnd ?? idle,
                5 => guardSlam,
                6 => guardSunder,
                7 => spawn,
                8 => death,
                9 => lemurianBite,
                10 => lemurianCharge,
                11 => lemurianFire,
                12 => wispCharge,
                13 => wispFire,
                14 => jellyfishNova,
                15 => bruiserBreathPrep,
                16 => bruiserBreath,
                17 => bruiserBreathExit,
                18 => bruiserCharge,
                19 => bruiserFire,
                20 => queenCharge,
                21 => queenSpit,
                22 => queenSummon,
                23 => queenWards,
                24 => greaterWispCharge,
                25 => greaterWispFire,
                26 => impSlash,
                RangeResearchEnemy.ImpBlinkAnimation => impBlink,
                RangeResearchEnemy.ImpBlinkExitAnimation => impBlinkExit,
                RangeResearchEnemy.BisonHeadbuttAnimation => bisonHeadbutt,
                RangeResearchEnemy.BisonChargePrepAnimation => bisonChargePrep,
                RangeResearchEnemy.BisonChargeAnimation => bisonCharge,
                RangeResearchEnemy.BisonStunAnimation => bisonStun,
                RangeResearchEnemy.VerminFirstBiteAnimation => verminBite1,
                RangeResearchEnemy.VerminFirstBiteAnimation + 1 => verminBite2,
                RangeResearchEnemy.VerminFirstBiteAnimation + 2 => verminBite3,
                RangeResearchEnemy.FlyingVerminSpitAnimation => flyingVerminSpit,
                RangeResearchEnemy.FlyingVerminDeathLoopAnimation => flyingVerminDeathLoop,
                RangeResearchEnemy.HermitBurrowInAnimation => hermitBurrowIn,
                RangeResearchEnemy.HermitBurrowedAnimation => hermitBurrowed,
                RangeResearchEnemy.HermitBurrowOutAnimation => hermitBurrowOut,
                RangeResearchEnemy.HermitFireAnimation => hermitFire,
                RangeResearchEnemy.AcidLarvaLeapStartAnimation => acidLarvaLeapStart,
                RangeResearchEnemy.AcidLarvaLeapLoopAnimation => acidLarvaLeapLoop,
                RangeResearchEnemy.AcidLarvaLandingAnimation => acidLarvaLanding,
                _ => idle
            };
            if(!clip || (clip==current && !restart))return;
            if (attackAudio && elapsed < .25d)
            {
                bool primaryAttack = state == primaryAudioState ||
                    primaryAudioState == RangeResearchEnemy.VerminFirstBiteAnimation && RangeResearchEnemy.IsVerminBiteAnimation(state);
                var attackSound = primaryAttack ? primaryAttackAudio :
                    state == secondaryAudioState ? secondaryAttackAudio : null;
                if (attackSound)
                {
                    attackAudio.clip = attackSound;
                    attackAudio.volume = .7f;
                    attackAudio.Play();
                }
            }
            if (clayLayer.IsValid()) graph.DestroyPlayable(clayLayer);
            if (clayBase.IsValid()) graph.DestroyPlayable(clayBase);
            current=clip;if(playing.IsValid())graph.DestroyPlayable(playing);playing=AnimationClipPlayable.Create(graph,clip);output.SetSourcePlayable(playing);            if (model.name == "mdlClayBruiser" && state == 2 && idle)
            {
                clayBase = AnimationClipPlayable.Create(graph, idle);
                clayLayer = AnimationLayerMixerPlayable.Create(graph, 2);
                graph.Connect(clayBase, 0, clayLayer, 0); clayLayer.SetInputWeight(0, 1f);
                graph.Connect(playing, 0, clayLayer, 1); clayLayer.SetInputWeight(1, 1f);
                clayLayer.SetLayerAdditive(1, true); output.SetSourcePlayable(clayLayer);
            }
            if (state == RangeResearchEnemy.ParentSlamAnimation) playing.SetSpeed(clip.length / RangeResearchEnemy.ParentSlamDuration);
            if (state == 5) playing.SetSpeed(clip.length / RangeResearchEnemy.GuardSlamDuration);
            if (state == 6) playing.SetSpeed(clip.length / RangeResearchEnemy.GuardSunderDuration);
            if (state == 7) playing.SetSpeed(clip.length / spawnDuration);
            if (state == 2 && model.name is "mdlGup" or "mdlGeep" or "mdlGip")
                playing.SetSpeed(clip.length / RangeResearchEnemy.GupAttackDuration);
            if (state == 8 && model.name is "mdlGup" or "mdlGeep" or "mdlGip")
                playing.SetSpeed(clip.length / .5f);
            if (state == 8 && model.name == "mdlLunarExploder")
                playing.SetSpeed(clip.length / RangeResearchEnemy.LunarExploderDeathDuration);
            if (state == 9) playing.SetSpeed(clip.length);
            if (state == 10) playing.SetSpeed(clip.length / RangeResearchEnemy.LemurianChargeDuration);
            if (state == 11) playing.SetSpeed(clip.length / RangeResearchEnemy.LemurianFireDuration);
            if (state == 12) playing.SetSpeed(clip.length / RangeResearchEnemy.WispChargeDuration);
            if (state == 13) playing.SetSpeed(clip.length / RangeResearchEnemy.WispFireDuration);
            if (state == 14) playing.SetSpeed(clip.length);
            if (state == 15) playing.SetSpeed(clip.length / RangeResearchEnemy.BruiserBreathEntry);
            if (state == 16) playing.SetSpeed(clip.length / RangeResearchEnemy.BruiserBreathDuration);
            if (state == 17) playing.SetSpeed(clip.length);
            if (state == 18) playing.SetSpeed(clip.length / RangeResearchEnemy.BruiserFireCharge);
            if (state == 19) playing.SetSpeed(clip.length / RangeResearchEnemy.BruiserFireDuration);
            if (state == 20 || state == 21) playing.SetSpeed(clip.length / RangeResearchEnemy.QueenSpitDuration);
            if (state == 23) playing.SetSpeed(speed * clip.length / RangeResearchEnemy.QueenWardCastDuration);
            if (state == 24 || state == 25)
                playing.SetSpeed(clip.length / RangeResearchEnemy.GreaterWispCannonDuration);
            if (state == 26) playing.SetSpeed(clip.length / RangeResearchEnemy.ImpSlashDuration);
            if (state == RangeResearchEnemy.ImpBlinkAnimation)
                playing.SetSpeed(clip.length / RangeResearchEnemy.ImpBlinkDuration);
            if (state == RangeResearchEnemy.BisonHeadbuttAnimation)
                playing.SetSpeed(clip.length / RangeResearchEnemy.BisonHeadbuttDuration);
            if (state == RangeResearchEnemy.BisonChargePrepAnimation)
                playing.SetSpeed(clip.length / RangeResearchEnemy.BisonChargePrepDuration);
            if (RangeResearchEnemy.IsVerminBiteAnimation(state))
                playing.SetSpeed(clip.length);
            if (state == RangeResearchEnemy.FlyingVerminSpitAnimation)
                playing.SetSpeed(clip.length / RangeResearchEnemy.FlyingVerminSpitDuration);
            if (state == RangeResearchEnemy.HermitBurrowInAnimation)
                playing.SetSpeed(clip.length / RangeResearchEnemy.HermitBurrowInDuration);
            if (state == RangeResearchEnemy.HermitBurrowOutAnimation)
                playing.SetSpeed(clip.length / RangeResearchEnemy.HermitBurrowOutDuration);
            if (elapsed > 0d) playing.SetTime(System.Math.Min(clip.length, elapsed * playing.GetSpeed()));
        }
        void OnDestroy(){if(graph.IsValid())graph.Destroy();if(model)Destroy(model);foreach(var r in hidden)if(r)r.enabled=true;foreach(var m in materials)if(m)Destroy(m);}
    }
}
