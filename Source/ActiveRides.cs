using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LittlePlaygrounds;

// Trampoline and zipline have their own JobDefs; both use the shared active-ride motion driver.
public static class ActiveRideJobs
{
    public static JobDef For(Building_ActiveRide r)=>r.Kind==ActiveRideKind.Zipline?ActiveRideDefOf.LP_RideZipline:ActiveRideDefOf.LP_BounceTrampoline;
    public static bool IsRide(JobDef d)=>d!=null&&d.driverClass==typeof(JobDriver_ActiveRide);
}

[DefOf]
public static class ActiveRideDefOf
{
    public static ThingDef LP_Trampoline, LP_Zipline;
    public static JobDef LP_BounceTrampoline,LP_RideZipline;
    public static AnimationDef LP_ActiveRidePose;
    static ActiveRideDefOf()=>DefOfHelper.EnsureInitializedInCtor(typeof(ActiveRideDefOf));
}

// Owns only this facility's rider and empty carriage. Rendering is read-only.
public sealed class Building_ActiveRide:Building
{
    // Follows a push from Tucked Furniture; zero without it (PlaygroundTuck).
    public override Vector3 DrawPos=>base.DrawPos+PlaygroundTuck.Shift(this);
    Pawn rider;
    int returnStart=-1;
    float returnFrom,lastRideProgress,returnSway,lastRideSway;

