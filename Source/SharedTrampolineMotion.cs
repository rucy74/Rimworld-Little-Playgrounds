using System;
using UnityEngine;

namespace LittlePlaygrounds;

// Landing points stay in three disjoint sectors. The complete flight segment
// stays inside its sector, even when visitors arrive at unrelated times.
public static class SharedTrampolineMotion
{
    static uint Pick(int seed,int salt)
    {unchecked{uint n=(uint)seed+0x9e3779b9u*(uint)(salt+1);n^=n>>16;n*=0x7feb352du;n^=n>>15;n*=0x846ca68bu;return n^(n>>16);}}
    public static int Period(int slot,int seed)=>70+slot*9+(int)(Pick(seed,0)%9);
    public static float Phase(int age,int slot,int seed)=>Mathf.Repeat(Math.Max(0,age-60)/(float)Period(slot,seed),1);
    public static int Cycle(int age,int slot,int seed)=>Math.Max(0,age-60)/Period(slot,seed);
    public static Vector3 Landing(int slot,int seed,int step)
    {
        float angle=(slot==0?240:slot==1?120:0)+((int)(Pick(seed,10+step*2)%401)-200)*.1f;
        float radius=.96f+(Pick(seed,11+step*2)%30)*.01f;
        float a=angle*Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(a)*radius,0,Mathf.Cos(a)*radius*.82f);
    }
    public static Vector3 Contact(int age,int slot,int seed)
    {
        int cycle=Cycle(age,slot,seed);float phase=Phase(age,slot,seed);
        return Vector3.Lerp(Landing(slot,seed,cycle),Landing(slot,seed,cycle+1),SwingMotion.Smooth((phase-.2f)/.8f));
    }
    public static Vector3 Direction(int age,int slot,int seed)
    {int cycle=Cycle(age,slot,seed);return Landing(slot,seed,cycle+1)-Landing(slot,seed,cycle);}
    public static float Height(int age,int slot,int seed)
    {
        float phase=Phase(age,slot,seed),envelope=ActiveRideMotion.BounceEnvelope(age);
        if(phase<.2f)return -.07f*Mathf.Sin(phase/.2f*Mathf.PI)*envelope;
        float t=(phase-.2f)/.8f;return 1.25f*4*t*(1-t)*envelope;
    }
    public static float Flip(int age,int slot,int seed)
    {
        int cycle=Cycle(age,slot,seed),period=Period(slot,seed);
        // Decide for the whole flight, not anew at each frame. Low take-offs and
        // the final settling bounces never turn upside down.
        if(cycle<1||Pick(seed,200+cycle)%4!=0||ActiveRideMotion.BounceEnvelope(60+cycle*period+period/2)<.72f)return 0;
        return 360*SwingMotion.Smooth((Phase(age,slot,seed)-.35f)/.40f);
    }
}
