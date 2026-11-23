using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

[StaticConstructorOnStartup]
public static class SwingArt
{
    static readonly Material Outline = SolidColorMaterials.SimpleSolidColorMaterial(new Color(.16f,.22f,.22f));
    static readonly Material Teal = SolidColorMaterials.SimpleSolidColorMaterial(new Color(.30f,.59f,.54f));
    static readonly Material TealLight = SolidColorMaterials.SimpleSolidColorMaterial(new Color(.51f,.73f,.65f));
    static readonly Material DarkTeal = SolidColorMaterials.SimpleSolidColorMaterial(new Color(.23f,.39f,.37f));
    static readonly Material Coral = SolidColorMaterials.SimpleSolidColorMaterial(new Color(.76f,.39f,.31f));
    static readonly Material CoralLight = SolidColorMaterials.SimpleSolidColorMaterial(new Color(.90f,.57f,.43f));
    static readonly Material Yellow = SolidColorMaterials.SimpleSolidColorMaterial(new Color(.89f,.69f,.34f));
    static readonly Material YellowLight = SolidColorMaterials.SimpleSolidColorMaterial(new Color(.99f,.82f,.47f));
    static readonly Material YellowDark = SolidColorMaterials.SimpleSolidColorMaterial(new Color(.60f,.40f,.22f));
    static readonly Material Metal = SolidColorMaterials.SimpleSolidColorMaterial(new Color(.73f,.77f,.73f));
    static readonly Material Rubber = SolidColorMaterials.SimpleSolidColorMaterial(new Color(.25f,.29f,.27f));
    static readonly Material Shadow = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0f,0f,0f,.065f));
    static readonly Material ShadowSoft = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0f,0f,0f,.025f));
    static Vector3 V(float x,float z) => new(x,0,z);
    static void Line(Vector3 origin,Vector3 a,Vector3 b,float width,Material mat,float layer)
    {
        Vector3 d=b-a;var at=origin+(a+b)*.5f;at.y=layer;
        Graphics.DrawMesh(MeshPool.plane10,Matrix4x4.TRS(at,Quaternion.AngleAxis(d.AngleFlat(),Vector3.up),
            new Vector3(width,1,d.MagnitudeHorizontal())),mat,0);
    }
    static void Oval(Vector3 o,Vector3 at,float width,float height,Material mat,float layer)
    {
        var p=o+at;p.y=layer;
        Graphics.DrawMesh(MeshPool.circle,Matrix4x4.TRS(p,Quaternion.identity,new Vector3(width*.5f,1,height*.5f)),mat,0);
    }
    static void Capsule(Vector3 o,Vector3 a,Vector3 b,float width,Material mat,float layer)
    {
        Line(o,a,b,width,mat,layer);
        Oval(o,a,width,width,mat,layer);Oval(o,b,width,width,mat,layer);
    }
    static void Bar(Vector3 o,Vector3 a,Vector3 b,float width,Material fill,float layer)
    {
        Capsule(o,a,b,width+.035f,Outline,layer);
        Capsule(o,a,b,width,fill,layer+.003f);
    }
    static void Side(Vector3 origin,float side,Material paint,Material highlight,float rear,float front)
    {
        var top=V(side*.96f,SwingMotion.PivotHeight+.08f);
        var rearFoot=V(side*1.25f,.05f);
        var nearFoot=V(side*1.66f,-.65f);
        Bar(origin,rearFoot,top,.13f,DarkTeal,rear);
        Bar(origin,rearFoot,nearFoot,.08f,DarkTeal,rear+.01f);
        Bar(origin,nearFoot,top,.15f,paint,rear+.02f);
        // A narrow continuous highlight gives the pipe volume without turning the view.
        var shine=V(-.033f,.018f);
        Line(origin,nearFoot+(top-nearFoot)*.035f+shine,top+(nearFoot-top)*.035f+shine,.03f,highlight,rear+.024f);
        Capsule(origin,nearFoot+V(-.075f,0),nearFoot+V(.075f,0),.13f,Outline,front+.01f);
        Capsule(origin,nearFoot+V(-.065f,.015f),nearFoot+V(.065f,.015f),.085f,Rubber,front+.02f);
        Oval(origin,top,.13f,.13f,Outline,front+.03f);
        Oval(origin,top,.065f,.065f,Metal,front+.04f);
    }
    public static void Draw(Building_Swing swing,Vector3 origin,int age)
    {
        float rear=AltitudeLayer.Pawn.AltitudeFor()-.13f;
        float front=AltitudeLayer.Pawn.AltitudeFor()+.3f;
        var seat=SwingMotion.SeatOffset(age);
        float pivot=SwingMotion.PivotHeight, beam=pivot+.08f;
        // Soft oval belongs only to the seat; native pawn artwork/shadows are untouched.
        float groundZ=-.78f*SwingMotion.SeatModel(age).z;
        float spread=1f+.18f*(SwingMotion.SeatModel(age).y-.5f);
        float ground=AltitudeLayer.FloorEmplacement.AltitudeFor();
        Oval(origin,V(0,groundZ),1.15f*spread,.48f*spread,ShadowSoft,ground);
        Oval(origin,V(0,groundZ),.87f*spread,.29f*spread,Shadow,ground+.001f);
        Side(origin,-1,Teal,TealLight,rear,front);Side(origin,1,Coral,CoralLight,rear,front);
        Bar(origin,V(-1.18f,beam),V(1.18f,beam),.18f,Yellow,rear+.03f);
        Capsule(origin,V(-1.15f,beam+.04f),V(1.15f,beam+.04f),.028f,YellowLight,rear+.035f);
        foreach(float side in Sides)
        {
            // Fixed bearing and a separate hanging eye read as an attached chain.
            var eye=V(side*.32f,pivot);
            Capsule(origin,eye+V(0,.12f),eye+V(0,.03f),.085f,Outline,rear+.04f);
            Capsule(origin,eye+V(0,.105f),eye+V(0,.035f),.04f,Metal,rear+.043f);
            Oval(origin,eye,.11f,.14f,Outline,rear+.044f);
            Oval(origin,eye,.055f,.08f,Metal,rear+.047f);
            var end=seat+V(side*.32f,0);
            Line(origin,eye,end,.045f,Outline,rear+.048f);
            Line(origin,eye+V(-.008f,0),end+V(-.008f,0),.018f,Metal,rear+.05f);
        }
        // The native pawn is the sole rider artwork. The seat lies underneath it.
        Capsule(origin,seat+V(-.31f,0),seat+V(.31f,0),.23f,Outline,rear+.055f);
        Capsule(origin,seat+V(-.30f,.015f),seat+V(.30f,.015f),.16f,Yellow,rear+.06f);
        Capsule(origin,seat+V(-.30f,.055f),seat+V(.30f,.055f),.035f,YellowLight,rear+.065f);
        Capsule(origin,seat+V(-.31f,-.075f),seat+V(.31f,-.075f),.065f,YellowDark,front+.015f);
        foreach(float side in Sides) Oval(origin,seat+V(side*.32f,.015f),.05f,.045f,Metal,front+.02f);
    }
    static readonly float[] Sides = {-1,1};
}
