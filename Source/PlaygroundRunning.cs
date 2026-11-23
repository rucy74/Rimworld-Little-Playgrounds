using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LittlePlaygrounds;

[DefOf]
public static class FreePlayDefOf
{
    public static JobDef LP_RunAround;
    static FreePlayDefOf()=>DefOfHelper.EnsureInitializedInCtor(typeof(FreePlayDefOf));
}

public static class RunGround
{
    public const float Radius=8f,PartnerReach=4f;
    // Room for a run rather than shuffling on the spot: about a 5x5 patch joined to the ride.
    public const int LeastOpenCells=24;

    public static bool ClearCell(Pawn p,Thing anchor,IntVec3 c)=>c.InBounds(p.Map)&&c.InHorDistOf(anchor.Position,Radius)
        &&c.Standable(p.Map)&&Firm(c.GetTerrain(p.Map))&&(LittlePlaygroundsMod.Settings.runAroundIndoors||UnderSky(c,p.Map))&&!c.Fogged(p.Map)&&!c.IsForbidden(p)
        &&c.GetDangerFor(p,p.Map)==Danger.None&&c.GetEdifice(p.Map)==null
        &&!c.GetThingList(p.Map).Exists(t=>t is Building||t is Fire);

    // Not water, marsh, mud or lava: what the game's own wander avoids (RCellFinder), though a pawn can stand there.
    public static bool Firm(TerrainDef terrain)=>!terrain.avoidWander&&!terrain.dangerous;
    // The playground's own canopy is shade, not indoors.
    static bool UnderSky(IntVec3 c,Map map){var roof=map.roofGrid.RoofAt(c);return roof==null||roof==GardenDefOf.LP_FabricShade;}

    // The largest patch of clear cells touching the ride. A flood, not a count inside the circle: ground behind a wall
    // is inside the circle but cannot be run on.
    public static List<IntVec3> OpenGround(Pawn p,Thing anchor)
    {
        var best=new List<IntVec3>();var seen=new HashSet<IntVec3>();
        foreach(var start in anchor.OccupiedRect().ExpandedBy(1).EdgeCells)
        {
            if(!seen.Add(start)||!ClearCell(p,anchor,start))continue;
            var patch=new List<IntVec3>{start};
            for(int i=0;i<patch.Count;i++)foreach(var step in GenAdj.CardinalDirections)
            {
                var c=patch[i]+step;
                if(seen.Add(c)&&ClearCell(p,anchor,c))patch.Add(c);
            }
            if(patch.Count>best.Count)best=patch;
        }
        return best;
    }

    // The next sprint. With a friend running too, a cell near that friend first, so the two stay together.
    public static bool TryDestination(Pawn p,Thing anchor,out IntVec3 cell,Pawn partner=null)
    {
        if(partner!=null&&TrySprint(p,anchor,c=>c.InHorDistOf(partner.Position,PartnerReach),out cell))return true;
        return TrySprint(p,anchor,null,out cell);
    }
    static bool TrySprint(Pawn p,Thing anchor,System.Predicate<IntVec3> wanted,out IntVec3 cell)
    {
        cell=IntVec3.Invalid;
        int count=GenRadial.NumCellsInRadius(Radius),first=Rand.Range(0,count);
        // Visit the bounded local candidate set once, in a rotated order. Random
        // retries could miss a narrow clear corridor even when one was available.
        for(int i=0;i<count;i++)
        {
            var c=anchor.Position+GenRadial.RadialPattern[(first+i)%count];
            if(p.Position.InHorDistOf(c,3)||!ClearCell(p,anchor,c)||c.GetFirstPawn(p.Map)!=null||wanted?.Invoke(c)==false)continue;
            // A clear, safe straight corridor keeps the sprint out of equipment,
            // doors and forbidden areas; native pather still owns every step.
            if(!GenSight.LineOfSight(p.Position,c,p.Map,true,x=>x==p.Position||ClearCell(p,anchor,x))
                ||!p.CanReach(c,PathEndMode.OnCell,Danger.None))continue;
            cell=c;return true;
        }
        return false;
    }
}

public static class PlaygroundRunning
{
    public const float DefaultChance=.1f,LeastChance=.05f,MostChance=.5f;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool RunsFirst()=>Rand.Value<LittlePlaygroundsMod.Settings.runAroundChance;

