using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace XiiiXR;
// 0.1.215: the controller drawn in the game's hints with the button to press
// lit (ControllerIconMath). On a pick-up hint it takes the place of the
// game's hand icon; on the others it stands right of the hint's text.
internal static class PromptIcons
{
    private static readonly Dictionary<string,Sprite> sprites=new();
    private sealed class Beside{internal GameObject? Go;internal Image? Image;internal string Key="";internal string Text="";}
    private static readonly Dictionary<IntPtr,Beside> beside=new();
    private static readonly HashSet<IntPtr> handIcons=new();
    private static readonly Dictionary<IntPtr,string> handKeys=new();
    private static float nextError;
    internal static Sprite? SpriteFor(IconSpec spec)
    {
        if(sprites.TryGetValue(spec.Key,out var cached)&&cached!=null)return cached;
        int n=ControllerIconMath.Size;var raw=ControllerIconMath.Paint(spec,n);
        var pixels=new Color32[n*n];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(raw[i*4],raw[i*4+1],raw[i*4+2],raw[i*4+3]);
        var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){name="XIII VR controller icon "+spec.Key,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.DontUnloadUnusedAsset};
        texture.SetPixels32(pixels);texture.Apply(false,true);
        var sprite=Sprite.CreateSprite(texture,new Rect(0,0,n,n),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,Vector4.zero,false);
        sprite.name=texture.name;sprite.hideFlags=HideFlags.DontUnloadUnusedAsset;
        sprites[spec.Key]=sprite;return sprite;
    }
    // A pick-up hint (the game's hand icon): the controller instead; null: the game's icon.
    internal static void TypeIcon(HUDInteractionPrompt? group,IconSpec? spec)
    {
        if(group==null)return;
        string key=spec?.Key??"";
        if(handKeys.TryGetValue(group.Pointer,out var last)&&last==key)return;
        handKeys[group.Pointer]=key;
        try
        {
            var icons=group.icons;if(icons==null||!icons.ContainsKey(HUDInteractionPrompt.PromptType.General))return;
            var hand=icons[HUDInteractionPrompt.PromptType.General];if(hand==null)return;
            var want=spec is IconSpec s?SpriteFor(s):null;
            if(want==null){if(handIcons.Remove(hand.Pointer))hand.overrideSprite=null;return;}
            if(hand.overrideSprite==null||hand.overrideSprite.Pointer!=want.Pointer){hand.overrideSprite=want;handIcons.Add(hand.Pointer);}
        }
        catch(Exception ex){Report("hand icon",ex);}
    }
    // The controller right of the hint's text (null: none).
    internal static void Show(ButtonPrompt prompt,IconSpec? spec)
    {
        try
        {
            var text=prompt.textRef;if(text==null)return;
            if(!beside.TryGetValue(prompt.Pointer,out var b)){if(spec==null)return;b=new Beside();beside[prompt.Pointer]=b;}
            if(spec is not IconSpec s){if(b.Go!=null&&b.Go.activeSelf)b.Go.SetActive(false);b.Key="";return;}
            if(b.Go==null||b.Image==null)
            {
                var go=new GameObject("XIII VR button icon");go.layer=text.gameObject.layer;go.transform.SetParent(text.transform,false);
                var rect=go.AddComponent(Il2CppType.Of<RectTransform>()).TryCast<RectTransform>()!;
                rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(0,.5f);
                var image=go.AddComponent(Il2CppType.Of<Image>()).TryCast<Image>()!;image.raycastTarget=false;image.preserveAspect=true;
                b.Go=go;b.Image=image;b.Key="";b.Text="";
            }
            if(!b.Go.activeSelf)b.Go.SetActive(true);
            if(b.Key!=s.Key){b.Image.sprite=SpriteFor(s);b.Key=s.Key;b.Text="";}
            if(b.Text==text.text)return;
            b.Text=text.text;
            // Placed after the text: right of its last letter.
            text.ForceMeshUpdate(false,false);
            var bounds=text.textBounds;float size=Math.Max(28,text.fontSize*2.1f);
            float x=bounds.size.x>0&&float.IsFinite(bounds.max.x)?bounds.max.x:text.rectTransform.rect.xMax;
            float y=bounds.size.y>0&&float.IsFinite(bounds.center.y)?bounds.center.y:text.rectTransform.rect.center.y;
            var r=b.Go.GetComponent(Il2CppType.Of<RectTransform>())?.TryCast<RectTransform>();
            if(r!=null){r.sizeDelta=new Vector2(size,size);r.localPosition=new Vector3(x+size*.15f,y,0);}
        }
        catch(Exception ex){Report("button icon",ex);}
    }
    private static void Report(string what,Exception ex)
    {
        if(Time.realtimeSinceStartup<nextError)return;nextError=Time.realtimeSinceStartup+10;
        Bootstrap.Warn("PROMPT "+what+": "+ex.Message);
    }
    internal static void Dispose()
    {
        foreach(var b in beside.Values)if(b.Go!=null)UnityEngine.Object.Destroy(b.Go);
        beside.Clear();handIcons.Clear();handKeys.Clear();
    }
}
