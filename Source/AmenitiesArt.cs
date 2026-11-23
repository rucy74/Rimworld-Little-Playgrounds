using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

// Selected PNGs stay intact. UV regions provide native sprites and shared fence meshes.
[StaticConstructorOnStartup]
public static class AmenitiesArt
{
    const string HouseTex="Things/Building/ExpansionPlayhouseV3",WaterTex="Things/Building/ExpansionWaterAtlasV3",BoundaryTex="Things/Building/ExpansionBoundaryAtlasV3";
    static float Rear=>AltitudeLayer.Pawn.AltitudeFor()-.18f;
    static float Front=>AltitudeLayer.Pawn.AltitudeFor()+.29f;
    static float Floor=>AltitudeLayer.FloorEmplacement.AltitudeFor()+.04f;
    static Vector3 P(float x,float z)=>new(x,0,z);
    static readonly SpritePart House=new(HouseTex,1254,1254,.0029f,new(627,748),SpritePart.Rect(0,0,1254,1254));
    static readonly SpritePart Counter=new(HouseTex,1254,1254,.0029f,new(627,748),SpritePart.Rect(589,784,540,325));
    static readonly SpritePart LowBowl=new(WaterTex,1774,887,.0025f,new(278,650),SpritePart.Rect(130,265,295,515));
    static readonly SpritePart HighBowl=new(WaterTex,1774,887,.0025f,new(605,650),SpritePart.Rect(465,70,295,710));
    static readonly SpritePart Pipe=new(WaterTex,1774,887,.0025f,new(443,650),SpritePart.Rect(345,623,195,88));
    static readonly SpritePart Basin=new(WaterTex,1774,887,.0031f,new(1335,540),SpritePart.Rect(920,190,830,590));
    static readonly SpritePart Pad=new(BoundaryTex,1254,1254,.0053f,new(337,879),SpritePart.Rect(60,615,555,527));
    static readonly Material Boundary=MaterialPool.MatFrom(BoundaryTex,ShaderDatabase.Cutout);
    static readonly Material Stream=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.49f,.72f,.72f,.72f));
    static readonly Material Glint=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.80f,.91f,.88f,.63f));
    static readonly Material Ripple=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.65f,.82f,.79f,.30f));
    static readonly Dictionary<string,Texture2D> Icons=new();
    static readonly Mesh[] Fences=new Mesh[16];
    static Mesh entrance;
    static readonly Vector3[] Directions={Vector3.forward,Vector3.right,Vector3.back,Vector3.left};
    static readonly Rect PostRect=new(711,707,82,374),PicketRect=new(820,772,59,263),RailRect=new(908,875,317,56),SignRect=new(1090,201,80,155);

    public static void Playhouse(Vector3 at,float tick,int mask)
    {
        PlaygroundGround.Contact(at+P(0,-.86f),2.82f,.35f);
        House.Draw(at,Rear);Counter.Draw(at,Front);
        // A small game piece slides across the shop counter only when visitors use it.
        if(mask!=0)ExpansionArt.ShopToy(at+P(.63f,-.17f),tick,mask,Front+.01f);
    }
    public static void Drink(Vector3 at,float tick,int mask)
    {
        PlaygroundGround.Contact(at+P(0,-.28f),1.94f,.31f);
        Pipe.Draw(at,Rear,scale:new(2,1,1));
        // Lower the illustrated columns around their fixed feet; leave pawn heights intact.
        LowBowl.Draw(at+P(-.672f,-.07f),Rear+.003f,scale:new(1,1,.75f));
        HighBowl.Draw(at+P(.672f,-.07f),Rear+.003f,scale:new(1,1,.75f));
        if((mask&1)!=0)Arc(at+P(-.847f,.605f),at+P(-.672f,.44375f),.10f,tick,Rear+.03f);
        if((mask&2)!=0)Arc(at+P(.847f,.96125f),at+P(.672f,.815f),.10f,tick+23,Rear+.03f);
    }
    public static void Fountain(Vector3 at,float tick)
    {
        PlaygroundGround.Contact(at+P(0,-.53f),2.47f,.75f);Basin.Draw(at,Rear);
        Arc(at+P(-.006f,.49f),at+P(-.42f,.11f),.50f,tick,Rear+.03f);
        Arc(at+P(-.006f,.49f),at+P(.42f,.11f),.50f,tick+17,Rear+.031f);
        Rings(at+P(-.42f,.11f),tick,Rear+.035f);Rings(at+P(.42f,.11f),tick+37,Rear+.035f);
    }
    public static void Splash(Vector3 at,float tick,int mask)
    {
        Pad.Draw(at,Floor);
        if(mask==0)return;
        for(int n=0;n<4;n++)
        {
            float pulse=ExpansionMotion.SplashPulse(tick,n);if(pulse<=.18f)continue;
            var foot=at+ExpansionMotion.SplashNozzle(n);
            Arc(foot,foot+P(.07f,0),.30f+pulse*.80f,tick+n*17,Rear+.06f);
            Rings(foot,tick+n*17,Floor+.02f);
        }
    }
    static void Arc(Vector3 from,Vector3 to,float rise,float tick,float layer)
    {
        const int segments=12;var old=from;
        for(int i=1;i<=segments;i++)
        {
            float t=i/(float)segments;var p=Vector3.Lerp(from,to,t)+P(0,Mathf.Sin(t*Mathf.PI)*rise);
            Line(old,p,.028f,Stream,layer);Line(old+P(-.006f,0),p+P(-.006f,0),.008f,Glint,layer+.002f);old=p;
        }
        float phase=Mathf.Repeat(tick*.036f,1);
        var drop=Vector3.Lerp(from,to,phase)+P(0,Mathf.Sin(phase*Mathf.PI)*rise);
        Line(drop,drop+P(.006f,.033f),.025f,Glint,layer+.004f);
    }
    static void Rings(Vector3 at,float tick,float layer)
    {
        float r=.06f+Mathf.Repeat(tick*.015f,1)*.15f;
        for(int i=0;i<16;i++)
        {
            float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;
            Line(at+P(Mathf.Cos(a)*r,Mathf.Sin(a)*r*.52f),at+P(Mathf.Cos(b)*r,Mathf.Sin(b)*r*.52f),.012f,Ripple,layer);
        }
    }
    static void Line(Vector3 a,Vector3 b,float width,Material material,float layer)
    {
        var delta=b-a;var mid=(a+b)*.5f;mid.y=layer;
        Graphics.DrawMesh(MeshPool.plane10,Matrix4x4.TRS(mid,Quaternion.AngleAxis(delta.AngleFlat(),Vector3.up),new(width,1,delta.MagnitudeHorizontal())),material,0);
    }

    // Each link mask is one cached atlas mesh. Upright posts/pickets never rotate
    // with a north-south link; only the horizontal rail follows the ground axis.
    sealed class FenceMesh
    {
        readonly List<Vector3> vertices=new();readonly List<Vector2> uv=new();readonly List<int> triangles=new();
        public void Part(Rect source,Vector2 origin,float scale,Vector3 at,float angle=0)
        {
            var q=Quaternion.AngleAxis(angle,Vector3.up);int start=vertices.Count;float layer=start*.00004f;
            foreach(var p in SpritePart.Rect(source.x,source.y,source.width,source.height))
            {
                vertices.Add(at+q*P((p.x-origin.x)*scale,(origin.y-p.y)*scale)+Vector3.up*layer);
                uv.Add(new(p.x/1254,1-p.y/1254));
            }
            triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
        }
        public void Post(Vector3 at)=>Part(PostRect,new(751,1072),.0017f,at);
        public void Link(Vector3 at,Vector3 dir)
        {
            float rotation=Math.Abs(dir.x)>.5f?0:90;
            Part(RailRect,new(1066.5f,902),.0017f,at+dir*.25f+P(0,.44f),rotation);
            Part(RailRect,new(1066.5f,902),.0017f,at+dir*.25f+P(0,.10f),rotation);
            for(int i=1;i<=2;i++)Part(PicketRect,new(849.5f,1030),.00135f,at+dir*(i*.20f)+P(0,.10f));
        }
        public Mesh Finish(string name)
        {var m=new Mesh{name=name,vertices=vertices.ToArray(),uv=uv.ToArray(),triangles=triangles.ToArray()};m.RecalculateNormals();m.RecalculateBounds();return m;}
    }
    static Mesh FenceMeshFor(bool[] links)
    {
        int mask=0;for(int i=0;i<4;i++)if(links!=null&&links[i])mask|=1<<i;
        if(Fences[mask]==null)
        {
            var m=new FenceMesh();
            for(int n=0;n<4;n++)if((mask&(1<<n))!=0||(mask==0&&(n==1||n==3)))m.Link(Vector3.zero,Directions[n]);
            m.Post(Vector3.zero);Fences[mask]=m.Finish("LP fence "+mask);
        }
        return Fences[mask];
    }
    public static void Fence(Vector3 at,bool[] links)
    {
        var mesh=FenceMeshFor(links);
        PlaygroundGround.Contact(at,.20f,.13f);at.y=Rear+.02f;
        Graphics.DrawMesh(mesh,Matrix4x4.Translate(at),Boundary,0);
    }
    // A planned fence: the same joined mesh in the blueprint's own shader and colour, at the blueprint's altitude.
    public static void FencePlanned(Vector3 at,bool[] links,Shader shader,Color color)
        =>Graphics.DrawMesh(FenceMeshFor(links),Matrix4x4.Translate(at),MaterialPool.MatFrom(BoundaryTex,shader,color),0);
    static readonly Vector3 EntranceSign=P(1.0f,.66f);
    static Mesh EntranceMesh()
    {
        if(entrance!=null)return entrance;
        var m=new FenceMesh();
        for(int i=-1;i<=1;i+=2){var post=P(i*1.25f,0);m.Link(post,P(i,0));m.Post(post);}
        m.Part(SignRect,new(1128.5f,278.5f),.006f,EntranceSign,90);
        return entrance=m.Finish("LP open entrance");
    }
    public static void Entrance(Vector3 at)
    {
        PlaygroundGround.Contact(at+P(-1.25f,0),.20f,.13f);PlaygroundGround.Contact(at+P(1.25f,0),.20f,.13f);
        at.y=Rear+.02f;Graphics.DrawMesh(EntranceMesh(),Matrix4x4.Translate(at),Boundary,0);
        ExpansionArt.EntranceLetters(at+EntranceSign,Rear+.05f);
    }
    public static Texture2D Icon(string key)
    {
        if(Icons.TryGetValue(key,out var cached))return cached;
        string texture;RectInt rect;
        switch(key)
        {
            case "Playhouse":texture=HouseTex;rect=new(0,0,1254,1254);break;
            case "Drink":texture=WaterTex;rect=new(130,70,630,710);break;
            case "Fountain":texture=WaterTex;rect=new(920,190,830,590);break;
            case "SplashPad":texture=BoundaryTex;rect=new(60,615,555,527);break;
            case "Fence":texture=BoundaryTex;rect=new(80,150,560,355);break;
            case "Entrance":texture=BoundaryTex;rect=new(750,110,425,450);break;
            default:return null;
        }
        var src=ContentFinder<Texture2D>.Get(texture);
        bool water=texture==WaterTex;
        cached=PlaygroundPixels.Region(src,rect,water?1774:1254,water?887:1254,"LP amenities icon "+key);Icons[key]=cached;return cached;
    }
}
