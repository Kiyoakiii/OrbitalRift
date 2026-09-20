using UnityEngine;
namespace OrbitalRift
{
    public sealed partial class GameManager
    {
        int configuredSpawnOrdinal;
        EncounterStep CurrentEncounter => GameRules.Current != null ? GameRules.Current.Classic.Step(phase) : null;
        void ConfigureAppearance(Enemy enemy, ActorAppearance appearance)
        {
            if(enemy.AppearanceView==null)enemy.AppearanceView=enemy.gameObject.AddComponent<ActorAppearanceView>();
            enemy.AppearanceView.Configure(appearance,enemy.Renderer);
            enemy.Renderer.enabled=appearance==null||!appearance.Enabled;
        }
        Enemy SpawnConfiguredMob(MobDefinition mob, bool defense)
        {
            var enemy=GetConfiguredEnemy(mob.Prefab);
            enemy.ResetEnemy(mob.Movement,Random.Range(0,Mathf.PI*2),phase,mob.Sprite);
            enemy.Mob=mob; enemy.Health=enemy.MaxHealth=mob.Health;enemy.Points=mob.Points;enemy.Life=mob.Lifetime;
            enemy.Renderer.enabled=true;enemy.Renderer.color=mob.Tint;SetSpriteWorldSize(enemy.Renderer,mob.Size);
            ConfigureAppearance(enemy,mob.Appearance);enemies.Add(enemy);return enemy;
        }
        void TickMobVisualsAndSpells(Enemy enemy,float dt,bool defense)
        {
            var mob=enemy.Mob;if(mob==null)return;
            enemy.VisualAge+=dt;enemy.AppearanceView?.Tick(enemy.VisualAge);
            if(!(defense?mob.ShootInDefense:mob.ShootInClassic))return;
            enemy.SpellTimer-=dt;
            if(enemy.ActiveAbility==null && enemy.SpellTimer<=0 && mob.Spells.Length>0)
            {
                var spell=mob.Spells[enemy.SpellIndex++%mob.Spells.Length];
                if(spell!=null&&!spell.SandboxOnly&&spell.Behaviour!=BossAbilityBehaviour.RebirthEgg) {BeginBossAbility(enemy,spell,1,false);enemy.SpellTimer=Mathf.Max(.05f,spell.Cooldown);ConfigureAppearance(enemy,spell.Appearance.Enabled?spell.Appearance:mob.Appearance);}
            }
            var a=enemy.ActiveAbility;if(a==null)return;
            enemy.AbilityAge+=dt;
            if(enemy.AbilityAge>=a.CastDelay+a.Duration){enemy.ActiveAbility=null;ConfigureAppearance(enemy,mob.Appearance);enemy.GetComponent<ConfiguredAttackView>()?.Render(enemy,false,ShipOrbitCenter);return;}
            bool casting=enemy.AbilityAge<a.CastDelay;
            if(!casting&&!enemy.AbilityActivated){enemy.AbilityActivated=true;if(a.Behaviour==BossAbilityBehaviour.Summon)SpawnHarrierClones(enemy,a.Summon.Count,a);if(a.Behaviour==BossAbilityBehaviour.Roots)TryApplyGravroot(enemy);spellVfxPool?.EmitImpact(enemy.transform.position,a.CastPrefab,a.Vfx);}
            if(a.Behaviour==BossAbilityBehaviour.Beam){if(casting){var dir=(Vector2)player.position-(Vector2)enemy.transform.position;enemy.BossBeamAngle=Mathf.Atan2(dir.y,dir.x)*Mathf.Rad2Deg;}else{enemy.BossBeamAngle+=dt*a.Beam.AngularSpeed;if(enemy.FireTimer<=0){TryDamagePlayerWithBossBeam(enemy);enemy.FireTimer=a.Beam.HitInterval;}}}
            else if(!casting&&a.Shot!=null&&enemy.FireTimer<=0){FireConfiguredBossShot(enemy,a.Shot);enemy.FireTimer=a.Shot.FireInterval;}
            if(a.Behaviour==BossAbilityBehaviour.Dash)MoveConfiguredBoss(enemy,casting&&a.UseCastMovement?a.CastMovement:a.Movement,dt);
            var view=enemy.GetComponent<ConfiguredAttackView>()??enemy.gameObject.AddComponent<ConfiguredAttackView>();view.Render(enemy,casting,ShipOrbitCenter);
        }

        void MoveConfiguredMob(Enemy e,int index,float dt)
        {
            var m=e.Mob;
            if(m.Movement==EnemyKind.Scout)e.Radius=Mathf.Min(m.Radius,e.Radius+dt*BalanceSettings.EnemyMovementMultiplier(phase));
            else if(m.Movement==EnemyKind.Spiral)e.Radius=m.Radius+Mathf.Sin(Time.time*m.RadialFrequency+index)*m.RadialAmplitude;
            else if(m.Movement==EnemyKind.Diver){e.Radius=m.Radius+Mathf.PingPong(Time.time*m.RadialFrequency+index,m.RadialAmplitude);e.Angle=Mathf.LerpAngle(e.Angle*Mathf.Rad2Deg,playerAngle*Mathf.Rad2Deg,dt*m.TrackingSpeed)*Mathf.Deg2Rad;}
            else e.Radius=m.Radius;
            if(m.Movement!=EnemyKind.Diver)e.Angle+=dt*m.AngularSpeed;
            e.transform.position=new Vector2(Mathf.Cos(e.Angle),Mathf.Sin(e.Angle))*e.Radius;
            if(e.ActiveAbility==null&&e.FireTimer<=0&&m.ShootInClassic&&m.Shot!=null){FireConfiguredBossShot(e,m.Shot);e.FireTimer=m.Shot.FireInterval*BalanceSettings.EnemyFireInterval(phase)/BalanceSettings.EnemyFireInterval(1);}
            TickMobVisualsAndSpells(e,dt,false);
        }
    }
}
