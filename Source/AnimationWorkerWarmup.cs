using System;
using System.Linq;
using Verse;

namespace LittlePlaygrounds;

// RimWorld 1.7 keeps animation workers in GenWorker<T>, a plain Dictionary shared by the game and every mod, which
// PawnRenderNode.AnimationWorker fills lazily from the parallel pawn pre-draw. If two worker types are first needed in
// the same frame (a save loaded with several children mid-ride, a group starting together) the dictionary can be
// corrupted, after which every animated pawn throws while drawing. Creating the worker of every AnimationDef here, on
// the main thread at startup, means the parallel pre-draw only ever reads it. Workers that some mod creates only in
// code, without naming them in an AnimationDef, are still added lazily and are not covered.
[StaticConstructorOnStartup]
public static class AnimationWorkerWarmup
{
    static AnimationWorkerWarmup()
    {
        var fromDefs=DefDatabase<AnimationDef>.AllDefsListForReading
            .SelectMany(d=>(d.keyframeParts?.Values.Cast<AnimationPart>()??Enumerable.Empty<AnimationPart>())
                .Concat(d.curveParts?.Values.Cast<AnimationPart>()??Enumerable.Empty<AnimationPart>()))
            .Select(p=>p.WorkerType);
        var ours=typeof(AnimationWorkerWarmup).Assembly.GetTypes().Where(t=>typeof(BaseAnimationWorker).IsAssignableFrom(t));
        foreach(Type t in fromDefs.Concat(ours).Distinct())
        {
            // GenWorker creates workers with Activator.CreateInstance(type): skip anything it could not create either.
            if(t==null||t.IsAbstract||t.GetConstructor(Type.EmptyTypes)==null)continue;
            try{GenWorker<BaseAnimationWorker>.Get(t);}
            catch(Exception e){Log.Warning($"[Little Playgrounds] Could not pre-create animation worker {t.FullName}: {e.Message}");}
        }
    }
}
