using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LittlePlaygrounds;

public static class PlaygroundCompanion
{
    public const int SpareTicks=400,HoldTicks=600;
    const int LeastOpinion=-20,ListedNames=4;
    const float WalkFactor=1.3f,SearchRadius=45f;

    public enum Kind{Friend,Care,Watch,Recreation}

    internal sealed class Call
    {
        public Pawn caller,companion;public Thing site;public Kind kind;public int callerJob;public bool run,couple;
    }

    static int Now=>Find.TickManager.TicksGame;
    // The child's spawned free colonists. Not MapPawns.FreeColonistsSpawned: that is one shared list the game clears and
    // refills on every call, and this runs inside job decisions that an outer loop over that list may have started.
    static List<Pawn> Colonists(Pawn child)=>child.Map.mapPawns.SpawnedPawnsInFaction(child.Faction)
        .Where(p=>p.RaceProps.Humanlike&&p.HostFaction==null&&!p.IsSubhuman).ToList();
    internal static string Hours(int ticks)=>(ticks/(float)GenDate.TicksPerHour).ToString("0.#");

    // A seesaw within reach where someone already waits for a partner: go there first (before calling a friend).
    public static Job TryLonePartner(Pawn child)
    {
        var seesaw=child.Map.listerThings.ThingsOfDef(SeesawDefOf.LP_Seesaw).OfType<Building_Seesaw>()
            .Where(s=>s.Faction==child.Faction&&(s.Position-child.Position).LengthHorizontal<=SearchRadius&&s.HasWaitingPartner(child)
                &&PlaygroundSocial.HasTurn(child,s)&&s.CanUse(child)&&PlaygroundSocialAdapter.ApproachClear(s,child))
            .OrderBy(s=>(s.Position-child.Position).LengthHorizontalSquared).FirstOrDefault();
        if(seesaw==null)return null;
        return JobMaker.MakeJob(SeesawDefOf.LP_RideSeesaw,seesaw);
    }

    // An idle friend child and a shared ride for both. Returns the calling child's job (its friend follows this tick).
    // why (play log only): filled with the reasons when no friend comes.
    public static Job TryFriend(Pawn child,List<string> why=null,Thing chosen=null)
    {
        var social=child.Map.GetComponent<PlaygroundSocialMap>();
        if(!social.MayCall(child)){why?.Add("LP_Rec_CallerCooldown".Translate(Hours(social.CallCooldownLeft(child))));return null;}
        var sites=(chosen==null?PlaygroundSocialAdapter.Facilities(child.Map):new[]{chosen}).Where(t=>PlaygroundSocialAdapter.Invitable(t)&&MorePlay.SecondSeatOpen(t)
                &&t.Faction==child.Faction&&(t.Position-child.Position).LengthHorizontal<=SearchRadius
                &&PlaygroundSocialAdapter.Occupants(t).Count()+2<=PlaygroundSocialAdapter.Capacity(t))
            .OrderBy(t=>(t.Position-child.Position).LengthHorizontalSquared).ToList();
        var children=Colonists(child).Where(p=>p!=child&&p.DevelopmentalStage.Child()).ToList();
        if(sites.Count==0||children.Count==0)
        {why?.Add((sites.Count==0?"LP_Rec_NoFriendSite":"LP_Rec_NoChildren").Translate());return null;}
        var friends=children.Where(p=>Why(p,Kind.Friend,child,social)==null).ToList();
        foreach(var site in sites)
        {
            if(friends.Count==0)break;
            // The queue lists every waiting pawn on the map: build it once per ride.
            var queue=PlaygroundSocial.Queue(site);
            bool Turn(Pawn p)=>queue.Count==0||queue[0].pawn==p;
            if(!Turn(child)||!PlaygroundSocialAdapter.CanUse(site,child)||!PlaygroundSocialAdapter.ApproachClear(site,child))continue;
            var friend=friends.Where(f=>WhyAtSite(f,Kind.Friend,child,site,Turn(f))==null).OrderBy(f=>WalkTicks(f,site)).FirstOrDefault();
            if(friend==null)continue;
            var job=PlaygroundSocialAdapter.JobFor(site,child);
            if(job==null)continue;
            social.Record(new Call{caller=child,companion=friend,site=site,kind=Kind.Friend,callerJob=job.loadID});
            PlaygroundRecords.Note(job,"LP_Rec_Called".Translate(friend.LabelShort,("LP_Rec_Kind_"+Kind.Friend).Translate()),friend);
            return job;
        }
        if(why!=null)
        {
            var site=sites[0];
            why.Add("LP_Rec_Friends".Translate(string.Join(", ",children.Take(ListedNames).Select(f=>f.LabelShort+" "
                +(Why(f,Kind.Friend,child,social)??WhyAtSite(f,Kind.Friend,child,site,PlaygroundSocial.HasTurn(f,site))??"LP_Why_NoSeat".Translate().ToString())))));
        }
        return null;
    }