    public ActiveRideKind Kind=>def.GetModExtension<ActiveRideProperties>()?.kind??ActiveRideKind.Trampoline;
    public IntVec3 EntryCell=>Position+(Kind==ActiveRideKind.Zipline?new IntVec3(-3,0,-1):IntVec3.South*2);
    public IntVec3 ExitCell=>Kind==ActiveRideKind.Zipline?Position+new IntVec3(3,0,-1):EntryCell;
    public JobDriver_ActiveRide RiderJob=>rider?.Spawned==true&&rider.Map==Map&&
        ActiveRideJobs.IsRide(rider.CurJobDef)&&rider.CurJob.targetA.Thing==this
        ?rider.jobs.curDriver as JobDriver_ActiveRide:null;
    public bool Returning=>Kind==ActiveRideKind.Zipline&&returnStart>=0&&returnFrom>.0001f&&
        Find.TickManager.TicksGame-returnStart<ActiveRideMotion.ReturnTicks;
    public float CarProgress
    {
        get
        {
            if(Kind!=ActiveRideKind.Zipline)return 0;
            var driver=RiderJob;
            if(driver?.Age>=0)return ActiveRideMotion.ZipProgress(driver.Age);
            return Returning?returnFrom*(1-SwingMotion.Smooth((Find.TickManager.TicksGame-returnStart)/(float)ActiveRideMotion.ReturnTicks)):0;
        }
    }
    public float CarSway=>RiderJob?.Age>=0?ActiveRideMotion.ZipSway(RiderJob.Age):Returning?
        returnSway*(1-SwingMotion.Smooth((Find.TickManager.TicksGame-returnStart)/90f)):0;
    public bool ClearCell(IntVec3 cell,Pawn pawn)=>Spawned&&cell.InBounds(Map)&&cell.Standable(Map)&&!cell.IsForbidden(pawn);
    public bool ClearRidePath(Pawn pawn)
    {
        if(!ClearCell(EntryCell,pawn)||!ClearCell(ExitCell,pawn))return false;
        if(Kind==ActiveRideKind.Trampoline)return ClearCell(Position,pawn);
        for(int x=-3;x<=3;x++)
            if(!ClearCell(Position+new IntVec3(x,0,0),pawn))return false;
        return true;
    }
    public bool CanUse(Pawn pawn,bool reserve=true)=>UseFailure(pawn,reserve)==PlayBlocked.None;
    public PlayBlocked UseFailure(Pawn pawn,bool reserve=true)
    {
        var reason=PlaygroundAvailability.Common(this,pawn);if(reason!=PlayBlocked.None)return reason;
        if(Returning)return PlayBlocked.Returning;
        if(RiderJob!=null&&rider!=pawn)return PlayBlocked.Busy;
        if(!ClearRidePath(pawn))return PlayBlocked.Entrance;
        reason=PlaygroundAvailability.Entry(pawn,EntryCell);if(reason!=PlayBlocked.None)return reason;
        reason=PlaygroundAvailability.Entry(pawn,ExitCell);if(reason!=PlayBlocked.None)return reason;
        return !reserve||pawn.CanReserve(this,1,-1)?PlayBlocked.None:PlayBlocked.Busy;
    }
    public void SetRider(Pawn pawn)
    {
        if(pawn==rider)return;
        if(pawn==null)
        {
            float progress=CarProgress,sway=CarSway;
            bool mounted=RiderJob?.Age>=0;
            rider=null;
            if(mounted)BeginReturn(progress,sway);
            return;
        }
        rider=pawn;
        lastRideProgress=0;lastRideSway=0;
    }
    internal void ReleaseRider(Pawn pawn,int mountedAge)
    {
        if(rider!=pawn)return;
        rider=null;
        // Use the exiting driver's saved phase directly: the current job may
        // already have changed before its finish action is called.
        if(mountedAge>=0)BeginReturn(ActiveRideMotion.ZipProgress(mountedAge),ActiveRideMotion.ZipSway(mountedAge));
    }
    void BeginReturn(float progress,float sway)
    {
        if(Kind!=ActiveRideKind.Zipline)return;
        returnFrom=Mathf.Clamp01(progress);
        returnSway=sway;
        returnStart=returnFrom>.0001f?Find.TickManager.TicksGame:-1;
        lastRideProgress=0;
    }
    protected override void Tick()
    {
        base.Tick();
        var driver=RiderJob;
        if(driver?.Age>=0){lastRideProgress=ActiveRideMotion.ZipProgress(driver.Age);lastRideSway=ActiveRideMotion.ZipSway(driver.Age);}
        else if(rider!=null&&driver==null)
        {
            // A carried, despawned or otherwise lost pawn must not strand the seat.
            rider=null;
            BeginReturn(lastRideProgress,lastRideSway);
        }
        if(returnStart>=0&&!Returning){returnStart=-1;returnFrom=0;returnSway=0;}
    }
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref rider,"lpActiveRider");
        Scribe_Values.Look(ref returnStart,"lpActiveReturnStart",-1);
        Scribe_Values.Look(ref returnFrom,"lpActiveReturnFrom",0);
        Scribe_Values.Look(ref lastRideProgress,"lpActiveLastProgress",0);
        Scribe_Values.Look(ref returnSway,"lpActiveReturnSway",0);
        Scribe_Values.Look(ref lastRideSway,"lpActiveLastSway",0);
    }
    public override void DeSpawn(DestroyMode mode=DestroyMode.Vanish)
    {
        RiderJob?.EndJobWith(JobCondition.Incompletable);
        rider=null;
        base.DeSpawn(mode);
    }
    public override Graphic Graphic=>PaletteArt.GraphicFor(this);
    protected override void DrawAt(Vector3 at,bool flip=false)=>ActiveRideArt.Draw(this,at);
    public override string GetInspectString()=>PlaygroundInspect.Append(PlaygroundInspect.Append(base.GetInspectString(),Returning?"LP_Inspect_SeatReturning".Translate().ToString():
        RiderJob!=null?"LP_Inspect_Rider".Translate(rider.LabelShort).ToString():"LP_Inspect_ReadyOne".Translate().ToString()),
        PlaygroundRecords.InspectLine(this));
    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
    {
        foreach(var option in base.GetFloatMenuOptions(pawn))yield return option;
        yield return PlaygroundAvailability.Menu(Kind==ActiveRideKind.Trampoline?"LP_Menu_JumpTrampoline".Translate():"LP_Menu_RideZipline".Translate(),UseFailure(pawn),()=>
            pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(ActiveRideJobs.For(this),this),JobTag.SatisfyingNeeds));
    }
}

