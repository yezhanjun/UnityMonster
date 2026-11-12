using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public partial class CharacterMotor
    {
        // False is the source project's startup mode. Only the two requested modes are exposed.
        [SyncVar] bool godWarControl;
        internal bool GodWarControl => godWarControl;
        internal void RequestControlModeToggle()
        { if (isOwned && UsesFourWayRoll) CmdSetHeroControlMode(!godWarControl); }
        [Command]
        void CmdSetHeroControlMode(bool value) => ServerSetHeroControlMode(value);
        [Server]
        internal bool ServerSetHeroControlMode(bool value)
        {
            if (!UsesFourWayRoll || _charInstance.CurrentHealth <= 0 || IsFourWayRolling || fourJumpStarted >= 0) return false;
            godWarControl = value; pendingRootMotion = Vector3.zero;
            return true;
        }
    }
}
