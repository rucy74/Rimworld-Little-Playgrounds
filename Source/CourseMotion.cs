using UnityEngine;

namespace LittlePlaygrounds;

public static class CourseMotion
{
    public const int RecoveryTicks=36;
    static uint Hash(int seed)
    {unchecked{uint n=(uint)seed;n^=n>>16;n*=0x7feb352d;n^=n>>15;n*=0x846ca68b;n^=n>>16;return n;}}
    // One decision when a visit starts. Short two-point courses have no interior landing.
    public static int Plan(int seed,bool child,int points)
    {
        if(points<3||Hash(seed)%1000>=(child?120u:50u))return -1;
        int landing=1+(int)(Hash(seed^0x5c71329)%(uint)(points-2));
        return 60+landing*50;
    }
    public static int Side(int seed)=>Hash(seed^0x29415)%2==0?1:-1;
    public static int Clock(int age,int at)=>at<0?age:age-Mathf.Clamp(age-at,0,RecoveryTicks);
    public static float Lean(int age,int at,int side,bool child)
    {
        if(at<0||age<=at||age>=at+RecoveryTicks)return 0;
        float t=(age-at)/(float)RecoveryTicks;
        float lean=t<.32f?SwingMotion.Smooth(t/.32f):t<.72f?1-1.32f*SwingMotion.Smooth((t-.32f)/.40f):-.32f*(1-SwingMotion.Smooth((t-.72f)/.28f));
        return lean*side*(child?9f:5f);
    }
}
