using System;
using UnityEngine;
namespace MTPSKIT.RainImported {
    [Serializable] internal sealed class HitProcRule{
        public string item;public float chance,damageFactor,childCoefficient;
    }
    [Serializable] internal sealed class ProcTable{public HitProcRule missile,lightning;}
    internal sealed partial class WeaponBuild{
        // Child impacts inherit ancestry; a missile may start a lightning chain, but not another missile.
        [Flags] enum Ancestor{None=0,Missile=1,Lightning=2}
        readonly ProcTable procRules=JsonUtility.FromJson<ProcTable>(Resources.Load<TextAsset>("RainImported/WeaponProcs").text);
        internal int missileCount,lightningCount;
        void ProcImpact(Target target,float damage,float coefficient,Ancestor ancestors){
            if(target==null || coefficient<=0)return;
            var m=procRules.missile;
            int missiles=N(m.item);
            if(missiles>0&&(ancestors&Ancestor.Missile)==0&&Roll(m.chance*coefficient)){
                var next=target.Alive?target:Near(target.x);
                if(next!=null){
                    float value=Mathf.Max(1,damage*m.damageFactor*missiles);
                    missileCount++;Deal(next,value);last="导弹";ProcImpact(next,value,m.childCoefficient,ancestors|Ancestor.Missile);
                }
            }
            var l=procRules.lightning;
            int stacks=N(l.item);
            if(stacks==0||(ancestors&Ancestor.Lightning)!=0||!Roll(l.chance*coefficient))return;
            int visited=1<<targets.IndexOf(target);var current=target;
            // Initial enemy plus 2 per stack. Re-evaluate distance at each bounce.
            for(int i=0;i<2*stacks;i++){
                int best=-1;float distance=20+2*(stacks-1);
                for(int j=0;j<targets.Count;j++){
                    float separation=Mathf.Abs(targets[j].x-current.x);
                    if((visited&(1<<j))==0&&targets[j].Alive&&separation<=distance){best=j;distance=separation;}
                }
                if(best<0)break;
                visited|=1<<best;current=targets[best];float value=Mathf.Max(1,damage*l.damageFactor);
                lightningCount++;Deal(current,value);last="链电";ProcImpact(current,value,l.childCoefficient,ancestors|Ancestor.Lightning);
            }
        }
    }
}

