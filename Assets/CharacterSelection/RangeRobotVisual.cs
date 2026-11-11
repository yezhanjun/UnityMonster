using UnityEngine;
using AvatarPortability;
using AvatarPortability.Wardrobe;

namespace MTPSKIT.Gameplay
{
    // Keeps TPS collision, equipment and movement authoritative while retargeting the body.
    [DefaultExecutionOrder(5000)]
    public sealed class RangeRobotVisual : MonoBehaviour
    {
        public static readonly string[] SuperBotNames = { "Etasphera05", "Etasphera07", "Etasphera14", "Etasphera17", "Etasphera22", "Etasphera31", "Etasphera46", "Etasphera53", "Etasphera72", "Etasphera85" };
        int selection = 1;
        bool initialized;
        public int Selection => selection;
        Animator source;
        Animator target;
        HeroAnimationGraph animationGraph;
        GameObject model;
        SkinnedMeshRenderer[] original;
        HumanPoseHandler reader, writer;
        HumanPose pose;
        Transform sourceLeftFoot, sourceRightFoot, targetLeftFoot, targetRightFoot;
        PortableBonePhysics[] bonePhysics;
        WardrobePoseFollower[] wardrobeFollowers;
        CharacterInstance character;
        Vector3 lastPosition;
        bool resetSimulation;
        int preparedFrame = -1;
        Vector3 heroModelOffset;
        internal bool UsesHeroBase => animationGraph != null && animationGraph.BaseActive;
        public Animator DisplayAnimator => target;
        internal WendyRiflePresentation RiflePresentation { get; private set; }
        internal FourWayRollPresentation RollPresentation { get; private set; }
        internal FourWayJumpPresentation JumpPresentation { get; private set; }
        internal void RifleShot(Item item) => RiflePresentation?.Shot(item);
        internal void ReleaseRifle(Item item) => RiflePresentation?.Release(item);

        // The display is driven through HumanPoseHandler, not the source Animator's hierarchy.
        // Rebinding a controller while it is nested can resolve duplicate display/pet bone names
        // against the source Avatar and invalidate Animation Rigging stream handles.
        internal void SetSourceController(Animator controllerSource, RuntimeAnimatorController controller)
        {
            var display = model ? model.transform : null;
            if (!display || !display.IsChildOf(controllerSource.transform))
            {
                controllerSource.runtimeAnimatorController = controller;
                return;
            }

            var parent = display.parent;
            var position = display.localPosition;
            var rotation = display.localRotation;
            var scale = display.localScale;
            int sibling = display.GetSiblingIndex();
            display.SetParent(controllerSource.transform.parent, true);
            try
            {
                controllerSource.runtimeAnimatorController = controller;
            }
            finally
            {
                if (display)
                {
                    display.SetParent(parent, false);
                    display.SetLocalPositionAndRotation(position, rotation);
                    display.localScale = scale;
                    display.SetSiblingIndex(sibling);
                }
            }
        }

