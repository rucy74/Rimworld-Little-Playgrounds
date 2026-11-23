using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

[DefOf]
public static class MorePlayDefOf
{
    public static ThingDef LP_BubbleTub,LP_PullUpBar;
    public static JobDef LP_BlowBubbles,LP_CareBubbles,LP_SwingOnBar;
    static MorePlayDefOf()=>DefOfHelper.EnsureInitializedInCtor(typeof(MorePlayDefOf));
}

public static class HeldPlayDefOf
{
    public static ThingDef LP_KiteStand,LP_JumpRopeBox;
    public static JobDef LP_FlyKite,LP_JumpRope;
}

public static class MorePlay
{
    public static bool Handles(ExpansionKind kind)=>kind is ExpansionKind.BubbleTub or ExpansionKind.PullBar or ExpansionKind.Kite or ExpansionKind.JumpRope;
    public static bool SlotOpen(Building_PlaygroundFacility b,int slot,Pawn p)=>b.Kind switch
    {
        ExpansionKind.BubbleTub=>slot==0||b.VisitAt(0)!=null&&BubbleMotion.RoomToChase(b,p),
        ExpansionKind.Kite=>KiteMotion.SkyOpen(b,slot),
        _=>true
    };
    // The same, while playing: something built or forbidden during the visit ends it.
    public static bool StillClear(Building_PlaygroundFacility b,int slot,Pawn p)=>b.Kind switch
    {
        ExpansionKind.BubbleTub=>slot==0||BubbleMotion.RoomToChase(b,p),
        ExpansionKind.Kite=>KiteMotion.SkyOpen(b,slot),
        _=>true
    };
    // Whether someone called over could play here once the caller has its seat. The tub's second seat needs ground to run on: a call there would bring them to a seat that never opens.
    public static bool SecondSeatOpen(Thing site)=>site is not Building_PlaygroundFacility {Kind:ExpansionKind.BubbleTub} tub||BubbleMotion.GroundOpen(tub);
}

// The first visitor blows, standing south of the tub. A second one runs about catching bubbles: half of all bubbles burst early,
// and the catcher's path goes to where those burst, so the catching is planned rather than detected.
public static class BubbleMotion
{
    public const int Cycle=360,DipEnd=70,RaiseEnd=100,Emit=16,Count=12,BlowEnd=RaiseEnd+Emit*Count,Quiet=230;
    const float Reach=1.8f;
    public static readonly Vector3 Spot=new(0,0,-1);
    static readonly Vector3 TubFromSpot=new(0,0,.72f);

    public static int Clock(Thing b)=>Find.TickManager.TicksGame+Math.Abs(b.thingIDNumber*13%Cycle);
    static uint Mix(int a,int b)
    {
        unchecked
        {
            uint h=(uint)a*2654435761u^(uint)b*2246822519u;
            h^=h>>15;h*=2654435761u;h^=h>>13;return h;
        }
    }
    static float Unit(int a,int b)=>Mix(a,b)%1000/1000f;
    // Blown to one side, where the pawn's profile and the bubbles can both be seen; south would hide them against its own body.
    public static Rot4 Side(int cycle,int id)=>Mix(cycle,id)%2==0?Rot4.West:Rot4.East;
    static Vector3 Mouth(Rot4 side)=>side==Rot4.West?new Vector3(-.3f,0,.28f):new Vector3(.3f,0,.28f);
    static float Lean(Rot4 side)=>side==Rot4.West?-42:42;
    public static Rot4 BlowerFacing(Thing b){int t=Clock(b);return t%Cycle<DipEnd?Rot4.North:Side(t/Cycle,b.thingIDNumber);}
    public static bool Blowing(Thing b){int phase=Clock(b)%Cycle;return phase>=RaiseEnd&&phase<BlowEnd;}

