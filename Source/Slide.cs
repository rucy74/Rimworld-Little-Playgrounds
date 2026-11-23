using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LittlePlaygrounds;

[DefOf]
public static class SlideDefOf
{
    public static ThingDef LP_Slide;
    public static JobDef LP_RideSlide;
    public static AnimationDef LP_SlidePose;
    static SlideDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(SlideDefOf));
}

// x/z are map-aligned ground coordinates; y is height above the ground.
public static class SlideMotion
{
    public const int BoardTicks=60, ClimbTicks=240, PlatformTicks=90, SitTicks=48, SlideTicks=96, LandTicks=60;
    public const int ClimbEnd=BoardTicks+ClimbTicks, PlatformEnd=ClimbEnd+PlatformTicks;
    public const int SlideStart=PlatformEnd+SitTicks, SlideEnd=SlideStart+SlideTicks, TotalTicks=SlideEnd+LandTicks;
    public const float DeckHeight=1.55f;
    public static readonly Vector3 Entry=new(-1,0,-1), LadderFoot=SelectedSlideArt.Point(260,1000);
    public static readonly Vector3 LadderTop=SelectedSlideArt.Point(390,310), ChuteTop=SelectedSlideArt.Point(750,320);
    public static readonly Vector3 ChuteFoot=SelectedSlideArt.Point(922,1245), Exit=new(1,0,-2);
    public static Vector3 Project(Vector3 p)=>new(p.x,0,p.z+p.y);
    public static float Smooth(float t)=>SwingMotion.Smooth(t);
    public static string Phase(int age)=>age<0?"approach":age<BoardTicks?"boarding":age<ClimbEnd?"climbing":
        age<PlatformEnd?"platform":age<SlideStart?"seated":age<SlideEnd?"sliding":age<TotalTicks?"landing":"finished";
    public static Vector3 Chute(float t)
    {
        t=Mathf.Clamp01(t);
        float x=Mathf.Lerp(750,922,t);
        return SelectedSlideArt.Point(x,Mathf.Lerp(320,1245,t));
    }
    public static Vector3 Model(int age)
    {
        if(age<0)return Entry;
        if(age<BoardTicks)return Vector3.Lerp(Entry,LadderFoot,Smooth(age/(float)BoardTicks));
        if(age<ClimbEnd)
        {
            float step=(age-BoardTicks)*6f/ClimbTicks;
            float progress=(Mathf.Floor(step)+Smooth((step-Mathf.Floor(step))/.8f))/6f;
            return Vector3.Lerp(LadderFoot,LadderTop,progress);
        }
        if(age<PlatformEnd)return Vector3.Lerp(LadderTop,ChuteTop,Smooth((age-ClimbEnd)/(float)PlatformTicks));
        if(age<SlideStart)return ChuteTop;
        if(age<SlideEnd)
        {
            float t=(age-SlideStart)/(float)SlideTicks;
            // Accelerate from the top; flatten the speed at the run-out.
            float distance=t*t*(3-2*t);
            return Chute(distance);
        }
        if(age<TotalTicks)return Vector3.Lerp(ChuteFoot,Exit,Smooth((age-SlideEnd)/(float)LandTicks));
        return Exit;
    }
    public static float PoseBlend(int age)=>age<0?0:age<BoardTicks?Smooth(age/(float)BoardTicks):
        age>=SlideEnd?1-Smooth((age-SlideEnd)/(float)LandTicks):1;
    public static Rot4 Facing(int age)=>age<ClimbEnd?Rot4.North:age<PlatformEnd?Rot4.East:Rot4.South;
}

