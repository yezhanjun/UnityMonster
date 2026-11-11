using System;
using System.Collections.Generic;
using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public enum JotunnSkillAction : byte { Accept, Reroll, Banish, Seal, Unseal, ExcludeGod, Dash }

    [Serializable]
    public struct JotunnSkillSnapshot
    {
        public uint session, revision, character;
        public int level, pending, gold, rerolls, banishes, sealCapacity;
        public long experience;
        public float attackSpeed;
        public bool alive;
        public string[] labels, details, sealedLabels;
        public byte[] actions;
    }

    // Scene-owned server coordinator. Remote runtimes never use the host's profile or UI.
    public sealed class SkillMatchSession : MonoBehaviour
    {
        internal static SkillMatchSession Active { get; private set; }
        private static uint nextSession;
        private uint session;
        private SkillCatalog catalog;
        private Font font;
        private float nextPoll;
        private sealed class Participant
        {
            internal PlayerInstance player;
            internal SkillProgress profile;
            internal SkillBattlePreview battle;
            internal JotunnSkillSnapshot last;
            internal bool sent;
            internal uint revision;
        }
        private readonly Dictionary<int, Participant> participants = new Dictionary<int, Participant>();
        private readonly List<int> removed = new List<int>();

        internal void Initialize(SkillCatalog skillCatalog, Font rewardFont)
        {
            catalog = skillCatalog;
            font = rewardFont;
            session = ++nextSession;
            Active = this;
        }

        private void Update()
        {
            if (!NetworkServer.active || catalog == null) return;
            foreach (var pair in NetworkServer.connections)
            {
                var connection = pair.Value;
                if (connection is LocalConnectionToClient || !connection.isReady || !connection.identity) continue;
                var player = connection.identity.GetComponent<PlayerInstance>();
                if (!player || participants.ContainsKey(pair.Key)) continue;
                participants.Add(pair.Key, new Participant { player = player });
                player.TargetBeginJotunnSkills(session);
            }
            removed.Clear();
            foreach (var pair in participants)
            {
                var participant = pair.Value;
                if (!participant.player || !NetworkServer.connections.TryGetValue(pair.Key, out var connection) ||
                    connection.identity != participant.player.netIdentity)
                {
                    Release(participant);
                    removed.Add(pair.Key);
                    continue;
                }
                if (participant.battle)
                    participant.battle.Bind(participant.player.MyCharacter, participant.profile, null);
            }
            foreach (int key in removed) participants.Remove(key);
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + .25f;
            foreach (var participant in participants.Values)
                if (participant.battle) SendState(participant, false);
        }

        private bool TryParticipant(PlayerInstance player, uint token, out Participant participant)
        {
            participant = null;
            return token == session && NetworkServer.active && player && player.connectionToClient != null &&
                participants.TryGetValue(player.connectionToClient.connectionId, out participant) &&
                participant.player == player;
        }

        internal bool StartParticipant(PlayerInstance player, uint token, byte selectedClass, string[] learned, bool english)
        {
            if (!TryParticipant(player, token, out var participant) || participant.battle || selectedClass > (byte)RaidClass.Valkyrie ||
                !SkillProgress.TryImportLoadout(catalog, learned, out var profile)) return false;
            var root = new GameObject("Jotunnslayer remote skills " + player.netId);
            root.transform.SetParent(transform, false);
            var battle = root.AddComponent<SkillBattlePreview>();
            battle.LiveMatch = battle.UseExperienceCrystals = true;
            battle.SelectedRaidClass = (RaidClass)selectedClass;
            battle.ShowControls = false;
            battle.SuppressPresentation = true;
            battle.ConfigureRewardDisplay(catalog, font, english);
            participant.profile = profile;
            participant.battle = battle;
            battle.Bind(player.MyCharacter, profile, null);
            SendState(participant, true);
            return true;
        }

        internal bool ApplyAction(PlayerInstance player, uint token, uint revision, JotunnSkillAction action, int index)
        {
            if (!TryParticipant(player, token, out var participant) || !participant.battle) return false;
            participant.battle.Bind(player.MyCharacter, participant.profile, null);
            // Refresh eligibility before comparing the version shown by the requesting client.
            SendState(participant, false);
            if (revision != participant.revision || !participant.battle.HasLiveCaster)
            {
                SendState(participant, true);
                return false;
            }
            bool accepted = action == JotunnSkillAction.Dash
                ? participant.battle.RequestRaidDash(player.MyCharacter)
                : participant.battle.TryRemoteRewardAction(action, index);
            if (accepted && action == JotunnSkillAction.Dash)
                player.TargetJotunnSkillDash(session, participant.revision, player.MyCharacter.netId,
                    participant.battle.ActiveRaidDashDirection);
            // Every accepted action consumes its old version even if labels happen to match.
            SendState(participant, true);
            return accepted;
        }

        private void SendState(Participant participant, bool force)
        {
            var state = participant.battle.BuildRemoteSnapshot();
            if (!force && participant.sent && SameState(state, participant.last)) return;
            state.session = session;
            state.revision = ++participant.revision;
            participant.last = state;
            participant.sent = true;
            participant.player.TargetJotunnSkillState(state);
        }

        private static bool SameArray<T>(T[] left, T[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
                if (!EqualityComparer<T>.Default.Equals(left[i], right[i])) return false;
            return true;
        }

        private static bool SameState(JotunnSkillSnapshot a, JotunnSkillSnapshot b) =>
            a.character == b.character && a.alive == b.alive && a.attackSpeed == b.attackSpeed && a.level == b.level && a.experience == b.experience &&
            a.pending == b.pending && a.gold == b.gold && a.rerolls == b.rerolls &&
            a.banishes == b.banishes && a.sealCapacity == b.sealCapacity && SameArray(a.labels, b.labels) &&
            SameArray(a.actions, b.actions) && SameArray(a.details, b.details) && SameArray(a.sealedLabels, b.sealedLabels);

        private void Release(Participant participant)
        {
            if (!participant.battle) return;
            participant.battle.Bind(null, participant.profile, null);
            Destroy(participant.battle.gameObject);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal SkillProgress ValidationProfile(PlayerInstance player) =>
            player && player.connectionToClient != null && participants.TryGetValue(player.connectionToClient.connectionId, out var participant)
                ? participant.profile : null;

        internal SkillBattlePreview ValidationRuntime(PlayerInstance player) =>
            player && player.connectionToClient != null && participants.TryGetValue(player.connectionToClient.connectionId, out var participant)
                ? participant.battle : null;

        internal bool ValidationGrantGold(PlayerInstance player, int amount) =>
            player && player.connectionToClient != null && participants.TryGetValue(player.connectionToClient.connectionId, out var participant) &&
            participant.profile != null && participant.profile.TryCollectGold(amount);
#endif

        private void OnDestroy()
        {
            if (Active == this) Active = null;
            foreach (var participant in participants.Values)
            {
                if (NetworkServer.active && participant.player && participant.player.connectionToClient != null)
                {
                    if (participant.battle) SendState(participant, false);
                    participant.player.TargetEndJotunnSkills(session);
                }
                Release(participant);
            }
            participants.Clear();
        }
    }
}
