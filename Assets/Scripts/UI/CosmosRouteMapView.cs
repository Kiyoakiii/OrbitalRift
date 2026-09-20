using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalRift.UI
{
    [ExecuteAlways,RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class CosmosRouteMapView : MonoBehaviour
    {
        [Header("Optional hand-painted map; nodes and routes remain interactive")]
        [SerializeField] private Sprite mapArtwork;
        private ScrollRect scroll;
        private RectTransform content,viewport;
        private Image artwork;
        private CosmosRouteGraphic chart;
        private TMP_Text heading,subtitle,detail,actionLabel,backLabel;
        private Button action,back;
        private CanvasGroup group;
        private LivingCosmosRunState run;
        private int focus=-1,lastNode=-1;
        private LivingEncounterPhase lastPhase;
        private bool paused;
        private readonly Button[] nodes=new Button[8];
        private readonly TMP_Text[] labels=new TMP_Text[8];
        public event Action<int> DestinationRequested;
        public event Action ResumeRequested;
        public event Action ExitRequested;

        public void EnsureBuilt()
        {
            if(chart!=null)return;
            group=gameObject.GetComponent<CanvasGroup>();
            if(group==null)group=gameObject.AddComponent<CanvasGroup>();
            var bg=Rect("Background",transform,Vector2.zero,Vector2.one);
            var fill=bg.GetComponent<Image>()??bg.gameObject.AddComponent<Image>();fill.color=new Color(.008f,.016f,.032f,.97f);
            heading=Text("Heading",transform,new Vector2(.06f,.89f),new Vector2(.94f,.97f),38);
            subtitle=Text("Subtitle",transform,new Vector2(.06f,.82f),new Vector2(.94f,.89f),23);
            viewport=Rect("Star chart viewport",transform,new Vector2(.035f,.32f),new Vector2(.965f,.81f));
            var catcher=viewport.GetComponent<Image>()??viewport.gameObject.AddComponent<Image>();catcher.color=new Color(0,0,0,.001f);
            if(viewport.GetComponent<RectMask2D>()==null)viewport.gameObject.AddComponent<RectMask2D>();
            content=Rect("Route content",viewport,Vector2.zero,new Vector2(0,1));content.pivot=new Vector2(0,.5f);
            content.sizeDelta=new Vector2(1400,0);content.anchoredPosition=Vector2.zero;
            scroll=viewport.GetComponent<ScrollRect>()??viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport=viewport;scroll.content=content;scroll.horizontal=true;scroll.vertical=false;
            scroll.movementType=ScrollRect.MovementType.Clamped;
            var art=Rect("Your map artwork",content,Vector2.zero,Vector2.one);
            artwork=art.GetComponent<Image>()??art.gameObject.AddComponent<Image>();artwork.raycastTarget=false;
            var graphics=Rect("Procedural star chart",content,Vector2.zero,Vector2.one);
            if(graphics.GetComponent<CanvasRenderer>()==null)graphics.gameObject.AddComponent<CanvasRenderer>();
            chart=graphics.GetComponent<CosmosRouteGraphic>()??graphics.gameObject.AddComponent<CosmosRouteGraphic>();
            for(var i=0;i<8;i++)
            {
                var index=i;
                var node=Rect("Waypoint "+i,content,Vector2.zero,Vector2.zero);node.sizeDelta=new Vector2(76,76);
                var image=node.GetComponent<Image>()??node.gameObject.AddComponent<Image>();image.color=new Color(1,1,1,.001f);
                nodes[i]=node.GetComponent<Button>()??node.gameObject.AddComponent<Button>();nodes[i].targetGraphic=image;
                nodes[i].onClick.RemoveAllListeners();nodes[i].onClick.AddListener(()=>{focus=index;RefreshLabels();});
                labels[i]=Text("Name "+i,content,Vector2.zero,Vector2.zero,20);
                labels[i].rectTransform.sizeDelta=new Vector2(205,50);labels[i].alignment=TextAlignmentOptions.Center;
            }
            detail=Text("Destination detail",transform,new Vector2(.065f,.175f),new Vector2(.935f,.30f),26);
            action=Button("Set course",transform,new Vector2(.54f,.065f),new Vector2(.94f,.15f),out actionLabel);
            action.onClick.RemoveAllListeners();action.onClick.AddListener(()=>{if(run!=null && !paused)DestinationRequested?.Invoke(focus);});
            back=Button("Resume",transform,new Vector2(.06f,.065f),new Vector2(.45f,.15f),out backLabel);
            back.onClick.RemoveAllListeners();back.onClick.AddListener(()=>ResumeRequested?.Invoke());
            var exit=Button("Exit",transform,new Vector2(.71f,.012f),new Vector2(.94f,.05f),out var exitLabel);
            exitLabel.text="ВЫЙТИ В МЕНЮ";exitLabel.fontSize=18;
            exit.onClick.RemoveAllListeners();exit.onClick.AddListener(()=>ExitRequested?.Invoke());
        }

        public void Apply(LivingCosmosRunState state,bool isPaused)
        {
            var visible=state!=null && (isPaused || state.Phase==LivingEncounterPhase.RouteChoice || state.Phase==LivingEncounterPhase.Departing);
            if(!visible){gameObject.SetActive(false);return;}
            EnsureBuilt();var opening=!gameObject.activeSelf;gameObject.SetActive(true);transform.SetAsLastSibling();
            run=state;paused=isPaused;chart.Run=run;
            artwork.sprite=mapArtwork;artwork.enabled=mapArtwork!=null;chart.DrawAtmosphere=mapArtwork==null;
            content.sizeDelta=new Vector2(Mathf.Max(1400,viewport.rect.width),0);
            group.alpha=state.Phase==LivingEncounterPhase.Departing&&!paused?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1f,state.Transition)):1;
            if(opening||lastNode!=run.RoomIndex||lastPhase!=run.Phase)
            {
                lastNode=run.RoomIndex;lastPhase=run.Phase;focus=run.PendingNodeId>=0?run.PendingNodeId:run.RoomIndex;
                if(run.Phase==LivingEncounterPhase.RouteChoice && run.Layout.Rooms[run.RoomIndex].Connections.Count>0)
                    focus=run.Layout.Rooms[run.RoomIndex].Connections[0];
                Canvas.ForceUpdateCanvases();
                var x=LivingCosmosRunState.MapPosition(run.Layout,run.RoomIndex).x*content.rect.width;
                var overflow=Mathf.Max(1,content.rect.width-viewport.rect.width);
                scroll.horizontalNormalizedPosition=Mathf.Clamp01((x-viewport.rect.width*.28f)/overflow);
            }
            for(var i=0;i<8;i++)
            {
                var p=LivingCosmosRunState.MapPosition(run.Layout,i);
                var rect=(RectTransform)nodes[i].transform;rect.anchorMin=rect.anchorMax=p;rect.anchoredPosition=Vector2.zero;
                var label=labels[i].rectTransform;label.anchorMin=label.anchorMax=p;label.anchoredPosition=new Vector2(0,-52);
                labels[i].text=LivingCosmosRunState.NodeName(i);
                labels[i].color=run.Cleared.Contains(i)?new Color(.91f,.75f,.45f):run.IsAvailable(i)||i==run.RoomIndex?new Color(.75f,.88f,.98f):new Color(.34f,.45f,.55f);
            }
            RefreshLabels();chart.SetVerticesDirty();
        }
        private void RefreshLabels()
        {
            if(run==null)return;
            chart.Focus=focus;chart.SetVerticesDirty();
            heading.text=run.Phase==LivingEncounterPhase.Departing?"КУРС ПРОЛОЖЕН":"КАРТА ЭКСПЕДИЦИИ";
            subtitle.text=LivingCosmosRunState.RegionName(run.Region)+"  ·  ПРОЙДЕНО "+run.ClearedCount+"  ·  ЛИСТАЙ КАРТУ ↔";
            var type=run.Layout.Rooms[Mathf.Max(0,focus)].Type;
            var description=type==SectorRoomType.Elite?"ЭЛИТА · ОПАСНО\nДва выбора улучшения на следующей станции":
                type==SectorRoomType.Shop?"СТАНЦИЯ · БЕЗ БОЯ\nСтыковка и выбор улучшения корабля":type==SectorRoomType.Boss?"ФИНАЛЬНАЯ ЦЕЛЬ\nПобеди стража и заверши путешествие":type==SectorRoomType.Start?"МЫ ВОШЛИ В СЕКТОР\nВыбери первый участок маршрута":"ПАТРУЛЬ · ОБЫЧНАЯ УГРОЗА\nОдин выбор улучшения на следующей станции";
            detail.text=LivingCosmosRunState.NodeName(focus)+"\n"+description;
            var choosing=run.Phase==LivingEncounterPhase.RouteChoice&&!paused;
            action.interactable=choosing&&run.IsAvailable(focus);
            actionLabel.text=action.interactable?"ЛЕТЕТЬ СЮДА  →":paused?"ОБЗОР МАРШРУТА":choosing?"НЕДОСТУПНЫЙ КУРС":"ПЕРЕЛЁТ…";
            actionLabel.color=action.interactable?new Color(.61f,.9f,1):new Color(.39f,.5f,.62f);
            back.interactable=paused;backLabel.text=paused?"ПРОДОЛЖИТЬ ИГРУ":"ВЫБЕРИ ТОЧКУ НА КАРТЕ";
        }
        [ContextMenu("Preview star chart")]
        public void Preview(){var state=new LivingCosmosRunState();state.Initialize(17);Apply(state,true);}
        private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var old=parent.Find(name)as RectTransform;if(old!=null)return old;
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }
        private static TMP_Text Text(string name,Transform parent,Vector2 min,Vector2 max,float size)
        {
            var rect=Rect(name,parent,min,max);var text=rect.GetComponent<TextMeshProUGUI>()??rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font=Resources.Load<TMP_FontAsset>("Fonts/Jura SDF")??TMP_Settings.defaultFontAsset;
            text.fontSize=size;text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=size;
            text.color=new Color(.78f,.87f,.96f);text.alignment=TextAlignmentOptions.Left;text.raycastTarget=false;return text;
        }
        private static Button Button(string name,Transform parent,Vector2 min,Vector2 max,out TMP_Text label)
        {
            var rect=Rect(name,parent,min,max);var image=rect.GetComponent<Image>()??rect.gameObject.AddComponent<Image>();image.color=new Color(.15f,.25f,.35f,.10f);
            var button=rect.GetComponent<Button>()??rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            label=Text("Label",rect,new Vector2(.04f,0),new Vector2(.96f,1),25);return button;
        }
    }
}
