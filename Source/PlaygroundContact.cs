using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

// Read the native body silhouette once per visit, on the simulation thread.
// Moving the whole pawn preserves race/child proportions and all attached apparel.
public static class PlaygroundContact
{
    static readonly Dictionary<Texture2D,float> Bottoms=new();
    public static float[] MeasureFeet(Pawn pawn)
    {
        var result=new float[4];
        for(int i=0;i<4;i++)result[i]=pawn.DevelopmentalStage.Child()?.28f:.43f;
        // A restored/teleported actor may not have been drawn yet. Initialise its
        // own render tree through the native API before asking for body graphics.
        pawn.Drawer.renderer.renderTree.EnsureInitialized(PawnDrawParms.DefaultFor(pawn).flags);
        if(!pawn.Drawer.renderer.renderTree.TryGetNodeByTag(PawnRenderNodeTagDefOf.Body,out var body)
            ||body.PrimaryGraphic==null)return result;
        for(int i=0;i<4;i++)
        {
            var parms=PawnDrawParms.DefaultFor(pawn);parms.facing=new Rot4(i);
            var texture=body.PrimaryGraphic.MatAt(parms.facing).mainTexture as Texture2D;
            var mesh=body.GetMesh(parms);
            if(texture==null||mesh==null)continue;
            if(!Bottoms.TryGetValue(texture,out float bottom))
            {
                // Some external texture providers do not expose readable pixels.
#if !RW16
                if(!texture.isReadable)continue;
#endif
                var pixels=PlaygroundPixels.Read32(texture);int row=0;
                for(;row<texture.height;row++)
                {
                    int count=0;for(int x=0;x<texture.width;x++)if(pixels[row*texture.width+x].a>=96)count++;
                    if(count>=Math.Max(2,texture.width/24))break;
                }
                if(row==texture.height)continue;
                Bottoms[texture]=bottom=row/(float)texture.height;
            }
            body.GetTransform(parms,out var offset,out _,out _,out var scale);
            float z=(mesh.bounds.min.z+mesh.bounds.size.z*bottom)*scale.z+offset.z;
            result[i]=Mathf.Clamp(-z,.08f,1.1f);
        }
        return result;
    }

    // Atlas pixel centres, using exactly the transform used by RestyleArt.Hopscotch.
    public static readonly Vector3[] HopMarks={Hop(1245,899),Hop(1191,822),Hop(1297,822),Hop(1244,751),
        Hop(1201,685),Hop(1287,685),Hop(1192,622),Hop(1294,622)};
    static Vector3 Hop(float x,float y)=>new((x-1252)*.0061f,0,(769-y)*.0117f);
    public static void Step(int age,int count,int ticks,out int from,out int to,out float flight)
    {
        float t=Math.Max(0,age-60)/(float)ticks;int step=(int)t,period=(count-1)*2;
        int Index(int n){n%=period;return n<count?n:period-n;}
        from=Index(step);to=Index(step+1);
        // A visible landing/weight shift before the next hop, rather than constant gliding.
        flight=Mathf.Clamp01((t-step-.25f)/.75f);
    }
}
