using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MTPSKIT.UI;
namespace MTPSKIT.Gameplay
{
    public class PlayerGameplayInput : MonoBehaviour
    {
        public static PlayerGameplayInput Instance;
        private CharacterInstance _myCharIntance;
        private CharacterMotor _motor;

        public Vector2 LookInput;
        public Vector2 MovementInput;

        public float CharacterRotationSpeed = 5f;

        float _targetCharacterRot;

        public float IdleLookAngle = -20f;

        public float LerpLookSpeed = 10f;

        float _itemUseTimer;

        public bool AlwaysSnapCharacterToCamera;


        private void Awake()
        {
            if (Instance)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }
        void Update() {
            if (!_myCharIntance) return;

            //game managament related input
            /*if (Input.GetKeyDown(KeyCode.L))
            {
                ClientFrontend.GamemodeUI.Btn_ShowTeamSelector();
            }*/

            if (ClientFrontend.GamePlayInput())
            {
                //character related input

                var heroPlayer = GameManager.myPlayerInstance;
                bool heroKeys = heroPlayer && heroPlayer.HasHeroLoadout;
                if (heroKeys && Input.GetKeyDown(KeyCode.N)) _motor.RequestControlModeToggle();
                if (heroKeys && !_motor.IsFourWayRolling)
                {
                    if (Input.GetKeyDown(KeyCode.Q)) heroPlayer.CmdUseHeroSkill(0);
                    if (Input.GetKeyDown(KeyCode.E)) heroPlayer.CmdUseHeroSkill(1);
                    if (Input.GetKeyDown(KeyCode.V)) heroPlayer.CmdUseHeroSkill(2);
                }
                if (Input.GetKeyDown(heroKeys ? KeyCode.F : KeyCode.E))
                {
                    var player = GameManager.myPlayerInstance;
                    if (!player || (!player.TryOpenRainScrapper() && !player.TryInteractRainNode()))
                        _myCharIntance.CharacterItemManager.TryGrabItem();
                    // A local host can receive the menu RPC synchronously during this Update.
                    if (!enabled) return;
                }
                if (Input.GetKeyDown(KeyCode.G)) _myCharIntance.CharacterItemManager.TryDropItem();
                if (Input.GetKeyDown(KeyCode.F10) && GameManager.myPlayerInstance)
                    GameManager.myPlayerInstance.RequestUseRangeEquipment();

                if (Input.GetKeyDown(KeyCode.Alpha1)) _myCharIntance.CharacterItemManager.ClientTakeItem(0);
                if (Input.GetKeyDown(KeyCode.Alpha2)) _myCharIntance.CharacterItemManager.ClientTakeItem(1);
                if (Input.GetKeyDown(KeyCode.Alpha3)) _myCharIntance.CharacterItemManager.ClientTakeItem(2);
                if (Input.GetKeyDown(KeyCode.Alpha4)) _myCharIntance.CharacterItemManager.ClientTakeItem(3);
                if (Input.GetKeyDown(KeyCode.Alpha5)) _myCharIntance.CharacterItemManager.ClientTakeItem(4);

                MovementInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                if (Input.GetKeyDown(KeyCode.Space)) _motor.Jump();
                _myCharIntance.SetActionKeyCode(ActionCodes.Crouch, Input.GetKey(KeyCode.C));
                bool bladeWalk = _myCharIntance.CharacterItemManager.CurrentlyUsedItem is MeleeWeapon blade &&
                    blade.FullBodyLocomotion && Input.GetMouseButton(1);
                _myCharIntance.SetActionKeyCode(ActionCodes.BladeWalk, bladeWalk);
                bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                bool rollMode = _motor.UsesFourWayRoll;
                if (rollMode && (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)))
                {
                    bool gunLocked = _myCharIntance.CharacterItemManager.CurrentlyUsedItem is Gun &&
                        (Input.GetMouseButton(0) || Input.GetMouseButton(1) || _itemUseTimer >= Time.time);
                    _motor.RequestMovementRoll(MovementInput, LookInput.y, gunLocked);
                }
                bool rolling = _motor.IsFourWayRolling;
                bool sprint = !rollMode && !bladeWalk && shiftHeld && _motor.CanSprint(MovementInput);
                _myCharIntance.SetActionKeyCode(ActionCodes.Sprint, sprint);

                var items = _myCharIntance.CharacterItemManager;
                items.UsePrimaryInput = (!rolling || items.CurrentlyUsedItem is Gun) && !sprint && Input.GetMouseButton(0);
                items.UseSecondaryInput = (!rolling || items.CurrentlyUsedItem is Gun) && !sprint && items.AllowsSecondaryInput && Input.GetMouseButton(1);
                _myCharIntance.IsAiming = items.UseSecondaryInput;
                if (!rolling && !sprint && Input.GetKeyDown(KeyCode.R)) items.Reload();

                float scopeMultiplier = _myCharIntance.IsAiming && _myCharIntance.CharacterItemManager.CurrentlyUsedItem ? _myCharIntance.CharacterItemManager.CurrentlyUsedItem.AimSensitivityMultiplier : 1f;


                LookInput.y += Input.GetAxis("Mouse X") * UserSettings.MouseSensitivity * scopeMultiplier;
                LookInput.x -= Input.GetAxis("Mouse Y") * UserSettings.MouseSensitivity * scopeMultiplier;
                LookInput.x = GodOfWarHeroCamera.HandlesPlayer ? Mathf.Clamp(LookInput.x, -40f, 80f) : Mathf.Clamp(LookInput.x, -90f, 90f);
                
                ApplyLocomotion(heroKeys, Input.GetMouseButton(0) || items.UseSecondaryInput || bladeWalk);
            }
            else
            {
                _myCharIntance.SetMovementInput(Vector2.zero);
                _myCharIntance.CharacterItemManager.UsePrimaryInput = false;
                _myCharIntance.CharacterItemManager.UseSecondaryInput = false;
                _myCharIntance.IsAiming = false;
                _myCharIntance.SetActionKeyCode(ActionCodes.Sprint, false);
                _myCharIntance.SetActionKeyCode(ActionCodes.BladeWalk, false);
                _myCharIntance.SetActionKeyCode(ActionCodes.LockedLocomotion, false);
            }
        }

