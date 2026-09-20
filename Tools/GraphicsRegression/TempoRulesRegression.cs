using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OrbitalRift;

public static class TempoRulesRegression
{
    public static void Run(Action<bool,string> check)
    {
        check(TempoRewardRules.ChanceBasisPoints(16500,30000,0)==5500,"Tempo 55 percent");
        check(TempoRewardRules.ChanceBasisPoints(23250,30000,0)==3500,"Tempo 35 percent");
        check(TempoRewardRules.ChanceBasisPoints(30000,30000,0)==1500,"Tempo 15 percent");
        check(TempoRewardRules.ChanceBasisPoints(int.MaxValue,30000,2)==10000,"Pity guaranteed");
        check(TempoRewardRules.ChanceBasisPoints(0,int.MaxValue,1)==6500,"No integer overflow");
        int previous=10000;
        for(int time=0;time<=60000;time++)
        {
            int chance=TempoRewardRules.ChanceBasisPoints(time,30000,0);
            check(chance<=previous && chance>=1500 && chance<=5500,"Monotonic chance"); previous=chance;
        }
        check(TempoRewardRules.Draw(0,1,1,0)==2433279855u,"Known answer zero seed");
        check(TempoRewardRules.Draw(-17,5,2,0)==1581206037u,"Known answer negative seed");
        check(TempoRewardRules.Draw(42,2,1,0xB5297A4Du)==726471462u,"Known answer option stream");
        for(int seed=0;seed<1000;seed++)
        {
            var a=new TempoRewardState("test",seed);var b=new TempoRewardState("test",seed);
            for(int node=0;node<6;node++)
            {
                check(a.BeginEncounter(node,SectorRoomType.Combat,30000),"Begin eligible encounter");
                check(b.BeginEncounter(node,SectorRoomType.Combat,30000),"Repro begin");
                a.AdvanceCombat(30000,false,true);b.AdvanceCombat(30000,false,true);
                var x=a.FinishEncounter(node,true,true);var y=b.FinishEncounter(node,true,true);
                check(x.RollBasisPoints==y.RollBasisPoints && x.First==y.First && x.Second==y.Second,"Stable rewards");
                check(a.ConsecutiveMisses<=2,"Never more than two consecutive misses");
                check(ReferenceEquals(x,a.FinishEncounter(node,true,true)),"Duplicate clear returns same immutable outcome");
                if(x.Granted)
                {
                    check(x.First!=x.Second && TempoRewardRules.Valid(x.First) && TempoRewardRules.Valid(x.Second),"Two distinct valid choices");
                    check(!a.BeginEncounter(node+1,SectorRoomType.Combat,30000),"Pending reward blocks next fight");
                    check(!a.Choose("wrong",x.First),"Wrong reward ID rejected");
                    check(a.Choose(x.RewardId,x.First) && b.Choose(y.RewardId,y.First),"Choose exactly once");
                    check(!a.Choose(x.RewardId,x.First),"Repeated choice rejected");
                }
                check(a.ModuleCount<=2,"Slot limit");
            }
            check(a.FinishEncounter(0,true,true).NodeId==0,"Old replay stays old, not just last ID");
        }

        var clock=new TempoRewardState("clock",0);
        check(!clock.BeginEncounter(0,SectorRoomType.Shop,30000),"Shop ineligible");
        check(!clock.BeginEncounter(0,SectorRoomType.Start,30000),"Start ineligible");
        check(!clock.BeginEncounter(0,SectorRoomType.Event,30000),"Event ineligible");
        clock.AdvanceCombat(1000,false,true);check(clock.CombatMilliseconds==0,"No out-of-combat time");
        clock.BeginEncounter(1,SectorRoomType.Boss,30000);
        check(!clock.BeginEncounter(1,SectorRoomType.Boss,30000),"Duplicate begin cannot reset clock or shield");
        clock.AdvanceCombat(1200,true,true);clock.AdvanceCombat(1200,false,false);clock.AdvanceCombat(321,false,true);
        check(clock.CombatMilliseconds==321,"Only active vulnerable combat counts");
        check(clock.FinishEncounter(1,true,false)==null && clock.Failed && clock.Pending==null && clock.ConsecutiveMisses==0,"Failure priority, no pity/reward");
        check(!clock.BeginEncounter(2,SectorRoomType.Combat,30000),"Terminal state cannot resume combat");

        foreach(TempoModule module in new[]{TempoModule.RapidFire,TempoModule.FastPlasma,TempoModule.ReserveCapacitor})
        {
            var state=WithModule(module);check(state.ModuleAt(0).RemainingCombats==2,"New module not spent on awarding fight");
            check(!state.BeginEncounter(20,SectorRoomType.Shop,30000) && state.ModuleAt(0).RemainingCombats==2,"Shop does not expire module");
            for(int fight=0;fight<2;fight++)
            {
                // Award outcomes after these fights are resolved normally, but choose another type.
                check(state.BeginEncounter(100+fight,SectorRoomType.Combat,30000),"Next encounter");
                var stats=WeaponStatsResolver.ResolveTempo(.3f,10f,state);
                check(Math.Abs(stats.FireInterval-(state.Has(TempoModule.RapidFire)?.3f/1.12f:.3f))<.00001f,"Fire stat");
                check(Math.Abs(stats.ProjectileSpeed-(state.Has(TempoModule.FastPlasma)?11.8f:10f))<.00001f,"Projectile stat");
                for(int i=0;i<100;i++)check(WeaponStatsResolver.ResolveTempo(.3f,10f,state).FireInterval==stats.FireInterval,"No cumulative stat drift");
                check(WeaponStatsResolver.ResolveTempo(.001f,10f,state).FireInterval==.08f,"Experimental fire interval floor");
                if(module==TempoModule.ReserveCapacitor)
                {
                    check(!state.TryBlockDamage(1) && state.ReserveChargeAvailable,"Ordinary shields first");
                    check(state.TryBlockDamage(0) && !state.TryBlockDamage(0),"One block per fight");
                }
                state.AdvanceCombat(60000,false,true);
                var reward=state.FinishEncounter(100+fight,true,true);
                check(state.Has(module)==(fight==0),"Exactly two subsequent fights");
                if(reward.Granted)state.Choose(reward.RewardId,reward.First==module?reward.Second:reward.First);
            }
        }
        // Explicit restored-slot fixture anticipates M4: full slots require visible replacement.
        var full=PendingSuccess();
        var slots=(List<TempoModuleSlot>)typeof(TempoRewardState).GetField("modules",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(full);
        var choice=full.Pending.First;
        foreach(TempoModule type in new[]{TempoModule.RapidFire,TempoModule.FastPlasma,TempoModule.ReserveCapacitor})
            if(type!=choice)slots.Add(new TempoModuleSlot(type,1));
        check(!full.Choose(full.Pending.RewardId,choice),"Full slots cannot silently replace");
        var replacing=slots[0].Module;
        check(full.Choose(full.Pending.RewardId,choice,replacing) && full.Has(choice) && !full.Has(replacing) && full.ModuleCount==2,"Explicit replacement");

        var refresh=PendingSuccess();var refreshChoice=refresh.Pending.First;
        var refreshSlots=(List<TempoModuleSlot>)typeof(TempoRewardState).GetField("modules",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(refresh);
        refreshSlots.Add(new TempoModuleSlot(refreshChoice,1));
        check(refresh.Choose(refresh.Pending.RewardId,refreshChoice) && refresh.ModuleCount==1 && refresh.ModuleAt(0).RemainingCombats==2,"Refresh to two, not add two");

        // M4 serializes a pending deterministic reward, including earlier misses, rather than
        // rolling again after the process is restarted.
        var pending=PendingSuccess("resume");
        var tempoCheckpoint=pending.CreateCheckpoint();
        var restoredTempo=TempoRewardState.Restore(tempoCheckpoint);
        check(restoredTempo.Pending != null && restoredTempo.Pending.RewardId==pending.Pending.RewardId &&
              restoredTempo.Pending.First==pending.Pending.First,"Pending reward survives state restore");
        var route=new LivingCosmosRunState();route.Initialize(tempoCheckpoint.seed);route.OpenInitialNavigation();
        check(route.TryChoose(1),"Resume fixture first route edge");
        check(route.Tick(StarStreamSettings.JumpDuration+.01f,0,5,0,false,false,false),"Resume fixture reaches combat room");
        route.EnterRoom(1);route.Tick(.01f,4,5,0,false,false,false);
        check(route.BeginReward() && route.Phase==LivingEncounterPhase.Reward,"Reward phase blocks route advance");
        var savedRoute=route.CreateCheckpoint();var restoredRoute=new LivingCosmosRunState();restoredRoute.Restore(savedRoute);
        check(restoredRoute.Phase==LivingEncounterPhase.Reward && restoredRoute.RoomIndex==1 && restoredRoute.Cleared.Contains(0),"Reward route survives restore");
        var fileCheckpoint=new LivingCosmosCheckpoint {
            seed=tempoCheckpoint.seed, runId="resume", route=savedRoute, tempo=tempoCheckpoint, teamHealth=4,
            fireIntervalMilli=1000, projectileSpeedMilli=1000, damageBonus=2, prismLevel=1, aegisCharges=0,
            fieldRepairLevel=0, aegisLevel=0
        };
        var folder=Path.Combine("Results","tempo-checkpoint");Directory.CreateDirectory(folder);
        var store=new LivingCosmosCheckpointStore(folder);
        check(store.Save(fileCheckpoint),"Atomic pending checkpoint write");
        check(store.TryLoad(out var loaded) && loaded.runId=="resume" && loaded.route.phase==(int)LivingEncounterPhase.Reward,"Pending checkpoint read");
        File.WriteAllText(Path.Combine(folder,"orbital-rift-living-cosmos-v1.json"),"corrupted primary");
        check(store.TryLoad(out loaded) && loaded.integrity!=null && loaded.runId=="resume","Backup recovers corrupted primary");
    }
    static TempoRewardState PendingSuccess(string runId="fixture")
    {
        for(int seed=0;seed<10000;seed++)
        {
            var s=new TempoRewardState(runId,seed);s.BeginEncounter(1,SectorRoomType.Combat,30000);
            if(s.FinishEncounter(1,true,true).Granted)return s;
        }
        throw new Exception("No reward fixture");
    }
    static TempoRewardState WithModule(TempoModule module)
    {
        for(int seed=0;seed<10000;seed++)
        {
            var s=new TempoRewardState("module",seed);s.BeginEncounter(1,SectorRoomType.Combat,30000);
            var reward=s.FinishEncounter(1,true,true);
            if(reward.Granted && (reward.First==module || reward.Second==module)) { s.Choose(reward.RewardId,module);return s; }
        }
        throw new Exception("No module fixture");
    }
}
