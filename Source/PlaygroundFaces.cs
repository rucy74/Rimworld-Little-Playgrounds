using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace LittlePlaygrounds;

// Optional adapter, called only by our own play rewards. No race/package allowlist,
// patches, new artwork or changes to FA's race/default animation dictionaries.
public static class PlaygroundFaces
{
    static FaceContract contract;
    static bool attempted;
    public static bool Faulted { get; private set; }
    public static void Tick(Pawn p)
    {
        if(!LittlePlaygroundsMod.Settings.facialPlay||Faulted||p?.Spawned!=true)return;
        try
        {
            if(!attempted){attempted=true;contract=FaceContract.TryCreate();}
            if(contract==null)return;
            var map=p.Map.GetComponent<PlaygroundFaceMap>();
            if(p.Dead||p.Downed||p.InMentalState||p.needs?.mood?.CurLevel<.45f||p.health.hediffSet.PainTotal>.20f)
                map.Remove(p);
            else map.Refresh(p,contract);
        }
        catch(Exception e){Fail(e);}
    }
    public static void Stop(Pawn p)=>p?.Map?.GetComponent<PlaygroundFaceMap>()?.Remove(p);
    public static void SettingChanged()
    {
        if(Current.Game!=null)foreach(var map in Find.Maps)map.GetComponent<PlaygroundFaceMap>().Clear();
        if(LittlePlaygroundsMod.Settings.facialPlay){Faulted=false;attempted=false;contract=null;}
    }
    internal static void Fail(Exception e)
    {
        if(Faulted)return;
        Faulted=true;
        // Clear only contributions owned by this adapter, never another mod's animations.
        if(Current.Game!=null)foreach(var map in Find.Maps)map.GetComponent<PlaygroundFaceMap>().Clear();
        Log.Warning("[Little Playgrounds] Optional Facial Animation connection disabled for this session: "+
            (e.InnerException??e).Message+". Playground jobs continue normally. Toggle the facial option off/on to retry.");
    }