public sealed class JobDriver_ActiveRide:JobDriver
{
    int rideStart=-1;
    bool mounted,cleanupDone;
    public Building_ActiveRide Ride=>job.targetA.Thing as Building_ActiveRide;
    public int Age=>mounted&&rideStart>=0?Math.Max(0,Find.TickManager.TicksGame-rideStart):-1;
    public int Duration=>Ride?.Kind==ActiveRideKind.Zipline?ActiveRideMotion.ZiplineTicks:ActiveRideMotion.TrampolineTicks;
    public float MountBlend=>Age<0?0:SwingMotion.Smooth(Age/60f)*SwingMotion.Smooth((Duration-Age)/60f);
    // The trampoline jump is a render-tree translation, leaving the real pawn and
    // its ground shadow at the mat. Native hair, clothing and proportions remain.
    public float PoseHeight=>Age<0?0:Ride?.Kind==ActiveRideKind.Trampoline?ActiveRideMotion.BounceHeight(Age)+ActiveRideMotion.TrampolineLift*MountBlend:
        (.36f+PlaygroundUse.AdultSeatLift(pawn))*MountBlend;
    Vector3 Contact
    {
        get
        {
            var ride=Ride;
            if(ride==null||Age<0)return Vector3.zero;
            Vector3 entry=ride.EntryCell.ToVector3Shifted()-ride.Position.ToVector3Shifted();
            Vector3 exit=ride.ExitCell.ToVector3Shifted()-ride.Position.ToVector3Shifted();
            if(ride.Kind==ActiveRideKind.Trampoline)
                return Vector3.Lerp(entry,Vector3.zero,MountBlend);
            Vector3 seat=ActiveRideMotion.ZipSeat(ActiveRideMotion.ZipProgress(Age),ActiveRideMotion.ZipSway(Age));
            if(Age<60)return Vector3.Lerp(entry,seat,SwingMotion.Smooth(Age/60f));
            if(Age>=Duration-60)return Vector3.Lerp(seat,exit,SwingMotion.Smooth((Age-(Duration-60))/60f));
            return seat;
        }
    }
    public override Vector3 ForcedBodyOffset=>Age>=0&&Ride?.Spawned==true?
        Ride.Position.ToVector3Shifted()+PlaygroundTuck.Shift(Ride)*MountBlend+Contact-pawn.Drawer.tweener.TweenedPos:Vector3.zero;
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        if(Ride==null||!Ride.CanUse(pawn)||!pawn.Reserve(Ride,job,1,-1,null,errorOnFailed))return false;
        Ride.SetRider(pawn);
        return true;
    }
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref rideStart,"lpActiveRideStart",-1);
        Scribe_Values.Look(ref mounted,"lpActiveMounted",false);
        Scribe_Values.Look(ref cleanupDone,"lpActiveCleanupDone",false);
    }
    void ClearRide()
    {
        if(cleanupDone)return;
        cleanupDone=true;
        int age=Age;
        bool wasMounted=mounted;
        var ride=Ride;
        // Release before disabling the mounted phase so the empty carriage starts
        // returning from its exact current position, including mid-ride cancellation.
        ride?.ReleaseRider(pawn,age);
        mounted=false;
        if(wasMounted&&pawn.Spawned&&ride?.Map==pawn.Map)
        {
            var desired=ride.Kind==ActiveRideKind.Zipline&&ActiveRideMotion.ZipProgress(age)>=.5f?ride.ExitCell:ride.EntryCell;
            if(!ride.ClearCell(desired,pawn))
            {
                var alternative=desired==ride.ExitCell?ride.EntryCell:ride.ExitCell;
                if(ride.ClearCell(alternative,pawn))desired=alternative;
                else
                {
                    desired=pawn.Position;
                    foreach(var cell in GenRadial.RadialCellsAround(pawn.Position,3,true))
                        if(ride.ClearCell(cell,pawn)){desired=cell;break;}
                }
            }
            if(ride.ClearCell(desired,pawn))
            {pawn.Position=desired;pawn.Notify_Teleported(endCurrentJob:false);}
        }
        if(pawn.Drawer?.renderer?.CurAnimation==ActiveRideDefOf.LP_ActiveRidePose)pawn.Drawer.renderer.SetAnimation(null);
        if(pawn.Spawned){var visits=pawn.Map.GetComponent<PlaygroundMap>();if(wasMounted)visits.Finished(pawn);else visits.Failed(pawn);}
    }
    bool NeedsToLeave()=>pawn.Drafted||pawn.Downed||pawn.InMentalState||!PlaygroundUse.CanParticipate(pawn)||
        PawnUtility.WillSoonHaveBasicNeed(pawn,-.05f)||!(pawn.timetable?.CurrentAssignment??TimeAssignmentDefOf.Anything).allowJoy;
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOn(()=>NeedsToLeave());
        AddFinishAction(_=>ClearRide());
        if(Ride==null)yield break;
        yield return Toils_Goto.GotoCell(Ride.EntryCell,PathEndMode.OnCell);
        var play=ToilMaker.MakeToil("UseActivePlaygroundRide");
        play.initAction=()=>
        {
            if(!Ride.CanUse(pawn,false)){EndJobWith(JobCondition.Incompletable);return;}
            pawn.pather.StopDead();
            pawn.Notify_Teleported(endCurrentJob:false);
            rideStart=Find.TickManager.TicksGame;mounted=true;cleanupDone=false;
            Ride.SetRider(pawn);
            pawn.Rotation=Rot4.South;
            if(pawn.Drawer.renderer.CurAnimation==null)pawn.Drawer.renderer.SetAnimation(ActiveRideDefOf.LP_ActiveRidePose);
        };
        play.tickIntervalAction=delta=>
        {
            var ride=Ride;
            if(ride?.Spawned!=true||!ride.ClearRidePath(pawn))
            {EndJobWith(JobCondition.Incompletable);return;}
            ride.SetRider(pawn);pawn.Rotation=Rot4.South;
            if(pawn.Drawer.renderer.CurAnimation==null)pawn.Drawer.renderer.SetAnimation(ActiveRideDefOf.LP_ActiveRidePose);
            if(Age>=60&&Age<Duration-60&&PlaygroundUse.Reward(pawn,delta,ride))return;
            if(Age>=Duration)ReadyForNextToil();
        };
        play.handlingFacing=true;play.defaultCompleteMode=ToilCompleteMode.Never;
        play.AddFinishAction(ClearRide);
        yield return play;
        // The release action has placed the same pawn at the clear landing cell.
        // A short ordinary step frees that cell for the next visitor.
        yield return Toils_Goto.GotoCell(Ride.ExitCell+IntVec3.South,PathEndMode.OnCell);
        yield return PlaygroundReaction.AfterRide(pawn);
    }
}

public sealed class ActiveRideAnimationWorker:AnimationWorker_Keyframes
{
    public override bool Enabled(AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>
        parms.pawn?.jobs?.curDriver is JobDriver_ActiveRide driver&&driver.Age>=0&&!parms.pawn.Downed;
    public override float AngleAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>
        parms.pawn.jobs.curDriver is JobDriver_ActiveRide d?d.Ride?.Kind==ActiveRideKind.Zipline
            ?-ActiveRideMotion.ZipSway(d.Age)*d.MountBlend
            :parms.pawn.DevelopmentalStage.Child()?ActiveRideMotion.FlipAngle(d.Age):0:0;
    public override Vector3 ScaleAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>Vector3.one;
    public override Vector3 OffsetAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>
        parms.pawn.jobs.curDriver is JobDriver_ActiveRide driver?new Vector3(0,0,driver.PoseHeight):Vector3.zero;
    public override GraphicStateDef GraphicStateAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>null;
}
