using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT
{
    // Self-contained, single-player mechanics sandbox. No network or permanent progression state.
    public sealed class RainWhitebox : MonoBehaviour
    {
        sealed class Enemy
        {
            public Transform body;
            public float hp, attackAt;
            public bool boss;
        }
        sealed class Point
        {
            public Transform body;
            public bool used, objective;
            public RectTransform map, marker;
            public TMP_Text label;
        }
        readonly List<Enemy> enemies = new List<Enemy>(32);
        readonly List<Point> points = new List<Point>(12);
        readonly List<Material> materials = new List<Material>();
        readonly List<Camera> oldCameras = new List<Camera>();
        Action onExit;
        CharacterController player;
        Camera view;
        Transform avatar;
        Canvas hud;
        TMP_Text stats, objective, prompt, result;
        RectTransform bossMap, bossMarker;
        TMP_Text bossLabel;
        Vector3 origin = new Vector3(12000, 200, 12000);
        float yaw, pitch = 20, gravity, shotAt, spawnAt, elapsed, nextHud;
        float health = 100, maxHealth = 100, damage = 25, speed = 8;
        int coins, kills, completed, purchases;
        bool ended, paused, bossSpawned;
        Enemy boss;
        readonly RaycastHit[] hits = new RaycastHit[64];
        static readonly string[] ItemDescriptions={"伤害 +10","生命上限 +25","移速 +1"};
        GameObject pausePanel;
        CombatInventoryPanel bag;
        TMP_Text wallet, clock, life;
        Image lifeFill;
        readonly int[] acquired = new int[3];
        readonly Sprite[] itemIcons = new Sprite[3];
        static readonly string[] UpgradeNames={"强化弹药","生命强化","疾行强化"};

        public static void Open(Action exit)
        {
            var run = new GameObject("Rain Whitebox Run").AddComponent<RainWhitebox>();
            run.onExit = exit;
            run.Build();
        }
        Material Mat(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if(!shader) shader = Shader.Find("Standard");
            var m = new Material(shader); m.color = color; materials.Add(m); return m;
        }
        GameObject Shape(string title, PrimitiveType type, Vector3 local, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = title;
            go.transform.SetParent(transform); go.transform.position = origin + local;
            go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
        Material enemyMaterial, bossMaterial, spentMaterial;
        void Build()
        {
            foreach(var c in Camera.allCameras) if(c.enabled){oldCameras.Add(c); c.enabled=false;}
            var floor = Mat(new Color(.35f,.4f,.43f));
            var wall = Mat(new Color(.6f,.65f,.67f));
            enemyMaterial=Mat(new Color(.8f,.2f,.15f)); bossMaterial=Mat(new Color(.65f,.12f,.8f));
            spentMaterial=Mat(new Color(.2f,.25f,.25f));
            Shape("Arena 80m",PrimitiveType.Cube,new Vector3(0,-1,0),new Vector3(80,2,80),floor);
            for(int i=0;i<4;i++)
            {
                bool side=i<2;
                Shape("Boundary",PrimitiveType.Cube,new Vector3(side?(i==0?-40:40):0,2,side?0:(i==2?-40:40)),side?new Vector3(2,6,80):new Vector3(80,6,2),wall);
            }
            var lightGo=new GameObject("Whitebox light",typeof(Light)); lightGo.transform.SetParent(transform);
            lightGo.transform.rotation=Quaternion.Euler(55,-35,0); var light=lightGo.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;
            var body=new GameObject("Whitebox Player");body.transform.SetParent(transform);body.transform.position=origin+Vector3.up;
            player=body.AddComponent<CharacterController>();player.height=2;player.radius=.45f;player.center=Vector3.up;
            avatar=Shape("Operator",PrimitiveType.Capsule,Vector3.up,new Vector3(.8f,1,.8f),Mat(Color.cyan)).transform;
            Destroy(avatar.GetComponent<Collider>());
            var cameraGo=new GameObject("Whitebox Camera",typeof(Camera),typeof(AudioListener));cameraGo.transform.SetParent(transform);
            view=cameraGo.GetComponent<Camera>();view.nearClipPlane=.1f;view.farClipPlane=150;view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.12f,.18f,.23f);
            // Existing scene audio listener belongs to the disabled camera; avoid duplicate listeners.
            Destroy(cameraGo.GetComponent<AudioListener>());
            hud=FrontEndUI.Canvas(transform,100);
            FrontEndUI.Box(hud.transform,"Wallet",90,70,240,95,new Color(.12f,.14f,.16f,.6f),"texDetailPanel");
            wallet=FrontEndUI.Text(hud.transform,"",108,75,205,85,27);
            FrontEndUI.Box(hud.transform,"Run clock",1540,70,290,115,new Color(.12f,.14f,.16f,.6f),"texDetailPanel");
            clock=FrontEndUI.Text(hud.transform,"",1555,75,260,65,27);
            FrontEndUI.Box(hud.transform,"Difficulty",1550,145,270,30,new Color(.22f,.55f,.12f));
            FrontEndUI.Text(hud.transform,"普通",1550,145,270,30,22,TextAlignmentOptions.Center);
            FrontEndUI.Box(hud.transform,"Objective",1540,195,290,130,new Color(0,0,0,.45f));
            objective=FrontEndUI.Text(hud.transform,"",1555,205,260,110,22);
            FrontEndUI.Text(hud.transform,"生命",100,920,200,30,22);
            FrontEndUI.Box(hud.transform,"Health background",100,955,430,35,new Color(.15f,.18f,.13f));
            lifeFill=FrontEndUI.Box(hud.transform,"Health",100,955,430,35,new Color(.35f,.75f,.1f));
            lifeFill.type=Image.Type.Filled;lifeFill.fillMethod=Image.FillMethod.Horizontal;
            life=FrontEndUI.Text(hud.transform,"",100,955,430,35,22,TextAlignmentOptions.Center);
            FrontEndUI.Text(hud.transform,"Tab 背包   Shift 奔跑",1180,1010,300,30,20);
            var actions=new[]{"射击","奔跑","交互"};var keys=new[]{"M1","Shift","E"};
            for(int i=0;i<3;i++)
            {
                FrontEndUI.Box(hud.transform,"Action",1510+i*100,935,85,70,new Color(.20f,.25f,.30f,.85f),"texDetailPanel");
                FrontEndUI.Text(hud.transform,actions[i],1510+i*100,935,85,70,22,TextAlignmentOptions.Center);
                FrontEndUI.Text(hud.transform,keys[i],1510+i*100,1007,85,30,23,TextAlignmentOptions.Center);
            }
            FrontEndUI.Text(hud.transform,"+",925,510,70,60,35,TextAlignmentOptions.Center);
            prompt=FrontEndUI.Text(hud.transform,"",450,855,1020,100,26,TextAlignmentOptions.Center);
            var map=FrontEndUI.Box(hud.transform,"Minimap",1540,350,290,290,new Color(0,0,0,.45f));
            FrontEndUI.Text(map.transform,"北 ↑  小地图",10,0,260,35,20,TextAlignmentOptions.Center);
            FrontEndUI.Text(map.transform,"▲",133,133,25,25,23,TextAlignmentOptions.Center).color=Color.cyan;
            for(int i=0;i<6;i++)
            {
                float a=i*Mathf.PI/3;AddPoint(new Vector3(Mathf.Cos(a)*13, .7f, Mathf.Sin(a)*13),false,map.transform);
            }
            for(int i=0;i<3;i++)
            {
                float a=i*Mathf.PI*2/3;AddPoint(new Vector3(Mathf.Cos(a)*28,1,Mathf.Sin(a)*28),true,map.transform);
            }
            bossMap=Dot(map.transform,"B",Color.magenta);
            bossLabel=FrontEndUI.Text(hud.transform,"BOSS",0,0,250,60,24,TextAlignmentOptions.Center);bossMarker=bossLabel.rectTransform;
            bossMap.gameObject.SetActive(false);bossMarker.gameObject.SetActive(false);
            bag=new GameObject("Run inventory",typeof(RectTransform)).AddComponent<CombatInventoryPanel>();bag.Initialize(hud.transform);
            itemIcons[0]=Resources.Load<Sprite>("RainImported/LogIcons/texSyringeIcon");
            itemIcons[1]=Resources.Load<Sprite>("RainImported/LogIcons/texSteakIcon");
            itemIcons[2]=Resources.Load<Sprite>("RainImported/LogIcons/texHoofIcon");
            pausePanel=FrontEndUI.Box(hud.transform,"Pause",610,340,700,390,FrontEndUI.Ink).gameObject;
            result=FrontEndUI.Text(pausePanel.transform,"暂停",35,25,630,120,35,TextAlignmentOptions.Center);
            FrontEndUI.Button(pausePanel.transform,"继续",180,170,340,60,()=>{if(!ended)SetPaused(false);});
            FrontEndUI.Button(pausePanel.transform,"返回主菜单",180,265,340,60,Exit);
            SetPaused(false);TickHud();
        }
        RectTransform Dot(Transform owner,string label,Color color)
        {
            var t=FrontEndUI.Text(owner,label,0,0,24,24,20,TextAlignmentOptions.Center);t.color=color;return t.rectTransform;
        }
        void AddPoint(Vector3 position,bool task,Transform map)
        {
            var go=Shape(task?"任务信标":"道具箱",PrimitiveType.Cube,position,task?new Vector3(1.4f,2,1.4f):new Vector3(1.4f,1.4f,1.4f),Mat(task?Color.cyan:Color.yellow));
            var label=FrontEndUI.Text(hud.transform,"",0,0,250,60,23,TextAlignmentOptions.Center);
            points.Add(new Point{body=go.transform,objective=task,map=Dot(map,task?"◆":"箱",task?Color.cyan:Color.yellow),marker=label.rectTransform,label=label});
        }
        void Update()
        {
            if(!player)return;
            if(Input.GetKeyDown(KeyCode.Escape)&&!ended)SetPaused(!paused);
            if(paused||ended)return;
            bool inspect=Input.GetKey(KeyCode.Tab);
            if(bag.IsOpen!=inspect){bag.SetOpen(inspect);Cursor.lockState=inspect?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=inspect;}
            float dt=Time.deltaTime;elapsed+=dt;
            if(!inspect){yaw+=Input.GetAxisRaw("Mouse X")*2;pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*2,-20,65);}
            var rotation=Quaternion.Euler(0,yaw,0);
            var movement=rotation*Vector3.ClampMagnitude(new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical")),1);
            if(inspect)movement=Vector3.zero;
            gravity=player.isGrounded?-2:gravity-20*dt;
            player.Move((movement*speed*(Input.GetKey(KeyCode.LeftShift)?1.45f:1)+Vector3.up*gravity)*dt);
            avatar.position=player.transform.position+Vector3.up;avatar.rotation=rotation;
            view.transform.rotation=Quaternion.Euler(pitch,yaw,0);
            view.transform.position=player.transform.position+Vector3.up*2-view.transform.forward*5;
            if(!inspect&&Input.GetMouseButton(0)&&elapsed>=shotAt){shotAt=elapsed+.16f;Shoot();}
            if(elapsed>=spawnAt&&enemies.Count<24){spawnAt=elapsed+Mathf.Max(.8f,3-elapsed/90);Spawn(false);}
            for(int i=enemies.Count-1;i>=0;i--)
            {
                var e=enemies[i];var delta=player.transform.position-e.body.position;delta.y=0;
                float distance=delta.magnitude;
                if(distance>1.7f+(e.boss?1.5f:0))e.body.position+=delta/Mathf.Max(.01f,distance)*(e.boss?2.5f:3.5f)*dt;
                else if(elapsed>=e.attackAt){e.attackAt=elapsed+1;health=Mathf.Max(0,health-(e.boss?25:8));}
            }
            // Death takes precedence over victory on a shared frame.
            if(health<=0){Finish(false);return;}
            if(bossSpawned&&boss==null){Finish(true);return;}
            if(!inspect&&Input.GetKeyDown(KeyCode.E))Interact();
            if(elapsed>=nextHud){nextHud=elapsed+.1f;TickHud();}
        }
        void Shoot()
        {
            int count=Physics.RaycastNonAlloc(view.ViewportPointToRay(new Vector3(.5f,.5f,0)),hits,110);
            int nearest=-1;float distance=float.MaxValue;
            for(int h=0;h<count;h++)if(hits[h].collider!=player && hits[h].distance<distance){nearest=h;distance=hits[h].distance;}
            if(nearest<0)return;
            var hit=hits[nearest];
            for(int i=0;i<enemies.Count;i++)
            {
                var e=enemies[i];if(hit.transform!=e.body)continue;
                e.hp-=damage;
                if(e.hp<=0){coins+=e.boss?100:10;kills++;Destroy(e.body.gameObject);enemies.RemoveAt(i);if(e==boss)boss=null;}
                break;
            }
        }
        void Spawn(bool isBoss)
        {
            float angle=UnityEngine.Random.value*Mathf.PI*2;
            var go=Shape(isBoss?"白盒 Boss":"怪物",PrimitiveType.Capsule,new Vector3(Mathf.Cos(angle)*34,isBoss?3:1,Mathf.Sin(angle)*34),isBoss?new Vector3(3,3,3):Vector3.one,isBoss?bossMaterial:enemyMaterial);
            var e=new Enemy{body=go.transform,hp=isBoss?1200:60+elapsed*.2f,boss=isBoss};enemies.Add(e);
            if(isBoss){boss=e;bossSpawned=true;bossMap.gameObject.SetActive(true);bossMarker.gameObject.SetActive(true);}
        }
        Point Nearby()
        {
            Point best=null;float distance=3.5f;
            foreach(var p in points)if(!p.used){float d=Vector3.Distance(player.transform.position,p.body.position);if(d<distance){distance=d;best=p;}}
            return best;
        }
        void Interact()
        {
            var p=Nearby();if(p==null)return;
            int price=25+purchases*10;
            if(!p.objective&&coins<price)return;
            // Consume before rewards, making repeated input idempotent.
            p.used=true;p.body.GetComponent<Renderer>().sharedMaterial=spentMaterial;
            if(p.objective){completed++;coins+=20;if(completed==3)Spawn(true);}
            else
            {
                coins-=price;
                switch(purchases%3){case 0:damage+=10;break;case 1:maxHealth+=25;health=Mathf.Min(maxHealth,health+25);break;case 2:speed+=1;break;}
                acquired[purchases%3]++;
                purchases++;
            }
            TickHud();
        }
        void TickHud()
        {
            wallet.text=$"$  {coins}\n击杀  {kills}";
            clock.text=$"关卡 1    {Mathf.FloorToInt(elapsed)/60:00}:{Mathf.FloorToInt(elapsed)%60:00}\n单人白盒";
            life.text=$"{health:0} / {maxHealth:0}";lifeFill.fillAmount=health/maxHealth;
            objective.text=bossSpawned?"目标：\n□ 击败 Boss":"目标：\n□ 激活任务信标  "+completed+" / 3";
            for(int i=0;i<3;i++)bag.SetItem(i,itemIcons[i],UpgradeNames[i],"白盒升级 · 每层"+ItemDescriptions[i]+"。\n当前层数："+acquired[i],acquired[i]);
            var nearby=Nearby();
            prompt.text=nearby==null?"黄色：道具箱  /  青色：任务信标":nearby.objective?"E 激活任务信标":$"E 购买道具 · {25+purchases*10} 金币 · "+ItemDescriptions[purchases%3];
            foreach(var p in points)
            {
                p.map.gameObject.SetActive(!p.used);p.marker.gameObject.SetActive(!p.used);
                if(p.used)continue;MapPosition(p.map,p.body.position);
                Marker(p.marker,p.body.position);p.label.text=(p.objective?"◆ 任务":"道具箱")+" "+Vector3.Distance(player.transform.position,p.body.position).ToString("0")+"m";
            }
            if(boss!=null){MapPosition(bossMap,boss.body.position);Marker(bossMarker,boss.body.position);bossLabel.text=$"BOSS {boss.hp:0} HP";}
        }
        void MapPosition(RectTransform dot,Vector3 world)
        {
            var d=world-player.transform.position;dot.anchoredPosition=new Vector2(133+Mathf.Clamp(d.x*3,-120,120),-133-Mathf.Clamp(d.z*-3,-120,120));
        }
        void Marker(RectTransform marker,Vector3 world)
        {
            var v=view.WorldToViewportPoint(world+Vector3.up*2);
            if(v.z<0){v.x=1-v.x;v.y=v.y>.5f?0:1;}
            marker.anchoredPosition=new Vector2(Mathf.Clamp(v.x*1920-125,15,1655),-Mathf.Clamp((1-v.y)*1080,240,800));
        }
        void SetPaused(bool value)
        {
            if(bag)bag.SetOpen(false);
            paused=value;pausePanel.SetActive(value);Cursor.lockState=value?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=value;
        }
        void Finish(bool won){ended=true;result.text=won?$"挑战完成！\n击杀 {kills} · 道具 {purchases}":"挑战失败 · 返回主菜单重试";SetPaused(true);}
        void OnApplicationFocus(bool focused){if(!focused&&pausePanel&&!ended)SetPaused(true);}
        void Exit(){var callback=onExit;onExit=null;Destroy(gameObject);callback?.Invoke();}
        void OnDestroy()
        {
            foreach(var c in oldCameras)if(c)c.enabled=true;
            foreach(var m in materials)if(m)Destroy(m);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
    }
}
