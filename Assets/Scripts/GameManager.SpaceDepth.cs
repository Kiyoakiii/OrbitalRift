using UnityEngine;
namespace OrbitalRift
{
    public partial class GameManager
    {
        private SpaceFlightVisualController spaceDepth;
        private SpaceLayerDebugPanel spaceDepthPanel;
        public SpaceFlightVisualController SpaceDepth => spaceDepth;
        public Vector2 ShipOrbitCenter => spaceDepth!=null&&spaceDepth.RuntimeProfile.Steering.SteeringEnabled&&!coopPlaying&&!defensePlaying&&!soloExpeditionPlaying
            ?spaceDepth.CourseOffset*spaceDepth.RuntimeProfile.Steering.OrbitCenterFollowStrength:Vector2.zero;
        private bool CreateDepthSpaceBackdrop()
        {
            if(!Application.isPlaying)return false;
            var profile=Resources.Load<SpaceDepthProfile>("SpaceDepth/Profiles/SpaceDepthProfile");
            if(profile==null)return false;
            if(spaceDepth!=null)return true;
            spaceBackdrop=new GameObject("SpaceDepthRoot").transform;
            spaceDepth=spaceBackdrop.gameObject.AddComponent<SpaceFlightVisualController>();
            spaceDepth.Initialize(gameCamera,profile);spaceDepthPanel=new SpaceLayerDebugPanel(spaceDepth);return true;
        }
        private void TickDepthSpace(float dt,PlayerCommandFrame command)
        {
            if(spaceDepth==null)return;
            var active=(abilitySandbox!=null&&abilitySandbox.IsOpen&&!abilitySandbox.VfxEditorOpen)||(playing&&!showResults&&!coopPlaying&&!defensePlaying&&!soloExpeditionPlaying);
            if(!active)spaceDepth.Course.ResetInstant();
            else spaceDepth.TickCourse(command.CourseDirection,dt,spaceDepthPanel.BlocksInput||showSettings||showMenu||!Application.isFocused);
            spaceDepth.SetGameplaySpeed(spaceTravelSpeed);
            spaceDepth.Tick(dt,abilitySandbox==null||!abilitySandbox.VfxEditorOpen);
            musicReactiveVisuals?.SetCourseCenter(spaceDepth.CurrentVanishingPoint);
        }
        private void BindCourseInput()
        {
            if(spaceDepth!=null&&playerCommandSource is LocalPlayerCommandSource local)
            {
                local.CourseBindings=spaceDepth.RuntimeProfile.Steering;
                local.CourseControlsActive=!coopPlaying&&!defensePlaying&&!soloExpeditionPlaying;
            }
        }
        private bool DrawDepthSpacePanel()
        {
            spaceDepthPanel?.DrawDebug();
            if(spaceDepthPanel==null||abilitySandbox==null||!abilitySandbox.IsOpen||abilitySandbox.VfxEditorOpen)return false;
            return spaceDepthPanel.Draw();
        }
    }
}
