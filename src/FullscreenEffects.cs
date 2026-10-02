using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
namespace XiiiXR;
// Full-screen effects are not menu panels. Native effect animation/timing stays
// intact, but their rectangles cover the complete pair of headset frusta.
internal sealed class FullscreenEffects : IDisposable
{
    private sealed class Saved
    {
        internal Graphic Graphic=null!;
        internal Texture? Texture;internal Sprite? Sprite;
        internal bool Healing,PreserveAspect;internal Image.Type Type;
        internal Vector2 Min,Max,Pivot,Size,Position;
    }
    private readonly List<Saved> graphics=new();
    private readonly List<(RectTransform rect,Vector3 position,Quaternion rotation,Vector3 scale,Vector2 size,Vector2 min,Vector2 max,Vector2 pivot)> canvases=new();
    private bool failed;
    private Texture2D? healingTexture;
    private Sprite? healingSprite;
    internal void Discover()
    {
        if(failed)return;
        graphics.RemoveAll(g=>g.Graphic==null);
        foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<PlayerCameraEffectController>()))
        {
            var effect=obj.TryCast<PlayerCameraEffectController>();var canvas=effect?.healthAndArmorEffectsCanvas;
            if(canvas==null||!canvas.gameObject.scene.IsValid())continue;
            var canvasRect=canvas.transform.TryCast<RectTransform>();
            if(canvasRect==null)continue;
            if(!canvases.Exists(c=>c.rect==canvasRect))canvases.Add((canvasRect,canvasRect.localPosition,canvasRect.localRotation,canvasRect.localScale,canvasRect.sizeDelta,canvasRect.anchorMin,canvasRect.anchorMax,canvasRect.pivot));
            foreach(var component in canvas.GetComponentsInChildren(Il2CppType.Of<Graphic>(),true))
            {
                var g=component.TryCast<Graphic>();if(g==null||graphics.Exists(s=>s.Graphic==g))continue;
                var image=g.TryCast<Image>();var raw=g.TryCast<RawImage>();if(image==null&&raw==null)continue;
                var r=g.rectTransform;
                // Identify healing by its own hierarchy and texture, not colour.
                string name=g.name+" "+(image?.sprite==null?"":image.sprite.name)+" "+(raw?.texture==null?"":raw.texture.name);
                for(var p=r.parent;p!=null&&p!=canvas.transform;p=p.parent)name+=" "+p.name;
                bool heal=name.Contains("healing",StringComparison.OrdinalIgnoreCase)||name.Contains("heal_",StringComparison.OrdinalIgnoreCase)||name.Contains("_heal_",StringComparison.OrdinalIgnoreCase)||name.EndsWith("_heal",StringComparison.OrdinalIgnoreCase);
                graphics.Add(new Saved{Graphic=g,
                    Texture=raw?.texture,Sprite=image?.overrideSprite,Healing=heal,PreserveAspect=image!=null&&image.preserveAspect,Type=image==null?Image.Type.Simple:image.type,
                    Min=r.anchorMin,Max=r.anchorMax,Pivot=r.pivot,Size=r.sizeDelta,Position=r.anchoredPosition});
                Bootstrap.Write("FULLSCREEN effect="+name+" healing="+heal);
            }
        }
    }
    private void MakeTexture()
    {
        if(healingTexture!=null)return;
        const int size=1024;
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            pixels[y*size+x]=new Color32(100,210,255,ViewCoverage.HealingAlpha(x*2f/(size-1)-1,y*2f/(size-1)-1));
        healingTexture=new Texture2D(size,size,TextureFormat.RGBA32,false);
        healingTexture.name="XIII smooth healing 1024";healingTexture.wrapMode=TextureWrapMode.Clamp;healingTexture.filterMode=FilterMode.Bilinear;
        healingTexture.SetPixels32(pixels);healingTexture.Apply(false,true);
        healingSprite=Sprite.CreateSprite(healingTexture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,Vector4.zero,false);
    }
    internal void Render(CameraRig rig)
    {
        if(failed)return;
        try{RenderCore(rig);}
        catch(Exception ex){failed=true;try{Dispose();}catch{}Bootstrap.Warn("FULLSCREEN visual adapter disabled; XR and controls continue: "+ex);}
    }
    private void RenderCore(CameraRig rig)
    {
        if(graphics.Count==0)return;
        const float distance=1.2f;
        var b=ViewCoverage.Bounds(rig.FrustumLeft,rig.EyeLeft,rig.FrustumRight,rig.EyeRight,distance);
        var origin=rig.HeadPosition+rig.HeadRotation*new Vector3((b.X+b.Y)*.5f,(b.Z+b.W)*.5f,distance);
        foreach(var c in canvases)
        {
            var r=c.rect;if(r==null)continue;
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(1920,1080);
            r.SetPositionAndRotation(origin,rig.HeadRotation);
            var p=r.parent==null?Vector3.one:r.parent.lossyScale;
            float Div(float a,float v)=>a/(Math.Abs(v)<1e-6f?(v<0?-1e-6f:1e-6f):v);
            r.localScale=new Vector3(Div((b.Y-b.X)/1920,p.x),Div((b.W-b.Z)/1080,p.y),Div(.001f,p.z));
        }
        foreach(var s in graphics)
        {
            var g=s.Graphic;if(g==null)continue;
            if(s.Healing)
            {
                MakeTexture();var image=g.TryCast<Image>();var raw=g.TryCast<RawImage>();
                var rect=g.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=Vector2.zero;rect.anchoredPosition=Vector2.zero;
                if(image!=null){image.overrideSprite=healingSprite!;image.preserveAspect=false;image.type=Image.Type.Simple;}
                if(raw!=null)raw.texture=healingTexture!;
            }
        }
    }
    public void Dispose()
    {
        foreach(var s in graphics)if(s.Graphic!=null)
        {

            var image=s.Graphic.TryCast<Image>();if(image!=null&&s.Healing){image.overrideSprite=s.Sprite!;image.preserveAspect=s.PreserveAspect;image.type=s.Type;}
            if(s.Healing){var r=s.Graphic.rectTransform;r.anchorMin=s.Min;r.anchorMax=s.Max;r.pivot=s.Pivot;r.sizeDelta=s.Size;r.anchoredPosition=s.Position;}
            var raw=s.Graphic.TryCast<RawImage>();if(raw!=null&&s.Healing)raw.texture=s.Texture!;
        }
        foreach(var c in canvases)if(c.rect!=null){var r=c.rect;r.localPosition=c.position;r.localRotation=c.rotation;r.localScale=c.scale;r.sizeDelta=c.size;r.anchorMin=c.min;r.anchorMax=c.max;r.pivot=c.pivot;}
        canvases.Clear();graphics.Clear();if(healingSprite!=null)UnityEngine.Object.Destroy(healingSprite);if(healingTexture!=null)UnityEngine.Object.Destroy(healingTexture);
        healingSprite=null;healingTexture=null;
    }
}
