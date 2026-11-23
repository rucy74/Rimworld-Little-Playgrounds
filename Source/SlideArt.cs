using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

[StaticConstructorOnStartup]
public static class SlideArt
{
    static readonly Material Outline=Mat(.16f,.22f,.22f), Teal=Mat(.30f,.59f,.54f), DarkTeal=Mat(.23f,.39f,.37f);
    static readonly Material Coral=Mat(.76f,.39f,.31f), CoralLight=Mat(.90f,.57f,.43f);
    static readonly Material Yellow=Mat(.89f,.69f,.34f), YellowLight=Mat(.99f,.82f,.47f);
    static readonly Material Blue=Mat(.31f,.58f,.68f), BlueLight=Mat(.49f,.72f,.79f), BlueDark=Mat(.23f,.43f,.53f);
    static readonly Material Metal=Mat(.73f,.77f,.73f), Rubber=Mat(.25f,.29f,.27f);
    static readonly Mesh ChuteOutline=Ribbon(.57f,.65f), ChuteSurface=Ribbon(.51f,.59f), ChuteCenter=Ribbon(.35f,.42f);
    static readonly float[] Posts={-1.42f,1.12f}, Sides={-1,1};
    static Material Mat(float r,float g,float b)=>SolidColorMaterials.SimpleSolidColorMaterial(new Color(r,g,b));
    static Vector3 V(float x,float z)=>new(x,0,z);
    static Vector3 P(Vector3 model)=>SlideMotion.Project(model);
    static void Line(Vector3 o,Vector3 a,Vector3 b,float width,Material material,float layer)
    {
        var d=b-a;var p=o+(a+b)*.5f;p.y=layer;
        Graphics.DrawMesh(MeshPool.plane10,Matrix4x4.TRS(p,Quaternion.AngleAxis(d.AngleFlat(),Vector3.up),
            new Vector3(width,1,d.MagnitudeHorizontal())),material,0);
    }
    static void Oval(Vector3 o,Vector3 at,float w,float h,Material material,float layer)
    {
        var p=o+at;p.y=layer;
        Graphics.DrawMesh(MeshPool.circle,Matrix4x4.TRS(p,Quaternion.identity,new Vector3(w*.5f,1,h*.5f)),material,0);
    }
    static void Capsule(Vector3 o,Vector3 a,Vector3 b,float width,Material material,float layer)
    {
        Line(o,a,b,width,material,layer);Oval(o,a,width,width,material,layer);Oval(o,b,width,width,material,layer);
    }
    static void Pipe(Vector3 o,Vector3 a,Vector3 b,float width,Material material,float layer)
    {
        Capsule(o,a,b,width+.035f,Outline,layer);Capsule(o,a,b,width,material,layer+.003f);
    }
    static Mesh Ribbon(float topHalf,float bottomHalf)
    {
        var vertices=new Vector3[18];var triangles=new int[48];
        for(int i=0;i<=8;i++)
        {
            float t=i/8f,w=Mathf.Lerp(topHalf,bottomHalf,t);var center=P(SlideMotion.Chute(t));
            vertices[i*2]=center+V(-w,0);vertices[i*2+1]=center+V(w,0);
            if(i==8)continue;
            int a=i*2,k=i*6;
            triangles[k]=a;triangles[k+1]=a+1;triangles[k+2]=a+2;
            triangles[k+3]=a+2;triangles[k+4]=a+1;triangles[k+5]=a+3;
        }
        var mesh=new Mesh{name="LittlePlaygroundsSlideRibbon"};
        mesh.vertices=vertices;mesh.triangles=triangles;mesh.uv=new Vector2[vertices.Length];mesh.RecalculateNormals();
        return mesh;
    }
    static void DrawRibbon(Vector3 origin,Mesh mesh,Material material,float layer)
    {
        origin.y=layer;Graphics.DrawMesh(mesh,origin,Quaternion.identity,material,0);
    }
    public static void Draw(Vector3 o)
    {
        float rear=AltitudeLayer.Pawn.AltitudeFor()-.20f,front=AltitudeLayer.Pawn.AltitudeFor()+.3f;
        float deck=P(SlideMotion.ChuteTop).z;
        // Supporting posts and far-side guard rail, all behind the unmodified pawn.
        foreach(float x in Posts)
        {
            Pipe(o,V(x,.35f),V(x,deck+.38f),.13f,DarkTeal,rear);
            Capsule(o,V(x-.07f,.35f),V(x+.07f,.35f),.11f,Rubber,rear+.005f);
        }
        Pipe(o,V(-1.42f,deck+.38f),V(1.12f,deck+.38f),.10f,Teal,rear+.01f);
        Capsule(o,V(-1.38f,deck+.405f),V(1.08f,deck+.405f),.022f,Metal,rear+.014f);
        // Upper platform connects the two distinct lanes.
        Pipe(o,V(-1.02f,deck),V(.68f,deck),.48f,Yellow,rear+.02f);
        Capsule(o,V(-1.05f,deck+.13f),V(.70f,deck+.13f),.035f,YellowLight,rear+.025f);
        // Six broad treads; the motion samples these same endpoints.
        for(int i=0;i<=6;i++)
        {
            var rung=P(Vector3.Lerp(SlideMotion.LadderFoot,SlideMotion.LadderTop,i/6f));
            Pipe(o,rung+V(-.36f,0),rung+V(.36f,0),.13f,Teal,rear+.03f);
            Capsule(o,rung+V(-.34f,.032f),rung+V(.34f,.032f),.022f,Metal,rear+.035f);
        }
        foreach(float side in Sides)
        {
            var bottom=P(SlideMotion.LadderFoot)+V(side*.43f,-.04f);
            var top=P(SlideMotion.LadderTop)+V(side*.43f,.30f);
            Pipe(o,bottom,top,.10f,Coral,front);
            Line(o,bottom+V(-.018f,.03f),top+V(-.018f,0),.022f,CoralLight,front+.006f);
        }
        DrawRibbon(o,ChuteOutline,Outline,rear+.04f);
        DrawRibbon(o,ChuteSurface,Blue,rear+.045f);
        DrawRibbon(o,ChuteCenter,BlueLight,rear+.05f);
        // Raised side walls stay outside the rider silhouette.
        foreach(float side in Sides)
        {
            for(int i=0;i<8;i++)
            {
                float a=i/8f,b=(i+1)/8f;
                var start=P(SlideMotion.Chute(a))+V(side*Mathf.Lerp(.49f,.57f,a),.06f);
                var end=P(SlideMotion.Chute(b))+V(side*Mathf.Lerp(.49f,.57f,b),.06f);
                Pipe(o,start,end,.09f,Yellow,front+.01f);
                Line(o,start+V(-.015f,.025f),end+V(-.015f,.025f),.023f,YellowLight,front+.015f);
            }
        }
        var foot=P(SlideMotion.ChuteFoot);
        Pipe(o,foot+V(-.51f,0),foot+V(.51f,0),.12f,BlueDark,rear+.055f);
        Capsule(o,foot+V(-.48f,.025f),foot+V(.48f,.025f),.028f,BlueLight,rear+.06f);
        foreach(float x in Posts)
        {
            Oval(o,V(x,deck+.38f),.12f,.12f,Outline,rear+.07f);
            Oval(o,V(x,deck+.38f),.055f,.055f,Metal,rear+.075f);
        }
    }
}
