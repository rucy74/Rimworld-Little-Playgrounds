using System;
using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

// Optional per-building geometry; never patches or replaces another building.
[StaticConstructorOnStartup]
public static class TimberArt
{
    const string Root="Things/Building/Timber/";
    static float Rear=>AltitudeLayer.Pawn.AltitudeFor()-.18f;
    static float Front=>AltitudeLayer.Pawn.AltitudeFor()+.30f;
    static float Floor=>AltitudeLayer.FloorEmplacement.AltitudeFor()+.04f;
    static Vector3 P(float x,float z)=>new(x,0,z);
    static Vector3 Rot(Vector3 p,float angle)=>Quaternion.AngleAxis(angle,Vector3.up)*p;
    static SpritePart Part(string name,int w,int h,float scale,Vector2 anchor,Vector2[] shape,Vector2[] uv=null)
        =>new(Root+name,w,h,scale,anchor,shape,uv);
    static SpritePart Box(string n,int w,int h,float s,Vector2 o,float x,float y,float rw,float rh)
        =>Part(n,w,h,s,o,SpritePart.Rect(x,y,rw,rh));
    const float SS=.0037f;
    static readonly Vector2 SO=new(627,1110);
    static Vector3 SP(float x,float y)=>P((x-627)*SS,(1110-y)*SS);
    static readonly SpritePart SwingTop=Box("swing",1254,1254,SS,SO,0,0,1254,179);
    static readonly SpritePart SwingLeft=Box("swing",1254,1254,SS,SO,0,179,350,1075),SwingRight=Box("swing",1254,1254,SS,SO,910,179,344,1075);
    static readonly SpritePart[] SwingSeats={Box("swing",1254,1254,SS,SO,356,963,225,68),Box("swing",1254,1254,SS,SO,673,963,225,68)};
    static readonly SpritePart[] SwingLips={Box("swing",1254,1254,SS,SO,356,1000,225,31),Box("swing",1254,1254,SS,SO,673,1000,225,31)};
    static readonly float[] SX={393,547,708,862};
    static readonly SpritePart[] Ropes=MakeRopes();
    static SpritePart[] MakeRopes(){var parts=new SpritePart[4];for(int i=0;i<4;i++)parts[i]=Box("swing",1254,1254,SS,new(SX[i],179),SX[i]-12,179,24,806);return parts;}
    static Vector3 SRest(int slot)=>SP(slot==0?469:785,990);
    static Vector3 SSeat(int slot,int age)=>SRest(slot)+SwingMotion.SeatOffset(age,slot)-SwingMotion.SeatOffset(-1,slot);
    public static Vector3 SwingSeat(int slot,int age)=>SSeat(slot,age);
    public static void SwingDraw(Building_Swing building,Vector3 at,PlaygroundPalette palette)
    {
        SwingTop.Draw(at,Rear,palette:palette);SwingLeft.Draw(at,Rear,palette:palette);SwingRight.Draw(at,Rear,palette:palette);
        for(int i=0;i<2;i++)
        {
            var delta=SSeat(i,building.JobAt(i)?.Age??-1)-SRest(i);float stretch=1-delta.z/(806*SS);
            for(int n=i*2;n<i*2+2;n++)Ropes[n].Draw(at+SP(SX[n],179),Rear+.01f,scale:new(1,1,stretch),palette:palette);
            SwingSeats[i].Draw(at+delta,Rear+.02f,palette:palette);SwingLips[i].Draw(at+delta,Front,palette:palette);
        }
        
    }

    const float QS=.00285f;
    static readonly Vector2 QO=new(971,390);
    static Vector3 QP(float x,float y)=>P((x-971)*QS,(390-y)*QS);
    static readonly SpritePart QLeft=Box("seesaw",1942,809,QS,QO,0,220,920,185),QRight=Box("seesaw",1942,809,QS,QO,1022,220,920,185);
    static readonly SpritePart QMid=Part("seesaw",1942,809,QS,QO,SpritePart.Rect(920,290,102,115),SpritePart.Rect(710,290,102,115));
    static readonly SpritePart QFront=Part("seesaw",1942,809,QS,QO,new[]{new Vector2(937,337),new Vector2(1005,337),new Vector2(1138,613),new Vector2(805,613)});
    public static Vector3 SeesawSeat(int slot,float angle)=>Rot(QP(slot==0?171:1771,340),angle);
    public static void SeesawDraw(Building_Seesaw b,Vector3 at,PlaygroundPalette palette)
    {QMid.Draw(at,Rear,b.Angle,palette:palette);QLeft.Draw(at,Rear+.002f,b.Angle,palette:palette);QRight.Draw(at,Rear+.002f,b.Angle,palette:palette);QFront.Draw(at,Front,palette:palette);}

    const float LS=.0036f;
    static readonly Vector2 LO=new(580,1040);
    static Vector3 LP(float x,float y)=>P((x-580)*LS,(1040-y)*LS);
    static readonly SpritePart SlideWhole=Box("slide",1145,1374,LS,LO,0,0,1145,1374);
    static readonly SpritePart SlideRail=Part("slide",1145,1374,LS,LO,new[]{new Vector2(467,316),new Vector2(500,316),new Vector2(465,808),new Vector2(432,808)});
    public static void SlideDraw(Vector3 at,PlaygroundPalette palette){SlideWhole.Draw(at,Rear,palette:palette);SlideRail.Draw(at,Front,palette:palette);}
    static Vector3 LChute(float t)=>LP(724,Mathf.Lerp(388,1192,Mathf.Clamp01(t)));
    public static Vector3 SlideModel(int age)
    {
        var foot=LP(355,786);var top=LP(449,391);var chute=LChute(0);
        if(age<0)return SlideMotion.Entry;
        if(age<60)return Vector3.Lerp(SlideMotion.Entry,foot,SwingMotion.Smooth(age/60f));
        if(age<300){float step=(age-60)*6f/240;float t=(Mathf.Floor(step)+SwingMotion.Smooth((step-Mathf.Floor(step))/.8f))/6;return Vector3.Lerp(foot,top,t);}
        if(age<390)return Vector3.Lerp(top,chute,SwingMotion.Smooth((age-300)/90f));
        if(age<438)return chute;
        if(age<534){float t=(age-438)/96f;return LChute(t*t*(3-2*t));}
        if(age<594)return Vector3.Lerp(LChute(1),SlideMotion.Exit,SwingMotion.Smooth((age-534)/60f));
        return SlideMotion.Exit;
    }

