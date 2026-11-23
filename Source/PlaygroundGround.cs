using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

// Small contact shadows keep the sprites on the ground without changing sun or pawn rendering.
[StaticConstructorOnStartup]
public static class PlaygroundGround
{
    static readonly Material Soft=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.12f,.10f,.07f,.035f));
    static readonly Material Core=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.12f,.10f,.07f,.075f));
    static void Oval(Vector3 at,float width,float depth,Material material,float layer)
    {
        at.y=layer;
        Graphics.DrawMesh(MeshPool.circle,Matrix4x4.TRS(at,Quaternion.identity,new Vector3(width*.5f,1,depth*.5f)),material,0);
    }
    public static void Contact(Vector3 at,float width,float depth)
    {
        float layer=AltitudeLayer.FloorEmplacement.AltitudeFor();
        Oval(at,width*1.18f,depth*1.2f,Soft,layer);
        Oval(at,width,depth,Soft,layer+.001f);
        Oval(at,width*.74f,depth*.68f,Core,layer+.002f);
    }
}