        internal void ApplyLocomotion(bool heroKeys, bool usingItem)
        {
                if (heroKeys && _motor.LocksAirFacing)
                {
                    _myCharIntance.lookInput.y = _motor.AirFacing;
                    var airMove = Quaternion.Euler(0, LookInput.y - _motor.AirFacing, 0) *
                        new Vector3(MovementInput.x, 0, MovementInput.y);
                    _myCharIntance.SetMovementInput(airMove.x, airMove.z);
                    return;
                }
                bool locked = (heroKeys ? _motor.GodWarControl : AlwaysSnapCharacterToCamera) ||
                    _itemUseTimer >= Time.time || usingItem;
                _myCharIntance.SetActionKeyCode(ActionCodes.LockedLocomotion, locked);
                if (heroKeys ? _motor.GodWarControl : AlwaysSnapCharacterToCamera)
                {
                    _myCharIntance.SetMovementInput(MovementInput);
                    _myCharIntance.lookInput = LookInput;
                    return;
                }

                //if player wishes to use item or item is in use snap character to camera view
                if (locked)
                {
                    _targetCharacterRot = LookInput.y;

                    _myCharIntance.lookInput.x = Mathf.MoveTowards(_myCharIntance.lookInput.x, LookInput.x, LerpLookSpeed * Time.deltaTime);
                }
                else
                {
                    Vector3 playerInput = new Vector3(MovementInput.x, 0, MovementInput.y);
                    Vector3 direction = Quaternion.Euler(0, LookInput.y, 0) * playerInput;

                    if (MovementInput != Vector2.zero)
                        _targetCharacterRot = Quaternion.LookRotation(direction, Vector3.up).eulerAngles.y;

                    _myCharIntance.lookInput.x = Mathf.MoveTowards(_myCharIntance.lookInput.x, -IdleLookAngle, LerpLookSpeed*Time.deltaTime);    
                }


                //rotate player
                _myCharIntance.lookInput.y = heroKeys && _motor.ApplyHeroSprintFacing(_targetCharacterRot, MovementInput.sqrMagnitude > .01f, locked, out float sprintYaw)
                    ? sprintYaw : heroKeys
                    ? Mathf.LerpAngle(_myCharIntance.lookInput.y, _targetCharacterRot, 1f - Mathf.Exp(-12f * Time.deltaTime))
                    : Mathf.MoveTowardsAngle(_myCharIntance.lookInput.y, _targetCharacterRot, CharacterRotationSpeed * Time.deltaTime);


                Quaternion camRot = Quaternion.Euler(0, LookInput.y - _myCharIntance.lookInput.y, 0);
                Vector3 m = camRot * new Vector3(MovementInput.x, 0, MovementInput.y);

                _myCharIntance.SetMovementInput(m.x, m.z);
        }

        public void AssignCharacterToBeControlledByPlayer(CharacterInstance character)
        {
            _myCharIntance = character;
            _motor = character.GetComponent<CharacterMotor>();

            _myCharIntance.CharacterEvent_OnItemUsed += OnItemUsed;
        }

        void OnItemUsed(float time)
        {
            _itemUseTimer = Time.time + time;
        }
        private void OnDestroy()
        {
            if(_myCharIntance)
                _myCharIntance.CharacterEvent_OnItemUsed -= OnItemUsed;
        }
    }
}
