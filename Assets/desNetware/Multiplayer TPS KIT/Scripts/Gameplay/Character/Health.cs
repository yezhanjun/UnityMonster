using UnityEngine;
using Mirror;

using MTPSKIT.UI.HUD;
using MTPSKIT;

namespace MTPSKIT.Gameplay
{
    [System.Flags]
    internal enum RangeProcSource : ushort { None = 0, Missile = 1, Lightning = 2, Ring = 4, Dagger = 8, Razorwire = 16, Tesla = 32, Urn = 64, FrostRelic = 128, Firework = 256, MoltenPerforator = 512, HikingBoots = 1024 }

    
    public class Health : NetworkBehaviour
    {
        
        [Header("Health")]
        /// <summary>
        /// character name, used for example for killfeed
        /// </summary>
        [SyncVar] public string CharacterName = "DEFAULT";
        [SyncVar] public int CurrentHealth = 100;
        [SyncVar] public int MaxHealth = 100;

        [SyncVar] public int Team = -1;

        [Header("Element 1 -Body, 2 -Head, another ones can be custom")]
        [SerializeField] float[] damageMultipliers = { 1f, 2, 0.5f };


        //server callbacks
        public delegate void SHealthDepleted(CharacterPart characterPart, AttackType attackType, Health killer, int attackForce);
        public SHealthDepleted Server_OnHealthDepleted { set; get; } //when this object is killed on server

        public KilledCharacter Server_KilledCharacter;

        #region assists
        public delegate void KilledCharacter(Health victimID);
        public KilledCharacter Server_OnEarnedAssist;
        #endregion

        public delegate void SOnDamaged(int currentHealth, CharacterPart damagedPart, AttackType attackType, Health attacker, int attackForce);
        public SOnDamaged Server_OnDamaged { set; get; } //when this object is killed on server



        //client callbacks
        public delegate void ClientHealthStateChanged(int currentHealth, CharacterPart damagedPart, AttackType attackType, Health attackerID);
        public ClientHealthStateChanged Client_OnHealthStateChanged { set; get; }



        public delegate void ClientHealthAdded(int currentHealth, int addedHealth, uint healerID);
        public ClientHealthAdded Client_OnHealthAdded { set; get; }



        public delegate void HealthDepleted(CharacterPart damagedPart, Health attacker);
        public HealthDepleted Client_OnHealthDepleted { set; get; } //when this object is killed on client



        public delegate void KillConfirmation(CharacterPart damagedPart, Health victim);
        public HealthDepleted Client_KillConfirmation { set; get; }
        public KilledCharacter Client_OnEarnedAssist { set; get; } 


        public delegate void ClientDamageDealt(int currentHealth, int takenDamage, CharacterPart damagedPart, AttackType attackType, Health victim);
        public ClientDamageDealt Client_OnDamageDealt { set; get; }


        public delegate void ResurrectEvent(int health);
        public ResurrectEvent Server_Resurrect { set; get; }
        public ResurrectEvent Client_Resurrect { set; get; }


        public System.Func<Health, bool> ServerDamageAllowed;
        protected virtual float BaseArmor => 0f;
        internal bool RangeInvulnerable { get; set; }
        bool _dead = false;
        bool _clientDead = false;

        public Vector3 centerPosition = new Vector3(0, 1.5f, 0);

        #region register/deregister health object

        Transform _targetPositionTracker;

        public DamageMemory[] _damageMemories;
        int _lastDamageMemory = 0;

        uint _netID;
        protected virtual void Awake() 
        {
            _damageMemories = new DamageMemory[6];
            _targetPositionTracker = new GameObject("TargetTracker").transform;
            _targetPositionTracker.transform.SetParent(transform);
            _targetPositionTracker.transform.localPosition = centerPosition;
            _targetPositionTracker.transform.localRotation = Quaternion.identity;
        }

        protected virtual void Start()
        {
            _netID = netId;

            _damageMemories = new DamageMemory[6];

            gameObject.layer = (int)GameLayers.character;

            CustomSceneManager.RegisterCharacter(this); //we have to register spawned characters in order to let bot "see" them, and select nearest enemies from that register
            GameManager.AddHealthInstance(this);
        }
        protected virtual void OnDestroy()
        {
            CustomSceneManager.DeRegisterCharacter(this);
            GameManager.RemoveHealthInstance(_netID);
        }
        #endregion


