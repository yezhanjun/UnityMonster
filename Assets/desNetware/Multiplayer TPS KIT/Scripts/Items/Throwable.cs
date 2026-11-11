using Mirror;
using MTPSKIT;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT.Gameplay {
	public class Throwable : NetworkBehaviour
	{
		//if someone is killed by this throwable then we will assign kill to this owner
		CharacterInstance _myOwner;
		public float ExplosionRange = 4f;
		public int MaxDamage = 200;

		public float TimeToDetonate = 4f;
		public bool DetonateOnCollision = false;
		public int DamageOnDirectCollision = 30;
		public float ExplosionRigidbodyForce = 300;

		bool _detonated = false;

		[SerializeField] GameObject _model;
		[SerializeField] ParticleSystem _explosionEffect;
		[SerializeField] AudioClip _explosionClip;
        [SerializeField] GameObject blastPrefab;
        [SerializeField] GameObject craterPrefab;
        [SerializeField] GameObject fireEffect;
        [SyncVar] Vector3 firePosition;
        [SyncVar] Quaternion fireRotation;
        [SyncVar(hook = nameof(OnBurningChanged))] bool burning;
        GameObject flames;

        public float PredictionDuration => fireEffect ? 10f : TimeToDetonate;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (burning) ShowFlames();
        }

        void OnBurningChanged(bool previous, bool current)
        {
            if (current && isClient) ShowFlames();
        }

        void ShowFlames()
        {
            _model.SetActive(false);
            if (!flames)
                flames = Instantiate(fireEffect, firePosition, fireRotation, transform);
        }

        IEnumerator BurnArea()
        {
            var interval = new WaitForSeconds(1f);
            var targets = new HashSet<Health>();
            var hits = new Collider[32];
            for (int tick = 0; tick < 10; tick++)
            {
                int count;
                while ((count = Physics.OverlapSphereNonAlloc(firePosition, 3f, hits, GameManager.characterLayer)) == hits.Length)
                    System.Array.Resize(ref hits, hits.Length * 2);
                targets.Clear();
                for (int i = 0; _myOwner && i < count; i++)
                {
                    var health = hits[i].GetComponentInParent<Health>();
                    if (health && health.CurrentHealth > 0 && targets.Add(health))
                        health.Server_ChangeHealthState(20, CharacterPart.body, AttackType.explosion, _myOwner, 0);
                }
                yield return interval;
            }
            RemoveProjectile();
        }

        void RemoveProjectile() => NetworkServer.Destroy(gameObject);

		Rigidbody _rigidBody;

		CapsuleCollider _collider;

		bool _crashed = false;

        /// <summary>
        /// method launched on spawning
        /// </summary>
        private void Awake()
        {
			GameTools.SetLayerRecursively(gameObject, 12);
			_collider = GetComponent<CapsuleCollider>();
			_collider.enabled = false;
		}
        public void Activate(CharacterInstance owner, Vector3 force)
		{
			_collider = GetComponent<CapsuleCollider>();
			_collider.enabled = true;

			_myOwner = owner;
			_detonated = false;

			_rigidBody = gameObject.AddComponent<Rigidbody>();
			_rigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

			_rigidBody.linearVelocity = force;
            foreach (var ownCollider in owner.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(_collider, ownCollider);
            GameTools.SetLayerRecursively(gameObject, 10);
            if (fireEffect) Invoke(nameof(RemoveProjectile), 30f);
            else Invoke(nameof(Detonate), TimeToDetonate);
        }

        private void OnCollisionEnter(Collision collision)
		{

			if (!isServer) return; //server will handle collision of throwables

			if (burning) return;
            if (fireEffect)
            {
                var contact = collision.GetContact(0);
                firePosition = contact.point + contact.normal * 0.03f;
                fireRotation = Quaternion.FromToRotation(Vector3.up, contact.normal);
                _collider.enabled = false;
                _rigidBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                _rigidBody.isKinematic = true;
                transform.SetPositionAndRotation(firePosition, fireRotation);
                CancelInvoke(nameof(RemoveProjectile));
                burning = true;
                StartCoroutine(BurnArea());
                return;
            }
            if (!_myOwner) return;

			if (collision.transform.root == _myOwner.transform.root) return;

			if (!_crashed) 
			{
				HitBox hb = collision.gameObject.GetComponent<HitBox>();
				if (hb && DamageOnDirectCollision > 0)
				{
					hb._health.Server_ChangeHealthState(DamageOnDirectCollision, CharacterPart.body, AttackType.explosion, _myOwner, 100);
				}
				StartCoroutine(CancelVelocity());
				IEnumerator CancelVelocity() 
				{
					yield return new WaitForSeconds(0f);
					_rigidBody.linearVelocity = Vector3.zero;
				}
			}
			_crashed = true;

			if (!DetonateOnCollision) return;

			if (_detonated) return;

			Detonate();
		}
		void Detonate()
		{
			if (_detonated) return;
            _detonated = true;

			RpcDetonate(); //visual effect for clients

			_collider.enabled = false;

			_rigidBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
			_rigidBody.isKinematic = true;
			_rigidBody.useGravity = false;

			transform.rotation = Quaternion.identity;

			//before exploding we have to check if our owner exist, because if not, for example due to situation when player
			//thrown granade and disconnected from the game immediately after that, then this granade may kill somebody, but killer does
			//not exist anymore, and this must be prevented, because game will try to find killer which will lead to error.
			if (_myOwner)
			{
				Collider[] colliders = Physics.OverlapSphere(transform.position, ExplosionRange, GameManager.characterLayer);

				foreach (Collider c in colliders)
				{
					Health health = c.GetComponent<Health>();
					if (health != null)
					{
						Vector3 _victimPos = health.transform.position + health.transform.up * 0.5f;
						Vector3 _myPos = transform.position + transform.up * 0.4f;

						float dist = Vector3.Distance(_myPos, _victimPos);
						Ray rayFire = new Ray(_myPos, (_victimPos - _myPos).normalized);


						if (!Physics.Raycast(rayFire, dist, GameManager.environmentLayer)) //avoid damaging things behind the cover
						{
							int damage;

							float percentOfDamage = Mathf.Clamp01(1f - dist / ExplosionRange);
							damage = Mathf.FloorToInt(MaxDamage * percentOfDamage);
							damage = Mathf.Max(1, damage);

							health.Server_ChangeHealthState(damage, CharacterPart.body, AttackType.explosion, _myOwner, 100);
						}
					}


				}
			}

			

			StartCoroutine(CountToDestroyFromScene());

			IEnumerator CountToDestroyFromScene() 
			{
				yield return new WaitForEndOfFrame();

				Collider[] collidersRigidbody = Physics.OverlapSphere(transform.position, ExplosionRange, GameManager.rigidbodyLayer);

				foreach (Collider c in collidersRigidbody)
				{
					Rigidbody rg = c.GetComponent<Rigidbody>();
					if (rg)
					{
						//			Debug.Log("exploded: " + rg.name);
						rg.AddExplosionForce(ExplosionRigidbodyForce, transform.position, ExplosionRange);
					}
				}

				yield return new WaitForSeconds(5f);
				NetworkServer.Destroy(gameObject);
			}

		}
		[ClientRpc]
		void RpcDetonate() 
		{
			GetComponent<AudioSource>().PlayOneShot(_explosionClip);

			_collider.enabled = false;
			transform.rotation = Quaternion.identity;
			_model.SetActive(false);
			if (blastPrefab)
            {
                var blast = Instantiate(blastPrefab, transform.position, Quaternion.identity);
                blast.transform.localScale *= 2f;
                Destroy(blast, 8f);
            }
            else
                _explosionEffect.Play();
            if (craterPrefab && Physics.Raycast(transform.position, Vector3.down, out var ground,
                3f, GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
            {
                var crater = Instantiate(craterPrefab, ground.point + ground.normal * 0.01f,
                    Quaternion.FromToRotation(Vector3.up, ground.normal));
                crater.transform.localScale *= 4f;
                Destroy(crater, 60f);
            }
		}
	}
}
