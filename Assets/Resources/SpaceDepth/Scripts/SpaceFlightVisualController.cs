using UnityEngine;

namespace OrbitalRift
{
    public sealed class SpaceFlightVisualController : MonoBehaviour
    {
        public SpaceDepthProfile SavedProfile { get; private set; }
        public SpaceDepthProfile RuntimeProfile { get; private set; }
        public SpaceDepthLayer[] Layers { get; private set; }
        public Vector2 CurrentVanishingPoint { get; private set; }
        public CourseController Course { get; private set; }
        public Vector2 NeutralCourseCenter => initialCamera;
        public Vector2 CourseOffset => Course.WorldCourseCenter-initialCamera;
        public bool PauseMotion;
        public int SoloLayer=-1;
        public bool FollowGameplaySpeed=true;
        private float travelMultiplier=1,gameplayMultiplier=1;
        private Vector2 initialCamera;
        private Camera view;
        public Camera ViewCamera => view;
        public void ReloadArtwork(){foreach(var layer in Layers)layer.ReloadArtwork();}
        public int ActiveElements { get { var n=0;foreach(var l in Layers)n+=l.ActiveCount;return n; } }
        public int ActiveLargeObjects { get { var n=0;foreach(var l in Layers)if(l.Settings.Kind==SpaceLayerKind.Planet||l.Settings.Kind==SpaceLayerKind.Galaxy||l.Settings.Kind==SpaceLayerKind.Asteroids||l.Settings.Kind==SpaceLayerKind.Nebula)n+=l.ActiveCount;return n; } }
        public void Initialize(Camera camera,SpaceDepthProfile profile)
        {
            view=camera;initialCamera=camera.transform.position;CurrentVanishingPoint=initialCamera;
            SavedProfile=profile;RuntimeProfile=Instantiate(profile);RuntimeProfile.name="SpaceDepth (runtime copy)";
            if(RuntimeProfile.Steering==null)RuntimeProfile.Steering=new CourseSteeringSettings();
            if(RuntimeProfile.Nebula==null)RuntimeProfile.Nebula=new SpaceNebulaSettings();
            Course=new CourseController(camera,initialCamera,OrbitSettings.Radius,RuntimeProfile.Steering);
            Layers=new SpaceDepthLayer[profile.Layers.Length];
            for(var i=0;i<Layers.Length;i++)
            {
                var s=RuntimeProfile.Layers[i];
                var go=new GameObject((i+1).ToString("00")+" "+s.DisplayName);go.transform.SetParent(transform,false);
                Layers[i]=go.AddComponent<SpaceDepthLayer>();Layers[i].Initialize(s,i);
                if(s.Kind==SpaceLayerKind.Nebula)Layers[i].SetNebulaSettings(RuntimeProfile.Nebula);
            }
        }
        public void TickCourse(Vector2 direction,float dt,bool blocked=false)
        {Course.Settings=RuntimeProfile.Steering;Course.Tick(direction,dt,blocked);CurrentVanishingPoint=Course.WorldCourseCenter;}
        public float GetTravelSpeed()=>Mathf.Max(0,RuntimeProfile.GlobalTravelSpeed)*travelMultiplier*(FollowGameplaySpeed?gameplayMultiplier:1);
        public void SetTravelSpeed(float value){RuntimeProfile.GlobalTravelSpeed=Mathf.Clamp(value,0,30);}
        public void SetTravelSpeedMultiplier(float value){travelMultiplier=Mathf.Clamp(value,0,20);}
        public void ResetTravelSpeed(){RuntimeProfile.GlobalTravelSpeed=SavedProfile.GlobalTravelSpeed;travelMultiplier=1;}
        public void SetGameplaySpeed(float speed){gameplayMultiplier=Mathf.Max(0,speed);}
        public void Tick(float dt,bool visible=true)
        {
            if(Layers==null)return;
            var cameraOffset=(Vector2)view.transform.position-initialCamera;
            CurrentVanishingPoint=Course.WorldCourseCenter;
            for(var i=0;i<Layers.Length;i++)
            {
                Layers[i].SetSteering(initialCamera,CurrentVanishingPoint,RuntimeProfile.Steering.GlobalSteeringStrength,RuntimeProfile.Steering.SteeringEnabled,PauseMotion?0:dt);
                Layers[i].Tick(PauseMotion?0:dt,GetTravelSpeed(),initialCamera,cameraOffset,visible&&(SoloLayer<0||SoloLayer==i));
            }
        }
        public void ShowAll(bool show){SoloLayer=-1;foreach(var s in RuntimeProfile.Layers)s.Enabled=show&&s.Kind!=SpaceLayerKind.ExternalMusicRings;}
        public void ResetLayer(int i)
        {
            RuntimeProfile.Layers[i]=SavedProfile.Layers[i].Copy();Layers[i].SetSettings(RuntimeProfile.Layers[i]);
            if(RuntimeProfile.Layers[i].Kind==SpaceLayerKind.Nebula)
            {
                RuntimeProfile.Nebula=(SavedProfile.Nebula??new SpaceNebulaSettings()).Copy();
                Layers[i].SetNebulaSettings(RuntimeProfile.Nebula);
            }
        }
        public void ResetAll()
        {
            for(var i=0;i<Layers.Length;i++)ResetLayer(i);
            RuntimeProfile.Nebula=(SavedProfile.Nebula??new SpaceNebulaSettings()).Copy();
            for(var i=0;i<Layers.Length;i++)if(RuntimeProfile.Layers[i].Kind==SpaceLayerKind.Nebula)Layers[i].SetNebulaSettings(RuntimeProfile.Nebula);
            RuntimeProfile.Steering=(SavedProfile.Steering??new CourseSteeringSettings()).Copy();Course.Settings=RuntimeProfile.Steering;Course.AutoTest=CourseAutoTest.Off;Course.ResetInstant();SoloLayer=-1;ResetTravelSpeed();PauseMotion=false;
        }
        public void SaveProfile()
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(SavedProfile,"Save Space Depth Profile");
            UnityEditor.EditorUtility.CopySerialized(RuntimeProfile,SavedProfile);
            SavedProfile.name="SpaceDepthProfile";
            UnityEditor.EditorUtility.SetDirty(SavedProfile);UnityEditor.AssetDatabase.SaveAssets();
#endif
        }
        private void OnDestroy()
        {
            if(RuntimeProfile!=null)Destroy(RuntimeProfile);
        }
    }
}
