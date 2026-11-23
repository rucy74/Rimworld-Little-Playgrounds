using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LittlePlaygrounds;

public enum LPSfx { SandDig, SandPour, WaterSplash, SwingCreak, TrampolineBounce, RoundaboutSpin, SeesawCreak, SpringRiderCreak, RockerCreak, FountainDrink }

public static class LPSound
{
    public const int Kinds=10,MaxPerFrame=3;
    // In settings-list order. The two trampolines share one recording but switch separately.
    public static readonly string[] Rides={"LP_Swing","LP_Seesaw","LP_Zipline","LP_Trampoline","LP_SharedTrampoline","LP_Sandbox",
        "LP_SpringFox","LP_Roundabout","LP_FloorRocker","LP_SplashPad","LP_DrinkingFountain"};
    // Shortest real-time gap per kind, so many riders or a fast game speed never stack one sound into a buzz.
    static readonly float[] Gap={.35f,1.2f,.3f,.45f,.16f,.8f,.45f,.55f,.55f,1f};
    static readonly float[] nextAt=new float[Kinds],candDist=new float[Kinds];
    static readonly bool[] has=new bool[Kinds];
    static readonly IntVec3[] cand=new IntVec3[Kinds];
    static readonly System.Random rng=new();
    public static readonly int[] Requested=new int[Kinds],Played=new int[Kinds];
    public static int SprayStarted,RollStarted;

    public static bool On=>LittlePlaygroundsMod.Settings.sounds&&LittlePlaygroundsMod.Settings.soundVolume>0f;
    public static bool RideOn(Thing ride)=>!LittlePlaygroundsMod.Settings.mutedRides.Contains(ride.def.defName);
    static SoundDef Def(LPSfx k)=>k switch
    {
        LPSfx.SandDig=>LPSoundDefOf.LP_SandDig,LPSfx.SandPour=>LPSoundDefOf.LP_SandPour,LPSfx.WaterSplash=>LPSoundDefOf.LP_WaterSplash,
        LPSfx.SwingCreak=>LPSoundDefOf.LP_SwingCreak,LPSfx.TrampolineBounce=>LPSoundDefOf.LP_TrampolineBounce,
        LPSfx.RoundaboutSpin=>LPSoundDefOf.LP_RoundaboutSpin,LPSfx.SeesawCreak=>LPSoundDefOf.LP_SeesawCreak,
        LPSfx.SpringRiderCreak=>LPSoundDefOf.LP_SpringRiderCreak,
        LPSfx.RockerCreak=>LPSoundDefOf.LP_RockerCreak,_=>LPSoundDefOf.LP_FountainDrink
    };
    // Of all requests of one kind in a frame, keep the one nearest the camera centre.
    public static void Request(LPSfx k,IntVec3 at)
    {
        int i=(int)k;Requested[i]++;
        float d=(at.ToVector3Shifted()-Find.CameraDriver.MapPosition.ToVector3Shifted()).MagnitudeHorizontalSquared();
        if(has[i]&&d>=candDist[i])return;
        has[i]=true;cand[i]=at;candDist[i]=d;
    }
    public static void Flush(Map map)
    {
        float now=Time.realtimeSinceStartup;int made=0;
        for(int i=0;i<Kinds;i++)
        {
            if(!has[i])continue;
            has[i]=false;
            if(made>=MaxPerFrame||now<nextAt[i])continue;
            nextAt[i]=now+Gap[i]*(1f+.3f*(float)rng.NextDouble());
            if(Play(Def((LPSfx)i),map,cand[i])){Played[i]++;made++;}
        }
    }
    static bool Play(SoundDef def,Map map,IntVec3 c)
    {
        if(def==null||!c.InBounds(map))return false;
        var info=SoundInfo.InMap(new TargetInfo(c,map));info.volumeFactor=LittlePlaygroundsMod.Settings.soundVolume;
        Rand.PushState(rng.Next());
        try{def.PlayOneShot(info);}finally{Rand.PopState();}
        return true;
    }
    public static Sustainer Loop(SoundDef def,Thing at)
    {
        if(def==null)return null;
        var info=SoundInfo.InMap(at,MaintenanceType.PerFrame);info.volumeFactor=LittlePlaygroundsMod.Settings.soundVolume;
        Rand.PushState(rng.Next());
        try
        {
            var s=def.TrySpawnSustainer(info);
            if(s!=null){if(def==LPSoundDefOf.LP_ZiplineRoll)RollStarted++;else SprayStarted++;}
            return s;
        }
        finally{Rand.PopState();}
    }
    public static void Forget(){for(int i=0;i<Kinds;i++){has[i]=false;nextAt[i]=0f;}}
}