    // needsMargin: as on the rides, a little stricter to start than to keep going.
    public static bool CanRun(Pawn p,Thing anchor,float needsMargin=.1f)=>p?.Spawned==true&&p.DevelopmentalStage.Child()
        &&p.needs?.learning!=null&&!p.Drafted&&!p.Downed&&!p.InMentalState
        &&!PawnUtility.WillSoonHaveBasicNeed(p,needsMargin)
        &&(p.timetable?.CurrentAssignment??TimeAssignmentDefOf.Anything).allowJoy
        &&anchor?.Spawned==true&&anchor.Map==p.Map&&!anchor.IsForbidden(p)&&CompPlaygroundAccess.Allows(anchor,p);

    // Called once a ride has been drawn. Null: ride it as usual (no dice, the option is off, or no ground to run on).
    public static Job TryInstead(Pawn child,Thing ride,List<string> why=null)
    {
        if(!LittlePlaygroundsMod.Settings.runAround||!RunsFirst())return null;
        var job=TryCreate(child,ride);
        if(job!=null)PlaygroundCompanion.CallForRun(child,ride,job,why);
        return job;
    }
    public static Job TryCreate(Pawn p,Thing anchor)
    {
        if(!CanRun(p,anchor))return null;
        var ground=RunGround.OpenGround(p,anchor);
        if(ground.Count<RunGround.LeastOpenCells)return null;
        // Walk to the near edge of the ground; the first sprint starts there.
        var entry=ground.OrderBy(c=>(c-p.Position).LengthHorizontalSquared).First();
        if(!p.CanReach(entry,PathEndMode.OnCell,Danger.None))return null;
        var job=JobMaker.MakeJob(FreePlayDefOf.LP_RunAround,anchor,entry);
        job.isLearningDesire=true;return job;
    }

    // The ride this child is running around, walk there included.
    public static Thing RunSite(Pawn child)=>child?.jobs?.curDriver is JobDriver_RunAround?child.CurJob.targetA.Thing:null;
    // A friend still at this run with this child, not one who has gone off to something else.
    public static bool HasFriend(Pawn child)=>child?.jobs?.curDriver is JobDriver_RunAround {Partner:{} friend}
        &&friend.Spawned&&friend.jobs?.curDriver is JobDriver_RunAround {Partner:{} back}&&back==child;

    public static void DrawSettings(Listing_Standard list)
    {
        var settings=LittlePlaygroundsMod.Settings;
        list.CheckboxLabeled("LP_Setting_RunAround".Translate(),ref settings.runAround,"LP_Setting_RunAroundTip".Translate());
        if(!settings.runAround)return;
        // Indent moves only the left edge, so the column narrows with it or the rows run past the page.
        list.Indent(24f);list.ColumnWidth-=24f;
        list.Label("LP_Setting_RunAroundChance".Translate(settings.runAroundChance.ToStringPercent()));
        settings.runAroundChance=Mathf.Round(list.Slider(settings.runAroundChance,LeastChance,MostChance)*20f)/20f;
        list.CheckboxLabeled("LP_Setting_RunAroundIndoors".Translate(),ref settings.runAroundIndoors,"LP_Setting_RunAroundIndoorsTip".Translate());
        list.ColumnWidth+=24f;list.Outdent(24f);
    }
}

