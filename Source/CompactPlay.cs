using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

[DefOf]
public static class CompactDefOf
{
    public static ThingDef LP_CompactSlide,LP_FloorRocker,LP_BallPit;
    public static JobDef LP_RideCompactSlide,LP_RideFloorRocker,LP_PlayBallPit,LP_CareFloorRocker,LP_CareBallPit;
    static CompactDefOf()=>DefOfHelper.EnsureInitializedInCtor(typeof(CompactDefOf));
}

// The small rides reuse the existing visit/reservation/save/learning lifecycle.
// All motion is a pure function of saved game ticks; rendering never advances it or draws random numbers.
public static class CompactMotion
{
    public static bool IsCompact(ExpansionKind kind)=>kind is ExpansionKind.CompactSlide or ExpansionKind.FloorRocker or ExpansionKind.BallPit;
    public const int SlideTicks=400;
    static Vector3 P(float x,float z)=>new(x,0,z);
    public static Vector3 SlidePoint(int age)
    {
        if(age<60)return Vector3.Lerp(P(-.5f,-2),P(-.4f,-.38f),SwingMotion.Smooth(age/60f));
        if(age<190)
        {
            float t=(age-60)/130f;
            return Vector3.Lerp(P(-.4f,-.38f),P(-.4f,1.0f),SwingMotion.Smooth(t))+P(0,.035f*Mathf.Sin(t*Mathf.PI*6));
        }
        if(age<240)return Vector3.Lerp(P(-.4f,1f),P(.48f,1f),SwingMotion.Smooth((age-190)/50f));
        if(age<265)return P(.48f,1f);
        if(age<335){float t=(age-265)/70f;return Vector3.Lerp(P(.48f,1f),P(.48f,-1.32f),t*t);}
        return Vector3.Lerp(P(.48f,-1.32f),P(.5f,-2),SwingMotion.Smooth((age-335)/65f));
    }
    public static Rot4 SlideFacing(int age)=>age<190?Rot4.North:age<240?Rot4.East:Rot4.South;
    public static float Rock(Building_PlaygroundFacility b)
    {
        float blend=0;
        for(int i=0;i<b.Capacity;i++)if(b.VisitAt(i) is {Age:>=0} v)blend=Mathf.Max(blend,v.Blend);
        float tick=Find.TickManager.TicksGame+b.thingIDNumber*17;
        return blend*(8+2*Mathf.Sin(tick*.009f))*Mathf.Sin(tick*Mathf.PI/82f+.22f*Mathf.Sin(tick*.006f));
    }
    public static Vector3 RockSeat(int slot,float angle)=>Quaternion.AngleAxis(angle,Vector3.up)*P(slot==0?-.86f:.86f,.22f);
    // Turned a quarter (PlaygroundTurn) the seats come from the side picture; the step in front of each seat turns with the rocker.
    public static Vector3 RockSeat(Building_PlaygroundFacility b,int slot,float angle)
        =>PlaygroundTurn.Map(b) is TurnMap.East or TurnMap.FlipEast?TurnRides.RockSeat(b,slot,angle):RockSeat(slot,angle);
    public static Vector3 RockPath(Building_PlaygroundFacility b,int age,int duration,int slot,float angle,Vector3 entry)
    {
        var map=PlaygroundTurn.Map(b);
        if(map is not (TurnMap.East or TurnMap.FlipEast))return RockPath(age,duration,slot,angle,entry);
        int edge=Math.Min(age,duration-age);
        var front=PlaygroundTurn.Ground(map,P(slot==0?-.86f:.86f,-.42f));
        if(edge<40)return Vector3.Lerp(entry,front,SwingMotion.Smooth(edge/40f));
        return Vector3.Lerp(front,RockSeat(b,slot,angle),SwingMotion.Smooth((edge-40)/35f));
    }
    public static float RockSeated(int age,int duration)=>SwingMotion.Smooth((age-40)/35f)*SwingMotion.Smooth((duration-age-40)/35f);
    public static Vector3 RockPath(int age,int duration,int slot,float angle,Vector3 entry)
    {
        int edge=Math.Min(age,duration-age);
        var front=P(slot==0?-.86f:.86f,-.42f);
        if(edge<40)return Vector3.Lerp(entry,front,SwingMotion.Smooth(edge/40f));
        return Vector3.Lerp(front,RockSeat(slot,angle),SwingMotion.Smooth((edge-40)/35f));
    }
    public const int PitTransit=120;
    public static bool PitDoorBusy(JobDriver_VisitPlayground visitor)
    {
        var other=visitor.Facility.VisitAt(1-visitor.Slot);
        return other?.Age>=0&&(other.Age<PitTransit||other.Age>=other.Duration-PitTransit);
    }
    // Queue in separate outside cells, then pass through the same center opening.
    public static Vector3 PitPath(int age,int duration,int slot,int seed)
    {
        int edge=Math.Min(age,duration-age);
        var entry=P(slot==0?-1:1,-2);var door=P(0,-1.55f);var inside=P(0,-.55f);
        if(edge<45)return Vector3.Lerp(entry,door,SwingMotion.Smooth(edge/45f));
        if(edge<90)return Vector3.Lerp(door,inside,SwingMotion.Smooth((edge-45)/45f));
        return Vector3.Lerp(inside,PitPoint(age,slot,seed),SwingMotion.Smooth((edge-90)/30f));
    }
    // Separate lanes keep two visitors from crossing one another. Slow turns and a brief bob suggest wading in balls.
    public static Vector3 PitPoint(int age,int slot,int seed)
    {
        float t=age+Math.Abs(seed%211),side=slot==0?-1:1;
        return P(side*(.72f+.08f*Mathf.Sin(t*.013f)),.28f+.36f*Mathf.Sin(t*.009f));
    }
    public static Rot4 PitFacing(int age,int slot,int seed)
    {
        var direction=PitPoint(age+5,slot,seed)-PitPoint(age,slot,seed);
        return Rot4.FromAngleFlat(direction.AngleFlat());
    }
    public static IntVec3 SlideExit(Building_PlaygroundFacility b)=>PlaygroundTurn.Cell(b,new IntVec3(1,0,-2));
    public static IntVec3 PitEntrance(Building_PlaygroundFacility b)=>b.Position+new IntVec3(0,0,-2);
}

