using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

// All four pieces of the zipline and the trampoline use the unmodified selected
// PNGs. The same primitives are composited for native UI icons and build ghosts.
[StaticConstructorOnStartup]
public static class ActiveRideArt
{
    const string TrampolineTexture="Things/Building/TrampolineV1",ZipTexture="Things/Building/ZiplineAtlasV1";
    const float TrampolineScale=.00285f,TowerScale=2.9f/910f;
    static float Floor=>AltitudeLayer.FloorEmplacement.AltitudeFor()+.045f;
    static float Rear=>AltitudeLayer.Pawn.AltitudeFor()-.18f;
    static float Front=>AltitudeLayer.Pawn.AltitudeFor()+.29f;
    static Vector3 P(float x,float z)=>new(x,0,z);
    static readonly Material CableInk=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.19f,.21f,.17f));
    static readonly Material CableLight=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.57f,.61f,.49f));
    static readonly Material RopeInk=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.24f,.29f,.20f));
    static readonly Material RopeLight=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.60f,.66f,.48f));
    static readonly Material Shadow=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.08f,.09f,.07f,.14f));
    static readonly Shape LeftTower=Part(ZipTexture,new Rect(90,70,280,1040),new Vector2(322,150),TowerScale);
    static readonly Shape RightTower=Part(ZipTexture,new Rect(885,70,280,1040),new Vector2(933,150),TowerScale);
    static readonly Shape Trolley=Part(ZipTexture,new Rect(525,490,200,130),new Vector2(627,535),.0028f);
    static readonly Shape Seat=Part(ZipTexture,new Rect(480,1010,295,170),new Vector2(627,1100),.0034f);
    static readonly Shape SeatLip=Region(ZipTexture,new[]{new Vector2(501,1127),new Vector2(752,1127),
        new Vector2(741,1148),new Vector2(716,1160),new Vector2(542,1160),new Vector2(515,1148)},new Vector2(627,1100),.0034f);
    static readonly Shape Cable=MakeCable(.046f,CableInk,0),CableGlint=MakeCable(.014f,CableLight,.010f);
    static readonly Shape[] TrampolineFrames=MakeTrampolineFrames();
    static readonly Shape[] TimberFrames=MakeTrampolineFrames("Things/Building/Timber/trampoline");
    static readonly Shape GroundShadow=MakeShadow();
    static readonly Dictionary<(ActiveRideKind,PlaygroundPalette),Texture2D> Icons=new();
    static List<Paint> capture;

    sealed class Shape
    {
        public Mesh mesh;public Material material;public Texture2D texture;public string texturePath;
        public Vector3[] vertices;public Vector2[] uv;public int[] triangles;
        public void Draw(Vector3 at,float layer,Vector3? scale=null,float angle=0,PlaygroundPalette palette=PlaygroundPalette.Legacy)
        {
            var size=scale??Vector3.one;
            if(capture!=null){capture.Add(new Paint{shape=this,at=at,layer=layer,scale=size,angle=angle,palette=palette});return;}
            at.y=layer;Graphics.DrawMesh(mesh,Matrix4x4.TRS(at,Quaternion.AngleAxis(angle,Vector3.up),size),
                texturePath!=null?PaletteArt.Material(texturePath,palette):material,0);
        }
    }
    sealed class Paint { public Shape shape;public Vector3 at,scale;public float layer,angle;public PlaygroundPalette palette; }
    static Shape MakeShape(string name,Vector3[] vertices,Vector2[] uv,int[] triangles,Material material,Texture2D texture=null)
    {
        var mesh=new Mesh{name=name,vertices=vertices,triangles=triangles};
        if(uv!=null)mesh.uv=uv;
        mesh.RecalculateNormals();mesh.RecalculateBounds();
        return new Shape{mesh=mesh,material=material,texture=texture,vertices=vertices,uv=uv,triangles=triangles};
    }
    static Shape Part(string texture,Rect rect,Vector2 anchor,float scale)
        =>Region(texture,SpritePart.Rect(rect.x,rect.y,rect.width,rect.height),anchor,scale);
    static Shape Region(string texture,Vector2[] pixels,Vector2 anchor,float scale)
    {
        var vertices=new Vector3[pixels.Length];var uv=new Vector2[pixels.Length];var triangles=new int[(pixels.Length-2)*3];
        for(int i=0;i<pixels.Length;i++){vertices[i]=P((pixels[i].x-anchor.x)*scale,(anchor.y-pixels[i].y)*scale);uv[i]=new Vector2(pixels[i].x/1254,1-pixels[i].y/1254);}
        for(int i=0;i<pixels.Length-2;i++){triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=i+2;}
        var shape=MakeShape("LP active ride sprite "+texture,vertices,uv,triangles,MaterialPool.MatFrom(texture,ShaderDatabase.Cutout),ContentFinder<Texture2D>.Get(texture));
        shape.texturePath=texture;return shape;
    }

    public static void Draw(Building_ActiveRide building,Vector3 at)
    {
        int age=building.RiderJob?.Age??-1;
        DrawGeometry(building.Kind,at,age,building.CarProgress,PaletteArt.Of(building),building.CarSway,DesignArt.Timber(building));
    }
    static void DrawGeometry(ActiveRideKind kind,Vector3 at,int age,float progress,PlaygroundPalette palette,float sway=0,bool timber=false)
    {
        if(kind==ActiveRideKind.Trampoline){Trampoline(at,age,palette,timber);return;}
        if(kind==ActiveRideKind.SharedTrampoline){SharedGeometry(at,palette,0,timber);return;}
        Zipline(at,progress,palette,sway);
    }
    public static void DrawShared(Building_PlaygroundFacility building,Vector3 at)
    {
        float compression=0;
        for(int i=0;i<building.Capacity;i++)if(building.VisitAt(i) is {Age:>=0} d)
            compression=Mathf.Max(compression,-SharedTrampolineMotion.Height(d.Age,i,d.MotionSeed));
        SharedGeometry(at,PaletteArt.Of(building),compression,DesignArt.Timber(building));
        for(int i=0;i<building.Capacity;i++)if(building.VisitAt(i) is {Age:>=60} d&&d.Age<d.Duration-60)
        {
            float height=Mathf.Max(0,SharedTrampolineMotion.Height(d.Age,i,d.MotionSeed));
            float width=Mathf.Lerp(.48f,.28f,height/1.25f);
            GroundShadow.Draw(at+d.Contact+P(0,ActiveRideMotion.TrampolineLift),Floor+.008f,new Vector3(width,1,width*.52f));
        }
    }
    static void SharedGeometry(Vector3 at,PlaygroundPalette palette,float compression,bool timber=false)
    {
        const float size=1.75f;
        for(int side=-1;side<=1;side+=2)
        {
            var left=at+P(side*1.04f*size-.25f,-.48f*size);var right=at+P(side*1.04f*size+.25f,-.48f*size);
            Support(left,left+P(0,-.53f),timber);Support(right,right+P(0,-.53f),timber);Support(left+P(0,-.53f),right+P(0,-.53f),timber);
        }
        StepOutline.Draw(at+P(0,-1.11f*size),Floor-.006f,new Vector3(1.10f,1,.34f));
        StepSide.Draw(at+P(0,-1.135f*size),Floor-.004f,new Vector3(1.02f,1,.23f));
        StepWood.Draw(at+P(0,-1.075f*size),Floor-.002f,new Vector3(1.02f,1,.17f));
        int frame=Mathf.Clamp(Mathf.RoundToInt(compression*200/size),0,20);
        (timber?TimberFrames:TrampolineFrames)[frame].Draw(at+P(0,ActiveRideMotion.TrampolineLift),Floor,new Vector3(size,1,size),palette:palette);
    }
    static void Trampoline(Vector3 at,int age,PlaygroundPalette palette,bool timber=false)
    {
        // Low U supports and a step visibly separate the rim from the ground.
        for(int side=-1;side<=1;side+=2)
        {
            var left=at+P(side*1.04f-.18f,-.48f);var right=at+P(side*1.04f+.18f,-.48f);
            Support(left,left+P(0,-.53f),timber);Support(right,right+P(0,-.53f),timber);
            Support(left+P(0,-.53f),right+P(0,-.53f),timber);
        }
        StepOutline.Draw(at+P(0,-1.11f),Floor-.006f,new Vector3(.63f,1,.29f));
        StepSide.Draw(at+P(0,-1.135f),Floor-.004f,new Vector3(.57f,1,.19f));
        StepWood.Draw(at+P(0,-1.075f),Floor-.002f,new Vector3(.57f,1,.14f));
        at+=P(0,ActiveRideMotion.TrampolineLift);
        // One grid carries the whole selected sprite. Its padded rim never moves;
        // only vertices within the dark bed receive a smooth, radial depression.
        int frame=Mathf.Clamp(Mathf.RoundToInt(ActiveRideMotion.BedCompression(age)*200),0,20);
        (timber?TimberFrames:TrampolineFrames)[frame].Draw(at,Floor,palette:palette);
        if(age>=0&&capture==null)
        {
            float height=Mathf.Max(0,ActiveRideMotion.BounceHeight(age));
            float width=Mathf.Lerp(.48f,.27f,height/1.25f),depth=width*.52f;
            GroundShadow.Draw(at+P(0,-.08f),Floor+.008f,new Vector3(width,1,depth));
        }
    }
    static Shape[] MakeTrampolineFrames(string texturePath=TrampolineTexture)
    {
        const int grid=36;var result=new Shape[21];var material=MaterialPool.MatFrom(texturePath,ShaderDatabase.Cutout);
        var texture=ContentFinder<Texture2D>.Get(texturePath);
        for(int frame=0;frame<result.Length;frame++)
        {
            var vertices=new Vector3[(grid+1)*(grid+1)];var uv=new Vector2[vertices.Length];var triangles=new int[grid*grid*6];
            for(int y=0;y<=grid;y++)for(int x=0;x<=grid;x++)
            {
                float px=x*1254f/grid,py=y*1254f/grid;
                float dx=(px-628)/355f,dz=(py-617)/279f;
                float inward=Mathf.Clamp01(1-dx*dx-dz*dz);
                float compression=frame*.005f*inward*inward;
                int i=y*(grid+1)+x;
                vertices[i]=P((px-627)*TrampolineScale,(627-py)*TrampolineScale-compression);
                uv[i]=new Vector2(px/1254,1-py/1254);
                if(x==grid||y==grid)continue;
                int t=(y*grid+x)*6;
                triangles[t]=i;triangles[t+1]=i+1;triangles[t+2]=i+grid+2;
                triangles[t+3]=i;triangles[t+4]=i+grid+2;triangles[t+5]=i+grid+1;
            }
            result[frame]=MakeShape("LP trampoline bed "+frame,vertices,uv,triangles,material,texture);
            result[frame].texturePath=texturePath;
        }
        return result;
    }
    static Shape MakeShadow()
    {
        const int sides=32;var v=new Vector3[sides+1];var t=new int[sides*3];
        for(int i=0;i<sides;i++){float a=-i*Mathf.PI*2/sides;v[i+1]=P(Mathf.Cos(a)*.5f,Mathf.Sin(a)*.5f);t[i*3]=0;t[i*3+1]=i+1;t[i*3+2]=(i+1)%sides+1;}
        return MakeShape("LP trampoline contact shadow",v,null,t,Shadow);
    }
    static Shape MakeCable(float width,Material material,float rise)
    {
        const int segments=40;var vertices=new Vector3[(segments+1)*2];var triangles=new int[segments*6];
        for(int i=0;i<=segments;i++)
        {
            float t=i/(float)segments;float x=Mathf.Lerp(-ActiveRideMotion.ZipHalfSpan,ActiveRideMotion.ZipHalfSpan,t);
            float height=ActiveRideMotion.CableHeight(t)+rise;
            vertices[i*2]=P(x,height+width*.5f);vertices[i*2+1]=P(x,height-width*.5f);
            if(i==segments)continue;
            int k=i*6,n=i*2;triangles[k]=n;triangles[k+1]=n+2;triangles[k+2]=n+1;
            triangles[k+3]=n+1;triangles[k+4]=n+2;triangles[k+5]=n+3;
        }
        return MakeShape("LP zipline curved cable",vertices,null,triangles,material);
    }
    static void Zipline(Vector3 at,float progress,PlaygroundPalette palette,float sway)
    {
        if(capture==null)
        {
            PlaygroundGround.Contact(at+P(-3.616f,0),.40f,.22f);
            PlaygroundGround.Contact(at+P(3.60f,0),.40f,.22f);
        }
        LeftTower.Draw(at+P(-ActiveRideMotion.ZipHalfSpan,2.9f),Rear,palette:palette);
        RightTower.Draw(at+P(ActiveRideMotion.ZipHalfSpan,2.9f),Rear,palette:palette);
        Cable.Draw(at,Rear+.006f);CableGlint.Draw(at,Rear+.008f);
        var trolley=at+ActiveRideMotion.ZipTrolley(progress);var seat=at+ActiveRideMotion.ZipSeat(progress,sway);
        for(int side=-1;side<=1;side+=2)
        {
            var top=trolley+P(side*.09f,-.16f);var bottom=seat+Quaternion.AngleAxis(-sway,Vector3.up)*P(side*.383f,.115f);
            Stroke(top,bottom,.040f,RopeInk,Rear+.010f);
            Stroke(top+P(-.006f,.004f),bottom+P(-.006f,.004f),.014f,RopeLight,Rear+.012f);
        }
        Trolley.Draw(trolley,Rear+.016f,palette:palette);Seat.Draw(seat,Rear+.018f,angle:-sway,palette:palette);SeatLip.Draw(seat,Front,angle:-sway,palette:palette);
    }
    static readonly Shape StrokeMesh=MakeShape("LP active ride line",new[]{P(-.5f,.5f),P(.5f,.5f),P(.5f,-.5f),P(-.5f,-.5f)},null,new[]{0,1,2,0,2,3},RopeInk);
    static readonly Shape StrokeHighlightMesh=MakeShape("LP active ride line highlight",new[]{P(-.5f,.5f),P(.5f,.5f),P(.5f,-.5f),P(-.5f,-.5f)},null,new[]{0,1,2,0,2,3},RopeLight);
    static readonly Shape SupportInk=Block("support outline",new Color(.16f,.15f,.13f));
    static readonly Shape TimberSupportLight=Block("timber support inner",new Color(.64f,.43f,.22f));
    static readonly Shape SupportLight=Block("support inner",new Color(.50f,.49f,.43f));
    static readonly Shape StepOutline=Block("step outline",new Color(.17f,.14f,.10f));
    static readonly Shape StepSide=Block("step front",new Color(.55f,.39f,.22f));
    static readonly Shape StepWood=MakeShape("LP trampoline wooden step",new[]{P(-.5f,.5f),P(.5f,.5f),P(.5f,-.5f),P(-.5f,-.5f)},null,new[]{0,1,2,0,2,3},SolidColorMaterials.SimpleSolidColorMaterial(PaletteArt.Wood));
    static Shape Block(string name,Color color)=>MakeShape("LP trampoline "+name,new[]{P(-.5f,.5f),P(.5f,.5f),P(.5f,-.5f),P(-.5f,-.5f)},null,new[]{0,1,2,0,2,3},SolidColorMaterials.SimpleSolidColorMaterial(color));
    static void Support(Vector3 from,Vector3 to,bool timber=false)
    {
        var delta=to-from;var mid=(from+to)*.5f;
        SupportInk.Draw(mid,Floor-.012f,new Vector3(.11f,1,delta.MagnitudeHorizontal()+.06f),delta.AngleFlat());
        (timber?TimberSupportLight:SupportLight).Draw(mid+P(-.012f,.008f),Floor-.010f,new Vector3(.055f,1,delta.MagnitudeHorizontal()+.015f),delta.AngleFlat());
    }
    static void Stroke(Vector3 from,Vector3 to,float width,Material material,float layer)
    {
        var delta=to-from;var shape=material==RopeInk?StrokeMesh:StrokeHighlightMesh;
        shape.Draw((from+to)*.5f,layer,new Vector3(width,1,delta.MagnitudeHorizontal()),delta.AngleFlat());
    }

    // Fixed world-space canvases make the icon, blueprint and built geometry agree
    // exactly. The zipline canvas includes the raised cable above its ground feet.
    public static Rect IconWorldRect(ActiveRideKind kind)=>kind switch
    {ActiveRideKind.Trampoline=>new Rect(-1.78695f,-1.78695f,3.5739f,3.5739f),
     ActiveRideKind.SharedTrampoline=>new Rect(-3.1272f,-3.1272f,6.2544f,6.2544f),
     _=>new Rect(-4.10f,-.25f,8.20f,3.55f)};
    public static Texture2D Icon(ActiveRideKind kind,PlaygroundPalette palette=PlaygroundPalette.Blue)
    {
        if(Icons.TryGetValue((kind,palette),out var existing))return existing;
        var paints=new List<Paint>();capture=paints;
        try{DrawGeometry(kind,Vector3.zero,-1,0,palette);}finally{capture=null;}
        // Stable layer order preserves primitive order when layers are equal.
        for(int i=1;i<paints.Count;i++){var p=paints[i];int j=i-1;while(j>=0&&paints[j].layer>p.layer){paints[j+1]=paints[j];j--;}paints[j+1]=p;}
        var rect=IconWorldRect(kind);float pixelsPerWorld=768/Mathf.Max(rect.width,rect.height);
        int width=Mathf.CeilToInt(rect.width*pixelsPerWorld),height=Mathf.CeilToInt(rect.height*pixelsPerWorld);
        var colors=new Color[width*height];var sourcePixels=new Dictionary<Texture2D,Color[]>();
        foreach(var p in paints)
        {
            var shape=p.shape;Color[] source=null;
            var painted=shape.texturePath!=null?PaletteArt.Texture(shape.texturePath,p.palette):shape.texture;
            if(painted!=null&&!sourcePixels.TryGetValue(painted,out source))sourcePixels[painted]=source=PlaygroundPixels.Read(painted);
            var points=new Vector2[shape.vertices.Length];float angle=p.angle*Mathf.Deg2Rad,c=Mathf.Cos(angle),s=Mathf.Sin(angle);
            for(int i=0;i<points.Length;i++)
            {
                float x=shape.vertices[i].x*p.scale.x,z=shape.vertices[i].z*p.scale.z;
                points[i]=new Vector2((p.at.x+x*c+z*s-rect.x)*pixelsPerWorld,(p.at.z-x*s+z*c-rect.y)*pixelsPerWorld);
            }
            for(int n=0;n<shape.triangles.Length;n+=3)
            {
                int ai=shape.triangles[n],bi=shape.triangles[n+1],ci=shape.triangles[n+2];
                var a=points[ai];var b=points[bi];var cc=points[ci];
                float denominator=(b.y-cc.y)*(a.x-cc.x)+(cc.x-b.x)*(a.y-cc.y);if(Mathf.Abs(denominator)<.00001f)continue;
                int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,cc.x))),0,width-1),x1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,cc.x))),0,width-1);
                int y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,cc.y))),0,height-1),y1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,cc.y))),0,height-1);
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    float u=((b.y-cc.y)*(x+.5f-cc.x)+(cc.x-b.x)*(y+.5f-cc.y))/denominator;
                    float v=((cc.y-a.y)*(x+.5f-cc.x)+(a.x-cc.x)*(y+.5f-cc.y))/denominator;
                    if(u<0||v<0||u+v>1)continue;
                    Color color=shape.material.color;
                    if(source!=null)
                    {
                        var uv=shape.uv[ai]*u+shape.uv[bi]*v+shape.uv[ci]*(1-u-v);
                        int sx=Mathf.Clamp(Mathf.FloorToInt(uv.x*shape.texture.width),0,shape.texture.width-1),sy=Mathf.Clamp(Mathf.FloorToInt(uv.y*shape.texture.height),0,shape.texture.height-1);
                        color*=source[sy*shape.texture.width+sx];
                    }
                    if(color.a<.004f)continue;
                    int i=y*width+x;var dst=colors[i];float alpha=color.a+dst.a*(1-color.a);
                    colors[i]=new Color((color.r*color.a+dst.r*dst.a*(1-color.a))/alpha,(color.g*color.a+dst.g*dst.a*(1-color.a))/alpha,
                        (color.b*color.a+dst.b*dst.a*(1-color.a))/alpha,alpha);
                }
            }
        }
        var result=new Texture2D(width,height,TextureFormat.RGBA32,false){name="LP assembled active ride icon "+kind,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        result.SetPixels(colors);result.Apply();Icons[(kind,palette)]=result;return result;
    }
}

public sealed class Graphic_ActiveRide:Graphic_Single
{
    public override void Init(GraphicRequest req)
    {
        data=req.graphicData;path=req.path;maskPath=req.maskPath;color=req.color;colorTwo=req.colorTwo;drawSize=req.drawSize;
        if(!Enum.TryParse<ActiveRideKind>(path.Substring(path.LastIndexOf('/')+1),out var kind))throw new ArgumentException("Unknown active ride graphic: "+path);
        mat=MaterialPool.MatFrom(new MaterialRequest(ActiveRideArt.Icon(kind),req.shader,color)
            {colorTwo=colorTwo,renderQueue=req.renderQueue,shaderParameters=req.shaderParameters});
    }
    public override Graphic GetColoredVersion(Shader shader,Color tint,Color tintTwo)
        =>GraphicDatabase.Get<Graphic_ActiveRide>(path,shader,drawSize,tint,tintTwo,data);
}
