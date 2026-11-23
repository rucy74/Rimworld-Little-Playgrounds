using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

[StaticConstructorOnStartup]
public static class TurnRides
{
    static float Rear=>AltitudeLayer.Pawn.AltitudeFor()-.18f;
    static float Front=>AltitudeLayer.Pawn.AltitudeFor()+.30f;
    static Vector3 P(float x,float z)=>new(x,0,z);
    static bool Side(TurnMap map)=>map is TurnMap.East or TurnMap.FlipEast;
    static TurnPicture Pick(TurnPicture side,TurnMap map)=>map==TurnMap.FlipEast?side.Mirrored:side;

    // Small slide: from behind (the platform nearest, the steps running away on the right, the chute on the left) and from the side
    // (the chute in front running down to the left, the steps behind it).
    static readonly TurnPicture SlideBack=new("Things/Building/Turn/compactslide-back",434,613,.0045f,new(217,311));
    static readonly TurnPicture SlideSide=new("Things/Building/Turn/compactslide-side",700,441,.0039f,new(350,200));
    static readonly SpritePart SlideBackWhole=SlideBack.Whole();
    static readonly SpritePart[] SlideSideWhole={SlideSide.Whole(),SlideSide.Mirrored.Whole()};
    // foot of the steps, top of the steps, top of the chute, bottom of the chute
    static Vector3[] SlideKeys(TurnMap map)
    {
        if(map==TurnMap.South)return new[]{SlideBack.Point(305,190),SlideBack.Point(305,450),SlideBack.Point(130,470),SlideBack.Point(130,40)};
        var s=Pick(SlideSide,map);
        return new[]{s.Point(130,260),s.Point(440,120),s.Point(520,160),s.Point(60,330)};
    }
    // The same timing as the north slide (CompactMotion.SlidePoint), between this picture's own points.
    public static Vector3 SlidePoint(Building_PlaygroundFacility b,int age)
    {
        var map=PlaygroundTurn.Map(b);var k=SlideKeys(map);
        var entry=PlaygroundTurn.Ground(map,P(-.5f,-2));var exit=PlaygroundTurn.Ground(map,P(.5f,-2));
        if(age<60)return Vector3.Lerp(entry,k[0],SwingMotion.Smooth(age/60f));
        if(age<190){float t=(age-60)/130f;return Vector3.Lerp(k[0],k[1],SwingMotion.Smooth(t))+P(0,.035f*Mathf.Sin(t*Mathf.PI*6));}
        if(age<240)return Vector3.Lerp(k[1],k[2],SwingMotion.Smooth((age-190)/50f));
        if(age<265)return k[2];
        if(age<335){float t=(age-265)/70f;return Vector3.Lerp(k[2],k[3],t*t);}
        return Vector3.Lerp(k[3],exit,SwingMotion.Smooth((age-335)/65f));
    }

    // Floor rocker seen along its length: one seat at the top, one at the bottom, facing each other. Rocking raises one end and
    // lowers the other, which on the screen makes the whole boat longer or shorter about its middle (as the turned seesaw,
    // TurnSeesaw): the picture is stretched by cos + sin of the rock and the seats ride on it.
    static readonly TurnPicture RockSide=new("Things/Building/Turn/rocker-side",460,1380,.0022f,new(230,685));
    static readonly SpritePart[] RockWhole={RockSide.Whole(),RockSide.Mirrored.Whole()};
    static readonly SpritePart[][] RockFront={new[]{RockSide.Rect(0,1150,460,230),RockSide.Rect(0,420,460,140)},
        new[]{RockSide.Mirrored.Rect(0,1150,460,230),RockSide.Mirrored.Rect(0,420,460,140)}};
    static float RockStretch(float degrees){float a=degrees*Mathf.Deg2Rad;return Mathf.Cos(a)+Mathf.Sin(a);}
    public static Vector3 RockSeat(Building_PlaygroundFacility b,int slot,float angle)
    {
        var p=Pick(RockSide,PlaygroundTurn.Map(b)).Point(230,slot==0?300:1075);p.z*=RockStretch(angle);return p;
    }