    public static bool CallForRun(Pawn child,Thing site,Job job,List<string> why=null)
    {
        var social=child.Map.GetComponent<PlaygroundSocialMap>();
        if(!social.MayCall(child)){why?.Add("LP_Rec_CallerCooldown".Translate(Hours(social.CallCooldownLeft(child))));return false;}
        Pawn companion=null;var kind=Kind.Friend;
        if(PlaygroundSocialAdapter.Invitable(site)&&PlaygroundSocialAdapter.Capacity(site)>=2)
            companion=Colonists(child).Where(p=>p!=child&&p.DevelopmentalStage.Child()&&Why(p,Kind.Friend,child,social)==null
                &&WalkTicks(p,site)<=WalkTicks(child,site)+SpareTicks&&PlaygroundRunning.CanRun(p,site))
                .OrderBy(p=>WalkTicks(p,site)).FirstOrDefault();
        if(companion==null&&LittlePlaygroundsMod.Settings.childcarePlay){kind=Kind.Watch;companion=Fastest(child,site,kind,social);}
        if(companion==null)return false;
        social.Record(new Call{caller=child,companion=companion,site=site,kind=kind,callerJob=job.loadID,run=kind==Kind.Friend});
        PlaygroundRecords.Note(job,"LP_Rec_Called".Translate(companion.LabelShort,("LP_Rec_Kind_"+kind).Translate()),companion);
        return true;
    }

    // After the ordinary choice: who comes along to this ride, if anyone.
    public static bool CallFor(Pawn child,Thing site,Job job,List<string> why=null)
    {
        if(site?.Spawned!=true||job==null)return false;
        var social=child.Map.GetComponent<PlaygroundSocialMap>();
        if(!social.MayCall(child)){why?.Add("LP_Rec_CallerCooldown".Translate(Hours(social.CallCooldownLeft(child))));return false;}
        Pawn companion=null;Kind kind;
        var tried=new List<Kind>();
        if(PlaygroundSocialAdapter.Invitable(site))
        {
            // Somebody is already there or on the way: the child plays with them.
            if(PlaygroundSocialAdapter.Capacity(site)<2||PlaygroundSocialAdapter.Occupants(site).Any(p=>p!=child))
            {why?.Add("LP_Rec_SomeoneThere".Translate());return false;}
            if(!MorePlay.SecondSeatOpen(site)){why?.Add("LP_Why_NoSeat".Translate());return false;}
            kind=Kind.Care;
            if(LittlePlaygroundsMod.Settings.childcarePlay&&PlaygroundCare.JoinFor(site)!=null){tried.Add(kind);companion=Fastest(child,site,kind,social);}
            // Two can use it but a carer has no part in it (the pull-up bar): a Childcare adult comes to watch, as on a solo ride.
            else if(LittlePlaygroundsMod.Settings.childcarePlay&&PlaygroundCare.Watchable(site)){kind=Kind.Watch;tried.Add(kind);companion=Fastest(child,site,kind,social);}
            if(companion==null){kind=Kind.Recreation;tried.Add(kind);companion=Fastest(child,site,kind,social);}
        }
        else if(PlaygroundCare.Watchable(site)&&LittlePlaygroundsMod.Settings.childcarePlay)
        {kind=Kind.Watch;tried.Add(kind);companion=Fastest(child,site,kind,social);}
        else
        {if(PlaygroundCare.Watchable(site))why?.Add("LP_Why_CareSettingOff".Translate());return false;}
        if(companion!=null)
        {
            social.Record(new Call{caller=child,companion=companion,site=site,kind=kind,callerJob=job.loadID});
            PlaygroundRecords.Note(job,"LP_Rec_Called".Translate(companion.LabelShort,("LP_Rec_Kind_"+kind).Translate()),companion);
            return true;
        }
        if(why==null)return false;
        var adults=Colonists(child).Where(p=>p.DevelopmentalStage==DevelopmentalStage.Adult).ToList();
        if(adults.Count==0){why.Add("LP_Rec_NoAdults".Translate());return false;}
        // One line per adult: the reason for the last group tried (recreation after Childcare, or watching).
        why.Add("LP_Rec_Adults".Translate(string.Join(", ",adults.Take(ListedNames).Select(p=>p.LabelShort+" "
            +string.Join(" / ",tried.Select(k=>Why(p,k,child,social)??WhyAtSite(p,k,child,site,PlaygroundSocial.HasTurn(p,site))
                ??"LP_Why_NoSeat".Translate().ToString()).Distinct())))));
        return false;
    }