public sealed class PlaygroundSounds:MapComponent
{
    const int MaxStep=40;   // a larger jump (load, very fast speed) is skipped rather than replayed
    sealed class Track{public int job,age;}
    sealed class Rocker{public float value,slope;}
    readonly Dictionary<Pawn,Track> riders=new();
    readonly Dictionary<Thing,Rocker> rockers=new();
    readonly Dictionary<Thing,int> halfTurns=new(),castles=new();
    readonly Dictionary<Thing,Sustainer> sprays=new(),rolls=new();
    readonly HashSet<Pawn> seenRiders=new();
    readonly HashSet<Thing> active=new(),wet=new(),rolling=new();
    readonly List<Pawn> goneRiders=new();
    readonly List<Thing> goneThings=new();
    public PlaygroundSounds(Map map):base(map){}
    public Sustainer Roll(Thing ride)=>rolls.TryGetValue(ride,out var s)?s:null;
    public int RollingNow{get{int n=0;foreach(var s in rolls.Values)if(s!=null&&!s.Ended)n++;return n;}}

    public override void MapComponentUpdate()
    {
        if(map!=Find.CurrentMap||!LPSound.On){Clear();return;}
        seenRiders.Clear();active.Clear();wet.Clear();rolling.Clear();
        var pawns=map.mapPawns.AllPawnsSpawned;
        for(int i=0;i<pawns.Count;i++)Rider(pawns[i]);
        foreach(var b in active)Facility(b);
        Loops(wet,sprays,LPSoundDefOf.LP_SplashPadSpray);
        Loops(rolling,rolls,LPSoundDefOf.LP_ZiplineRoll);
        Forget();
        LPSound.Flush(map);
    }
    public override void MapRemoved(){Clear();}

    // Ages between the last frame and this one, after the same job; a new job or a jump starts over without sounds.
    bool Step(Pawn p,int age,out int from)
    {
        from=age;
        if(age<0)return false;
        seenRiders.Add(p);
        int job=p.CurJob?.loadID??-1;
        if(!riders.TryGetValue(p,out var r)){riders[p]=new Track{job=job,age=age};return false;}
        bool ok=r.job==job&&age>r.age&&age-r.age<=MaxStep;
        from=r.age;r.job=job;r.age=age;
        return ok;
    }

    void Rider(Pawn p)
    {
        switch(p.jobs?.curDriver)
        {
            case JobDriver_RideSwing s when s.job.targetA.Thing is Thing swing&&LPSound.RideOn(swing):
                if(Step(p,s.Age,out int f1))
                    for(int t=f1+1;t<=s.Age;t++)
                    {
                        float a0=SwingMotion.Angle(t-2,s.Slot),a1=SwingMotion.Angle(t-1,s.Slot),a2=SwingMotion.Angle(t,s.Slot);
                        if((a1-a0)*(a2-a1)<0&&Mathf.Abs(a1)>.12f)LPSound.Request(LPSfx.SwingCreak,swing.Position);
                    }
                break;
            case JobDriver_RideSeesaw d when d.job.targetA.Thing is Building_Seesaw seesaw&&LPSound.RideOn(seesaw):
                if(d.Age>=0)active.Add(seesaw);
                break;
            case JobDriver_ActiveRide a when a.Ride is Building_ActiveRide ride&&LPSound.RideOn(ride):
                if(ride.Kind==ActiveRideKind.Zipline){if(ActiveRideMotion.ZipMoving(a.Age))rolling.Add(ride);break;}
                if(!Step(p,a.Age,out int f2))break;
                for(int t=f2+1;t<=a.Age;t++)
                    if(t>60&&(t-60)%72==0&&ActiveRideMotion.BounceEnvelope(t)>.25f)LPSound.Request(LPSfx.TrampolineBounce,ride.Position);
                break;
            case JobDriver_VisitPlayground v when v.Facility is Building_PlaygroundFacility b&&LPSound.RideOn(b):
                Visit(p,v,b);
                break;
            case JobDriver_GardenVisit g when g.job.targetA.Thing is Building_Sandbox sand&&LPSound.RideOn(sand):
                active.Add(sand);
                if(Step(p,g.Age,out int f3))
                    for(int t=f3+1;t<=g.Age;t++)
                        if(t>=60&&t<JobDriver_GardenVisit.Duration-75&&(t+g.Slot*57)%190==0)LPSound.Request(LPSfx.SandDig,p.Position);
                break;
        }
    }

