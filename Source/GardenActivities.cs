using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LittlePlaygrounds;

[DefOf]
public static class GardenDefOf
{
    public static ThingDef LP_Sandbox,LP_GardenBench,LP_ShadeCanopy,LP_OakCurb,LP_IvoryCurb;
    public static JobDef LP_PlayInSand,LP_RelaxOnBench;
    public static AnimationDef LP_GardenPose;
    public static RoofDef LP_FabricShade;
    public static TerrainDef LP_PlaySand,LP_PlaySandGrainy,LP_RubberSage,LP_RubberCream,LP_RubberApricot;
    static GardenDefOf()=>DefOfHelper.EnsureInitializedInCtor(typeof(GardenDefOf));
}

// Seats are owned by the exact activity job, including its approach phase.
// Work, repair and deconstruction jobs never count as occupants.
public abstract class Building_GardenSeats:Building
{
    // Follows a push from Tucked Furniture; zero without it (PlaygroundTuck).
    public override Vector3 DrawPos=>base.DrawPos+PlaygroundTuck.Shift(this);
    List<Pawn> occupants;
    public abstract int SeatCount{get;}
    public abstract JobDef Activity{get;}
    public abstract IntVec3 EntryAt(int slot);
    // Where a visitor walks to after getting up: one cell further out from its entrance (south of a north-facing seat).
    public virtual IntVec3 LeaveAt(int slot)=>EntryAt(slot)+IntVec3.South;
    public abstract Vector3 SeatOffset(int slot);
    public abstract Rot4 Facing(int slot);
    public Pawn PawnAt(int slot)=>occupants!=null&&slot>=0&&slot<occupants.Count?occupants[slot]:null;
    public void SetRider(int slot,Pawn p)
    {
        occupants??=new();while(occupants.Count<SeatCount)occupants.Add(null);
        occupants[slot]=p;
    }
    public JobDriver_GardenVisit JobAt(int slot)
    {
        var p=PawnAt(slot);
        return p?.Spawned==true&&p.Map==Map&&p.jobs.curDriver is JobDriver_GardenVisit&&p.CurJob.targetA.Thing==this
            ?p.jobs.curDriver as JobDriver_GardenVisit:null;
    }
    public int FreeSlot(Pawn p)
    {
        for(int i=0;i<SeatCount;i++)if(PawnAt(i)==p&&JobAt(i)!=null)return i;
        for(int i=0;i<SeatCount;i++)if(JobAt(i)==null&&ClearEntry(i,p)&&PlaygroundAvailability.Entry(p,EntryAt(i))==PlayBlocked.None)return i;
        return -1;
    }
    public bool ClearEntry(int slot,Pawn p)
    {
        if(p?.Spawned!=true||Map==null)return false;
        var c=EntryAt(slot);return c.InBounds(Map)&&c.Standable(Map)&&!c.IsForbidden(p);
    }
    public bool CanUse(Pawn p,bool reserve=true)=>UseFailure(p,reserve)==PlayBlocked.None;
    public PlayBlocked UseFailure(Pawn p,bool reserve=true)
    {
        var failure=PlaygroundAvailability.Common(this,p);if(failure!=PlayBlocked.None)return failure;
        failure=PlayBlocked.Busy;
        for(int i=0;i<SeatCount;i++)
        {
            if(JobAt(i)!=null&&PawnAt(i)!=p)continue;
            failure=PlaygroundAvailability.Entry(p,EntryAt(i));
            if(failure==PlayBlocked.None)return !reserve||p.CanReserve(this,SeatCount,0)?PlayBlocked.None:PlayBlocked.Busy;
        }
        return failure;
    }
    public override void ExposeData(){base.ExposeData();Scribe_Collections.Look(ref occupants,"lpGardenOccupants",LookMode.Reference);}
    public override void DeSpawn(DestroyMode mode=DestroyMode.Vanish)
    {
        for(int i=0;i<SeatCount;i++)JobAt(i)?.EndJobWith(JobCondition.Incompletable);
        occupants?.Clear();base.DeSpawn(mode);
    }
    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn p)
    {
        foreach(var option in base.GetFloatMenuOptions(p))yield return option;
        yield return PlaygroundAvailability.Menu(this is Building_Sandbox?"LP_Menu_BuildSandcastles".Translate():"LP_Menu_RelaxBench".Translate(),UseFailure(p),()=>
            p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Activity,this),JobTag.SatisfyingNeeds));
    }
}

