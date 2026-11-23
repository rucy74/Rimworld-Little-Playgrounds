using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LittlePlaygrounds;

public static class PlaygroundLearning
{
    public const string DesireName="LP_PlaygroundLearning";
    public const float DefaultWeight=2f,MinWeight=.5f,MaxWeight=4f;
    public static LearningDesireDef Desire=>DefDatabase<LearningDesireDef>.GetNamedSilentFail(DesireName);
    public static float NormalizeWeight(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value)
        ?Mathf.Round(Mathf.Clamp(value,MinWeight,MaxWeight)*2f)/2f:DefaultWeight;
    // Change only the definition's next-draw weight. Never replace a pawn's active desires or current job.
    public static void ApplyWeight()
    {
        var settings=LittlePlaygroundsMod.Settings;
        settings.playgroundLearningWeight=NormalizeWeight(settings.playgroundLearningWeight);
        if(Desire is { } desire)desire.selectionWeight=settings.playgroundLearningWeight;
    }
}

// Settings may deserialize before defs exist; keep the post-def application in a separate startup type.
[StaticConstructorOnStartup]
public static class PlaygroundLearningStartup
{
    static PlaygroundLearningStartup()=>PlaygroundLearning.ApplyWeight();
}

// The native JobGiver_Learn owns desire selection and scheduling. CanDo has no job/companion/record side effects.
public sealed class LearningGiver_Playground:LearningGiver
{
    public override bool CanDo(Pawn pawn)=>base.CanDo(pawn)&&PlayChoice.CanChoose(pawn,false);
    public override Job TryGiveJob(Pawn pawn)=>CanDo(pawn)?PlayChoice.Choose(pawn,false):null;
}

// A drink offered after play is a follow-up, not a new learning activity or another recreation roll.
public sealed class JobGiver_PlaygroundRefresh:ThinkNode_JobGiver
{
    protected override Job TryGiveJob(Pawn pawn)=>PlayChoice.Refresh(pawn,PlaySource.Learning);
}

// Reuse the activities already registered for vanilla recreation. Children have learning, not a joy/tolerance need;
// they use those activities' ordinary base chances, without an invented boredom or recent-use score.
public static class PlaygroundActivities
{
    public static IEnumerable<JoyGiverDef> ChildActivities=>DefDatabase<JoyGiverDef>.AllDefsListForReading
        .Where(d=>d.giverClass==typeof(JoyGiver_PlayOnPlayground)&&!d.thingDefs.NullOrEmpty()
            &&!d.thingDefs.Contains(ExpansionDefOf.LP_DrinkingFountain));

    // Native closest search; each ride validates its real entry/exit, rather than reaching the building's centre.
    public static Thing Closest(Pawn pawn,IEnumerable<ThingDef> definitions)=>Closest(pawn,definitions,null);
    public static Thing Closest(Pawn pawn,IEnumerable<ThingDef> definitions,System.Predicate<Thing> extra)=>GenClosest.ClosestThing_Global(
        pawn.Position,definitions.SelectMany(d=>pawn.Map.listerThings.ThingsOfDef(d)),9999f,
        t=>PlaygroundSocialAdapter.CanUse(t,pawn)&&PlaygroundSocial.HasTurn(pawn,t)
            &&PlaygroundSocialAdapter.ApproachClear(t,pawn)&&(extra==null||extra(t)));
}
