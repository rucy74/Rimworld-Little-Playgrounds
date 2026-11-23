using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LittlePlaygrounds;

[StaticConstructorOnStartup]
public static class ExpansionCatalog
{
    public static readonly ThingDef[] PlayDefs=DefDatabase<ThingDef>.AllDefsListForReading
        .Where(d=>d.GetModExtension<ExpansionProperties>() is {seats:>0}).ToArray();
    // A facility people can use must have a job; a missing mapping would otherwise make a job with no definition.
    static ExpansionCatalog()
    {
        foreach(var d in PlayDefs)
            if(ExpansionJobs.ForKind(d.GetModExtension<ExpansionProperties>().kind)==null)
                Log.Error($"[Little Playgrounds] {d.defName} ({d.GetModExtension<ExpansionProperties>().kind}) has seats but no job in ExpansionJobs.ForKind.");
    }
    public static Job TryChildRefresh(Pawn p)
    {
        if(p?.Spawned!=true||!p.DevelopmentalStage.Child()||p.Drafted||p.Downed||p.InMentalState
            ||!(p.timetable?.CurrentAssignment??TimeAssignmentDefOf.Anything).allowJoy||PawnUtility.WillSoonHaveBasicNeed(p,.1f)
            ||!p.Map.GetComponent<PlaygroundMap>().WantsDrink(p))return null;
        var fountain=p.Map.listerThings.ThingsOfDef(ExpansionDefOf.LP_DrinkingFountain).OfType<Building_PlaygroundFacility>()
            .Where(b=>(b.Position-p.Position).LengthHorizontalSquared<=20*20&&b.CanUse(p))
            .OrderBy(b=>(b.Position-p.Position).LengthHorizontalSquared).FirstOrDefault();
        return fountain==null?null:JobMaker.MakeJob(ExpansionDefOf.LP_DrinkAtFountain,fountain);
    }
}

[DefOf]
public static class ExpansionDefOf
{
    public static ThingDef LP_SpringFox,LP_Roundabout,LP_Playhouse,LP_DrinkingFountain,LP_Hopscotch,LP_SplashPad,LP_SteppingStone,LP_SteppingStump,LP_BalanceLog,LP_PlayFence,LP_PlayEntrance,LP_GardenFountain;
    public static JobDef LP_RideSpringFox,LP_SpinRoundabout,LP_PlayShop,LP_PlayHopscotch,LP_PlaySplashPad,LP_WalkCourse,LP_BounceTogether,LP_DrinkAtFountain;
    public static AnimationDef LP_ExpansionPose;
    static ExpansionDefOf()=>DefOfHelper.EnsureInitializedInCtor(typeof(ExpansionDefOf));
}

// Each expansion facility has its own JobDef (name, report text, recreation type, participants) while all of them
// share one motion driver, JobDriver_VisitPlayground, which branches on the facility kind.
public static class ExpansionJobs
{
    public static JobDef For(Building_PlaygroundFacility b)=>ForKind(b.Kind);
    // Fence, Entrance and Fountain have no seats and no job. Add every new usable kind here (checked at startup).
    public static JobDef ForKind(ExpansionKind kind)=>kind switch
    {
        ExpansionKind.SpringRider=>ExpansionDefOf.LP_RideSpringFox,ExpansionKind.Roundabout=>ExpansionDefOf.LP_SpinRoundabout,
        ExpansionKind.Playhouse=>ExpansionDefOf.LP_PlayShop,ExpansionKind.Hopscotch=>ExpansionDefOf.LP_PlayHopscotch,
        ExpansionKind.SplashPad=>ExpansionDefOf.LP_PlaySplashPad,ExpansionKind.SteppingStone or ExpansionKind.BalanceLog=>ExpansionDefOf.LP_WalkCourse,
        ExpansionKind.SharedTrampoline=>ExpansionDefOf.LP_BounceTogether,ExpansionKind.Drink=>ExpansionDefOf.LP_DrinkAtFountain,
        ExpansionKind.CompactSlide=>CompactDefOf.LP_RideCompactSlide,ExpansionKind.FloorRocker=>CompactDefOf.LP_RideFloorRocker,
        ExpansionKind.BallPit=>CompactDefOf.LP_PlayBallPit,
        ExpansionKind.BubbleTub=>MorePlayDefOf.LP_BlowBubbles,ExpansionKind.PullBar=>MorePlayDefOf.LP_SwingOnBar,
        ExpansionKind.Kite=>HeldPlayDefOf.LP_FlyKite,ExpansionKind.JumpRope=>HeldPlayDefOf.LP_JumpRope,_=>null
    };
    public static bool IsVisit(JobDef d)=>d!=null&&d.driverClass==typeof(JobDriver_VisitPlayground);
}