public sealed class Building_Sandbox:Building_GardenSeats
{
    const int CastleWork=1080,TowerWork=450,QuietTicks=20000;
    int work,lastPlayed=-1;
    public override int SeatCount=>3;
    public override JobDef Activity=>GardenDefOf.LP_PlayInSand;
    public int Progress=>work;
    public int Stage=>work>=CastleWork?3:work>=TowerWork?2:work>=80?1:0;
    public int LastPlayed=>lastPlayed;
    public override IntVec3 EntryAt(int slot)=>Position+(slot==0?new IntVec3(-2,0,0):slot==1?new IntVec3(2,0,0):new IntVec3(0,0,-2));
    public override Vector3 SeatOffset(int slot)=>slot==0?new(-1.08f,0,-.05f):slot==1?new(1.08f,0,-.05f):new(0,0,-.98f);
    public override Rot4 Facing(int slot)=>slot==0?Rot4.East:slot==1?Rot4.West:Rot4.North;
    // A finished castle has nothing left to build, so someone starting fresh at it knocks it back to a tower and builds
    // it up again. Joining a child who is already playing keeps the castle as it is.
    public void StartPlay(int slot)
    {
        if(work>=CastleWork&&!Enumerable.Range(0,SeatCount).Any(i=>i!=slot&&JobAt(i)?.Age>=0))work=TowerWork;
    }
    public void Play(int delta)
    {
        work=Math.Min(CastleWork,work+delta);lastPlayed=Find.TickManager.TicksGame;
    }
    public override void TickRare()
    {
        base.TickRare();
        if(work>0&&lastPlayed>=0&&Find.TickManager.TicksGame-lastPlayed>QuietTicks
            &&!Enumerable.Range(0,SeatCount).Any(i=>JobAt(i)!=null))work=Math.Max(0,work-18);
    }
    public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref work,"lpCastleWork",0);Scribe_Values.Look(ref lastPlayed,"lpLastSandPlay",-1);}
    protected override void DrawAt(Vector3 at,bool flip=false)=>GardenArt.Sandbox(this,at);
    public override string GetInspectString()=>PlaygroundInspect.Append(PlaygroundInspect.Append(base.GetInspectString(),(Stage==3?"LP_Inspect_SandCastle":Stage==2?"LP_Inspect_SandTower":Stage==1?"LP_Inspect_SandMound":"LP_Inspect_SandReady").Translate().ToString()),
        PlaygroundRecords.InspectLine(this));
}

public sealed class Building_GardenBench:Building_GardenSeats
{
    public override string GetInspectString()=>PlaygroundInspect.Append(base.GetInspectString(),PlaygroundRecords.InspectLine(this));
    public override int SeatCount=>2;
    public override JobDef Activity=>GardenDefOf.LP_RelaxOnBench;
    // Turnable (PlaygroundTurn): cells and facing follow the turn, the seats come from each turned picture (GardenArt.BenchSeat).
    public override IntVec3 EntryAt(int slot)=>PlaygroundTurn.Cell(this,new IntVec3(slot==0?-1:1,0,-1));
    public override IntVec3 LeaveAt(int slot)=>PlaygroundTurn.Cell(this,new IntVec3(slot==0?-1:1,0,-2));
    public override Vector3 SeatOffset(int slot)=>GardenArt.BenchSeat(this,slot);
    public override Rot4 Facing(int slot)=>PlaygroundTurn.Facing(this,Rot4.South);
    protected override void DrawAt(Vector3 at,bool flip=false)=>GardenArt.Bench(this,at);
}

public sealed class JoyGiver_RelaxOnGardenBench:JoyGiver
{
    public override Job TryGiveJob(Pawn p)
    {
        Building_GardenBench best=null;float distance=45*45;
        foreach(var thing in p.Map.listerThings.ThingsOfDef(GardenDefOf.LP_GardenBench))
        {
            if(thing is not Building_GardenBench bench||!bench.CanUse(p))continue;
            float d=(p.Position-bench.Position).LengthHorizontalSquared;
            if(d<distance){distance=d;best=bench;}
        }
        if(best==null)return null;
        var job=JobMaker.MakeJob(GardenDefOf.LP_RelaxOnBench,best);PlaygroundCompanion.CallLover(p,best,job);PlaygroundRecords.Tag(job,PlaySource.Recreation);return job;
    }
}

