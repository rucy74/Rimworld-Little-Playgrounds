using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LittlePlaygrounds;

[DefOf]
public static class LPDefOf
{
    public static ThingDef LP_Swing;
    public static JobDef LP_RideSwing;
    public static AnimationDef LP_SwingPose;
    static LPDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(LPDefOf));
}

// All phase decisions use game ticks. Rendering never changes jobs, cells, RNG or reservations.
public static class SwingMotion
{
    public const int MountTicks = 60, RideTicks = 1080, DismountTicks = 75;
    public const int TotalTicks = MountTicks + RideTicks + DismountTicks;
    public const float RopeLength = 3.0f, PivotHeight = 3.5f;
    // Zero speed and acceleration at both ends, including mounting/dismounting.
    public static float Smooth(float t) { t = Mathf.Clamp01(t); return t*t*t*(t*(t*6f-15f)+10f); }
    public static float Angle(int age,int slot=0)
    {
        float rideAge = age - MountTicks;
        if (rideAge < 0 || rideAge >= RideTicks) return 0;
        float envelope = Smooth(rideAge / 180f) * Smooth((RideTicks - rideAge) / 240f);
        float wave=Mathf.Sin(rideAge*(2f*Mathf.PI/(slot==0?162f:175f)));
        // A modestly quicker centre passage, keeping the same endpoints and map-aligned arc.
        wave+=.10f*wave*(1-wave*wave);
        return .60f*envelope*PlaygroundMotion.SwingStrength((int)rideAge,slot)*wave;
    }
    // Model coordinates: x = across the beam, y = height, z = forward.
    // The seat pivots about the beam, with no movement along that beam.
    public static Vector3 SeatModel(int age,int slot=0)
    {
        float a = Angle(age,slot);
        return new Vector3(0, PivotHeight-RopeLength*Mathf.Cos(a), RopeLength*Mathf.Sin(a));
    }
    // Keep the original map-aligned view: the beam is horizontal and depth has
    // no screen-X component. Only height and forward travel affect screen Z.
    public static Vector3 Project(Vector3 p) => new(p.x, 0, p.y-.78f*p.z);
    public static Vector3 SeatOffset(int age,int slot=0) => Project(SeatModel(age,slot));
    public static float SeatedBlend(int age) => age < 0 ? 0 : age < MountTicks ? Smooth(age/(float)MountTicks)
        : age >= MountTicks+RideTicks ? 1-Smooth((age-MountTicks-RideTicks)/(float)DismountTicks) : 1;
    public static Vector3 TorsoOffset(int age) => new(0,0,.36f*SeatedBlend(age));
    public static Vector3 BodyOffset(int age)
    {
        // Keep the pawn's anchor at the seat; raise only its native render tree.
        // Preserve the normal pawn silhouette and keep its lower edge on the seat.
        return SeatOffset(age)*SeatedBlend(age);
    }
    public static string Phase(int age) => age < 0 ? "approach" : age < MountTicks ? "boarding"
        : age < MountTicks + RideTicks ? "riding" : age < TotalTicks ? "dismounting" : "finished";
}

