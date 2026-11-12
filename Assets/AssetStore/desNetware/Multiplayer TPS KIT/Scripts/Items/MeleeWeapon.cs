using MTPSKIT.Gameplay;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace MTPSKIT.Gameplay
{
    public class MeleeWeapon : Item
    {
        [Tooltip("Weapon model rotation relative to the character model in its authored level hold pose.")]
        [SerializeField] Vector3 neutralHoldEuler;
        [SerializeField] bool useGripOverride;
        [SerializeField] Vector3 gripEuler;
        [SerializeField] bool useAttackRootMotion;
        public bool UsesAttackRootMotion => useAttackRootMotion && ComboActive;
        [SerializeField] bool fullBodyLocomotion;
        public bool FullBodyLocomotion => fullBodyLocomotion;
        public Quaternion NeutralHoldRotation => Quaternion.Euler(neutralHoldEuler);

        [System.Serializable]
        public struct ComboPhase
        {
            public AnimationClip clip;
            public float preInputTime, jumpTime;
        }
        [SerializeField] ComboPhase[] combo;
        [Tooltip("All combo phases share this hit time in source-clip seconds.")]
        [SerializeField, Min(0f)] float comboHitTime = 0.16f;
        [SerializeField, Min(0.01f)] float attackPlaybackSpeed = 1f;
        [SerializeField] MeleeFeedbackProfile hitFeedback;
        [Tooltip("Height of the visual contact probe; overlap damage range is unchanged.")]
        [SerializeField, Min(0f)] float contactHeight = 1.35f;
        float phaseElapsed;
        float PhaseElapsed => phaseElapsed;
        int phase = -1;
        bool queued, hitDone, inputSent;
        readonly Collider[] hitBuffer = new Collider[64];
        readonly HashSet<Health> victims = new HashSet<Health>();
        [SerializeField] AnimationClip turn90Left, turn90Right, turn180Left, turn180Right;
        public float TurnDuration(bool halfTurn, bool left)
        {
            var clip = halfTurn ? (left ? turn180Left : turn180Right) : (left ? turn90Left : turn90Right);
            return clip ? clip.length : 0.6f;
        }
        public bool ComboActive => phase >= 0;
        public int ComboIndex => phase;
        public float ComboNormalizedTime => phase >= 0 ? PhaseElapsed / combo[phase].clip.length : 0f;

        bool CanAttack => _myOwner && _myOwner.CurrentHealth > 0 && currentlyInUse && !OwnerBlockedByRaidControl &&
            _myOwner.CharacterItemManager.CurrentlyUsedItem == this;
        protected override float PrimaryCooldown => coolDown / OwnerAttackSpeed;
        float phasePlaybackSpeed;

        public override void PushLeftTrigger()
        {
            if (combo == null || combo.Length == 0) { base.PushLeftTrigger(); return; }
            if (!CanAttack || _myOwner.IsRunning || inputSent || Time.time < coolDownTimer) return;
            if (phase >= 0 && PhaseElapsed < combo[phase].preInputTime) return;
            inputSent = true;
            if (isServer) ReceiveComboInput(); else CmdComboInput();
        }

        [Command]
        void CmdComboInput() => ReceiveComboInput();

        [Server]
        void ReceiveComboInput()
        {
            if (!CanAttack || _myOwner.IsRunning) return;
            if (phase < 0) { BeginPhase(0); return; }
            if (PhaseElapsed >= combo[phase].preInputTime)
                queued = true;
        }

        [Server]
        void BeginPhase(int index)
        {
            ApplyPhase(index);
            RpcComboPhase(index);
        }

        [ClientRpc]
        void RpcComboPhase(int index)
        {
            if (!isServer && CanAttack) ApplyPhase(index);
        }

        void ApplyPhase(int index)
        {
            phase = index;
            phaseElapsed = 0f;
            queued = hitDone = inputSent = false;
            victims.Clear();
            phasePlaybackSpeed = Mathf.Max(0.01f, attackPlaybackSpeed) * OwnerAttackSpeed;
            if (index >= 0 && _myOwner)
                _myOwner.CharacterAnimator.PlayMeleeCombo(index, phasePlaybackSpeed);
        }

        protected override void Update()
        {
            base.Update();
            if (phase < 0) return;
            if (!CanAttack) { StopCombo(); return; }
            phaseElapsed += _myOwner.CharacterAnimator.CombatDeltaTime(Time.deltaTime) * phasePlaybackSpeed;
            if (!isServer) return;
            float elapsed = PhaseElapsed;
            var current = combo[phase];
            if (!hitDone && elapsed >= comboHitTime)
            {
                hitDone = true;
                float reach = Jotunnslayer.SkillBattlePreview.ScaleRaidWeaponRange(_myOwner, 1f);
                Vector3 forward = _myOwner.transform.forward * reach;
                Vector3 hitCenter = _myOwner.transform.position + _myOwner.transform.up + forward;
                int count = Physics.OverlapSphereNonAlloc(hitCenter, reach, hitBuffer, GameManager.characterLayer);
                // Preserve all contacts if a dense crowd exceeds the reusable buffer.
                Collider[] hits = count == hitBuffer.Length
                    ? Physics.OverlapSphere(hitCenter, reach, GameManager.characterLayer) : hitBuffer;
                if (hits != hitBuffer) count = hits.Length;
                bool feedbackSent = false;
                for (int i = 0; i < count; i++)
                {
                    if (hits[i].transform.IsChildOf(_myOwner.transform)) continue;
                    var victim = hits[i].GetComponentInParent<Health>();
                    if (!victim || !victims.Add(victim)) continue;
                    Vector3 contactPoint = hits[i].ClosestPoint(_myOwner.transform.position + _myOwner.transform.up * contactHeight + forward);
                    int healthBefore = victim.CurrentHealth;
                    ServerDamage(victim, (byte)CharacterPart.body);
                    if (victim.CurrentHealth >= healthBefore) continue;
                    SendSwordStamp(victim, contactPoint);
                    if (!feedbackSent)
                    {
                        feedbackSent = true;
                        ApplyMeleeFeedback(victim);
                        RpcMeleeFeedback(victim);
                    }
                }
            }
            if (queued && elapsed >= current.jumpTime)
                BeginPhase((phase + 1) % combo.Length);
            else if (elapsed >= current.clip.length)
            {
                ApplyPhase(-1);
                RpcComboPhase(-1);
            }
        }

        void SendSwordStamp(Health victim, Vector3 point)
        {
            var stamp = hitFeedback ? hitFeedback.swordStamp : null;
            if (stamp == null || !stamp.enabled || !stamp.material) return;
            Vector3 slash = _myOwner.transform.TransformDirection(stamp.localSlashDirection);
            Vector3 approach = _myOwner.transform.forward;
            if (isClient) SpawnSwordStamp(victim, point, slash, approach);
            RpcSwordStamp(victim, point, slash, approach);
        }

        // Match health/death RPC ordering so fatal stamps see the spawned ragdoll.
        [ClientRpc]
        void RpcSwordStamp(Health victim, Vector3 point, Vector3 slash, Vector3 approach)
        {
            if (!isServer) SpawnSwordStamp(victim, point, slash, approach);
        }

        void SpawnSwordStamp(Health victim, Vector3 point, Vector3 slash, Vector3 approach)
        {
            if (!victim || !hitFeedback || hitFeedback.swordStamp == null) return;
            Component target = victim;
            if (victim.CurrentHealth <= 0)
            {
                var ragdoll = victim.GetComponentInChildren<RagDoll>();
                if (ragdoll) target = ragdoll;
            }
            bool stamped = CombatScratchReceiver.TrySpawn(target, point, slash, hitFeedback.swordStamp, approach, out Vector3 surfacePoint);
            if (hitFeedback.swordStamp.contactSparks)
                CombatStampSparkPool.Play(stamped ? surfacePoint : point, -approach, hitFeedback.swordStamp.glowColor);
        }

        [ClientRpc]
        void RpcMeleeFeedback(Health victim)
        {
            if (!isServer) ApplyMeleeFeedback(victim);
        }

        void ApplyMeleeFeedback(Health victim)
        {
            if (!hitFeedback || !_myOwner || _myOwner.CurrentHealth <= 0) return;
            _myOwner.CharacterAnimator.PlayHitStop(hitFeedback.attackerRate, hitFeedback.attackerDuration);
            if (victim is CharacterInstance character && character.CurrentHealth > 0)
                character.CharacterAnimator.PlayHitStop(hitFeedback.victimRate, hitFeedback.victimDuration);
            if (_myOwner.IsObserved && MeleeCameraFeedback.Instance)
                MeleeCameraFeedback.Instance.Play(hitFeedback);
        }

        void StopCombo()
        {
            if (_myOwner)
            {
                _myOwner.CharacterAnimator.CancelHitStop();
                if (_myOwner.IsObserved && MeleeCameraFeedback.Instance) MeleeCameraFeedback.Instance.Clear();
            }
            phaseElapsed = 0f;
            phase = -1;
            queued = hitDone = inputSent = false;
            victims.Clear();
        }
        public override void AssignToCharacter(CharacterInstance owner)
        {
            base.AssignToCharacter(owner);
            if (useGripOverride) ItemModel.transform.localRotation = Quaternion.Euler(gripEuler);
        }

        public override void Take() { StopCombo(); base.Take(); }
        public override void PutDown() { StopCombo(); base.PutDown(); }
        public override void Drop() { StopCombo(); base.Drop(); }

        protected override void Use()
        {
            base.Use();

            float reach = Jotunnslayer.SkillBattlePreview.ScaleRaidWeaponRange(_myOwner, 1f);
            Vector3 hitCenter = _myOwner.transform.position + _myOwner.transform.up + _myOwner.transform.forward * reach;
            Collider[] collider = Physics.OverlapSphere(hitCenter, reach, GameManager.characterLayer);

            for (int i = 0; i < collider.Length; i++)
            {
                Collider col = collider[i];
                if (col.transform.root != _myOwner.transform.root)
                {
                    Health victim = col.GetComponent<Health>();
                    if (victim)
                    {
                        if (isServer)
                            ServerDamage(victim, (byte)CharacterPart.body);
                        else
                            CmdDamage(victim, (byte)CharacterPart.body);
                    }
                }
            }
        }
        protected override void SingleUse()
        {
            base.SingleUse();
            _myOwner.CharacterAnimator.Play("Attack1");
        }
    }
}
