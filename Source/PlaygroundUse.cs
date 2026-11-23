using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LittlePlaygrounds;

// Shared eligibility and rewards for our own playground activities only.
public static class PlaygroundUse
{
    // Translate the intact adult render tree to meet the seat. Never scale a pawn
    // or change the bench pose; native body-height metadata accounts for tall bodies.
    public static float AdultSeatLift(Pawn p)=>p.DevelopmentalStage==DevelopmentalStage.Adult
        ?.05f+.5f*Mathf.Max(0,(p.story?.bodyType?.bodyGraphicScale.y??1f)-.9f):0;

    public static bool CanParticipate(Pawn p)=>PlaygroundToddlers.Walking(p)||p?.RaceProps.Humanlike==true
        &&(p.DevelopmentalStage.Child()?p.needs?.learning!=null:
            p.DevelopmentalStage==DevelopmentalStage.Adult&&p.needs?.joy!=null);

    public static bool Reward(Pawn p,int delta,Building source)
    {
        if(PlaygroundToddlers.Walking(p)){PlaygroundToddlers.Play(p,delta);return false;}
        if(source is not Building_PlaygroundFacility f||f.Kind!=ExpansionKind.Drink)PlaygroundFaces.Tick(p);
        if(p.DevelopmentalStage.Child())
        {
            p.needs.learning.Learn(LearningUtility.NeedSatisfiedPerTick*LearningUtility.LearningRateFactor(p)*delta);
            return false;
        }
        PlaygroundCouples.Tick(p,source);
        // Childcare play (own JobDefs, no joyKind): Social joy directly, like vanilla baby play; never ends on joy.
        if(PlaygroundCare.IsCare(p.CurJobDef)){PlaygroundCare.GainSocialJoy(p,delta,source,1f,true);return false;}
        // Finish the short ride/dismount even if joy fills midway through it.
        return JoyUtility.JoyTickCheckEnd(p,delta,JoyTickFullJoyAction.None,joySource:source);
    }
}

// Registered with vanilla recreation, not the children's learning think tree.
public sealed class JoyGiver_PlayOnPlayground:JoyGiver
{
    public override bool CanBeGivenTo(Pawn p)=>p.DevelopmentalStage==DevelopmentalStage.Adult
        &&PlaygroundUse.CanParticipate(p)&&base.CanBeGivenTo(p);

    public override Job TryGiveJob(Pawn p)
    {
        bool refresh=def.thingDefs.Contains(ExpansionDefOf.LP_DrinkingFountain)&&p.Spawned&&p.Map.GetComponent<PlaygroundMap>().WantsDrink(p);
        if(!p.Spawned||p.Drafted||p.Downed||p.InMentalState||!CanBeGivenTo(p)
            ||p.needs.joy.CurLevel>=.9f||PawnUtility.WillSoonHaveBasicNeed(p,.1f)
            ||!(p.timetable?.CurrentAssignment??TimeAssignmentDefOf.Anything).allowJoy
            ||(!refresh&&!p.Map.GetComponent<PlaygroundMap>().Ready(p)))return null;
        var together=PlaygroundSocial.TryJoin(p,def.thingDefs);
        if(together!=null){PlaygroundRecords.Tag(together,PlaySource.Invitation,"LP_Rec_Joined".Translate(together.targetA.Thing.LabelShort));return together;}
        var best=PlaygroundActivities.Closest(p,def.thingDefs);
        if(best==null)return PlaygroundSocial.TryWait(p,def.thingDefs);
        var job=JobMaker.MakeJob(def.jobDef,best);
        if(!PlaygroundCompanion.CallLover(p,best,job)&&best is Building_Seesaw seesaw&&!seesaw.HasWaitingPartner(p))PlaygroundCompanion.CallAdultSeesaw(p,seesaw,job);
        PlaygroundRecords.Tag(job,PlaySource.Recreation);return job;
    }
}
