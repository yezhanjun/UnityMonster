using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System;

namespace MTPSKIT.Gameplay
{
    public class CharacterItemManager : NetworkBehaviour
    {
        public List<Slot> Slots = new List<Slot>();
        public Item CurrentlyUsedItem { private set; get; }

        public int currentlyUsedSlotID {private set; get; } = -1;

        /// <summary>
        /// max item slots for character
        /// </summary>
        int interactLayerMask = (1 << 0 | 1 << 7);

        CharacterInstance _characterInstance;

        CharacterAnimator _characterAnimator;

        //for killfeed to know what item to display
        [HideInInspector] public Item LastUsedItem { private set; get; }

        Coroutine _c_usingItem;

        #region ammo managament

        public int MaxGranadeSupply = 2;

        public int GranadeSupply = 2; //granade count for client UI
        public int ServerGranadeSupply = 2; //granade count for gameplay logic executed on server

        public delegate void CharacterEvent_PickedupAmmo();
        public CharacterEvent_PickedupAmmo Client_PickedupAmmo;

        #endregion

        public delegate void CharacterEvent_EquipmentChanged(int currentlyUsedSlot);
        public CharacterEvent_EquipmentChanged Client_EquipmentChanged { get; set; }

        public bool UsePrimaryInput;
        public bool UseSecondaryInput;
        public bool AllowsSecondaryInput => CurrentlyUsedItem && !(CurrentlyUsedItem is MeleeWeapon);

        private void Awake()
        {
            _characterInstance = GetComponent<CharacterInstance>();
            _characterAnimator = GetComponent<CharacterAnimator>();
            _characterInstance.Server_OnHealthDepleted += OnHealthStateChanged;
        }