    static Pawn Fastest(Pawn child,Thing site,Kind kind,PlaygroundSocialMap social,bool notPartners=false)
        =>Colonists(child).Where(p=>p!=child&&p.DevelopmentalStage==DevelopmentalStage.Adult&&!(notPartners&&PlaygroundCouples.IsPartner(child,p))&&Why(p,kind,child,social)==null)
            .OrderBy(p=>WalkTicks(p,site))
            .FirstOrDefault(p=>WhyAtSite(p,kind,child,site,PlaygroundSocial.HasTurn(p,site))==null);

    // Adults may bring a partner too. Reuse the ordinary idle/recreation gates, never Childcare, for adult-to-adult calls.
    // The query is pure; the actual call is recorded only for the chosen job and still confirmed at MapComponentTick.
    public static bool CanStartAdultSeesaw(Pawn adult,Building_Seesaw site)=>site?.CanUse(adult)==true;
    public static bool CallAdultSeesaw(Pawn adult,Building_Seesaw site,Job job)
    {
        var social=adult.Map.GetComponent<PlaygroundSocialMap>();
        if(!social.MayCall(adult))return false;
        // Whether a love partner comes is CallLover's decision, made just before this one.
        var partner=Fastest(adult,site,Kind.Recreation,social,true);if(partner==null)return false;
        social.Record(new Call{caller=adult,companion=partner,site=site,kind=Kind.Recreation,callerJob=job.loadID});
        PlaygroundRecords.Note(job,"LP_Rec_Called".Translate(partner.LabelShort,("LP_Rec_Kind_"+Kind.Recreation).Translate()),partner);
        return true;
    }

    // An adult on a date (PlaygroundCouples): on a ride or bench two can share, the love partner comes first, if free.
    public static bool CallLover(Pawn adult,Thing site,Job job)
    {
        var lover=PlaygroundCouples.Lover(adult);
        if(lover?.Spawned!=true||lover.Map!=adult.Map||!PlaygroundCouples.Shareable(site))return false;
        var social=adult.Map.GetComponent<PlaygroundSocialMap>();
        if(!LoverFree(adult,lover)||!LoverCanCome(adult,lover,site))return false;
        social.Record(new Call{caller=adult,companion=lover,site=site,kind=Kind.Recreation,callerJob=job.loadID,couple=true});
        PlaygroundRecords.Note(job,"LP_Rec_Called".Translate(lover.LabelShort,("LP_Rec_Kind_"+Kind.Recreation).Translate()),lover);
        return true;
    }

    // This seat suits both. WhyAtSite: the lover arrives in time. The other way round too: a lover there long before the
    // caller would give up and play alone.
    internal static bool LoverCanCome(Pawn adult,Pawn lover,Thing site)=>WalkTicks(adult,site)<=WalkTicks(lover,site)+SpareTicks
        &&WhyAtSite(lover,Kind.Recreation,adult,site,PlaygroundSocial.HasTurn(lover,site))==null;

    // Never in high danger (Romance On The Rim's rule): nobody leaves anything for a date in a raid.
    internal static bool LoverFree(Pawn adult,Pawn lover)
    {
        var social=adult.Map.GetComponent<PlaygroundSocialMap>();
        return adult.Map.dangerWatcher.DangerRating!=StoryDanger.High&&social.MayCall(adult)&&Why(lover,Kind.Recreation,adult,social,true)==null;
    }