public sealed class Building_Swing : Building
{
    // Follows a push from Tucked Furniture; zero without it (PlaygroundTuck).
    public override Vector3 DrawPos=>base.DrawPos+PlaygroundTuck.Shift(this);
    public Pawn rider;
    public Pawn secondRider;
    public Pawn PawnAt(int slot)=>slot==0?rider:secondRider;
    public void SetRider(int slot,Pawn p){if(slot==0)rider=p;else secondRider=p;}
    public JobDriver_RideSwing JobAt(int slot)
    {
        var p=PawnAt(slot);
        // By driver, not JobDef: recreation, learning and Childcare rides all occupy the seat.
        return p?.Spawned==true&&p.Map==Map&&p.jobs.curDriver is JobDriver_RideSwing&&p.CurJob.targetA.Thing==this
            ?p.jobs.curDriver as JobDriver_RideSwing:null;
    }
    public JobDriver_RideSwing RiderJob=>JobAt(0);
    public bool Occupied=>JobAt(0)!=null&&JobAt(1)!=null;
    public int FreeSlot(Pawn p)
    {
        if(!Spawned||p?.Spawned!=true||p.Map!=Map)return -1;
        for(int i=0;i<2;i++)if(PawnAt(i)==p&&JobAt(i)!=null)return i;
        for(int i=0;i<2;i++)if(JobAt(i)==null&&EntryUsable(i,p))return i;
        return -1;
    }
    public IntVec3 EntryCell => PlaygroundTurn.Cell(this,IntVec3.South*2);
    // Turnable (PlaygroundTurn): entrances and the step away follow the turn; a quarter-turned swing draws its side view (TurnSwing).
    public IntVec3 EntryAt(int slot)=>PlaygroundTurn.Cell(this,new IntVec3(slot==0?-1:1,0,-2));
    public IntVec3 LeaveAt(int slot)=>PlaygroundTurn.Cell(this,new IntVec3(slot==0?-1:1,0,-3));
    bool EntryUsable(int slot,Pawn p)
    {
        var entry=EntryAt(slot);
        return entry.InBounds(Map)&&entry.Standable(Map)&&!entry.IsForbidden(p)
            &&PlaygroundAvailability.Entry(p,entry)==PlayBlocked.None;
    }
    public bool CanUse(Pawn p,bool checkReserve=true)=>UseFailure(p,checkReserve)==PlayBlocked.None;
    public PlayBlocked UseFailure(Pawn p,bool reserve=true)
    {
        var failure=PlaygroundAvailability.Common(this,p);if(failure!=PlayBlocked.None)return failure;
        if(!Position.Standable(Map)||Position.IsForbidden(p))return PlayBlocked.Forbidden;
        failure=PlayBlocked.Busy;
        for(int i=0;i<2;i++)
        {
            if(JobAt(i)!=null&&PawnAt(i)!=p)continue;
            failure=PlaygroundAvailability.Entry(p,EntryAt(i));
            if(failure==PlayBlocked.None)return !reserve||p.CanReserve(this,2,0)?PlayBlocked.None:PlayBlocked.Busy;
        }
        return failure;
    }
    public override void ExposeData() { base.ExposeData(); Scribe_References.Look(ref rider, "lpRider"); Scribe_References.Look(ref secondRider,"lpSecondRider"); }
    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        JobAt(0)?.EndJobWith(JobCondition.Incompletable);JobAt(1)?.EndJobWith(JobCondition.Incompletable);
        rider=null;secondRider=null;
        base.DeSpawn(mode);
    }
    public override Graphic Graphic=>PaletteArt.GraphicFor(this);
    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {if(TurnSwing.Turned(this))TurnSwing.Draw(this,drawLoc);else if(DesignArt.Timber(this))TimberArt.SwingDraw(this,drawLoc,PaletteArt.Of(this));else SelectedSwingArt.Draw(this,drawLoc);}
    public override string GetInspectString()
        => PlaygroundInspect.Append(PlaygroundInspect.Append(base.GetInspectString(),"LP_Inspect_SwingSeats".Translate(JobAt(0)!=null?rider.LabelShort:"LP_Inspect_SeatFree".Translate().ToString(),
            JobAt(1)!=null?secondRider.LabelShort:"LP_Inspect_SeatFree".Translate().ToString()).ToString()),PlaygroundRecords.InspectLine(this));
    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
    {
        foreach(var option in base.GetFloatMenuOptions(selPawn))yield return option;
        yield return PlaygroundAvailability.Menu("LP_Menu_PlaySwing".Translate(),UseFailure(selPawn),()=>
            selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(LPDefOf.LP_RideSwing,this),JobTag.SatisfyingNeeds));
    }
}