        public void Server_ChangeHealthState(int damage, CharacterPart damagedPart, AttackType attackType, Health attacker, int attackForce, float procCoefficient = 1f, Vector3? impact = null)
        {
            int part = (int)damagedPart;
            if (part < 0 || part >= damageMultipliers.Length) return;
            if (attacker != this && attacker is CharacterInstance character && attackType != AttackType.falldamage)
                damage = character.ScaleRainWeaponDamage(damage);
            ApplyServerDamage(Mathf.FloorToInt(damage * damageMultipliers[part]), damagedPart, attackType, attacker, attackForce, procCoefficient: procCoefficient, impact: impact);
        }
        public void Server_ChangeHealthStateRaw(int damage, CharacterPart damagedPart, AttackType attackType, Health attacker, int attackForce, float procCoefficient = 1f)
        {
            ApplyServerDamage(damage, damagedPart, attackType, attacker, attackForce, procCoefficient: procCoefficient);
        }
        internal void ServerApplyRainBlast(int damage, Health attacker, Vector3 impulse)
        {
            ApplyServerDamage(damage, CharacterPart.body, AttackType.explosion, attacker, 0, rainImpulse: impulse);
        }
        internal void ServerApplyRaidSkillDamage(int damage, Health attacker)
        {
            ApplyServerDamage(damage, CharacterPart.body, AttackType.explosion, attacker, 0, raidSkill: true);
        }

        internal void ServerApplyRangeDot(int damage, AttackType type, Health attacker)
        {
            ApplyServerDamage(damage, CharacterPart.body, type, attacker, 0, periodic: true);
        }

        internal void ServerApplyRangeItemBlast(int damage, Health attacker)
        {
            ApplyServerDamage(damage, CharacterPart.body, AttackType.explosion, attacker, 0, periodic: true);
        }

        internal void ServerApplyRangeChainLightning(int damage, Health attacker, RangeProcSource ancestors)
        {
            ApplyServerDamage(damage, CharacterPart.body, AttackType.explosion, attacker, 0,
                procCoefficient: .2f, ancestors: ancestors | RangeProcSource.Lightning);
        }

        internal void ServerApplyRangeMissile(int damage, Health attacker, RangeProcSource ancestors, float coefficient)
        {
            ApplyServerDamage(damage, CharacterPart.body, AttackType.explosion, attacker, 0,
                procCoefficient: coefficient, ancestors: ancestors | RangeProcSource.Missile);
        }

        internal void ServerApplyRangeAreaProc(int damage, Health attacker, float coefficient, RangeProcSource ancestors, bool preRolledCritical, bool moltenImpact)
        {
            ApplyServerDamage(damage, CharacterPart.body, AttackType.explosion, attacker, 0,
                procCoefficient: coefficient, ancestors: ancestors, preRolledCritical: preRolledCritical, moltenImpact: moltenImpact);
        }

        internal void ServerApplyRangeDagger(int damage, Health attacker)
        {
            ApplyServerDamage(damage, CharacterPart.body, AttackType.explosion, attacker, 0,
                procCoefficient: 1f, ancestors: RangeProcSource.Dagger);
        }

        internal void ServerApplyRangeIceRing(int damage, Health attacker, RangeProcSource ancestors)
        {
            ApplyServerDamage(damage, CharacterPart.body, AttackType.explosion, attacker, 0,
                ancestors: ancestors | RangeProcSource.Ring);
        }

