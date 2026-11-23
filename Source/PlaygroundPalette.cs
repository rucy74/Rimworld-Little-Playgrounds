using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

// Stable save IDs. Legacy is deliberately not offered as a fifth new design.
public enum PlaygroundPalette { Legacy=0, Blue=1, Yellow=2, Sage=3, Classic=4 }
public sealed class CompProperties_PlaygroundPalette:CompProperties
{
    public CompProperties_PlaygroundPalette(){compClass=typeof(CompPlaygroundPalette);}
}
public sealed class CompPlaygroundPalette:ThingComp
{
    PlaygroundPalette palette=PlaygroundPalette.Blue;
    public PlaygroundPalette Palette=>palette;
    public void Select(PlaygroundPalette value)
    {
        palette=PaletteArt.Valid(value)?value:PlaygroundPalette.Legacy;
        parent.Notify_ColorChanged();
    }
    public override void PostExposeData()
    {
        base.PostExposeData();
        // Old saves have no field: retain their original appearance, rather than
        // silently repainting every installed ride when the mod is updated.
        Scribe_Values.Look(ref palette,"lpPalette",PlaygroundPalette.Legacy);
        if(!PaletteArt.Valid(palette))palette=PlaygroundPalette.Legacy;
    }
    public List<FloatMenuOption> Options()=>PaletteArt.Choices.Select(value=>new FloatMenuOption(
        PaletteArt.Label(value)+(palette==value?" ✓":""),()=>Select(value))).ToList();
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if(parent.Faction!=Faction.OfPlayerSilentFail)yield break;
        yield return new Command_Action {
            defaultLabel="LP_PaletteButton".Translate(),
            defaultDesc="LP_PaletteDescription".Translate(PaletteArt.Label(palette)),
            icon=PaletteArt.Swatch(palette),
            action=()=>Find.WindowStack.Add(new FloatMenu(Options()))
        };
    }
    public override string CompInspectStringExtra()=>"LP_PaletteCurrent".Translate(PaletteArt.Label(palette));
}