    // Fox spring rider: turned a quarter it faces away (rear view, its tail in front of the rider) or towards us (front view, its
    // head in front of the rider). Turned around it is the original picture flipped (RestyleArt / TimberArt).
    static readonly TurnPicture FoxRear=new("Things/Building/Turn/fox-rear",340,796,.00174f,new(170,601));
    static readonly TurnPicture FoxFront=new("Things/Building/Turn/fox-front",330,799,.00174f,new(165,601));
    static readonly TurnPicture WoodFoxRear=new("Things/Building/Timber/fox-rear",427,820,.0019f,new(215,570));
    static readonly TurnPicture WoodFoxFront=new("Things/Building/Timber/fox-front",426,819,.0019f,new(214,570));
    static readonly SpritePart FoxRearWhole=FoxRear.Whole(),FoxRearTail=FoxRear.Rect(60,290,220,260);
    static readonly SpritePart FoxFrontWhole=FoxFront.Whole(),FoxFrontHead=FoxFront.Rect(60,90,210,250);
    static readonly SpritePart WoodRearWhole=WoodFoxRear.Whole(),WoodRearTail=WoodFoxRear.Rect(112,172,196,368);
    static readonly SpritePart WoodFrontWhole=WoodFoxFront.Whole(),WoodFrontHead=WoodFoxFront.Rect(88,8,252,318);
    const float PlainSpringFoot=672f,WoodSpringFoot=678f;
    static SpritePart Above(TurnPicture p)=>p.Rect(0,0,p.width,p.origin.y);
    static SpritePart Spring(TurnPicture p,float foot)=>p.PartAbout(new Vector2(p.origin.x,foot),SpritePart.Rect(0,p.origin.y,p.width,foot-p.origin.y));
    static SpritePart Base(TurnPicture p,float foot)=>p.Rect(0,foot,p.width,p.height-foot);
    static readonly SpritePart FoxRearAbove=Above(FoxRear),FoxRearSpring=Spring(FoxRear,PlainSpringFoot),FoxRearBase=Base(FoxRear,PlainSpringFoot);
    static readonly SpritePart FoxFrontAbove=Above(FoxFront),FoxFrontSpring=Spring(FoxFront,PlainSpringFoot),FoxFrontBase=Base(FoxFront,PlainSpringFoot);
    static readonly SpritePart WoodRearAbove=Above(WoodFoxRear),WoodRearSpring=Spring(WoodFoxRear,WoodSpringFoot),WoodRearBase=Base(WoodFoxRear,WoodSpringFoot);
    static readonly SpritePart WoodFrontAbove=Above(WoodFoxFront),WoodFrontSpring=Spring(WoodFoxFront,WoodSpringFoot),WoodFrontBase=Base(WoodFoxFront,WoodSpringFoot);
    // The spring's length on the screen, and how far the fox rides up or down on it: up to 45% of that at the strongest rock (13 degrees).
    static float SpringLength(bool wood)=>wood?(WoodSpringFoot-WoodFoxRear.origin.y)*WoodFoxRear.scale:(PlainSpringFoot-FoxRear.origin.y)*FoxRear.scale;
    static float FoxBob(bool wood,float degrees)=>.45f*SpringLength(wood)*Mathf.Clamp(degrees/13f,-1f,1f);
    public static Vector3 FoxContact(Building_PlaygroundFacility b,float degrees)
    {
        var map=PlaygroundTurn.Map(b);bool wood=DesignArt.Timber(b);
        // The wooden rider sits behind the tail or the head, so it shows higher, its head and shoulders above them.
        var seat=map==TurnMap.East?(wood?WoodFoxRear.Point(215,160):FoxRear.Point(170,380)):(wood?WoodFoxFront.Point(214,120):FoxFront.Point(165,380));
        return seat+P(0,FoxBob(wood,degrees));
    }

    static TurnRides()
    {
        var slide=CompactDefOf.LP_CompactSlide;
        TurnGhosts.Add(slide,Rot4.South,SlideBackWhole);TurnGhosts.Add(slide,Rot4.East,SlideSideWhole[0]);TurnGhosts.Add(slide,Rot4.West,SlideSideWhole[1]);
        var rocker=CompactDefOf.LP_FloorRocker;
        TurnGhosts.Add(rocker,Rot4.East,RockWhole[0]);TurnGhosts.Add(rocker,Rot4.West,RockWhole[1]);
        var fox=ExpansionDefOf.LP_SpringFox;
        TurnGhosts.Add(fox,Rot4.South,RestyleArt.FoxMirrorWhole);TurnGhosts.Add(fox,Rot4.East,FoxRearWhole);TurnGhosts.Add(fox,Rot4.West,FoxFrontWhole);
        var hop=ExpansionDefOf.LP_Hopscotch;
        TurnGhosts.Add(hop,Rot4.East,RestyleArt.HopPart,default,90,RestyleArt.HopScale);TurnGhosts.Add(hop,Rot4.South,RestyleArt.HopPart,default,180,RestyleArt.HopScale);
        TurnGhosts.Add(hop,Rot4.West,RestyleArt.HopPart,default,270,RestyleArt.HopScale);
    }

