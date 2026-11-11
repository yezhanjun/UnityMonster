using UnityEngine;

namespace AvatarPortability
{
    /// <summary>Allows pose followers to leave simulated transforms under the solver's control.</summary>
    public interface IBoneSimulationOwnership
    {
        bool OwnsBone(Transform bone);
    }
}
