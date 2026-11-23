using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LittlePlaygrounds;

[DefOf]
public static class PlaygroundReactionDefOf
{
    public static AnimationDef LP_AfterPlayHop;
    static PlaygroundReactionDefOf()=>DefOfHelper.EnsureInitializedInCtor(typeof(PlaygroundReactionDefOf));
}

// A final, interruptible step of our own rides. No global job/animation patch,
// queued job, extra reward, or reaction to an interrupted visit.
public static class PlaygroundReaction
{
    // The native job ID is unique per visit and survives save/load. Hashing it
    // gives each visit its own plan without consuming random values in rendering.
    static uint Pick(int seed,int salt)
    {unchecked{uint n=(uint)seed+0x9e3779b9u*(uint)(salt+1);n^=n>>16;n*=0x7feb352du;n^=n>>15;n*=0x846ca68bu;return n^(n>>16);}}
    public static int Seed(Pawn p)=>p.CurJob?.loadID??0;
    public static bool Chosen(int seed)=>Pick(seed,90)%100<70;
    public static int HopCount(int seed)=>2+(int)(Pick(seed,0)%4);
    public static int HopTicks(int seed,int hop)=>40+(int)(Pick(seed,10+hop)%17);
    public static int TotalTicks(int seed)
    {int n=0;for(int i=0;i<HopCount(seed);i++)n+=HopTicks(seed,i);return n;}
    public static bool Eligible(Pawn p)=>p?.Spawned==true&&p.DevelopmentalStage.Child()
        &&!p.Drafted&&!p.Downed&&!p.InMentalState&&p.mindState?.enemyTarget?.Spawned!=true
        &&(p.needs.mood?.CurLevel??1)>=.4f&&p.health.hediffSet.PainTotal<.25f
        &&!PawnUtility.WillSoonHaveBasicNeed(p,.1f)
        &&(p.timetable?.CurrentAssignment??TimeAssignmentDefOf.Anything).allowJoy
        &&p.Position.GetDangerFor(p,p.Map)==Danger.None;
    public static int Age(Pawn p)=>Mathf.Clamp(TotalTicks(Seed(p))-(p.jobs?.curDriver?.ticksLeftThisToil??TotalTicks(Seed(p))),0,TotalTicks(Seed(p)));
    static int HopAt(int seed,ref int age)
    {for(int i=0;i<HopCount(seed);i++){int d=HopTicks(seed,i);if(age<d)return i;age-=d;}return HopCount(seed);}
    public static float Height(int seed,int age)
    {
        int n=HopAt(seed,ref age);float flight=25+Pick(seed,30+n)%7,t=(age-7)/flight;
        return n>=HopCount(seed)||t<=0||t>=1?0:(.23f+(Pick(seed,50+n)%15)*.01f)*4*t*(1-t);
    }
    public static Rot4 Facing(int seed,int age)
    {
        int n=Math.Min(HopCount(seed)-1,HopAt(seed,ref age)),direction=(int)(Pick(seed,70)%4);
        for(int i=1;i<=n;i++)direction=(direction+1+(int)(Pick(seed,70+i)%3))%4;
        return new Rot4(direction);
    }
    public static Toil AfterRide(Pawn pawn,Func<bool> enjoyed=null)
    {
        var toil=ToilMaker.MakeToil("HappyAfterPlay");
        bool Allowed()=>Chosen(Seed(pawn))&&Eligible(pawn)&&(enjoyed==null||enjoyed())
            &&(pawn.Drawer.renderer.CurAnimation==null||pawn.Drawer.renderer.CurAnimation==PlaygroundReactionDefOf.LP_AfterPlayHop);
        void Pose()
        {
            if(!Allowed()){pawn.jobs.curDriver.ReadyForNextToil();return;}
            pawn.pather.StopDead();pawn.Rotation=Facing(Seed(pawn),Age(pawn));
            if(pawn.Drawer.renderer.CurAnimation==null)pawn.Drawer.renderer.SetAnimation(PlaygroundReactionDefOf.LP_AfterPlayHop);
        }
        toil.initAction=Pose;toil.tickIntervalAction=_=>Pose();
        toil.handlingFacing=true;toil.socialMode=RandomSocialMode.Off;
        toil.defaultCompleteMode=ToilCompleteMode.Delay;toil.defaultDuration=TotalTicks(Seed(pawn));
        toil.AddFinishAction(()=>{if(pawn.Drawer.renderer.CurAnimation==PlaygroundReactionDefOf.LP_AfterPlayHop)pawn.Drawer.renderer.SetAnimation(null);});
        return toil;
    }
}

public sealed class AfterPlayAnimationWorker:AnimationWorker_Keyframes
{
    public override bool Enabled(AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)
        =>parms.pawn?.Drawer.renderer.CurAnimation==PlaygroundReactionDefOf.LP_AfterPlayHop&&PlaygroundReaction.Eligible(parms.pawn);
    public override float AngleAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>0;
    public override Vector3 OffsetAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)
        =>new(0,0,PlaygroundReaction.Height(PlaygroundReaction.Seed(parms.pawn),PlaygroundReaction.Age(parms.pawn)));
    public override Vector3 ScaleAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>Vector3.one;
    public override GraphicStateDef GraphicStateAtTick(int tick,AnimationDef def,PawnRenderNode node,AnimationPart part,PawnDrawParms parms)=>null;
}