public sealed class JobDriver_GardenVisit:JobDriver
{
    public int Slot=-1;
    int start=-1,holdSince=-1;
    public int CompanionCallerJob=-1;
    bool mounted,cleanupDone;
    Building_GardenSeats Seat=>job.targetA.Thing as Building_GardenSeats;
    public int Age=>mounted&&start>=0?Math.Max(0,Find.TickManager.TicksGame-start):-1;
    public const int Duration=1080;
    public float Blend=>Age<0?0:Age<60?SwingMotion.Smooth(Age/60f):Age>Duration-75?1-SwingMotion.Smooth((Age-Duration+75)/75f):1;
    public Vector3 SandReach=>Seat is Building_Sandbox&&Age>=0?PlaygroundMotion.SandReach(Age,Slot)*Blend:Vector3.zero;
    public override Vector3 ForcedBodyOffset=>Age>=0&&Seat?.Spawned==true?
        Seat.Position.ToVector3Shifted()+PlaygroundTuck.Shift(Seat)*Blend+Vector3.Lerp(Seat.EntryAt(Slot).ToVector3Shifted()-Seat.Position.ToVector3Shifted(),Seat.SeatOffset(Slot),Blend)
        +SandReach-pawn.Drawer.tweener.TweenedPos:Vector3.zero;
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        if(Seat==null||!Seat.CanUse(pawn)||!pawn.Reserve(Seat,job,Seat.SeatCount,0,null,errorOnFailed))return false;
        Slot=Seat.FreeSlot(pawn);if(Slot<0)return false;
        Seat.SetRider(Slot,pawn);return true;
    }
    public override void ExposeData()
    {
        base.ExposeData();Scribe_Values.Look(ref Slot,"lpGardenSlot",-1);Scribe_Values.Look(ref start,"lpGardenStart",-1);
        Scribe_Values.Look(ref mounted,"lpGardenMounted",false);
        Scribe_Values.Look(ref cleanupDone,"lpCleanupDone",false);
        Scribe_Values.Look(ref holdSince,"lpEntranceHoldStart",-1);
        Scribe_Values.Look(ref CompanionCallerJob,"lpCompanionCallerJob",-1);
    }
    void ClearVisit()
    {
        if(cleanupDone)return;cleanupDone=true;
        bool played=mounted;
        mounted=false;
        if(Seat!=null&&Slot>=0&&Seat.PawnAt(Slot)==pawn)Seat.SetRider(Slot,null);
        if(pawn.Drawer.renderer.CurAnimation==GardenDefOf.LP_GardenPose)pawn.Drawer.renderer.SetAnimation(null);
        if(pawn.Spawned&&Seat is Building_Sandbox)
        {var visits=pawn.Map.GetComponent<PlaygroundMap>();if(played)visits.Finished(pawn);else visits.Failed(pawn);}
    }
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOn(()=>pawn.Drafted||pawn.Downed||PlaygroundToddlers.StopsPlay(pawn)||Seat==null||Slot<0||!Seat.ClearEntry(Slot,pawn)
            ||(Seat is Building_Sandbox&&!PlaygroundUse.CanParticipate(pawn)));
        this.FailOn(()=>PlaygroundCare.ShouldStop(pawn,job));
        this.FailOn(()=>PlaygroundCompanion.ShouldStopBeforeMount(this,CompanionCallerJob,start>=0));
        AddFinishAction(_=>ClearVisit());if(Seat==null)yield break;
        yield return Toils_Goto.GotoCell(Seat.EntryAt(Slot),PathEndMode.OnCell);
        var visit=ToilMaker.MakeToil("GardenVisit");
        visit.initAction=()=>
        {
            if(Seat.PawnAt(Slot)!=pawn||!Seat.CanUse(pawn,false)){EndJobWith(JobCondition.Incompletable);return;}
            pawn.pather.StopDead();
            if(!PlaygroundCompanion.HoldAtEntrance(this,Seat,ref holdSince))Mount();
        };
        visit.tickIntervalAction=delta=>
        {
            Seat.SetRider(Slot,pawn);
            if(!mounted)
            {
                if(PawnUtility.WillSoonHaveBasicNeed(pawn,-.05f)||!PlaygroundCare.IsCare(job.def)&&!(pawn.timetable?.CurrentAssignment??TimeAssignmentDefOf.Anything).allowJoy)
                {EndJobWith(JobCondition.InterruptOptional);return;}
                if(PlaygroundCompanion.HoldAtEntrance(this,Seat,ref holdSince))return;
                Mount();
            }
            pawn.Rotation=Seat.Facing(Slot);
            if(pawn.Drawer.renderer.CurAnimation==null)pawn.Drawer.renderer.SetAnimation(GardenDefOf.LP_GardenPose);
            if(PawnUtility.WillSoonHaveBasicNeed(pawn,-.05f)||!PlaygroundCare.IsCare(job.def)&&!(pawn.timetable?.CurrentAssignment??TimeAssignmentDefOf.Anything).allowJoy)
            {EndJobWith(JobCondition.InterruptOptional);return;}
            if(Age>=60&&Age<Duration-75)
            {
                if(Seat is Building_Sandbox sand)
                {
                    sand.Play(delta);
                    if(PlaygroundUse.Reward(pawn,delta,sand))return;
                }
                else
                {
                    pawn.needs.comfort?.ComfortUsed(Seat.GetStatValue(StatDefOf.Comfort));
                    if(pawn.DevelopmentalStage==DevelopmentalStage.Adult)PlaygroundCouples.Tick(pawn,Seat);
                    // Children can rest on an ordered bench visit. Sitting is
                    // comfort, not a learning activity, and they have no joy need.
                    if(pawn.needs.joy!=null&&JoyUtility.JoyTickCheckEnd(pawn,delta,PlaygroundCouples.OnDate(pawn,Seat)?JoyTickFullJoyAction.None:JoyTickFullJoyAction.EndJob,joySource:Seat))return;
                }
            }
            if(Age>=Duration)ReadyForNextToil();
        };
        visit.handlingFacing=true;visit.socialMode=RandomSocialMode.SuperActive;
        visit.defaultCompleteMode=ToilCompleteMode.Never;visit.AddFinishAction(ClearVisit);yield return visit;
        yield return Toils_Goto.GotoCell(Seat.LeaveAt(Slot),PathEndMode.OnCell);
        yield return PlaygroundReaction.AfterRide(pawn,()=>Seat is Building_Sandbox);
    }
    void Mount()
    {
        pawn.Notify_Teleported(endCurrentJob:false);start=Find.TickManager.TicksGame;mounted=true;
        if(Seat is Building_Sandbox sand)sand.StartPlay(Slot);
        if(pawn.Drawer.renderer.CurAnimation==null)pawn.Drawer.renderer.SetAnimation(GardenDefOf.LP_GardenPose);
    }
}

