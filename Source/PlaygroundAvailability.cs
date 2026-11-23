using System;
using RimWorld;
using Verse;
using Verse.AI;

namespace LittlePlaygrounds;

public enum PlayBlocked { None, Unavailable, CannotPlay, Drafted, Downed, MentalState, Forbidden, Audience, TooHot, TooCold, Busy, Entrance, Returning, CourseTooShort, WaterCold, NoSky }

// Availability is shared by autonomous selection, the float menu and job reservations.
// Temperature depends on the destination, never the pawn's current region danger.
public static class PlaygroundAvailability
{
    public static PlayBlocked Common(Building b,Pawn p)
    {
        if(b?.Spawned!=true||b.Destroyed||p?.Spawned!=true||p.Map!=b.Map)return PlayBlocked.Unavailable;
        if(!PlaygroundUse.CanParticipate(p)||PlaygroundToddlers.Kept(b,p))return PlayBlocked.CannotPlay;
        if(p.Drafted)return PlayBlocked.Drafted;
        if(p.Downed)return PlayBlocked.Downed;
        if(p.InMentalState)return PlayBlocked.MentalState;
        if(b.IsForbidden(p))return PlayBlocked.Forbidden;
        if(!CompPlaygroundAccess.Allows(b,p))return PlayBlocked.Audience;
        var range=p.SafeTemperatureRange();
        foreach(var cell in b.OccupiedRect())
        {
            var reason=Temperature(p,cell,range);
            if(reason!=PlayBlocked.None)return reason;
        }
        return PlayBlocked.None;
    }
    static PlayBlocked Temperature(Pawn p,IntVec3 c,FloatRange range)
    {
        if(!c.InBounds(p.Map))return PlayBlocked.Entrance;
        float temperature=c.GetTemperature(p.Map);
        return temperature>range.max?PlayBlocked.TooHot:temperature<range.min?PlayBlocked.TooCold:PlayBlocked.None;
    }
    public static PlayBlocked Cell(Pawn p,IntVec3 c)
    {
        if(p?.Spawned!=true||!c.InBounds(p.Map)||!c.Standable(p.Map))return PlayBlocked.Entrance;
        if(c.IsForbidden(p))return PlayBlocked.Forbidden;
        return Temperature(p,c,p.SafeTemperatureRange());
    }
    public static PlayBlocked Entry(Pawn p,IntVec3 c)
    {
        var reason=Cell(p,c);if(reason!=PlayBlocked.None)return reason;
        return p.CanReach(c,PathEndMode.OnCell,Danger.None)?PlayBlocked.None:PlayBlocked.Entrance;
    }
    public static FloatMenuOption Menu(string label,PlayBlocked reason,Action action)
        =>reason==PlayBlocked.None?new FloatMenuOption(label,action):
            new FloatMenuOption(label+" — "+("LP_Blocked"+reason).Translate(),null);
}
