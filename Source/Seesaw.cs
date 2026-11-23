using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LittlePlaygrounds;

[DefOf]
public static class SeesawDefOf
{
    public static ThingDef LP_Seesaw;
    public static JobDef LP_RideSeesaw;
    public static AnimationDef LP_SeesawPose;
    static SeesawDefOf()=>DefOfHelper.EnsureInitializedInCtor(typeof(SeesawDefOf));
}

public sealed class Building_Seesaw:Building
{
    // Follows a push from Tucked Furniture; zero without it (PlaygroundTuck).
    public override Vector3 DrawPos=>base.DrawPos+PlaygroundTuck.Shift(this);
    public const int RideTicks=1080;
    Pawn left,right;
    int cycleStart=-1,settleStart=-1;
    float settleAngle;
    bool motionReady;
    int motionMode,motionSince=-1;
    float motionAngle,motionVelocity;
    public Pawn PawnAt(int slot)=>slot==0?left:right;
    public void SetRider(int slot,Pawn p){if(slot==0)left=p;else right=p;}
    public JobDriver_RideSeesaw JobAt(int slot)
    {
        var p=PawnAt(slot);
        return p?.Spawned==true&&p.Map==Map&&p.jobs.curDriver is JobDriver_RideSeesaw&&p.CurJob.targetA.Thing==this
            ?p.jobs.curDriver as JobDriver_RideSeesaw:null;
    }
    public int CycleAge=>cycleStart<0?-1:Math.Max(0,Find.TickManager.TicksGame-cycleStart);
    public bool Paired=>ActiveMask==3;
    public bool HasWaitingPartner(Pawn p)=>(JobAt(0)!=null&&left!=p)||(JobAt(1)!=null&&right!=p);
    bool PlayingAt(int slot)=>JobAt(slot) is {Age:>=60,Leaving:false};
    int ActiveMask=>(PlayingAt(0)?1:0)|(PlayingAt(1)?2:0);
    static float RidingAngle(int age)=>PlaygroundMotion.SeesawAngle(age);
    float LegacyAngle=>cycleStart>=0?RidingAngle(CycleAge):settleStart>=0?
        settleAngle*(1-SwingMotion.Smooth((Find.TickManager.TicksGame-settleStart)/120f)):0;
    public float Angle=>motionReady?motionAngle:LegacyAngle;
    // Turnable (PlaygroundTurn): entrances and the step away follow the turn; a quarter-turned seesaw draws its side view (TurnSeesaw).
    public IntVec3 EntryAt(int slot)=>PlaygroundTurn.Cell(this,new IntVec3(slot==0?-3:3,0,-1));
    public IntVec3 LeaveAt(int slot)=>PlaygroundTurn.Cell(this,new IntVec3(slot==0?-3:3,0,-2));
    public int FreeSlot(Pawn p)
    {
        if(!Spawned||p?.Spawned!=true||p.Map!=Map)return -1;
        for(int i=0;i<2;i++)if(PawnAt(i)==p&&JobAt(i)!=null)return i;
        for(int i=0;i<2;i++)if(JobAt(i)==null&&EntryUsable(i,p))return i;
        return -1;
    }
    bool EntryUsable(int slot,Pawn p)
    {
        var entry=EntryAt(slot);
        return entry.InBounds(Map)&&entry.Standable(Map)&&!entry.IsForbidden(p)
            &&PlaygroundAvailability.Entry(p,entry)==PlayBlocked.None;
    }
    public bool CanUse(Pawn p,bool reserve=true)=>UseFailure(p,reserve)==PlayBlocked.None;
    public PlayBlocked UseFailure(Pawn p,bool reserve=true)
    {
        var failure=PlaygroundAvailability.Common(this,p);if(failure!=PlayBlocked.None)return failure;
        failure=PlayBlocked.Busy;
        for(int i=0;i<2;i++)
        {
            if(JobAt(i)!=null&&PawnAt(i)!=p)continue;
            if(JobAt(1-i)?.Leaving==true){failure=PlayBlocked.Busy;continue;}
            failure=PlaygroundAvailability.Entry(p,EntryAt(i));
            if(failure==PlayBlocked.None)return !reserve||p.CanReserve(this,2,0)?PlayBlocked.None:PlayBlocked.Busy;
        }
        return failure;
    }
    protected override void Tick()
    {
        base.Tick();
        int now=Find.TickManager.TicksGame,mask=ActiveMask;
        if(!motionReady)
        {
            // Old saves have only cycle/settle fields. Begin at their actual visible angle.
            motionAngle=LegacyAngle;motionVelocity=0;motionMode=mask;
            motionSince=mask==3&&cycleStart>=0?cycleStart:now;motionReady=true;
        }
        if(mask!=motionMode)
        {motionMode=mask;motionSince=now;}
        cycleStart=mask==3?motionSince:-1;
        float target=mask==3?RidingAngle(now-motionSince):mask==0?0:
            PlaygroundMotion.SoloSeesawAngle(now-motionSince,mask==1?0:1);
        PlaygroundMotion.StepSeesaw(ref motionAngle,ref motionVelocity,target);
        if(mask==0&&Math.Abs(motionAngle)<.0001f&&Math.Abs(motionVelocity)<.0001f)
        {motionAngle=0;motionVelocity=0;}
    }
    public override void ExposeData()
    {
        base.ExposeData();Scribe_References.Look(ref left,"lpSeesawLeft");Scribe_References.Look(ref right,"lpSeesawRight");
        Scribe_Values.Look(ref cycleStart,"lpSeesawCycle",-1);Scribe_Values.Look(ref settleStart,"lpSeesawSettle",-1);
        Scribe_Values.Look(ref settleAngle,"lpSeesawAngle",0);
        Scribe_Values.Look(ref motionReady,"lpSeesawMotionReady",false);
        Scribe_Values.Look(ref motionMode,"lpSeesawMotionMode",0);
        Scribe_Values.Look(ref motionSince,"lpSeesawMotionSince",-1);
        Scribe_Values.Look(ref motionAngle,"lpSeesawMotionAngle",0);
        Scribe_Values.Look(ref motionVelocity,"lpSeesawMotionVelocity",0);
    }
    public override void DeSpawn(DestroyMode mode=DestroyMode.Vanish)
    {
        JobAt(0)?.EndJobWith(JobCondition.Incompletable);JobAt(1)?.EndJobWith(JobCondition.Incompletable);
        left=null;right=null;base.DeSpawn(mode);
    }
    public override Graphic Graphic=>PaletteArt.GraphicFor(this);
    protected override void DrawAt(Vector3 drawLoc,bool flip=false){if(TurnSeesaw.Turned(this))TurnSeesaw.Draw(this,drawLoc);else if(DesignArt.Timber(this))TimberArt.SeesawDraw(this,drawLoc,PaletteArt.Of(this));else SeesawArt.Draw(this,drawLoc);}
    public override string GetInspectString()=>PlaygroundInspect.Append(PlaygroundInspect.Append(base.GetInspectString(),
        Paired?"LP_Inspect_SeesawTogether".Translate().ToString():ActiveMask!=0?"LP_Inspect_SeesawSolo".Translate().ToString():
            HasWaitingPartner(null)?"LP_Inspect_SeesawWaiting".Translate().ToString():"LP_Inspect_SeesawFree".Translate().ToString()),
        PlaygroundRecords.InspectLine(this));
    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn p)
    {
        foreach(var option in base.GetFloatMenuOptions(p))yield return option;
        yield return PlaygroundAvailability.Menu("LP_Menu_PlaySeesaw".Translate(),UseFailure(p),()=>
            p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(SeesawDefOf.LP_RideSeesaw,this),JobTag.SatisfyingNeeds));
    }
}

