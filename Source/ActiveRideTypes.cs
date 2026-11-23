using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

// Shared geometry for the native job and artwork. Keep ordinary pawn proportions and map axes.
public enum ActiveRideKind { Trampoline, Zipline, SharedTrampoline }
public sealed class ActiveRideProperties:DefModExtension { public ActiveRideKind kind; }
public static class ActiveRideMotion
{
    public const int TrampolineTicks=1080,ZiplineTicks=450,ReturnTicks=240,ZipStart=60,ZipEnd=340;
    public const float ZipHalfSpan=3.1f;
    public const float TrampolineLift=.24f;
    public static float BounceEnvelope(int age)=>age<60||age>=TrampolineTicks-60?0:
        SwingMotion.Smooth((age-60)/150f)*SwingMotion.Smooth((TrampolineTicks-60-age)/150f)
        *(.68f+.32f*Mathf.Pow(Mathf.Sin((age-60)*Mathf.PI/360f),2));
    public static float BouncePhase(int age)=>Mathf.Repeat(Mathf.Max(0,age-60)/72f,1);
    public static float BounceHeight(int age)
    {
        float phase=BouncePhase(age),envelope=BounceEnvelope(age);
        if(phase<.18f)return -.10f*Mathf.Sin(phase/.18f*Mathf.PI)*envelope;
        float t=(phase-.18f)/.82f;return 1.25f*4*t*(1-t)*envelope;
    }
    public static float BedCompression(int age)=>Mathf.Max(0,-BounceHeight(age));
    // Only two high flights per visit. Finish the full turn above the mat;
    // the other bounces keep their ordinary pose and height.
    public static float FlipAngle(int age)
    {
        if(age<60)return 0;
        int cycle=(age-60)/72;
        if(cycle!=2&&cycle!=7)return 0;
        return 360*SwingMotion.Smooth((BouncePhase(age)-.32f)/.52f);
    }
    public static bool ZipMoving(int age)=>age>=ZipStart&&age<ZipEnd;
    public static float ZipProgress(int age)
    {
        float t=Mathf.Clamp01((age-ZipStart)/(float)(ZipEnd-ZipStart));
        const float a=.42f,b=.68f,d=1-b,v=1/(a*.5f+(b-a)+d*.5f);
        if(t<a)return v*(t*.5f-a/(2*Mathf.PI)*Mathf.Sin(Mathf.PI*t/a));
        if(t<b)return v*(a*.5f+t-a);
        float u=t-b;
        return Mathf.Clamp01(v*(a*.5f+(b-a)+u*.5f+d/(2*Mathf.PI)*Mathf.Sin(Mathf.PI*u/d)));
    }
    // Lean back as the trolley pulls away, swing through on braking, then settle.
    public static float ZipSway(int age)
    {
        if(age<=60||age>=390)return 0;
        float t=(age-60)/280f;
        float pull=t<.42f?-7*Mathf.Sin(t/.42f*Mathf.PI):t>=.68f&&t<1?9*Mathf.Sin((t-.68f)/.32f*Mathf.PI):0;
        float coast=3.8f*Mathf.Sin((age-60)*2*Mathf.PI/93f)*SwingMotion.Smooth((age-60)/65f)*SwingMotion.Smooth((390-age)/80f);
        return pull+coast;
    }
    public static float CableHeight(float progress)=>2.9f-.24f*4*progress*(1-progress);
    public static Vector3 ZipSeat(float progress,float sway=0)
    {
        float a=sway*Mathf.Deg2Rad;
        return new(Mathf.Lerp(-ZipHalfSpan,ZipHalfSpan,progress)+2.34f*Mathf.Sin(a),0,CableHeight(progress)-2.34f*Mathf.Cos(a));
    }
    public static Vector3 ZipTrolley(float progress)=>new(Mathf.Lerp(-ZipHalfSpan,ZipHalfSpan,progress),0,CableHeight(progress));
}

// Runtime contract implemented by ActiveRides.cs:
// Building_ActiveRide: Kind, CanUse(Pawn,bool), EntryCell, ExitCell, RiderJob,
//                     CarProgress (0..1), Returning, SetRider(Pawn).
// JobDriver_ActiveRide: Age, Ride, PoseHeight; Age=-1 until physically mounted.
// Renderer supplied by the main: ActiveRideArt.Draw(Building_ActiveRide,Vector3).
// Child selection integration and adult JoyGiver switch are owned by the main.
