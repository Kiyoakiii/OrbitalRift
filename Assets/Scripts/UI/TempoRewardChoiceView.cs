using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalRift.UI
{
    /// <summary>Modal presentation only. GameManager validates and commits the selected module.</summary>
    [ExecuteAlways, RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class TempoRewardChoiceView : MonoBehaviour
    {
        private CanvasGroup group;
        private TMP_Text title, meta, instruction, replaceInstruction;
        private Button first, second, replaceFirst, replaceSecond;
        private TMP_Text firstLabel, secondLabel, replaceFirstLabel, replaceSecondLabel;
        private TempoRewardState state;
        private TempoModule selected=TempoModule.None;
        private string rewardId=string.Empty;
        public event Action<TempoModule,TempoModule> ChoiceRequested;
        public bool IsOpen => gameObject.activeSelf;

        private void OnEnable() => EnsureBuilt();
        [ContextMenu("Build editable impulse reward")]
        public void EnsureBuilt()
        {
            if (first != null) return;
            group=GetComponent<CanvasGroup>(); if(group==null)group=gameObject.AddComponent<CanvasGroup>();
            var blocker=Rect("Blocker",transform,Vector2.zero,Vector2.one);
            var background=blocker.GetComponent<Image>()??blocker.gameObject.AddComponent<Image>();
            background.color=new Color(.004f,.010f,.026f,.975f); background.raycastTarget=true;
            title=Label("Title",blocker,new Vector2(.08f,.82f),new Vector2(.92f,.92f),39);
            meta=Label("Meta",blocker,new Vector2(.08f,.755f),new Vector2(.92f,.82f),22);
            instruction=Label("Instruction",blocker,new Vector2(.08f,.69f),new Vector2(.92f,.755f),20);
            first=Card("First module",blocker,new Vector2(.07f,.33f),new Vector2(.46f,.65f),out firstLabel);
            second=Card("Second module",blocker,new Vector2(.54f,.33f),new Vector2(.93f,.65f),out secondLabel);
            first.onClick.RemoveAllListeners(); first.onClick.AddListener(()=>Select(PendingFirst()));
            second.onClick.RemoveAllListeners(); second.onClick.AddListener(()=>Select(PendingSecond()));
            replaceInstruction=Label("Replacement instruction",blocker,new Vector2(.08f,.25f),new Vector2(.92f,.31f),19);
            replaceFirst=Card("Replace first",blocker,new Vector2(.08f,.14f),new Vector2(.47f,.23f),out replaceFirstLabel);
            replaceSecond=Card("Replace second",blocker,new Vector2(.53f,.14f),new Vector2(.92f,.23f),out replaceSecondLabel);
            replaceFirst.onClick.RemoveAllListeners(); replaceFirst.onClick.AddListener(()=>ConfirmReplace(0));
            replaceSecond.onClick.RemoveAllListeners(); replaceSecond.onClick.AddListener(()=>ConfirmReplace(1));
            var footer=Label("Footer",blocker,new Vector2(.08f,.055f),new Vector2(.92f,.115f),18);
            footer.text="ИМПУЛЬ ДЕЙСТВУЕТ ДВА СЛЕДУЮЩИХ БОЯ · РЕЙТИНГ НЕ МЕНЯЕТСЯ";
        }
        public void Apply(TempoRewardState next)
        {
            bool visible=next != null && next.Pending != null;
            if(!visible) { gameObject.SetActive(false); return; }
            EnsureBuilt();
            if(!gameObject.activeSelf) { gameObject.SetActive(true); transform.SetAsLastSibling(); }
            state=next; var pending=state.Pending;
            if(rewardId!=pending.RewardId) { rewardId=pending.RewardId; selected=TempoModule.None; }
            title.text="ИМПУЛЬ НАЙДЕН";
            meta.text="БОЙ " + (pending.CombatMilliseconds/1000f).ToString("0.0") + " С  ·  ШАНС " +
                (pending.ChanceBasisPoints/100f).ToString("0") + "%  ·  ВЫБЕРИ ОДИН МОДУЛЬ";
            instruction.text="МОДУЛЬ ЗАПУСТИТСЯ В СЛЕДУЮЩЕМ БОЮ";
            PaintCard(first,firstLabel,pending.First);
            PaintCard(second,secondLabel,pending.Second);
            bool replacing=selected!=TempoModule.None && !state.Has(selected) && state.ModuleCount>=TempoRewardRules.MaximumModules;
            replaceInstruction.gameObject.SetActive(replacing);
            replaceFirst.gameObject.SetActive(replacing); replaceSecond.gameObject.SetActive(replacing);
            if(replacing)
            {
                replaceInstruction.text="СЛОТЫ ЗАНЯТЫ · ЧТО ЗАМЕНИТЬ НА «"+TempoRewardRules.Name(selected)+"»?";
                PaintReplacement(replaceFirst,replaceFirstLabel,0); PaintReplacement(replaceSecond,replaceSecondLabel,1);
            }
        }
        private TempoModule PendingFirst() => state==null||state.Pending==null?TempoModule.None:state.Pending.First;
        private TempoModule PendingSecond() => state==null||state.Pending==null?TempoModule.None:state.Pending.Second;
        private void Select(TempoModule module)
        {
            if(state==null || state.Pending==null || !TempoRewardRules.Valid(module)) return;
            if(state.Has(module) || state.ModuleCount<TempoRewardRules.MaximumModules)
                ChoiceRequested?.Invoke(module,TempoModule.None);
            else { selected=module; Apply(state); }
        }
        private void ConfirmReplace(int index)
        {
            if(state==null || selected==TempoModule.None || index<0 || index>=state.ModuleCount) return;
            ChoiceRequested?.Invoke(selected,state.ModuleAt(index).Module);
        }
        private static void PaintCard(Button button,TMP_Text label,TempoModule module)
        {
            var color=ColorFor(module); var image=button.GetComponent<Image>(); image.color=new Color(color.r*.16f,color.g*.16f,color.b*.20f,.92f);
            label.text=TempoRewardRules.Name(module)+"\n"+Description(module)+"\n\nЕЩЁ "+TempoRewardRules.ModuleDuration+" БОЯ";
            label.color=Color.white;
        }
        private void PaintReplacement(Button button,TMP_Text label,int index)
        {
            var slot=state.ModuleAt(index); var color=ColorFor(slot.Module); var image=button.GetComponent<Image>();
            image.color=new Color(color.r*.12f,color.g*.12f,color.b*.15f,.94f);
            label.text="ЗАМЕНИТЬ\n"+TempoRewardRules.Name(slot.Module)+" · ЕЩЁ "+slot.RemainingCombats+" БОЯ";
        }
        private static string Description(TempoModule module)
        {
            switch(module) {
                case TempoModule.RapidFire:return "+12% К ТЕМПУ ОГНЯ";
                case TempoModule.FastPlasma:return "+18% К СКОРОСТИ ПЛАЗМЫ";
                case TempoModule.ReserveCapacitor:return "1 БЛОК УРОНА В НАЧАЛЕ БОЯ";
                default:return string.Empty;
            }
        }
        private static Color ColorFor(TempoModule module)
        {
            switch(module) {
                case TempoModule.RapidFire:return new Color(.54f,.88f,1f);
                case TempoModule.FastPlasma:return new Color(1f,.63f,.30f);
                default:return new Color(.72f,.48f,1f);
            }
        }
        private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var existing=parent.Find(name) as RectTransform;if(existing!=null)return existing;
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }
        private static TMP_Text Label(string name,Transform parent,Vector2 min,Vector2 max,float size)
        {
            var rect=Rect(name,parent,min,max);var text=rect.GetComponent<TextMeshProUGUI>()??rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font=Resources.Load<TMP_FontAsset>("Fonts/Jura SDF")??TMP_Settings.defaultFontAsset;
            text.enableAutoSizing=true;text.fontSizeMin=15;text.fontSizeMax=size;text.fontSize=size;
            text.color=new Color(.80f,.90f,1f);text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;return text;
        }
        private static Button Card(string name,Transform parent,Vector2 min,Vector2 max,out TMP_Text label)
        {
            var rect=Rect(name,parent,min,max);var image=rect.GetComponent<Image>()??rect.gameObject.AddComponent<Image>();
            var button=rect.GetComponent<Button>()??rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            label=Label("Label",rect,new Vector2(.06f,.08f),new Vector2(.94f,.92f),27);return button;
        }
    }
}
