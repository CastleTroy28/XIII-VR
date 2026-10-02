using System;
using UnityEngine;
namespace XiiiXR;
// Native Timeline timing only. The desktop Sobel/dizzy shader must never sample
// an XR image: it also changes screen coordinates. Fade both eyes identically.
internal static class StoryBlink
{
    private static PlayerBlinkBehaviour? owner;
    private static int frame=-100;
    private static float closure, closedAt=-100;
    private static bool latched;
    internal static float Amount=>frame>=Time.frameCount-1?closure:0;
    internal static float Closure(float upper,float lower)
        =>float.IsFinite(upper)&&float.IsFinite(lower)?Math.Clamp(1-(upper-lower),0,1):0;
    internal static void Sample(PlayerBlinkBehaviour blink)
    {
        if(CameraRig.Current==null||!blink.useBlinking)return;
        float raw=Closure(blink.EyeUpperEyeLid,blink.EyeLowerEyeLid);
        float now=Time.realtimeSinceStartup;
        if(raw<.1f){latched=false;closedAt=-100;}
        else if(!latched){latched=true;closedAt=now;}
        // A held/default Timeline value is not a continuously closed eye.
        // Release a held fade and rearm only after the authored lids reopen.
        float envelope=latched?Math.Clamp(1-(now-closedAt-.15f)/.25f,0,1):1;
        closure=Math.Min(raw,.85f)*envelope;owner=blink;frame=Time.frameCount;
    }
    internal static void End(PlayerBlinkBehaviour blink)
    {if(owner==blink){owner=null;frame=-100;closure=0;latched=false;closedAt=-100;}}
    internal static void Reset(){owner=null;frame=-100;closure=0;latched=false;closedAt=-100;}
}