    // Draws a turned facility; false for a north one (and for kinds that do not turn), which the caller draws as before.
    public static bool Draw(Building_PlaygroundFacility b,Vector3 at)
    {
        var map=PlaygroundTurn.Map(b);
        if(map==TurnMap.North)return false;
        int i=map==TurnMap.FlipEast?1:0;
        switch(b.Kind)
        {
            case ExpansionKind.CompactSlide:
                if(map==TurnMap.South)SlideBackWhole.Draw(at,Rear);else SlideSideWhole[i].Draw(at,Rear);
                return true;
            case ExpansionKind.FloorRocker:
                if(!Side(map))return false;
                PlaygroundGround.Contact(at+P(0,-.25f),.95f,2.4f);
                var stretch=new Vector3(1,1,RockStretch(CompactMotion.Rock(b)));
                RockWhole[i].Draw(at,Rear,0,stretch);foreach(var part in RockFront[i])part.Draw(at,Front,0,stretch);
                return true;
            case ExpansionKind.SpringRider:
                if(map==TurnMap.FlipNorth)return false; // RestyleArt / TimberArt draw it flipped
                bool wood=DesignArt.Timber(b);float bob=FoxBob(wood,b.Rock);var up=at+P(0,bob);
                var squeeze=new Vector3(1,1,1+bob/SpringLength(wood));float foot=wood?WoodSpringFoot:PlainSpringFoot;
                PlaygroundGround.Contact(at+P(0,wood?-.40f:-.36f),wood?.78f:.62f,.30f);
                var pal=PaletteArt.Of(b);
                if(map==TurnMap.East)
                {
                    if(wood){WoodRearBase.Draw(at,Rear,palette:pal);WoodRearSpring.Draw(at+WoodFoxRear.Point(WoodFoxRear.origin.x,foot),Rear+.001f,0,squeeze,pal);
                        WoodRearAbove.Draw(up,Rear+.002f,palette:pal);WoodRearTail.Draw(up,Front,palette:pal);}
                    else{FoxRearBase.Draw(at,Rear);FoxRearSpring.Draw(at+FoxRear.Point(FoxRear.origin.x,foot),Rear+.001f,0,squeeze);
                        FoxRearAbove.Draw(up,Rear+.002f);FoxRearTail.Draw(up,Front);}
                }
                else
                {
                    if(wood){WoodFrontBase.Draw(at,Rear,palette:pal);WoodFrontSpring.Draw(at+WoodFoxFront.Point(WoodFoxFront.origin.x,foot),Rear+.001f,0,squeeze,pal);
                        WoodFrontAbove.Draw(up,Rear+.002f,palette:pal);WoodFrontHead.Draw(up,Front,palette:pal);}
                    else{FoxFrontBase.Draw(at,Rear);FoxFrontSpring.Draw(at+FoxFront.Point(FoxFront.origin.x,foot),Rear+.001f,0,squeeze);
                        FoxFrontAbove.Draw(up,Rear+.002f);FoxFrontHead.Draw(up,Front);}
                }
                return true;
            case ExpansionKind.Hopscotch:
                RestyleArt.Hopscotch(at,map==TurnMap.East?90:map==TurnMap.South?180:map==TurnMap.West?270:0);return true;
        }
        return false;
    }
}

