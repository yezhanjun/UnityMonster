using MTPSKIT.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT.Gameplay {
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] CameraCollision _cameraCollision;
        [HideInInspector] public CharacterInstance _myCharacterToFollow;
        public Transform cameraSticker;
        [SerializeField] float _characterHeight = 1.75f;
        [SerializeField] float _characterCrouchHeight = 1.25f;
        [SerializeField] float _currentCharacterHeight;
        [SerializeField] float scopeSpeed;
        [SerializeField] float cameraFollowPositionSpeed;
        [SerializeField] Vector3 cameraPosition = new Vector3(0.75f, 1.25f, -2.5f);
        [SerializeField] Vector3 _cameraAimPosition = new Vector3(0.75f, 1.25f, -2.5f);
        Vector3 currentTargetCameraPosition;

        [Tooltip("Object that camera will get closer to when there are game objects colliding with it")]
        public Transform rayCastSource;

        Transform _recoilObjectReference;


        Health _objectToFollow;
        GameplayCamera _camera;

        void Update()
        {
            if (GodOfWarHeroCamera.HandlesPlayer) return;
            if (!_myCharacterToFollow) return;
            bool cameraAiming = _myCharacterToFollow.IsAiming &&
                _myCharacterToFollow.CharacterItemManager.CurrentlyUsedItem is Gun;
            //aiming fov
            if (cameraAiming)
                _camera.SetFovMultiplier(_myCharacterToFollow.CharacterItemManager.CurrentlyUsedItem.FOVScopeMultiplier);
            else
                _camera.SetFovMultiplier(1);


            _currentCharacterHeight = Mathf.MoveTowards(_currentCharacterHeight, _myCharacterToFollow.IsCrouching ? _characterCrouchHeight : _characterHeight, 3f * Time.deltaTime);

            //killcam
            if (_objectToFollow) 
            {
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(_objectToFollow.GetPositionToAttack() - transform.position), 7f * Time.deltaTime);
                return;
            }

            if (!_myCharacterToFollow) return;

            //change sides od 3rd person view
            if (Input.GetKeyDown(KeyCode.LeftAlt) && ClientFrontend.GamePlayInput())
            {
                _cameraAimPosition.x *= (-1);
                cameraPosition.x *= (-1);
            }
            _cameraCollision.UpdateCameraProperties(cameraAiming ? _cameraAimPosition : cameraPosition);
            cameraSticker.transform.eulerAngles = new Vector3(PlayerGameplayInput.Instance.LookInput.x, PlayerGameplayInput.Instance.LookInput.y,0);

            transform.localPosition = new Vector3(0, _currentCharacterHeight, 0);

            _myCharacterToFollow.FPPCameraTarget = _cameraCollision.transform;

            transform.rotation = Quaternion.Euler(PlayerGameplayInput.Instance.LookInput.x, PlayerGameplayInput.Instance.LookInput.y, 0);

            currentTargetCameraPosition = Vector3.Lerp(currentTargetCameraPosition, cameraAiming ? _cameraAimPosition : cameraPosition, scopeSpeed * Time.deltaTime);


            rayCastSource.localPosition = new Vector3(currentTargetCameraPosition.x, currentTargetCameraPosition.y, 0.6f); //its for avoiding aiming character backwards

            //float shift = Mathf.Sin(Mathf.Abs(PlayerGameplayInput.Instance.LookInput.x)) * (PlayerGameplayInput.Instance.LookInput.x > 0 ? 0.5f : 0.45f);
            float shift = Mathf.Abs(PlayerGameplayInput.Instance.LookInput.x / 90f) * (PlayerGameplayInput.Instance.LookInput.x > 0 ? 0.5f : 0.45f);
            SetCameraPosition(currentTargetCameraPosition+new Vector3(0, shift, 0));


            _cameraCollision.transform.localRotation = _recoilObjectReference.transform.localRotation;
            

            /*
            //killcam
            if (_objectToFollow)
            {
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(_objectToFollow.GetPositionToAttack() - transform.position), 7f * Time.deltaTime);
                transform.position = _myCharacterToFollow.transform.position + new Vector3(0, characterHeight, 0);
                return;
            }

            if (!_myCharacterToFollow) return;

            //_myCharacterToFollow.PlayerRecoil.recoilObject.position = GameplayCamera._instance.transform.position;

            //change sides od 3rd person view
            if (Input.GetKeyDown(KeyCode.LeftAlt) && ClientFrontend.GamePlayInput())
            {
                _cameraAimPosition.x *= (-1);
                cameraPosition.x *= (-1);
            }

            float shift = Mathf.Abs(PlayerGameplayInput.Instance.LookInput.x / 90f) * (PlayerGameplayInput.Instance.LookInput.x > 0 ? 0.3f : 0.1f);

            /*if (_myCharacterToFollow.IsAiming && _myCharacterToFollow.CharacterItemManager.CurrentlyUsedItem)
                _camera.SetFovMultiplier(_myCharacterToFollow.CharacterItemManager.CurrentlyUsedItem.FovScopeMultiplier);
            else
                _camera.SetFovMultiplier(1);

            cameraSticker.transform.eulerAngles = new Vector3(PlayerGameplayInput.Instance.LookInput.x, PlayerGameplayInput.Instance.LookInput.y, 0);

            transform.position = _myCharacterToFollow.transform.position + new Vector3(0, characterHeight, 0);



            transform.rotation = Quaternion.Euler(PlayerGameplayInput.Instance.LookInput.x, PlayerGameplayInput.Instance.LookInput.y, 0);

            currentTargetCameraPosition = Vector3.Lerp(currentTargetCameraPosition, _myCharacterToFollow.IsAiming ? _cameraAimPosition : cameraPosition, scopeSpeed * Time.deltaTime);

            rayCastSource.localPosition = new Vector3(currentTargetCameraPosition.x, currentTargetCameraPosition.y, 0.6f); //its for avoiding aiming character backwards
            SetCameraPosition(currentTargetCameraPosition +
                new Vector3(0, shift, 0));

            //make camera follow recoil
            _cameraCollision.transform.localRotation = _recoilObjectReference.transform.localRotation;

            _shootingRaycastSource.localPosition = new Vector3(_cameraCollision.FinalCameraLocalPos.x, _cameraCollision.FinalCameraLocalPos.y, -0.65f);
            */
        }



        public void SetThirdPersonCameraFor(CharacterInstance _characterToFollow = null)
        {
            _objectToFollow = null;

            transform.SetParent(_characterToFollow.transform);

            if (!_characterToFollow)
            {
                enabled = false;
                return;
            }
            enabled = true;
            _myCharacterToFollow = _characterToFollow;
            
            currentTargetCameraPosition = cameraPosition;
            _currentCharacterHeight = _characterHeight;

            _cameraCollision.UpdateCameraProperties(cameraPosition);
            SetCameraPosition(cameraPosition);

            GameplayCamera._instance.SetTarget(cameraSticker);

            transform.rotation = Quaternion.Euler(0, _myCharacterToFollow.transform.eulerAngles.y, 0);

            _recoilObjectReference = _myCharacterToFollow.PlayerRecoil.recoilObject;
            _camera = GameplayCamera._instance;

        }
        void SetCameraPosition(Vector3 pos)
        {
            _cameraCollision.UpdateCameraProperties(pos);
        }

        public void FollowObject(Health objectToFollow) 
        {
            _cameraCollision.UpdateCameraProperties(new Vector3(0,0,-4f));
            _objectToFollow = objectToFollow;
        }
    }
}
