using UnityEngine;
namespace MTPSKIT.Gameplay
{
    public sealed class RangeDroneStatus : MonoBehaviour
    {
        float controlUntil,shieldUntil;
        int shield;
        CharacterInstance character;
        bool blockedByUs;
        static RangeDroneStatus Get(Health health){var status=health.GetComponent<RangeDroneStatus>();return status?status:health.gameObject.AddComponent<RangeDroneStatus>();}
        public static void Control(Health health,float seconds){if(!health)return;var s=Get(health);s.controlUntil=Mathf.Max(s.controlUntil,Time.time+seconds);}
        public static void Shield(Health health,float amount){var s=Get(health);s.shield=Mathf.Min(health.MaxHealth,s.shield+Mathf.CeilToInt(amount));s.shieldUntil=Time.time+6;}
        public int Absorb(int amount){if(Time.time>=shieldUntil)shield=0;int absorbed=Mathf.Min(shield,amount);shield-=absorbed;return amount-absorbed;}
        void Awake(){character=GetComponent<CharacterInstance>();}
        void Update()
        {
            if(!character||!character.isServer)return;
            if(Time.time<controlUntil&&!character.Block){character.BlockCharacter(true);blockedByUs=true;}
            else if(Time.time>=controlUntil&&blockedByUs){character.BlockCharacter(false);blockedByUs=false;}
        }
        void OnDestroy(){if(blockedByUs&&character&&character.isServer)character.BlockCharacter(false);}
    }
}