// Every moving object owns only its visitors; no vanilla production or pawn jobs are patched.
public sealed class Building_PlaygroundFacility:Building
{
    // Follows a push from Tucked Furniture; zero without it (PlaygroundTuck).
    public override Vector3 DrawPos=>base.DrawPos+PlaygroundTuck.Shift(this);
    List<Pawn> visitors;
    float angle,speed;
    readonly bool[] links=new bool[4];
    int linksAt=-1;
    static readonly IntVec3[] Directions={IntVec3.North,IntVec3.East,IntVec3.South,IntVec3.West};
    static readonly IntVec3[] RoundEntries={IntVec3.South*2,IntVec3.East*2,IntVec3.North*2,IntVec3.West*2};
    public ExpansionKind Kind=>def.GetModExtension<ExpansionProperties>().kind;
    public int Capacity=>def.GetModExtension<ExpansionProperties>().seats;
    public bool IsCourse=>Kind==ExpansionKind.SteppingStone||Kind==ExpansionKind.BalanceLog;
    public bool Interactive=>Capacity>0;
    public override Graphic Graphic=>DesignArt.Timber(this)?DesignArt.Graphic(this):Kind==ExpansionKind.SharedTrampoline?PaletteArt.GraphicFor(this):base.Graphic;
    public float SpinAngle=>angle;
    public float SpinSpeed=>speed;
    public Pawn PawnAt(int slot)=>visitors!=null&&slot>=0&&slot<visitors.Count?visitors[slot]:null;
    public JobDriver_VisitPlayground VisitAt(int slot)
    {
        var p=PawnAt(slot);
        return p?.Spawned==true&&p.Map==Map&&ExpansionJobs.IsVisit(p.CurJobDef)&&p.CurJob.targetA.Thing==this
            ?p.jobs.curDriver as JobDriver_VisitPlayground:null;
    }
    public void SetVisitor(int slot,Pawn p)
    {visitors??=new();while(visitors.Count<Capacity)visitors.Add(null);visitors[slot]=p;}
    // Turnable kinds (PlaygroundTurn) map the north entrance onto the footprint as it stands; the others keep Position + offset.
    public IntVec3 EntryAt(int slot)=>PlaygroundTurn.Cell(this,Kind switch
    {
        ExpansionKind.CompactSlide=>new IntVec3(0,0,-2),
        ExpansionKind.FloorRocker=>new IntVec3(slot==0?-1:1,0,-1),
        ExpansionKind.BallPit=>new IntVec3(slot==0?-1:1,0,-2),
        ExpansionKind.Roundabout=>RoundEntries[Math.Max(0,slot)%4],
        ExpansionKind.SharedTrampoline=>slot==2?IntVec3.North*3:new IntVec3(slot==0?-2:2,0,-3),
        ExpansionKind.Playhouse=>new IntVec3(slot==0?-1:1,0,-2),
        ExpansionKind.Drink=>new IntVec3(slot==0?-1:1,0,-1),
        ExpansionKind.Hopscotch=>IntVec3.South*3,
        ExpansionKind.SplashPad=>new IntVec3(slot-1,0,-2),
        ExpansionKind.SteppingStone or ExpansionKind.BalanceLog=>IntVec3.Zero,
        ExpansionKind.BubbleTub=>slot==0?IntVec3.South:IntVec3.East,
        ExpansionKind.PullBar=>BarEntry(slot),
        ExpansionKind.Kite or ExpansionKind.JumpRope=>new IntVec3(slot==0?-1:1,0,-1),
        _=>IntVec3.South
    });
    // Below the bar, whichever way a gravship landed it: Odyssey turns buildings that are not square (Gravship.GetPlacementValues). The picture always faces north.
    IntVec3 BarEntry(int slot){var r=this.OccupiedRect();return new IntVec3(r.minX+slot-Position.x,0,r.minZ-1-Position.z);}
    public bool ClearCell(IntVec3 c,Pawn p)=>Spawned&&c.InBounds(Map)&&c.Standable(Map)&&!c.IsForbidden(p);
    public int FreeSlot(Pawn p)
    {
        for(int i=0;i<Capacity;i++)if(PawnAt(i)==p&&VisitAt(i)!=null)return i;
        if(Kind==ExpansionKind.Drink)
        {
            int preferred=p.DevelopmentalStage.Child()?0:1;
            if(VisitAt(preferred)==null&&ClearCell(EntryAt(preferred),p)&&PlaygroundAvailability.Entry(p,EntryAt(preferred))==PlayBlocked.None)return preferred;
        }
        for(int i=0;i<Capacity;i++)if(VisitAt(i)==null&&ClearCell(EntryAt(i),p)&&PlaygroundAvailability.Entry(p,EntryAt(i))==PlayBlocked.None&&MorePlay.SlotOpen(this,i,p))return i;
        return -1;
    }
    public bool CanUse(Pawn p,bool reserve=true)=>UseFailure(p,reserve)==PlayBlocked.None;
    public PlayBlocked UseFailure(Pawn p,bool reserve=true)
    {
        if(!Interactive)return PlayBlocked.Unavailable;
        var reason=PlaygroundAvailability.Common(this,p);if(reason!=PlayBlocked.None)return reason;
        if(IsCourse)
        {
            var course=BuildCourse(p);if(course.Count<2)return PlayBlocked.CourseTooShort;
            foreach(var c in course){reason=PlaygroundAvailability.Cell(p,c);if(reason!=PlayBlocked.None)return reason;}
        }
        if(CompactMotion.IsCompact(Kind))foreach(var c in this.OccupiedRect())
        {reason=PlaygroundAvailability.Cell(p,c);if(reason!=PlayBlocked.None)return reason;}
        if(Kind==ExpansionKind.CompactSlide)
        {reason=PlaygroundAvailability.Cell(p,CompactMotion.SlideExit(this));if(reason!=PlayBlocked.None)return reason;}
        if(Kind==ExpansionKind.BallPit)
        {reason=PlaygroundAvailability.Cell(p,CompactMotion.PitEntrance(this));if(reason!=PlayBlocked.None)return reason;}
        if(Kind==ExpansionKind.SplashPad&&(Position.GetTemperature(Map)<10||Map.snowGrid.GetDepth(Position)>.1f))return PlayBlocked.WaterCold;
        if(Kind==ExpansionKind.Kite&&!KiteMotion.SkyClear(this))return PlayBlocked.NoSky;
        reason=PlayBlocked.Busy;
        for(int i=0;i<Capacity;i++)
        {
            if(VisitAt(i)!=null&&PawnAt(i)!=p)continue;
            if(PawnAt(i)!=p&&!MorePlay.SlotOpen(this,i,p)){if(reason==PlayBlocked.Busy)reason=PlayBlocked.Entrance;continue;}
            reason=PlaygroundAvailability.Entry(p,EntryAt(i));
            if(reason==PlayBlocked.None)return !reserve||p.CanReserve(this,Capacity,0)?PlayBlocked.None:PlayBlocked.Busy;
        }
        return reason;
    }
    public List<IntVec3> BuildCourse(Pawn p)
    {
        var result=new List<IntVec3>{Position};var visited=new HashSet<IntVec3>{Position};
        var cursor=Position;
        // A bounded, connected walk. Branches use a stable order; no global path scan.
        while(result.Count<24)
        {
            IntVec3 next=IntVec3.Invalid;
            foreach(var d in GenAdj.CardinalDirections)
            {
                var c=cursor+d;
                if(visited.Contains(c)||!ClearCell(c,p)||!p.CanReserve(c))continue;
                if(c.GetThingList(Map).Any(t=>t is Building_PlaygroundFacility f&&f.IsCourse&&!f.IsForbidden(p)&&CompPlaygroundAccess.Allows(f,p)))
                {next=c;break;}
            }
            if(!next.IsValid)break;result.Add(next);visited.Add(next);cursor=next;
        }
        return result;
    }
    // Refreshed by frame, not by tick: a fence planned next to this one while the game is paused shows the join at once.
    public bool[] Links()
    {
        int now=UnityEngine.Time.frameCount;if(linksAt>=0&&now-linksAt<20)return links;linksAt=now;
        if(!IsCourse)return FenceLinks(Map,Position,links);
        for(int i=0;i<4;i++)
        {
            var c=Position+Directions[i];links[i]=false;if(!c.InBounds(Map))continue;
            links[i]=c.GetThingList(Map).OfType<Building_PlaygroundFacility>().Any(f=>f.IsCourse);
        }
        return links;
    }
    // Fences join fences and the entrance, built or planned (blueprint or frame), as vanilla's linked fence joins its own
    // blueprints (ThingDefGenerator_Buildings copies the link flags onto the blueprint). Course pieces join built ones only.
    static bool JoinsFence(Thing t)=>t is Building_PlaygroundFacility f?f.Kind is ExpansionKind.Fence or ExpansionKind.Entrance
        :t is Blueprint or Frame&&t.def.entityDefToBuild is ThingDef d&&d.GetModExtension<ExpansionProperties>()?.kind is ExpansionKind.Fence or ExpansionKind.Entrance;
    public static bool[] FenceLinks(Map map,IntVec3 at,bool[] into)
    {
        for(int i=0;i<4;i++){var c=at+Directions[i];into[i]=c.InBounds(map)&&c.GetThingList(map).Any(JoinsFence);}
        return into;
    }
    protected override void Tick()
    {
        base.Tick();if(Kind!=ExpansionKind.Roundabout)return;
        bool riding=false,boarding=false;
        for(int i=0;i<Capacity;i++)
        {
            var visit=VisitAt(i);if(visit==null)continue;
            if((visit.Arrived&&!visit.Holding&&visit.Age<90)||visit.Age>=visit.Duration-90)boarding=true;
            else if(visit.Age>=90)riding=true;
        }
        speed=Mathf.MoveTowards(speed,riding&&!boarding?PlaygroundMotion.RoundTarget(Find.TickManager.TicksGame,thingIDNumber):0,
            boarding||!riding?.045f:.018f);
        angle=Mathf.Repeat(angle+speed,360);
    }
    public float Rock=>VisitAt(0) is {Age:>=0} v ? PlaygroundMotion.SpringRock(v.Age)*v.Blend : 0;
    public override void ExposeData()
    {
        base.ExposeData();Scribe_Collections.Look(ref visitors,"lpFacilityVisitors",LookMode.Reference);
        Scribe_Values.Look(ref angle,"lpRoundaboutAngle");Scribe_Values.Look(ref speed,"lpRoundaboutSpeed");
    }
    public override void DeSpawn(DestroyMode mode=DestroyMode.Vanish)
    {
        for(int i=0;i<Capacity;i++)VisitAt(i)?.EndJobWith(JobCondition.Incompletable);
        visitors?.Clear();base.DeSpawn(mode);
    }
    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn p)
    {
        foreach(var option in base.GetFloatMenuOptions(p))yield return option;
        var job=ExpansionJobs.For(this);if(job==null)yield break;
        yield return PlaygroundAvailability.Menu(Kind==ExpansionKind.Drink?"LP_Menu_TakeDrink".Translate():IsCourse?"LP_Menu_PlayCourse".Translate():"LP_Menu_PlayAt".Translate(Label),UseFailure(p),
            ()=>p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(job,this),JobTag.SatisfyingNeeds));
    }
    protected override void DrawAt(Vector3 at,bool flip=false)
    {
        if(TurnRides.Draw(this,at))return;
        bool flippedFox=Kind==ExpansionKind.SpringRider&&PlaygroundTurn.Map(this)==TurnMap.FlipNorth;
        if(DesignArt.Timber(this)&&Kind==ExpansionKind.SpringRider)
        {if(flippedFox)TimberArt.FoxDrawFlipped(at,Rock,PaletteArt.Of(this));else TimberArt.FoxDraw(at,Rock,PaletteArt.Of(this));return;}
        if(flippedFox){RestyleArt.Spring(at,Rock,true);return;}
        if(DesignArt.Timber(this)&&Kind==ExpansionKind.Roundabout){TimberArt.RoundDraw(at,angle,PaletteArt.Of(this));return;}
        if(CompactMotion.IsCompact(Kind)){CompactArt.Draw(this,at);return;}
        if(MorePlay.Handles(Kind)){MorePlayArt.Draw(this,at);return;}
        if(Kind==ExpansionKind.SharedTrampoline){ActiveRideArt.DrawShared(this,at);return;}
        if(def.GetModExtension<ExpansionProperties>().woodenStep){RestyleArt.Stump(at,thingIDNumber);return;}
        if(Kind==ExpansionKind.BalanceLog){RestyleArt.Balance(at,Rotation.IsHorizontal);return;}
        int mask=0;for(int i=0;i<Capacity;i++)if(VisitAt(i) is {Age:>=60} v&&v.Age<v.Duration-60)mask|=1<<i;
        ExpansionArt.Draw(Kind,at,Kind==ExpansionKind.Roundabout?angle:Kind==ExpansionKind.SpringRider?Rock:Find.TickManager.TicksGame,mask,Links());
    }
    public override string GetInspectString()=>PlaygroundInspect.Append(InspectBody(),PlaygroundRecords.InspectLine(this));
    string InspectBody()
    {
        var text=base.GetInspectString();
        if(IsCourse)return PlaygroundInspect.Append(text,CourseGuide.Inspect(this));
        if(Kind==ExpansionKind.SplashPad&&Position.GetTemperature(Map)<10)return PlaygroundInspect.Append(text,"LP_Inspect_SplashClosed".Translate());
        if(Interactive)return PlaygroundInspect.Append(text,"LP_VisitorCount".Translate(Enumerable.Range(0,Capacity).Count(i=>VisitAt(i)!=null),Capacity));
        return text;
    }
}

