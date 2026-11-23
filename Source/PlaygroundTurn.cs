using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

public enum TurnMap { North, East, South, West, FlipNorth, FlipEast }

public static class PlaygroundTurn
{
    // How each ride turns, by placement. Rides not listed only ever stand north.
    public static TurnMap Map(Thing t)
    {
        if(t==null)return TurnMap.North;
        return MapFor(t.def,t.Rotation);
    }
    public static TurnMap MapFor(ThingDef def,Rot4 rot)
    {
        switch(Kind(def))
        {
            case TurnKind.Distinct:return rot.AsInt switch{1=>TurnMap.East,2=>TurnMap.South,3=>TurnMap.FlipEast,_=>TurnMap.North};
            case TurnKind.Symmetric:return rot.AsInt switch{1=>TurnMap.East,3=>TurnMap.FlipEast,_=>TurnMap.North};
            case TurnKind.SideView:return rot.AsInt switch{1=>TurnMap.East,2=>TurnMap.FlipNorth,3=>TurnMap.West,_=>TurnMap.North};
            case TurnKind.Flat:return rot.AsInt switch{1=>TurnMap.East,2=>TurnMap.South,3=>TurnMap.West,_=>TurnMap.North};
            default:return TurnMap.North;
        }
    }
    // Distinct  - front and back differ: its own back picture (south) and side picture (east, flipped for west).
    // Symmetric - looks the same from front and back: south is north, east has its own picture (flipped for west).
    // SideView  - drawn from the side (the fox): south is the picture flipped, east and west are its rear and front.
    // Flat      - drawn on the ground (hopscotch): every placement is the picture turned.
    public enum TurnKind { None, Distinct, Symmetric, SideView, Flat }
    public static TurnKind Kind(ThingDef def)
    {
        if(def==null)return TurnKind.None;
        if(def.entityDefToBuild is ThingDef built)def=built;
        return def.GetModExtension<TurnProperties>()?.kind??TurnKind.None;
    }
    public static bool Turns(ThingDef def)=>Kind(def)!=TurnKind.None;
    // A planned ride with no turned picture for this placement (the south of a ride that looks the same both ways) shows its north
    // picture as it is: the plain graphic would spin it upside down.
    public static Rot4 PictureRot(ThingDef def,Rot4 rot)=>Turns(def)?Rot4.North:rot;

    // A cell given relative to the ride's Position as if it faced north, on the ride as it stands now.
    public static IntVec3 Cell(Thing t,IntVec3 north)=>Cell(t.def,t.Position,t.Rotation,north);
    public static IntVec3 Cell(ThingDef def,IntVec3 position,Rot4 rot,IntVec3 north)
    {
        var map=MapFor(def,rot);
        // Rides that never turn keep their own cells, whatever their rotation (the balance log turns on its own).
        if(map==TurnMap.North&&(rot==Rot4.North||!Turns(def)))return position+north;
        var size=def.size;
        int lx=north.x+(size.x-1)/2,lz=north.z+(size.z-1)/2; // from the corner of the north footprint
        var rect=GenAdj.OccupiedRect(position,rot,size);
        int x,z;
        switch(map)
        {
            case TurnMap.East:x=lz;z=size.x-1-lx;break;
            case TurnMap.South:x=size.x-1-lx;z=size.z-1-lz;break;
            case TurnMap.West:x=size.z-1-lz;z=lx;break;
            case TurnMap.FlipNorth:x=size.x-1-lx;z=lz;break;
            case TurnMap.FlipEast:x=size.z-1-lz;z=size.x-1-lx;break;
            default:x=lx;z=lz;break;
        }
        return new IntVec3(rect.minX+x,0,rect.minZ+z);
    }
    // A ground vector (x east, z north, no height) relative to the ride's centre, as if it faced north.
    public static Vector3 Ground(TurnMap map,Vector3 v)=>map switch
    {
        TurnMap.East=>new(v.z,v.y,-v.x),
        TurnMap.South=>new(-v.x,v.y,-v.z),
        TurnMap.West=>new(-v.z,v.y,v.x),
        TurnMap.FlipNorth=>new(-v.x,v.y,v.z),
        TurnMap.FlipEast=>new(-v.z,v.y,-v.x),
        _=>v
    };
    public static Vector3 Ground(Thing t,Vector3 v)=>Ground(Map(t),v);
    // The way a rider faces, given the way it would face on the north ride.
    public static Rot4 Facing(TurnMap map,Rot4 north)=>map switch
    {
        TurnMap.East=>new Rot4((north.AsInt+1)%4),
        TurnMap.South=>new Rot4((north.AsInt+2)%4),
        TurnMap.West=>new Rot4((north.AsInt+3)%4),
        TurnMap.FlipNorth=>north.IsHorizontal?north.Opposite:north,
        TurnMap.FlipEast=>Flip(new Rot4((north.AsInt+1)%4)),
        _=>north
    };
    static Rot4 Flip(Rot4 r)=>r.IsHorizontal?r.Opposite:r;
    public static Rot4 Facing(Thing t,Rot4 north)=>Facing(Map(t),north);
    // Pictures: north is the original; flipped placements draw the mirror image.
    public static bool Flipped(TurnMap map)=>map is TurnMap.FlipNorth or TurnMap.FlipEast;
}