        void ApplyServerDamage(int damage, CharacterPart part, AttackType type, Health attacker, int force,
            bool periodic = false, float procCoefficient = 1f, Vector3? impact = null,
            RangeProcSource ancestors = RangeProcSource.None, bool preRolledCritical = false, bool moltenImpact = false,
            bool raidSkill = false, Vector3? rainImpulse = null)
        {
            if (!isServer || _dead || RangeInvulnerable || damage <= 0 || !attacker || !GameManager.Gamemode) return;
            if (ServerDamageAllowed != null && !ServerDamageAllowed(attacker)) return;
            if (this is CharacterInstance drone && drone.RainDroneOwner != 0 && attacker.Team == Team) return;
            if (!GameManager.Gamemode.FriendyFire && attacker != this && attacker.Team == Team) return;
            if (Jotunnslayer.SkillBattlePreview.IsProtectedByIceBlock(this, Time.time)) return;
            if (Jotunnslayer.SkillBattlePreview.IsProtectedByEldritchWard(this, attacker, Time.time)) return;
            if (!periodic && Jotunnslayer.SkillBattlePreview.TryAvoidRaidDamage(this, attacker)) return;
            if (!periodic && ancestors == RangeProcSource.None && attacker != this &&
                type != AttackType.falldamage && attacker is CharacterInstance source)
            {
                float penalty = source.BeetleJuicePenalty;
                if (penalty > 0f)
                    damage = Mathf.Max(1, Mathf.RoundToInt(damage * (1f - penalty)));
            }
            if (attacker != this && type != AttackType.falldamage)
                damage = Jotunnslayer.SkillBattlePreview.ScaleRaidAllDamage(attacker, damage);
            bool raidCritical = false;
            if (!periodic && ancestors == RangeProcSource.None && procCoefficient > 0f &&
                attacker.Team != Team && type != AttackType.falldamage)
                damage = Jotunnslayer.SkillBattlePreview.ScaleRaidCriticalDamage(attacker, damage, raidSkill, out raidCritical);
            var research = attacker.GetComponent<RangeResearchItems>();
            if (!periodic && type != AttackType.falldamage)
                damage = Jotunnslayer.SkillBattlePreview.ModifyIllOmenDamage(attacker, this, damage, Time.time);
            damage = Jotunnslayer.SkillBattlePreview.ModifyRaidVulnerabilityDamage(this, damage, Time.time);
            damage = Jotunnslayer.SkillBattlePreview.ModifyFortifiedDamage(this, damage);
            int baseDamage = damage;
            bool critical = preRolledCritical;
            Vector3? focusCrystalImpact = null;
            if (!research && !periodic && ancestors == RangeProcSource.None && attacker != this &&
                attacker is CharacterInstance heroAttacker && heroAttacker.HeroBonuses.critical > 0f &&
                Random.value < heroAttacker.HeroBonuses.critical)
            {
                critical = true;
                damage = (int)System.Math.Min(int.MaxValue, (long)damage * 2);
            }
            if (!periodic && ancestors == RangeProcSource.None && research && attacker != this)
            {
                damage = research.ApplyBossDamage(research.ApplyCrowbarDamage(research.ModifyDamage(damage, out critical), this), this);
                Vector3 center = impact ?? (this is CharacterInstance target ? target.CombatAimPoint : transform.position);
                int amplified = research.ApplyNearbyDamage(damage, center);
                if (amplified > damage) focusCrystalImpact = center;
                damage = amplified;
            }
            int procDamage = damage;
            TryGetComponent<RangeResearchItems>(out var defenderItems);
            // Collector scales the combined signed armor value, including base/environment armor.
            var heroDefender = this as CharacterInstance;
            bool heroArmor = heroDefender && (heroDefender.HeroBonuses.armor != 0f || heroDefender.HeroFlatArmor != 0f);
            bool combinedArmor = defenderItems && (defenderItems.CollectorBuffCount > 0 || heroArmor || defenderItems.HasPowerCubeArmor);
            float baseArmor = BaseArmor;
            if (heroDefender) baseArmor += heroDefender.HeroFlatArmor;
            if (GameManager.Gamemode is Gamemodes.RangeGamemode effigyMode && effigyMode.IsWithinRainEffigy(transform.position))
                baseArmor -= 20f;
            if (!defenderItems && heroDefender) baseArmor *= heroDefender.HeroArmorMultiplier;
            if (!combinedArmor && baseArmor != 0f && damage > 0)
            {
                float scale = baseArmor > 0f ? 100f / (100f + baseArmor) : 2f - 100f / (100f - baseArmor);
                damage = Mathf.Max(1, (int)System.Math.Min(int.MaxValue, System.Math.Round(damage * (double)scale)));
            }
            if (defenderItems && defenderItems.HasExtraLifeProtection) return;
            if (defenderItems && attacker.Team != Team && defenderItems.RollDamageBlock()) return;
            if (TryGetComponent<RangeTestBuffs>(out var defenderBuffs)) damage = defenderBuffs.ReduceDamage(damage);
            if (defenderItems) damage = defenderItems.ReduceIncomingDamage(damage, combinedArmor ? baseArmor : 0f);
            var droneShield=GetComponent<RangeDroneStatus>();
            if(droneShield){damage=droneShield.Absorb(damage);if(damage<=0)return;}
            if (defenderItems)
            {
                damage = defenderItems.AbsorbBarrier(damage);
            }
            int beforePersonalShield = damage;
            if (damage > 0 && this is CharacterInstance shieldOwner)
                damage = shieldOwner.AbsorbResearchShield(damage);
            int shieldTaken = beforePersonalShield - damage;
            if (damage <= 0 && shieldTaken == 0) return;
            bool wasAtFullHealth = CurrentHealth >= MaxHealth;
            if (defenderItems && damage > 0) defenderItems.BeforeHealthDamage();
            int taken = Mathf.Min(CurrentHealth, damage);
            if (taken > 0 && taken < CurrentHealth && !periodic && research && attacker.Team != Team &&
                research.RollLostSeersExecution(this, procCoefficient))
            {
                taken = CurrentHealth;
                if (attacker is CharacterInstance seer)
                    seer.RpcRainLostSeersExecution(impact ?? transform.position + Vector3.up);
            }
            if (taken > 0 && taken < CurrentHealth && !periodic && ancestors == RangeProcSource.None &&
                procCoefficient > 0f && attacker.Team != Team && type is AttackType.hitscan or AttackType.melee &&
                Jotunnslayer.SkillBattlePreview.RollPerthSlay(attacker, this))
                taken = CurrentHealth;
            CurrentHealth = Mathf.Max(0, CurrentHealth - taken);
            if (CurrentHealth == 0 && taken > 0 &&
                (Jotunnslayer.SkillBattlePreview.TryConsumeRuneRevive(this) ||
                    (defenderItems && defenderItems.TryConsumeExtraLife())))
                CurrentHealth = MaxHealth;
            if (moltenImpact && taken > 0 && CurrentHealth > 0 && attacker.Team != Team)
            {
                if (!TryGetComponent<RangeBleeding>(out var burn))
                    burn = gameObject.AddComponent<RangeBleeding>();
                burn.AddIgnite(attacker, damage, research ? research.ItemBaseAttack : CharacterInstance.RainBaseAttackPower);
            }
            Vector3 hitPosition = transform.position;
            if (defenderItems && taken > 0) defenderItems.OnHealthDamaged(taken);
            int hitTaken = taken + shieldTaken;
            if (hitTaken > 0 && focusCrystalImpact.HasValue && attacker is CharacterInstance crystalOwner)
                crystalOwner.RpcRainFocusCrystal(focusCrystalImpact.Value);
            if (defenderItems && hitTaken > 0 && (ancestors & RangeProcSource.Razorwire) == 0)
                defenderItems.QueueRazorwire(hitPosition, ancestors);
            if (hitTaken > 0 && attacker.Team != Team)
            {
                if (research) research.RefreshChronicExpansion();
                if (taken > 0) Jotunnslayer.SkillBattlePreview.ApplyHagalazLifesteal(attacker, taken);
                if (taken > 0 && (raidCritical || critical))
                    Jotunnslayer.SkillBattlePreview.OnRaidCriticalHit(attacker, this, Time.time);
                if (ancestors == RangeProcSource.Urn)
                {
                    if (this is CharacterInstance tarred && CurrentHealth > 0)
                        tarred.ResearchTarUntil = NetworkTime.time + 1.25d;
                    attacker.ServerHeal(hitTaken, attacker.netId);
                }
                else
                {
                    if (ancestors == RangeProcSource.FrostRelic && this is CharacterInstance chilled && CurrentHealth > 0)
                        chilled.ResearchIceSlowUntil = System.Math.Max(chilled.ResearchIceSlowUntil, NetworkTime.time + 1.5d);
                    if (research) research.RecordCombat();
                }
                if (defenderItems) defenderItems.RecordCombat();
            }
            RememberDamage(attacker, (short)Mathf.Min(hitTaken, short.MaxValue));
            // Apply only after the real rejection/block/shield path accepts the hit,
            // before damage callbacks can disable or replace the receiving motor.
            if (rainImpulse.HasValue && TryGetComponent<CharacterMotor>(out var impulseMotor))
                impulseMotor.ServerApplyRainImpulse(rainImpulse.Value);
            RpcHealthStateChanged(CurrentHealth, hitTaken, part, type, attacker);
            Server_OnDamaged?.Invoke(CurrentHealth, part, type, attacker, force);
            if (taken > 0) Jotunnslayer.SkillBattlePreview.TriggerEldritchWard(this, attacker, Time.time);
            if (taken > 0 && !periodic && ancestors == RangeProcSource.None && type == AttackType.hitscan)
                Jotunnslayer.SkillBattlePreview.OnRaidWeaponHit(attacker, this, Time.time);
            if (defenderItems && taken > 0) defenderItems.ResolveLowHealthItems();
            bool canProc = !periodic && procCoefficient > 0f && research && attacker.Team != Team && hitTaken > 0;
            if (canProc)
            {
                if (research.TryHikingBootsHit(this, ancestors)) ancestors |= RangeProcSource.HikingBoots;
                if (wasAtFullHealth) research.QueueVoidsentFlame(impact ?? hitPosition);
                research.TryInflictBleed(this, procCoefficient, critical);
                research.TryAttachStickyBomb(this, procDamage, procCoefficient);
                research.TryChargedPerforator(this, procDamage, procCoefficient);
                if ((ancestors & RangeProcSource.MoltenPerforator) == 0)
                    research.TryMoltenPerforator(impact ?? hitPosition + Vector3.up, procDamage, procCoefficient, ancestors);
                if ((ancestors & RangeProcSource.Missile) == 0)
                    research.TryLaunchMissile(this, procDamage, procCoefficient, ancestors);
                research.TryEarnBrittleCrownGold(procCoefficient);
                research.TryStunOnHit(this, procCoefficient);
                research.TryApplyBaubleSlow(this, procCoefficient);
                research.ApplyShatteringJustice(this);
                research.ApplySymbioticScorpion(this, procCoefficient);
                research.ApplyDeathMark(this);
                research.TryHealFromSeed(procCoefficient);
                if (critical) research.OnCriticalHit(procCoefficient);
                if ((ancestors & RangeProcSource.Lightning) == 0)
                    research.TryUkulele(this, impact ?? hitPosition, procDamage, procCoefficient, ancestors);
            }
            if (!periodic && attacker != this && attacker.TryGetComponent<RangeTestBuffs>(out var buffs))
                buffs.OnDamageDealt(hitTaken);
            if (research && attacker.Team != Team && hitTaken > 0)
                research.HealFromLeech(hitTaken);
            if (CurrentHealth <= 0)
            {
                _dead = true;
                if (research && attacker != this)
                {
                    bool enemyKill = attacker.Team != Team;
                    if (enemyKill) research.TryExplodeShatterspleen(this, hitPosition);
                    research.OnKill(hitPosition, enemyKill);
                }
                Server_OnHealthDepleted?.Invoke(part, type, attacker, force);
                RpcNotifyGamemode(part, type, attacker, force, EvaluateAssist(attacker));
            }
            if (canProc && research)
            {
                if ((ancestors & RangeProcSource.Ring) == 0)
                    research.TryElementalRings(this, hitPosition, procDamage, ancestors);
                research.TryBehemothBlast(hitPosition, procDamage, procCoefficient);
            }
        }