// Per-map visit cooldowns and a short-lived wish for a drink; never adds an unmet learning desire.
public sealed class PlaygroundMap : MapComponent
{
    private Dictionary<Pawn, int> nextVisit = new();
    private List<Pawn> keys; private List<int> values;
    private List<Pawn> waitingForDrink = new();
    private Dictionary<Pawn,int> drinkUntil=new();
    private List<Pawn> drinkKeys;private List<int> drinkValues;
    const int DrinkWindow=GenDate.TicksPerHour;
    public const float DrinkChance=.30f;
    public const int RetryTicks=300;
    public PlaygroundMap(Map map) : base(map) { }
    public bool Ready(Pawn p) => !nextVisit.TryGetValue(p, out int tick) || Find.TickManager.TicksGame >= tick;
    public int TicksUntilReady(Pawn p) => nextVisit.TryGetValue(p, out int tick) ? System.Math.Max(0, tick - Find.TickManager.TicksGame) : 0;
    public bool WantsDrink(Pawn p)=>drinkUntil.TryGetValue(p,out int until)&&Find.TickManager.TicksGame<until;
    public void Finished(Pawn p,bool refreshed=false)
    {
        PlaygroundFaces.Stop(p);
        // A drink fulfils the wish; it is not another playground visit.
        if(refreshed){drinkUntil.Remove(p);return;}
        nextVisit[p]=Find.TickManager.TicksGame+6000;
        if(p.DevelopmentalStage.Child()&&Rand.Value<DrinkChance)drinkUntil[p]=Find.TickManager.TicksGame+DrinkWindow;
        else drinkUntil.Remove(p);
    }
    public void Failed(Pawn p)
    {
        PlaygroundFaces.Stop(p);
        int retry=Find.TickManager.TicksGame+RetryTicks;
        nextVisit[p]=nextVisit.TryGetValue(p,out int old)?System.Math.Max(old,retry):retry;
        drinkUntil.Remove(p);
    }
    void Prune(Dictionary<Pawn,int> table,int now)
    {
        foreach(var p in table.Keys.Where(p=>p.Destroyed||p.Dead||!p.Spawned||p.Map!=map||table[p]<=now).ToArray())table.Remove(p);
    }
    public override void MapComponentTick()
    {
        PlaygroundToddlers.KeepPoses(map);
        int now=Find.TickManager.TicksGame;
        if((now+map.uniqueID)%600!=0)return;
        Prune(nextVisit,now);Prune(drinkUntil,now);
    }
    public override void ExposeData()
    {
        Scribe_Collections.Look(ref nextVisit, "lpNextVisit", LookMode.Reference, LookMode.Value, ref keys, ref values);
        // Read the old list only to migrate existing saves; new visits carry an expiry.
        if(Scribe.mode!=LoadSaveMode.Saving)Scribe_Collections.Look(ref waitingForDrink,"lpWaitingForDrink",LookMode.Reference);
        Scribe_Collections.Look(ref drinkUntil,"lpDrinkUntil",LookMode.Reference,LookMode.Value,ref drinkKeys,ref drinkValues);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            nextVisit ??= new();
            drinkUntil??=new();int now=Find.TickManager.TicksGame;
            if(waitingForDrink!=null)foreach(var p in waitingForDrink)
                if(p!=null&&!p.Destroyed&&!drinkUntil.ContainsKey(p))drinkUntil[p]=now+DrinkWindow;
            waitingForDrink?.Clear();
            // Map/SpawnSetup restoration may not be complete during PostLoadInit; prune after loading on map ticks.
        }
    }
}

// Legacy tool entry point, no longer inserted ahead of native learning. New learning play is offered by
// LearningGiver_Playground; the native JobGiver_Learn chooses whether that desire is active.
public sealed class JobGiver_PlayOnSwing : ThinkNode_JobGiver
{
    protected override Job TryGiveJob(Pawn pawn)=>PlayChoice.Choose(pawn,false);
}

