using UnityEngine;
namespace OrbitalRift
{
    public sealed class SpaceLayerDebugPanel
    {
        private readonly SpaceFlightVisualController c;
        public bool Open;
        private bool courseExpanded=true, artworkExpanded;
        private int selected;
        private Vector2 scroll;
        private string status="Изменения временные до сохранения.";
        public bool BlocksInput
        {
            get
            {
                if(!Open)return false;
#if UNITY_EDITOR
                if(UnityEditor.EditorGUIUtility.editingTextField)return true;
#endif
                var point=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
                return GUIUtility.hotControl!=0||(new Rect(12,12,Mathf.Min(410,Screen.width-24),Screen.height-24).Contains(point)&&(Input.GetMouseButton(0)||Input.touchCount>0));
            }
        }
        public SpaceLayerDebugPanel(SpaceFlightVisualController value){c=value;}
        public bool Draw()
        {
            var width=Mathf.Min(410,Screen.width-24);
            if(!Open){if(GUI.Button(new Rect(Screen.width-width-14,Screen.height-42,width,32),"SPACE DEPTH · слои космоса")){Open=true;c.FollowGameplaySpeed=false;}return false;}
            GUILayout.BeginArea(new Rect(12,12,width,Screen.height-24),GUI.skin.box);
            GUILayout.BeginHorizontal();GUILayout.Label("SPACE DEPTH · COURSE");if(GUILayout.Button("Закрыть",GUILayout.Width(75)))Open=false;GUILayout.EndHorizontal();
            scroll=GUILayout.BeginScrollView(scroll);
            Slider("Скорость космоса",ref c.RuntimeProfile.GlobalTravelSpeed,0,100);
            c.PauseMotion=GUILayout.Toggle(c.PauseMotion,"Пауза движения космоса");
            c.FollowGameplaySpeed=GUILayout.Toggle(c.FollowGameplaySpeed,"Скорость зависит от боя / перехода");
            GUILayout.Label("Итоговая скорость: "+c.GetTravelSpeed().ToString("0.00"));
            GUILayout.BeginHorizontal();if(GUILayout.Button("Все"))c.ShowAll(true);if(GUILayout.Button("Скрыть"))c.ShowAll(false);if(GUILayout.Button("Сбросить всё"))c.ResetAll();GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();if(GUILayout.Button("Перечитать профиль")){c.ResetAll();status="Загружен сохранённый профиль.";}
#if UNITY_EDITOR
            if(GUILayout.Button("Сохранить")){c.SaveProfile();status="Профиль сохранён в проект.";}
#endif
            GUILayout.EndHorizontal();GUILayout.Label(status);
            courseExpanded=GUILayout.Toggle(courseExpanded,"КУРС · управление и диагностика","Button");
            if(courseExpanded)DrawCourse();
            for(var i=0;i<c.Layers.Length;i++)if(GUILayout.Toggle(selected==i,(i+1)+". "+c.RuntimeProfile.Layers[i].DisplayName,"Button"))selected=i;
            var s=c.RuntimeProfile.Layers[selected];
            if(s.Kind==SpaceLayerKind.ExternalMusicRings)GUILayout.Label("Музыкальный визуализатор следует за центром курса.\nЕго оформление настраивается отдельно.");
            else
            {
                s.Enabled=GUILayout.Toggle(s.Enabled,"Слой включён");
                var solo=GUILayout.Toggle(c.SoloLayer==selected,"SOLO выбранного слоя");if(solo){c.SoloLayer=selected;s.Enabled=true;}else if(c.SoloLayer==selected)c.SoloLayer=-1;
                if(GUILayout.Button("Сбросить выбранный слой")){c.ResetLayer(selected);s=c.RuntimeProfile.Layers[selected];}
                Slider("Глубина · далеко 0 / близко 1",ref s.Depth,0,1);Slider("Множитель скорости",ref s.SpeedMultiplier,0,4);
                Slider("Влияние курса · 0 = неподвижный центр",ref s.SteeringInfluence,0,1);Slider("Скорость отклика курса",ref s.SteeringResponseSpeed,0,10);
                artworkExpanded=GUILayout.Toggle(artworkExpanded,"ИЗОБРАЖЕНИЕ / МАТЕРИАЛ","Button");
                if(artworkExpanded)DrawArtwork(s);
                if(s.Kind==SpaceLayerKind.Nebula||s.Kind==SpaceLayerKind.Galaxy)s.FrameComposition=GUILayout.Toggle(s.FrameComposition,"Композиция у краёв экрана");
                Slider("Параллакс камеры",ref s.ParallaxMultiplier,0,3);Slider("Плотность",ref s.Density,0,3);
                GUILayout.Label("Базовое количество: "+s.BaseCount+" · сейчас: "+c.Layers[selected].ActiveCount);
                s.BaseCount=Mathf.RoundToInt(GUILayout.HorizontalSlider(s.BaseCount,0,Mathf.Max(1,s.Capacity)));
                var large=s.Kind==SpaceLayerKind.DeepSpace||s.Kind==SpaceLayerKind.Galaxy||s.Kind==SpaceLayerKind.Nebula||s.Kind==SpaceLayerKind.Planet;
                Slider("Размер min",ref s.MinScale,.005f,large?45:1);Slider("Размер max",ref s.MaxScale,s.MinScale,large?50:2);
                Slider("Прозрачность min",ref s.MinAlpha,0,1);Slider("Прозрачность max",ref s.MaxAlpha,s.MinAlpha,1);
                Slider("Яркость",ref s.Brightness,0,3);Slider("Вращение",ref s.RotationSpeed,-45,45);Slider("Шум движения",ref s.NoiseAmount,0,1);
                s.RadialMotion=GUILayout.Toggle(s.RadialMotion,"Радиальное движение");Slider("Касательное движение",ref s.TangentialMotion,-1,1);
                Slider("Радиус появления",ref s.SpawnRadius,0,5);Slider("Радиус исчезновения",ref s.DespawnRadius,s.SpawnRadius+.2f,25);
                if(s.Kind==SpaceLayerKind.Streaks)Slider("Длина стриков",ref s.Stretch,1,60);
                Slider("Цвет R",ref s.PrimaryColor.r,0,1);Slider("Цвет G",ref s.PrimaryColor.g,0,1);Slider("Цвет B",ref s.PrimaryColor.b,0,1);
                Slider("Разброс оттенков",ref s.ColorVariation,0,1);
                if(s.ColorVariation>0){Slider("Второй цвет R",ref s.SecondaryColor.r,0,1);Slider("Второй цвет G",ref s.SecondaryColor.g,0,1);Slider("Второй цвет B",ref s.SecondaryColor.b,0,1);}
                if(s.Kind==SpaceLayerKind.Asteroids||s.Kind==SpaceLayerKind.Dust)
                {
                    s.DepthVariety=GUILayout.Toggle(s.DepthVariety,"Три плана · дальний / средний / ближний");
                    Slider("Свободный центр · крупные элементы",ref s.CenterClearance,0,9);
                }
                if(s.Kind==SpaceLayerKind.Dust){Slider("Мягкость / расфокус пыли",ref s.Softness,0,1);GUILayout.Label("Время пролёта задаётся скоростью и радиусами рождения / исчезновения. Дымка размывает только себя.");}
                if(s.Kind==SpaceLayerKind.Nebula)DrawNebula();
                if(s.Kind==SpaceLayerKind.Planet)GUILayout.Label("Не больше одной планеты. По умолчанию выключена: главный акцент — туманности.");
                if(s.Kind==SpaceLayerKind.Galaxy&&s.FrameComposition)GUILayout.Label("Две позиции галактик у краёв. Плотность не накладывает копии друг на друга.");
            }
            GUILayout.Label("Элементов: "+c.ActiveElements+" · "+(1/Mathf.Max(.001f,Time.smoothDeltaTime)).ToString("0")+" FPS · "+(Time.smoothDeltaTime*1000).ToString("0.0")+" ms");
            GUILayout.Label("Крупных объектов: "+c.ActiveLargeObjects+" · выбранный слой: "+c.Layers[selected].ActiveCount);
            GUILayout.EndScrollView();GUILayout.EndArea();
            return true;
        }
        private void DrawCourse()
        {
            var s=c.RuntimeProfile.Steering;var course=c.Course;
            s.SteeringEnabled=GUILayout.Toggle(s.SteeringEnabled,"Управление курсом включено");
            GUILayout.Label("Курс: I J K L / левый стик. Орбита: A D / стрелки / правый стик.");
            Slider("Скорость ввода · доля экрана/с",ref s.CourseMoveSpeed,0,1);
            Slider("Сглаживание · секунды",ref s.CourseSmoothTime,.01f,2);
            Slider("Макс. скорость центра",ref s.MaxCourseCenterSpeed,.01f,2);
            Slider("Макс. смещение X",ref s.MaxCourseOffsetX,0,.45f);Slider("Макс. смещение Y",ref s.MaxCourseOffsetY,0,.45f);
            Slider("Мёртвая зона стика",ref s.InputDeadZone,0,.9f);
            s.ReturnToCenter=GUILayout.Toggle(s.ReturnToCenter,"Возвращаться в центр при отпускании");
            if(s.ReturnToCenter)Slider("Скорость возврата",ref s.ReturnToCenterSpeed,0,1);
            Slider("Общее влияние курса на слои",ref s.GlobalSteeringStrength,0,2);
            Slider("Следование центра орбиты",ref s.OrbitCenterFollowStrength,0,1);
            Slider("Запас до края экрана · world units",ref s.SafeScreenMargin,0,1);
            s.InstantReset=GUILayout.Toggle(s.InstantReset,"Мгновенный сброс кнопкой Center");
            for(var row=1;row>=-1;row--)
            {
                GUILayout.BeginHorizontal();
                for(var col=-1;col<=1;col++)
                {
                    var label=col==0&&row==0?"Center":(row>0?"↑":row<0?"↓":"")+(col<0?"←":col>0?"→":"");
                    if(GUILayout.Button(label)){course.AutoTest=CourseAutoTest.Off;if(col==0&&row==0)course.Center();else course.Preset(new Vector2(col,row));}
                }
                GUILayout.EndHorizontal();
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUILayout.Label("Автотест курса");
            course.AutoTest=(CourseAutoTest)GUILayout.SelectionGrid((int)course.AutoTest,new[]{"Off","↔","↕","Круг","Восьмёрка"},3);
            s.ShowCourseDebug=GUILayout.Toggle(s.ShowCourseDebug,"Маркеры центра и слоёв");s.ShowDebugLines=GUILayout.Toggle(s.ShowDebugLines,"Линии между маркерами");
#endif
            GUILayout.Label("Neutral "+course.NeutralCourseCenter.ToString("F2")+" · Target "+course.TargetCourseCenter.ToString("F2")+"\nCurrent "+course.CurrentCourseCenter.ToString("F2")+" · Offset "+course.CourseOffset.ToString("F2")+"\nБезопасные пределы "+course.SafeOffset.ToString("F2"));
        }
        private void DrawArtwork(SpaceLayerSettings s)
        {
            s.Appearance=(SpaceLayerAppearance)GUILayout.SelectionGrid((int)s.Appearance,new[]{"Процедурная форма","Изображение"},1);
#if UNITY_EDITOR
            var skin=GUI.skin;
            try
            {
                GUI.skin=UnityEditor.EditorGUIUtility.GetBuiltinSkin(UnityEditor.EditorSkin.Inspector);
                s.Material=(Material)UnityEditor.EditorGUI.ObjectField(GUILayoutUtility.GetRect(0,UnityEditor.EditorGUIUtility.singleLineHeight,GUILayout.ExpandWidth(true)),"Material",s.Material,typeof(Material),false);
                s.Texture=(Texture2D)UnityEditor.EditorGUI.ObjectField(GUILayoutUtility.GetRect(0,UnityEditor.EditorGUIUtility.singleLineHeight,GUILayout.ExpandWidth(true)),"Texture",s.Texture,typeof(Texture2D),false);
                s.Sprite=(Sprite)UnityEditor.EditorGUI.ObjectField(GUILayoutUtility.GetRect(0,UnityEditor.EditorGUIUtility.singleLineHeight,GUILayout.ExpandWidth(true)),"Sprite",s.Sprite,typeof(Sprite),false);
            }
            finally{GUI.skin=skin;}
#else
            GUILayout.Label("Материал и изображение назначаются в профиле проекта Unity.");
#endif
            s.PreserveImageAspect=GUILayout.Toggle(s.PreserveImageAspect,"Сохранять пропорции изображения");
            GUILayout.Label("Sprite имеет приоритет над Texture. Пустой Material использует встроенный шейдер. PNG: прозрачный фон; Sprite: Full Rect.");
            if(GUILayout.Button("Обновить материалы"))c.ReloadArtwork();
        }
        private void DrawNebula()
        {
            var n=c.RuntimeProfile.Nebula;if(n==null){n=new SpaceNebulaSettings();c.RuntimeProfile.Nebula=n;}
            GUILayout.Label("ЛОКАЛЬНЫЕ КЛАСТЕРЫ · центр остаётся читаемым");
            IntSlider("Количество кластеров",ref n.ClusterCount,2,4);
            Slider("Размер кластеров",ref n.ClusterSize,3,12);
            Slider("Яркость ядер",ref n.Brightness,0,3);
            Slider("Общая прозрачность",ref n.Opacity,0,1);
            Slider("Glow pockets",ref n.Glow,0,2);
            Slider("Filaments / wisps",ref n.Filament,0,1);
            Slider("Тёмные внутренние voids",ref n.DarkVoids,0,1);
            Slider("Медленный дрейф",ref n.Drift,0,1);
            Slider("Звёздная пыль",ref n.Dust,0,2);
            Slider("Tint R",ref n.Tint.r,0,1);Slider("Tint G",ref n.Tint.g,0,1);Slider("Tint B",ref n.Tint.b,0,1);
            Slider("Highlight R",ref n.HighlightTint.r,0,1);Slider("Highlight G",ref n.HighlightTint.g,0,1);Slider("Highlight B",ref n.HighlightTint.b,0,1);
            GUILayout.Label("Параметры применяются сразу; максимум — четыре локальных кластера.");
        }
        public void DrawDebug()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var s=c.RuntimeProfile.Steering;if(!s.ShowCourseDebug||c.ViewCamera==null)return;
            var course=c.Course;
            var neutral=ScreenPoint(course.NeutralWorldCenter);var current=ScreenPoint(course.WorldCourseCenter);
            var target=new Vector2(course.TargetCourseCenter.x*Screen.width,(1-course.TargetCourseCenter.y)*Screen.height);
            if(s.ShowDebugLines){Line(neutral,target,Color.yellow);Line(target,current,Color.cyan);}
            Marker(neutral,"N",Color.white);Marker(target,"Target",Color.yellow);Marker(current,"Current",Color.cyan);
            for(var i=0;i<c.Layers.Length;i++)
            {
                var layer=c.Layers[i];if(!layer.Settings.Enabled||layer.Settings.Kind==SpaceLayerKind.ExternalMusicRings)continue;
                var p=ScreenPoint(layer.CurrentEffectiveVanishingPoint);var tint=Color.Lerp(Color.blue,Color.magenta,layer.Settings.Depth);
                if(s.ShowDebugLines)Line(current,p,tint);
                Marker(p,(i+1).ToString(),tint,(i%4)*13);
            }
#endif
        }
        private Vector2 ScreenPoint(Vector2 world){var p=c.ViewCamera.WorldToScreenPoint(world);return new Vector2(p.x,Screen.height-p.y);}
        private static void Marker(Vector2 p,string label,Color color,float dy=0){var old=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(p.x-3,p.y-3,6,6),Texture2D.whiteTexture);GUI.Label(new Rect(p.x+5,p.y+dy,100,22),label);GUI.color=old;}
        private static void Line(Vector2 a,Vector2 b,Color color){var old=GUI.color;var matrix=GUI.matrix;GUI.color=color;GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a);GUI.DrawTexture(new Rect(a.x,a.y,(b-a).magnitude,1),Texture2D.whiteTexture);GUI.matrix=matrix;GUI.color=old;}
        private static void Slider(string label,ref float value,float min,float max){GUILayout.Label(label+": "+value.ToString("0.00"));value=GUILayout.HorizontalSlider(value,min,max);}
        private static void IntSlider(string label,ref int value,int min,int max){GUILayout.Label(label+": "+value);value=Mathf.RoundToInt(GUILayout.HorizontalSlider(value,min,max));}
    }
}
