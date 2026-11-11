using System.Collections;
using UnityEngine;
using MTPSKIT.UI;

namespace MTPSKIT.Gameplay {
[DefaultExecutionOrder(20000)]
public sealed class FpsViewController : MonoBehaviour {
 public static FpsViewController Instance { get; private set; }
 public bool FirstPerson { get; private set; }
 public bool Ready => ready;
 CharacterInstance character; Item item; GameObject view; FpsGunBinding binding;
 Transform throwHand;
 Animator grenadeAnimator;
 AaaViewAnimations animations;
 float aimWeight;
 Renderer[] weaponRenderers;
 UnityEngine.Rendering.ShadowCastingMode[] weaponShadows;
 SkinnedMeshRenderer[] bodyRenderers, legs;
 UnityEngine.Rendering.ShadowCastingMode[] shadowModes;
 Camera cameraComponent; float originalNear; bool originalSnap, ready; Coroutine initialize;
 void Awake() { Instance=this; cameraComponent=GetComponent<Camera>(); originalNear=cameraComponent.nearClipPlane; }
 void Update() {
  var current=GameManager.myPlayerInstance ? GameManager.myPlayerInstance.MyCharacter : null;
  if(current!=character) { ExitView(); character=current; }
  if(!character || character.CurrentHealth<=0) { ExitView(); return; }
  if(GodOfWarHeroCamera.HandlesPlayer) { if(FirstPerson)ExitView(); return; }
  if(!GodOfWarHeroCamera.HandlesPlayer && ClientFrontend.GamePlayInput() && Input.GetKeyDown(GameManager.myPlayerInstance && GameManager.myPlayerInstance.HasHeroLoadout ? KeyCode.B : KeyCode.V)) ToggleView();
  if(!FirstPerson) return;
  var currentItem=character.CharacterItemManager.CurrentlyUsedItem;
  if(currentItem!=item) ChangeItem(currentItem);

  aimWeight = Mathf.MoveTowards(aimWeight, character.IsAiming && !character.IsRunning ? 1 : 0, Time.deltaTime * 5f);
  if (animations && binding) {
   Vector3 velocity = binding.OwnerVelocity;
   velocity.y = 0f;
   animations.UpdateMovement(velocity.magnitude, character.IsGrounded, character.IsRunning, aimWeight);
  }
 }
 public void ToggleView() {
   if(!character || character.CurrentHealth<=0) return;
   if(FirstPerson) ExitView(); else {
    FirstPerson=true; originalSnap=PlayerGameplayInput.Instance.AlwaysSnapCharacterToCamera;
    PlayerGameplayInput.Instance.AlwaysSnapCharacterToCamera=true;
    ShowFirstPersonBody();
    cameraComponent.nearClipPlane=.03f;
   }
 }
 void LateUpdate() {
  if(!FirstPerson || !character || !PlayerGameplayInput.Instance) return;
  var look=PlayerGameplayInput.Instance.LookInput;
  Vector3 eye = character.transform.position + Vector3.up * (character.IsCrouching ? 1.25f : 1.75f);
  Vector3 forward = Quaternion.Euler(0f, look.y, 0f) * Vector3.forward;
  // Looking down brings the eyes in front of the hips as the spine flexes.
  float reach = Mathf.Lerp(0.08f, 0.52f, Mathf.Clamp01(look.x / 75f));
  if (Physics.SphereCast(eye, 0.07f, forward, out RaycastHit obstruction, reach, GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
   reach = Mathf.Max(0f, obstruction.distance - 0.01f);
  transform.SetPositionAndRotation(eye + forward * reach, Quaternion.Euler(look.x, look.y, 0f) * (binding ? Quaternion.Euler(binding.CameraKick) : Quaternion.identity));
  if(view && binding) { var rotation = transform.rotation * Quaternion.Euler(binding.RotationKick + animations.MotionRotation); view.transform.SetPositionAndRotation(transform.position + transform.rotation * (binding.PositionKick + animations.MotionPosition + animations.aimOffset * aimWeight), rotation); }
 }
 void ChangeItem(Item next) {
  ClearWeapon(); item=next; if(!item) return;
  if (item is Granade) {
   weaponRenderers = item.ItemModel.GetComponentsInChildren<Renderer>(true);
   weaponShadows = KeepShadows(weaponRenderers);
   view = Instantiate(Resources.Load<GameObject>("AaaGrenadeView"), transform, false);
   DisableViewShadows(view);
   grenadeAnimator = view.GetComponentInChildren<Animator>();
   foreach (Transform bone in grenadeAnimator.GetComponentsInChildren<Transform>())
    if (bone.name == "hand_L.R") { throwHand = bone; break; }
   return;
  }
  binding=item.GetComponent<FpsGunBinding>(); if(!binding || !binding.viewPrefab) return;
  weaponRenderers=item.ItemModel.GetComponentsInChildren<Renderer>(true); weaponShadows=KeepShadows(weaponRenderers);
  view = Instantiate(binding.viewPrefab);
  DisableViewShadows(view);
  animations = view.GetComponent<AaaViewAnimations>();
  initialize = StartCoroutine(InitializeView());
 }
 IEnumerator InitializeView() {
  yield return null;
  ready = animations.Initialize();
  initialize = null;
  if (ready && character.IsReloading) animations.Reload(character.IsAiming);
 }
 public void Shot(CharacterInstance owner, Item source) {
  if (ready && owner == character && source == item) animations.Fire();
 }
 public void Reload(CharacterInstance owner, Item source) {
  if (ready && owner == character && source == item) animations.Reload(owner.IsAiming);
 }
 public void FinishReload(CharacterInstance owner, Item source) {
  if (ready && owner == character && source == item) animations.FinishReload(owner.IsAiming);
 }
 void ShowFirstPersonBody() {
  bodyRenderers = character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
  shadowModes = new UnityEngine.Rendering.ShadowCastingMode[bodyRenderers.Length];
  legs = new SkinnedMeshRenderer[bodyRenderers.Length];
  for (int i = 0; i < bodyRenderers.Length; i++) {
   var skin = bodyRenderers[i];
   shadowModes[i] = skin.shadowCastingMode;
   if (!skin.enabled || skin.forceRenderingOff || !skin.sharedMesh) continue;
   skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
   var mesh = Resources.Load<Mesh>("FpsLegs/" + skin.sharedMesh.name);
   if (!mesh) continue;
   var part = new GameObject("First person legs");
   part.layer = skin.gameObject.layer;
   part.transform.SetParent(skin.transform, false);
   var renderer = part.AddComponent<SkinnedMeshRenderer>();
   legs[i] = renderer;
   renderer.sharedMesh = mesh;
   renderer.sharedMaterials = skin.sharedMaterials;
   renderer.bones = skin.bones;
   renderer.rootBone = skin.rootBone;
   renderer.localBounds = skin.localBounds;
   renderer.updateWhenOffscreen = true;
   renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
  }
 }
 void RestoreBody() {
  if (bodyRenderers == null) return;
  for (int i = 0; i < bodyRenderers.Length; i++) {
   if (bodyRenderers[i]) bodyRenderers[i].shadowCastingMode = shadowModes[i];
   if (legs[i]) { legs[i].enabled = false; Destroy(legs[i].gameObject); }
  }
  bodyRenderers = null; legs = null; shadowModes = null;
 }
 static void DisableViewShadows(GameObject viewModel) {
  foreach (var renderer in viewModel.GetComponentsInChildren<Renderer>(true))
   renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
 }
 static UnityEngine.Rendering.ShadowCastingMode[] KeepShadows(Renderer[] renderers) {
  var previous = new UnityEngine.Rendering.ShadowCastingMode[renderers.Length];
  for (int i = 0; i < renderers.Length; i++) {
   previous[i] = renderers[i].shadowCastingMode;
   renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
  }
  return previous;
 }
 public bool TryGetThrowHand(Granade source, out Transform hand) {
  hand = throwHand;
  return FirstPerson && item == source && hand;
 }
 public void ShowGrenadeAction(Granade source, string state) {
  if (source == item && grenadeAnimator) grenadeAnimator.CrossFadeInFixedTime(state, 0.03f, 0);
 }
 void ClearWeapon() {
  throwHand = null;
  grenadeAnimator = null;
  if(initialize!=null)StopCoroutine(initialize); initialize=null; ready=false;
  if (weaponRenderers != null)
   for (int i = 0; i < weaponRenderers.Length; i++)
    if (weaponRenderers[i]) weaponRenderers[i].shadowCastingMode = weaponShadows[i];
  weaponRenderers=null;weaponShadows=null;
  animations=null; aimWeight=0;
  if(view)Destroy(view);view=null;item=null;binding=null;
 }
 void ExitView() {
  if(FirstPerson && PlayerGameplayInput.Instance)PlayerGameplayInput.Instance.AlwaysSnapCharacterToCamera=originalSnap;
  FirstPerson=false;ClearWeapon();RestoreBody();
  if(cameraComponent)cameraComponent.nearClipPlane=originalNear;
 }
 void OnDisable()=>ExitView();
 void OnDestroy(){ExitView();if(Instance==this)Instance=null;}
}}
