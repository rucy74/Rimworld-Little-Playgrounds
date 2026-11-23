using UnityEngine;
using Verse;

namespace LittlePlaygrounds;

// 1.6 discards CPU texture data after upload. Use a temporary GPU readback
// only for one-off palette/icon generation, and release that temporary immediately.
static class PlaygroundPixels
{
    public static Color[] Read(Texture2D texture)
    {
        if(texture.isReadable)return texture.GetPixels();
        var readable=CopyReadable(texture);
        try{return readable.GetPixels();}
        finally{Object.Destroy(readable);}
    }
    public static Color[] Read(Texture2D texture,int x,int y,int width,int height)
    {
        if(texture.isReadable)return texture.GetPixels(x,y,width,height);
        var readable=CopyReadable(texture);
        try{return readable.GetPixels(x,y,width,height);}
        finally{Object.Destroy(readable);}
    }
    public static Texture2D Region(Texture2D source,RectInt drawn,int drawnWidth,int drawnHeight,string name)
    {
        float sx=source.width/(float)drawnWidth,sy=source.height/(float)drawnHeight;
        int x=Mathf.RoundToInt(drawn.x*sx),width=Mathf.Clamp(Mathf.RoundToInt(drawn.width*sx),1,source.width-x);
        int height=Mathf.Max(1,Mathf.RoundToInt(drawn.height*sy)),y=Mathf.Clamp(source.height-Mathf.RoundToInt(drawn.y*sy)-height,0,source.height-height);
        var result=new Texture2D(width,height,TextureFormat.RGBA32,false){name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        result.SetPixels(Read(source,x,y,width,height));result.Apply();return result;
    }
    public static Color32[] Read32(Texture2D texture)
    {
        if(texture.isReadable)return texture.GetPixels32();
        var readable=CopyReadable(texture);
        try{return readable.GetPixels32();}
        finally{Object.Destroy(readable);}
    }
    static Texture2D CopyReadable(Texture2D texture)
    {
        // Blit changes active. Capture it BEFORE Blit and restore it BEFORE releasing the buffer.
        var previous=RenderTexture.active;
        var buffer=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.Default,RenderTextureReadWrite.Linear);
        Texture2D copy=null;
        try
        {
            Graphics.Blit(texture,buffer);RenderTexture.active=buffer;
            copy=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
            copy.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);copy.Apply();return copy;
        }
        catch{if(copy!=null)Object.Destroy(copy);throw;}
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(buffer);}
    }
}