        private void OnHealthStateChanged(CharacterPart characterPart, AttackType attackType, Health killer, int attackForce)
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                Drop(i);
                RpcDropItem(i);
            }
        }

        private void Start()
        {
            //spawn starter items and assign them to player
            if (isServer)
            {
                for (int i = 0; i < Slots.Count; i++)
                {
                    Slot slot = Slots[i];
                    slot.Item = null;

                    GameObject itemToSpawn = slot.ItemOnSpawn;


                    if (itemToSpawn) 
                    {
                        Item itemGM = Instantiate(itemToSpawn, transform.position, transform.rotation).GetComponent<Item>();
                        NetworkServer.Spawn(itemGM.gameObject);
                        AttachItemToCharacter(itemGM.netIdentity, i);
                    }
                }
                ServerCommandTakeItem(0);
            }
        }

        private void Update()
        {
            if (UsePrimaryInput)
                Fire1();

            if (UseSecondaryInput)
                Fire2();
        }

        /// <summary>
        /// Fire inputs, launched by clients and bots to use items
        /// </summary>
        public void Fire1()
        {
            if (!_characterInstance.BOT && _characterInstance.IsRunning) return;
            StartUsingItem();

            //if (!_isAbleToUseItem) return;

            if (!_characterInstance.Block && _characterInstance.CurrentHealth > 0 && CurrentlyUsedItem)
            {
                _characterInstance.CharacterEvent_OnItemUsed?.Invoke(0.15f);
                CurrentlyUsedItem.PushLeftTrigger();
            }
        }
        public void Fire2()
        {
            if (!AllowsSecondaryInput) return;
            if (!_characterInstance.BOT && _characterInstance.IsRunning) return;
            StartUsingItem();

            //if (!_isAbleToUseItem) return;

            if (!_characterInstance.Block && _characterInstance.CurrentHealth > 0 && CurrentlyUsedItem)
            {
                _characterInstance.CharacterEvent_OnItemUsed?.Invoke(0.15f);
                CurrentlyUsedItem.PushRightTrigger();
            }
        }
        public void Reload() 
        {
            if (!_characterInstance.Block && _characterInstance.CurrentHealth > 0 && CurrentlyUsedItem)
                CurrentlyUsedItem.RequestReload();
        }

        public void StartUsingItem()
        {
            if (_characterInstance.IsUsingItem) return;

            if (_c_usingItem != null)
            {
                StopCoroutine(_c_usingItem);
                _c_usingItem = null;
            }

            _c_usingItem = StartCoroutine(UsingItemCoroutine());

            IEnumerator UsingItemCoroutine()
            {
                _characterInstance.IsUsingItem = true;
                yield return new WaitForSeconds(0.5f);                
                _characterInstance.IsUsingItem = false;
            }
        }

        /// <summary>
        /// item activation
        /// </summary>
        void Take(int slotID)
        {
            slotID = Mathf.Clamp(slotID, 0, Slots.Count - 1);

            if (CurrentlyUsedItem == Slots[slotID].Item && Slots[slotID].Item) return; //dont retake current item

            if (Slots[slotID].Item && !Slots[slotID].Item.CanBeUsed()) return;

            _characterInstance.IsReloading = false;

            if (CurrentlyUsedItem)
                PutDownItem(CurrentlyUsedItem);

            currentlyUsedSlotID = slotID;

            CurrentlyUsedItem = Slots[slotID].Item;
            if (!AllowsSecondaryInput)
            {
                UseSecondaryInput = false;
                _characterInstance.IsAiming = false;
            }

            if (CurrentlyUsedItem)
            {
                CurrentlyUsedItem.Take();

                _characterAnimator.SetRuntimeAnimatorController(CurrentlyUsedItem.AnimatorControllerForCharacter);
            }
            else

                _characterAnimator.SetRuntimeAnimatorController(null);

            //play equip animation
            if (_characterAnimator.enabled)
                _characterAnimator.Play("quipL");

            if(CurrentlyUsedItem != null)
                LastUsedItem = CurrentlyUsedItem;

            Client_EquipmentChanged?.Invoke(currentlyUsedSlotID);
        }



        /// <summary>
        /// Boolean for checking if we already own a weapon that we want to pick up
        /// for example: dont allow player to pick up m4 when he already owns m4
        /// </summary>
        public bool AlreadyAquired(Item item)
        {
            foreach (Slot i in Slots)
                if (i.Item && i.Item.ItemName == item.ItemName)
                    return true;
            return false;
        }

        #region equipment managament
        public void ClientTakeItem(int _slotID)
        {
            Take(_slotID);
            CmdTakeItem(_slotID);
        }

        /// <summary>
        /// If player changes item then send this info to server so everyone else will se that change
        /// </summary>
        [Command]
        void CmdTakeItem(int _slotID)
        {
            RpcClientTookItem(_slotID);
        }
        /// <summary>
        /// bunch of checks launched on client side when he want to grab weapon
        /// 
        /// check if player looks at weapoon, then if he does check if we dont have
        /// already this weapon in eq
        /// </summary>     
        public void TryGrabItem()
        {
            //don't fill slots that are not meant to be filled
            if (Slots[currentlyUsedSlotID].Type == SlotType.BuiltIn) return;

            RaycastHit hit;
            if (Physics.Raycast(GameplayCamera._instance.transform.position, GameplayCamera._instance.transform.forward, out hit, 4.5f, interactLayerMask))
            {
                GameObject go = hit.collider.gameObject;
                Item _item = go.GetComponent<Item>();
                if (_item)
                {
                    if (!AlreadyAquired(_item))
                    {
                        CmdPickUpItem(_item.netIdentity, currentlyUsedSlotID);
                    }
                }
            }
        }

        //launched by client input to drop item
        public void TryDropItem()
        {
            //dont drop items that are not meant to be dropped
            if (Slots[currentlyUsedSlotID].Type == SlotType.BuiltIn) return;

            CmdDropItem(currentlyUsedSlotID);
        }

        /// <summary>
        /// server processor for client request to pickup item
        /// </summary>
        [Command]
        void CmdPickUpItem(NetworkIdentity _itemNetIdentity, int _slotID)
        {
            //do not let dead character pickup weapons
            if (_characterInstance.CurrentHealth <= 0) return;

            AttachItemToCharacter(_itemNetIdentity, _slotID);
        }
        // GM uses the same assignment/drop RPC chain as normal pickups.
        [Server]
        public bool ServerEquipTestWeapon(GameObject prefab)
        {
            if(!prefab||!prefab.GetComponent<Item>()||_characterInstance.CurrentHealth<=0)return false;
            var source=prefab.GetComponent<Item>();if(!source)return false;
            int slot=-1;
            for(int i=0;i<Slots.Count;i++)
            {
                if(Slots[i].Item&&Slots[i].Item.ItemName==source.ItemName){ServerCommandTakeItem(i);return true;}
                if(Slots[i].Type!=SlotType.Normal&&!Slots[i].Item&&Slots[i].ItemOnSpawn&&Slots[i].ItemOnSpawn.GetComponent<Item>().ItemName==source.ItemName)slot=i;
            }
            for(int i=0;i<Slots.Count;i++)
            {
                if(slot>=0&&Slots[slot].Type!=SlotType.Normal)break;
                if(Slots[i].Type!=SlotType.Normal)continue;
                if(Slots[i].Item&&Slots[i].Item.ItemName==source.ItemName){ServerCommandTakeItem(i);return true;}
                if(slot<0||!Slots[i].Item)slot=i;
            }
            if(slot<0)return false;
            var spawned=Instantiate(prefab,transform.position,transform.rotation);
            NetworkServer.Spawn(spawned);AttachItemToCharacter(spawned.GetComponent<NetworkIdentity>(),slot);return true;
        }
        [Server]
        public bool ServerRemoveTestWeapon(GameObject prefab)
        {
            var source=prefab?prefab.GetComponent<Item>():null;if(!source)return false;
            for(int i=0;i<Slots.Count;i++)
            {
                var item=Slots[i].Item;
                if(!item||item.ItemName!=source.ItemName)continue;
                int previous=currentlyUsedSlotID;
                // Keep the dropped network item alive until its ownership update is delivered.
                Drop(i);RpcDropItem(i);
                if(previous!=i&&previous>=0&&previous<Slots.Count&&Slots[previous].Item)ServerCommandTakeItem(previous);
                return true;
            }
            return false;
        }
        void AttachItemToCharacter(NetworkIdentity _itemNetIdentity, int _slotID)
        {
            Item _item = _itemNetIdentity.GetComponent<Item>();

            if (AlreadyAquired(_item))
            {
                print("ALREADY AQUIRED: " + _item.name);
                return; //if player tries to equip same item twice than dont allow that
            }

            if (!Slots[_slotID].Item) //prefer to add item to current slot, if its empty
            {
                AttachItemToSlot(_slotID);
                return;
            }

            //searching for free slot when player have item on currently used slot
            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i].Type == SlotType.Normal && !Slots[i].Item)
                {
                    AttachItemToSlot(i);
                    return;
                }
            }

            //replace item in currently used slot when eq if full
            Drop(_slotID);
            RpcDropItem(_slotID);

            AttachItemToSlot(_slotID);

            void AttachItemToSlot(int __slotID)
            {
                //for server
                AssignItem(__slotID, _item.netIdentity);
                Take(__slotID);
                //for clients
                RpcAssignItem(__slotID, _item.netIdentity);
                RpcTakeItem(__slotID);
            }
        }
        [ClientRpc]
        void RpcAssignItem(int slotID, NetworkIdentity itemToAssign)
        {
            if (isServer) return;
            AssignItem(slotID, itemToAssign);
        }

        /// <summary>
        /// assign item to character equipment
        /// </summary>
        void AssignItem(int slotID, NetworkIdentity IDitemToAssign)
        {

            if (isServer && netIdentity.connectionToClient != null) IDitemToAssign.AssignClientAuthority(netIdentity.connectionToClient);

            Item itemToAssign = IDitemToAssign.GetComponent<Item>();

            Slots[slotID].Item = itemToAssign;
            itemToAssign.AssignToCharacter(_characterInstance);

            PutDownItem(itemToAssign);
        }


        [ClientRpc]
        void RpcTakeItem(int slotID)
        {
            if (!isServer)
                Take(slotID);
        }
        [ClientRpc(includeOwner = false)]
        void RpcClientTookItem(int slotID)
        {
            Take(slotID);
        }

        public void ServerCommandTakeItem(int slotID) 
        {
            Take(slotID);
            RpcClientTakeItemIncludeOwner(slotID);
        }
        [ClientRpc]
        void RpcClientTakeItemIncludeOwner(int slotID)
        {
            if (isServer) return;
                Take(slotID);
        }
        //drop
        [Command]
        void CmdDropItem(int slotIDtoDrop)
        {
            Drop(slotIDtoDrop);
            RpcDropItem(slotIDtoDrop);
        }
        [ClientRpc]
        void RpcDropItem(int slotIDtoDrop)
        {
            if (!isServer) Drop(slotIDtoDrop);
        }

        /// <summary>
        /// detach item from character and drop it
        /// </summary>
        void Drop(int slotIDtoDrop)
        {
            Slot slotToEmpty = Slots[slotIDtoDrop];

            if (slotToEmpty.Item)
            {
                Item itemToDrop = slotToEmpty.Item;
                if (itemToDrop)
                {
                    if (slotIDtoDrop == currentlyUsedSlotID) //if we want to drop item that is in use then we need to put it down first
                    {
                        PutDownItem(itemToDrop);
                    }
                    slotToEmpty.Item = null;

                    if (isServer)
                    {
                        itemToDrop.netIdentity.RemoveClientAuthority();
                    }

                    itemToDrop.transform.position = transform.position + transform.rotation * new Vector3(-0.3f+slotIDtoDrop*0.2f, 1.5f, 0.5f);
                    itemToDrop.transform.rotation = transform.rotation;

                    itemToDrop.Drop();

                    if (isServer)
                        itemToDrop.GetComponent<Rigidbody>().AddForce(transform.forward * 150f); //push weapon forward on drop

                    CurrentlyUsedItem = null;
                    Take(slotIDtoDrop);
                }
            }
        }

        /// <summary>
        /// when changing item, put down old item before taking new one
        /// </summary>
        void PutDownItem(Item _itemToPutdown)
        {
            if (_itemToPutdown)
                _itemToPutdown.PutDown();
        }
        #endregion

        /// <summary>
        /// if we want to destroy character, destroy also character equipment
        /// </summary>
        public void OnDespawnCharacter()
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i].Item)
                    NetworkServer.Destroy(Slots[i].Item.gameObject);
            }
        }

        #region UpdateLatePlayer

        /// <summary>
        /// if new player joins the game, and others are already spawned on the map, then his game should know equipment of those other players
        /// who were already playing on the server, and these methods do exactly that, they will tell new client wchich items do those players have
        /// </summary>
        void UpdateForLatePlayer(NetworkConnection conn)
        {
            List<NetworkIdentity> itemIdenties = new List<NetworkIdentity>();
            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i].Item)
                {
                    itemIdenties.Add(Slots[i].Item.GetComponent<NetworkIdentity>());
                }
                else
                {
                    itemIdenties.Add(null);
                }
            }
            TargetRpcUpdateForLatePlayer(conn, itemIdenties, currentlyUsedSlotID);
        }
        [TargetRpc]
        void TargetRpcUpdateForLatePlayer(NetworkConnection conn, List<NetworkIdentity> itemsID, int currentlyUsedSlot)
        {
            for (int i = 0; i < itemsID.Count; i++)
            {
                if (itemsID[i])
                {
                    AssignItem(i, itemsID[i]);
                }
            }
            Take(currentlyUsedSlot);
        }
        #endregion

        /// <summary>
        /// sub to NetworkManager event which will notify this object about new players joining the game, so
        /// we will be able to send them info about our equipment
        /// </summary>
        private void OnEnable()
        {
            DNNetworkManager.Instance.OnNewPlayerConnected += UpdateForLatePlayer;
        }
        private void OnDisable()
        {
            DNNetworkManager.Instance.OnNewPlayerConnected -= UpdateForLatePlayer;
        }

        public void AddGranadeNumber(int grenadeNumber) 
        {
            ServerGranadeSupply += grenadeNumber;

            if (ServerGranadeSupply > MaxGranadeSupply)
                ServerGranadeSupply = MaxGranadeSupply;

            RpcUpdateGranadeNumber(ServerGranadeSupply);
        }
        [ClientRpc]
        void RpcUpdateGranadeNumber(int grenadeNumber) 
        {
            GranadeSupply = grenadeNumber;

            Client_PickedupAmmo?.Invoke();
        }

        //unity editor only, inspector value validation
        protected override void OnValidate()
        {
            base.OnValidate();
            for (int i = 0; i < Slots.Count; i++) 
            {
                if (Slots[i].ItemOnSpawn)
                {
                    Item itemCheck = Slots[i].ItemOnSpawn.GetComponent<Item>();

                    if (!itemCheck)
                    {
                        Debug.LogError("MTPSKIT WARNING: Item that is meant to be used by player must have Item component attached to it!");
                        Slots[i].ItemOnSpawn = null;
                    }
                }
            }
        }
        
    }
    [System.Serializable]
    public class Slot 
    {
        //slot type, determines if item can be dropped or replaced by another, or not
        //Normal: item can be dropped/replaced by another
        //BuildIn: item will stay in this slot forever
        //You can customize slots hovever you like in the inspector
        public SlotType Type;

        //actual gameplay item, dont drag anything here in the inspector, game will fill that on runtime.
        //it does not need to be visible in the inspector, but we kept it visible so You can see what is going on real time
        //You can hide it if you like by uncommenting "[HideInInspector]" attribute below

        /*[HideInInspector]*/
        public Item Item;

        //If you wish player character to have certain default item for certain slot
        //then drag and drop here that item prefab from project files
        public GameObject ItemOnSpawn;

        //key that is used on keyboard to acces this item, this field is only used for ui
        public string Key = "0";
    }
    public enum SlotType
    {
        Normal, //=> item can be dropped/replaced by another
        BuiltIn, //=>  item will stay in this slot forever
    }
}
