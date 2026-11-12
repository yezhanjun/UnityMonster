using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace MTPSKIT.RainImported
{
    [Serializable] internal sealed class DroneRunStat {
        public string id,name,state; public float damage,hp; public int kills,commands,repairs;
    }
    [Serializable] internal sealed class RunLedger
    {
        public string id,utc,role,difficulty,outcome,killer;
        public int kills,minionKills,deaths,damage,minionDamage,peakHit,received,level=1,gold,items,stages,purchases,xp,score;
        public float seconds;
        public List<DroneRunStat> drones=new List<DroneRunStat>();
        public List<string> inventory=new List<string>(), unlocks=new List<string>();
        public static long Experience(int lv)=>(long)(20*((1-Math.Pow(1.55,lv-1))/(1-1.55)));
                public static string Folder {
            get {
#if RAIN_UI_CHECK
                var qa=Environment.GetEnvironmentVariable("RAIN_SCREEN_CHECK");
                if(!string.IsNullOrEmpty(qa))return Path.Combine(qa,"Reports");
#endif
                return Path.Combine(Application.persistentDataPath,"Reports");
            }
        }
        public int[] Points()=>new[]{(int)seconds,kills*10,minionKills*10,0,damage/100,minionDamage/100,peakHit,0,level*100,gold,items*110,stages*100,purchases*35};
        public string Save(){
            score=0;foreach(int n in Points())score+=n;
            Directory.CreateDirectory(Folder);
            string path=Path.Combine(Folder,id+".json");
            if(!File.Exists(path)){
                var temp=path+".tmp";
                File.WriteAllText(temp,JsonUtility.ToJson(this,true));
                File.Move(temp,path);
            }
            return path;
        }
        public static RunLedger Last(){
            if(!Directory.Exists(Folder))return null;
            var files=Directory.GetFiles(Folder,"*.json");Array.Sort(files,StringComparer.Ordinal);
            return files.Length==0?null:JsonUtility.FromJson<RunLedger>(File.ReadAllText(files[files.Length-1]));
        }
    }
    [Serializable] internal sealed class JournalEntry{
        public string key,group,name,summary,description,stats,source,asset;
    }
    [Serializable] internal sealed class RainJournal{public JournalEntry[] entries;}
}



