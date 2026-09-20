using UnityEngine;
namespace OrbitalRift {
public sealed class ConfiguredAttackView:MonoBehaviour {
LineRenderer[] lines;Material material;
public void Render(Enemy enemy,bool casting,Vector2 orbitCenter){
 var a=enemy.ActiveAbility;
 if(lines==null){material=new Material(Shader.Find("Sprites/Default"));lines=new LineRenderer[8];for(int i=0;i<8;i++){var go=new GameObject("Configured attack "+i);go.transform.SetParent(transform,false);var l=go.AddComponent<LineRenderer>();l.sharedMaterial=material;l.positionCount=2;l.useWorldSpace=true;l.sortingOrder=9;lines[i]=l;}}
 foreach(var l in lines)l.enabled=false;
 if(a==null)return;
 if(a.Behaviour==BossAbilityBehaviour.Beam){int count=a.Beam.Number(enemy.Health/Mathf.Max(1,enemy.MaxHealth));for(int i=0;i<Mathf.Min(8,count);i++){var l=lines[i];l.enabled=true;var angle=(enemy.BossBeamAngle+360f*i/count)*Mathf.Deg2Rad;var direction=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle));l.SetPosition(0,enemy.transform.position+direction*a.Beam.InnerSafeRadius);l.SetPosition(1,enemy.transform.position+direction*a.Beam.Length);l.widthMultiplier=casting?.012f:.08f;a.Style.ApplyLine(l,casting?.35f:1);}}
 if(a.Behaviour==BossAbilityBehaviour.Roots){var l=lines[0];l.enabled=true;l.SetPosition(0,enemy.transform.position);var angle=enemy.BossRootAngle*Mathf.Deg2Rad;l.SetPosition(1,orbitCenter+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*OrbitSettings.Radius);l.widthMultiplier=casting?.012f:.05f;a.Style.ApplyLine(l,casting?.3f:1);}
}
void OnDisable(){if(lines!=null)foreach(var l in lines)l.enabled=false;}
void OnDestroy(){if(material!=null)Destroy(material);}
}}
