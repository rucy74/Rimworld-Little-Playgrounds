using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

[StaticConstructorOnStartup]
public static class PlaygroundTuck
{
    static readonly Type comp=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="TuckedFurniture")?.GetType("TuckedFurniture.CompTuckedFurniture");
    static readonly Func<ThingComp,float> horizontal=Getter("HorizontalOffset"),vertical=Getter("VerticalOffset");
    public static bool Loaded=>horizontal!=null&&vertical!=null;
    static Func<ThingComp,float> Getter(string name)
    {
        var property=comp?.GetProperty(name,BindingFlags.Public|BindingFlags.Instance);
        if(property==null||property.PropertyType!=typeof(float)||!typeof(ThingComp).IsAssignableFrom(comp))return null;
        var p=Expression.Parameter(typeof(ThingComp));
        return Expression.Lambda<Func<ThingComp,float>>(Expression.Property(Expression.Convert(p,comp),property),p).Compile();
    }
    // How far the building is pushed on the ground (x east, z north).
    public static Vector3 Shift(Thing thing)
    {
        if(horizontal==null||vertical==null||thing is not ThingWithComps building)return Vector3.zero;
        var comps=building.AllComps;
        for(int i=0;i<comps.Count;i++)
            if(comps[i].GetType()==comp)return new Vector3(horizontal(comps[i]),0f,vertical(comps[i]));
        return Vector3.zero;
    }
}
