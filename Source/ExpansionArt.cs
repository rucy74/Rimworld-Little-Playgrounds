using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

// All geometry stays in the native map plane. Pawn rendering is left to the
// activity animation; these layers only decide which rail sits in front of it.
[StaticConstructorOnStartup]
public static class ExpansionArt
{
    static Material Ink=Mat(.22f,.23f,.18f), Sage=Mat(.46f,.55f,.40f), SageLight=Mat(.66f,.73f,.55f),
        SageDark=Mat(.31f,.40f,.28f), Ivory=Mat(.94f,.90f,.77f), IvoryLight=Mat(1f,.97f,.87f),
        IvoryShade=Mat(.75f,.72f,.60f), Oak=Mat(.73f,.51f,.28f), OakLight=Mat(.89f,.69f,.40f),
        OakDark=Mat(.45f,.32f,.19f), Water=Mat(.43f,.72f,.73f,.70f), WaterLight=Mat(.80f,.95f,.94f,.78f),
        Wet=Mat(.35f,.62f,.64f,.18f), Apricot=Mat(.88f,.65f,.43f), Stone=Mat(.72f,.70f,.60f),
        Metal=Mat(.60f,.65f,.61f);
    static Material Mat(float r,float g,float b,float a=1)=>SolidColorMaterials.SimpleSolidColorMaterial(new Color(r,g,b,a));
    static readonly Mesh Disk=CircleMesh(48), Hex=CircleMesh(6), Ring=RingMesh(48,.89f);
    static readonly Mesh[] Numerals;
    static readonly Mesh Little,Playgrounds;
    static ExpansionArt(){Numerals=MakeNumerals();Little=Letters("LITTLE");Playgrounds=Letters("PLAYGROUNDS");}
    sealed class IconPrimitive
    {
        public Mesh mesh;public Vector3 at;public float width,depth,layer,angle;public Color color;public Material material;
    }
    sealed class StaticBatch { public Mesh mesh;public Material material;public float layer; }
    static List<IconPrimitive> iconCapture;
    static readonly Dictionary<ExpansionKind,Texture2D> Icons=new();
    static readonly Dictionary<int,List<StaticBatch>> StaticGeometry=new();
    // Transparent originals are copied unchanged into the mod. Regions/anchors
    // below map their pixels without resampling or overwriting the art.
    static readonly SpritePart FoxBody=new("Things/Building/ExpansionSpringFoxV1",1254,1254,.0017f,new Vector2(650,895),SpritePart.Rect(0,0,1254,895));
    static readonly SpritePart FoxBase=new("Things/Building/ExpansionSpringFoxV1",1254,1254,.0017f,new Vector2(650,680),SpritePart.Rect(0,891,1254,363));
    static readonly SpritePart FoxSeatLip=new("Things/Building/ExpansionSpringFoxV1",1254,1254,.0017f,new Vector2(650,895),
        new[]{new Vector2(514,660),new Vector2(805,660),new Vector2(797,691),new Vector2(775,706),new Vector2(551,708),new Vector2(527,698)});
    static readonly SpritePart House=Sprite("ExpansionPlayhouseV1",1254,1254,.0033f,627,650);
    static readonly SpritePart HouseCounterLip=new("Things/Building/ExpansionPlayhouseV1",1254,1254,.0033f,new Vector2(627,650),SpritePart.Rect(655,804,325,17));
    static readonly SpritePart Drinker=Sprite("ExpansionDrinkingFountainV1",1536,1024,.0021f,795,620);
    static readonly SpritePart Fountain=Sprite("ExpansionGardenFountainV1",1254,1254,.0026f,627,650);
    static SpritePart Sprite(string name,int w,int h,float scale,float x,float y)
        =>new("Things/Building/"+name,w,h,scale,new Vector2(x,y),SpritePart.Rect(0,0,w,h));
    static float Rear=>AltitudeLayer.Pawn.AltitudeFor()-.18f;
    static float Front=>AltitudeLayer.Pawn.AltitudeFor()+.29f;
    static float Floor=>AltitudeLayer.FloorEmplacement.AltitudeFor()+.04f;
    static Vector3 P(float x,float z)=>new(x,0,z);
    static Vector3 Turn(Vector3 p,float angle)=>Quaternion.AngleAxis(angle,Vector3.up)*p;
    static void Mesh(Mesh mesh,Vector3 at,float width,float depth,Material material,float y,float angle=0)
    {
        if(iconCapture!=null){iconCapture.Add(new IconPrimitive{mesh=mesh,at=at,width=width,depth=depth,layer=y,angle=angle,color=material.color,material=material});return;}
        at.y=y;Graphics.DrawMesh(mesh,Matrix4x4.TRS(at,Quaternion.AngleAxis(angle,Vector3.up),new Vector3(width,1,depth)),material,0);
    }
    static void Contact(Vector3 at,float width,float depth){if(iconCapture==null)PlaygroundGround.Contact(at,width,depth);}
    static void Box(Vector3 at,float width,float depth,Material m,float y,float angle=0)=>Mesh(MeshPool.plane10,at,width,depth,m,y,angle);
    static void Oval(Vector3 at,float width,float depth,Material m,float y)=>Mesh(Disk,at,width*.5f,depth*.5f,m,y);
    static void Line(Vector3 a,Vector3 b,float width,Material m,float y)
    {
        var d=b-a;Box((a+b)*.5f,width,d.MagnitudeHorizontal(),m,y,d.AngleFlat());
    }
    static void Rounded(Vector3 at,float w,float h,float radius,Material m,float y)
    {
        Box(at,w-radius*2,h,m,y);Box(at,w,h-radius*2,m,y);
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Oval(at+P(x*(w*.5f-radius),z*(h*.5f-radius)),radius*2,radius*2,m,y);
    }
    public static void Draw(ExpansionKind kind,Vector3 at,float motion,int activeMask,bool[] links)
    {
        if(kind==ExpansionKind.SpringRider){RestyleArt.Spring(at,motion);return;}
        if(kind==ExpansionKind.Roundabout){RestyleArt.Roundabout(at,motion);return;}
        if(kind==ExpansionKind.Hopscotch){RestyleArt.Hopscotch(at);return;}
        if(kind==ExpansionKind.BalanceLog){RestyleArt.Balance(at,false);return;}
        if(kind==ExpansionKind.Playhouse){AmenitiesArt.Playhouse(at,motion,activeMask);return;}
        if(kind==ExpansionKind.Drink){AmenitiesArt.Drink(at,motion,activeMask);return;}
        if(kind==ExpansionKind.Fountain){AmenitiesArt.Fountain(at,motion);return;}
        if(kind==ExpansionKind.SplashPad){AmenitiesArt.Splash(at,motion,activeMask);return;}
        if(kind==ExpansionKind.Fence){AmenitiesArt.Fence(at,links);return;}
        if(kind==ExpansionKind.Entrance){AmenitiesArt.Entrance(at);return;}
        if(iconCapture==null&&(kind is ExpansionKind.Hopscotch or ExpansionKind.Fence or ExpansionKind.Entrance or ExpansionKind.SteppingStone or ExpansionKind.BalanceLog))
        {
            DrawStatic(kind,at,links);return;
        }
        switch(kind)
        {
            case ExpansionKind.SpringRider: Spring(at,motion);break;
            case ExpansionKind.Roundabout: Roundabout(at,motion);break;
            case ExpansionKind.Playhouse: Playhouse(at,motion,activeMask);break;
            case ExpansionKind.Drink: Drink(at,motion,activeMask);break;
            case ExpansionKind.Hopscotch: Hopscotch(at);break;
            case ExpansionKind.SplashPad: Splash(at,motion,activeMask);break;
            case ExpansionKind.SteppingStone: Course(at,false,links);break;
            case ExpansionKind.BalanceLog: Course(at,true,links);break;
            case ExpansionKind.Fence: Fence(at,links);break;
            case ExpansionKind.Entrance: Entrance(at);break;
            case ExpansionKind.Fountain: GardenFountain(at,motion);break;
        }
    }
    static void DrawStatic(ExpansionKind kind,Vector3 at,bool[] links)
    {
        int mask=0;for(int i=0;i<4;i++)if(links!=null&&i<links.Length&&links[i])mask|=1<<i;
        int key=(int)kind*16+mask;
        if(!StaticGeometry.TryGetValue(key,out var batches))
        {
            var parts=new List<IconPrimitive>();iconCapture=parts;
            try{Draw(kind,Vector3.zero,0,0,links);}finally{iconCapture=null;}
            var groups=new Dictionary<(Material material,float layer),List<IconPrimitive>>();
            foreach(var part in parts){var groupKey=(part.material,part.layer);if(!groups.TryGetValue(groupKey,out var list))groups[groupKey]=list=new List<IconPrimitive>();list.Add(part);}
            batches=new List<StaticBatch>();
            foreach(var group in groups)
            {
                var vertices=new List<Vector3>();var triangles=new List<int>();
                foreach(var part in group.Value)
                {
                    int offset=vertices.Count;float angle=part.angle*Mathf.Deg2Rad,c=Mathf.Cos(angle),s=Mathf.Sin(angle);
                    foreach(var v in part.mesh.vertices)
                    {
                        float x=v.x*part.width,z=v.z*part.depth;
                        vertices.Add(new Vector3(part.at.x+x*c+z*s,0,part.at.z-x*s+z*c));
                    }
                    foreach(int index in part.mesh.triangles)triangles.Add(offset+index);
                }
                var mesh=new Mesh{name="LP static "+kind+" "+mask,vertices=vertices.ToArray(),triangles=triangles.ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();
                batches.Add(new StaticBatch{mesh=mesh,material=group.Key.material,layer=group.Key.layer});
            }
            batches.Sort((a,b)=>a.layer.CompareTo(b.layer));
            StaticGeometry[key]=batches;
        }
        if(kind==ExpansionKind.SteppingStone)PlaygroundGround.Contact(at,.75f,.61f);
        if(kind==ExpansionKind.Fence)PlaygroundGround.Contact(at,.22f,.15f);
        foreach(var batch in batches){at.y=batch.layer;Graphics.DrawMesh(batch.mesh,Matrix4x4.TRS(at,Quaternion.identity,Vector3.one),batch.material,0);}
    }
    static void Spring(Vector3 at,float degrees)
    {
        Contact(at+P(0,-.38f),.73f,.30f);
        FoxBase.Draw(at,Rear);FoxBody.Draw(at+P(0,-.3655f),Rear+.003f,degrees);
        FoxSeatLip.Draw(at+P(0,-.3655f),Front,degrees);
    }
    static void Roundabout(Vector3 at,float angle)
    {
        Contact(at+P(0,-.06f),2.60f,2.60f);
        Oval(at+P(0,-.055f),2.66f,2.66f,Ink,Floor+.01f);
        Oval(at,2.58f,2.58f,SageDark,Floor+.02f);Oval(at,2.46f,2.46f,Ivory,Floor+.03f);
        Oval(at,2.30f,2.30f,Oak,Floor+.04f);
        for(int n=0;n<8;n++)
        {
            var edge=Turn(P(0,1.13f),angle+n*45);Line(at,at+edge,.017f,OakDark,Floor+.05f);
            var inset=Turn(P(.025f,.75f),angle+n*45);Line(at+inset*.4f,at+inset,.013f,OakLight,Floor+.06f);
        }
        Oval(at,.32f,.32f,Ink,Floor+.07f);Oval(at,.25f,.25f,Ivory,Floor+.08f);
        for(int n=0;n<4;n++)
        {
            float a=angle+n*90;var seat=at+Turn(P(0,.85f),a);
            var footA=at+Turn(P(-.25f,1.04f),a);var footB=at+Turn(P(.25f,1.04f),a);
            // Posts stay screen-upright while their ground contacts rotate.
            var topA=footA+P(0,.34f);var topB=footB+P(0,.34f);
            float layer=(seat.z>=at.z?Rear+.04f:Front);
            Line(footA,topA,.09f,Ink,layer);Line(footB,topB,.09f,Ink,layer);
            Line(topA,topB,.10f,Ink,layer);Line(footA,topA,.052f,SageLight,layer+.002f);
            Line(footB,topB,.052f,SageLight,layer+.002f);Line(topA,topB,.064f,Sage,layer+.003f);
            Oval(footA,.12f,.08f,Ivory,layer+.004f);Oval(footB,.12f,.08f,Ivory,layer+.004f);
        }
    }
    static void Drink(Vector3 at,float tick,int mask)
    {
        Contact(at+P(0,-.3f),1.65f,.33f);Drinker.Draw(at,Rear);
        for(int s=0;s<2;s++)if((mask&(1<<s))!=0)
        {
            var nozzle=at+P(s==0?-.672f:.672f,s==0?.651f:1.05f);
            Jet(nozzle,s==0?.09f:-.09f,.16f,tick+s*23,Rear+.03f);
        }
    }
    static void Playhouse(Vector3 at,float tick,int mask)
    {
        House.Draw(at,Rear);HouseCounterLip.Draw(at,Front);
        if(mask==0)return;
        var counter=at+P(.60f,-.42f);
        float trade=(mask&3)==3?Mathf.Sin(tick*.025f):0;
        var toy=counter+P(trade*.12f,.035f*Mathf.Abs(trade));
        Rounded(toy,.18f,.14f,.025f,Ink,Front+.01f);Rounded(toy+P(-.01f,.012f),.14f,.10f,.017f,SageLight,Front+.012f);
        Oval(counter+P(.25f,.01f),.12f,.09f,OakDark,Front+.014f);Oval(counter+P(.25f,.023f),.09f,.06f,OakLight,Front+.016f);
    }
    public static void ShopToy(Vector3 counter,float tick,int mask,float layer)
    {
        float trade=(mask&3)==3?Mathf.Sin(tick*.025f):0;
        var toy=counter+P(trade*.12f,.035f*Mathf.Abs(trade));
        Rounded(toy,.16f,.12f,.025f,Ink,layer);Rounded(toy+P(-.01f,.012f),.12f,.08f,.017f,SageLight,layer+.002f);
        Oval(counter+P(.24f,.01f),.10f,.07f,OakDark,layer+.004f);Oval(counter+P(.24f,.023f),.07f,.04f,OakLight,layer+.006f);
    }
    public static void EntranceLetters(Vector3 sign,float layer)
    {
        Mesh(Little,sign+P(0,.10f),.43f,.08f,Ivory,layer);
        Mesh(Playgrounds,sign+P(0,-.035f),.76f,.09f,Ivory,layer+.002f);
    }
    static void Hopscotch(Vector3 at)
    {
        int number=1;
        for(int row=0;row<5;row++)
        {
            bool pair=row==1||row>=3;float z=(row-2)*.85f;
            for(int c=0;c<(pair?2:1);c++)
            {
                var pos=at+P(pair?(c==0?-.43f:.43f):0,z);
                Rounded(pos,.82f,.81f,.07f,IvoryShade,Floor);
                Rounded(pos,.77f,.76f,.055f,row%2==0?Ivory:SageLight,Floor+.002f);
                Rounded(pos,.67f,.66f,.035f,number%3==0?Apricot:row%2==0?IvoryLight:Sage,Floor+.003f);
                Mesh(Numerals[number],pos,.23f,.33f,Ink,Floor+.005f);number++;
            }
        }
        // Small start dots invite a clear entry; no visually distracting border.
        Oval(at+P(-.12f,-2.2f),.075f,.075f,Ivory,Floor+.006f);Oval(at+P(.12f,-2.2f),.075f,.075f,Ivory,Floor+.006f);
    }
    static void Splash(Vector3 at,float tick,int mask)
    {
        Rounded(at,2.85f,2.85f,.35f,IvoryShade,Floor);Rounded(at,2.77f,2.77f,.31f,Ivory,Floor+.002f);
        Rounded(at,2.62f,2.62f,.26f,SageLight,Floor+.003f);
        Oval(at,2.38f,2.38f,Ivory,Floor+.004f);Oval(at,2.24f,2.24f,Sage,Floor+.005f);
        Oval(at,.32f,.32f,Ink,Floor+.006f);Oval(at,.25f,.25f,Metal,Floor+.007f);
        for(int i=-1;i<=1;i++)Line(at+P(-.085f,i*.055f),at+P(.085f,i*.055f),.015f,Ink,Floor+.008f);
        for(int n=0;n<4;n++)
        {
            var p=at+P((n%2==0?-.7f:.7f),(n<2?-.7f:.7f));
            float pulse=ExpansionMotion.SplashPulse(tick,n);
            Oval(p,.25f,.25f,Ink,Floor+.01f);Oval(p,.19f,.19f,IvoryLight,Floor+.012f);Oval(p,.075f,.075f,SageDark,Floor+.014f);
            if(mask!=0&&pulse>.18f)
            {
                Oval(p,.54f+pulse*.12f,.35f,Wet,Floor+.016f);
                Jet(p,.03f,.20f+pulse*.74f,tick+n*17,Rear+.06f);
            }
        }
    }
    static void Jet(Vector3 p,float arc,float height,float tick,float layer)
    {
        const int count=9;Vector3 old=p;
        for(int i=1;i<=count;i++)
        {
            float u=(float)i/count;var q=p+P(arc*u,height*Mathf.Sin(u*Mathf.PI*.82f));
            Line(old,q,.038f,Water,layer);Line(old+P(-.009f,0),q+P(-.009f,0),.012f,WaterLight,layer+.002f);old=q;
        }
        float phase=(tick*.026f)%1;
        var drop=p+P(arc+.035f*Mathf.Sin(tick*.12f),height*.50f*(1-phase));
        Oval(drop,.038f,.055f,WaterLight,layer+.004f);
    }
    static void Course(Vector3 at,bool log,bool[] links)
    {
        if(log)
        {
            bool horizontal=links!=null&&((links.Length>1&&links[1])||(links.Length>3&&links[3]));
            float angle=horizontal?90:0;
            Mesh(MeshPool.plane10,at,.40f,.96f,Ink,Floor+.01f,angle);
            Mesh(MeshPool.plane10,at+P(0,.035f),.33f,.91f,Oak,Floor+.012f,angle);
            Mesh(MeshPool.plane10,at+P(0,.045f),.22f,.91f,OakLight,Floor+.014f,angle);
            for(int i=-1;i<=1;i+=2)
            {
                var end=at+Turn(P(0,i*.42f),angle);
                Oval(end,.32f,.21f,OakDark,Floor+.016f);Oval(end+P(0,.014f),.26f,.15f,OakLight,Floor+.018f);
                Mesh(Ring,end+P(0,.015f),.095f,.05f,Oak,Floor+.02f);
            }
        }
        else
        {
            Contact(at,.75f,.61f);
            Mesh(Hex,at+P(0,-.055f),.43f,.37f,Ink,Floor+.01f,14);
            Mesh(Hex,at,.41f,.35f,Stone,Floor+.012f,14);
            Mesh(Hex,at+P(-.025f,.025f),.36f,.29f,Ivory,Floor+.014f,14);
            Line(at+P(-.20f,.14f),at+P(-.05f,.20f),.024f,IvoryLight,Floor+.016f);
        }
    }
    static readonly Vector3[] Directions={new(0,0,1),new(1,0,0),new(0,0,-1),new(-1,0,0)};
    static void Fence(Vector3 at,bool[] links)
    {
        bool any=false;
        for(int n=0;n<4;n++)if(links!=null&&n<links.Length&&links[n])
        {
            any=true;var end=at+Directions[n]*.51f;var lift=P(0,.30f);
            Line(at+lift,end+lift,.105f,Ink,Rear);Line(at+lift,end+lift,.065f,Ivory,Rear+.002f);
            Line(at+P(0,.10f),end+P(0,.10f),.10f,Ink,Rear+.003f);Line(at+P(0,.10f),end+P(0,.10f),.06f,IvoryShade,Rear+.005f);
            for(int j=1;j<=2;j++)Picket(at+Directions[n]*(j*.20f),Rear+.008f);
        }
        if(!any){for(int n=-1;n<=1;n+=2){Line(at+P(0,.3f),at+P(n*.42f,.3f),.075f,Ivory,Rear);Picket(at+P(n*.3f,0),Rear+.005f);}}
        Post(at,Rear+.02f);
    }
    static void Picket(Vector3 at,float layer)
    {
        Rounded(at+P(0,.22f),.092f,.49f,.038f,Ink,layer);
        Rounded(at+P(-.006f,.23f),.059f,.45f,.026f,IvoryLight,layer+.002f);
    }
    static void Post(Vector3 at,float layer)
    {
        Contact(at,.22f,.15f);
        Rounded(at+P(0,.25f),.19f,.65f,.055f,Ink,layer);
        Rounded(at+P(-.012f,.26f),.14f,.60f,.04f,Sage,layer+.002f);
        Oval(at+P(-.007f,.54f),.15f,.10f,SageLight,layer+.004f);
        Box(at+P(0,.015f),.14f,.07f,IvoryShade,layer+.006f);
    }
    static void Entrance(Vector3 at)
    {
        Post(at+P(-1.25f,0),Rear);Post(at+P(1.25f,0),Rear);
        for(int n=-1;n<=1;n+=2)
        {
            Line(at+P(n*1.25f,.32f),at+P(n*1.50f,.32f),.07f,Ivory,Rear+.01f);
            Picket(at+P(n*1.43f,0),Rear+.02f);
        }
        var sign=at+P(.92f,.63f);
        Rounded(sign,1.10f,.50f,.10f,Ink,Rear+.03f);Rounded(sign+P(0,.012f),1.055f,.455f,.082f,OakLight,Rear+.032f);
        Mesh(Little,sign+P(0,.115f),.54f,.095f,OakDark,Rear+.035f);
        Mesh(Playgrounds,sign+P(0,-.045f),.93f,.115f,Ink,Rear+.036f);
        Oval(sign+P(-.45f,.14f),.037f,.037f,Ivory,Rear+.04f);Oval(sign+P(.45f,.14f),.037f,.037f,Ivory,Rear+.04f);
    }
    static void GardenFountain(Vector3 at,float tick)
    {
        Contact(at+P(0,-.40f),2.10f,.80f);Fountain.Draw(at,Rear);
        Jet(at+P(0,.333f),.08f,.43f,tick,Rear+.02f);
        float phase=(tick*.007f)%1;
        Mesh(Ring,at+P(.40f,-.32f),.12f+phase*.17f,.045f+phase*.065f,WaterLight,Rear+.03f);
        Mesh(Ring,at+P(-.40f,-.25f),.09f+phase*.14f,.04f+phase*.05f,WaterLight,Rear+.032f);
    }
    public static Texture2D Icon(ExpansionKind kind)
    {
        var amenities=AmenitiesArt.Icon(kind.ToString());if(amenities!=null)return amenities;
        var selected=RestyleArt.Icon(kind.ToString());if(selected!=null)return selected;
        if(Icons.TryGetValue(kind,out var found))return found;
        if(kind is ExpansionKind.SpringRider or ExpansionKind.Playhouse or ExpansionKind.Drink or ExpansionKind.Fountain)
            throw new ArgumentException("Bitmap facilities use their selected sprite for icons.",nameof(kind));
        var parts=new List<IconPrimitive>();iconCapture=parts;
        try{Draw(kind,Vector3.zero,0,0,new[]{false,true,false,true});}
        finally{iconCapture=null;}
        parts.Sort((a,b)=>a.layer.CompareTo(b.layer));
        var polygons=new List<(Vector2[] vertices,int[] triangles,Color color)>();
        float minX=float.MaxValue,maxX=float.MinValue,minY=float.MaxValue,maxY=float.MinValue;
        foreach(var part in parts)
        {
            var original=part.mesh.vertices;var points=new Vector2[original.Length];
            float a=part.angle*Mathf.Deg2Rad,c=Mathf.Cos(a),s=Mathf.Sin(a);
            for(int i=0;i<original.Length;i++)
            {
                float x=original[i].x*part.width,z=original[i].z*part.depth;
                var p=new Vector2(part.at.x+x*c+z*s,part.at.z-x*s+z*c);points[i]=p;
                minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);
            }
            polygons.Add((points,part.mesh.triangles,part.color));
        }
        float width=maxX-minX,height=maxY-minY,pad=Mathf.Max(width,height)*.035f;
        minX-=pad;minY-=pad;width+=pad*2;height+=pad*2;
        float scale=384/Mathf.Max(width,height);int w=Math.Max(8,Mathf.CeilToInt(width*scale)),h=Math.Max(8,Mathf.CeilToInt(height*scale));
        var pixels=new Color[w*h];
        foreach(var poly in polygons)
        {
            var v=poly.vertices;
            for(int n=0;n<poly.triangles.Length;n+=3)
            {
                var a=(v[poly.triangles[n]]-new Vector2(minX,minY))*scale;
                var b=(v[poly.triangles[n+1]]-new Vector2(minX,minY))*scale;
                var c=(v[poly.triangles[n+2]]-new Vector2(minX,minY))*scale;
                float denom=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);
                if(Mathf.Abs(denom)<.00001f)continue;
                int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))),0,w-1),x1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))),0,w-1);
                int y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))),0,h-1),y1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))),0,h-1);
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    float u=((b.y-c.y)*(x+.5f-c.x)+(c.x-b.x)*(y+.5f-c.y))/denom;
                    float vv=((c.y-a.y)*(x+.5f-c.x)+(a.x-c.x)*(y+.5f-c.y))/denom;
                    if(u<0||vv<0||u+vv>1)continue;
                    var src=poly.color;var dst=pixels[y*w+x];float alpha=src.a+dst.a*(1-src.a);
                    pixels[y*w+x]=alpha<=0?Color.clear:new Color((src.r*src.a+dst.r*dst.a*(1-src.a))/alpha,
                        (src.g*src.a+dst.g*dst.a*(1-src.a))/alpha,(src.b*src.a+dst.b*dst.a*(1-src.a))/alpha,alpha);
                }
            }
        }
        var texture=new Texture2D(w,h,TextureFormat.RGBA32,false){name="LP native geometry icon "+kind,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        texture.SetPixels(pixels);texture.Apply();Icons[kind]=texture;return texture;
    }
    static Mesh CircleMesh(int count)
    {
        var v=new Vector3[count+1];var t=new int[count*3];
        for(int i=0;i<count;i++){float a=-i*Mathf.PI*2/count;v[i+1]=P(Mathf.Cos(a),Mathf.Sin(a));t[i*3]=0;t[i*3+1]=i+1;t[i*3+2]=(i+1)%count+1;}
        var m=new Mesh{name="Playgrounds expansion disc",vertices=v,triangles=t};m.RecalculateNormals();m.RecalculateBounds();return m;
    }
    static Mesh RingMesh(int count,float inner)
    {
        var v=new Vector3[count*2];var t=new int[count*6];
        for(int i=0;i<count;i++){float a=-i*Mathf.PI*2/count;v[i*2]=P(Mathf.Cos(a),Mathf.Sin(a));v[i*2+1]=v[i*2]*inner;int j=(i+1)%count;t[i*6]=i*2;t[i*6+1]=j*2;t[i*6+2]=i*2+1;t[i*6+3]=j*2;t[i*6+4]=j*2+1;t[i*6+5]=i*2+1;}
        var m=new Mesh{name="Playgrounds expansion ring",vertices=v,triangles=t};m.RecalculateNormals();m.RecalculateBounds();return m;
    }
    static readonly Dictionary<char,string> Font=new()
    {
        ['1']="010/110/010/010/111",['2']="110/001/010/100/111",['3']="110/001/010/001/110",['4']="101/101/111/001/001",
        ['5']="111/100/110/001/110",['6']="011/100/111/101/111",['7']="111/001/010/010/010",['8']="111/101/111/101/111",
        ['L']="100/100/100/100/111",['I']="111/010/010/010/111",['T']="111/010/010/010/010",['E']="111/100/110/100/111",
        ['P']="110/101/110/100/100",['A']="010/101/111/101/101",['Y']="101/101/010/010/010",['G']="011/100/101/101/011",
        ['R']="110/101/110/101/101",['O']="010/101/101/101/010",['U']="101/101/101/101/111",['N']="101/111/111/111/101",
        ['D']="110/101/101/101/110",['S']="011/100/010/001/110"
    };
    static Mesh[] MakeNumerals(){var a=new Mesh[9];for(int i=1;i<=8;i++)a[i]=Letters(i.ToString());return a;}
    static Mesh Letters(string word)
    {
        // Each glyph is one cached mesh; no text objects or per-frame allocation.
        var vertices=new List<Vector3>();var triangles=new List<int>();float total=word.Length*4-1;
        for(int c=0;c<word.Length;c++)
        {
            var rows=Font[word[c]].Split('/');
            for(int y=0;y<5;y++)for(int x=0;x<3;x++)if(rows[y][x]=='1')
            {
                int n=vertices.Count;float left=(c*4+x)/total-.5f,right=(c*4+x+1)/total-.5f,top=.5f-y*.2f,bottom=top-.2f;
                vertices.Add(P(left,top));vertices.Add(P(right,top));vertices.Add(P(right,bottom));vertices.Add(P(left,bottom));
                triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
            }
        }
        var mesh=new Mesh{name="Playgrounds lettering "+word,vertices=vertices.ToArray(),triangles=triangles.ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
}

// UI icons, construction ghosts and blueprints use exactly the same procedural
// geometry as the finished facility, rasterized once rather than a generic icon.
public sealed class Graphic_PlaygroundExpansion:Graphic_Single
{
    public override void Init(GraphicRequest req)
    {
        data=req.graphicData;path=req.path;maskPath=req.maskPath;color=req.color;colorTwo=req.colorTwo;drawSize=req.drawSize;
        string key=path.Substring(path.LastIndexOf('/')+1);
        var selected=RestyleArt.Icon(key);
        if(selected==null&&!Enum.TryParse<ExpansionKind>(key,out _))throw new ArgumentException("Unknown playground icon "+path);
        var request=new MaterialRequest(selected??ExpansionArt.Icon((ExpansionKind)Enum.Parse(typeof(ExpansionKind),key)),req.shader,color)
            {colorTwo=colorTwo,renderQueue=req.renderQueue,shaderParameters=req.shaderParameters};
        mat=MaterialPool.MatFrom(request);
    }
    public override Graphic GetColoredVersion(Shader shader,Color tint,Color tintTwo)
        =>GraphicDatabase.Get<Graphic_PlaygroundExpansion>(path,shader,drawSize,tint,tintTwo,data);
    // A planned fence (blueprint) draws the joined fence, as vanilla's linked fence blueprints do; the mouse ghost keeps
    // the menu picture, as vanilla's linked buildings' ghosts do (GhostUtility.GhostGraphicFor).
    static readonly bool[] planned=new bool[4];
    public override void DrawWorker(Vector3 loc,Rot4 rot,ThingDef thingDef,Thing thing,float extraRotation)
    {
        if(thing is RimWorld.Blueprint{Spawned:true}&&thing.def.entityDefToBuild is ThingDef built&&built.GetModExtension<ExpansionProperties>()?.kind==ExpansionKind.Fence)
        {AmenitiesArt.FencePlanned(loc,Building_PlaygroundFacility.FenceLinks(thing.Map,thing.Position,planned),Shader,color);return;}
        if(TurnGhosts.Draw(loc,rot,thingDef,Shader,color))return;
        base.DrawWorker(loc,PlaygroundTurn.PictureRot(thingDef,rot),thingDef,thing,extraRotation);
    }
}
