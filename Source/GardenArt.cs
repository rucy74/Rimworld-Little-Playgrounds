using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

[StaticConstructorOnStartup]
public static class GardenArt
{
    static readonly SpritePart BenchBody=new("Things/Building/GardenBenchCompactV3",2020,778,.0019f,new Vector2(1010,410),SpritePart.Rect(0,0,2020,778));
    // The shallow two-slat seat keeps its drawn proportions, with no stretched apron.
    static readonly SpritePart BenchLip=new("Things/Building/GardenBenchCompactV3",2020,778,.0019f,new Vector2(1010,410),
        SpritePart.Rect(350,490,1315,43));
    static readonly SpritePart SandBase=new("Things/Building/OakSandpitPolishV2",1254,1254,.0036f,new Vector2(627,627),SpritePart.Rect(0,0,1254,1254));
    static readonly SpritePart SandLip=new("Things/Building/OakSandpitPolishV2",1254,1254,.0036f,new Vector2(627,627),SpritePart.Rect(120,994,1020,90));
    static readonly SpritePart[] Castles={Castle(40,330,465,285,new Vector2(270,590)),Castle(735,250,410,365,new Vector2(935,594)),
        Castle(1275,85,865,540,new Vector2(1700,603))};
    static readonly Material SandGrain=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.78f,.61f,.33f));
    static SpritePart Castle(float x,float y,float w,float h,Vector2 anchor)
        =>new("Things/Building/SandcastleStages",2172,724,.00235f,anchor,SpritePart.Rect(x,y,w,h));
    static readonly SpritePart Posts=new("Things/Building/ShadeCanopy",1536,1024,.0038f,new Vector2(768,800),
        SpritePart.Rect(0,350,1536,674),SpritePart.Rect(0,475,1536,549));
    static readonly SpritePart Roof=new("Things/Building/ShadeCanopy",1536,1024,.0038f,new Vector2(768,800),SpritePart.Rect(0,0,1536,475));
    static readonly SpritePart FadedRoof=new("Things/Building/ShadeCanopy",1536,1024,.0038f,new Vector2(768,800),SpritePart.Rect(0,0,1536,475),
        shader:ShaderDatabase.Transparent,tint:new Color(1,1,1,.32f));
    // Turned benches (PlaygroundTurn): seen from behind the backrest is nearest and hides a sitter's lower half; seen from the side
    // the seat runs up the map with the backrest on its far side, and the near end's frame is in front of the nearer sitter.
    static readonly TurnPicture BenchBack=new("Things/Building/Turn/bench-back",1011,423,.00274f,new Vector2(505,244));
    static readonly TurnPicture BenchSide=new("Things/Building/Turn/bench-side",278,892,.0037f,new Vector2(140,525));
    static readonly SpritePart BackWhole=BenchBack.Whole(),BackFront=BenchBack.Rect(0,0,1011,232);
    static readonly SpritePart[] SideWhole={BenchSide.Whole(),BenchSide.Mirrored.Whole()},SideFront={BenchSide.Rect(0,668,278,224),BenchSide.Mirrored.Rect(0,668,278,224)};
    static GardenArt()
    {
        TurnGhosts.Add(GardenDefOf.LP_GardenBench,Rot4.South,BackWhole);
        TurnGhosts.Add(GardenDefOf.LP_GardenBench,Rot4.East,SideWhole[0]);TurnGhosts.Add(GardenDefOf.LP_GardenBench,Rot4.West,SideWhole[1]);
    }
    public static Vector3 BenchSeat(Thing bench,int slot)
    {
        var map=PlaygroundTurn.Map(bench);
        switch(map)
        {
            case TurnMap.South:return new(slot==0?.65f:-.65f,0,.15f);
            case TurnMap.East:case TurnMap.FlipEast:
                var side=map==TurnMap.East?BenchSide:BenchSide.Mirrored;
                // The sitter is drawn about half a cell above its seat point (GardenAnimationWorker), so the points sit low on each half.
                return side.Point(105,slot==0?370:660);
            default:return new(slot==0?-.65f:.65f,0,.1f);
        }
    }
    public static void Bench(Thing bench,Vector3 at)
    {
        float rear=AltitudeLayer.Pawn.AltitudeFor()-.15f,front=AltitudeLayer.Pawn.AltitudeFor()+.3f;
        switch(PlaygroundTurn.Map(bench))
        {
            case TurnMap.South:
                PlaygroundGround.Contact(at+new Vector3(0,0,-.40f),2.65f,.32f);
                BackWhole.Draw(at,rear);BackFront.Draw(at,front);return;
            case TurnMap.East:case TurnMap.FlipEast:
                int i=PlaygroundTurn.Map(bench)==TurnMap.East?0:1;
                PlaygroundGround.Contact(at+new Vector3(0,0,-.35f),.95f,2.6f);
                SideWhole[i].Draw(at,rear);SideFront[i].Draw(at,front);return;
        }
        PlaygroundGround.Contact(at+new Vector3(0,0,-.33f),2.65f,.32f);
        BenchBody.Draw(at,rear);BenchLip.Draw(at,front);
    }
    public static void Sandbox(Building_Sandbox box,Vector3 at)
    {
        PlaygroundGround.Contact(at+new Vector3(0,0,-1.54f),3.62f,.26f);
        SandBase.Draw(at,AltitudeLayer.Pawn.AltitudeFor()-.20f);SandLip.Draw(at,AltitudeLayer.Pawn.AltitudeFor()+.3f);
        if(box.Stage>0)Castles[box.Stage-1].Draw(at+new Vector3(0,0,-.15f),AltitudeLayer.Pawn.AltitudeFor()-.1f);
        for(int slot=0;slot<box.SeatCount;slot++)
        {
            var visit=box.JobAt(slot);if(visit==null||visit.Age<60||visit.Blend<.99f)continue;
            float phase=PlaygroundMotion.SandPhase(visit.Age,slot);
            if(phase<.32f||phase>.52f)continue;
            float u=(phase-.32f)/.20f;
            var from=box.SeatOffset(slot)*.68f;
            for(int n=0;n<3;n++)
            {
                var grain=at+Vector3.Lerp(from,from*.35f,u)+new Vector3((n-1)*.045f,0,.18f*Mathf.Sin(u*Mathf.PI));
                grain.y=AltitudeLayer.Pawn.AltitudeFor()+.03f;
                Graphics.DrawMesh(MeshPool.circle,Matrix4x4.TRS(grain,Quaternion.identity,new Vector3(.035f,1,.024f)),SandGrain,0);
            }
        }
    }
    public static void Canopy(Building_ShadeCanopy canopy,Vector3 at)
    {
        at.z-=1;
        Posts.Draw(at,AltitudeLayer.Pawn.AltitudeFor()-.18f);
        (canopy.PawnObscuredByFabric?FadedRoof:Roof).Draw(at+new Vector3(0,0,.475f),AltitudeLayer.Pawn.AltitudeFor()+.5f);
    }
}

