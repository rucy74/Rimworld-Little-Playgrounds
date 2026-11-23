using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace LittlePlaygrounds;

[DefOf]
public static class PlaygroundSocialDefOf
{
    public static JobDef LP_WaitForPlayground;
    static PlaygroundSocialDefOf()=>DefOfHelper.EnsureInitializedInCtor(typeof(PlaygroundSocialDefOf));
}

// Called by our own learning/recreation selectors, after their ordinary eligibility gate.
// Nobody here issues an order to a different pawn or patches a vanilla activity.
public static class PlaygroundSocial
{
    public const int MaxQueue=2,MaxWaitTicks=600,RetryTicks=1200,InviteRadius=12;
    internal static int Now=>Find.TickManager.TicksGame;

    public static Job TryJoin(Pawn p,IEnumerable<ThingDef> allowed=null,Thing chosen=null)
    {
        if(!Eligible(p))return null;
        var social=p.Map.GetComponent<PlaygroundSocialMap>();
        var invitation=social.InvitationTarget(p);
        var best=PlaygroundSocialAdapter.Facilities(p.Map,allowed)
            .Where(t=>(chosen==null||t==chosen)&&PlaygroundSocialAdapter.Invitable(t)&&Near(p,t,InviteRadius)
                &&t.Faction==p.Faction&&HasTurn(p,t)&&PlaygroundSocialAdapter.CanUse(t,p)
                &&PlaygroundSocialAdapter.Host(t,p)!=null)
            .OrderBy(t=>t==invitation?0:1)
            .ThenBy(t=>(t.Position-p.Position).LengthHorizontalSquared).FirstOrDefault();
        if(best==null)return null;
        social.ConsumeInvitation(p,best);
        return PlaygroundSocialAdapter.JobFor(best,p);
    }

    // Only a fallback: the caller must try an immediately usable alternative first.
    // Generating a candidate job does not register or reserve a place in the queue.
    public static Job TryWait(Pawn p,IEnumerable<ThingDef> allowed=null)
    {
        if(!Eligible(p)||!p.Map.GetComponent<PlaygroundSocialMap>().MayQueue(p))return null;
        foreach(var t in PlaygroundSocialAdapter.Facilities(p.Map,allowed)
            .Where(t=>Near(p,t,20)&&PlaygroundSocialAdapter.QueueSupported(t))
            .OrderBy(t=>(p.Position-t.Position).LengthHorizontalSquared))
        {
            if(!CompPlaygroundAccess.Allows(t,p)||!PlaygroundSocialAdapter.Busy(t)||!PlaygroundSocialAdapter.ApproachClear(t,p)
                ||Queue(t).Count>=MaxQueue)continue;
            var cell=FindWaitingCell(p,t);
            if(cell.IsValid)return JobMaker.MakeJob(PlaygroundSocialDefOf.LP_WaitForPlayground,t,cell);
        }
        return null;
    }

    // Apply this to automatic direct selections as well, so newcomers cannot jump the queue.
    // Explicit player orders remain authoritative and keep their existing CanUse behaviour.
    public static bool HasTurn(Pawn p,Thing facility)
    {
        var queue=Queue(facility);
        return queue.Count==0||queue[0].pawn==p;
    }
    public static IReadOnlyList<Pawn> WaitingPawns(Thing facility)=>Queue(facility).Select(d=>d.pawn).ToArray();
    internal static List<JobDriver_WaitForPlayground> Queue(Thing facility)
        =>facility?.Spawned!=true?new():facility.Map.mapPawns.AllPawnsSpawned
            .Select(p=>p.jobs?.curDriver).OfType<JobDriver_WaitForPlayground>()
            .Where(d=>d.Facility==facility&&d.Live)
            .OrderBy(d=>d.QueueSequence).ThenBy(d=>d.pawn.thingIDNumber).ToList();