    const float FS=.0018f;
    static readonly Vector2 FO=new(775,665);
    static Vector3 FP(float x,float y)=>P((x-775)*FS,(665-y)*FS);
    static readonly SpritePart FoxUpper=Box("fox",1536,1024,FS,FO,0,0,1536,627);
    static readonly SpritePart FoxLeft=Box("fox",1536,1024,FS,FO,0,627,676,86),FoxRight=Box("fox",1536,1024,FS,FO,866,627,670,86);
    static readonly SpritePart FoxBase=Box("fox",1536,1024,FS,FO,560,627,430,320);
    static readonly SpritePart FoxLip=Box("fox",1536,1024,FS,FO,620,436,391,60);
    // The fox turned around (PlaygroundTurn, TurnMap.FlipNorth): the same parts flipped left to right.
    static SpritePart Flip(float x,float y,float w,float h)=>new(Root+"fox",1536,1024,FS,FO,SpritePart.Rect(x,y,w,h),mirror:true);
    static readonly SpritePart FoxUpperF=Flip(0,0,1536,627),FoxLeftF=Flip(0,627,676,86),FoxRightF=Flip(866,627,670,86),FoxBaseF=Flip(560,627,430,320),FoxLipF=Flip(620,436,391,60);
    public static void FoxDrawFlipped(Vector3 at,float degrees,PlaygroundPalette palette)
    {degrees=-degrees;FoxBaseF.Draw(at,Rear,palette:palette);FoxUpperF.Draw(at,Rear+.004f,degrees,palette:palette);FoxLeftF.Draw(at,Rear+.004f,degrees,palette:palette);FoxRightF.Draw(at,Rear+.004f,degrees,palette:palette);FoxLipF.Draw(at,Front,degrees,palette:palette);}
    public static Vector3 FoxContact(float degrees)=>Rot(FP(813,415),degrees);
    public static void FoxDraw(Vector3 at,float degrees,PlaygroundPalette palette)
    {FoxBase.Draw(at,Rear,palette:palette);FoxUpper.Draw(at,Rear+.004f,degrees,palette:palette);FoxLeft.Draw(at,Rear+.004f,degrees,palette:palette);FoxRight.Draw(at,Rear+.004f,degrees,palette:palette);FoxLip.Draw(at,Front,degrees,palette:palette);}

    static readonly SpritePart RoundDeck=Box("rounddeck",1254,1254,.00238f,new(627,627),0,0,1254,1254);
    static readonly Material Ink=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.17f,.16f,.12f));
    static readonly Material Sage=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.48f,.57f,.36f));
    static readonly Material Light=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.68f,.74f,.56f));
    static readonly Mesh Disk=MakeDisk();
    public static void RoundDraw(Vector3 at,float angle,PlaygroundPalette palette)
    {
        RoundDeck.Draw(at,Floor,angle,palette:palette);
        for(int side=0;side<2;side++)for(int n=0;n<4;n++)
        {
            float a=angle+n*90;var left=Rot(P(-.40f,-1.05f),a);var right=Rot(P(.40f,-1.05f),a);
            bool back=(left.z+right.z)>0;if(back!=(side==0))continue;float layer=back?Rear:Front;
            var p=new[]{left,left+P(0,.52f),right+P(0,.52f),right};
            for(int i=0;i<3;i++){Stroke(at+p[i],at+p[i+1],.11f,Ink,layer);Stroke(at+p[i]+P(-.005f,.008f),at+p[i+1]+P(-.005f,.008f),.076f,Sage,layer+.003f);Stroke(at+p[i]+P(-.016f,.015f),at+p[i+1]+P(-.016f,.015f),.02f,Light,layer+.006f);}
        }
        
    }
    public static Vector3 RoundContact(int slot,float angle)=>Rot(P(0,-.78f),angle+slot*90);
    static void Stroke(Vector3 a,Vector3 b,float w,Material m,float y)
    {var d=b-a;var at=(a+b)*.5f;at.y=y;Graphics.DrawMesh(MeshPool.plane10,Matrix4x4.TRS(at,Quaternion.AngleAxis(d.AngleFlat(),Vector3.up),new(w,1,d.MagnitudeHorizontal())),m,0);Oval(a,w,m,y);Oval(b,w,m,y);}
    static void Oval(Vector3 at,float w,Material m,float y){at.y=y;Graphics.DrawMesh(Disk,Matrix4x4.TRS(at,Quaternion.identity,new(w*.5f,1,w*.5f)),m,0);}
    static Mesh MakeDisk(){var v=new Vector3[25];var t=new int[72];for(int i=0;i<24;i++){float a=-i*Mathf.PI/12;v[i+1]=P(Mathf.Cos(a),Mathf.Sin(a));t[i*3+1]=i+1;t[i*3+2]=(i+1)%24+1;}var m=new Mesh{vertices=v,triangles=t};m.RecalculateNormals();m.RecalculateBounds();return m;}
}
