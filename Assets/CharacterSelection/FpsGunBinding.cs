using UnityEngine;
using KINEMATION.FPSAnimationFramework.Runtime.Playables;

namespace MTPSKIT.Gameplay
{
    [System.Serializable]
    public class AaaSpray
    {
        public float maxAmount, verticalMultiplier, horizontalMultiplier, passiveMultiplier, rampUpTime, recoveryTime;
        public Vector2[] points;

        public Vector3 Next(Transform basis, float amount, ref int index)
        {
            int count = points.Length;
            index = Mathf.Clamp(index, 0, count * 2 - 1);
            int point = index / 2;
            Vector2 offset = points[point];
            if ((index & 1) != 0)
                offset = (offset + points[(point + 1) % count]) * 0.5f;
            index = (index + 1) % (count * 2);
            Vector3 direction = (basis.right * offset.x + basis.up * offset.y) * (amount / 90f);
            direction.x *= horizontalMultiplier;
            direction.y *= verticalMultiplier;
            return (basis.forward + direction).normalized;
        }
    }

    [System.Serializable]
    public class AaaKick
    {
        public Vector3 staticPosition, staticRotation, randomPosition, randomRotation;
        public Vector3 SamplePosition() => staticPosition + new Vector3(Random.Range(-randomPosition.x, randomPosition.x), Random.Range(-randomPosition.y, randomPosition.y), randomPosition.z);
        public Vector3 SampleRotation() => staticRotation + new Vector3(randomRotation.x, Random.Range(-randomRotation.y, randomRotation.y), Random.Range(-randomRotation.z, randomRotation.z));
    }

    [System.Serializable]
    public class AaaWeaponSettings
    {
        public float muzzleVelocity, projectileSize;
        public float fireRate, range, reloadTime, emptyReloadTime, aimFieldOfView, horizontalRecoil, verticalRecoil, cameraRecoil;
        public float fireWalkPlayerSpeed, aimWalkPlayerSpeed, decalSize, pitch, pitchVariation;
        public int damage, fireMode, shotCount, magazineCapacity;
        public string modelGuid, iconGuid, audioGuid;
        public AaaSpray sprayPattern, aimSprayPattern;
        public AaaKick hipKick, aimKick;
    }

    public sealed class FpsGunBinding : MonoBehaviour
    {
        public GameObject viewPrefab;
        public float cycleSeconds;
        public int setupVersion;
        public AaaWeaponSettings settings;
        public GameObject[] impacts;
        public static readonly string[] ResourceNames = { "FpsRifle", "FpsPistol", "FpsShotgun", "FpsSniper" };

        float magnitude, aim, speed, runSpeed;
        int index;
        CharacterController motor;
        public Vector3 OwnerVelocity => motor ? motor.velocity : Vector3.zero;
        Vector3 positionImpulse, rotationImpulse, cameraImpulse;
        public Vector3 PositionKick { get; private set; }
        public Vector3 RotationKick { get; private set; }
        public Vector3 CameraKick { get; private set; }

        public void Equip(CharacterInstance owner)
        {
            motor = owner.GetComponent<CharacterController>();
            runSpeed = owner.GetComponent<CharacterMotor>().RunSpeed;
            magnitude = settings.sprayPattern.passiveMultiplier;
            aim = 0;
            index = 0;
            PositionKick = RotationKick = CameraKick = positionImpulse = rotationImpulse = cameraImpulse = Vector3.zero;
        }

        public void Tick(CharacterInstance owner, bool firing)
        {
            aim = Mathf.MoveTowards(aim, owner.IsAiming ? 1 : 0, Time.deltaTime * 5);
            speed = motor ? motor.velocity.magnitude : 0;
            var spray = owner.IsAiming ? settings.aimSprayPattern : settings.sprayPattern;
            if (firing)
                magnitude = Mathf.MoveTowards(magnitude, 1, Time.deltaTime / spray.rampUpTime);
            else if (speed <= 0.7f)
            {
                magnitude = Mathf.MoveTowards(magnitude, spray.passiveMultiplier, Time.deltaTime / spray.recoveryTime);
                index = Mathf.RoundToInt(Mathf.InverseLerp(spray.passiveMultiplier, 1, magnitude) * (spray.points.Length - 1));
            }
        }

        public Vector3 Direction(Transform basis, CharacterInstance owner)
        {
            var hip = settings.sprayPattern;
            var ads = settings.aimSprayPattern;
            float amount = Mathf.Lerp(hip.maxAmount * 0.5f, ads.maxAmount, aim) * magnitude;
            if (!owner.IsAiming && !owner.IsGrounded) amount = hip.maxAmount;
            else if (!owner.IsAiming && speed > 0.7f) amount = hip.maxAmount * Mathf.Lerp(0.5f, 1, speed / Mathf.Max(0.01f, runSpeed));
            return (owner.IsAiming ? ads : hip).Next(basis, amount, ref index);
        }

        public void ApplyKick(CharacterInstance owner)
        {
            if (!owner.isOwned || owner.BOT || !PlayerGameplayInput.Instance) return;
            var kick = owner.IsAiming ? settings.aimKick : settings.hipKick;
            positionImpulse += kick.SamplePosition();
            rotationImpulse += kick.SampleRotation();
            cameraImpulse += new Vector3(-3, 0, Random.Range(-5f, 5f) + 1) * settings.cameraRecoil;
            // CharacterInput.AddLookValue is consumed once, on top of raw mouse input.
            var input = PlayerGameplayInput.Instance;
            input.LookInput.y += settings.verticalRecoil * 1.005f;
            input.LookInput.x = Mathf.Clamp(input.LookInput.x - settings.horizontalRecoil * 1.005f, -90, 90);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            positionImpulse = Vector3.Lerp(positionImpulse, Vector3.zero, 35 * dt);
            rotationImpulse = Vector3.Lerp(rotationImpulse, Vector3.zero, 35 * dt);
            cameraImpulse = Vector3.Lerp(cameraImpulse, Vector3.zero, 35 * dt);
            PositionKick = Vector3.Slerp(PositionKick, positionImpulse, 10 * dt);
            RotationKick = Vector3.Slerp(RotationKick, rotationImpulse, 10 * dt);
            CameraKick = Vector3.Slerp(CameraKick, cameraImpulse, 10 * dt);
        }
    }
}