public sealed class Building_Slide : Building
{
    // Follows a push from Tucked Furniture; zero without it (PlaygroundTuck).
    public override Vector3 DrawPos=>base.DrawPos+PlaygroundTuck.Shift(this);
    public Pawn rider;
    // Turnable (PlaygroundTurn): the steps' foot, the landing and the step away follow the turn (TurnSlide draws the turned slide).
    public IntVec3 EntryCell=>PlaygroundTurn.Cell(this,new IntVec3(-1,0,-1));
    public IntVec3 ExitCell=>PlaygroundTurn.Cell(this,new IntVec3(1,0,-2));
    public IntVec3 LeaveCell=>PlaygroundTurn.Cell(this,new IntVec3(1,0,-3));
    public JobDriver_RideSlide RiderJob=>rider?.Spawned==true && rider.Map==Map &&
        rider.CurJobDef==SlideDefOf.LP_RideSlide && rider.CurJob.targetA.Thing==this
        ?rider.jobs.curDriver as JobDriver_RideSlide:null;
    public bool Occupied=>RiderJob!=null;
    public bool ClearCell(IntVec3 cell,Pawn p)=>cell.InBounds(Map)&&cell.Standable(Map)&&!cell.IsForbidden(p);
    public bool CanUse(Pawn p,bool checkReserve=true)=>UseFailure(p,checkReserve)==PlayBlocked.None;
    public PlayBlocked UseFailure(Pawn p,bool reserve=true)
    {
        var reason=PlaygroundAvailability.Common(this,p);if(reason!=PlayBlocked.None)return reason;
        if(Occupied&&rider!=p)return PlayBlocked.Busy;
        reason=PlaygroundAvailability.Entry(p,EntryCell);if(reason!=PlayBlocked.None)return reason;
        reason=PlaygroundAvailability.Cell(p,ExitCell);if(reason!=PlayBlocked.None)return reason;
        return !reserve||p.CanReserve(this)?PlayBlocked.None:PlayBlocked.Busy;
    }
    public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref rider,"lpSlideRider");}
    public override void DeSpawn(DestroyMode mode=DestroyMode.Vanish)
    {
        RiderJob?.EndJobWith(JobCondition.Incompletable);
        rider=null;base.DeSpawn(mode);
    }
    public override Graphic Graphic=>PaletteArt.GraphicFor(this);
    protected override void DrawAt(Vector3 drawLoc,bool flip=false){if(TurnSlide.Turned(this))TurnSlide.Draw(this,drawLoc);else if(DesignArt.Timber(this))TimberArt.SlideDraw(drawLoc,PaletteArt.Of(this));else SelectedSlideArt.Draw(drawLoc,PaletteArt.Of(this));}
    public override string GetInspectString()=>PlaygroundInspect.Append(PlaygroundInspect.Append(base.GetInspectString(),Occupied?
        "LP_Inspect_SlideRider".Translate(rider.LabelShort,("LP_SlidePhase_"+SlideMotion.Phase(RiderJob.Age)).Translate().ToString()).ToString():"LP_Inspect_ReadyAny".Translate().ToString()),
        PlaygroundRecords.InspectLine(this));
    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
    {
        foreach(var option in base.GetFloatMenuOptions(selPawn))yield return option;
        yield return PlaygroundAvailability.Menu("LP_Menu_PlaySlide".Translate(),UseFailure(selPawn),()=>
            selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(SlideDefOf.LP_RideSlide,this),JobTag.SatisfyingNeeds));
    }
}

