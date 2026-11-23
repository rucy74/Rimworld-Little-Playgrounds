using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace LittlePlaygrounds;

public static class PlaygroundResearch
{
    public const string ProjectName="ComplexFurniture";
    public static ResearchProjectDef Project=>DefDatabase<ResearchProjectDef>.GetNamedSilentFail(ProjectName);
    public static string ProjectLabel=>Project?.label??ProjectName;
    // The research window lists what a project unlocks from caches filled the first time it is shown.
    static readonly FieldInfo unlocked=typeof(ResearchProjectDef).GetField("cachedUnlockedDefs",BindingFlags.Instance|BindingFlags.NonPublic),
        links=typeof(ResearchProjectDef).GetField("cachedHyperlinks",BindingFlags.Instance|BindingFlags.NonPublic);
    public static IEnumerable<BuildableDef> Gated=>DefDatabase<ThingDef>.AllDefs.Concat<BuildableDef>(DefDatabase<TerrainDef>.AllDefs)
        .Where(d=>d.designationCategory!=null&&d.modContentPack!=null&&d.modContentPack==LittlePlaygroundsMod.Pack);
    public static void Apply()
    {
        var project=Project;if(project==null)return;
        bool on=LittlePlaygroundsMod.Settings.requireResearch;
        foreach(var def in Gated)
        {
            var list=def.researchPrerequisites;
            bool has=list!=null&&list.Contains(project);
            if(on&&!has)(def.researchPrerequisites??=new()).Add(project);
            else if(!on&&has){list.Remove(project);if(list.Count==0)def.researchPrerequisites=null;}
        }
        unlocked?.SetValue(project,null);links?.SetValue(project,null);
    }
}

// Settings load before defs exist; apply once they do.
[StaticConstructorOnStartup]
public static class PlaygroundResearchStartup
{
    static PlaygroundResearchStartup()=>PlaygroundResearch.Apply();
}