    internal static bool Eligible(Pawn p,bool checkVisitCooldown=true,bool needsFun=true)
        =>!PlaygroundToddlers.Walking(p)&&p?.Spawned==true&&!p.Dead&&!p.Drafted&&!p.Downed&&!p.InMentalState
            &&p.Faction!=null&&PlaygroundUse.CanParticipate(p)
            &&(p.mindState?.enemyTarget?.Spawned!=true)
            &&(p.timetable?.CurrentAssignment??TimeAssignmentDefOf.Anything).allowJoy
            &&!PawnUtility.WillSoonHaveBasicNeed(p,.1f)
            &&(!needsFun||(p.DevelopmentalStage.Child()?p.needs.learning.CurLevel<.9f:p.needs.joy.CurLevel<.9f))
            &&(!checkVisitCooldown||p.Map.GetComponent<PlaygroundMap>().Ready(p));
    internal static bool IdleForInvitation(Pawn p)=>Eligible(p)&&p.CurJob?.playerForced!=true
        &&(p.CurJob==null||p.CurJobDef==JobDefOf.Wait||p.CurJobDef==JobDefOf.Wait_Wander||p.CurJobDef==JobDefOf.GotoWander);
    internal static bool Near(Pawn p,Thing t,int radius)=>(p.Position-t.Position).LengthHorizontalSquared<=radius*radius;
    internal static bool ClearWaitingCell(Pawn p,IntVec3 cell)=>p?.Spawned==true&&cell.IsValid&&cell.InBounds(p.Map)
        &&cell.Standable(p.Map)&&!cell.IsForbidden(p)&&cell.GetDangerFor(p,p.Map)==Danger.None
        // A running child (PlaygroundRunning) is only passing over the spot.
        &&!cell.GetThingList(p.Map).Any(t=>t is Fire||t is Building||t is Pawn other&&other!=p&&other.jobs?.curDriver is not JobDriver_RunAround);
    static IntVec3 FindWaitingCell(Pawn p,Thing facility)
    {
        var rect=facility.OccupiedRect();
        // Side positions lie outside the equipment footprint and its boarding/exit cells.
        for(int step=2;step<=3;step++)
        foreach(int side in new[]{-1,1})
        foreach(int dz in new[]{0,-1,1})
        {
            var cell=new IntVec3(side<0?rect.minX-step:rect.maxX+step,0,facility.Position.z+dz);
            if(!ClearWaitingCell(p,cell)||!p.CanReserve(cell)||!p.CanReach(cell,PathEndMode.OnCell,Danger.None))continue;
            bool boarding=PlaygroundSocialAdapter.Facilities(p.Map).Any(t=>t.OccupiedRect().Contains(cell)
                // A turned ride (PlaygroundTurn) is stepped off away from its entrance on its own side, not always south.
                ||PlaygroundSocialAdapter.Entries(t).Any(e=>e==cell||e+IntVec3.South==cell||e+PlaygroundTurn.Facing(t,Rot4.South).FacingCell==cell));
            if(!boarding)return cell;
        }
        return IntVec3.Invalid;
    }
}