    // Where the wand's ring is, relative to the blower, and how its handle leans: down into the tub, up to the mouth, lowered to watch.
    public static void Wand(Thing b,float rest,out Vector3 ring,out float lean)=>WandAt(Clock(b),b.thingIDNumber,rest,out ring,out lean);
    // rest: 0 while blowing, 1 once the blower has stopped to watch the last bubbles go.
    public static float RestAt(int age,int duration)=>Mathf.Clamp01((age-(duration-Quiet))/30f);
    public static void WandAt(int t,int id,float rest,out Vector3 ring,out float lean)
    {
        int cycle=t/Cycle,phase=t%Cycle;
        var side=Side(cycle,id);var before=Side(cycle-1,id);
        Vector3 Low(Rot4 r)=>Mouth(r)+new Vector3(0,0,-.22f);
        if(phase<DipEnd){float f=SwingMotion.Smooth(Mathf.Clamp01(phase/40f));ring=Vector3.Lerp(Low(before),TubFromSpot,f);lean=Mathf.Lerp(Lean(before),0,f);return;}
        var high=Vector3.Lerp(Mouth(side),Low(side),SwingMotion.Smooth(rest));
        if(phase<RaiseEnd){float f=SwingMotion.Smooth((phase-DipEnd)/(float)(RaiseEnd-DipEnd));ring=Vector3.Lerp(TubFromSpot,high,f);lean=Mathf.Lerp(0,Lean(side),f);return;}
        lean=Lean(side);
        ring=phase<BlowEnd?high:Vector3.Lerp(high,Low(side),SwingMotion.Smooth(Mathf.Clamp01((phase-BlowEnd)/30f)));
    }
    // Nobody blowing, or blowing no more: a catcher arriving now has nothing to catch.
    public static bool NothingToCatch(Building_PlaygroundFacility b){var blower=b.VisitAt(0);return blower==null||blower.Age>blower.Duration-Quiet;}

    static int Birth(int cycle,int i)=>cycle*Cycle+RaiseEnd+i*Emit;
    // Odd bubbles burst early, where the catcher will be; even ones drift until they burst by themselves.
    public static int Life(int cycle,int i,int id)=>i%2==1?60+(int)(Mix(cycle*12+i,id)%16):150+(int)(Mix(cycle*12+i,id)%70);
    public static float Size(int cycle,int i,int id)=>.2f+.22f*Unit(cycle*12+i+3,id);
    public static Vector3 Path(int cycle,int i,int id,float age)
    {
        int seed=cycle*12+i;var side=Side(cycle,id);
        var forward=side==Rot4.West?Vector3.left:Vector3.right;
        var direction=Quaternion.AngleAxis((Unit(seed,id)-.5f)*70f,Vector3.up)*forward;
        float speed=.8f+.9f*Unit(seed+7,id);
        return Spot+Mouth(side)+direction*(speed*(1-Mathf.Exp(-age/48f)))
            +new Vector3(.06f*Mathf.Sin(age*.055f+seed),0,.0035f*age+.03f*Mathf.Sin(age*.08f+seed*2));
    }
    // Blown only while someone stands here blowing, and not in the last moments of a visit, so all have gone before the blower leaves.
    static bool Blown(Building_PlaygroundFacility b,int clock,int birth)
    {
        var blower=b.VisitAt(0);if(blower==null||blower.Age<0)return false;
        int atBirth=blower.Age-(clock-birth);
        return atBirth>=60&&atBirth<=blower.Duration-Quiet;
    }
    public const int PopTicks=8;
    // Visits every bubble in the air now, in a fixed order. Counting and drawing share it.
    public static void ForEach(Building_PlaygroundFacility b,Action<Vector3,float,bool> each)
    {
        int clock=Clock(b),id=b.thingIDNumber,now=clock/Cycle;
        for(int cycle=now-1;cycle<=now;cycle++)for(int i=0;i<Count;i++)
        {
            int birth=Birth(cycle,i),age=clock-birth,life=Life(cycle,i,id);
            if(age<0||age>life+PopTicks||!Blown(b,clock,birth))continue;
            float grow=Mathf.Clamp01(age/12f);
            if(age<=life)each(Path(cycle,i,id,age),Size(cycle,i,id)*(.4f+.6f*grow),false);
            else each(Path(cycle,i,id,life),Size(cycle,i,id)*(1+.09f*(age-life)),true);
        }
    }
    public static int Alive(Building_PlaygroundFacility b){int n=0;ForEach(b,(_,_,pop)=>{if(!pop)n++;});return n;}