    // Site-specific reasons, after Why: in time, the ride's own rules and seats, a spot to watch from.
    static string WhyAtSite(Pawn p,Kind kind,Pawn child,Thing site,bool hasTurn)
    {
        int spare=kind==Kind.Watch?RideTicks(site)/2:SpareTicks;
        float walk=WalkTicks(p,site),limit=WalkTicks(child,site)+spare;
        if(walk>limit)return "LP_Why_TooFar".Translate(Seconds(walk),Seconds(limit));
        if(kind is Kind.Friend or Kind.Recreation&&!MayEnjoy(p,site))return "LP_Why_NotAllowedFun".Translate();
        if(kind==Kind.Watch)return PlaygroundCare.TryWatchCell(p,site,out _)?null:"LP_Why_NoWatchSpot".Translate().ToString();
        if(kind is Kind.Friend or Kind.Recreation&&!hasTurn||!PlaygroundSocialAdapter.CanUse(site,p)||!PlaygroundSocialAdapter.ApproachClear(site,p))
            return "LP_Why_NoSeat".Translate();
        return null;
    }
    static string Seconds(float ticks)=>(ticks/60f).ToString("0");

    // The ride's own recreation rules (e.g. a royal title that forbids this kind of fun), as JoyGiver.CanBeGivenTo checks.
    internal static bool MayEnjoy(Pawn p,Thing site)=>PlaygroundSocialAdapter.JobDefFor(site)?.joyKind?.PawnCanDo(p)!=false;

    // Straight-line walk estimate; real paths are usually a little longer than the straight line.
    static float WalkTicks(Pawn p,Thing site)=>(p.Position-site.Position).LengthHorizontal*WalkFactor*p.TicksPerMoveCardinal;
    // One ride on a solo ride, for a watcher's margin (the shortest course visit for stepping-stone courses).
    static int RideTicks(Thing site)=>site switch
    {
        Building_Slide=>SlideMotion.TotalTicks,
        Building_ActiveRide r=>r.Kind==ActiveRideKind.Zipline?ActiveRideMotion.ZiplineTicks:ActiveRideMotion.TrampolineTicks,
        Building_PlaygroundFacility f when f.IsCourse=>420,
        _=>1080
    };

    // Not busy with anything: no job, or a wait/wander/mod idle pastime (IdleJob_*) the pawn's own think tree gave it.
    // A Wait without a job giver was put on the pawn by someone else (PawnUtility.ForceWait: being carried, held for a
    // ritual or an operation) and is not idleness.
    static bool Idle(Pawn p)=>p.CurJob==null||p.CurJob.jobGiver!=null&&(p.CurJobDef==JobDefOf.Wait||p.CurJobDef==JobDefOf.Wait_Wander
        ||p.CurJobDef==JobDefOf.GotoWander||p.CurJobDef.defName.StartsWith("IdleJob_"));

    // The first reason this pawn cannot be called as a companion of this kind (null: it can be). Site-free reasons only;
    // WhyAtSite adds the ride's. A friend child only needs free time (an idle child whose learning is met may come too).
    internal static string Why(Pawn p,Kind kind,Pawn child,PlaygroundSocialMap social,bool date=false)
    {
        if(p?.Spawned!=true||p.Dead||p.Map!=child.Map||p.Faction!=child.Faction)return "LP_Why_Other".Translate();
        if(p.Drafted)return "LP_Why_Drafted".Translate();
        if(p.Downed)return "LP_Why_Downed".Translate();
        if(p.InMentalState)return "LP_Why_Mental".Translate();
        if(p.IsBurning())return "LP_Why_Burning".Translate();
        if(p.CurJob?.playerForced==true)return "LP_Why_Ordered".Translate();
        if(!Idle(p)&&!(date&&PlaygroundCouples.CanLeaveForDate(p,child)))return "LP_Why_Busy".Translate(p.CurJobDef?.label??"?");
        if(p.GetLord()!=null)return "LP_Why_Gathering".Translate();
        if(p.mindState?.enemyTarget?.Spawned==true)return "LP_Why_Fighting".Translate();
        if(PawnUtility.WillSoonHaveBasicNeed(p,.1f))return "LP_Why_Needs".Translate();
        var hour=p.timetable?.CurrentAssignment??TimeAssignmentDefOf.Anything;
        var map=p.Map.GetComponent<PlaygroundMap>();
        switch(kind)
        {
            case Kind.Friend:
            case Kind.Recreation:
                if(!PlaygroundUse.CanParticipate(p))return "LP_Why_Other".Translate();
                if(!hour.allowJoy)return "LP_Why_NotPlayTime".Translate();
                if(kind==Kind.Recreation&&!date&&p.needs.joy.CurLevel>=.9f)return "LP_Why_JoyFull".Translate();
                if(!map.Ready(p))return "LP_Why_PlayCooldown".Translate(Hours(map.TicksUntilReady(p)));
                break;
            default:
                // Childcare is work: an adult with it switched on, in a work or anything hour; no recreation condition.
                if(!LittlePlaygroundsMod.Settings.childcarePlay)return "LP_Why_CareSettingOff".Translate();
                if(!PlaygroundUse.CanParticipate(p))return "LP_Why_Other".Translate();
                if(hour!=TimeAssignmentDefOf.Work&&hour!=TimeAssignmentDefOf.Anything)return "LP_Why_NotWorkHour".Translate();
                if(p.WorkTypeIsDisabled(WorkTypeDefOf.Childcare))return "LP_Why_CannotChildcare".Translate();
                if(p.workSettings?.WorkIsActive(WorkTypeDefOf.Childcare)!=true)return "LP_Why_ChildcareOff".Translate();
                break;
        }
        if(!social.MayCall(p))return "LP_Why_CallCooldown".Translate(Hours(social.CallCooldownLeft(p)));
        if(social.Calling(p))return "LP_Why_BeingCalled".Translate();
        if(kind==Kind.Friend)
        {
            int mine=child.relations?.OpinionOf(p)??0,theirs=p.relations?.OpinionOf(child)??0;
            if(mine<LeastOpinion||theirs<LeastOpinion)return "LP_Why_Dislike".Translate(System.Math.Min(mine,theirs));
        }
        return null;
    }