public sealed class JobDriver_RideSeesaw:JobDriver
{
    public int Slot=-1;
    int start=-1,leaveStart=-1,holdSince=-1;
    public int CompanionCallerJob=-1;
    bool mounted,wasPaired,played,cleanupDone;
    Building_Seesaw Seesaw=>job.targetA.Thing as Building_Seesaw;
    public int Age=>mounted&&start>=0?Math.Max(0,Find.TickManager.TicksGame-start):-1;
    public bool Leaving=>leaveStart>=0;
    public float Blend=>Age<0?0:Leaving?1-SwingMotion.Smooth((Find.TickManager.TicksGame-leaveStart)/75f):SwingMotion.Smooth(Age/60f);
    public override Vector3 ForcedBodyOffset=>Age>=0&&Seesaw?.Spawned==true?
        Seesaw.Position.ToVector3Shifted()+PlaygroundTuck.Shift(Seesaw)*Blend+Vector3.Lerp(Seesaw.EntryAt(Slot).ToVector3Shifted()-Seesaw.Position.ToVector3Shifted(),
            (TurnSeesaw.Turned(Seesaw)?TurnSeesaw.Seat(Seesaw,Slot,Seesaw.Angle):DesignArt.Timber(Seesaw)?TimberArt.SeesawSeat(Slot,Seesaw.Angle):SeesawArt.Seat(Slot,Seesaw.Angle)),Blend)-pawn.Drawer.tweener.TweenedPos:Vector3.zero;
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        if(Seesaw==null||!Seesaw.CanUse(pawn)||!pawn.Reserve(Seesaw,job,2,0,null,errorOnFailed))return false;
        Slot=Seesaw.FreeSlot(pawn);if(Slot<0)return false;
        Seesaw.SetRider(Slot,pawn);return true;
    }
    public override void ExposeData()
    {
        base.ExposeData();Scribe_Values.Look(ref Slot,"lpSeesawSlot",-1);Scribe_Values.Look(ref start,"lpSeesawStart",-1);
        Scribe_Values.Look(ref leaveStart,"lpSeesawLeave",-1);Scribe_Values.Look(ref mounted,"lpSeesawMounted",false);
        Scribe_Values.Look(ref wasPaired,"lpSeesawWasPaired",false);
        Scribe_Values.Look(ref played,"lpSeesawPlayed",false);
        Scribe_Values.Look(ref cleanupDone,"lpCleanupDone",false);
        Scribe_Values.Look(ref holdSince,"lpEntranceHoldStart",-1);
        Scribe_Values.Look(ref CompanionCallerJob,"lpCompanionCallerJob",-1);
    }
    void ClearRide()
    {
        if(cleanupDone)return;cleanupDone=true;
        mounted=false;
        if(Seesaw!=null&&Slot>=0&&Seesaw.PawnAt(Slot)==pawn)Seesaw.SetRider(Slot,null);
        if(pawn.Drawer.renderer.CurAnimation==SeesawDefOf.LP_SeesawPose)pawn.Drawer.renderer.SetAnimation(null);
        if(pawn.Spawned){var visits=pawn.Map.GetComponent<PlaygroundMap>();if(played||wasPaired)visits.Finished(pawn);else visits.Failed(pawn);}
    }
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOn(()=>pawn.Drafted||pawn.Downed||pawn.InMentalState||!PlaygroundUse.CanParticipate(pawn));
        this.FailOn(()=>PlaygroundCare.ShouldStop(pawn,job));
        this.FailOn(()=>PlaygroundCompanion.ShouldStopBeforeMount(this,CompanionCallerJob,start>=0));
        AddFinishAction(_=>ClearRide());
        if(Seesaw==null)yield break;
        yield return Toils_Goto.GotoCell(Seesaw.EntryAt(Slot),PathEndMode.OnCell);
        var play=ToilMaker.MakeToil("PlayOnSeesaw");
        play.initAction=()=>
        {
            if(!Seesaw.CanUse(pawn,false)){EndJobWith(JobCondition.Incompletable);return;}
            pawn.pather.StopDead();
            if(!PlaygroundCompanion.HoldAtEntrance(this,Seesaw,ref holdSince))Mount();
        };
        play.tickIntervalAction=delta=>
        {
            if(Seesaw?.Spawned!=true){EndJobWith(JobCondition.Incompletable);return;}
            Seesaw.SetRider(Slot,pawn);
            if(!mounted)
            {
                if(PawnUtility.WillSoonHaveBasicNeed(pawn,-.05f)||!PlaygroundCare.IsCare(job.def)&&!(pawn.timetable?.CurrentAssignment??TimeAssignmentDefOf.Anything).allowJoy)
                {EndJobWith(JobCondition.InterruptOptional);return;}
                if(PlaygroundCompanion.HoldAtEntrance(this,Seesaw,ref holdSince))return;
                Mount();
            }
            pawn.Rotation=PlaygroundTurn.Facing(Seesaw,Slot==0?Rot4.East:Rot4.West);
            if(pawn.Drawer.renderer.CurAnimation==null)pawn.Drawer.renderer.SetAnimation(SeesawDefOf.LP_SeesawPose);
            if(PawnUtility.WillSoonHaveBasicNeed(pawn,-.05f)||!PlaygroundCare.IsCare(job.def)&&!(pawn.timetable?.CurrentAssignment??TimeAssignmentDefOf.Anything).allowJoy)
            {EndJobWith(JobCondition.InterruptOptional);return;}
            if(Seesaw.Paired)wasPaired=true;
            // Each rider owns a finite visit. Joining/leaving changes motion, not anybody's elapsed time.
            if(!Leaving&&Age>=Building_Seesaw.RideTicks)
                leaveStart=Find.TickManager.TicksGame;
            if(!Leaving&&Age>=60)
            {played=true;if(PlaygroundUse.Reward(pawn,delta,Seesaw))return;}
            if(Leaving&&Find.TickManager.TicksGame-leaveStart>=75)ReadyForNextToil();
        };
        play.handlingFacing=true;play.defaultCompleteMode=ToilCompleteMode.Never;play.AddFinishAction(ClearRide);
        yield return play;
        yield return Toils_Goto.GotoCell(Seesaw.LeaveAt(Slot),PathEndMode.OnCell);
        yield return PlaygroundReaction.AfterRide(pawn,()=>played||wasPaired);
    }
    void Mount()
    {
        pawn.Notify_Teleported(endCurrentJob:false);
        mounted=true;start=Find.TickManager.TicksGame;Seesaw.SetRider(Slot,pawn);
        if(pawn.Drawer.renderer.CurAnimation==null)pawn.Drawer.renderer.SetAnimation(SeesawDefOf.LP_SeesawPose);
    }
}

public sealed class SeesawAnimationWorker:AnimationWorker_Keyframes
{
    public override bool Enabled(AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)
        =>parms.pawn?.jobs?.curDriver is JobDriver_RideSeesaw d&&d.Age>=0&&!parms.pawn.Downed;
    public override float AngleAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>0;
    public override Vector3 OffsetAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)
        =>parms.pawn.jobs.curDriver is JobDriver_RideSeesaw d?new Vector3(0,0,(.43f+PlaygroundUse.AdultSeatLift(parms.pawn))*d.Blend):Vector3.zero;
    public override Vector3 ScaleAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>Vector3.one;
    public override GraphicStateDef GraphicStateAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>null;
}