    // The catcher's room: the cells it can be drawn on. Corners are outside its reach.
    static readonly List<IntVec3> Room=MakeRoom();
    static List<IntVec3> MakeRoom()
    {
        var room=new List<IntVec3>();
        for(int x=-2;x<=2;x++)for(int z=-2;z<=2;z++)if(!(x==0&&z==0)&&!(Math.Abs(x)==2&&Math.Abs(z)==2))room.Add(new IntVec3(x,0,z));
        return room;
    }
    // Open, firm ground, whoever is to run on it. A pawn can stand in shallow water or mud, but would be drawn running on top of it.
    public static bool GroundOpen(Building_PlaygroundFacility b)
    {
        foreach(var at in Room)
        {
            var c=b.Position+at;
            if(!c.InBounds(b.Map)||!c.Standable(b.Map)||!RunGround.Firm(c.GetTerrain(b.Map)))return false;
        }
        return true;
    }
    public static bool RoomToChase(Building_PlaygroundFacility b,Pawn p)
    {
        if(!GroundOpen(b))return false;
        foreach(var at in Room)if(PlaygroundAvailability.Cell(p,b.Position+at)!=PlayBlocked.None)return false;
        return true;
    }
    // Places around the tub as (angle from north, distance). Running between them by angle goes round the tub and never crosses
    // the blower, who stands due south.
    static Vector2 Polar(Vector3 at)=>new(Mathf.Clamp(Mathf.Atan2(at.x,at.z)*Mathf.Rad2Deg,-130,130),Mathf.Clamp(at.MagnitudeHorizontal(),.9f,Reach));
    static Vector3 Place(Vector2 polar)=>Quaternion.AngleAxis(polar.x,Vector3.up)*new Vector3(0,0,polar.y);
    // Key 0: between rounds, somewhere around the tub. Keys 1-3: where three of this round's early bubbles burst, reaching up from
    // the ground. Three, because bubbles come faster than a child runs; the rest burst by themselves.
    public const int Keys=4;
    static int KeyBubble(int key)=>key*4-3;
    static int KeyTime(int cycle,int key,int id)
    {
        if(key==0)return cycle*Cycle+60;
        int i=KeyBubble(key);return Birth(cycle,i)+Life(cycle,i,id);
    }
    static Vector2 KeyPlace(int cycle,int key,int id)
    {
        // Before a round, toward the side it will be blown to.
        if(key==0){var side=Side(cycle,id);return new Vector2(side==Rot4.West?-90:90,1.45f);}
        int i=KeyBubble(key);return Polar(Path(cycle,i,id,Life(cycle,i,id))+new Vector3(0,0,-.42f));
    }
    public static void Catcher(Thing b,out Vector3 at,out float hop,out Rot4 facing)=>CatcherAt(Clock(b),b.thingIDNumber,out at,out hop,out facing);
    public static void CatcherAt(int t,int id,out Vector3 at,out float hop,out Rot4 facing)
    {
        int cycle=t/Cycle,fromCycle=cycle,fromKey=0,toCycle=cycle,toKey=0;
        if(t<KeyTime(cycle,0,id)){fromCycle=cycle-1;fromKey=Keys-1;}
        else
        {
            fromKey=Keys-1;toCycle=cycle+1;
            for(int k=0;k<Keys-1;k++)if(t<KeyTime(cycle,k+1,id)){fromKey=k;toCycle=cycle;toKey=k+1;break;}
        }
        int t0=KeyTime(fromCycle,fromKey,id),t1=KeyTime(toCycle,toKey,id);
        Vector2 a=KeyPlace(fromCycle,fromKey,id),c=KeyPlace(toCycle,toKey,id);
        float f=Mathf.Clamp01((t-t0)/(float)Math.Max(1,t1-t0));
        at=Place(Vector2.Lerp(a,c,Mathf.SmoothStep(0,1,Mathf.Clamp01(f/.8f))));
        // A small jump that peaks as the bubble bursts, once it has arrived underneath.
        float jump=Mathf.Clamp01((t-(t1-22))/22f);
        hop=.2f*Mathf.Sin(jump*Mathf.PI);
        var delta=Place(c)-Place(a);facing=delta.sqrMagnitude>.01f?Rot4.FromAngleFlat(delta.AngleFlat()):Rot4.South;
    }
    public static float CatchMiss(int id,int cycle,int key)
    {
        int i=KeyBubble(key);
        return (Place(KeyPlace(cycle,key,id))+new Vector3(0,0,.42f)-Path(cycle,i,id,Life(cycle,i,id))).MagnitudeHorizontal();
    }
}