        [ClientRpc]
        void RpcHealthStateChanged(int currentHealth, int takenDamage, CharacterPart damagedPart, AttackType attackType, Health attacker)
        {
            if (!isServer)
                CurrentHealth = currentHealth;

            //Health attacker = GameSync.Singleton.Healths.GetObj(attackerID);

            if (!attacker) return;

            if (!_clientDead)
            {
                Client_OnHealthStateChanged?.Invoke(currentHealth, damagedPart, attackType, attacker);

                attacker.Client_OnDamageDealt?.Invoke(currentHealth, takenDamage, damagedPart, attackType, this);

                if (CurrentHealth <= 0)
                {
                    Client_OnHealthDepleted?.Invoke(damagedPart, attacker);


                    if (attacker)
                        attacker.Client_KillConfirmation?.Invoke(damagedPart, this);

                    _clientDead = true;
                }
            }
        }

        #region assists
        void RememberDamage(Health attackerToRemember, short receivedDamage)
        {
            int currentID = DoWeRememberHim(attackerToRemember);

            if (currentID > -1)
            {
                _damageMemories[currentID].DamageDealt += (ushort)receivedDamage;
                _lastDamageMemory = currentID + 1;
            }
            else
            {
                if (_lastDamageMemory >= _damageMemories.Length)
                    _lastDamageMemory = 0;

                _damageMemories[_lastDamageMemory].DamageDealt = (ushort)receivedDamage;
                _damageMemories[_lastDamageMemory].PIID = attackerToRemember;

                _lastDamageMemory++;
            }

            int DoWeRememberHim(Health attacker)
            {
                for (int i = 0; i < _damageMemories.Length; i++)
                {
                    DamageMemory mem = _damageMemories[i];
                    if (mem.DamageDealt > 0 && mem.PIID == attacker) return i;
                }
                return -1;
            }
        }
        protected Health EvaluateAssist(Health killer)
        {
            int highestDmg = 0;
            Health id = null;

            DamageMemory mem;

            for (byte i = 0; i < _damageMemories.Length; i++)
            {
                mem = _damageMemories[i];
                if (mem.PIID != killer && mem.DamageDealt > highestDmg)
                {
                    highestDmg = mem.DamageDealt;
                    id = mem.PIID;
                }
            }

            if (id && highestDmg > Mathf.FloorToInt(MaxHealth * 0.5f))
            {
                id.Server_OnEarnedAssist?.Invoke(this);
                id.RpcOnAssistEarned(this);
                return id;
            }
            
            return null;
        }
        #endregion
        [ClientRpc]
        void RpcOnAssistEarned(Health victim) 
        {
            Client_OnEarnedAssist?.Invoke(victim);
        }


