using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace LittlePlaygrounds;

[DefOf]
public static class CoupleDefOf
{
    public static ThoughtDef LP_PlaygroundDate,LP_PlaygroundDateOpinion;
    static CoupleDefOf()=>DefOfHelper.EnsureInitializedInCtor(typeof(CoupleDefOf));
}

public static class PlaygroundCouples
{
    const int DateTicks=600,DaysBetweenDates=GenDate.TicksPerDay;
    const float IdleDateChance=.25f,DateReach=65f;   // As far as Romance On The Rim lets a couple's interaction reach.
    static bool On=>LittlePlaygroundsMod.Settings.couplePlay;

    // Whom this adult calls: the love partner it likes most among those on this map.
    public static Pawn Lover(Pawn p)
    {
        if(!On||p?.DevelopmentalStage!=DevelopmentalStage.Adult||p.relations==null)return null;
        Pawn best=null;int bestOpinion=int.MinValue;
        var relations=p.relations.DirectRelations;
        for(int i=0;i<relations.Count;i++)
        {
            var other=relations[i].otherPawn;
            if(!LovePartnerRelationUtility.IsLovePartnerRelation(relations[i].def)||other?.Spawned!=true||other.Map!=p.Map)continue;
            int opinion=p.relations.OpinionOf(other);
            if(best==null||opinion>bestOpinion){best=other;bestOpinion=opinion;}
        }
        return best;
    }

    // Two can play on it now: a shared ride or a bench with two free seats whose entrances are open. With one blocked there
    // is a seat for one, and a partner called to it could not start.
    public static bool Shareable(Thing site)=>(PlaygroundSocialAdapter.Invitable(site)||site is Building_GardenBench)&&MorePlay.SecondSeatOpen(site)
        &&PlaygroundSocialAdapter.Entries(site).Take(PlaygroundSocialAdapter.Capacity(site)).Count(c=>c.InBounds(site.Map)&&c.Standable(site.Map))>=PlaygroundSocialAdapter.Occupants(site).Count()+2;

    // Love partners, whichever of them Lover would name. Always false with the option off.
    public static bool IsPartner(Pawn p,Pawn other)=>On&&p?.relations!=null&&other!=null&&other!=p&&LovePartnerRelationUtility.LovePartnerRelationExists(p,other);

    // A love partner on this seat or walking to it, for whom this adult waits at the entrance. Any partner, by seat: with
    // several it need not be the one Lover names, and working on the seat (repairing it) is no date.
    public static Pawn ComingLover(Pawn p,Thing site)=>PlaygroundSocialAdapter.Occupants(site).FirstOrDefault(o=>IsPartner(p,o));

    public static bool OnDate(Pawn p,Thing site)=>ComingLover(p,site)!=null;

    // Enjoying some other recreation counts as free for a date: it can wait. Work, meals and rides do not.
    public static bool AtLeisure(Pawn p)=>p.CurJob?.playerForced==false&&p.CurJobDef.joyKind!=null&&!p.CurJobDef.defName.StartsWith("LP_");

    static bool OrdinaryWork(Pawn p)
    {
        var giver=p.CurJob?.workGiverDef;
        return giver!=null&&p.CurJob.playerForced==false&&!giver.emergency&&p.carryTracker?.CarriedThing==null
            &&giver.workType!=WorkTypeDefOf.Doctor&&giver.workType!=WorkTypeDefOf.Firefighter&&giver.workType!=WorkTypeDefOf.Childcare;
    }
    // Between tasks: walking, or the last stretch of a timed step. A step with no timer reads zero or below (JobDriver sets
    // ticksLeftThisToil from defaultDuration), so 'at most 2000' alone would also take someone halfway through a bill.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool BetweenTasks(Pawn p)
    {
        int left=p.jobs.curDriver.ticksLeftThisToil;
        return p.pather.Moving||left>0&&left<=2000;
    }
    public static bool CanLeaveForDate(Pawn lover,Pawn caller)=>AtLeisure(lover)
        ||OrdinaryWork(lover)&&BetweenTasks(lover)&&!DatedLately(caller,lover);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool IdleDateRoll()=>Rand.Value<IdleDateChance;
    static bool DatedLately(Pawn p,Pawn lover)=>p.needs?.mood?.thoughts?.memories?.Memories
        .Exists(m=>m.def==CoupleDefOf.LP_PlaygroundDateOpinion&&m.otherPawn==lover&&m.age<DaysBetweenDates)!=false;
    // Called from the idle node, so this adult has nothing to do. Null: no date now. Needing recreation is no condition
    // here or for the lover (PlaygroundCompanion.Why): 64% of idle adults measured were above 90% of it already.
    public static Job TryDate(Pawn p)
    {
        var lover=Lover(p);
        if(lover==null||!PlaygroundCompanion.LoverFree(p,lover)||DatedLately(p,lover)||!PlaygroundSocial.Eligible(p,true,false)||!IdleDateRoll())return null;
        var benches=p.Map.listerThings.ThingsOfDef(GardenDefOf.LP_GardenBench);
        var sites=PlaygroundSocialAdapter.Facilities(p.Map).Where(PlaygroundSocialAdapter.Invitable).Concat(benches)
            .Where(t=>t.Faction==p.Faction&&t.Position.InHorDistOf(p.Position,DateReach)&&Shareable(t)&&PlaygroundCompanion.MayEnjoy(p,t)&&PlaygroundSocial.HasTurn(p,t)&&PlaygroundSocialAdapter.CanUse(t,p)&&PlaygroundSocialAdapter.ApproachClear(t,p)&&PlaygroundCompanion.LoverCanCome(p,lover,t)).ToList();
        if(!sites.TryRandomElement(out var site))return null;
        var job=PlaygroundSocialAdapter.JobFor(site,p);
        if(job==null||!PlaygroundCompanion.CallLover(p,site,job))return null;
        PlaygroundRecords.Tag(job,PlaySource.Recreation);return job;
    }

    // Once both have been at it a while, the date counts. Ride age and memory age are both saved, so a load neither loses
    // nor repeats it: a memory of this partner younger than this ride was given on this ride.
    public static void Tick(Pawn p,Thing site)
    {
        int age=PlaygroundCare.RideAge(p.jobs?.curDriver);
        var memories=p.needs?.mood?.thoughts?.memories;
        if(age<DateTicks||memories==null)return;
        foreach(var lover in PlaygroundSocialAdapter.Occupants(site))
        {
            if(!IsPartner(p,lover)||PlaygroundCare.RideAge(lover.jobs.curDriver)<0
                ||memories.Memories.Exists(m=>m.def==CoupleDefOf.LP_PlaygroundDateOpinion&&m.otherPawn==lover&&m.age<=age))continue;
            memories.TryGainMemory(CoupleDefOf.LP_PlaygroundDateOpinion,lover);
            memories.TryGainMemory(CoupleDefOf.LP_PlaygroundDate);
        }
    }
}
