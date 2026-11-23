using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

// Cached UV meshes sample the selected PNGs without changing the source bitmap.
public sealed class SpritePart
{
    readonly Mesh mesh;
    readonly Material material;
    readonly string texturePath;
    // mirror: drawn flipped left to right about the origin (a turned ride's west side, PlaygroundTurn); the triangles keep facing up.
    public SpritePart(string texture,int width,int height,float scale,Vector2 origin,Vector2[] polygon,Vector2[] source=null,Shader shader=null,Color? tint=null,bool mirror=false)
    {
        texturePath=texture;
        material=MaterialPool.MatFrom(texture,shader??ShaderDatabase.Cutout,tint??Color.white);
        var vertices=new Vector3[polygon.Length];var uv=new Vector2[polygon.Length];
        for(int i=0;i<polygon.Length;i++)
        {
            vertices[i]=new Vector3((polygon[i].x-origin.x)*scale*(mirror?-1:1),0,(origin.y-polygon[i].y)*scale);
            var p=(source??polygon)[i];uv[i]=new Vector2(p.x/width,1-p.y/height);
        }
        var triangles=new int[(polygon.Length-2)*3];
        for(int i=0;i<polygon.Length-2;i++){triangles[i*3]=0;triangles[i*3+1]=mirror?i+2:i+1;triangles[i*3+2]=mirror?i+1:i+2;}
        mesh=new Mesh{name="LittlePlaygrounds sprite region",vertices=vertices,uv=uv,triangles=triangles};
        mesh.RecalculateNormals();mesh.RecalculateBounds();
    }
    // The same shape in another material (a planned ride's ghost or blueprint, PlaygroundTurn).
    public void DrawWith(Vector3 origin,Material with,float angle=0,Vector3? scale=null)
        =>Graphics.DrawMesh(mesh,Matrix4x4.TRS(origin,Quaternion.AngleAxis(angle,Vector3.up),scale??Vector3.one),with,0);
    public string TexturePath=>texturePath;
    public static Vector2[] Rect(float x,float y,float w,float h)
        =>new[]{new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h)};
    public void Draw(Vector3 origin,float altitude,float angle=0,Vector3? scale=null,PlaygroundPalette palette=PlaygroundPalette.Legacy)
    {
        origin.y=altitude;
        Graphics.DrawMesh(mesh,Matrix4x4.TRS(origin,Quaternion.AngleAxis(angle,Vector3.up),scale??Vector3.one),
            palette==PlaygroundPalette.Legacy?material:PaletteArt.Material(texturePath,palette),0);
    }
}

[StaticConstructorOnStartup]
public static class SelectedSwingArt
{
    public const float Scale=.0037f;
    public static readonly Vector2 Origin=new(627,1110);
    public static Vector3 Point(float x,float y)=>new((x-Origin.x)*Scale,0,(Origin.y-y)*Scale);
    static SpritePart Part(float x,float y,float w,float h,Vector2? anchor=null)
        =>new("Things/Building/Selectedswing",1254,1254,Scale,anchor??Origin,SpritePart.Rect(x,y,w,h));
    static readonly SpritePart Top=Part(0,0,1254,156),Left=Part(0,156,285,1098),Right=Part(970,156,284,1098);
    static readonly SpritePart[] Seats={Part(290,930,245,142),Part(720,930,245,142)};
    static readonly SpritePart[] Lips={Part(290,1036,245,36),Part(720,1036,245,36)};
    static readonly float[] Chains={319,502,751,934};
    static readonly SpritePart[] Straps=MakeStraps();
    static SpritePart[] MakeStraps()
    {
        var result=new SpritePart[4];
        for(int i=0;i<4;i++)result[i]=Part(Chains[i]-24,156,48,774,new Vector2(Chains[i],156));
        return result;
    }
    public static Vector3 RestSeat(int slot)=>Point(slot==0?411:842,1024);
    public static Vector3 Seat(int slot,int age)=>RestSeat(slot)+SwingMotion.SeatOffset(age,slot)-SwingMotion.SeatOffset(-1,slot);
    public static void Draw(Building_Swing building,Vector3 at)
    {
        var palette=PaletteArt.Of(building);
        PlaygroundGround.Contact(at+Point(65,1160),.43f,.24f);
        PlaygroundGround.Contact(at+Point(1190,1160),.43f,.24f);
        PlaygroundGround.Contact(at+Point(242,930),.36f,.21f);
        PlaygroundGround.Contact(at+Point(1010,930),.36f,.21f);
        float rear=AltitudeLayer.Pawn.AltitudeFor()-.16f,front=AltitudeLayer.Pawn.AltitudeFor()+.3f;
        Top.Draw(at,rear,palette:palette);Left.Draw(at,rear,palette:palette);Right.Draw(at,rear,palette:palette);
        for(int slot=0;slot<2;slot++)
        {
            int age=building.JobAt(slot)?.Age??-1;var delta=Seat(slot,age)-RestSeat(slot);
            float stretch=1-delta.z/(774*Scale);
            Straps[slot*2].Draw(at+Point(Chains[slot*2],156),rear+.01f,scale:new Vector3(.68f,1,stretch));
            Straps[slot*2+1].Draw(at+Point(Chains[slot*2+1],156),rear+.01f,scale:new Vector3(.68f,1,stretch));
            var ground=at+RestSeat(slot);ground.z+=delta.z*.55f-.35f;
            PlaygroundGround.Contact(ground,.85f,.28f);
            Seats[slot].Draw(at+delta,rear+.02f,palette:palette);Lips[slot].Draw(at+delta,front,palette:palette);
        }
    }
}

