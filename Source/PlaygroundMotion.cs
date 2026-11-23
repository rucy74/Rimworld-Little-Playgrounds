using UnityEngine;

namespace LittlePlaygrounds;

// Tick-derived motion: a rider pumps, coasts and tries again. No render-time RNG.
// Jobs retain phase origins; the seesaw also saves its shared transition angle and velocity.
public static class PlaygroundMotion
{
    static float Wave(float t)=>.5f+.5f*Mathf.Sin(t*2*Mathf.PI);
    public static float SwingStrength(int age,int slot=0)
    {
        float phase=Mathf.Repeat(age/(480f+slot*31)+slot*.23f,1);
        // A short effort builds momentum; a longer coast gradually loses it.
        float effort=phase<.30f?SwingMotion.Smooth(phase/.30f):1-SwingMotion.Smooth((phase-.30f)/.70f);
        return .52f+.48f*effort;
    }
    public static float SpringRock(int age)=>
        (3.5f+9.5f*Mathf.Pow(Wave(age/340f),2))*Mathf.Sin(age*2*Mathf.PI/83f+.22f*Mathf.Sin(age/110f));
    public static float SeesawAngle(int age)=>age<0||age>=1080?0:
        (8+7*Wave(age/460f)) * SwingMotion.Smooth(age/120f)*SwingMotion.Smooth((1080-age)/180f)
        *Mathf.Sin(age*2*Mathf.PI/174f+.20f*Mathf.Sin(age/150f));
    // One rider pushes gently from the low end; the occupied seat never crosses the centre.
    public static float SoloSeesawAngle(int age,int slot)
    {
        float push=Mathf.Pow(Wave(age/126f+.12f*Mathf.Sin(age/310f)),2);
        return (slot==0?-1:1)*(10-(2.2f+.8f*Wave(age/410f))*push);
    }
    // Called once per game tick, never from Draw. A mode change keeps both position and velocity continuous.
    public static void StepSeesaw(ref float angle,ref float velocity,float target)
    {velocity=(velocity+(target-angle)*.006f)*.84f;angle+=velocity;}
    public static float RoundTarget(int tick,int seed)
    {
        float phase=Mathf.Repeat((tick+seed%257)/280f,1);
        float push=phase<.36f?Mathf.Pow(Mathf.Sin(phase/.36f*Mathf.PI),2):0;
        return .48f+1.22f*push;
    }
    public static float SandPhase(int age,int slot)=>Mathf.Repeat((age+slot*57)/190f,1);
    public static float SandScoop(int age,int slot)
    {
        float t=SandPhase(age,slot);
        return t<.48f?Mathf.Pow(Mathf.Sin(t/.48f*Mathf.PI),2):0;
    }
    public static float SandPat(int age,int slot)
    {
        float t=SandPhase(age,slot);
        return t>=.52f&&t<.80f?Mathf.Pow(Mathf.Sin((t-.52f)/.14f*Mathf.PI),2):0;
    }
    public static Vector3 SandReach(int age,int slot)
    {
        float reach=.16f*SandScoop(age,slot)+.065f*SandPat(age,slot);
        return slot==0?new(reach,0,0):slot==1?new(-reach,0,0):new(0,0,reach);
    }
}
