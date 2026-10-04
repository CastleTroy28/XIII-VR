using System;
using System.Collections;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR;
using BepInEx.Unity.IL2CPP.Utils.Collections;
namespace XiiiXR;
// 0.1.237: STEREO CHECK. When VR SETTINGS opens, and a moment after the world
// scale is changed, the two eyes' pictures are read back small and compared
// (StereoDisparity): how far a thing moves between the eyes, against where a
// thing far ahead would be. The world scale changes that by the eye distance
// drawn; if it does not, the game is drawn from eyes the mod does not reach.
// Only on those events (a few milliseconds each), never every frame.
internal static class StereoCheck
{
    private static bool pending;
    private static float baseNear=float.NaN,baseEyes=float.NaN;
    private static bool menuWas;private static float scaleSeen=float.NaN,dueAt=-1;private static string dueWhy="";private static bool dueBase;
    internal const int Width=480;
    // Every frame while VR runs: schedules a check at the settings' opening and after a world scale change.
    internal static void Tick(MonoBehaviour owner,XRDisplaySubsystem? display,CameraRig? rig)
    {
        if(display==null||rig==null)return;
        float now=Time.realtimeSinceStartup,scale=QualityOptions.WorldScaleValue;
        bool open=QualityMenu.Open;
        if(open&&!menuWas){dueAt=now+.6f;dueWhy="VR SETTINGS opened";dueBase=true;}
        menuWas=open;
        if(float.IsFinite(scaleSeen)&&Math.Abs(scale-scaleSeen)>1e-4f){dueAt=now+.8f;dueWhy="world scale "+MathF.Round(scale*100)+"%";dueBase=false;}
        scaleSeen=scale;
        if(dueAt<0||now<dueAt||pending)return;
        dueAt=-1;
        try{owner.StartCoroutine(Run(display,rig,dueWhy,dueBase).WrapToIl2Cpp());}
        catch(Exception ex){Bootstrap.Warn("STEREO CHECK not started: "+ex.Message);}
    }
    private static IEnumerator Run(XRDisplaySubsystem display,CameraRig rig,string why,bool isBase)
    {
        if(pending)yield break;
        pending=true;
        try
        {
            yield return new WaitForEndOfFrame();
            byte[]? rgb=null;int w=0,h=0;
            try{rgb=Read(display,out w,out h);}
            catch(Exception ex){Bootstrap.Warn("STEREO CHECK no pictures ("+why+"): "+ex.Message);}
            if(rgb==null)yield break;
            float eyes=rig.EyeDistanceDrawn;
            float far=float.NaN;
            try{far=StereoDisparity.FarShift(ProjectionMath.Build(rig.FrustumLeft,.1f,100).M13,ProjectionMath.Build(rig.FrustumRight,.1f,100).M13,w);}catch(Exception){}
            var task=Task.Run(()=>StereoDisparity.Measure(rgb,w*2,h));
            while(!task.IsCompleted)yield return null;
            if(task.IsFaulted){Bootstrap.Warn("STEREO CHECK failed: "+task.Exception?.GetBaseException().Message);yield break;}
            Report(task.Result,far,eyes,why,isBase);
        }
        finally{pending=false;}
    }
    private static void Report(StereoShift s,float far,float eyes,string why,bool isBase)
    {
        var c=CultureInfo.InvariantCulture;
        string line="STEREO CHECK ("+why+", eyes drawn "+eyes.ToString("F4",c)+" m apart, pictures "+s.Width+" px wide): ";
        if(float.IsFinite(s.Identical)&&s.Identical<StereoDisparity.SameBelow)
        {Bootstrap.Warn(line+"the two eyes' pictures are the same (difference "+s.Identical.ToString("F2",c)+" per pixel): no depth at all");return;}
        if(s.Blocks==0){Bootstrap.Write(line+"nothing with enough detail to compare (difference "+s.Identical.ToString("F1",c)+" per pixel)");return;}
        // How much nearer than "far ahead" things are seen (grows with the eye distance).
        float near=float.IsFinite(far)?far-s.MedianShift:float.NaN;
        line+=s.Blocks+" places matched, moved "+s.MedianShift.ToString("F1",c)+" px between the eyes (the middle one "+s.CenterShift.ToString("F0",c)+"), a thing far ahead would move "+far.ToString("F1",c)+" px: depth "+near.ToString("F1",c)+" px";
        if(isBase){baseNear=near;baseEyes=eyes;}
        else if(float.IsFinite(baseNear)&&Math.Abs(baseNear)>.5f&&float.IsFinite(baseEyes)&&baseEyes>0)
            line+="; at the settings' opening "+baseNear.ToString("F1",c)+" px at "+baseEyes.ToString("F4",c)+" m: depth x"+(near/baseNear).ToString("F2",c)+" for eyes x"+(eyes/baseEyes).ToString("F2",c)+(Math.Abs(near/baseNear-1)<.15f&&Math.Abs(eyes/baseEyes-1)>.3f?" - the eye distance does not reach the picture":"");
        Bootstrap.Write(line);
    }
    private static byte[] Read(XRDisplaySubsystem display,out int w,out int h)
    {
        w=h=0;
        if(!display.running)throw new InvalidOperationException("the XR display is not running");
        int count=display.GetRenderPassCount();
        if(count!=2)throw new NotSupportedException(count+" render passes (two expected)");
        var left=display.GetRenderTextureForRenderPass(0);var right=display.GetRenderTextureForRenderPass(1);
        if(left==null||right==null)throw new InvalidOperationException("no eye texture");
        if(left.GetInstanceID()==right.GetInstanceID())throw new NotSupportedException("both eyes in one texture");
        w=Width;h=Math.Max(64,(int)Math.Round(Width*(double)left.height/Math.Max(1,left.width)));
        var previous=RenderTexture.active;RenderTexture? small=null;Texture2D? pixels=null;
        try
        {
            small=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,1);
            pixels=new Texture2D(w*2,h,TextureFormat.RGB24,false);
            Graphics.Blit(left,small);RenderTexture.active=small;pixels.ReadPixels(new Rect(0,0,w,h),0,0,false);
            Graphics.Blit(right,small);RenderTexture.active=small;pixels.ReadPixels(new Rect(0,0,w,h),w,0,false);
            return pixels.GetRawTextureData().AsSpan().ToArray();
        }
        finally
        {
            RenderTexture.active=previous;
            if(small!=null)RenderTexture.ReleaseTemporary(small);
            if(pixels!=null)UnityEngine.Object.Destroy(pixels);
        }
    }
}