        [ClientRpc]
        void RpcNotifyGamemode(CharacterPart hittedPart, AttackType attackType, Health attacker, int attackForce, Health assist)
        {
            GameManager.Gamemode.Client_PlayerKilledByPlayer?.Invoke(this, hittedPart, attackType, attacker, assist); //killfeed listens to this event
        }


        [ClientRpc]
        void RpcHealthAdded(int currentHealth, int addedHealth, uint healerID)
        {
            if (!isServer)
                CurrentHealth = currentHealth;

            Client_OnHealthAdded?.Invoke(currentHealth, addedHealth, healerID);
        }

        public int CountDamage(byte hittedPart, int damage)
        {
            return Mathf.FloorToInt(damage * damageMultipliers[hittedPart]);
        }

        //for AI to know where to aim at
        public Vector3 GetPositionToAttack()
        {
            return _targetPositionTracker.position;
        }

        //execute only on server, then server will update health state for every client
        public int ServerHeal(int healthToAdd, uint healerID, bool allowOverhealBarrier = true)
        {
            if (healthToAdd > 0 && Jotunnslayer.SkillBattlePreview.BlocksAllInHealing(this)) return 0;
            TryGetComponent<RangeResearchItems>(out var items);
            if (items) healthToAdd = items.ScaleHealing(healthToAdd);
            healthToAdd = Jotunnslayer.SkillBattlePreview.ScaleRaidHealing(this, healthToAdd);
            if (healthToAdd > 0 && Jotunnslayer.SkillBattlePreview.HasDisease(this)) healthToAdd /= 2;
            if (isServer && items) items.StoreHealingSoul(healthToAdd);
            int neededHealth = MaxHealth - CurrentHealth;
            if (allowOverhealBarrier && items && healthToAdd > neededHealth && healthToAdd > 0)
                items.ConvertOverheal(healthToAdd - neededHealth);
            if (neededHealth == 0) return 0;

            if (neededHealth > healthToAdd)
                neededHealth = healthToAdd;

            CurrentHealth += neededHealth;

            RpcHealthAdded(CurrentHealth, neededHealth, healerID);

            return neededHealth;
        }

