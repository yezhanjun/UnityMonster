using Mirror;
using MTPSKIT.UI;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    [DefaultExecutionOrder(20100)]
    public class Granade : Item
    {
        public GameObject ThrowablePrefab;
        [SerializeField] float throwSpeed = 30f;
        [SerializeField] Material trajectoryMaterial;
        LineRenderer preview;
        bool preparing;
        double nextServerThrow;
        float previewDuration;
        const int StepsPerSegment = 2;

        public override void PushLeftTrigger()
        {
            if (!isOwned)
            {
                base.PushLeftTrigger();
                return;
            }
            if (!preparing && Time.time >= coolDownTimer && CanBeUsed() && ClientFrontend.GamePlayInput())
            {
                preparing = true;
                FpsViewController.Instance?.ShowGrenadeAction(this, "Trigger");
            }
        }

        void LateUpdate()
        {
            if (!preparing) return;
            if (!currentlyInUse || !_myOwner || !_myOwner.isOwned || _myOwner.Block ||
                _myOwner.CurrentHealth <= 0 || !ClientFrontend.GamePlayInput())
            {
                CancelPreview();
                return;
            }

            if (!Input.GetMouseButton(0))
            {
                CancelPreview();
                FpsViewController.Instance?.ShowGrenadeAction(this, "Grenade_1 |Throw");
                Use();
                coolDownTimer = Time.time + coolDown;
                return;
            }

            DrawPreview();
        }

        void DrawPreview()
        {
            if (!preview)
            {
                var lineObject = new GameObject("投掷轨迹");
                lineObject.transform.SetParent(transform, false);
                preview = lineObject.AddComponent<LineRenderer>();
                preview.sharedMaterial = trajectoryMaterial;
                preview.startWidth = 0.035f;
                preview.endWidth = 0.018f;
                preview.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                preview.receiveShadows = false;
                previewDuration = ThrowablePrefab.GetComponent<Throwable>().PredictionDuration;
            }

            preview.enabled = true;
            Vector3 position = HandOrigin();
            Vector3 velocity = AimDirection() * throwSpeed;
            float dt = Time.fixedDeltaTime;
            int steps = Mathf.CeilToInt(previewDuration / dt);
            preview.positionCount = 2 + steps / StepsPerSegment;
            int count = 0;
            preview.SetPosition(count++, position);
            for (int i = 1; i <= steps; i++)
            {
                velocity += Physics.gravity * dt;
                Vector3 delta = velocity * dt;
                if (Physics.SphereCast(position, 0.07f, delta.normalized, out RaycastHit hit,
                    delta.magnitude, GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                {
                    preview.SetPosition(count++, hit.point);
                    break;
                }
                position += delta;
                if (i % StepsPerSegment == 0 || i == steps) preview.SetPosition(count++, position);
            }
            preview.positionCount = count;
        }

        Vector3 HandOrigin()
        {
            var view = FpsViewController.Instance;
            if (isOwned && view && view.TryGetThrowHand(this, out var hand)) return hand.position;
            return ItemModel.transform.position;
        }

        Vector3 AimDirection()
        {
            Vector2 look = isOwned && PlayerGameplayInput.Instance ? PlayerGameplayInput.Instance.LookInput : _myOwner.lookInput;
            return Quaternion.Euler(look.x, look.y, 0f) * Vector3.forward;
        }

        void CancelPreview()
        {
            preparing = false;
            if (preview) preview.enabled = false;
        }

        public override void PutDown()
        {
            CancelPreview();
            base.PutDown();
        }

        protected override void Use()
        {
            if (!CanBeUsed()) return;
            Vector3 direction = AimDirection();
            Vector3 offset = HandOrigin() - _myOwner.transform.position;
            base.Use();
            if (isServer) SpawnThrowable(direction, offset);
            else CmdSpawnThrowable(direction, offset);
            UpdateAmmoInHud("999");
        }

        [Command]
        void CmdSpawnThrowable(Vector3 direction, Vector3 offset) => SpawnThrowable(direction, offset);

        void SpawnThrowable(Vector3 direction, Vector3 offset)
        {
            if (!_myOwner || _myOwner.Block || _myOwner.CurrentHealth <= 0 ||
                !(offset.sqrMagnitude <= 9f) ||
                !(direction.sqrMagnitude > 0.99f && direction.sqrMagnitude < 1.01f)) return;
            if (NetworkTime.time < nextServerThrow) return;
            nextServerThrow = NetworkTime.time + coolDown;
            Vector3 origin = _myOwner.transform.position + offset;
            Vector3 eye = _myOwner.characterMind.position;
            if (Physics.Linecast(eye, origin, out RaycastHit wall, GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                origin = wall.point + wall.normal * 0.08f;
            var projectile = Instantiate(ThrowablePrefab, origin, Quaternion.LookRotation(direction));
            projectile.GetComponent<Throwable>().Activate(_myOwner, direction.normalized * throwSpeed);
            NetworkServer.Spawn(projectile);
        }
        public override void Take()
        {
            UpdateAmmoInHud("999");
            base.Take();
        }

        protected override void OnOwnerPickedupAmmo()
        {
            UpdateAmmoInHud("999");
            ShowItemModel(true);
        }

        protected override void SingleUse()
        {
            _myOwner.CharacterItemManager.StartUsingItem(); //will disable ability tu run for 0.5 seconds
            _myOwner.CharacterAnimator.Play("Attack1");
        }

        public override bool CanBeUsed()
        {
            return _myOwner && _myOwner.CurrentHealth > 0;
        }
    }
}