public sealed class JobDriver_VisitPlayground:JobDriver
{
    static readonly Vector3[] HopMarks=PlaygroundContact.HopMarks;
    static readonly Vector3[] SplashMarks={new(-.95f,0,-.9f),new(-.25f,0,-.2f),new(.85f,0,.9f),new(.95f,0,-.8f)};
    public int Slot=-1;
    int start=-1,holdSince=-1;
    int courseStumbleAt=-1,courseStumbleSide=1;
    public int CompanionCallerJob=-1;
    bool mounted,cleanupDone;
    bool arrived;
    bool holding;   // waiting at the entrance for a companion (not saved; the next tick decides again)
    List<IntVec3> course;
    float[] footLifts;
    public float FootLift=>footLifts?[pawn.Rotation.AsInt]??(pawn.DevelopmentalStage.Child()?.28f:.43f);
    public Building_PlaygroundFacility Facility=>job.targetA.Thing as Building_PlaygroundFacility;
    public int Age=>mounted&&start>=0?Math.Max(0,Find.TickManager.TicksGame-start):-1;
    public bool Arrived=>arrived;
    public bool Holding=>holding;
    public int MotionSeed=>job.loadID;
    public int Duration=>Facility?.Kind==ExpansionKind.PullBar?BarMotion.Duration:Facility?.Kind==ExpansionKind.Kite?KiteMotion.Duration:Facility?.Kind==ExpansionKind.JumpRope?RopeMotion.Duration:Facility?.Kind==ExpansionKind.CompactSlide?CompactMotion.SlideTicks:Facility?.Kind==ExpansionKind.Drink?240:Facility?.IsCourse==true?Math.Max(420,(course?.Count??2)*100+120)+(courseStumbleAt>=0?CourseMotion.RecoveryTicks:0):1080;
    public float Blend=>Age<0?0:Facility?.Kind==ExpansionKind.FloorRocker?CompactMotion.RockSeated(Age,Duration):Age<60?SwingMotion.Smooth(Age/60f):Age>Duration-60?1-SwingMotion.Smooth((Age-Duration+60)/60f):1;
    public IReadOnlyList<IntVec3> Course=>course;
    public int CourseAge=>CourseMotion.Clock(Age,courseStumbleAt);
    public float CourseLean=>CourseMotion.Lean(Age,courseStumbleAt,courseStumbleSide,pawn.DevelopmentalStage.Child());
    public float HopPhase { get {PlaygroundContact.Step(Facility?.IsCourse==true?CourseAge:Age,Facility?.IsCourse==true?Math.Max(2,course?.Count??2):HopMarks.Length,Facility?.IsCourse==true?50:42,out _,out _,out float phase);return phase;} }
    Vector3 CoursePoint(int index)
    {
        var cell=course[index];var tile=cell.GetThingList(pawn.Map).OfType<Building_PlaygroundFacility>().FirstOrDefault(f=>f.IsCourse);
        if(tile==null)return (cell-Facility.Position).ToVector3();
        float top=tile.def.GetModExtension<ExpansionProperties>().woodenStep
            ?(Math.Abs(tile.thingIDNumber)%3 switch {0=>.24f,1=>.243f,_=>.273f}):tile.Kind==ExpansionKind.BalanceLog?.0675f:.025f;
        // A two-cell log contributes two distinct footsteps, not its center twice.
        var point=tile.Kind==ExpansionKind.BalanceLog?cell.ToVector3Shifted()+PlaygroundTuck.Shift(tile):tile.DrawPos;
        return point-Facility.DrawPos+new Vector3(0,0,top);
    }
    // The hopscotch squares turn with the board (PlaygroundTurn).
    Vector3 Mark(int i)=>PlaygroundTurn.Ground(Facility,HopMarks[i]);
    Vector3 StepContact(bool hop)
    {
        int count=hop?HopMarks.Length:course?.Count??0;if(count<2)return Vector3.zero;
        PlaygroundContact.Step(hop?Age:CourseAge,count,hop?42:50,out int from,out int to,out float phase);
        return Vector3.Lerp(hop?Mark(from):CoursePoint(from),hop?Mark(to):CoursePoint(to),SwingMotion.Smooth(phase));
    }
    public Rot4 PlayFacing
    {
        get
        {
            var b=Facility;if(b==null)return Rot4.South;
            if(b.Kind==ExpansionKind.CompactSlide)return PlaygroundTurn.Facing(b,CompactMotion.SlideFacing(Age));
            if(b.Kind==ExpansionKind.FloorRocker)return PlaygroundTurn.Facing(b,Slot==0?Rot4.East:Rot4.West);
            if(b.Kind==ExpansionKind.BubbleTub)
            {
                if(Slot==0)return BubbleMotion.BlowerFacing(b);
                BubbleMotion.Catcher(b,out _,out _,out var running);return running;
            }
            if(b.Kind==ExpansionKind.Kite)return Rot4.North;
            if(b.Kind==ExpansionKind.BallPit)
            {
                var delta=CompactMotion.PitPath(Math.Min(Age+5,Duration),Duration,Slot,MotionSeed)-CompactMotion.PitPath(Age,Duration,Slot,MotionSeed);
                return delta.sqrMagnitude>.000001f?Rot4.FromAngleFlat(delta.AngleFlat()):Rot4.South;
            }
            if(b.Kind==ExpansionKind.Roundabout)return Rot4.FromAngleFlat((-Contact).AngleFlat());
            if(b.Kind==ExpansionKind.SharedTrampoline)return Rot4.FromAngleFlat(SharedTrampolineMotion.Direction(Age,Slot,MotionSeed).AngleFlat());
            if(b.Kind==ExpansionKind.Hopscotch||b.IsCourse)
            {
                bool hop=b.Kind==ExpansionKind.Hopscotch;int count=hop?HopMarks.Length:course?.Count??0;
                if(count<2)return Rot4.South;
                PlaygroundContact.Step(hop?Age:CourseAge,count,hop?42:50,out int from,out int to,out _);
                var delta=(hop?Mark(to)-Mark(from):(course[to]-course[from]).ToVector3());
                return Rot4.FromAngleFlat(delta.AngleFlat());
            }
            return PlaygroundTurn.Facing(b,b.Kind==ExpansionKind.Drink?Rot4.North:b.Kind==ExpansionKind.SpringRider?Rot4.West:
                b.Kind==ExpansionKind.Playhouse?(Slot==0?Rot4.South:Rot4.North):Rot4.South);
        }
    }
    public Vector3 Contact
    {
        get
        {
            var b=Facility;if(b==null)return Vector3.zero;
            switch(b.Kind)
            {
                case ExpansionKind.CompactSlide:return PlaygroundTurn.Map(b)==TurnMap.North?CompactMotion.SlidePoint(Age):TurnRides.SlidePoint(b,Age);
                case ExpansionKind.BubbleTub:
                    if(Slot==0)return BubbleMotion.Spot;
                    BubbleMotion.Catcher(b,out var chasing,out _,out _);return chasing;
                case ExpansionKind.PullBar:return BarMotion.Contact(Age,Slot,pawn.DevelopmentalStage.Child());
                case ExpansionKind.Kite:return KiteMotion.Contact(Age,Slot);
                case ExpansionKind.JumpRope:return RopeMotion.Spot(Slot);
                case ExpansionKind.FloorRocker:return CompactMotion.RockSeat(b,Slot,CompactMotion.Rock(b));
                case ExpansionKind.BallPit:return CompactMotion.PitPath(Age,Duration,Slot,MotionSeed);
                case ExpansionKind.SpringRider:
                {
                    var map=PlaygroundTurn.Map(b);
                    if(map is TurnMap.East or TurnMap.West)return TurnRides.FoxContact(b,b.Rock);
                    var seat=DesignArt.Timber(b)?TimberArt.FoxContact(b.Rock):RestyleArt.SpringContact(b.Rock);
                    return map==TurnMap.FlipNorth?new Vector3(-seat.x,seat.y,seat.z):seat;
                }
                case ExpansionKind.Roundabout:
                    return DesignArt.Timber(b)?TimberArt.RoundContact(Slot,b.SpinAngle):Quaternion.AngleAxis(b.SpinAngle+Slot*90,Vector3.up)*new Vector3(0,0,-.85f);
                case ExpansionKind.SharedTrampoline:return SharedTrampolineMotion.Contact(Age,Slot,MotionSeed);
                case ExpansionKind.Playhouse:
                    return Slot==0?new Vector3(.58f+.06f*Mathf.Sin(Age*Mathf.PI/180),0,.1f):new Vector3(.65f,0,-.8f);
                case ExpansionKind.Drink:return new Vector3(Slot==0?-.672f:.672f,0,Slot==0?.12f:.22f);
                case ExpansionKind.Hopscotch:
                    return StepContact(true);
                case ExpansionKind.SplashPad:
                    // Visit the quiet edge, dart inward between jet pulses, then retreat.
                    var point=Follow(SplashMarks,85,Slot*70);
                    for(int n=0;n<4;n++)
                    {
                        float pulse=ExpansionMotion.SplashPulse(Find.TickManager.TicksGame,n);
                        var nozzle=ExpansionMotion.SplashNozzle(n);
                        var away=point-nozzle;float distance=away.magnitude;
                        if(pulse>.6f&&distance<.65f)
                            point+=(distance>.01f?away/distance:-nozzle.normalized)*(.35f*(pulse-.6f)/.4f*(1-distance/.65f));
                    }
                    point.x=Mathf.Clamp(point.x,-1.18f,1.18f);point.z=Mathf.Clamp(point.z,-1.18f,1.18f);return point;
                case ExpansionKind.SteppingStone:case ExpansionKind.BalanceLog:
                    return StepContact(false);
                default:return Vector3.zero;
            }
        }
    }
    Vector3 Follow(Vector3[] points,int segment,int delay=0)
    {
        if(points.Length<2)return points[0];
        float t=Math.Max(0,Age-60+delay)/(float)segment;
        float traversal=Mathf.PingPong(t,points.Length-1);
        int i=Math.Min(points.Length-2,(int)traversal);
        return Vector3.Lerp(points[i],points[i+1],SwingMotion.Smooth(traversal-i));
    }
    // Pawn_DrawTracker overwrites ForcedBodyOffset.y. Depth therefore belongs to
    // this job's root animation, after the native pawn origin has been resolved.
    public float PoseDepth=>Facility?.Kind switch
    {
        ExpansionKind.Playhouse when Slot==1=>.45f*Blend,
        // While approaching from the front, the rim must not cover the visitor's
        // head. Once seated/inside, restore the foreground lip over the lower body.
        ExpansionKind.FloorRocker when Math.Min(Age,Duration-Age)<60=>.45f,
        ExpansionKind.BallPit when Contact.z<-.6f=>.45f,
        ExpansionKind.PullBar when BarMotion.InFront(Age,Slot)=>.45f,
        _=>0
    };
    public Vector3 PoseScale=>Facility?.Kind==ExpansionKind.JumpRope?new Vector3(1,1,RopeMotion.Length(Age,Slot)):Vector3.one;
    public float PoseHeight=>Facility?.Kind switch
    {
        ExpansionKind.CompactSlide=>FootLift*Mathf.Clamp01(Age/30f)*Mathf.Clamp01((Duration-Age)/35f),
        ExpansionKind.BubbleTub=>BubbleHop*Blend,
        ExpansionKind.JumpRope=>RopeMotion.Hop(Age,Slot)*Blend,
        ExpansionKind.FloorRocker=>(.08f+PlaygroundUse.AdultSeatLift(pawn))*Blend,
        ExpansionKind.BallPit=>(.04f*Mathf.Sin(Age*.09f))*SwingMotion.Smooth((Math.Min(Age,Duration-Age)-120)/60f),
        ExpansionKind.SpringRider=>((pawn.DevelopmentalStage.Child()?.35f:.21f)+PlaygroundUse.AdultSeatLift(pawn))*Blend,
        ExpansionKind.Roundabout=>(FootLift+.025f)*Blend,
        ExpansionKind.Hopscotch=>(FootLift+.22f*Mathf.Sin(HopPhase*Mathf.PI))*Blend,
        ExpansionKind.SteppingStone=>(FootLift+.17f*Mathf.Sin(HopPhase*Mathf.PI))*Blend,
        ExpansionKind.BalanceLog=>FootLift*Blend,
        ExpansionKind.SharedTrampoline=>(FootLift+ActiveRideMotion.TrampolineLift+SharedTrampolineMotion.Height(Age,Slot,MotionSeed))*Blend,
        _=>0
    };
    float BubbleHop{get{if(Slot==0)return 0;BubbleMotion.Catcher(Facility,out _,out float hop,out _);return hop;}}
    public float PoseAngle=>Facility?.Kind switch
    {
        ExpansionKind.BubbleTub=>(Slot==0?BubbleMotion.Blowing(Facility)?2.5f*Mathf.Sin(Age*.1f):0:4*Mathf.Sin(Age*.2f))*Blend,
        ExpansionKind.PullBar=>BarMotion.Angle(Age,Slot)*Blend,
        ExpansionKind.Kite=>2.5f*Mathf.Sin(Age*.03f)*Blend,
        ExpansionKind.FloorRocker=>PlaygroundTurn.Map(Facility) is TurnMap.East or TurnMap.FlipEast?0:CompactMotion.Rock(Facility)*Blend,
        ExpansionKind.BallPit=>4*Mathf.Sin(Age*.035f)*SwingMotion.Smooth((Math.Min(Age,Duration-Age)-120)/60f),
        ExpansionKind.SpringRider=>PlaygroundTurn.Map(Facility) switch{TurnMap.FlipNorth=>-Facility.Rock,TurnMap.East or TurnMap.West=>0,_=>Facility.Rock},
        ExpansionKind.Drink=>-9*Mathf.Sin(Mathf.Clamp01((Age-60)/120f)*Mathf.PI)*Blend,
        ExpansionKind.Playhouse=>2*Mathf.Sin(Age*Mathf.PI/70)*Blend,
        ExpansionKind.SteppingStone=>CourseLean*Blend,
        ExpansionKind.BalanceLog=>(CourseLean+1.2f*Mathf.Sin(CourseAge*Mathf.PI/40))*Blend,
        ExpansionKind.Roundabout=>-Contact.x*5*Mathf.Clamp01(Facility.SpinSpeed/1.2f)*Blend,
        ExpansionKind.SharedTrampoline=>(pawn.DevelopmentalStage.Child()?SharedTrampolineMotion.Flip(Age,Slot,MotionSeed):0)
            +3*Mathf.Sin(Age*Mathf.PI/SharedTrampolineMotion.Period(Slot,MotionSeed))*Blend,
        _=>0
    };
    public override Vector3 ForcedBodyOffset=>Age>=0&&Facility?.Spawned==true
        // Use the actual visual building origin (even-size footprints have a half-cell offset).
        // Cancel only this visitor's native position tween/collision separation: another
        // pawn walking onto its entry cell must not push a seated rider off the saddle.
        ?(Facility.Kind==ExpansionKind.FloorRocker?Facility.DrawPos+CompactMotion.RockPath(Facility,Age,Duration,Slot,CompactMotion.Rock(Facility),Facility.EntryAt(Slot).ToVector3Shifted()-Facility.DrawPos):
            Facility.Kind==ExpansionKind.BallPit?Facility.DrawPos+CompactMotion.PitPath(Age,Duration,Slot,MotionSeed):
            Facility.Kind==ExpansionKind.CompactSlide?Facility.DrawPos+Contact:
            Vector3.Lerp(Facility.EntryAt(Slot).ToVector3Shifted(),Facility.DrawPos+Contact,Blend))-pawn.Drawer.tweener.TweenedPos:Vector3.zero;
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        var b=Facility;if(b==null||!b.CanUse(pawn)||!pawn.Reserve(b,job,b.Capacity,0,null,errorOnFailed))return false;
        Slot=b.FreeSlot(pawn);if(Slot<0)return false;
        if(b.IsCourse)
        {
            course=b.BuildCourse(pawn);if(course.Count<2)return false;
            foreach(var c in course)if(!pawn.Reserve(c,job,1,-1,null,errorOnFailed))return false;
        }
        b.SetVisitor(Slot,pawn);return true;
    }
    public override void ExposeData()
    {
        base.ExposeData();Scribe_Values.Look(ref Slot,"lpFacilitySlot",-1);Scribe_Values.Look(ref start,"lpFacilityStart",-1);
        Scribe_Values.Look(ref mounted,"lpFacilityMounted",false);Scribe_Collections.Look(ref course,"lpCourse",LookMode.Value);
        Scribe_Values.Look(ref cleanupDone,"lpCleanupDone",false);
        Scribe_Values.Look(ref arrived,"lpFacilityArrived",false);
        Scribe_Values.Look(ref holdSince,"lpEntranceHoldStart",-1);
        Scribe_Values.Look(ref CompanionCallerJob,"lpCompanionCallerJob",-1);
        Scribe_Values.Look(ref courseStumbleAt,"lpCourseStumbleAt",-1);
        Scribe_Values.Look(ref courseStumbleSide,"lpCourseStumbleSide",1);
        if(Scribe.mode==LoadSaveMode.PostLoadInit)
            LongEventHandler.ExecuteWhenFinished(()=>
            {
                if(mounted&&pawn?.jobs?.curDriver==this)
                {footLifts=PlaygroundContact.MeasureFeet(pawn);if(pawn.Drawer.renderer.CurAnimation==null)pawn.Drawer.renderer.SetAnimation(ExpansionDefOf.LP_ExpansionPose);}
            });
    }
    bool SafeArea()
    {
        var b=Facility;if(b==null||Slot<0||!b.ClearCell(b.EntryAt(Slot),pawn))return false;
        if(course!=null)return course.All(c=>b.ClearCell(c,pawn)&&c.GetThingList(pawn.Map).Any(t=>t is Building_PlaygroundFacility f&&f.IsCourse&&CompPlaygroundAccess.Allows(f,pawn)));
        if(b.Kind==ExpansionKind.CompactSlide&&!b.ClearCell(CompactMotion.SlideExit(b),pawn))return false;
        if(b.Kind==ExpansionKind.BallPit&&!b.ClearCell(CompactMotion.PitEntrance(b),pawn))return false;
        if(!MorePlay.StillClear(b,Slot,pawn))return false;
        if(CompactMotion.IsCompact(b.Kind)||b.Kind==ExpansionKind.Roundabout||b.Kind==ExpansionKind.Hopscotch||b.Kind==ExpansionKind.SplashPad||b.Kind==ExpansionKind.SharedTrampoline)
            return b.OccupiedRect().All(c=>b.ClearCell(c,pawn));
        return true;
    }
    void ClearVisit()
    {
        if(cleanupDone)return;cleanupDone=true;
        bool wasMounted=mounted;mounted=false;arrived=false;
        var b=Facility;if(b!=null&&Slot>=0&&b.PawnAt(Slot)==pawn)b.SetVisitor(Slot,null);
        if(wasMounted&&b?.Kind==ExpansionKind.CompactSlide&&pawn.Spawned&&b.Map==pawn.Map)
        {
            var exit=CompactMotion.SlideExit(b);
            if(b.ClearCell(exit,pawn)){pawn.Position=exit;pawn.Notify_Teleported(endCurrentJob:false);}
        }
        if(pawn.Drawer?.renderer.CurAnimation==ExpansionDefOf.LP_ExpansionPose)pawn.Drawer.renderer.SetAnimation(null);
        if(pawn.Spawned)
        {
            var visits=pawn.Map.GetComponent<PlaygroundMap>();
            if(b?.Kind==ExpansionKind.Drink)visits.Finished(pawn,true);
            else if(wasMounted)visits.Finished(pawn);else visits.Failed(pawn);
        }
    }
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOn(()=>pawn.Drafted||pawn.Downed||PlaygroundToddlers.StopsPlay(pawn)||!PlaygroundUse.CanParticipate(pawn)||!SafeArea());
        this.FailOn(()=>PlaygroundCare.ShouldStop(pawn,job));
        this.FailOn(()=>PlaygroundCompanion.ShouldStopBeforeMount(this,CompanionCallerJob,start>=0));
        AddFinishAction(_=>ClearVisit());if(Facility==null||Slot<0)yield break;
        yield return Toils_Goto.GotoCell(Facility.EntryAt(Slot),PathEndMode.OnCell);
        var play=ToilMaker.MakeToil("PlaygroundFacilityVisit");
        play.initAction=()=>{pawn.pather.StopDead();pawn.Notify_Teleported(endCurrentJob:false);footLifts=PlaygroundContact.MeasureFeet(pawn);arrived=true;};
        play.tickIntervalAction=delta=>
        {
            var b=Facility;
            if(b.PawnAt(Slot)!=pawn){EndJobWith(JobCondition.Incompletable);return;}
            if(PawnUtility.WillSoonHaveBasicNeed(pawn,-.05f)||!PlaygroundCare.IsCare(job.def)&&!(pawn.timetable?.CurrentAssignment??TimeAssignmentDefOf.Anything).allowJoy
                ||(b.Kind==ExpansionKind.SplashPad&&b.Position.GetTemperature(b.Map)<10))
            {EndJobWith(JobCondition.InterruptOptional);return;}
            if(!mounted)
            {
                holding=PlaygroundCompanion.HoldAtEntrance(this,b,ref holdSince);
                if(holding)return;
                // A catcher comes for the bubbles. Getting there after they have stopped, it has not played: it finds something else, with only a failed visit's short wait.
                if(b.Kind==ExpansionKind.BubbleTub&&Slot==1&&BubbleMotion.NothingToCatch(b)){EndJobWith(JobCondition.Incompletable);return;}
                if(b.Kind==ExpansionKind.BallPit&&CompactMotion.PitDoorBusy(this))return;
                if(b.Kind==ExpansionKind.Roundabout&&b.SpinSpeed>.01f)return;
                start=Find.TickManager.TicksGame;mounted=true;
                if(b.IsCourse)
                {
                    courseStumbleAt=CourseMotion.Plan(MotionSeed,pawn.DevelopmentalStage.Child(),course?.Count??0);
                    courseStumbleSide=CourseMotion.Side(MotionSeed);
                }
            }
            // Keep the departure phase visible to the building while it brakes.
            // A late arrival may still be in the doorway. Wait inside our lane;
            // keep the saved clock at the last playing tick until it clears.
            if(b.Kind==ExpansionKind.BallPit&&Age>=Duration-CompactMotion.PitTransit&&CompactMotion.PitDoorBusy(this))
                start=Find.TickManager.TicksGame-(Duration-CompactMotion.PitTransit-1);
            // Subtracting delta could move Age back below the threshold each tick,
            // making the building accelerate again and preventing dismount forever.
            if(b.Kind==ExpansionKind.Roundabout&&Age>=Duration-90&&b.SpinSpeed>.01f)
                start=Find.TickManager.TicksGame-(Duration-90);
            // With nobody left blowing, the catcher goes back to its place and is done. Its path runs on the game clock, and one still coming up turns back from where it has got to, so this does not jump.
            if(b.Kind==ExpansionKind.BubbleTub&&Slot==1&&Age<Duration-60&&b.VisitAt(0)==null)start=Find.TickManager.TicksGame-(Duration-Math.Min(Age,60));
            if(pawn.Drawer.renderer.CurAnimation==null)pawn.Drawer.renderer.SetAnimation(ExpansionDefOf.LP_ExpansionPose);
            pawn.Rotation=PlayFacing;
            if(Age>=60&&Age<Duration-60)
            {
                // The drinking fountain is a short rest/joy break, not a new thirst need or learning activity.
                if(b.Kind==ExpansionKind.Drink)
                {if(pawn.needs.rest!=null&&pawn.needs.rest.CurLevel<.8f)pawn.needs.rest.CurLevel+=.00008f*delta;
                 if(!pawn.DevelopmentalStage.Child()&&PlaygroundUse.Reward(pawn,delta,b))return;}
                else if(PlaygroundUse.Reward(pawn,delta,b))return;
            }
            if(Age>=Duration)ReadyForNextToil();
        };
        play.handlingFacing=true;play.socialMode=Facility.Kind==ExpansionKind.Playhouse?RandomSocialMode.SuperActive:RandomSocialMode.Off;
        play.defaultCompleteMode=ToilCompleteMode.Never;
        play.AddFinishAction(ClearVisit);yield return play;
        yield return PlaygroundReaction.AfterRide(pawn,()=>Facility?.Kind!=ExpansionKind.Drink);
    }
}

public sealed class ExpansionAnimationWorker:AnimationWorker_Keyframes
{
    public override bool Enabled(AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)
        =>parms.pawn?.jobs?.curDriver is JobDriver_VisitPlayground d&&d.Age>=0&&!parms.pawn.Downed;
    public override float AngleAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)
        =>parms.pawn.jobs.curDriver is JobDriver_VisitPlayground d?d.PoseAngle:0;
    public override Vector3 OffsetAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)
        =>parms.pawn.jobs.curDriver is JobDriver_VisitPlayground d?new Vector3(0,d.PoseDepth,d.PoseHeight):Vector3.zero;
    public override Vector3 ScaleAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)
        =>parms.pawn.jobs.curDriver is JobDriver_VisitPlayground d?d.PoseScale:Vector3.one;
    public override GraphicStateDef GraphicStateAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>null;
}