public sealed class JobGiver_IdlePlay : ThinkNode_JobGiver
{
    protected override Job TryGiveJob(Pawn pawn)
    {
        if(!pawn.DevelopmentalStage.Child())return PlaygroundCouples.TryDate(pawn);
        return PlayChoice.Refresh(pawn,PlaySource.Idle)??(PlayChoice.IdleRoll()?PlayChoice.Choose(pawn,true):null);
    }
    // Tagged as play, like learning play, not as idleness: the idle node's ThinkNode_Tagger tags only untagged results,
    // and an Idle tag would put a playing child in the idle-colonist alert and the colonist bar's idle icon.
    public override ThinkResult TryIssueJobPackage(Pawn pawn,JobIssueParams jobParams)
    {
        var result=base.TryIssueJobPackage(pawn,jobParams);
        return result.IsValid?new ThinkResult(result.Job,result.SourceNode,JobTag.SatisfyingNeeds):result;
    }
}

public static class PlayChoice
{
    public const float IdleChance=.5f;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool IdleRoll()=>Rand.Value<IdleChance;

    public static bool CanChoose(Pawn pawn,bool idle)=>pawn?.Spawned==true&&!pawn.Drafted&&!pawn.Downed&&!pawn.InMentalState
        &&pawn.DevelopmentalStage.Child()&&pawn.needs?.learning!=null&&(idle||pawn.needs.learning.CurLevel<.9f)
        &&!PawnUtility.WillSoonHaveBasicNeed(pawn,.1f)
        &&(pawn.timetable?.CurrentAssignment??TimeAssignmentDefOf.Anything).allowJoy
        &&pawn.Map.GetComponent<PlaygroundMap>().Ready(pawn);

    public static Job Refresh(Pawn pawn,PlaySource source)
    {
        var job=ExpansionCatalog.TryChildRefresh(pawn);
        return job==null?null:Done(pawn,job,source,"LP_Rec_Drink".Translate(),false,true);
    }

    public static Job Choose(Pawn pawn,bool idle)
    {
        if(!CanChoose(pawn,idle))return null;
        var source=idle?PlaySource.Idle:PlaySource.Learning;
        var why=PlaygroundRecords.On?new List<string>():null;
        var remaining=PlaygroundActivities.ChildActivities.ToList();
        Job waiting=null;
        // Same order as vanilla recreation: draw an activity, then find its closest usable facility.
        // Unavailable activities are removed and another is tried, without distance entering the activity weights.
        while(remaining.TryRandomElementByWeight(d=>d.Worker.GetChance(pawn),out var activity))
        {
            remaining.Remove(activity);
            var best=PlaygroundActivities.Closest(pawn,activity.thingDefs);
            if(best==null){waiting??=PlaygroundSocial.TryWait(pawn,activity.thingDefs);continue;}
            var together=PlaygroundSocial.TryJoin(pawn,activity.thingDefs,best);
            if(together!=null)return Done(pawn,together,PlaySource.Invitation,"LP_Rec_Joined".Translate(best.LabelShort),!idle,idle);
            var running=PlaygroundRunning.TryInstead(pawn,best,why);
            if(running!=null)return Done(pawn,running,source,"LP_Rec_Running".Translate(),!idle,idle);
            // Pick the ride before inviting anyone: a nearby shared ride must not crowd out slides or other solo play.
            var withFriend=PlaygroundCompanion.TryFriend(pawn,why,best);
            if(withFriend!=null)return Done(pawn,withFriend,source,"LP_Rec_Chose".Translate(best.LabelShort),!idle,idle);
            var job=JobMaker.MakeJob(activity.jobDef,best);
            bool called=PlaygroundCompanion.CallFor(pawn,best,job,why);
            string text="LP_Rec_Chose".Translate(best.LabelShort);
            if(!called&&why?.Count>0)text+=" "+"LP_Rec_Nobody".Translate(string.Join(" / ",why));
            return Done(pawn,job,source,text,!idle,idle);
        }
        return waiting==null?null:Done(pawn,waiting,source,"LP_Rec_Queue".Translate(waiting.targetA.Thing.LabelShort),false,idle);
    }