public static class PaletteArt
{
    public static readonly PlaygroundPalette[] Choices={PlaygroundPalette.Blue,PlaygroundPalette.Yellow,PlaygroundPalette.Sage,PlaygroundPalette.Classic};
    public static bool Valid(PlaygroundPalette value)=>(int)value>=0&&(int)value<=4;
    public static PlaygroundPalette Of(Thing thing)=>thing?.TryGetComp<CompPlaygroundPalette>()?.Palette??PlaygroundPalette.Legacy;
    public static string Label(PlaygroundPalette value)=>("LP_Palette"+value).Translate();
    public static readonly Color Blue=new(.24f,.57f,.78f),Yellow=new(.95f,.77f,.25f),Sage=new(.56f,.62f,.46f),
        Wood=new(.80f,.60f,.36f),Red=new(.89f,.27f,.22f),Green=new(.30f,.65f,.31f);
    public static Color Accent(PlaygroundPalette value)=>value==PlaygroundPalette.Yellow?Yellow:value==PlaygroundPalette.Sage?Sage:Blue;
    static readonly Dictionary<PlaygroundPalette,Texture2D> swatches=new();
    static readonly Dictionary<(string,PlaygroundPalette),Texture2D> textures=new();
    static readonly Dictionary<(string,PlaygroundPalette),Material> materials=new();
    public static int CachedTextureCount=>textures.Count;
    public static Texture2D Swatch(PlaygroundPalette value)
    {
        if(swatches.TryGetValue(value,out var found))return found;
        var pixels=new Color[32*32];
        for(int y=0;y<32;y++)for(int x=0;x<32;x++)pixels[y*32+x]=value==PlaygroundPalette.Classic?
            (x<16?(y<16?Red:Blue):(y<16?Yellow:Green)):value==PlaygroundPalette.Legacy?Sage:Accent(value);
        var texture=new Texture2D(32,32,TextureFormat.RGBA32,false){name="LP palette swatch "+value};
        texture.SetPixels(pixels);texture.Apply();return swatches[value]=texture;
    }
    public static Material Material(string path,PlaygroundPalette palette)
    {
        var key=(path,palette);
        if(materials.TryGetValue(key,out var material))return material;
        return materials[key]=MaterialPool.MatFrom(new MaterialRequest(Texture(path,palette),ShaderDatabase.Cutout,Color.white));
    }
    public static Texture2D Texture(string path,PlaygroundPalette palette)
    {
        var original=ContentFinder<Texture2D>.Get(path);
        if(palette==PlaygroundPalette.Legacy)return original;
        var key=(path,palette);if(textures.TryGetValue(key,out var existing))return existing;
        var pixels=PlaygroundPixels.Read(original);
        // Padding seams are irregular in the drawing. Follow connected painted
        // pads, rather than slicing one pad into two colors with angle wedges.
        var pads=path.EndsWith("TrampolineV1",StringComparison.Ordinal)&&palette==PlaygroundPalette.Classic?
            PadRegions(pixels,original.width,original.height):null;
        var drawn=DrawnSize(path,original);
        for(int y=0;y<original.height;y++)for(int x=0;x<original.width;x++)
        {
            int i=y*original.width+x;
            pixels[i]=pads!=null&&pads[i]>=0?Paint(pixels[i],ClassicColor(pads[i]),.56f):
                Recolor(path,x*drawn.x/original.width,(original.height-1-y)*drawn.y/original.height,pixels[i],palette);
        }
        // One shared bitmap per design and palette, never per building or frame.
        var result=new Texture2D(original.width,original.height,TextureFormat.RGBA32,false){
            name="LP "+path+" "+palette,filterMode=original.filterMode,wrapMode=TextureWrapMode.Clamp};
        result.SetPixels(pixels);result.Apply();textures[key]=result;return result;
    }
    // Recolor's positions are in the pixels a picture was drawn at; the files ship smaller (PlaygroundPixels.Region).
    static Vector2Int DrawnSize(string path,Texture2D file)
    {
        string name=path.Substring(path.LastIndexOf("Building/",StringComparison.Ordinal)+9);
        return name switch
        {
            "Timber/swing" or "Timber/trampoline" or "Timber/rounddeck" or "Selectedswing" or "TrampolineV1" or "ZiplineAtlasV1"=>new(1254,1254),
            "Timber/slide"=>new(1145,1374),"Timber/fox"=>new(1536,1024),"Timber/seesaw"=>new(1942,809),
            "SlidePolishV2"=>new(1165,1350),"Selectedseesaw"=>new(2172,724),_=>new(file.width,file.height)
        };
    }
    static Color Paint(Color source,Color target,float reference)
    {
        float luminance=source.r*.299f+source.g*.587f+source.b*.114f;
        // Preserve soft highlights without channel clipping turning them neon.
        var result=luminance<=reference?target*(luminance/reference):
            Color.Lerp(target,Color.white,(luminance-reference)/(1-reference)*.65f);
        result.a=source.a;return result;
    }
    static bool Olive(Color c)=>c.g>c.r*1.005f&&c.g>c.b*1.07f&&c.maxColorComponent>.24f;
    static Color ClassicColor(int n)=>(n%4) switch {0=>Red,1=>Yellow,2=>Blue,_=>Green};
    static int[] PadRegions(Color[] pixels,int width,int height)
    {
        var regions=new int[pixels.Length];for(int i=0;i<regions.Length;i++)regions[i]=-1;
        var seen=new bool[pixels.Length];var queue=new Queue<int>();var component=new List<int>();
        var pads=new List<(float angle,List<int> pixels)>();
        for(int start=0;start<pixels.Length;start++)
        {
            if(seen[start]||pixels[start].a<.5f||!Olive(pixels[start]))continue;
            component.Clear();queue.Enqueue(start);seen[start]=true;double sx=0,sy=0;
            while(queue.Count>0)
            {
                int i=queue.Dequeue(),x=i%width,y=i/width;component.Add(i);sx+=x;sy+=y;
                Visit(x-1,y);Visit(x+1,y);Visit(x,y-1);Visit(x,y+1);
            }
            // 500 pixels at the drawn 1254 by 1254.
            if(component.Count>500*width*height/(1254*1254))pads.Add((Mathf.Atan2((float)(sy/component.Count)-height*.5f,(float)(sx/component.Count)-width*.5f),new List<int>(component)));
        }
        pads.Sort((a,b)=>a.angle.CompareTo(b.angle));
        for(int n=0;n<pads.Count;n++)foreach(int i in pads[n].pixels)regions[i]=n;
        return regions;
        void Visit(int x,int y)
        {
            if(x<0||x>=width||y<0||y>=height)return;int i=y*width+x;
            if(seen[i]||pixels[i].a<.5f||!Olive(pixels[i]))return;
            seen[i]=true;queue.Enqueue(i);
        }
    }
    static bool In(float x,float y,float l,float t,float r,float b)=>x>=l&&x<=r&&y>=t&&y<=b;
    static float NeutralWeight(float saturation)=>1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.30f,.45f,saturation));
    public static Color Recolor(string path,int x,int y,Color c,PlaygroundPalette palette)
    {
        if(c.a<.001f||palette==PlaygroundPalette.Legacy)return c;
        float max=Mathf.Max(c.r,Mathf.Max(c.g,c.b)),min=Mathf.Min(c.r,Mathf.Min(c.g,c.b));
        if(max<.24f)return c; // preserve the ink and dark hardware
        bool olive=Olive(c);
        bool warm=c.r>c.g&&c.g>c.b;
        bool cream=warm&&(max-min)/max<.36f;
        bool classic=palette==PlaygroundPalette.Classic;Color accent=Accent(palette);
        if(path.StartsWith("Things/Building/Timber/",StringComparison.Ordinal))
        {
            bool seat=path.EndsWith("/swing",StringComparison.Ordinal)&&y>=963&&y<=1031&&x>350&&x<910;
            if(olive||seat)return Paint(c,classic?ClassicColor((x/250+y/300)%4):accent,.55f);
            return c;
        }
        if(path.EndsWith("Selectedswing",StringComparison.Ordinal))
        {
            if(olive)
            {
                bool seat=x>285&&x<970&&y>920;
                return Paint(c,seat?(classic?(x<627?Blue:Red):accent):classic?(x<627?Red:Green):Wood,.53f);
            }
            if(cream&&In(x,y,280,84,974,143))return Paint(c,classic?Yellow:Wood,.79f);
        }
        else if(path.EndsWith("SlidePolishV2",StringComparison.Ordinal))
        {
            if(olive)
            {
                bool post=In(x,y,105,916,159,1108)||In(x,y,365,325,417,1108)||In(x,y,412,360,640,550)||In(x,y,620,350,702,679);
                bool back=In(x,y,390,40,775,202);
                return Paint(c,classic?(post?Yellow:x>680?Red:back?Blue:Green):(post||back?Wood:accent),.49f);
            }
            if(classic&&warm)
            {
                // Only the cream bed, platform and stair treads. Dark outlines
                // and saturated wooden fittings keep their original material.
                float neutral=NeutralWeight((max-min)/max);
                bool chute=x>700&&y>315;
                bool stairs=x<377&&y>330&&y<1048;
                if(chute||stairs)return Color.Lerp(c,Paint(c,chute?Blue:Yellow,.82f),neutral);
            }
        }
        else if(path.EndsWith("Selectedseesaw",StringComparison.Ordinal))
        {
            if(olive)return Paint(c,classic?(x<1086?Blue:Red):accent,.55f);
            bool handle=In(x,y,390,80,512,318)||In(x,y,1655,80,1780,318);
            if(warm&&handle)return Paint(c,classic?Green:accent,.65f);
            if(classic&&warm)
            {
                float neutral=NeutralWeight((max-min)/max);
                if(In(x,y,866,354,1308,635))return Color.Lerp(Paint(c,Yellow,.65f),Paint(c,Blue,.82f),neutral);
                if(y<490)return Color.Lerp(Paint(c,Yellow,.65f),c,neutral);
            }
        }
        else if(path.StartsWith("Things/Building/Turn/",StringComparison.Ordinal))
        {
            // Turned pictures of the palette rides (PlaygroundTurn): the sage parts take the palette as the north picture's main parts do;
            // the swing's frame turns wood, as it does north (its seats come from the north picture). Classic gives patches of the four colours.
            if(olive)return Paint(c,classic?ClassicColor((x/250+y/300)%4):path.EndsWith("swing-frame",StringComparison.Ordinal)?Wood:accent,.53f);
            return c;
        }
        else if(path.EndsWith("TrampolineV1",StringComparison.Ordinal)&&olive)
        {
            return Paint(c,classic?Blue:accent,.56f);
        }
        else if(path.EndsWith("ZiplineAtlasV1",StringComparison.Ordinal))
        {
            if(olive)return Paint(c,classic?Yellow:accent,.54f);
            if(classic&&warm&&!cream&&(x<375||x>880)&&y<1025)
            {
                bool brace=x>195&&x<310&&y>535||x>945&&x<1040&&y>535;
                return Paint(c,y<190?Yellow:brace?Green:x<627?Red:Blue,.65f);
            }
        }
        return c;
    }
    public static Graphic GraphicFor(Thing thing)
    {
        if(DesignArt.Timber(thing))return DesignArt.Graphic(thing);
        var data=thing.def.graphicData;
        return GraphicDatabase.Get<Graphic_PlaygroundPalette>(data.texPath+"@"+(int)Of(thing),
            ShaderDatabase.Cutout,data.drawSize,Color.white,Color.white,data);
    }
}