    void Visit(Pawn p,JobDriver_VisitPlayground v,Building_PlaygroundFacility b)
    {
        switch(b.Kind)
        {
            case ExpansionKind.Roundabout: case ExpansionKind.SpringRider: case ExpansionKind.FloorRocker:
                if(v.Age>=0)active.Add(b);
                return;
        }
        if(!Step(p,v.Age,out int from))return;
        int age=v.Age,end=v.Duration-60;
        switch(b.Kind)
        {
            case ExpansionKind.SharedTrampoline:
                for(int t=from+1;t<=age;t++)
                    if(t>60&&t<end&&SharedTrampolineMotion.Cycle(t,v.Slot,v.MotionSeed)!=SharedTrampolineMotion.Cycle(t-1,v.Slot,v.MotionSeed))
                        LPSound.Request(LPSfx.TrampolineBounce,p.Position);
                break;
            case ExpansionKind.SplashPad:
                if(age>=60&&age<end)wet.Add(b);
                int every=95+v.Slot*13,shift=(v.MotionSeed&0x7fffffff)%47;
                for(int t=from+1;t<=age;t++)
                    if(t>=60&&t<end&&(t+shift)%every==0)LPSound.Request(LPSfx.WaterSplash,p.Position);
                break;
            case ExpansionKind.Drink:
                if(from<70&&age>=70)LPSound.Request(LPSfx.FountainDrink,b.Position);
                break;
        }
    }

    void Facility(Thing t)
    {
        switch(t)
        {
            case Building_Seesaw s: Turn(s,s.Angle,4f,LPSfx.SeesawCreak);break;
            case Building_Sandbox sand:
                int stage=sand.Stage;
                if(castles.TryGetValue(sand,out int was)&&stage>was)LPSound.Request(LPSfx.SandPour,sand.Position);
                castles[sand]=stage;
                break;
            case Building_PlaygroundFacility b when b.Kind==ExpansionKind.SpringRider: Turn(b,b.Rock,5f,LPSfx.SpringRiderCreak);break;
            case Building_PlaygroundFacility b when b.Kind==ExpansionKind.FloorRocker: Turn(b,CompactMotion.Rock(b),4f,LPSfx.RockerCreak);break;
            case Building_PlaygroundFacility b when b.Kind==ExpansionKind.Roundabout:
                int half=(int)(b.SpinAngle/180f);
                if(halfTurns.TryGetValue(b,out int last)&&half!=last&&b.SpinSpeed>.25f)LPSound.Request(LPSfx.RoundaboutSpin,b.Position);
                halfTurns[b]=half;
                break;
        }
    }

    // A creak where a rocking value turns back, once it has swung far enough; a paused game keeps the last slope.
    void Turn(Thing b,float value,float least,LPSfx k)
    {
        if(!rockers.TryGetValue(b,out var r)){rockers[b]=new Rocker{value=value};return;}
        float slope=value-r.value;
        if(slope!=0f)
        {
            if(r.slope*slope<0f&&Mathf.Abs(r.value)>least)LPSound.Request(k,b.Position);
            r.slope=slope;
        }
        r.value=value;
    }

    void Loops(HashSet<Thing> on,Dictionary<Thing,Sustainer> playing,SoundDef def)
    {
        foreach(var t in on)
        {
            if(!playing.TryGetValue(t,out var s)||s==null||s.Ended)playing[t]=s=LPSound.Loop(def,t);
            s?.Maintain();
        }
        goneThings.Clear();
        foreach(var kv in playing)if(!on.Contains(kv.Key))goneThings.Add(kv.Key);
        foreach(var t in goneThings){var s=playing[t];if(s!=null&&!s.Ended)s.End();playing.Remove(t);}
    }

    void Forget()
    {
        goneRiders.Clear();
        foreach(var p in riders.Keys)if(!seenRiders.Contains(p))goneRiders.Add(p);
        foreach(var p in goneRiders)riders.Remove(p);
        Prune(rockers);Prune(halfTurns);Prune(castles);
    }
    void Prune<T>(Dictionary<Thing,T> d)
    {
        goneThings.Clear();
        foreach(var b in d.Keys)if(!active.Contains(b))goneThings.Add(b);
        foreach(var b in goneThings)d.Remove(b);
    }
    void Clear()
    {
        riders.Clear();rockers.Clear();halfTurns.Clear();castles.Clear();
        foreach(var s in sprays.Values)if(s!=null&&!s.Ended)s.End();
        foreach(var s in rolls.Values)if(s!=null&&!s.Ended)s.End();
        sprays.Clear();rolls.Clear();
    }
}

[DefOf]
public static class LPSoundDefOf
{
    public static SoundDef LP_SandDig,LP_SandPour,LP_WaterSplash,LP_SplashPadSpray,LP_SwingCreak,LP_TrampolineBounce,LP_RoundaboutSpin,
        LP_SeesawCreak,LP_SpringRiderCreak,LP_ZiplineRoll,LP_RockerCreak,LP_FountainDrink;
    static LPSoundDefOf(){DefOfHelper.EnsureInitializedInCtor(typeof(LPSoundDefOf));}
}
