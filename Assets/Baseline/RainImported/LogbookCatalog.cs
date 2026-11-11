using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MTPSKIT.RainImported
{
    [Serializable] internal sealed class JournalTier { public string key,name; public int tier; public bool isDroppable; }
    [Serializable] internal sealed class PickupRow {
        public string id,family,catalogName,nameToken,name,description,tierRef,unlock,expansion,icon,source,pack;
        public string pickupToken,descriptionToken,loreToken,iconGUID; public int[] tags;
        public float cooldown; public int tier; public bool canRemove,canDrop,hidden,isLunar;
    }
    [Serializable] internal sealed class DefinitionRef {public string key,name;}
    [Serializable] internal sealed class OtherRow {public string kind,key,name,info;}
    [Serializable] internal sealed class LogbookTables {
        public int version;
        public string[] categories,enabledPacks,enabledExpansions,referenceOnlyLocks,initialDiscovered,initialUnlocked;
        public DefinitionRef[] expansions,unlockables;
        public JournalTier[] tiers; public PickupRow[] items,equipment,keyItems; public OtherRow[] extras;
    }
    internal enum Discovery {Locked, Unencountered, Available}
    internal sealed class LogbookCatalog {
        public readonly LogbookTables Table;
        public readonly PickupRow[] Entries;
        readonly HashSet<string> discovered,unlocked;
        readonly Dictionary<string,Texture2D> icons=new Dictionary<string,Texture2D>();
        public LogbookCatalog(){
            Table=JsonUtility.FromJson<LogbookTables>(Resources.Load<TextAsset>("RainImported/LogbookTables").text);
            if(Table.version!=1)throw new InvalidOperationException("日志配表版本不匹配");
            discovered=new HashSet<string>(Table.initialDiscovered);
            unlocked=new HashSet<string>(Table.initialUnlocked);
            var packs=new HashSet<string>(Table.enabledPacks);
            var byRef=Table.tiers.ToDictionary(r=>r.key);
            var droppable=new HashSet<int>(Table.tiers.Where(r=>r.isDroppable).Select(r=>r.tier));
            var eligible=Table.items.Where(Enabled).Where(r=>{
                if(!string.IsNullOrEmpty(r.tierRef) && byRef.TryGetValue(r.tierRef,out var t))r.tier=t.tier;
                return droppable.Contains(r.tier);
            }).OrderBy(r=>r.tier==3?103:r.tier).ThenBy(r=>r.catalogName,StringComparer.Ordinal).ToArray();
            Entries=eligible.Where(r=>r.tier!=12)
                .Concat(Table.equipment.Where(r=>Enabled(r)&&r.canDrop).OrderBy(r=>!r.isLunar).ThenBy(r=>r.catalogName,StringComparer.Ordinal))
                .Concat(Table.keyItems.Where(r=>Enabled(r)&&!r.hidden).OrderBy(r=>r.catalogName=="CursedBook").ThenBy(r=>r.catalogName,StringComparer.Ordinal))
                .Concat(eligible.Where(r=>r.tier==12)).ToArray();
            bool Enabled(PickupRow row)=>packs.Contains(row.pack) && (string.IsNullOrEmpty(row.expansion)||Array.IndexOf(Table.enabledExpansions??Array.Empty<string>(),row.expansion)>=0);
        }
        public Discovery Status(PickupRow row){
            if(!string.IsNullOrEmpty(row.unlock)&&!unlocked.Contains(row.unlock)&&!Saved("unlock",row.unlock))return Discovery.Locked;
            if(discovered.Contains(row.id)||Saved("seen",row.id))return Discovery.Available;
            if(Array.IndexOf(Table.referenceOnlyLocks,row.id)>=0)return Discovery.Locked;
            return Discovery.Unencountered;
        }
        static bool Saved(string group,string id){
#if RAIN_UI_CHECK
            return false;
#else
            return PlayerPrefs.GetInt("RainLog.v1."+group+"."+id,0)!=0;
#endif
        }
        public void Discover(string id){
            discovered.Add(id);
#if !RAIN_UI_CHECK
            PlayerPrefs.SetInt("RainLog.v1.seen."+id,1);PlayerPrefs.Save();
#endif
        }
        public Texture2D Icon(string name){
            if(string.IsNullOrEmpty(name))return null;
            if(!icons.TryGetValue(name,out var t))icons[name]=t=Resources.Load<Texture2D>("RainImported/LogIcons/"+name);
            return t;
        }
        public static string Background(PickupRow r){
            if(r.family=="equipment")return r.isLunar?"texLunarBGIcon":"texEquipmentBGIcon";
            switch(r.tier){case 0:return "texTier1BGIcon";case 1:return "texTier2BGIcon";case 2:return "texTier3BGIcon";case 3:return "texLunarBGIcon";case 4:return "texBossBGIcon";default:return null;}
        }
    }
}




