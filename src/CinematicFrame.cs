using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
namespace XiiiXR;
// Hide only non-text decorative graphics under the game's CutsceneUI controller.
// Keep its timelines, subtitle cards, skip prompts and other comic layers alive.
internal sealed class CinematicFrame : IDisposable
{
    private sealed class Border
    {
        internal Graphic Graphic=null!;
        internal bool Enabled,Culled,Active,Leaf;
        internal Color Color;
    }
    private readonly List<Border> borders=new();
    private readonly Harmony patches=new("xiii.vr.xrbootstrap.cinematicframe");
    private static CinematicFrame? current;
    private readonly HashSet<int> ids=new();
    private static float nextWarning;
    private static void Report(Exception ex)
    {
        if(Time.realtimeSinceStartup<nextWarning)return;
        nextWarning=Time.realtimeSinceStartup+10;
        Bootstrap.Warn("CINEMATIC stale graphic skipped; tracking continues: "+ex);
    }
    internal CinematicFrame()
    {
        try
        {
            foreach(var type in new[]{typeof(Image),typeof(RawImage)})
                patches.Patch(AccessTools.DeclaredMethod(type,"OnPopulateMesh",new[]{typeof(VertexHelper)}),
                    prefix:new HarmonyMethod(typeof(CinematicFrame),nameof(Populate)));
            patches.Patch(AccessTools.DeclaredMethod(typeof(Graphic),"Rebuild",new[]{typeof(CanvasUpdate)}),
                postfix:new HarmonyMethod(typeof(CinematicFrame),nameof(Rebuilt)));
            current=this;
        }
        catch(Exception ex){patches.UnpatchSelf();Bootstrap.Warn("cinematic frame mesh guard unavailable; other VR features remain active: "+ex.Message);}
    }
    // Animation/Canvas rebuilds can re-enable an Image AFTER its render pose.
    // Remove only this decorative graphic's geometry at its actual build point.
    private static bool Populate(Graphic __instance,VertexHelper __0)
    {
        try
        {
        if(current==null)return true;
        if(!current.ids.Contains(__instance.GetInstanceID()))
        {
            var sprite=AssetName(__instance);
            if(!ExactFrame(sprite))return true;
            // Discovery owns hierarchy/state changes outside Canvas mesh traversal.
            __0.Clear();return false;
        }
        __0.Clear();return false;
        }
        catch(Exception ex){Report(ex);return true;}
    }
    private static void Rebuilt(Graphic __instance)
    {try{if(current!=null&&__instance!=null&&current.ids.Contains(__instance.GetInstanceID())){var renderer=__instance.canvasRenderer;if(renderer!=null){renderer.Clear();renderer.cull=true;}}}catch(Exception ex){Report(ex);}}
    private static void Clear(Graphic graphic)
    {
        try
        {
        if(current==null||graphic==null)return;
        var saved=current.borders.Find(p=>p.Graphic==graphic);if(saved==null)return;
        // A native timeline can resubmit the Image after it has been disabled.
        // A decorative leaf has no story content: remove it from the active
        // hierarchy as well, keeping its original state for a complete restore.
        if(saved.Leaf&&graphic.gameObject.activeSelf)graphic.gameObject.SetActive(false);
        var color=graphic.color;color.a=0;graphic.color=color;
        graphic.enabled=false;
        // Native animation can reuse already-batched vertices without calling
        // Image.OnPopulateMesh again. Remove the cached submission itself.
        var renderer=graphic.canvasRenderer;if(renderer!=null){renderer.Clear();renderer.cull=true;}
        }
        catch(Exception ex){Report(ex);}
    }
    internal void Discover()
    {
        if(borders.RemoveAll(p=>p.Graphic==null)>0)
        {ids.Clear();foreach(var border in borders)ids.Add(border.Graphic.GetInstanceID());}
        // The same killcam-frame asset also occurs outside a CutsceneUI subtree.
        // Match the exact cinematic sprite, never arbitrary black HUD graphics.
        foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Image>()))
            Inspect(obj.TryCast<Graphic>(),false);
        foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<RawImage>()))
            Inspect(obj.TryCast<Graphic>(),false);
        foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<CutsceneUI>()))
        {
            try
            {
            var c=obj.TryCast<CutsceneUI>();if(c==null||!c.gameObject.scene.IsValid())continue;
            foreach(var candidate in c.GetComponentsInChildren(Il2CppType.Of<Graphic>(),true))
                Inspect(candidate.TryCast<Graphic>(),true);
            }
            catch(Exception ex){Report(ex);}
        }
    }
    private void Inspect(Graphic? graphic,bool inCutscene)
    {
        try
        {
                if(graphic==null||!graphic.gameObject.scene.IsValid())return;
                string sprite=AssetName(graphic);
                if(!inCutscene){if(ExactFrame(sprite))Remember(graphic,sprite);return;}
                var image=graphic.TryCast<Image>();var raw=graphic.TryCast<RawImage>();if(image==null&&raw==null)return;
                // A subtitle background, prompt or interactive item is not a frame.
                if(graphic.GetComponentInParent(Il2CppType.Of<SubtitleCard>())!=null
                    ||graphic.GetComponentInParent(Il2CppType.Of<Selectable>())!=null)return;
                var rect=graphic.rectTransform.rect;var color=graphic.color;
                bool named=IsBorder(graphic.name)||IsBorder(sprite);
                bool blackBar=image!=null&&image.sprite==null&&color.r<.12f&&color.g<.12f&&color.b<.12f
                    &&(rect.width>rect.height*4||rect.height>rect.width*4);
                if(!named&&!blackBar)return;
                Remember(graphic,sprite);
        }
        catch(Exception ex){Report(ex);}
    }
    private void Remember(Graphic graphic,string sprite)
    {
        bool exactFrame=ExactFrame(sprite);
        if(ids.Contains(graphic.GetInstanceID())||!exactFrame&&(graphic.GetComponentInParent(Il2CppType.Of<SubtitleCard>())!=null
            ||graphic.GetComponentInParent(Il2CppType.Of<Selectable>())!=null))return;
        bool leaf=true;
        foreach(var c in graphic.gameObject.GetComponentsInChildren(Il2CppType.Of<Graphic>(),true))
            if(c!=graphic){leaf=false;break;}
        // Controllers/directors on the same object must keep running.
        if(graphic.gameObject.GetComponent(Il2CppType.Of<CutsceneUI>())!=null)leaf=false;
        borders.Add(new Border{Graphic=graphic,Enabled=graphic.enabled,Culled=graphic.canvasRenderer!=null&&graphic.canvasRenderer.cull,
            Active=graphic.gameObject.activeSelf,Leaf=leaf,Color=graphic.color});
        ids.Add(graphic.GetInstanceID());graphic.SetVerticesDirty();Clear(graphic);
        Bootstrap.Write("CINEMATIC frame isolated graphic="+graphic.name+" sprite="+sprite+" leafDisabled="+leaf);
    }
    private static bool ExactFrame(string value)=>value.Contains("hud_killcam_frame_cutscene",StringComparison.OrdinalIgnoreCase)
        ||value.Contains("cutscene",StringComparison.OrdinalIgnoreCase)&&(value.Contains("frame",StringComparison.OrdinalIgnoreCase)||value.Contains("border",StringComparison.OrdinalIgnoreCase));
    private static string AssetName(Graphic graphic)
    {
        // ?. tests the managed IL2CPP wrapper, not the native Unity object.
        // A destroyed sprite/material/shader still has a non-null wrapper and
        // Object.name throws, as in the 0.1.44 freeze log. Read each reference
        // once and use Unity's lifetime check before accessing its properties.
        if(graphic==null)return "";
        try
        {
            var image=graphic.TryCast<Image>();var raw=graphic.TryCast<RawImage>();
            var sprite=image==null?null:image.overrideSprite;
            if(sprite==null&&image!=null)sprite=image.sprite;
            var texture=raw==null?null:raw.texture;
            var mat=graphic.material;
            var shader=mat==null?null:mat.shader;
            return ObjectName(sprite)+" "+ObjectName(texture)+" "+ObjectName(mat)+" "+ObjectName(shader);
        }
        catch(Exception ex){Report(ex);return "";}
    }
    private static string ObjectName(UnityEngine.Object? value)
    {try{return value==null?"":value.name;}catch{return "";}}
    private static bool IsBorder(string value)
    {
        string s=value.ToLowerInvariant();
        return s.Contains("letterbox")||s.Contains("border")||s.Contains("frame")||s.Contains("blackbar")
            ||s.Contains("black_bar")||s.Contains("cinematic_bar");
    }
    internal void Render(bool cinematic)
    {foreach(var p in borders)if(p.Graphic!=null)Clear(p.Graphic);}
    public void Dispose(){if(current==this)current=null;patches.UnpatchSelf();foreach(var p in borders)if(p.Graphic!=null){p.Graphic.color=p.Color;p.Graphic.enabled=p.Enabled;if(p.Graphic.canvasRenderer!=null)p.Graphic.canvasRenderer.cull=p.Culled;if(p.Leaf)p.Graphic.gameObject.SetActive(p.Active);p.Graphic.SetVerticesDirty();}borders.Clear();ids.Clear();}
}