// No independently serialized queue: the exact active driver is the source of truth.
// Its deadline, admission sequence and job targets survive save/load; interrupted or removed pawns disappear
// from the queue immediately, and pooled Job objects cannot become duplicate entries.
public sealed class JobDriver_WaitForPlayground:JobDriver
{
    int queueStarted=-1;
    long queueSequence;
    bool finished;
    public int QueueStarted=>queueStarted;
    public long QueueSequence=>queueSequence;
    public Thing Facility=>job.targetA.Thing;
    public IntVec3 WaitingCell=>job.targetB.Cell;
    public int Elapsed=>queueStarted<0?0:PlaygroundSocial.Now-queueStarted;
    public bool Live=>!finished&&queueStarted>=0&&Elapsed<PlaygroundSocial.MaxWaitTicks
        &&pawn?.Spawned==true&&pawn.jobs.curDriver==this&&Facility?.Spawned==true
        &&pawn.Map==Facility.Map&&!pawn.Drafted&&!pawn.Downed&&!pawn.InMentalState&&CompPlaygroundAccess.Allows(Facility,pawn);
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        if(!CompPlaygroundAccess.Allows(Facility,pawn)||!PlaygroundSocial.Eligible(pawn)||!pawn.Map.GetComponent<PlaygroundSocialMap>().MayQueue(pawn)
            ||!PlaygroundSocialAdapter.QueueSupported(Facility)||!PlaygroundSocialAdapter.ApproachClear(Facility,pawn)
            ||PlaygroundSocial.Queue(Facility).Count(d=>d!=this)>=PlaygroundSocial.MaxQueue
            ||!PlaygroundSocial.ClearWaitingCell(pawn,WaitingCell)
            ||!pawn.Reserve(WaitingCell,job,1,-1,null,errorOnFailed))return false;
        if(queueStarted<0)
        {
            var social=pawn.Map.GetComponent<PlaygroundSocialMap>();
            queueStarted=PlaygroundSocial.Now;queueSequence=social.NextQueueSequence();social.QueueAdmissions++;
        }
        return true;
    }
    public override void ExposeData()
    {
        base.ExposeData();Scribe_Values.Look(ref queueStarted,"lpQueueStarted",-1);
        Scribe_Values.Look(ref queueSequence,"lpQueueSequence",0L);
        Scribe_Values.Look(ref finished,"lpQueueFinished",false);
    }
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOn(()=>!Live||!PlaygroundSocial.Eligible(pawn)
            ||!PlaygroundSocial.ClearWaitingCell(pawn,WaitingCell));
        AddFinishAction(_=>
        {
            if(finished)return;finished=true;
            if(pawn.Spawned)pawn.Map.GetComponent<PlaygroundSocialMap>().FinishedWaiting(pawn);
        });
        yield return Toils_Goto.GotoCell(TargetIndex.B,PathEndMode.OnCell);
        var wait=ToilMaker.MakeToil("WaitBesidePlayground");
        wait.initAction=()=>pawn.pather.StopDead();
        wait.tickIntervalAction=delta=>
        {
            pawn.rotationTracker.FaceTarget(Facility);
            if(!GenTicks.IsTickIntervalDelta(pawn.thingIDNumber,15,delta))return;
            if(!PlaygroundSocialAdapter.ApproachClear(Facility,pawn))
            {EndJobWith(JobCondition.Incompletable);return;}
            if(!PlaygroundSocial.HasTurn(pawn,Facility)||!PlaygroundSocialAdapter.CanUse(Facility,pawn))return;
            var next=PlaygroundSocialAdapter.JobFor(Facility,pawn);
            PlaygroundRecords.Tag(next,PlaySource.Queue,"LP_Rec_FromQueue".Translate((Elapsed/60f).ToString("0")));
            if(next==null){EndJobWith(JobCondition.Incompletable);return;}
            pawn.Map.GetComponent<PlaygroundSocialMap>().QueueBoardings++;
            // Only replace this pawn's own wait job, synchronously; the real ride makes
            // all of its normal reservations. A queue never reserves a seat itself.
            pawn.jobs.StartJob(next,JobCondition.Succeeded,tag:JobTag.SatisfyingNeeds,preToilReservationsCanFail:true);
        };
        wait.handlingFacing=true;wait.socialMode=RandomSocialMode.Normal;
        wait.defaultCompleteMode=ToilCompleteMode.Never;
        yield return wait;
    }
}