    // Free-time play is not a learning desire, even where a shared helper (PlaygroundSocialAdapter.JobFor) marks a
    // child's ride as one. The game only stores this flag (1.7: set by JobGiver_Learn, read by nothing else).
    static Job Done(Pawn pawn,Job job,PlaySource source,string text,bool learning,bool idle)
    {
        if(idle)job.isLearningDesire=false;else if(learning)job.isLearningDesire=true;
        PlaygroundRecords.Tag(job,source,text);
        PlaygroundRecords.Decided(pawn,job,source);
        return job;
    }
}

public sealed class JobDriver_RideSwing : JobDriver
{
    private int rideStart = -1;
    private bool mounted,cleanupDone;
    private int holdSince=-1;
    public int CompanionCallerJob=-1;
    public int Slot=-1;
    private Building_Swing Swing => job.targetA.Thing as Building_Swing;
    public int Age => mounted && rideStart >= 0 ? Math.Max(0, Find.TickManager.TicksGame - rideStart) : -1;
    float toddlerLift=-1; // Not saved: measured again on the first tick after a load.
    public Vector3 Torso => toddlerLift>=0 ? new Vector3(0,0,toddlerLift*SwingMotion.SeatedBlend(Age)) : SwingMotion.TorsoOffset(Age);
    public override Vector3 ForcedBodyOffset=>Age>=0&&Swing?.Spawned==true
        ?Swing.Position.ToVector3Shifted()+PlaygroundTuck.Shift(Swing)*SwingMotion.SeatedBlend(Age)+Vector3.Lerp(Swing.EntryAt(Slot).ToVector3Shifted()-Swing.Position.ToVector3Shifted(),
            (TurnSwing.Turned(Swing)?TurnSwing.Seat(Swing,Slot,Age):DesignArt.Timber(Swing)?TimberArt.SwingSeat(Slot,Age):SelectedSwingArt.Seat(Slot,Age)),SwingMotion.SeatedBlend(Age))-pawn.Drawer.tweener.TweenedPos:Vector3.zero;
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        if(Swing==null||!Swing.CanUse(pawn)||!pawn.Reserve(Swing,job,2,0,null,errorOnFailed))return false;
        Slot=Swing.FreeSlot(pawn);if(Slot<0)return false;
        Swing.SetRider(Slot,pawn);return true;
    }
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref rideStart, "lpRideStart", -1);
        Scribe_Values.Look(ref mounted, "lpMounted", false);
        Scribe_Values.Look(ref cleanupDone,"lpCleanupDone",false);
        Scribe_Values.Look(ref Slot,"lpSwingSlot",0);
        Scribe_Values.Look(ref holdSince,"lpEntranceHoldStart",-1);
        Scribe_Values.Look(ref CompanionCallerJob,"lpCompanionCallerJob",-1);
    }
    private void ClearRide()
    {
        if(cleanupDone)return;cleanupDone=true;
        bool played=mounted;
        mounted = false;
        if(Swing!=null&&Slot>=0&&Swing.PawnAt(Slot)==pawn)Swing.SetRider(Slot,null);
        if (pawn.Drawer.renderer.CurAnimation == LPDefOf.LP_SwingPose) pawn.Drawer.renderer.SetAnimation(null);
        if(pawn.Spawned){var visits=pawn.Map.GetComponent<PlaygroundMap>();if(played)visits.Finished(pawn);else visits.Failed(pawn);}
    }
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOn(() => pawn.Drafted || pawn.Downed || PlaygroundToddlers.StopsPlay(pawn) || !PlaygroundUse.CanParticipate(pawn));
        this.FailOn(()=>PlaygroundCare.ShouldStop(pawn,job));
        this.FailOn(()=>PlaygroundCompanion.ShouldStopBeforeMount(this,CompanionCallerJob,rideStart>=0));
        AddFinishAction(_ => ClearRide());
        if (Swing == null) yield break;
        yield return Toils_Goto.GotoCell(Swing.EntryAt(Slot),PathEndMode.OnCell);
        var ride = ToilMaker.MakeToil("RidePlaygroundSwing");
        ride.initAction = () =>
        {
            if(Slot<0||(Swing.PawnAt(Slot)!=pawn&&Swing.JobAt(Slot)!=null)){EndJobWith(JobCondition.Incompletable);return;}
            pawn.pather.StopDead();
            if(!PlaygroundCompanion.HoldAtEntrance(this,Swing,ref holdSince))Mount();
        };
        ride.tickIntervalAction = delta =>
        {
            if (Swing?.Spawned != true) { EndJobWith(JobCondition.Incompletable); return; }
            Swing.SetRider(Slot,pawn);
            if(!mounted)
            {
                if(PawnUtility.WillSoonHaveBasicNeed(pawn,-.05f)||!PlaygroundCare.IsCare(job.def)&&!(pawn.timetable?.CurrentAssignment??TimeAssignmentDefOf.Anything).allowJoy)
                {EndJobWith(JobCondition.InterruptOptional);return;}
                if(PlaygroundCompanion.HoldAtEntrance(this,Swing,ref holdSince))return;
                Mount();
            }
            pawn.Rotation = PlaygroundTurn.Facing(Swing,Rot4.South);
            if (toddlerLift<0 && PlaygroundToddlers.Walking(pawn)) toddlerLift=PlaygroundToddlers.SwingLift(pawn);
            if (pawn.Drawer.renderer.CurAnimation == null) pawn.Drawer.renderer.SetAnimation(LPDefOf.LP_SwingPose);
            if (PawnUtility.WillSoonHaveBasicNeed(pawn, -.05f) || !PlaygroundCare.IsCare(job.def) && !(pawn.timetable?.CurrentAssignment ?? TimeAssignmentDefOf.Anything).allowJoy)
            { EndJobWith(JobCondition.InterruptOptional); return; }
            if (Age >= SwingMotion.MountTicks && Age < SwingMotion.MountTicks + SwingMotion.RideTicks)
                if(PlaygroundUse.Reward(pawn,delta,Swing))return;
            if (Age >= SwingMotion.TotalTicks) ReadyForNextToil();
        };
        ride.handlingFacing = true;
        ride.defaultCompleteMode = ToilCompleteMode.Never;
        ride.AddFinishAction(ClearRide);
        yield return ride;
        yield return Toils_Goto.GotoCell(Swing.LeaveAt(Slot),PathEndMode.OnCell);
        yield return PlaygroundReaction.AfterRide(pawn);
    }
    void Mount()
    {
        pawn.Notify_Teleported(endCurrentJob:false);
        pawn.Rotation = PlaygroundTurn.Facing(Swing,Rot4.South);
        Swing.SetRider(Slot,pawn); rideStart = Find.TickManager.TicksGame; mounted = true;
        if (pawn.Drawer.renderer.CurAnimation == null) pawn.Drawer.renderer.SetAnimation(LPDefOf.LP_SwingPose);
    }
}

// Native pawn render tree: preserve clothing, hair and body instead of drawing a substitute pawn.
public sealed class SwingAnimationWorker : AnimationWorker_Keyframes
{
    public override bool Enabled(AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        => parms.pawn?.jobs?.curDriver is JobDriver_RideSwing d && d.Age >= 0 && !parms.pawn.Downed;
    public override float AngleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        => 0;
    public override Vector3 OffsetAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        => parms.pawn.jobs.curDriver is JobDriver_RideSwing d ? d.Torso
            +new Vector3(0,0,PlaygroundUse.AdultSeatLift(parms.pawn)*SwingMotion.SeatedBlend(d.Age)) : Vector3.zero;
    public override Vector3 ScaleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        => Vector3.one;
    public override GraphicStateDef GraphicStateAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        => null;
}