    internal sealed class FaceContract
    {
        const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        Type controllerType,mouthType,animationType,definitionType,frameType;
        FieldInfo temporary,frames,mouthShape,duration,optPath;
        PropertyInfo faceType;
        MethodInfo prefix,optPrefix,textureExists,shapeLookup,reset,finished,updateStatus,updateAnimation;
        ConstructorInfo animationCtor;
        readonly Dictionary<string,object> definitions=new();
        readonly Dictionary<string,object> capabilities=new();
        public static FaceContract TryCreate()
        {
            var assembly=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetType("FacialAnimation.FacialAnimationControllerComp",false)!=null);
            if(assembly==null)return null;
            var c=new FaceContract();
            Type T(string name)=>assembly.GetType("FacialAnimation."+name,true);
            c.controllerType=T("FacialAnimationControllerComp");c.mouthType=T("MouthControllerComp");
            c.animationType=T("FaceAnimation");c.definitionType=T("FaceAnimationDef");c.frameType=c.definitionType.GetNestedType("AnimationFrame");
            c.temporary=c.controllerType.GetField("forcedTemporaryAnimationList",All);
            c.frames=c.definitionType.GetField("animationFrames",All);
            c.mouthShape=c.frameType?.GetField("mouthShapeDef");c.duration=c.frameType?.GetField("duration");
            c.faceType=c.mouthType.GetProperty("FaceType");
            c.prefix=T("GraphicHelper").GetMethod("GetTexPathPrefix",new[]{T("FaceTypeDef"),typeof(Gender)});
            c.optPrefix=T("GraphicHelper").GetMethod("GetOptTexPathPrefix",new[]{T("FaceTypeDef"),typeof(Gender)});
            c.optPath=T("FaceTypeDef").GetField("optTexPath");
            c.textureExists=T("GraphicHelper").GetMethod("CheckTexPathExist",new[]{typeof(string)});
            c.shapeLookup=typeof(DefDatabase<>).MakeGenericType(T("MouthShapeDef")).GetMethod("GetNamedSilentFail",new[]{typeof(string)});
            c.animationCtor=c.animationType.GetConstructor(new[]{c.definitionType,typeof(int)});
            c.reset=c.animationType.GetMethod("Reset",new[]{typeof(int)});c.finished=c.animationType.GetMethod("IsFinished",new[]{typeof(int)});
            c.updateStatus=c.controllerType.GetMethod("UpdateStatus",new[]{typeof(int)});c.updateAnimation=c.controllerType.GetMethod("UpdateAnimation",Type.EmptyTypes);
            if(c.temporary?.FieldType!=typeof(List<>).MakeGenericType(c.animationType)||c.frames?.FieldType!=typeof(List<>).MakeGenericType(c.frameType)
                ||c.mouthShape==null||c.duration?.FieldType!=typeof(int)||c.faceType==null||c.prefix==null||c.optPrefix==null||c.optPath?.FieldType!=typeof(string)||c.textureExists==null||c.shapeLookup==null
                ||c.animationCtor==null||c.reset==null||c.finished==null||c.updateStatus==null||c.updateAnimation==null)
                throw new InvalidOperationException("The installed FA interface is not supported");
            return c;
        }
        object Definition(Pawn p,object mouth)
        {
            var type=faceType.GetValue(mouth);if(type==null)return null;
            // FA uses a fixed normal base plus an optional expression overlay when optTexPath is set.
            var lookup=string.IsNullOrEmpty((string)optPath.GetValue(type))?prefix:optPrefix;
            string path=(string)lookup.Invoke(null,new[]{type,(object)p.gender});
            if(capabilities.TryGetValue(path,out var existing))return existing;
            object result=null;
            // A shape enum alone is insufficient: some packs silently fall back to normal.
            foreach(string name in new[]{"smile","happy"})
            {
                var shape=shapeLookup.Invoke(null,new object[]{name});
                if(shape==null||!(bool)textureExists.Invoke(null,new object[]{string.Format(path,name)}))continue;
                if(!definitions.TryGetValue(name,out result))
                {
                    result=Activator.CreateInstance(definitionType);((Def)result).defName="LP_OptionalSmile_"+name;
                    var frame=Activator.CreateInstance(frameType);duration.SetValue(frame,90);mouthShape.SetValue(frame,shape);
                    var list=(IList)Activator.CreateInstance(frames.FieldType);list.Add(frame);frames.SetValue(result,list);
                    definitions.Add(name,result);
                }
                break;
            }
            capabilities[path]=result;return result;
        }
        public FaceBinding Create(Pawn p)
        {
            var controller=p.AllComps.FirstOrDefault(c=>controllerType.IsInstanceOfType(c));
            var mouth=p.AllComps.FirstOrDefault(c=>mouthType.IsInstanceOfType(c));
            return controller==null||mouth==null?null:new FaceBinding(this,controller,mouth,(IList)temporary.GetValue(controller));
        }
        internal sealed class FaceBinding
        {
            readonly FaceContract api;
            readonly object controller,mouth;
            readonly IList list;
            object animation,definition;
            public int Seen,Job;
            public FaceBinding(FaceContract api,object controller,object mouth,IList list)
            {this.api=api;this.controller=controller;this.mouth=mouth;this.list=list??throw new InvalidOperationException("FA animation list missing");}
            public void Refresh(Pawn p,int now)
            {
                Seen=now;Job=p.CurJob?.loadID??-1;
                var desired=api.Definition(p,mouth);
                // Yield to any other mod's live temporary expression.
                bool other=false;foreach(var a in list)if(!ReferenceEquals(a,animation)&&!(bool)api.finished.Invoke(a,new object[]{now})){other=true;break;}
                if(desired==null||other){Remove();return;}
                if(!ReferenceEquals(desired,definition)){Remove();definition=desired;animation=null;}
                // FA's constructor/reset touches Rand; cosmetic maintenance must not consume game RNG.
                Rand.PushState();try
                {
                    if(animation==null)animation=api.animationCtor.Invoke(new[]{definition,(object)now});
                    else api.reset.Invoke(animation,new object[]{now});
                }
                finally{Rand.PopState();}
                if(!list.Contains(animation))list.Add(animation);
            }
            public void Remove()
            {
                if(animation==null||!list.Contains(animation))return;
                list.Remove(animation);
                // Also restore a paused game's visible face immediately after the settings switch.
                api.updateStatus.Invoke(controller,new object[]{Find.TickManager.TicksGame});
                api.updateAnimation.Invoke(controller,null);
            }
        }
    }
}

// Only active riders are retained, only in their owning map. Not serialized; a loaded
// job reconnects on its next reward tick. No static pawn dictionary across saves.
public sealed class PlaygroundFaceMap:MapComponent
{
    sealed class Visit
    {
        public PlaygroundFaces.FaceContract.FaceBinding Binding;
        public int Seen,Job;
    }
    readonly Dictionary<Pawn,Visit> active=new();
    int nextSweep;
    public PlaygroundFaceMap(Map map):base(map){}
    internal void Refresh(Pawn p,PlaygroundFaces.FaceContract api)
    {
        int now=Find.TickManager.TicksGame;
        if(!active.TryGetValue(p,out var visit)){visit=new Visit{Seen=now-30};active.Add(p,visit);}
        // Jobs can tick in batches. An exact tick % 30 gate can miss every update.
        if(now-visit.Seen<30)return;
        visit.Seen=now;visit.Job=p.CurJob?.loadID??-1;
        visit.Binding??=api.Create(p);visit.Binding?.Refresh(p,now);
    }
    public void Remove(Pawn p)
    {
        if(!active.TryGetValue(p,out var visit))return;active.Remove(p);
        try{visit.Binding?.Remove();}catch(Exception e){PlaygroundFaces.Fail(e);}
    }
    public void Clear(){foreach(var p in active.Keys.ToArray())Remove(p);}
    public override void MapComponentTick()
    {
        if(active.Count==0)return;
        int now=Find.TickManager.TicksGame;if(now<nextSweep)return;nextSweep=now+30;
        foreach(var p in active.Keys.ToArray())
            if(active.TryGetValue(p,out var b)&&(!LittlePlaygroundsMod.Settings.facialPlay||p.Destroyed||!p.Spawned||p.Map!=map||p.CurJob?.loadID!=b.Job||now-b.Seen>45))Remove(p);
    }
}
