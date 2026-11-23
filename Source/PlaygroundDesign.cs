using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

public enum PlaygroundDesign { Original=0, Timber=1 }
public sealed class CompProperties_PlaygroundDesign:CompProperties
{public CompProperties_PlaygroundDesign(){compClass=typeof(CompPlaygroundDesign);}}
public sealed class CompPlaygroundDesign:ThingComp
{
    PlaygroundDesign design;
    public PlaygroundDesign Design=>design;
    public bool Busy=>parent switch {
        Building_Swing b=>b.JobAt(0)!=null||b.JobAt(1)!=null,
        Building_Seesaw b=>b.JobAt(0)!=null||b.JobAt(1)!=null,
        Building_Slide b=>b.RiderJob!=null,
        Building_ActiveRide b=>b.RiderJob!=null,
        Building_PlaygroundFacility b=>Occupied(b),_=>false};
    static bool Occupied(Building_PlaygroundFacility b){for(int i=0;i<b.Capacity;i++)if(b.VisitAt(i)!=null)return true;return false;}
    public bool Select(PlaygroundDesign value)
    {
        if(Busy||value is not (PlaygroundDesign.Original or PlaygroundDesign.Timber))return false;
        design=value;parent.Notify_ColorChanged();return true;
    }
    public override void PostExposeData()
    {
        base.PostExposeData();Scribe_Values.Look(ref design,"lpDesign",PlaygroundDesign.Original);
        if(design is not (PlaygroundDesign.Original or PlaygroundDesign.Timber))design=PlaygroundDesign.Original;
    }
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if(parent.Faction!=Faction.OfPlayerSilentFail)yield break;
        var command=new Command_Action{
            defaultLabel="LP_DesignButton".Translate(),defaultDesc="LP_DesignDescription".Translate(("LP_Design"+design).Translate()),
            icon=ContentFinder<Texture2D>.Get(DesignArt.Path(parent)),
            action=()=>Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>{Option(PlaygroundDesign.Original),Option(PlaygroundDesign.Timber)}))};
        if(Busy)command.Disable("LP_DesignBusy".Translate());
        yield return command;
    }
    FloatMenuOption Option(PlaygroundDesign value)=>new(("LP_Design"+value).Translate()+(design==value?" ✓":""),()=>Select(value));
    public override string CompInspectStringExtra()=>"LP_DesignCurrent".Translate(("LP_Design"+design).Translate());
}
public static class DesignArt
{
    public static bool Timber(Thing b)=>b?.TryGetComp<CompPlaygroundDesign>()?.Design==PlaygroundDesign.Timber;
    public static string Path(Thing b)=>"Things/Building/Timber/"+(b switch {
        Building_Swing=>"swing",Building_Seesaw=>"seesaw",Building_Slide=>"slide",
        Building_PlaygroundFacility f when f.Kind==ExpansionKind.SpringRider=>"fox",
        Building_PlaygroundFacility f when f.Kind==ExpansionKind.Roundabout=>"rounddeck",_=>"trampoline"});
    public static Graphic Graphic(Thing b)=>GraphicDatabase.Get<Graphic_PlaygroundPalette>(Path(b)+"@"+(int)PaletteArt.Of(b),
        ShaderDatabase.Cutout,b.def.graphicData.drawSize,Color.white,Color.white,b.def.graphicData);
}