// One picture of a turned ride: a PNG, the size it was drawn at, how big one pixel is on the map, and which pixel sits on
// the ride's centre. Points and parts are read in the picture's own pixels; a mirrored picture flips both.
public sealed class TurnPicture
{
    public readonly string path;public readonly int width,height;public readonly float scale;public readonly Vector2 origin;public readonly bool mirror;
    public TurnPicture(string path,int width,int height,float scale,Vector2 origin,bool mirror=false)
    {this.path=path;this.width=width;this.height=height;this.scale=scale;this.origin=origin;this.mirror=mirror;}
    public TurnPicture Mirrored=>new(path,width,height,scale,origin,!mirror);
    public SpritePart Part(params Vector2[] polygon)=>new(path,width,height,scale,origin,polygon,mirror:mirror);
    public SpritePart Rect(float x,float y,float w,float h)=>Part(SpritePart.Rect(x,y,w,h));
    public SpritePart Whole()=>Rect(0,0,width,height);
    // Pivoting parts (a seesaw plank) turn about their own pixel.
    public SpritePart PartAbout(Vector2 pivot,params Vector2[] polygon)=>new(path,width,height,scale,pivot,polygon,mirror:mirror);
    public Vector3 Point(float x,float y)=>new((x-origin.x)*scale*(mirror?-1:1),0,(origin.y-y)*scale);
    public Vector3 Point(Vector2 p)=>Point(p.x,p.y);
}

// Planned rides (the mouse ghost, blueprints and frames) draw the picture of the way they will stand. The art classes
// fill this in at startup; a north placement, or a ride without a turned picture, keeps the def's own graphic.
public static class TurnGhosts
{
    sealed class Piece{public string texture;public System.Action<Vector3,Material> draw;}
    static readonly System.Collections.Generic.Dictionary<(ThingDef,int),System.Collections.Generic.List<Piece>> pieces=new();
    public static void Add(ThingDef def,Rot4 rot,SpritePart part,Vector3 offset=default,float angle=0,Vector3? scale=null)
    {if(part!=null)Add(def,rot,part.TexturePath,(at,m)=>part.DrawWith(at+offset,m,angle,scale));}
    // Pieces drawn some other way (the entrance's joined fence mesh).
    public static void Add(ThingDef def,Rot4 rot,string texture,System.Action<Vector3,Material> draw)
    {
        if(def==null||draw==null)return;
        if(!pieces.TryGetValue((def,rot.AsInt),out var list))pieces[(def,rot.AsInt)]=list=new();
        list.Add(new Piece{texture=texture,draw=draw});
    }
    public static bool Draw(Vector3 loc,Rot4 rot,ThingDef thingDef,Shader shader,Color color)
    {
        if(rot==Rot4.North||thingDef==null)return false;
        var def=thingDef.entityDefToBuild as ThingDef??thingDef;
        if(!pieces.TryGetValue((def,rot.AsInt),out var list))return false;
        foreach(var piece in list)piece.draw(loc,MaterialPool.MatFrom(piece.texture,shader,color));
        return true;
    }
}

// Marks a ride that can be turned, and how (PlaygroundTurn.TurnKind).
public sealed class TurnProperties:DefModExtension
{
    public PlaygroundTurn.TurnKind kind=PlaygroundTurn.TurnKind.None;
}

public sealed class PlaceWorker_TurnSidesOnly:PlaceWorker
{
    static readonly System.Reflection.FieldInfo PlacingRot=typeof(Designator_Place).GetField("placingRot",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
    static Rot4 last=Rot4.North;
    public static void Keep(BuildableDef def)
    {
        if(PlacingRot==null||Find.DesignatorManager?.SelectedDesignator is not Designator_Place place||place.PlacingDef!=def)return;
        var rot=(Rot4)PlacingRot.GetValue(place);
        if(!rot.IsHorizontal){last=rot;return;}
        last=last.Opposite;PlacingRot.SetValue(place,last);
    }
    public override void DrawGhost(ThingDef def,IntVec3 center,Rot4 rot,Color ghostCol,Thing thing=null)=>Keep(def);
    public override void DrawOnGUIExtra(BuildableDef def)=>Keep(def);
}