public sealed class PlaygroundSocialMap:MapComponent
{
    sealed class Invitation { public Pawn host;public Thing facility;public int expires; }
    readonly Dictionary<Pawn,Invitation> invitations=new();
    Dictionary<Pawn,int> nextOffer=new(),nextQueue=new(),nextCall=new();
    List<Pawn> offerKeys,queueKeys,callKeys;List<int> offerValues,queueValues,callValues;
    int nextPoll;
    long nextQueueSequence=1;
    // Companion calls recorded by a child's decision this tick (PlaygroundCompanion); never saved, gone the same tick.
    internal readonly List<PlaygroundCompanion.Call> calls=new();
    public int CallsMade,CallsStarted,CallsDropped;
    public int OffersCreated {get;private set;}
    public int JoinJobsOffered {get;private set;}
    public int QueueAdmissions,QueueBoardings;
    public int InvitationCount=>invitations.Count;
    public PlaygroundSocialMap(Map map):base(map){}
    internal long NextQueueSequence()=>nextQueueSequence++;
    public bool MayQueue(Pawn p)=>!nextQueue.TryGetValue(p,out int tick)||PlaygroundSocial.Now>=tick;
    public void FinishedWaiting(Pawn p)=>nextQueue[p]=PlaygroundSocial.Now+PlaygroundSocial.RetryTicks;
    // Companion calls keep their own cooldown, so a call does not silence this component's invitations (and back).
    public bool MayCall(Pawn p)=>!nextCall.TryGetValue(p,out int tick)||PlaygroundSocial.Now>=tick;
    public int CallCooldownLeft(Pawn p)=>nextCall.TryGetValue(p,out int tick)?System.Math.Max(0,tick-PlaygroundSocial.Now):0;
    internal void MarkCall(Pawn p)=>nextCall[p]=PlaygroundSocial.Now+PlaygroundSocial.RetryTicks;
    internal bool Calling(Pawn companion)=>calls.Any(c=>c.companion==companion);
    internal bool CallPending(Pawn caller,Thing site)=>calls.Any(c=>c.caller==caller&&c.site==site);
    internal bool CallingLover(Pawn caller,Thing site)=>calls.Any(c=>c.caller==caller&&c.site==site&&c.couple);
    internal void Record(PlaygroundCompanion.Call call){calls.RemoveAll(c=>c.caller==call.caller);calls.Add(call);CallsMade++;}
    public Thing InvitationTarget(Pawn p)
    {
        if(!invitations.TryGetValue(p,out var offer))return null;
        if(!Valid(p,offer)){invitations.Remove(p);return null;}
        return offer.facility;
    }
    internal void ConsumeInvitation(Pawn p,Thing chosen)
    {JoinJobsOffered++;if(invitations.TryGetValue(p,out var offer)&&offer.facility==chosen)invitations.Remove(p);}
    bool Valid(Pawn p,Invitation offer)=>p?.Spawned==true&&p.Map==map&&offer.host?.Spawned==true
        &&offer.facility?.Spawned==true&&offer.host.Map==map&&offer.host.Faction==p.Faction
        &&PlaygroundSocial.Now<offer.expires&&PlaygroundSocial.Near(p,offer.facility,PlaygroundSocial.InviteRadius)
        &&PlaygroundSocialAdapter.Occupants(offer.facility).Contains(offer.host)
        &&PlaygroundSocialAdapter.CanUse(offer.facility,p);
    public override void MapComponentTick()
    {
        int now=PlaygroundSocial.Now;
        if(calls.Count>0)PlaygroundCompanion.Process(this,calls);
        if(now<nextPoll)return;nextPoll=now+120;
        foreach(var p in invitations.Keys.ToArray())if(!Valid(p,invitations[p]))invitations.Remove(p);
        foreach(var t in PlaygroundSocialAdapter.Facilities(map).Where(PlaygroundSocialAdapter.Invitable))
        {
            var host=PlaygroundSocialAdapter.Host(t,null);
            if(host==null||t.Faction!=host.Faction||nextOffer.TryGetValue(host,out int until)&&now<until)continue;
            var recipient=map.mapPawns.FreeColonistsSpawned.Where(p=>p!=host&&p.Faction==host.Faction
                &&!invitations.ContainsKey(p)&&(!nextOffer.TryGetValue(p,out int tick)||now>=tick)
                &&PlaygroundSocial.Near(p,t,PlaygroundSocial.InviteRadius)&&PlaygroundSocial.IdleForInvitation(p)
                &&PlaygroundSocial.HasTurn(p,t)&&PlaygroundSocialAdapter.CanUse(t,p))
                .OrderByDescending(p=>host.relations?.OpinionOf(p)??0)
                .ThenBy(p=>(p.Position-host.Position).LengthHorizontalSquared).FirstOrDefault();
            if(recipient==null)continue;
            invitations[recipient]=new Invitation{host=host,facility=t,expires=now+480};
            nextOffer[host]=nextOffer[recipient]=now+1200;OffersCreated++;
            if(host.Position.ShouldSpawnMotesAt(map))
            {
                // Own quiet native bubble; do not clear another mod's thoughts or add a social memory.
                var bubble=(MoteBubble)ThingMaker.MakeThing(ThingDefOf.Mote_Speech);
                bubble.SetupMoteBubble(InteractionDefOf.Chitchat.GetSymbol(),null);bubble.Attach(host);
                GenSpawn.Spawn(bubble,host.Position,map);
            }
        }
        Prune(nextOffer,now);Prune(nextQueue,now);Prune(nextCall,now);
    }
    static void Prune(Dictionary<Pawn,int> table,int now)
    {foreach(var p in table.Keys.Where(p=>p==null||p.Destroyed||table[p]<=now).ToArray())table.Remove(p);}
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref nextQueueSequence,"lpNextQueueSequence",1L);
        Scribe_Collections.Look(ref nextOffer,"lpSocialOfferCooldown",LookMode.Reference,LookMode.Value,ref offerKeys,ref offerValues);
        Scribe_Collections.Look(ref nextQueue,"lpSocialQueueCooldown",LookMode.Reference,LookMode.Value,ref queueKeys,ref queueValues);
        Scribe_Collections.Look(ref nextCall,"lpCompanionCallCooldown",LookMode.Reference,LookMode.Value,ref callKeys,ref callValues);
        if(Scribe.mode==LoadSaveMode.PostLoadInit)
        {nextOffer??=new();nextQueue??=new();nextCall??=new();invitations.Clear();calls.Clear();nextPoll=0;}
        // Invitations expire on load. They never own reservations or modify a pawn's current job.
    }
}

