using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT.Gameplay
{
    // Scene-owned UI; reads Health SyncVars and reuses widgets when bots respawn.
    [DefaultExecutionOrder(11000)]
    public sealed class RangeRobotHealthBars : MonoBehaviour
    {
        sealed class Bar
        {
            public CharacterInstance Character;
            public RangeRobotVisual Visual;
            public Animator Model;
            public Transform Head;
            public RectTransform Root, Fill;
            public Image FillImage;
            public TextMeshProUGUI Value;
            public int Health = -1, MaxHealth = -1;
        }
        readonly List<Bar> active = new List<Bar>(32);
        readonly Stack<Bar> pool = new Stack<Bar>(32);
        RectTransform layer;
        Camera worldCamera;
        public int TrackedCount => active.Count;
        public int VisibleCount { get; private set; }

        public static void Register(CharacterInstance character)
        {
            if (!character || !GameManager.Gamemode) return;
            var bars = GameManager.Gamemode.GetComponent<RangeRobotHealthBars>();
            if (!bars) bars = GameManager.Gamemode.gameObject.AddComponent<RangeRobotHealthBars>();
            bars.Add(character);
        }
        void Awake()
        {
            var root = new GameObject("Range Robot Health Bars", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            root.transform.SetParent(transform, false); layer = (RectTransform)root.transform;
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            var group = root.GetComponent<CanvasGroup>(); group.interactable = group.blocksRaycasts = false;
        }
        void Add(CharacterInstance character)
        {
            for(int i=0;i<active.Count;i++) if(active[i].Character==character)return;
            var bar=pool.Count>0?pool.Pop():Create();
            bar.Character=character;bar.Visual=character.GetComponent<RangeRobotVisual>();bar.Model=null;bar.Head=null;
            bar.Health=bar.MaxHealth=-1;active.Add(bar);
        }
        Bar Create()
        {
            var bar=new Bar();
            bar.Root=new GameObject("Robot Health",typeof(RectTransform)).GetComponent<RectTransform>();bar.Root.SetParent(layer,false);
            bar.Root.anchorMin=bar.Root.anchorMax=new Vector2(.5f,.5f);bar.Root.sizeDelta=new Vector2(124,34);
            var background=Image("Background",bar.Root,new Color(.015f,.02f,.025f,.95f));
            background.rectTransform.anchorMin=new Vector2(0,0);background.rectTransform.anchorMax=new Vector2(1,0);
            background.rectTransform.pivot=new Vector2(.5f,0);background.rectTransform.anchoredPosition=Vector2.zero;background.rectTransform.sizeDelta=new Vector2(0,9);
            bar.FillImage=Image("Fill",background.transform,new Color(.15f,.85f,.3f));bar.Fill=bar.FillImage.rectTransform;
            bar.Fill.anchorMin=Vector2.zero;bar.Fill.anchorMax=Vector2.one;bar.Fill.offsetMin=new Vector2(0,1);bar.Fill.offsetMax=new Vector2(0,-1);
            bar.Value=new GameObject("Health Value",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();bar.Value.transform.SetParent(bar.Root,false);
            bar.Value.font=TMP_Settings.defaultFontAsset;bar.Value.fontSize=20;bar.Value.alignment=TextAlignmentOptions.Center;
            bar.Value.raycastTarget=false;bar.Value.color=Color.white;bar.Value.outlineWidth=.2f;
            bar.Value.rectTransform.anchorMin=new Vector2(0,0);bar.Value.rectTransform.anchorMax=new Vector2(1,0);
            bar.Value.rectTransform.pivot=new Vector2(.5f,0);bar.Value.rectTransform.anchoredPosition=new Vector2(0,10);bar.Value.rectTransform.sizeDelta=new Vector2(0,25);
            bar.Root.gameObject.SetActive(false);return bar;
        }
        static Image Image(string name,Transform parent,Color color)
        {
            var image=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent,false);image.color=color;image.raycastTarget=false;return image;
        }
        void LateUpdate()
        {
            if(!worldCamera||!worldCamera.isActiveAndEnabled)
                worldCamera=GameplayCamera._instance?GameplayCamera._instance.GetComponent<Camera>():Camera.main;
            VisibleCount=0;
            for(int i=active.Count-1;i>=0;i--)
            {
                var bar=active[i];var character=bar.Character;
                if(!character){bar.Root.gameObject.SetActive(false);bar.Character=null;bar.Visual=null;bar.Model=null;bar.Head=null;active.RemoveAt(i);pool.Push(bar);continue;}
                if(bar.Health!=character.CurrentHealth||bar.MaxHealth!=character.MaxHealth)
                {
                    bar.Health=character.CurrentHealth;bar.MaxHealth=character.MaxHealth;
                    float ratio=Mathf.Clamp01((float)bar.Health/Mathf.Max(1,bar.MaxHealth));
                    bar.Fill.anchorMax=new Vector2(ratio,1);bar.FillImage.color=Color.Lerp(new Color(.95f,.15f,.08f),new Color(.15f,.85f,.3f),ratio);
                    bar.Value.SetText("{0:0} / {1:0}",bar.Health,bar.MaxHealth);
                }
                bool visible=character.CurrentHealth>0&&worldCamera;
                Vector2 point=default;
                if(visible)
                {
                    if(!bar.Visual)bar.Visual=character.GetComponent<RangeRobotVisual>();
                    var model=bar.Visual?bar.Visual.DisplayAnimator:null;
                    if(model!=bar.Model){bar.Model=model;bar.Head=model&&model.isHuman?model.GetBoneTransform(HumanBodyBones.Head):null;}
                    Vector3 anchor=bar.Head?bar.Head.position+Vector3.up*.4f:character.transform.position+Vector3.up*2.2f;
                    var screen=worldCamera.WorldToScreenPoint(anchor);
                    visible=(worldCamera.transform.position-anchor).sqrMagnitude<=10000&&screen.z>.1f&&screen.x>=0&&screen.x<=Screen.width&&screen.y>=0&&screen.y<=Screen.height;
                    if(visible)visible=!Physics.Linecast(worldCamera.transform.position,anchor,GameManager.environmentLayer,QueryTriggerInteraction.Ignore);
                    if(visible)RectTransformUtility.ScreenPointToLocalPointInRectangle(layer,screen,null,out point);
                }
                if(bar.Root.gameObject.activeSelf!=visible)bar.Root.gameObject.SetActive(visible);
                if(visible){bar.Root.anchoredPosition=point;VisibleCount++;}
            }
        }
        public bool TryGetDisplayedHealth(CharacterInstance character,out int health,out float ratio,out bool visible)
        {
            for(int i=0;i<active.Count;i++)if(active[i].Character==character)
            {var bar=active[i];health=bar.Health;ratio=bar.Fill.anchorMax.x;visible=bar.Root.gameObject.activeSelf;return true;}
            health=0;ratio=0;visible=false;return false;
        }
    }
}