// Own roof def prevents us from removing roofs built by the player. Nearby
// canopies share coverage; removing either one keeps the overlap sheltered.
public sealed class Building_ShadeCanopy:Building
{
    // Follows a push from Tucked Furniture; zero without it (PlaygroundTuck).
    public override Vector3 DrawPos=>base.DrawPos+PlaygroundTuck.Shift(this);
    public CellRect ShadeCells=>CellRect.CenteredOn(Position+IntVec3.South,5,3);
    public bool PawnObscuredByFabric=>Spawned&&Map.mapPawns.AllPawnsSpawned.Any(FabricOverlaps);
    bool FabricOverlaps(Pawn pawn)
    {
        // Fabric's visible rectangle is north of the bench. Ground shelter cells
        // are unrelated to whether the raised sprite actually hides a pawn.
        var relative=pawn.DrawPos-DrawPos;
        if(pawn.jobs?.curDriver is JobDriver_GardenVisit visit&&visit.Age>=0)
            relative.z+=(pawn.CurJob.targetA.Thing is Building_GardenBench ? .48f : .30f)*visit.Blend;
        return relative.x+.32f> -1.82f&&relative.x-.32f<1.82f
            &&relative.z+.95f>.78f&&relative.z<2.12f;
    }
    public override void SpawnSetup(Map map,bool respawningAfterLoad)
    {
        base.SpawnSetup(map,respawningAfterLoad);
        if(!respawningAfterLoad)
            foreach(var c in ShadeCells)if(c.InBounds(map)&&map.roofGrid.RoofAt(c)==null)map.roofGrid.SetRoof(c,GardenDefOf.LP_FabricShade);
    }
    public override void DeSpawn(DestroyMode mode=DestroyMode.Vanish)
    {
        var map=Map;var cells=ShadeCells;
        base.DeSpawn(mode);
        foreach(var c in cells)
            if(c.InBounds(map)&&map.roofGrid.RoofAt(c)==GardenDefOf.LP_FabricShade
                &&!map.listerThings.ThingsOfDef(GardenDefOf.LP_ShadeCanopy).OfType<Building_ShadeCanopy>().Any(b=>b.ShadeCells.Contains(c)))
                map.roofGrid.SetRoof(c,null);
    }
    protected override void DrawAt(Vector3 at,bool flip=false)=>GardenArt.Canopy(this,at);
}