[StaticConstructorOnStartup]
public static class TurnSeesaw
{
    static readonly TurnPicture Plain=new("Things/Building/Turn/seesaw-side",334,1437,.00374f,new(167,690));
    static readonly TurnPicture Wood=new("Things/Building/Timber/seesaw-side",216,1483,.00345f,new(108,715));
    static readonly TurnPicture PlainStand=new("Things/Building/Turn/seesaw-stand",334,267,.00374f,new(167,81));
    static readonly TurnPicture WoodStand=new("Things/Building/Timber/seesaw-stand",216,306,.00345f,new(108,139));
    static readonly SpritePart[] PlainWhole={Plain.Whole(),Plain.Mirrored.Whole()},WoodWhole={Wood.Whole(),Wood.Mirrored.Whole()};
    static readonly SpritePart[] PlainStandWhole={PlainStand.Whole(),PlainStand.Mirrored.Whole()},WoodStandWhole={WoodStand.Whole(),WoodStand.Mirrored.Whole()};
    static TurnSeesaw()
    {
        var def=SeesawDefOf.LP_Seesaw;
        TurnGhosts.Add(def,Rot4.East,PlainWhole[0]);TurnGhosts.Add(def,Rot4.East,PlainStandWhole[0]);
        TurnGhosts.Add(def,Rot4.West,PlainWhole[1]);TurnGhosts.Add(def,Rot4.West,PlainStandWhole[1]);
    }
    public static bool Turned(Thing b)=>PlaygroundTurn.Map(b) is TurnMap.East or TurnMap.FlipEast;
    static float Stretch(float degrees){float a=degrees*Mathf.Deg2Rad;return Mathf.Cos(a)+Mathf.Sin(a);}
    // Slot 0 sits on the north end (the west end of the north seesaw turns north).
    public static Vector3 Seat(Building_Seesaw b,int slot,float degrees)
    {
        bool wood=DesignArt.Timber(b);var pic=wood?Wood:Plain;
        var p=pic.Point(wood?108:167,slot==0?(wood?75:150):(wood?1365:1260));p.z*=Stretch(degrees);return p;
    }
    public static void Draw(Building_Seesaw b,Vector3 at)
    {
        bool wood=DesignArt.Timber(b);int i=PlaygroundTurn.Map(b)==TurnMap.FlipEast?1:0;
        PlaygroundGround.Contact(at+new Vector3(0,0,-.30f),.75f,.55f);
        float rear=AltitudeLayer.Pawn.AltitudeFor()-.16f;var palette=PaletteArt.Of(b);
        (wood?WoodWhole:PlainWhole)[i].Draw(at,rear,0,new Vector3(1,1,Stretch(b.Angle)),palette);
        (wood?WoodStandWhole:PlainStandWhole)[i].Draw(at,rear+.005f,palette:palette);
    }
}