[StaticConstructorOnStartup]
internal static class PlaygroundSocialAdapter
{
    static readonly ThingDef[] defs=DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.thingClass!=null&&
        (d.thingClass==typeof(Building_Swing)||d.thingClass==typeof(Building_Slide)||d.thingClass==typeof(Building_Seesaw)
            ||d.thingClass==typeof(Building_Sandbox)||d.thingClass==typeof(Building_ActiveRide)
            ||d.thingClass==typeof(Building_PlaygroundFacility))).ToArray();
    internal static IEnumerable<Thing> Facilities(Map map,IEnumerable<ThingDef> allowed=null)
    {
        foreach(var def in allowed??defs)
        foreach(var t in map.listerThings.ThingsOfDef(def))if(QueueSupported(t))yield return t;
    }
    internal static bool QueueSupported(Thing t)=>t is Building_Swing or Building_Slide or Building_Seesaw or Building_Sandbox or Building_ActiveRide
        ||t is Building_PlaygroundFacility f&&f.Kind is ExpansionKind.SpringRider or ExpansionKind.Roundabout or ExpansionKind.Playhouse or ExpansionKind.Hopscotch or ExpansionKind.SplashPad or ExpansionKind.SharedTrampoline or ExpansionKind.CompactSlide or ExpansionKind.FloorRocker or ExpansionKind.BallPit or ExpansionKind.BubbleTub or ExpansionKind.PullBar or ExpansionKind.Kite or ExpansionKind.JumpRope;
    internal static bool Invitable(Thing t)=>t is Building_Swing or Building_Seesaw or Building_Sandbox
        ||t is Building_PlaygroundFacility f&&f.Kind is ExpansionKind.Roundabout or ExpansionKind.Playhouse or ExpansionKind.SharedTrampoline or ExpansionKind.FloorRocker or ExpansionKind.BallPit or ExpansionKind.BubbleTub or ExpansionKind.PullBar or ExpansionKind.Kite or ExpansionKind.JumpRope;
    internal static int Capacity(Thing t)=>t switch
    {Building_Swing or Building_Seesaw=>2,Building_GardenSeats s=>s.SeatCount,Building_PlaygroundFacility f=>f.Capacity,_=>1};
    internal static IEnumerable<Pawn> Occupants(Thing t)
    {
        for(int i=0;i<Capacity(t);i++)
        {
            Pawn p=t switch
            {
                Building_Swing b=>b.JobAt(i)?.pawn,Building_Slide b=>b.RiderJob?.pawn,
                Building_Seesaw b=>b.JobAt(i)?.pawn,Building_GardenSeats b=>b.JobAt(i)?.pawn,
                Building_PlaygroundFacility b=>b.VisitAt(i)?.pawn,Building_ActiveRide b=>b.RiderJob?.pawn,_=>null
            };
            if(p!=null)yield return p;
        }
    }
    internal static Pawn Host(Thing t,Pawn recipient)=>Occupants(t).FirstOrDefault(p=>p!=recipient
        &&(recipient==null||p.Faction==recipient.Faction)&&PlaygroundSocial.Eligible(p,false)
        &&p.Faction!=null&&(p.jobs.curDriver switch
        {
            JobDriver_RideSwing d=>d.Age>=60&&d.Age<SwingMotion.TotalTicks-180,
            JobDriver_RideSeesaw d=>d.Age>=60&&!d.Leaving,
            JobDriver_GardenVisit d=>d.Age>=60&&d.Age<JobDriver_GardenVisit.Duration-180,
            JobDriver_VisitPlayground d=>d.Age>=60&&d.Age<d.Duration-180,_=>false
        }));
    internal static bool Busy(Thing t)=>Occupants(t).Count()>=Capacity(t)||t is Building_ActiveRide {Returning:true};
    internal static bool CanUse(Thing t,Pawn p)=>t switch
    {
        Building_Swing b=>b.CanUse(p),Building_Slide b=>b.CanUse(p),Building_Seesaw b=>b.CanUse(p),
        Building_GardenSeats b=>b.CanUse(p),Building_PlaygroundFacility b=>b.CanUse(p),Building_ActiveRide b=>b.CanUse(p),_=>false
    };
    internal static JobDef JobDefFor(Thing t)=>t switch
    {
        Building_Swing=>LPDefOf.LP_RideSwing,Building_Slide=>SlideDefOf.LP_RideSlide,Building_Seesaw=>SeesawDefOf.LP_RideSeesaw,
        Building_GardenSeats g=>g.Activity,Building_PlaygroundFacility f=>ExpansionJobs.For(f),
        Building_ActiveRide r=>ActiveRideJobs.For(r),_=>null
    };
    internal static Job JobFor(Thing t,Pawn p)
    {
        var def=JobDefFor(t);
        if(def==null)return null;var job=JobMaker.MakeJob(def,t);job.isLearningDesire=p.DevelopmentalStage.Child();return job;
    }
    internal static IEnumerable<IntVec3> Entries(Thing t)
    {
        for(int i=0;i<Capacity(t);i++)yield return t switch
        {
            Building_Swing b=>b.EntryAt(i),Building_Slide b=>b.EntryCell,Building_Seesaw b=>b.EntryAt(i),
            Building_GardenSeats b=>b.EntryAt(i),Building_PlaygroundFacility b=>b.EntryAt(i),Building_ActiveRide b=>b.EntryCell,_=>t.Position
        };
        if(t is Building_Slide slide)yield return slide.ExitCell;
        if(t is Building_PlaygroundFacility {Kind:ExpansionKind.CompactSlide} compact)yield return CompactMotion.SlideExit(compact);
        if(t is Building_PlaygroundFacility {Kind:ExpansionKind.BallPit} pit)yield return CompactMotion.PitEntrance(pit);
        if(t is Building_ActiveRide ride)yield return ride.ExitCell;
    }
    internal static bool ApproachClear(Thing t,Pawn p)
    {
        if(t?.Spawned!=true||t.Destroyed||p?.Spawned!=true||p.Map!=t.Map||t.IsForbidden(p)
            ||t.Faction!=p.Faction)return false;
        if(t is Building_PlaygroundFacility {Kind:ExpansionKind.SplashPad}
            &&(t.Position.GetTemperature(t.Map)<10||t.Map.snowGrid.GetDepth(t.Position)>.1f))return false;
        bool Clear(IntVec3 c)=>c.InBounds(p.Map)&&c.Standable(p.Map)&&!c.IsForbidden(p)
            &&!c.GetThingList(p.Map).Any(thing=>thing is Fire)&&p.CanReach(c,PathEndMode.OnCell,Danger.None);
        if(t is Building_Slide slide)return Clear(slide.EntryCell)&&Clear(slide.ExitCell);
        if(t is Building_PlaygroundFacility {Kind:ExpansionKind.CompactSlide} compact)return Clear(compact.EntryAt(0))&&Clear(CompactMotion.SlideExit(compact));
        if(t is Building_PlaygroundFacility {Kind:ExpansionKind.BallPit} pit)return Clear(CompactMotion.PitEntrance(pit))&&(Clear(pit.EntryAt(0))||Clear(pit.EntryAt(1)));
        if(t is Building_ActiveRide ride)return ride.ClearRidePath(p)&&Clear(ride.EntryCell)&&Clear(ride.ExitCell);
        return Entries(t).Any(Clear);
    }
}