[StaticConstructorOnStartup]
public static class SelectedSlideArt
{
    public const float Scale=.0036f;
    public static readonly Vector2 Origin=new(580,1040);
    public static Vector3 Point(float x,float y)=>new((x-Origin.x)*Scale,0,(Origin.y-y)*Scale);
    static SpritePart Part(Vector2[] polygon)=>new("Things/Building/SlidePolishV2",1165,1350,Scale,Origin,polygon);
    static readonly SpritePart Whole=Part(SpritePart.Rect(0,0,1165,1350));
    static readonly SpritePart StairRail=Part(new[]{new Vector2(108,1106),new Vector2(108,760),
        new Vector2(354,121),new Vector2(382,135),new Vector2(151,768),new Vector2(157,1106)});
    public static void Draw(Vector3 at,PlaygroundPalette palette=PlaygroundPalette.Legacy)
    {
        PlaygroundGround.Contact(at+Point(135,1080),.43f,.23f);
        PlaygroundGround.Contact(at+Point(390,1080),.43f,.23f);
        PlaygroundGround.Contact(at+Point(425,650),.43f,.23f);
        PlaygroundGround.Contact(at+Point(645,650),.43f,.23f);
        PlaygroundGround.Contact(at+Point(922,1285),1.12f,.27f);
        Whole.Draw(at,AltitudeLayer.Pawn.AltitudeFor()-.2f,palette:palette);StairRail.Draw(at,AltitudeLayer.Pawn.AltitudeFor()+.3f,palette:palette);
    }
}

[StaticConstructorOnStartup]
public static class SeesawArt
{
    public const float Scale=.00265f;
    public static readonly Vector2 Pivot=new(1086,418);
    public static Vector3 Point(float x,float y)=>new((x-Pivot.x)*Scale,0,(Pivot.y-y)*Scale);
    static SpritePart Part(Vector2[] polygon,Vector2[] source=null)
        =>new("Things/Building/Selectedseesaw",2172,724,Scale,Pivot,polygon,source);
    static readonly SpritePart Left=Part(SpritePart.Rect(0,0,976,449)),Right=Part(SpritePart.Rect(1197,0,975,449));
    static readonly SpritePart Center=Part(SpritePart.Rect(976,330,221,119),SpritePart.Rect(650,330,221,119));
    static readonly SpritePart Back=Part(SpritePart.Rect(977,260,219,74));
    static readonly SpritePart Front=Part(new[]{new Vector2(1086,354),new Vector2(1150,373),
        new Vector2(1200,446),new Vector2(1275,516),new Vector2(1308,568),new Vector2(1308,635),
        new Vector2(866,635),new Vector2(866,568),new Vector2(899,516),new Vector2(974,446),new Vector2(1022,373)});
    public static Vector3 Seat(int slot,float angle)=>Quaternion.AngleAxis(angle,Vector3.up)*Point(slot==0?260:1912,271);
    public static void Draw(Building_Seesaw b,Vector3 at)
    {
        var palette=PaletteArt.Of(b);
        PlaygroundGround.Contact(at+new Vector3(0,0,-.42f),1.28f,.38f);
        PlaygroundGround.Contact(at+new Vector3(-2.3f,0,-.12f),.92f,.24f);
        PlaygroundGround.Contact(at+new Vector3(2.3f,0,-.12f),.92f,.24f);
        float rear=AltitudeLayer.Pawn.AltitudeFor()-.16f;
        Back.Draw(at,rear,palette:palette);Center.Draw(at,rear+.01f,b.Angle,palette:palette);Left.Draw(at,rear+.02f,b.Angle,palette:palette);Right.Draw(at,rear+.02f,b.Angle,palette:palette);
        Front.Draw(at,AltitudeLayer.Pawn.AltitudeFor()+.3f,palette:palette);
    }
}