    // Map component tick: start each companion whose caller is on its ride job now. A call is recorded and its job
    // started in the same think, before this runs, so a call whose job is not running was never started: drop it.
    internal static void Process(PlaygroundSocialMap social,List<Call> calls)
    {
        var due=calls.ToArray();calls.Clear();
        foreach(var c in due)
        {
            if(c.caller?.Spawned==true&&c.caller.CurJob?.loadID==c.callerJob&&Start(social,c))social.CallsStarted++;
            else social.CallsDropped++;
        }
    }

    static bool Start(PlaygroundSocialMap social,Call c)
    {
        Pawn p=c.companion,child=c.caller;var site=c.site;
        if(site?.Spawned!=true||site.Map!=social.map||PlaygroundCare.WatchSite(child)!=site||Why(p,c.kind,child,social,c.couple)!=null)return false;
        Job job;PlaySource source;
        switch(c.kind)
        {
            case Kind.Watch:
                // Another adult may have taken this child's care in the meantime (work tab, same tick).
                if(PlaygroundCare.CaredForByOther(child,p)||!PlaygroundCare.TryWatchCell(p,site,out var cell))return false;
                job=JobMaker.MakeJob(CareDefOf.LP_WatchChildPlay,site,child,cell);source=PlaySource.WatchCall;break;
            case Kind.Care:
                var def=PlaygroundCare.JoinFor(site);
                if(def==null||PlaygroundCare.CaredForByOther(child,p)||!PlaygroundSocialAdapter.CanUse(site,p)||!PlaygroundSocialAdapter.ApproachClear(site,p))return false;
                job=JobMaker.MakeJob(def,site,child);source=PlaySource.CareCall;break;
            default:
                if(c.run)
                {
                    job=PlaygroundRunning.TryCreate(p,site);if(job==null)return false;
                    source=PlaySource.FriendCall;break;
                }
                if(!PlaygroundSocial.HasTurn(p,site)||!MayEnjoy(p,site)||!PlaygroundSocialAdapter.CanUse(site,p)||!PlaygroundSocialAdapter.ApproachClear(site,p))return false;
                job=PlaygroundSocialAdapter.JobFor(site,p);if(job==null)return false;
                job.SetTarget(TargetIndex.B,child);   // whom this ride is for: the companion waits for that child
                source=c.kind==Kind.Friend?PlaySource.FriendCall:PlaySource.RecreationCall;break;
        }
        PlaygroundRecords.Tag(job,source,"LP_Rec_CalledBy".Translate(child.LabelShort));
        p.jobs.StartJob(job,JobCondition.InterruptForced,tag:c.kind is Kind.Care or Kind.Watch?JobTag.MiscWork:JobTag.SatisfyingNeeds,
            preToilReservationsCanFail:true);
        if(p.CurJob!=job)return false;
        // Keep the caller's job identity on the follower's saved driver. A Job object can be pooled/reused.
        if(c.run)JobDriver_RunAround.Pair(child,p,c.callerJob);
        else if(c.kind is Kind.Friend or Kind.Recreation)BindCaller(p.jobs.curDriver,c.callerJob);
        social.MarkCall(child);social.MarkCall(p);
        if(child.Position.ShouldSpawnMotesAt(child.Map))
        {
            var bubble=(MoteBubble)ThingMaker.MakeThing(ThingDefOf.Mote_Speech);
            bubble.SetupMoteBubble(InteractionDefOf.Chitchat.GetSymbol(),null);bubble.Attach(child);
            GenSpawn.Spawn(bubble,child.Position,child.Map);
        }
        return true;
    }