// A low bar: reach up and hang, three pull-ups, hand over hand along the bar and back, up to sit on it, down, let go, and again. A pawn is one flat
// picture, so nothing here tips it toward the viewer: it only moves and leans. It hangs behind the bar and sits in front of it.
public static class BarMotion
{
    public const int Loop=480,Duration=60+Loop*2+30+60;
    public const float Bar=.62f;
    const float Stand=-.62f;
    const int Hung=30,PullFrom=70,PullTo=190,WalkTo=270,UpTo=300,SitTo=400,DownTo=430,LetGo=450;
    static float Side(int slot)=>slot==0?-.5f:.5f;
    static int Phase(int age,int slot){int t=age-60-slot*30;return t<0||t>=Loop*2?-1:t%Loop;}
    public static Vector3 Contact(int age,int slot,bool child)
    {
        float hang=Bar-(child?.42f:.56f),sit=Bar+(child?.2f:.3f),x=Side(slot),z=Stand;int p=Phase(age,slot);
        if(p>=0)
        {
            if(p<Hung)z=Mathf.Lerp(Stand,hang,SwingMotion.Smooth(p/(float)Hung));
            else if(p<PullFrom)z=hang+.015f*Mathf.Sin((p-Hung)*.16f);
            else if(p<PullTo)z=hang+.24f*(1-Mathf.Cos((p-PullFrom)/(float)(PullTo-PullFrom)*Mathf.PI*6))/2;
            else if(p<WalkTo)
            {
                // Outward along the bar and back, a little lower at each reach.
                float f=(p-PullTo)/(float)(WalkTo-PullTo);
                x+=(slot==0?-1:1)*.24f*Mathf.Sin(f*Mathf.PI);z=hang-.03f*Mathf.Abs(Mathf.Sin(f*Mathf.PI*4));
            }
            else if(p<UpTo)z=Mathf.Lerp(hang,sit,SwingMotion.Smooth((p-WalkTo)/(float)(UpTo-WalkTo)));
            else if(p<SitTo)z=sit+.015f*Mathf.Sin((p-UpTo)*.09f);
            else if(p<DownTo)z=Mathf.Lerp(sit,hang,SwingMotion.Smooth((p-SitTo)/(float)(DownTo-SitTo)));
            else if(p<LetGo)z=hang;
            else z=Mathf.Lerp(hang,Stand,SwingMotion.Smooth((p-LetGo)/(float)(Loop-LetGo)));
        }
        return new Vector3(x,0,z);
    }
    public static float Angle(int age,int slot)
    {
        int p=Phase(age,slot);
        if(p>=PullTo&&p<WalkTo)return 5*Mathf.Sin((p-PullTo)/(float)(WalkTo-PullTo)*Mathf.PI*4);
        return p>=UpTo&&p<SitTo?2.5f*Mathf.Sin((p-UpTo)*.05f):0;
    }
    // Sitting, the pawn is in front of the bar; it changes over half way up and half way down.
    public static bool InFront(int age,int slot){int p=Phase(age,slot);return p>=(WalkTo+UpTo)/2&&p<(SitTo+DownTo)/2;}
}

// Each flier stands to one side of the stand, looking up. The kite climbs, drifts on its string and comes down again before the visit ends.
// Open sky and no rain, both at the start and while flying. Wind only changes how far it drifts.
public static class KiteMotion
{
    public const int Duration=1080;
    public static bool SkyClear(Building_PlaygroundFacility b)=>!b.Position.Roofed(b.Map)&&b.Map.weatherManager.RainRate<=.1f;
    public static bool SkyOpen(Building_PlaygroundFacility b,int slot)=>SkyClear(b)&&!b.EntryAt(slot).Roofed(b.Map);
    public static Vector3 Contact(int age,int slot)=>new((slot==0?-1:1)+.1f*Mathf.Sin(age*.018f+slot),0,-1);
    static float Up(int age)=>SwingMotion.Smooth(Mathf.Clamp01((age-60)/200f))*SwingMotion.Smooth(Mathf.Clamp01((Duration-60-age)/160f));
    // Where the kite is, measured from the flier.
    public static Vector3 Offset(int age,int slot,int id,float wind)
    {
        float t=age+Math.Abs(id%997),up=Up(age);
        float x=(.8f*Mathf.Sin(t*.0105f+slot*2.1f)+.25f*Mathf.Sin(t*.034f+slot))*(.7f+.3f*Mathf.Clamp01(wind));
        return new Vector3(x*up,0,(3.2f+slot*.5f+.3f*Mathf.Sin(t*.016f+slot*1.3f))*up);
    }
    public static float Lean(int age,int slot,int id)=>14*Mathf.Cos((age+Math.Abs(id%997))*.0105f+slot*2.1f);
}