[StaticConstructorOnStartup]
public sealed class Building_GardenCurb:Building
{
    // Follows a push from Tucked Furniture; zero without it (PlaygroundTuck).
    public override Vector3 DrawPos=>base.DrawPos+PlaygroundTuck.Shift(this);
    static readonly Material Outline=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.24f,.21f,.15f));
    static readonly Material Oak=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.72f,.52f,.29f));
    static readonly Material OakTop=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.88f,.70f,.43f));
    static readonly Material Ivory=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.77f,.74f,.63f));
    static readonly Material IvoryTop=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.94f,.90f,.77f));
    static readonly IntVec3[] Directions={IntVec3.North,IntVec3.East,IntVec3.South,IntVec3.West};
    public bool Connects(IntVec3 delta)
    {
        var c=Position+delta;
        return Spawned&&c.InBounds(Map)&&c.GetThingList(Map).Any(t=>t is Building_GardenCurb);
    }
    static void Strip(Vector3 at,Vector3 to,float width,Material mat,float altitude)
    {
        var d=to-at;var midpoint=(at+to)*.5f;midpoint.y=altitude;
        Graphics.DrawMesh(MeshPool.plane10,Matrix4x4.TRS(midpoint,Quaternion.AngleAxis(d.AngleFlat(),Vector3.up),
            new Vector3(width,1,d.MagnitudeHorizontal())),mat,0);
    }
    static void Square(Vector3 at,float width,float depth,Material mat,float altitude)
    {
        at.y=altitude;
        Graphics.DrawMesh(MeshPool.plane10,Matrix4x4.TRS(at,Quaternion.identity,new Vector3(width,1,depth)),mat,0);
    }
    protected override void DrawAt(Vector3 at,bool flip=false)
    {
        var fill=def==GardenDefOf.LP_OakCurb?Oak:Ivory;
        var top=def==GardenDefOf.LP_OakCurb?OakTop:IvoryTop;
        float layer=AltitudeLayer.FloorEmplacement.AltitudeFor();bool any=false;
        foreach(var d in Directions)
        {
            if(!Connects(d))continue;any=true;
            var end=at+d.ToVector3()*.505f;
            Strip(at,end,.34f,Outline,layer);
            Strip(at+new Vector3(0,0,-.025f),end+new Vector3(0,0,-.025f),.29f,fill,layer+.002f);
            Strip(at+new Vector3(0,0,.028f),end+new Vector3(0,0,.028f),.26f,top,layer+.004f);
        }
        // Flat rectangular joins, not a round post at every tile.
        Square(at,any ? .34f : .9f,.34f,Outline,layer+.005f);
        Square(at+new Vector3(0,0,-.025f),any ? .29f : .85f,.29f,fill,layer+.006f);
        Square(at+new Vector3(0,0,.028f),any ? .26f : .82f,.26f,top,layer+.007f);
        if(Connects(IntVec3.East))Square(at+new Vector3(.49f,0,.028f),.012f,.26f,fill,layer+.008f);
        if(Connects(IntVec3.North))Square(at+new Vector3(0,0,.49f),.26f,.012f,fill,layer+.008f);
    }
}