    public static bool HoldAtEntrance(JobDriver driver,Thing site,ref int since)
    {
        var p=driver.pawn;
        if(site?.Spawned!=true)return false;
        bool child=p.DevelopmentalStage.Child();
        var partner=child?null:driver.job.targetB.Thing as Pawn??PlaygroundCouples.ComingLover(p,site);
        bool callingLover=!child&&partner==null&&site.Map.GetComponent<PlaygroundSocialMap>().CallingLover(p,site);
        if(!child&&partner==null&&!callingLover)return false;
        if(!PlaygroundSocialAdapter.Invitable(site)&&(child||site is not Building_GardenBench))return false;
        int now=Now;if(since<0)since=now;
        int left=HoldTicks-(now-since);
        bool Walking(Pawn o)=>(child||o==partner)&&o.jobs.curDriver.CurToilIndex<=0&&o.pather.Moving;
        var others=PlaygroundSocialAdapter.Occupants(site).Where(o=>o!=p).ToList();
        string outcome=null;bool wait=false;
        var riding=others.Where(o=>Riding(o.jobs.curDriver)).ToList();
        if(left<=0)outcome="LP_Rec_HoldCap".Translate();
        else if(riding.Count>0)outcome="LP_Rec_HoldRiding".Translate();
        else
        {
            var coming=others.Where(o=>Walking(o)&&WalkTicks(o,site)<=left).ToList();
            wait=coming.Count>0||callingLover||child&&site.Map.GetComponent<PlaygroundSocialMap>().CallPending(p,site);
            if(wait)PlaygroundRecords.Awaiting(driver.job,coming);
            else outcome=others.FirstOrDefault(Walking) is {} late?"LP_Rec_HoldLate".Translate(late.LabelShort).ToString():"LP_Rec_HoldArrived".Translate().ToString();
        }
        if(wait)p.rotationTracker.FaceCell(site.Position);
        else if(now>since)PlaygroundRecords.Held(driver.job,now-since,outcome,riding);
        return wait;
    }
    static bool Riding(JobDriver d)=>d switch
    {
        JobDriver_RideSwing s=>s.Age>=0,JobDriver_RideSeesaw s=>s.Age>=0,JobDriver_GardenVisit g=>g.Age>=0,
        JobDriver_VisitPlayground v=>v.Age>=0,_=>false
    };

    static void BindCaller(JobDriver driver,int callerJob)
    {
        switch(driver)
        {
            case JobDriver_RideSwing d:d.CompanionCallerJob=callerJob;break;
            case JobDriver_RideSeesaw d:d.CompanionCallerJob=callerJob;break;
            case JobDriver_GardenVisit d:d.CompanionCallerJob=callerJob;break;
            case JobDriver_VisitPlayground d:d.CompanionCallerJob=callerJob;break;
        }
    }

    // Friend/recreation followers cancel only before their first mount. Once playing, finish the visit normally.
    // Old saves have no caller job ID; their targetB still lets us stop if the caller has left the ride.
    public static bool ShouldStopBeforeMount(JobDriver driver,int callerJob,bool hasMounted)
    {
        if(hasMounted||PlaygroundCare.IsCare(driver.job.def)||driver.job.targetB.Thing is not Pawn child)return false;
        return child.Dead||!child.Spawned||child.Map!=driver.pawn.Map||child.Drafted||child.Downed||child.InMentalState
            ||PlaygroundCare.PlaySite(child)!=driver.job.targetA.Thing
            ||callerJob>=0&&child.CurJob?.loadID!=callerJob;
    }
}
