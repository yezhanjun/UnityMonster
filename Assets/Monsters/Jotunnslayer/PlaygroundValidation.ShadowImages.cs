#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class PlaygroundValidation
    {
        private static bool shadowImagesVerified;

        private static IEnumerator ValidateMatchShadowImages(CharacterInstance owner, SkillBattlePreview battle, SkillProgress restore)
        {
            shadowImagesVerified = false;
            var playground = FindFirstObjectByType<SkillPlayground>();
            bool playgroundEnabled = playground && playground.enabled;
            if (playground) playground.enabled = false;
            int originalLevel = battle.RaidLevel, originalChoices = battle.PendingRewards;
            battle.enabled = false;
            battle.Bind(null, restore, null);
            bool motorEnabled = owner.GetComponent<CharacterMotor>().enabled;
            owner.GetComponent<CharacterMotor>().enabled = false;
            for (int rank = 1; rank <= 4; rank++)
            {
                var profile = new SkillProgress(SkillCatalog.Load(), null, 10);
                if (rank == 4 && !profile.TryLearn(SkillBattlePreview.ShadowImagesEvolution))
                { Fail("Formal Shadow Images evolution profile unavailable"); yield break; }
                var runtime = new GameObject("Formal Shadow Images rank " + rank).AddComponent<SkillBattlePreview>();
                runtime.enabled = false;
                runtime.LiveMatch = true;
                runtime.SelectedRaidClass = RaidClass.Valkyrie;
                runtime.Bind(owner, profile, null);
                runtime.SetRaidLevel(18);
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    owner.transform.position, Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                target.MaxHealth = target.CurrentHealth = 10000;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                int hitCount = 0;
                bool foreignDamage = false;
                target.Server_OnDamaged += (health, part, type, attacker, force) =>
                {
                    hitCount++;
                    foreignDamage |= attacker != owner || type != AttackType.explosion;
                };
                yield return null;
                for (int i = 0; i < rank; i++)
                    if (!runtime.TryChooseShadowImages())
                    { Fail($"Formal Shadow Images rank {rank} unavailable"); yield break; }
                var images = (IList)typeof(SkillBattlePreview).GetField("shadowImages", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
                var batches = (IList)typeof(SkillBattlePreview).GetField("shadowImageBatches", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
                float start = Time.time + 10f;
                float delay = rank == 1 ? 0.25f : rank == 2 ? 0.2f : 0.18f;
                int count = 1 + 2 * Mathf.Min(rank, 3);
                target.CurrentHealth = target.MaxHealth;
                runtime.Cast(start);
                int castDamage = target.MaxHealth - target.CurrentHealth;
                if (images.Count != 0 || batches.Count != 1 || (rank == 4 ? castDamage < 27 || castDamage > 33 : castDamage != 0))
                { Fail($"Formal Shadow Images rank {rank} initial state or cast damage={castDamage}"); yield break; }
                // Keep the actual Valkyrie Soulmate running, but put the shadow target beyond
                // its seven-metre acquisition radius and inside Shadow Images' eight metres.
                // The earliest two evolved followers expire during later attacks; keep this
                // target opposite their initial forward arc so those blasts are tested separately.
                target.transform.position = owner.transform.position - owner.transform.forward * 7.5f;
                runtime.Tick(start + delay - 0.01f);
                if (images.Count != 0)
                { Fail("Formal Shadow Images spawned before its delay"); yield break; }
                int totalAttack = 0;
                for (int i = 1; i <= count; i++)
                {
                    // Tick only around each attack; do not advance an extra spawn accidentally.
                    float spawn = start + i * (delay + 0.001f);
                    runtime.Tick(spawn);
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        foreach (var item in images)
                            if (item.GetType().GetField("presentation").GetValue(item) == null)
                            { Fail("Formal Shadow Images original model presentation missing"); yield break; }
                    target.CurrentHealth = target.MaxHealth;
                    hitCount = 0;
                    runtime.Tick(spawn + 0.14f);
                    int attack = target.MaxHealth - target.CurrentHealth;
                    if (attack < 22 || attack > 28 || hitCount != 1 || foreignDamage)
                    { Fail($"Formal Shadow Images rank={rank} follower={i} attack={attack} hits={hitCount} foreign={foreignDamage}"); yield break; }
                    totalAttack += attack;
                }
                int expiryDamage = 0;
                if (rank == 4)
                {
                    object earliest = images[0];
                    target.transform.position = (Vector3)earliest.GetType().GetField("position").GetValue(earliest);
                    float expires = (float)earliest.GetType().GetField("expires").GetValue(earliest);
                    target.CurrentHealth = target.MaxHealth;
                    runtime.Tick(expires + 0.001f);
                    expiryDamage = target.MaxHealth - target.CurrentHealth;
                    if (expiryDamage < 27 || expiryDamage > 33)
                    { Fail($"Formal Shadow Images natural explosion damage={expiryDamage}"); yield break; }
                }
                target.Team = owner.Team; // Expiry cannot damage an ally, including overlapping blasts.
                target.CurrentHealth = target.MaxHealth;
                runtime.Tick(start + 4f);
                if (images.Count != 0 || batches.Count != 0 || target.CurrentHealth != target.MaxHealth)
                { Fail("Formal Shadow Images lifetime or ally exclusion failed"); yield break; }
                runtime.Cast(start + 5f);
                runtime.Tick(start + 5f + delay + 0.001f);
                runtime.Bind(null, profile, null);
                if (images.Count != 0 || batches.Count != 0)
                { Fail("Formal Shadow Images unbind retained queued or live followers"); yield break; }
                runtime.Tick(start + 8f);
                if (target.CurrentHealth != target.MaxHealth)
                { Fail("Formal Shadow Images cleanup caused damage"); yield break; }
                Debug.Log($"JOTUNNSLAYER_FORMAL_SHADOW_RANK_OK rank={rank} followers={count} castDamage={castDamage} attackTotal={totalAttack} expiryDamage={expiryDamage} perImageLifetime=1 cleanup=True foreignDamage={foreignDamage} fixtureClock=True");
                Destroy(runtime.gameObject);
                NetworkServer.Destroy(target.gameObject);
                yield return null;
            }
            owner.GetComponent<CharacterMotor>().enabled = motorEnabled;
            battle.Bind(owner, restore, null);
            battle.enabled = true;
            if (playground) playground.enabled = playgroundEnabled;
            if (battle.RaidLevel != originalLevel || battle.PendingRewards != originalChoices)
            { Fail("Formal Shadow Images modified the original raid progression"); yield break; }
            shadowImagesVerified = true;
        }
    }
}
#endif
