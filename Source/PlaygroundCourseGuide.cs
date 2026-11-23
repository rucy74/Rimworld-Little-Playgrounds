using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

// Tells the player whether a stepping stone, timber step or balance log is part of a course. Pieces of all three kinds
// link edge to edge and a course needs two cells (Building_PlaygroundFacility.CanUse): a one-cell stone or step needs a
// neighbour, a two-cell balance log is a short course on its own.
public static class CourseGuide
{
    static bool IsCourseDef(BuildableDef d)=>d is ThingDef t&&t.GetModExtension<ExpansionProperties>() is {kind: ExpansionKind.SteppingStone or ExpansionKind.BalanceLog};

    // A built course piece, or with planned=true also a blueprint or frame of one.
    public static bool IsPiece(Thing t,bool planned)=>t is Building_PlaygroundFacility f&&f.IsCourse
        ||planned&&t is Blueprint or Frame&&IsCourseDef(t.def.entityDefToBuild);
    public static bool PieceAt(Map map,IntVec3 c,bool planned)=>c.InBounds(map)&&c.GetThingList(map).Any(t=>IsPiece(t,planned));

    // Course cells linked edge to edge with start, start included (the whole connected set, not one visitor's walk).
    public static int Linked(Map map,IntVec3 start,bool planned,int cap=99)=>Linked(map,new[]{start},planned,cap);
    public static int Linked(Map map,IEnumerable<IntVec3> start,bool planned,int cap=99)
    {
        var seen=new HashSet<IntVec3>(start);var queue=new Queue<IntVec3>(seen);
        while(queue.Count>0&&seen.Count<cap)
        {
            var c=queue.Dequeue();
            foreach(var d in GenAdj.CardinalDirections)
            {
                var n=c+d;
                if(!seen.Contains(n)&&PieceAt(map,n,planned)){seen.Add(n);queue.Enqueue(n);}
            }
        }
        return seen.Count;
    }

    public static bool CornerOnly(Map map,IntVec3 c,bool planned)=>!GenAdj.CardinalDirections.Any(d=>PieceAt(map,c+d,planned))
        &&GenAdj.DiagonalDirections.Any(d=>PieceAt(map,c+d,planned));

    // Info-panel line for a built piece.
    public static string Inspect(Building_PlaygroundFacility piece)
    {
        var map=piece.Map;var c=piece.Position;
        int built=Linked(map,c,false);
        if(built>=2)return "LP_Inspect_CourseLinked".Translate(built);
        if(GenAdj.CardinalDirections.Any(d=>PieceAt(map,c+d,true)))return "LP_Inspect_CourseWaiting".Translate();
        return CornerOnly(map,c,true)?"LP_Inspect_CourseCorner".Translate():"LP_Inspect_CourseAlone".Translate();
    }

    static List<IntVec3> Footprint(ThingDef def,IntVec3 c,Rot4 rot)=>GenAdj.OccupiedRect(c,rot,def?.Size??IntVec2.One).Cells.ToList();

    // Text beside the mouse while placing a piece at c (its own cells and planned pieces count).
    public static string Placing(Map map,ThingDef def,IntVec3 c,Rot4 rot)
    {
        var cells=Footprint(def,c,rot);
        bool neighbour=cells.Any(x=>GenAdj.CardinalDirections.Any(d=>!cells.Contains(x+d)&&PieceAt(map,x+d,true)));
        if(neighbour)return "LP_Place_CourseLinked".Translate(Linked(map,cells,true));
        if(cells.Count>=2)return "LP_Place_CourseSelf".Translate(cells.Count);
        return CornerOnly(map,c,true)?"LP_Place_CourseCorner".Translate():"LP_Place_CourseAlone".Translate();
    }

    // Cells to outline while placing at c: the pieces it links to, or, when it links to none and is too short on its
    // own, the edge cells where the next piece would link.
    public static void GhostCells(Map map,ThingDef def,IntVec3 c,Rot4 rot,List<IntVec3> linked,List<IntVec3> open)
    {
        linked.Clear();open.Clear();var cells=Footprint(def,c,rot);
        foreach(var x in cells)foreach(var d in GenAdj.CardinalDirections)
        {
            var n=x+d;if(cells.Contains(n)||!n.InBounds(map)||linked.Contains(n)||open.Contains(n))continue;
            if(PieceAt(map,n,true))linked.Add(n);else if(n.Standable(map))open.Add(n);
        }
        if(linked.Count>0||cells.Count>=2)open.Clear();
    }
}

public sealed class PlaceWorker_CourseLink:PlaceWorker
{
    // Course pieces are not edifices, so the game would let them overlap; a two-cell log dragged along a line would put
    // one on every cell. One course piece (built or planned) per cell keeps a dragged line of logs end to end.
    public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef,IntVec3 loc,Rot4 rot,Map map,Thing thingToIgnore=null,Thing thing=null)
    {
        foreach(var c in GenAdj.OccupiedRect(loc,rot,checkingDef.Size))
            if(c.InBounds(map)&&c.GetThingList(map).Any(t=>t!=thingToIgnore&&t!=thing&&CourseGuide.IsPiece(t,true)
                &&!(t is Blueprint_Install install&&install.ThingToInstall==thing)))
                return "LP_Place_CourseOccupied".Translate();
        return true;
    }
    static readonly List<IntVec3> linked=new(),open=new();
    static readonly Color LinkColor=new(.45f,.85f,.45f),OpenColor=new(1f,.85f,.35f);
    public override void DrawGhost(ThingDef def,IntVec3 center,Rot4 rot,Color ghostCol,Thing thing=null)
    {
        var map=Find.CurrentMap;if(map==null||!center.InBounds(map))return;
        CourseGuide.GhostCells(map,def,center,rot,linked,open);
        if(linked.Count>0)GenDraw.DrawFieldEdges(linked,LinkColor);
        if(open.Count>0)GenDraw.DrawFieldEdges(open,OpenColor);
    }
    public override void DrawPlaceMouseAttachments(float curX,ref float curY,BuildableDef def,IntVec3 center,Rot4 rot)
    {
        var map=Find.CurrentMap;if(map==null||!center.InBounds(map))return;
        var rect=new Rect(curX,curY,9999f,Text.LineHeight);
        Widgets.Label(rect,CourseGuide.Placing(map,def as ThingDef,center,rot));
        curY+=rect.height;
    }
}