        [Server]
        public void ServerSetMaximumHealth(int maximum)
        {
            var hero = this as CharacterInstance;
            if (hero) maximum -= hero.HeroAppliedMaximum;
            if (TryGetComponent<RangeResearchItems>(out var items))
                maximum = items.RecalculateItemMaximum(maximum);
            if (hero) maximum = hero.ApplyHeroMaximum(maximum);
            MaxHealth=Mathf.Max(1,maximum);
            CurrentHealth=Mathf.Min(CurrentHealth,MaxHealth);
            RpcMaximumHealthChanged(CurrentHealth,MaxHealth);
        }
        [ClientRpc]
        void RpcMaximumHealthChanged(int current,int maximum)
        {
            if(!isServer){CurrentHealth=current;MaxHealth=maximum;}
            Client_OnHealthAdded?.Invoke(current,0,netId);
        }

        public void ServerResurrect()
        {
            //prepare client for resurrection
            RpcClientResurrect();

            //resurrect
            _dead = false;
            CurrentHealth = MaxHealth;
            Server_Resurrect?.Invoke(MaxHealth);
        }
        [ClientRpc]
        public void RpcClientResurrect()
        {
            _clientDead = false;
            CurrentHealth = MaxHealth;
            Client_Resurrect?.Invoke(CurrentHealth);
        }
    }

    [System.Serializable]
    public struct DamageMemory
    {
        public Health PIID;
        public int DamageDealt;
    }

    public enum CharacterPart : byte
    {
        body,
        head,
        legs,
    }

    public enum AttackType : byte
    {
        hitscan,
        melee,
        explosion,
        falldamage,
    }
}
