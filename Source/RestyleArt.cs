using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

// The selected sheet is sampled directly. No exported crop or repainted bitmap.
[StaticConstructorOnStartup]
public static class RestyleArt
{
    const string Atlas="Things/Building/ExpansionRestyleAtlasV2";
    const float FoxScale=.0034f;
    static readonly Vector2 FoxOrigin=new(1225,365);
    static SpritePart Part(float x,float y,float w,float h,float scale,Vector2 origin,bool mirror=false)
        =>new(Atlas,1536,1024,scale,origin,SpritePart.Rect(x,y,w,h),mirror:mirror);
    // [0] as drawn, [1] flipped: the fox turned around (PlaygroundTurn, TurnMap.FlipNorth) faces east.
    static readonly SpritePart[] FoxUpper={Part(935,95,580,273,FoxScale,FoxOrigin),Part(935,95,580,273,FoxScale,FoxOrigin,true)};
    static readonly SpritePart[] FoxLeg={Part(1015,368,140,87,FoxScale,FoxOrigin),Part(1015,368,140,87,FoxScale,FoxOrigin,true)};
    static readonly SpritePart[] FoxRearLeg={Part(1310,368,100,65,FoxScale,FoxOrigin),Part(1310,368,100,65,FoxScale,FoxOrigin,true)};
    static readonly SpritePart[] FoxBase={Part(1155,368,155,154,FoxScale,FoxOrigin),Part(1155,368,155,154,FoxScale,FoxOrigin,true)};
    static readonly Vector2[] FoxLipShape={new(1155,338),new(1310,338),new(1310,376),new(1155,376)};
    static readonly SpritePart[] FoxLip={new(Atlas,1536,1024,FoxScale,FoxOrigin,FoxLipShape),new(Atlas,1536,1024,FoxScale,FoxOrigin,FoxLipShape,mirror:true)};
    public static readonly SpritePart FoxMirrorWhole=Part(935,95,580,427,FoxScale,FoxOrigin,true);
    static readonly SpritePart[] Stumps={
        Part(43,675,174,172,.0050f,new(130,783)),
        Part(233,715,158,138,.0054f,new(311,800)),
        Part(397,720,137,145,.0058f,new(465,807))};
    // The beam in three slices: banded ends at their own size, plain wood between them stretched to length.
    static readonly SpritePart BeamLeft=Part(537,690,68,94,.00225f,new(571,760));
    static readonly SpritePart BeamMid=Part(605,690,360,94,.00225f,new(785,760));
    static readonly SpritePart BeamRight=Part(965,690,68,94,.00225f,new(999,760));
    const float LogLength=2.06f,LogCap=68*.00225f,LogMid=360*.00225f;
    static readonly SpritePart BeamFoot=Part(589,784,61,76,.00225f,new(617,784));
    static readonly SpritePart Hop=Part(1080,555,344,426,1,new(1252,769));
    static readonly SpritePart Deck=new("Things/Building/ExpansionRoundDeckV2",1254,1254,.00225f,new(627,627),SpritePart.Rect(0,0,1254,1254));
    static float Rear=>AltitudeLayer.Pawn.AltitudeFor()-.18f;
    static float Front=>AltitudeLayer.Pawn.AltitudeFor()+.29f;
    static float Floor=>AltitudeLayer.FloorEmplacement.AltitudeFor()+.04f;
    static Vector3 P(float x,float z)=>new(x,0,z);
    static readonly Material Ink=Mat(.18f,.17f,.13f),Sage=Mat(.52f,.58f,.43f),Light=Mat(.69f,.72f,.57f),
        SageDark=Mat(.35f,.41f,.29f),Ivory=Mat(.86f,.82f,.66f),IvoryLight=Mat(.97f,.93f,.80f),Rim=Mat(.25f,.24f,.21f);
    static readonly Material[] RailMaterials={Ink,SageDark,Sage,Light};
    static Material Mat(float r,float g,float b)=>SolidColorMaterials.SimpleSolidColorMaterial(new Color(r,g,b));
    static readonly Mesh Disk=MakeDisk();
    static readonly Dictionary<string,Texture2D> Icons=new();
    sealed class RailFrame { public Mesh[] rear,front; }
    static readonly RailFrame[] RailFrames=new RailFrame[360];
    public static Vector3 SpringContact(float degrees)
        =>P(0,-.01f)+Quaternion.AngleAxis(degrees,Vector3.up)*P(0,.23f);
    public static void Spring(Vector3 at,float degrees,bool flipped=false)
    {
        int i=flipped?1:0;if(flipped)degrees=-degrees;
        PlaygroundGround.Contact(at+P(0,-.41f),.60f,.25f);
        FoxBase[i].Draw(at,Rear);FoxUpper[i].Draw(at,Rear+.004f,degrees);FoxLeg[i].Draw(at,Rear+.004f,degrees);FoxRearLeg[i].Draw(at,Rear+.004f,degrees);
        FoxLip[i].Draw(at,Front,degrees);
    }
    public static void Stump(Vector3 at,int variant)
    {
        PlaygroundGround.Contact(at+P(0,-.08f),.75f,.49f);
        Stumps[Math.Abs(variant)%3].Draw(at,Floor+.02f);
    }
    // A balance log two cells long, lying east-west or (alongZ) north-south. The support feet stay upright.
    public static void Balance(Vector3 at,bool alongZ)
    {
        float angle=alongZ?90:0,layer=Floor+.03f;
        PlaygroundGround.Contact(at+P(0,-.08f),alongZ?.34f:LogLength-.1f,alongZ?LogLength-.1f:.28f);
        foreach(float x in new[]{-LogLength/2+.2f,0f,LogLength/2-.2f})BeamFoot.Draw(at+Rot(P(x,0),angle)+P(0,-.054f),layer-.002f);
        BeamLeft.Draw(at+Rot(P(-LogLength/2+LogCap/2,0),angle),layer,angle);
        BeamRight.Draw(at+Rot(P(LogLength/2-LogCap/2,0),angle),layer,angle);
        BeamMid.Draw(at,layer-.001f,angle,new Vector3((LogLength-2*LogCap)/LogMid,1,1));
    }
    public static readonly Vector3 HopScale=new(.0061f,1,.0117f);
    public static SpritePart HopPart=>Hop;
    // Drawn flat on the ground, so a turned hopscotch is the same picture turned (PlaygroundTurn, TurnKind.Flat).
    public static void Hopscotch(Vector3 at,float angle=0)=>Hop.Draw(at,Floor,angle,HopScale);

