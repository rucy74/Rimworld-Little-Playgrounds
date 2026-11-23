using System;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;

namespace LittlePlaygrounds;

// The Toddlers mod (cyanobot.toddlers), which turns 1-3 year olds into walking babies with a play need and their own think tree.
// Everything here is off without that mod: the two lookups stay null, so every branch that asks is false before it reads a pawn.
// Both Toddlers builds are reached by name, so this assembly needs no reference to them, and their tree gets our node from Patches/Toddlers.xml.
public static class PlaygroundToddlers
{
    const float PlayPerTick=.00012f; // Toddlers' own ToddlerPlayUtility.PlayNeedSatisfiedPerTick
    const int Reach=30;              // Toddlers walk at less than half speed: only what is close enough to toddle to.
    static readonly Func<Pawn,bool> isToddler=Lookup<Func<Pawn,bool>>("Toddlers.ToddlerUtility","IsToddler");
    static readonly Func<Pawn,float> maxPlay=Lookup<Func<Pawn,float>>("Toddlers.ToddlerPlayUtility","GetMaxPlay");
    static T Lookup<T>(string type,string method) where T:class
    {
        var found=GenTypes.GetTypeInAnyAssembly(type)?.GetMethod(method,BindingFlags.Public|BindingFlags.Static);
        return found==null?null:Delegate.CreateDelegate(typeof(T),found,false) as T;
    }
    public static bool Active=>isToddler!=null;
    public static bool On=>isToddler!=null&&LittlePlaygroundsMod.Settings.toddlerPlay;
    public static readonly string[] RideNames={"LP_Swing","LP_Sandbox","LP_SplashPad","LP_BallPit","LP_SpringFox","LP_FloorRocker","LP_BubbleTub"};

    // A toddler on its feet. Crawlers stay out: "LearningToWalk" is under .5 while crawling, then wobbly steps, and is gone once they can walk.
    public static bool Walking(Pawn p)=>On&&p?.DevelopmentalStage==DevelopmentalStage.Baby&&p.RaceProps.Humanlike
        &&isToddler(p)&&p.needs?.play!=null&&!Crawling(p);
    static bool Crawling(Pawn p)
    {
        var all=p.health?.hediffSet?.hediffs;
        if(all!=null)for(int i=0;i<all.Count;i++)if(all[i].def.defName=="LearningToWalk")return all[i].Severity<.5f;
        return false;
    }

    // Only low, gentle things a two-year-old can sit in or toddle around. Anything not named here stays closed to them, so a new ride starts closed.
    public static bool Fits(Thing t)=>t is Building_Swing or Building_Sandbox
        ||t is Building_PlaygroundFacility {Kind:ExpansionKind.SplashPad or ExpansionKind.BallPit or ExpansionKind.SpringRider or ExpansionKind.FloorRocker or ExpansionKind.BubbleTub};
    public static bool Kept(Thing t,Pawn p)=>Walking(p)&&!Fits(t);

    public static void Play(Pawn p,int delta)
    {
        // Same cap and room bonus as Toddlers' own games. Unlike them it does not end the job when full: a ride finishes its short dismount.
        var need=p.needs.play;
        if(need.CurLevel<=MaxPlay(p))need.Play(delta*PlayPerTick*BabyPlayUtility.GetRoomPlayGainFactors(p));
    }
    static float MaxPlay(Pawn p)=>maxPlay?.Invoke(p)??1f;
    // Babies giggle at nothing, a vanilla mental state that leaves their job alone. On a ride that is the point, so only other states end the visit.
    public static bool StopsPlay(Pawn p)=>p.InMentalState&&!(Walking(p)&&p.MentalStateDef.defName=="Giggling");

    // The swing's fixed lift fits a child's body; a toddler is smaller and would hang above the seat. Its own measured feet plus the gap a child has over its .28.
    public static float SwingLift(Pawn p)=>PlaygroundContact.MeasureFeet(p)[Rot4.South.AsInt]+.08f;

    // Toddlers puts its wobbling walk back on a learner once a second, over whatever else is playing, and a pawn shows one animation at a time.
    // Health ticks after jobs, so a ride's own check comes too late; the map tick runs after every pawn, so nothing is drawn without the ride's pose.
    public static void KeepPoses(Map map)
    {
        if(!On)return;
        var babies=map.mapPawns.SpawnedBabiesInFaction(Faction.OfPlayer);
        for(int i=0;i<babies.Count;i++)
        {
            var p=babies[i];
            var pose=p.jobs?.curDriver switch
            {
                JobDriver_RideSwing d when d.Age>=0=>LPDefOf.LP_SwingPose,
                JobDriver_VisitPlayground d when d.Age>=0=>ExpansionDefOf.LP_ExpansionPose,
                JobDriver_GardenVisit d when d.Age>=0=>GardenDefOf.LP_GardenPose,
                _=>null
            };
            if(pose!=null&&p.Drawer.renderer.CurAnimation?.defName=="ToddlerWobble"&&Walking(p))p.Drawer.renderer.SetAnimation(pose);
        }
    }

    // Beside Toddlers' own play in its priority sorter, with the same two levels: an equal priority shares the turn at random.
    public static float Priority(Pawn p)=>!Walking(p)?0f:p.needs.play.CurLevel<.7f*MaxPlay(p)?6f:2f;
    public static Job TryPlay(Pawn p)
    {
        if(!Walking(p)||p.Map==null||p.Downed||p.Drafted||p.InMentalState||PawnUtility.WillSoonHaveBasicNeed(p,0f)
            ||p.needs.play.CurLevel>=Math.Min(.9f,MaxPlay(p))||!p.Map.GetComponent<PlaygroundMap>().Ready(p))return null;
        var near=PlaygroundSocialAdapter.Facilities(p.Map).Where(t=>Fits(t)&&(t.Position-p.Position).LengthHorizontalSquared<=Reach*Reach
            &&PlaygroundSocialAdapter.CanUse(t,p)&&PlaygroundSocial.HasTurn(p,t)&&PlaygroundSocialAdapter.ApproachClear(t,p)).ToList();
        return near.Count==0?null:PlaygroundSocialAdapter.JobFor(near.RandomElement(),p);
    }
}

public sealed class JobGiver_ToddlerPlayground:ThinkNode_JobGiver
{
    public override float GetPriority(Pawn p)=>PlaygroundToddlers.Priority(p);
    protected override Job TryGiveJob(Pawn p)=>PlaygroundToddlers.TryPlay(p);
}
