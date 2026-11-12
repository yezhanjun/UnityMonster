using Jotunnslayer;
using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public partial class PlayerInstance
    {
        [TargetRpc]
        internal void TargetBeginJotunnSkills(uint session)
        {
            if (!isLocalPlayer) return;
            var panel = GetComponent<RemoteSkillPanel>();
            if (!panel) panel = gameObject.AddComponent<RemoteSkillPanel>();
            panel.Begin(this, session);
        }

        [Command]
        internal void CmdBeginJotunnSkills(uint session, byte raidClass, string[] learned, bool english)
        {
            if (SkillMatchSession.Active &&
                !SkillMatchSession.Active.StartParticipant(this, session, raidClass, learned, english))
                TargetRejectJotunnLoadout(session);
        }

        [TargetRpc]
        private void TargetRejectJotunnLoadout(uint session)
        {
            if (isLocalPlayer && TryGetComponent<RemoteSkillPanel>(out var panel)) panel.RejectBegin(session);
        }

        [Command]
        internal void CmdJotunnSkillAction(uint session, uint revision, JotunnSkillAction action, int index)
        {
            if (SkillMatchSession.Active)
                SkillMatchSession.Active.ApplyAction(this, session, revision, action, index);
        }

        [TargetRpc]
        internal void TargetJotunnSkillState(JotunnSkillSnapshot state)
        {
            if (isLocalPlayer && TryGetComponent<RemoteSkillPanel>(out var panel)) panel.Receive(state);
        }

        [TargetRpc]
        internal void TargetJotunnSkillDash(uint session, uint revision, uint character, Vector3 direction)
        {
            if (isLocalPlayer && TryGetComponent<RemoteSkillPanel>(out var panel))
                panel.ApproveDash(session, revision, character, direction);
        }

        [TargetRpc]
        internal void TargetEndJotunnSkills(uint session)
        {
            if (isLocalPlayer && TryGetComponent<RemoteSkillPanel>(out var panel)) panel.End(session);
        }
    }
}