    static Vector3 Rot(Vector3 p,float degrees)=>Quaternion.AngleAxis(degrees,Vector3.up)*p;
    public static void Roundabout(Vector3 at,float angle)
    {
        PlaygroundGround.Contact(at+P(0,-.07f),2.66f,2.62f);
        Oval(at+P(0,-.065f),2.64f,2.64f,Ink,Floor+.01f);
        Oval(at+P(0,-.04f),2.61f,2.61f,Rim,Floor+.015f);
        Deck.Draw(at,Floor+.025f,angle);
        // Radial rails rotate around the centre but their vertical rises stay north/up.
        // This prevents a painted upright from lying flat when the platform turns.
        for(int side=0;side<2;side++)
        {
            DrawRails(at,angle,side==0);
            for(int n=0;n<4;n++)
            {
                float a=angle+45+n*90;
                var foot=at+Rot(P(0,1.10f),a);
                bool back=foot.z>at.z;if(back!=(side==0))continue;
                float layer=back?Rear+.03f:Front+.03f;
                Oval(foot+P(0,.035f),.19f,.13f,Ink,layer+.012f);
                Oval(foot+P(0,.048f),.15f,.10f,Ivory,layer+.014f);
            }
            if(side==0)Hub(at);
        }
    }
    static Vector3[] RailPoints(float angle)
    {
        // Curved shoulder between radial handhold and the upright outer leg.
        var p=new Vector3[13];
        p[0]=Rot(P(0,.12f),angle)+P(0,.63f);
        p[1]=Rot(P(0,.50f),angle)+P(0,.76f);
        p[2]=Rot(P(0,.86f),angle)+P(0,.76f);
        for(int i=0;i<=8;i++)
        {float t=i/8f*Mathf.PI*.5f;p[i+3]=Rot(P(0,.86f+.24f*Mathf.Sin(t)),angle)+P(0,.52f+.24f*Mathf.Cos(t));}
        p[12]=Rot(P(0,1.10f),angle)+P(0,.06f);return p;
    }
    static void DrawRails(Vector3 at,float angle,bool rear)
    {
        int frame=Mathf.RoundToInt(Mathf.Repeat(angle,360))%360;
        var cached=RailFrames[frame];
        if(cached==null)RailFrames[frame]=cached=new RailFrame{rear=RailMeshes(frame,true),front=RailMeshes(frame,false)};
        var meshes=rear?cached.rear:cached.front;
        float layer=rear?Rear+.03f:Front+.03f;
        for(int i=0;i<4;i++){at.y=layer+i*.002f;Graphics.DrawMesh(meshes[i],Matrix4x4.Translate(at),RailMaterials[i],0);}
    }
    static Mesh[] RailMeshes(int angle,bool rear)
    {
        var result=new Mesh[4];float[] widths={.105f,.076f,.055f,.020f};
        Vector3[] offsets={P(0,0),P(-.004f,.007f),P(-.010f,.016f),P(-.017f,.025f)};
        for(int paint=0;paint<4;paint++)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int n=0;n<4;n++)
            {
                float a=angle+45+n*90;if((Rot(P(0,1.10f),a).z>0)!=rear)continue;
                var p=RailPoints(a);int start=vertices.Count;
                for(int i=0;i<p.Length;i++)
                {
                    var tangent=(p[Math.Min(i+1,p.Length-1)]-p[Math.Max(i-1,0)]).normalized;
                    var normal=P(-tangent.z,tangent.x)*widths[paint]*.5f;
                    vertices.Add(p[i]+offsets[paint]+normal);vertices.Add(p[i]+offsets[paint]-normal);
                    if(i==0)continue;int k=start+i*2;
                    triangles.Add(k-2);triangles.Add(k);triangles.Add(k-1);triangles.Add(k-1);triangles.Add(k);triangles.Add(k+1);
                }
            }
            var mesh=new Mesh{name="LP curved rail "+angle+" "+rear+" "+paint,vertices=vertices.ToArray(),triangles=triangles.ToArray()};
            mesh.RecalculateNormals();mesh.RecalculateBounds();result[paint]=mesh;
        }
        return result;
    }
    static void Hub(Vector3 at)
    {
        Oval(at,.48f,.35f,Ink,Rear+.04f);Oval(at+P(0,.014f),.42f,.29f,SageDark,Rear+.042f);
        var bottom=at+P(0,.05f);var top=at+P(0,.66f);
        Stroke(bottom,top,.235f,Ink,Rear+.05f);Stroke(bottom+P(-.012f,0),top+P(-.012f,0),.177f,Ivory,Rear+.052f);
        Stroke(bottom+P(-.045f,0),top+P(-.045f,0),.050f,IvoryLight,Rear+.054f);
        Oval(top,.24f,.15f,Ink,Rear+.056f);Oval(top+P(0,.012f),.19f,.11f,IvoryLight,Rear+.058f);
    }
    static void Stroke(Vector3 a,Vector3 b,float width,Material m,float y)
    {
        var d=b-a;var mid=(a+b)*.5f;mid.y=y;
        Graphics.DrawMesh(MeshPool.plane10,Matrix4x4.TRS(mid,Quaternion.AngleAxis(d.AngleFlat(),Vector3.up),new Vector3(width,1,d.MagnitudeHorizontal())),m,0);
        Oval(a,width,width,m,y);Oval(b,width,width,m,y);
    }
    static void Oval(Vector3 at,float w,float h,Material m,float y)
    {at.y=y;Graphics.DrawMesh(Disk,Matrix4x4.TRS(at,Quaternion.identity,new Vector3(w*.5f,1,h*.5f)),m,0);}
    static Mesh MakeDisk()
    {
        var v=new Vector3[25];var t=new int[72];
        for(int i=0;i<24;i++){float a=-i*Mathf.PI/12;v[i+1]=P(Mathf.Cos(a),Mathf.Sin(a));t[i*3]=0;t[i*3+1]=i+1;t[i*3+2]=(i+1)%24+1;}
        var m=new Mesh{vertices=v,triangles=t};m.RecalculateNormals();m.RecalculateBounds();return m;
    }
    // Native UI needs a standalone region of the atlas. Pixels remain unchanged;
    // this is only a cached in-memory texture for construction icons/blueprints.
    public static Texture2D Icon(string key)
    {
        if(Icons.TryGetValue(key,out var icon))return icon;
        var rect=key switch
        {
            "SpringRider"=>new RectInt(935,95,580,427),
            "Roundabout"=>new RectInt(210,75,570,465),
            "Hopscotch"=>new RectInt(1080,555,344,426),
            "BalanceLog"=>new RectInt(537,690,496,177),
            "SteppingStump"=>new RectInt(43,675,174,172),
            _=>new RectInt()
        };
        if(rect.width==0)return null;
        var src=ContentFinder<Texture2D>.Get(Atlas);
        icon=PlaygroundPixels.Region(src,rect,1536,1024,"LP selected sprite icon "+key);
        Icons[key]=icon;return icon;
    }
}