// The swing set turned a quarter (PlaygroundTurn): the top bar runs up the map, the A-frames are seen face-on and the swings from
// the side, so a seat moves left and right as it swings. The frame picture has no swings; the chain (or rope) and the seat, seen
// edge-on, come from the north picture and are drawn where the swing is. Seen from the side the near chain hangs in front of the
// rider, so the chain and the seat's front lip are drawn over the rider. The south A-frame stands nearer than the south swing,
// so it is drawn again, from just above its top, over that rider and chain. Slot 0 hangs on the north half.
[StaticConstructorOnStartup]
public static class TurnSwing
{
    static readonly TurnPicture Plain=new("Things/Building/Turn/swing-frame",450,1469,.0042f,new(225,975));
    static readonly TurnPicture Wood=new("Things/Building/Timber/swing-frame",395,1507,.00434f,new(195,970));
    static readonly SpritePart[] PlainFrame={Plain.Whole(),Plain.Mirrored.Whole()},WoodFrame={Wood.Whole(),Wood.Mirrored.Whole()};
    static readonly SpritePart[] PlainNear={Plain.Rect(0,1005,450,464),Plain.Mirrored.Rect(0,1005,450,464)};
    static readonly SpritePart[] WoodNear={Wood.Rect(0,1012,395,495),Wood.Mirrored.Rect(0,1012,395,495)};
    const string PlainTex="Things/Building/Selectedswing",WoodTex="Things/Building/Timber/swing";
    static readonly SpritePart PlainChain=new(PlainTex,1254,1254,.0037f,new(319,156),SpritePart.Rect(295,156,48,774));
    static readonly SpritePart PlainSeat=new(PlainTex,1254,1254,.0037f,new(412,1001),SpritePart.Rect(290,930,245,142));
    static readonly SpritePart PlainLip=new(PlainTex,1254,1254,.0037f,new(412,1001),SpritePart.Rect(290,1036,245,36));
    static readonly SpritePart WoodRope=new(WoodTex,1254,1254,.0037f,new(393,179),SpritePart.Rect(381,179,24,806));
    static readonly SpritePart WoodSeat=new(WoodTex,1254,1254,.0037f,new(468,997),SpritePart.Rect(356,963,225,68));
    static readonly SpritePart WoodLip=new(WoodTex,1254,1254,.0037f,new(468,997),SpritePart.Rect(356,1000,225,31));
    static TurnSwing()
    {
        var def=LPDefOf.LP_Swing;TurnGhosts.Add(def,Rot4.East,PlainFrame[0]);TurnGhosts.Add(def,Rot4.West,PlainFrame[1]);
    }
    public static bool Turned(Thing b)=>PlaygroundTurn.Map(b) is TurnMap.East or TurnMap.FlipEast;
    static float Along(int slot)=>slot==0?.8f:-.8f;
    static float PivotHeight(bool wood)=>wood?1.95f:1.85f;
    static float Rope(bool wood)=>wood?1.5f:1.45f;
    static Vector3 Pivot(bool wood,int slot)=>new(0,0,Along(slot)+PivotHeight(wood));
    // The seat swings forward (towards the way the rider faces: west on the east placement) and back.
    public static Vector3 Seat(Building_Swing b,int slot,int age)
    {
        bool wood=DesignArt.Timber(b);float a=SwingMotion.Angle(age,slot),rope=Rope(wood);
        float forward=PlaygroundTurn.Map(b)==TurnMap.FlipEast?1:-1;
        return Pivot(wood,slot)+new Vector3(forward*rope*Mathf.Sin(a),0,-rope*Mathf.Cos(a));
    }
    public static void Draw(Building_Swing b,Vector3 at)
    {
        bool wood=DesignArt.Timber(b);int i=PlaygroundTurn.Map(b)==TurnMap.FlipEast?1:0;var palette=PaletteArt.Of(b);
        float rear=AltitudeLayer.Pawn.AltitudeFor()-.16f,front=AltitudeLayer.Pawn.AltitudeFor()+.3f;
        PlaygroundGround.Contact(at+new Vector3(0,0,2.1f),1.6f,.22f);PlaygroundGround.Contact(at+new Vector3(0,0,-2.1f),1.6f,.22f);
        (wood?WoodFrame:PlainFrame)[i].Draw(at,rear,palette:palette);
        float chainLength=(wood?806:774)*.0037f;
        for(int slot=0;slot<2;slot++)
        {
            var seat=Seat(b,slot,b.JobAt(slot)?.Age??-1);var pivot=Pivot(wood,slot);var d=seat-pivot;
            float angle=Mathf.Atan2(-d.x,-d.z)*Mathf.Rad2Deg;
            PlaygroundGround.Contact(at+new Vector3(seat.x*.6f,0,Along(slot)-.1f),.45f,.22f);
            (wood?WoodRope:PlainChain).Draw(at+pivot,front-.01f,angle,new Vector3(wood?1:.68f,1,d.magnitude/chainLength),palette);
            (wood?WoodSeat:PlainSeat).Draw(at+seat,rear+.02f,0,new Vector3(.35f,1,1),palette);
            (wood?WoodLip:PlainLip).Draw(at+seat,front,0,new Vector3(.35f,1,1),palette);
        }
        (wood?WoodNear:PlainNear)[i].Draw(at,front+.01f,palette:palette);
    }
}