public sealed class JobDriver_RunAround:JobDriver
{
    // As long as a swing or seesaw ride, so a run teaches what those rides teach.
    public const int Duration=1080;
    int start=-1,legs,holdSince=-1,callerJob=-1;
    Pawn partner;
    public int Legs=>legs;
    public bool Started=>start>=0;
    public Pawn Partner=>partner;
    public int Age=>start<0?0:Find.TickManager.TicksGame-start;
    Thing Anchor=>job.targetA.Thing;
    Pawn RunningPartner=>partner?.Spawned==true&&partner.jobs?.curDriver is JobDriver_RunAround {Started:true}?partner:null;
    public override bool TryMakePreToilReservations(bool errorOnFailed)=>PlaygroundRunning.CanRun(pawn,Anchor);
    public override void ExposeData()
    {
        base.ExposeData();Scribe_Values.Look(ref start,"lpRunStart",-1);Scribe_Values.Look(ref legs,"lpRunLegs",0);
        Scribe_Values.Look(ref holdSince,"lpRunHoldStart",-1);Scribe_Values.Look(ref callerJob,"lpCompanionCallerJob",-1);
        Scribe_References.Look(ref partner,"lpRunPartner");
    }
    // Two children running together (PlaygroundCompanion.Start). The follower keeps the caller's job identity on its saved driver.
    internal static void Pair(Pawn caller,Pawn follower,int callerJobId)
    {
        if(follower.jobs.curDriver is JobDriver_RunAround f){f.partner=caller;f.callerJob=callerJobId;}
        if(caller.jobs.curDriver is JobDriver_RunAround c)c.partner=follower;
    }
    // A follower gives up before it starts if its caller has left, like a friend walking to a ride.
    bool CallerLeft=>start<0&&callerJob>=0&&partner?.CurJob?.loadID!=callerJob;
    // The caller waits at the edge of the ground for its friend, at most as long as a shared ride holds its riders.
    bool WaitingForFriend()
    {
        if(callerJob>=0)return false;
        int now=Find.TickManager.TicksGame;if(holdSince<0)holdSince=now;
        if(now-holdSince>=PlaygroundCompanion.HoldTicks)return false;
        if(partner==null)return pawn.Map.GetComponent<PlaygroundSocialMap>().CallPending(pawn,Anchor);
        return partner.Spawned&&partner.jobs.curDriver is JobDriver_RunAround {Started:false}&&partner.pather.Moving;
    }
    void Learn(int delta)
    {
        if(Age>=Duration){EndJobWith(JobCondition.Succeeded);return;}
        PlaygroundUse.Reward(pawn,delta,Anchor as Building);
    }
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOn(()=>!LittlePlaygroundsMod.Settings.runAround||!PlaygroundRunning.CanRun(pawn,Anchor,-.05f)||CallerLeft);
        // Same outcome as a ride: having played starts the wait for the next visit, not having played only a short retry.
        AddFinishAction(_=>
        {
            if(!pawn.Spawned)return;
            var visits=pawn.Map.GetComponent<PlaygroundMap>();
            if(legs>0)visits.Finished(pawn);else visits.Failed(pawn);
        });
        yield return Toils_Goto.GotoCell(TargetIndex.B,PathEndMode.OnCell);
        var hold=ToilMaker.MakeToil("WaitForRunningFriend");
        hold.initAction=()=>{pawn.pather.StopDead();if(!WaitingForFriend())ReadyForNextToil();};
        hold.tickIntervalAction=_=>{if(WaitingForFriend())pawn.rotationTracker.FaceCell(Anchor.Position);else ReadyForNextToil();};
        hold.defaultCompleteMode=ToilCompleteMode.Never;yield return hold;
        var pick=ToilMaker.MakeToil("PickPlaygroundSprint");
        pick.initAction=()=>
        {
            if(start<0)start=Find.TickManager.TicksGame;
            if(Age>=Duration){EndJobWith(JobCondition.Succeeded);return;}
            if(!RunGround.TryDestination(pawn,Anchor,out var c,RunningPartner))
            {
                if(legs==0){EndJobWith(JobCondition.Incompletable);return;}
                c=pawn.Position;   // Hemmed in for now: catch breath on the spot and look again.
            }
            job.SetTarget(TargetIndex.B,c);job.locomotionUrgency=LocomotionUrgency.Sprint;legs++;
        };
        pick.defaultCompleteMode=ToilCompleteMode.Instant;yield return pick;
        var run=Toils_Goto.GotoCell(TargetIndex.B,PathEndMode.OnCell);
        run.tickIntervalAction=delta=>
        {
            if(!RunGround.ClearCell(pawn,Anchor,job.targetB.Cell)||pawn.Position.GetDangerFor(pawn,pawn.Map)!=Danger.None)
            {EndJobWith(JobCondition.Incompletable);return;}
            Learn(delta);
        };
        yield return run;
        var rest=ToilMaker.MakeToil("CatchBreathInPlayground");
        rest.initAction=()=>pawn.pather.StopDead();rest.tickIntervalAction=Learn;
        rest.defaultCompleteMode=ToilCompleteMode.Delay;rest.defaultDuration=45+pawn.thingIDNumber%37;
        rest.socialMode=RandomSocialMode.SuperActive;yield return rest;
        yield return Toils_Jump.Jump(pick);
    }
}