[StaticConstructorOnStartup]
public static class CompactArt
{
    const int Pixels=1254;
    const string SlidePath="Things/Building/CompactSlideV1",RockPath="Things/Building/FloorRockerV1",
        PitPath="Things/Building/BallPitEmptyV1",BallPath="Things/Building/CompactPlayBallsV1";
    static readonly Vector2 Origin=new(627,627);
    static SpritePart Part(string path,float scale,Vector2[] area,Vector2? origin=null)
        =>new(path,Pixels,Pixels,scale,origin??Origin,area);
    static readonly SpritePart Slide=Part(SlidePath,.0032f,SpritePart.Rect(0,0,Pixels,Pixels));
    static readonly SpritePart Rocker=Part(RockPath,.0025f,SpritePart.Rect(0,0,Pixels,Pixels));
    static readonly SpritePart RockerLip=Part(RockPath,.0025f,new[]{new Vector2(80,676),new Vector2(260,713),
        new Vector2(627,742),new Vector2(1000,708),new Vector2(1170,665),new Vector2(1170,810),new Vector2(80,810)});
    static readonly SpritePart Pit=Part(PitPath,.0040f,SpritePart.Rect(0,0,Pixels,Pixels));
    static readonly SpritePart PitFront=Part(PitPath,.0040f,SpritePart.Rect(250,842,750,115));
    static readonly SpritePart[] Balls={Ball(209),Ball(627),Ball(1045)};
    static SpritePart Ball(float x)=>Part(BallPath,.0012f,SpritePart.Rect(x-162,465,324,324),new Vector2(x,627));
    public static void Draw(Building_PlaygroundFacility b,Vector3 at)
    {
        float rear=AltitudeLayer.Pawn.AltitudeFor()-.18f,front=AltitudeLayer.Pawn.AltitudeFor()+.30f;
        switch(b.Kind)
        {
            case ExpansionKind.CompactSlide:Slide.Draw(at,rear);break;
            case ExpansionKind.FloorRocker:
                float angle=CompactMotion.Rock(b);
                PlaygroundGround.Contact(at+new Vector3(0,0,-.35f),2.2f,.25f);
                Rocker.Draw(at,rear,angle);RockerLip.Draw(at,front,angle);break;
            case ExpansionKind.BallPit:
                Pit.Draw(at,rear);
                // Cached sprites, no per-frame textures/materials/mesh construction. Only balls close to a visitor move.
                for(int row=6;row>=0;row--)for(int col=0;col<7;col++)
                {
                    int n=row*7+col;
                    var point=new Vector3(-.94f+col*.31f+.055f*Mathf.Sin(n*4.73f),0,-.69f+row*.235f+.055f*Mathf.Cos(n*2.23f));
                    float ripple=0;
                    bool foreground=false;
                    for(int slot=0;slot<b.Capacity;slot++)if(b.VisitAt(slot) is {Age:>=60} v&&v.Age<v.Duration-60)
                    {
                        float distance=(point-v.Contact).MagnitudeHorizontal();
                        if(Mathf.Abs(point.x-v.Contact.x)<.33f&&point.z<v.Contact.z-.14f&&point.z>v.Contact.z-.40f)foreground=true;
                        ripple+=Mathf.Max(0,1-distance/.65f)*v.Blend;
                    }
                    float tick=Find.TickManager.TicksGame;
                    point+=new Vector3(Mathf.Sin(tick*.055f+n)*.065f,0,Mathf.Sin(tick*.07f+n*2)*.055f)*ripple;
                    int color=(int)Mathf.Abs(Mathf.Sin(n*12.9898f)*43758.5453f)%3;
                    Balls[color].Draw(at+point,foreground?front-.02f:rear+.02f+(6-row)*.001f,Mathf.Sin(n*17)*15f);
                }
                PitFront.Draw(at,front);break;
        }
    }
}