// The playground slide turned (PlaygroundTurn): behind it the platform is nearest and its rail hides a rider standing there; from the
// side the chute runs down in front of the steps. The rider keeps the north slide's timing (SlideMotion) between each picture's own
// points: foot of the steps, top of the steps, top of the chute, bottom of the chute.
[StaticConstructorOnStartup]
public static class TurnSlide
{
    static readonly TurnPicture PlainBack=new("Things/Building/Turn/slide-back",384,695,.007f,new(190,547));
    static readonly TurnPicture PlainSide=new("Things/Building/Turn/slide-side",733,645,.0055f,new(370,525));
    static readonly TurnPicture WoodBack=new("Things/Building/Timber/slide-back",396,686,.007f,new(200,545));
    static readonly TurnPicture WoodSide=new("Things/Building/Timber/slide-side",694,653,.0055f,new(350,530));
    static readonly SpritePart PlainBackWhole=PlainBack.Whole(),PlainBackRail=PlainBack.Rect(20,395,350,125);
    static readonly SpritePart WoodBackWhole=WoodBack.Whole(),WoodBackRail=WoodBack.Rect(10,380,380,160);
    static readonly SpritePart[] PlainSideWhole={PlainSide.Whole(),PlainSide.Mirrored.Whole()},WoodSideWhole={WoodSide.Whole(),WoodSide.Mirrored.Whole()};
    static TurnSlide()
    {
        var def=SlideDefOf.LP_Slide;
        TurnGhosts.Add(def,Rot4.South,PlainBackWhole);TurnGhosts.Add(def,Rot4.East,PlainSideWhole[0]);TurnGhosts.Add(def,Rot4.West,PlainSideWhole[1]);
    }
    public static bool Turned(Thing b)=>PlaygroundTurn.Map(b)!=TurnMap.North;
    static Vector3[] Keys(Building_Slide b)
    {
        var map=PlaygroundTurn.Map(b);bool wood=DesignArt.Timber(b);
        if(map==TurnMap.South)
        {
            var p=wood?WoodBack:PlainBack;
            return wood?new[]{p.Point(295,160),p.Point(295,370),p.Point(100,360),p.Point(100,40)}
                :new[]{p.Point(285,50),p.Point(285,390),p.Point(95,400),p.Point(95,40)};
        }
        var s=wood?WoodSide:PlainSide;if(map==TurnMap.FlipEast)s=s.Mirrored;
        return wood?new[]{s.Point(155,430),s.Point(420,190),s.Point(490,280),s.Point(40,590)}
            :new[]{s.Point(150,455),s.Point(520,150),s.Point(590,230),s.Point(40,590)};
    }
    // Screen offset from the slide's centre, age as in SlideMotion.Model.
    public static Vector3 Model(Building_Slide b,int age)
    {
        var map=PlaygroundTurn.Map(b);var k=Keys(b);
        var entry=PlaygroundTurn.Ground(map,SlideMotion.Entry);var exit=PlaygroundTurn.Ground(map,SlideMotion.Exit);
        if(age<0)return entry;
        if(age<SlideMotion.BoardTicks)return Vector3.Lerp(entry,k[0],SlideMotion.Smooth(age/(float)SlideMotion.BoardTicks));
        if(age<SlideMotion.ClimbEnd)
        {
            float step=(age-SlideMotion.BoardTicks)*6f/SlideMotion.ClimbTicks;
            float progress=(Mathf.Floor(step)+SlideMotion.Smooth((step-Mathf.Floor(step))/.8f))/6f;
            return Vector3.Lerp(k[0],k[1],progress);
        }
        if(age<SlideMotion.PlatformEnd)return Vector3.Lerp(k[1],k[2],SlideMotion.Smooth((age-SlideMotion.ClimbEnd)/(float)SlideMotion.PlatformTicks));
        if(age<SlideMotion.SlideStart)return k[2];
        if(age<SlideMotion.SlideEnd){float t=(age-SlideMotion.SlideStart)/(float)SlideMotion.SlideTicks;return Vector3.Lerp(k[2],k[3],t*t*(3-2*t));}
        if(age<SlideMotion.TotalTicks)return Vector3.Lerp(k[3],exit,SlideMotion.Smooth((age-SlideMotion.SlideEnd)/(float)SlideMotion.LandTicks));
        return exit;
    }
    public static void Draw(Building_Slide b,Vector3 at)
    {
        var map=PlaygroundTurn.Map(b);bool wood=DesignArt.Timber(b);var palette=PaletteArt.Of(b);
        float rear=AltitudeLayer.Pawn.AltitudeFor()-.2f,front=AltitudeLayer.Pawn.AltitudeFor()+.3f;
        if(map==TurnMap.South)
        {
            PlaygroundGround.Contact(at+new Vector3(0,0,-1.4f),2.2f,.3f);
            (wood?WoodBackWhole:PlainBackWhole).Draw(at,rear,palette:palette);(wood?WoodBackRail:PlainBackRail).Draw(at,front,palette:palette);
            return;
        }
        PlaygroundGround.Contact(at+new Vector3(map==TurnMap.FlipEast?1.4f:-1.4f,0,-.6f),1.3f,.3f);
        (wood?WoodSideWhole:PlainSideWhole)[map==TurnMap.FlipEast?1:0].Draw(at,rear,palette:palette);
    }
}