        public static void Apply(CharacterInstance character, int selection)
        {
            if (!character) return;
            var visual = character.GetComponent<RangeRobotVisual>();
            if (selection < 1 || (selection > SuperBotNames.Length + 1 && !RangeCharacterSelection.IsPortable(selection)))
            {
                if (visual) { visual.ReleaseDisplay(); visual.enabled = false; Destroy(visual); }
                return;
            }
            if (!visual) visual = character.gameObject.AddComponent<RangeRobotVisual>();
            if (visual.selection == selection) return;
            visual.selection = selection;
            if (visual.initialized) { visual.ReleaseDisplay(); visual.CreateDisplay(); }
        }
        void Start()
        {
            character = GetComponent<CharacterInstance>();
            source = GetComponentInChildren<Animator>();
            if (!source || !source.avatar || !source.avatar.isHuman)
            { Debug.LogError("Range robot requires a humanoid source and RangeRobot prefab.", this); Destroy(this); return; }
            original = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            // Equipment can already be parented below the source skeleton at Start.
            // Its visibility and lifetime belong to Item, not the character display.
            for (int i = 0; i < original.Length; i++)
                if (original[i].GetComponentInParent<Item>()) original[i] = null;
            initialized = true;
            character.Client_Resurrect += OnResurrect;
            CreateDisplay();
        }
        void CreateDisplay()
        {
            string key = RangeCharacterSelection.IsPortable(selection) || selection == 1
                ? RangeCharacterSelection.ResourcePath(selection)
                : "SuperBotPackVol1/" + SuperBotNames[selection - 2];
            var prefab = Resources.Load<GameObject>(key);
            if (!prefab) { Debug.LogError("Missing humanoid display prefab: " + key, this); Destroy(this); return; }
            // Separate Animator roots: source RigBuilder must never bind the portable
            // skeleton or its hand-mounted equipment when weapon controllers change.
            model = Instantiate(prefab, source.transform.parent);
            model.transform.localPosition = source.transform.localPosition;
            model.transform.localRotation = source.transform.localRotation;
            model.transform.localScale = Vector3.Scale(model.transform.localScale, source.transform.localScale);
            heroModelOffset = transform.InverseTransformPoint(model.transform.position);
            target = model.GetComponent<Animator>();
            if (!target || !target.avatar || !target.avatar.isHuman) { Destroy(this); return; }
            target.enabled = false;
            target.applyRootMotion = false;
            if (RangeCharacterSelection.IsPortable(selection)) animationGraph = new HeroAnimationGraph(target);
            if (animationGraph != null) RiflePresentation = new WendyRiflePresentation(character, target, animationGraph);
            if (RangeCharacterSelection.IsPortable(selection)) RollPresentation = new FourWayRollPresentation(character, target, animationGraph);
            if (RangeCharacterSelection.IsPortable(selection)) JumpPresentation = new FourWayJumpPresentation(character, target, animationGraph);
            bonePhysics = model.GetComponentsInChildren<PortableBonePhysics>(true);
            wardrobeFollowers = model.GetComponentsInChildren<WardrobePoseFollower>(true);
            lastPosition = transform.position;
            resetSimulation = true; preparedFrame = -1;
            reader = new HumanPoseHandler(source.avatar, source.transform);
            writer = new HumanPoseHandler(target.avatar, target.transform);
            sourceLeftFoot = source.GetBoneTransform(HumanBodyBones.LeftFoot);
            sourceRightFoot = source.GetBoneTransform(HumanBodyBones.RightFoot);
            targetLeftFoot = target.GetBoneTransform(HumanBodyBones.LeftFoot);
            targetRightFoot = target.GetBoneTransform(HumanBodyBones.RightFoot);
            pose.muscles = new float[HumanTrait.MuscleCount];
            foreach (var renderer in original) if (renderer) renderer.forceRenderingOff = true;
            var stamps = GetComponent<CombatScratchReceiver>();
            if (stamps && stamps.isActiveAndEnabled) stamps.RefreshSurfaces();
        }
        // The motor samples the hero graph before moving. LateUpdate only finishes the
        // display, so visible pose and collision displacement share one evaluation.
        internal bool SampleHeroMotion(out Vector3 displacement)
        {
            displacement = Vector3.zero;
            PrepareHeroPose();
            if (!UsesHeroBase || !character.IsGrounded || character.IsCrouching) return false;
            displacement = animationGraph.RootDisplacement;
            return true;
        }
        void PrepareHeroPose()
        {
            if (preparedFrame == Time.frameCount || !target || !model || !source) return;
            preparedFrame = Time.frameCount;
            foreach (var physics in bonePhysics)
                if (physics && physics.isActiveAndEnabled) physics.RestoreAnimationPose();
            RiflePresentation?.Tick(Time.deltaTime);
            JumpPresentation?.Tick();
            RollPresentation?.Tick();
            bool ownedPose = UsesHeroBase || (JumpPresentation?.Applied ?? false) || (RollPresentation?.Applied ?? false);
            if (ownedPose)
                model.transform.position = transform.TransformPoint(heroModelOffset);
            else
                model.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            // TPS is a fallback only. Never inject its pose/foot height into the hero graph.
            if (!UsesHeroBase && reader != null && writer != null)
            { reader.GetHumanPose(ref pose); writer.SetHumanPose(ref pose); }
            var motor = character.GetComponent<CharacterMotor>();
            float facing = motor && motor.IsFourWayRolling ? motor.RollDisplayFacing :
                motor && motor.LocksAirFacing ? motor.AirFacing : character.lookInput.y;
            animationGraph?.Evaluate(facing);
        }
        void LateUpdate()
        {
            if (reader == null || writer == null || !source || !model || !model.activeInHierarchy) return;
            PrepareHeroPose();
            if (UsesHeroBase || (JumpPresentation?.Applied ?? false) || (RollPresentation?.Applied ?? false))
                model.transform.position = transform.TransformPoint(heroModelOffset);
            if (!(RollPresentation?.Applied ?? false) && !(JumpPresentation?.Applied ?? false)) RiflePresentation?.ApplyAimPitch();
            if (!UsesHeroBase && !(RollPresentation?.Applied ?? false) && !(JumpPresentation?.Applied ?? false) && selection >= 2 && sourceLeftFoot && sourceRightFoot && targetLeftFoot && targetRightFoot)
            {
                float sourceGround = Mathf.Min(sourceLeftFoot.position.y, sourceRightFoot.position.y);
                float targetGround = Mathf.Min(targetLeftFoot.position.y, targetRightFoot.position.y);
                model.transform.position += Vector3.up * (sourceGround - targetGround);
            }
            RiflePresentation?.ApplyRunFootPlant();
            foreach (var follower in wardrobeFollowers)
                if (follower && follower.isActiveAndEnabled) follower.Synchronize();
            if (resetSimulation || (transform.position - lastPosition).sqrMagnitude > 1f)
            {
                foreach (var physics in bonePhysics)
                    if (physics && physics.isActiveAndEnabled) physics.ResetSimulation();
                resetSimulation = false;
            }
            lastPosition = transform.position;
        }
        void OnEnable() => resetSimulation = true;
        void OnResurrect(int health) => resetSimulation = true;
        void ReleaseDisplay(bool refreshSurfaces = true)
        {
            RollPresentation?.Release(); RollPresentation = null;
            JumpPresentation?.Release(); JumpPresentation = null;
            RiflePresentation?.Release(); RiflePresentation = null;
            animationGraph?.Dispose(); animationGraph = null;
            reader?.Dispose(); writer?.Dispose(); reader = writer = null; target = null;
            if (model) { model.SetActive(false); Destroy(model); model = null; }
            if (original != null) foreach (var renderer in original) if (renderer) renderer.forceRenderingOff = false;
            var stamps = GetComponent<CombatScratchReceiver>();
            if (refreshSurfaces && stamps && stamps.isActiveAndEnabled && gameObject.activeInHierarchy) stamps.RefreshSurfaces();
        }
        void OnDestroy()
        {
            if (character) character.Client_Resurrect -= OnResurrect;
            ReleaseDisplay(false);
        }
    }
}