// Turns of the rope so far: at a whole number it is under the feet, which is when the jumper is at the top of the hop. Five turns, three
// quick ones, a rest with the rope on the ground, and again.
public static class RopeMotion
{
    public const int Loop=340,Rounds=3,Duration=60+Loop*Rounds+60;
    const int SlowEnd=200,QuickEnd=278;
    public static Vector3 Spot(int slot)=>new(slot==0?-1:1,0,-1);
    static int Clock(int age,int slot)=>age-60-slot*17;
    public static float Turns(int age,int slot)
    {
        int t=Clock(age,slot);if(t<0)return 0;
        if(t>=Loop*Rounds)return Rounds*8;
        int a=t%Loop;
        return t/Loop*8+(a<SlowEnd?a/40f:a<QuickEnd?5+(a-SlowEnd)/26f:8);
    }
    public static bool Turning(int age,int slot){int t=Clock(age,slot);return t>=0&&t<Loop*Rounds&&t%Loop<QuickEnd;}
    static float Air(int age,int slot)
    {
        if(!Turning(age,slot))return 0;
        int a=Clock(age,slot)%Loop;float c=Mathf.Cos(Turns(age,slot)*Mathf.PI);
        return c*c*Mathf.Clamp01(a/10f)*Mathf.Clamp01((QuickEnd-a)/10f);
    }
    public static float Hop(int age,int slot)=>.17f*Air(age,slot);
    // Only a touch shorter on landing, plain at rest.
    public static float Length(int age,int slot)=>Turning(age,slot)?.97f+.03f*Air(age,slot):1;
}

[StaticConstructorOnStartup]
public static class MorePlayArt
{
    static SpritePart Whole(string path,int w,int h,float scale,Vector2? origin=null)=>new(path,w,h,scale,origin??new Vector2(w/2f,h/2f),SpritePart.Rect(0,0,w,h));
    static readonly SpritePart Tub=Whole("Things/Building/BubbleTubV1",640,640,1.25f/640);
    static readonly SpritePart WandSprite=Whole("Things/Building/BubbleWandV1",256,256,.5f/256,new Vector2(128,64));
    const float Cell=.004f;
    static readonly SpritePart Bar=Whole("Things/Building/PullBarV1",625,500,Cell),BarFront=Whole("Things/Building/PullBarFrontV1",625,500,Cell);
    static Material bubble,pop;
    static Material Soft(ref Material m,string path)
    {
        if(m==null||m.mainTexture==null)m=new Material(MaterialPool.MatFrom(path,ShaderDatabase.Transparent,Color.white));
        return m;
    }
    public static void Draw(Building_PlaygroundFacility b,Vector3 at)
    {
        float pawn=AltitudeLayer.Pawn.AltitudeFor(),rear=pawn-.18f,front=pawn+.30f;
        switch(b.Kind)
        {
            case ExpansionKind.PullBar:
                b.def.graphic.ShadowGraphic?.DrawWorker(at,Rot4.North,b.def,b,0f);
                Bar.Draw(at,rear);BarFront.Draw(at,front);break;
            case ExpansionKind.BubbleTub:
                b.def.graphic.ShadowGraphic?.DrawWorker(at,Rot4.North,b.def,b,0f);
                Tub.Draw(at,rear);
                var blower=b.VisitAt(0);
                if(blower?.Age>=0&&blower.Blend>.5f)
                {
                    BubbleMotion.Wand(b,BubbleMotion.RestAt(blower.Age,blower.Duration),out var ring,out float lean);
                    WandSprite.Draw(at+BubbleMotion.Spot+ring,front+.01f,lean);
                }
                int n=0;
                BubbleMotion.ForEach(b,(place,size,bursting)=>
                {
                    var material=bursting?Soft(ref pop,"Things/Building/BubblePopV1"):Soft(ref bubble,"Things/Building/BubbleV1");
                    var position=at+place;position.y=front+.04f+n++*.0004f;
                    Graphics.DrawMesh(MeshPool.plane10,Matrix4x4.TRS(position,Quaternion.identity,new Vector3(size,1,size)),material,0);
                });
                break;
        }
    }
}
