using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LittlePlaygrounds;

public enum PlaygroundAudience { Everyone=0, Children=1, Adults=2 }
public sealed class CompProperties_PlaygroundAccess:CompProperties
{public CompProperties_PlaygroundAccess(){compClass=typeof(CompPlaygroundAccess);}}
public sealed class CompPlaygroundAccess:ThingComp
{
    PlaygroundAudience audience;
    public PlaygroundAudience Audience=>audience;
    public bool Relevant=>parent is Building_Swing or Building_Slide or Building_Seesaw or Building_ActiveRide or Building_GardenSeats
        ||parent is Building_PlaygroundFacility f&&f.Interactive;
    public static bool Allows(Thing t,Pawn p)=>t?.TryGetComp<CompPlaygroundAccess>()?.Allows(p)??true;
    public bool Allows(Pawn p)=>!Relevant||audience==PlaygroundAudience.Everyone||p!=null&&
        (audience==PlaygroundAudience.Children?PlaygroundToddlers.Walking(p)||p.DevelopmentalStage.Child():p.DevelopmentalStage==DevelopmentalStage.Adult);
    public void Select(PlaygroundAudience value)
    {
        audience=Valid(value)?value:PlaygroundAudience.Everyone;
        if(!parent.Spawned)return;
        // Only our own visits/queues; construction and unrelated jobs are untouched.
        foreach(var p in parent.Map.mapPawns.AllPawnsSpawned.ToList())
            if(!Allows(p)&&p.CurJobDef?.defName.StartsWith("LP_")==true&&p.CurJob.targetA.Thing==parent)
                p.jobs.EndCurrentJob(JobCondition.InterruptForced);
    }
    static bool Valid(PlaygroundAudience a)=>(int)a>=0&&(int)a<=2;
    public override void PostExposeData()
    {
        base.PostExposeData();Scribe_Values.Look(ref audience,"lpAudience",PlaygroundAudience.Everyone);
        if(!Valid(audience))audience=PlaygroundAudience.Everyone;
    }
    public static string Label(PlaygroundAudience a)=>("LP_Audience"+a).Translate();
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if(!Relevant||parent.Faction!=Faction.OfPlayerSilentFail)yield break;
        yield return new Command_Action{defaultLabel="LP_AudienceButton".Translate(Label(audience)),
            defaultDesc="LP_AudienceDescription".Translate(),icon=Icon,
            action=()=>Find.WindowStack.Add(new FloatMenu(new[]{PlaygroundAudience.Everyone,PlaygroundAudience.Children,PlaygroundAudience.Adults}
                .Select(a=>new FloatMenuOption(Label(a)+(a==audience?" ✓":""),()=>Select(a))).ToList()))};
    }
    public override string CompInspectStringExtra()=>Relevant?"LP_AudienceCurrent".Translate(Label(audience)):null;
    static Texture2D icon;
    static Texture2D Icon
    {
        get
        {
            if(icon!=null)return icon;
            icon=new Texture2D(32,32,TextureFormat.RGBA32,false){name="LP child and adult access"};
            var pixels=new Color[1024];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            {
                bool adult=(x-10)*(x-10)+(y-25)*(y-25)<=20||(x-10)*(x-10)/36f+(y-11)*(y-11)/64f<=1;
                bool child=(x-24)*(x-24)+(y-19)*(y-19)<=13||(x-24)*(x-24)/25f+(y-8)*(y-8)/36f<=1;
                pixels[y*32+x]=adult?new Color(.94f,.85f,.64f):child?new Color(.57f,.8f,.9f):Color.clear;
            }
            icon.SetPixels(pixels);icon.Apply();return icon;
        }
    }
}
