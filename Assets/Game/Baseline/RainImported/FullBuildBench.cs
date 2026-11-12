using System;
using System.Linq;
using TMPro;
using UnityEngine;
namespace MTPSKIT.RainImported
{
    // Reuses the complete reference simulator, rather than duplicating its item switches.
    public sealed class FullBuildBench : MonoBehaviour
    {
        WeaponBuild model;
        PickupRow[] entries;
        DroneSpec[] drones;
        Canvas canvas;
        Transform page;
        TMP_Text readout;
        int offset,category;
        bool running;
        float accumulator,refresh;
        Action closed;
        public static FullBuildBench Open(Action onClose)
        {
            var bench=new GameObject("Full item and drone bench").AddComponent<FullBuildBench>();
            bench.closed=onClose;bench.model=new WeaponBuild();
            bench.entries=new LogbookCatalog().Entries.Where(e=>e.family=="item"||e.family=="equipment").ToArray();
            bench.drones=DroneCatalog.Load().units;
            bench.canvas=FrontEndUI.Canvas(bench.transform,200);bench.Draw();return bench;
        }
        void Draw()
        {
            if(page){page.gameObject.SetActive(false);Destroy(page.gameObject);}
            page=FrontEndUI.Rect(canvas.transform,"Bench",0,0,1920,1080);
            FrontEndUI.Box(page,"Backdrop",0,0,1920,1080,FrontEndUI.Ink);
            FrontEndUI.Text(page,"全道具 / 无人机效果试验台 · 独立模拟战斗",35,20,1800,60,34);
            FrontEndUI.Text(page,"复用参考工程全部效果。此台暂使用一维距离结算，与身后的三维靶场战斗独立。",35,85,1800,45,22);
            FrontEndUI.Button(page,"物品与装备",35,145,300,55,()=>{category=0;offset=0;Draw();});
            FrontEndUI.Button(page,"无人机编队",350,145,300,55,()=>{category=1;offset=0;Draw();});
            FrontEndUI.Button(page,"图标图鉴",665,145,160,55,()=>
            {
                running=false;
                JournalBrowser.Open(canvas.transform,()=>UpdateText(),entry=>
                {
                    if(entry.drone!=null){model.Recruit(entry.drone);return "已招募至模拟编队。";}
                    var e=entry.pickup;if(e.family=="equipment")model.Equip(e.catalogName,e.cooldown);else model.Add(e.catalogName,1);
                    return "已添加至构筑模拟台。";
                },entry=>entry.drone!=null||entry.pickup!=null&&(entry.pickup.family=="item"||entry.pickup.family=="equipment"),entry=>
                {
                    if(entry.drone!=null){model.Recall(entry.drone.id);return "已从模拟编队拆卸。";}
                    var e=entry.pickup;if(e.family=="equipment"){if(model.equipment==e.catalogName)model.Equip("",0);}else model.Add(e.catalogName,-1);
                    return "已从模拟构筑拆卸。";
                });
            });
            int count=category==0?entries.Length:drones.Length;
            for(int i=0;i<8&&offset+i<count;i++)
            {
                int index=offset+i;float y=220+i*87;
                string name=category==0?entries[index].name:drones[index].name;
                string detail=category==0?(entries[index].family=="equipment"?"主动装备":entries[index].catalogName):drones[index].id;
                FrontEndUI.Text(page,name+"  "+detail,35,y,610,40,21);
                FrontEndUI.Button(page,"+",650,y,70,50,()=>Change(index,1));
                FrontEndUI.Button(page,"−",730,y,70,50,()=>Change(index,-1));
            }
            FrontEndUI.Button(page,"上一页",35,940,240,55,()=>{offset=Mathf.Max(0,offset-8);Draw();});
            FrontEndUI.Button(page,$"下一页 {offset+1}/{count}",300,940,320,55,()=>{if(offset+8<count)offset+=8;Draw();});
            readout=FrontEndUI.Text(page,"",850,145,1020,650,23,TextAlignmentOptions.TopLeft);
            string[] actions={"开始 / 暂停","主攻击","次要技能","特殊技能","使用装备","全队指令","维修无人机","下一波","宝箱","打印机","神龛","传送器","拾取掉落","跳跃","辅助技能","清空重置"};
            for(int i=0;i<actions.Length;i++){int a=i;FrontEndUI.Button(page,actions[i],850+(i%4)*250,710+(i/4)*65,240,55,()=>Act(a));}
            FrontEndUI.Button(page,"返回三维训练场",1460,1000,410,55,()=>{Destroy(gameObject);closed?.Invoke();closed=null;});
            UpdateText();
        }
        void Change(int index,int delta)
        {
            if(category==1){if(delta>0)model.Recruit(drones[index]);else model.Recall(drones[index].id);}
            else {var e=entries[index];if(e.family=="equipment")model.Equip(delta>0?e.catalogName:"",e.cooldown);else model.Add(e.catalogName,delta);}
            UpdateText();
        }
        void Act(int a)
        {
            switch(a){case 0:running=!running;break;case 1:model.Shoot();break;case 2:model.Shoot(1);break;case 3:model.Shoot(3);break;case 4:model.UseEquipment();break;case 5:model.CommandFleet();break;case 6:model.RepairFleet();break;case 7:model.NextStage();break;case 8:model.Interact("宝箱");break;case 9:model.Interact("打印机");break;case 10:model.Interact("神龛");break;case 11:model.Interact("传送器");break;case 12:model.Pickup();break;case 13:model.Jump();break;case 14:model.Dodge();break;case 15:model=new WeaponBuild();running=false;break;}
            UpdateText();
        }
        void Update()
        {
            if(running)
            {
                accumulator+=Mathf.Min(Time.unscaledDeltaTime,.25f);
                while(accumulator>=.05f){accumulator-=.05f;model.sprint=Input.GetKey(KeyCode.LeftShift);model.Move(Input.GetAxisRaw("Horizontal"),.05f);model.Step(.05f);}
                if(Input.GetKey(KeyCode.J))model.Shoot();if(Input.GetKeyDown(KeyCode.Q))model.UseEquipment();if(Input.GetKeyDown(KeyCode.F))model.CommandFleet();
            }
            if(Time.unscaledTime>=refresh){refresh=Time.unscaledTime+.2f;UpdateText();}
        }
        void UpdateText()
        {
            if(!readout)return;
            string text=$"{(running?"模拟运行中":"模拟暂停")} · A/D移动 Shift奔跑 J射击 Q装备 F指令\n生命 {model.hp:0}/{model.Max:0} 护盾 {model.shield:0} 屏障 {model.barrier:0}\n金币 {model.gold} 击杀 {model.kills} 输出 {model.damageDone:0} 随从输出 {model.minionDamage:0}\n装备 {model.equipment} 充能 {model.equipmentCharges} 最近效果 {model.last}\n";
            foreach(var t in model.targets)text+=$"{(t.boss?"首领":t.elite?"精英":"敌人")} 距离 {Mathf.Abs(t.x-model.x):0.0}m 生命 {Mathf.Max(0,t.hp):0}/{t.max:0}\n";
            text+="库存：";foreach(var pair in model.Inventory)if(pair.Value>0)text+=pair.Key+"×"+pair.Value+" ";
            text+="\n编队：";foreach(var drone in model.fleet)text+=drone.def.name+"("+drone.hp.ToString("0")+") ";
            readout.text=text;
        }
    }
}