public sealed class JobDriver_RideSlide : JobDriver
{
    int rideStart=-1;
    bool mounted,cleanupDone;
    Building_Slide Slide=>job.targetA.Thing as Building_Slide;
    public int Age=>mounted&&rideStart>=0?Math.Max(0,Find.TickManager.TicksGame-rideStart):-1;
    public override Vector3 ForcedBodyOffset=>Age>=0&&Slide?.Spawned==true?
        Slide.DrawPos+(TurnSlide.Turned(Slide)?TurnSlide.Model(Slide,Age):SlideMotion.Project(DesignArt.Timber(Slide)?TimberArt.SlideModel(Age):SlideMotion.Model(Age)))-pawn.Drawer.tweener.TweenedPos:Vector3.zero;
    public override bool TryMakePreToilReservations(bool errorOnFailed)=>
        Slide!=null&&Slide.CanUse(pawn)&&pawn.Reserve(Slide,job,1,-1,null,errorOnFailed);
    public override void ExposeData()
    {
        base.ExposeData();Scribe_Values.Look(ref rideStart,"lpSlideStart",-1);Scribe_Values.Look(ref mounted,"lpSlideMounted",false);
        Scribe_Values.Look(ref cleanupDone,"lpCleanupDone",false);
    }
    void ClearRide()
    {
        if(cleanupDone)return;cleanupDone=true;
        int age=Age;
        bool wasMounted=mounted;
        mounted=false;
        // Release the same living pawn at a clear ground cell, including interruption.
        if(wasMounted&&pawn.Spawned&&Slide?.Map==pawn.Map)
        {
            var cell=age>=SlideMotion.ClimbEnd?Slide.ExitCell:Slide.EntryCell;
            // Cleanup already owns job termination. The default notification ends
            // the current job again, recursively re-entering this finish action.
            if(Slide.ClearCell(cell,pawn)){pawn.Position=cell;pawn.Notify_Teleported(endCurrentJob:false);}
        }
        if(Slide!=null&&Slide.rider==pawn)Slide.rider=null;
        if(pawn.Drawer.renderer.CurAnimation==SlideDefOf.LP_SlidePose)pawn.Drawer.renderer.SetAnimation(null);
        if(pawn.Spawned){var visits=pawn.Map.GetComponent<PlaygroundMap>();if(wasMounted)visits.Finished(pawn);else visits.Failed(pawn);}
    }
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOn(()=>pawn.Drafted||pawn.Downed||pawn.InMentalState||!PlaygroundUse.CanParticipate(pawn));
        AddFinishAction(_=>ClearRide());
        if(Slide==null)yield break;
        yield return Toils_Goto.GotoCell(Slide.EntryCell,PathEndMode.OnCell);
        var ride=ToilMaker.MakeToil("RidePlaygroundSlide");
        ride.initAction=()=>
        {
            if(!Slide.CanUse(pawn,false)){EndJobWith(JobCondition.Incompletable);return;}
            pawn.pather.StopDead();
            pawn.Notify_Teleported(endCurrentJob:false); // Finish incoming walking interpolation at the entry.
            rideStart=Find.TickManager.TicksGame;mounted=true;Slide.rider=pawn;
            pawn.Rotation=PlaygroundTurn.Facing(Slide,Rot4.North);
            if(pawn.Drawer.renderer.CurAnimation==null)pawn.Drawer.renderer.SetAnimation(SlideDefOf.LP_SlidePose);
        };
        ride.tickIntervalAction=delta=>
        {
            if(Slide?.Spawned!=true||!Slide.ClearCell(Slide.ExitCell,pawn)){EndJobWith(JobCondition.Incompletable);return;}
            Slide.rider=pawn;pawn.Rotation=PlaygroundTurn.Facing(Slide,SlideMotion.Facing(Age));
            if(pawn.Drawer.renderer.CurAnimation==null)pawn.Drawer.renderer.SetAnimation(SlideDefOf.LP_SlidePose);
            if(PawnUtility.WillSoonHaveBasicNeed(pawn,-.05f)||!(pawn.timetable?.CurrentAssignment??TimeAssignmentDefOf.Anything).allowJoy)
            {EndJobWith(JobCondition.InterruptOptional);return;}
            if(Age>=SlideMotion.BoardTicks&&Age<SlideMotion.SlideEnd)
                if(PlaygroundUse.Reward(pawn,delta,Slide))return;
            if(Age>=SlideMotion.TotalTicks)ReadyForNextToil();
        };
        ride.handlingFacing=true;ride.defaultCompleteMode=ToilCompleteMode.Never;
        ride.AddFinishAction(ClearRide);yield return ride;
        yield return Toils_Goto.GotoCell(Slide.LeaveCell,PathEndMode.OnCell);
        yield return PlaygroundReaction.AfterRide(pawn);
    }
}

public sealed class SlideAnimationWorker : AnimationWorker_Keyframes
{
    public override bool Enabled(AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>
        parms.pawn?.jobs?.curDriver is JobDriver_RideSlide d&&d.Age>=0&&!parms.pawn.Downed;
    public override float AngleAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>0;
    public override Vector3 ScaleAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>Vector3.one;
    public override Vector3 OffsetAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>
        parms.pawn.jobs.curDriver is JobDriver_RideSlide d?new Vector3(0,0,.36f*SlideMotion.PoseBlend(d.Age)):Vector3.zero;
    public override GraphicStateDef GraphicStateAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>null;
}