public sealed class GardenAnimationWorker:AnimationWorker_Keyframes
{
    public override bool Enabled(AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)
        =>parms.pawn?.jobs?.curDriver is JobDriver_GardenVisit d&&d.Age>=0&&!parms.pawn.Downed;
    public override float AngleAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)
        =>parms.pawn.jobs.curDriver is JobDriver_GardenVisit d&&parms.pawn.CurJob.targetA.Thing is Building_Sandbox
            ?(d.Slot==0?1:d.Slot==1?-1:0)*(8*PlaygroundMotion.SandScoop(d.Age,d.Slot)+3*PlaygroundMotion.SandPat(d.Age,d.Slot))*d.Blend:0;
    public override Vector3 OffsetAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)
    {
        if(parms.pawn.jobs.curDriver is not JobDriver_GardenVisit d)return Vector3.zero;
        // A small native-body lean shows sand work without adding any limbs.
        float lean=parms.pawn.CurJob.targetA.Thing is Building_Sandbox
            ?-.11f*PlaygroundMotion.SandScoop(d.Age,d.Slot)-.055f*PlaygroundMotion.SandPat(d.Age,d.Slot):0;
        float seatedHeight=parms.pawn.CurJob.targetA.Thing is Building_GardenBench ? .48f : .30f;
        return new Vector3(0,0,(seatedHeight+lean)*d.Blend);
    }
    public override Vector3 ScaleAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>Vector3.one;
    public override GraphicStateDef GraphicStateAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>null;
}