public sealed class Graphic_PlaygroundPalette:Graphic_Single
{
    public override void Init(GraphicRequest req)
    {
        data=req.graphicData;path=req.path;maskPath=req.maskPath;color=req.color;colorTwo=req.colorTwo;drawSize=req.drawSize;
        int separator=path.LastIndexOf('@');string source=separator<0?path:path.Substring(0,separator);
        var palette=separator>=0&&int.TryParse(path.Substring(separator+1),out int id)&&PaletteArt.Valid((PlaygroundPalette)id)?
            (PlaygroundPalette)id:PlaygroundPalette.Blue;
        Texture2D texture;
        if(source.StartsWith("ActiveRideIcons/",StringComparison.Ordinal))
            texture=ActiveRideArt.Icon((ActiveRideKind)Enum.Parse(typeof(ActiveRideKind),source.Substring(source.LastIndexOf('/')+1)),palette);
        else texture=PaletteArt.Texture(source,palette);
        mat=MaterialPool.MatFrom(new MaterialRequest(texture,req.shader,color){colorTwo=colorTwo,renderQueue=req.renderQueue,shaderParameters=req.shaderParameters});
    }
    public override Graphic GetColoredVersion(Shader shader,Color tint,Color tintTwo)
        =>GraphicDatabase.Get<Graphic_PlaygroundPalette>(path,shader,drawSize,tint,tintTwo,data);
    public override void DrawWorker(Vector3 loc,Rot4 rot,ThingDef thingDef,Thing thing,float extraRotation)
    {if(!TurnGhosts.Draw(loc,rot,thingDef,Shader,color))base.DrawWorker(loc,PlaygroundTurn.PictureRot(thingDef,rot),thingDef,thing,extraRotation);}
}

// The plain pictures (bench, small slide, floor rocker, pull-up bar): Graphic_Single, plus the turned picture when planned turned.
public sealed class Graphic_PlaygroundTurn:Graphic_Single
{
    public override Graphic GetColoredVersion(Shader shader,Color tint,Color tintTwo)
        =>GraphicDatabase.Get<Graphic_PlaygroundTurn>(path,shader,drawSize,tint,tintTwo,data);
    public override void DrawWorker(Vector3 loc,Rot4 rot,ThingDef thingDef,Thing thing,float extraRotation)
    {if(!TurnGhosts.Draw(loc,rot,thingDef,Shader,color))base.DrawWorker(loc,PlaygroundTurn.PictureRot(thingDef,rot),thingDef,thing,extraRotation);}
}
