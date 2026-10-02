using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
namespace XiiiXR;
// Use a private instance of a native image-effect component so Unity invokes
// the callback through its existing native image-effect registration. Never
// enable the game's existing desktop effects or keep an eye-image/history.
internal sealed class StoryColorEffect:IDisposable
{
    internal static bool Enabled=true;
    private static readonly StoryEffectState State=new();
    private static readonly Dictionary<int,StoryColorEffect> Owned=new();
    private static readonly Harmony Hooks=new("xiii.vr.xrbootstrap.storycolor");
    private static bool hooked;
    private static GameObject? attaching;
    private static int sampleFrame=-1;
    private static bool sampleMemory;
    private static float sampleWhite;
    private readonly Camera camera;
    private readonly StoryVolumeColor volumes;
    private CustomPostProcessing? carrier;
    private int carrierId;
    private PropertySheetFactory? sheets;
    private PropertySheet? sheet;
    private CommandBuffer? command;
    private Texture2D? lut;
    private Il2CppStructArray<Color>? pixels;
    private bool disposed,reported,callbackReported,skipReported,probePassed,probeAttempted;
    private float lutGray=-1,frameGray;
    private int colorFrame=-1,warmupCallbacks=4,lastStateBits=-1;
    private float nextStateReport;
    private float nextSampleWarning;
    private float lutWhite=-1,lutBlack=-1,frameBlack,nextFind;
    private float nextProbe;
    private static readonly int LutId=Shader.PropertyToID("_Lut2D");
    private static readonly int LutParams=Shader.PropertyToID("_Lut2D_Params");
    internal StoryColorEffect(Camera target)
    {
        camera=target;volumes=new StoryVolumeColor(target);Install();
        try
        {
            attaching=target.gameObject;
            carrier=target.gameObject.AddComponent(Il2CppType.Of<CustomPostProcessing>()).TryCast<CustomPostProcessing>();
            if(carrier==null)throw new InvalidOperationException("Native image-effect carrier unavailable");
            carrierId=carrier.GetInstanceID();Owned.Add(carrierId,this);carrier.enabled=true;
            Bootstrap.Write("STORY COLOR native carrier attached camera="+camera.name);
        }
        finally{attaching=null;}
    }
    internal static bool Owns(Behaviour component)=>Owned.ContainsKey(component.GetInstanceID());
    // Native Awake destroys a component with no serialized material. Bypass it
    // only during our synchronous AddComponent; leave every native instance alone.
    private static bool CarrierAwake(CustomPostProcessing __instance)=>attaching==null||__instance.gameObject!=attaching;
    private static bool Image(CustomPostProcessing __instance,RenderTexture src,RenderTexture dest)
    {
        if(!Owned.TryGetValue(__instance.GetInstanceID(),out var effect))return true;
        bool rendered=false;
        try{rendered=effect.Render(src,dest);}
        catch(Exception ex){if(!effect.skipReported){effect.skipReported=true;Bootstrap.Warn("STORY COLOR render failed: "+ex.Message);}}
        if(!rendered)Graphics.Blit(src,dest);
        return false;
    }
    internal static void Install()
    {
        if(hooked)return;
        Hooks.Patch(AccessTools.DeclaredMethod(typeof(CustomPostProcessing),"Awake"),prefix:new HarmonyMethod(typeof(StoryColorEffect),nameof(CarrierAwake)));
        Hooks.Patch(AccessTools.DeclaredMethod(typeof(CustomPostProcessing),"OnRenderImage"),prefix:new HarmonyMethod(typeof(StoryColorEffect),nameof(Image)));
        Patch(typeof(PlayerCameraEffectController),"TriggerFlashbackEffect",nameof(Flash));
        Patch(typeof(GameManager),"ChangeFlashbackState",nameof(Changed));
        Patch(typeof(FlashbackSequence),"TeleportPlayerStart",nameof(Enter));
        Patch(typeof(FlashbackSequence),"TeleportPlayerEnd",nameof(Exit));
        Patch(typeof(FlashbackSequence),"ResetFlashback",nameof(Reset));
        Patch(typeof(FlashbackSequence),"StartFlashback",nameof(SequenceFlash));
        Patch(typeof(FlashbackSequence),"EndFlashback",nameof(SequenceFlash));
        // OnBehaviourPlay contains an inlined animator write, not a call to
        // TriggerFlashbackEffect. The previous method-only patch missed it.
        Patch(typeof(FlashbackEffectBehaviour),"OnBehaviourPlay",nameof(TimelineFlash));
        Patch(typeof(AddressableSceneGameManager),"SetActiveSubscene",nameof(Subscene));
        Patch(typeof(PlayerBlinkBehaviour),"ProcessFrame",nameof(BlinkFrame));
        Patch(typeof(PlayerBlinkBehaviour),"OnBehaviourPause",nameof(BlinkEnd));
        // Do not detour OnGraphStop: IL2CPP folds this empty method into a
        // shared native RET used by unrelated methods with other signatures.
        // A bool argument can then be marshalled as a Playable pointer (0x1).
        // OnBehaviourPause clears the owner; stale-frame expiry also handles
        // graphs removed without a pause callback, and SceneChanged resets it.
        hooked=true;Bootstrap.Write("STORY COLOR hooks installed: Timeline flash + active flashback subscene + native volumes");
    }
    private static void BlinkFrame(PlayerBlinkBehaviour __instance)
    {try{StoryBlink.Sample(__instance);}catch(Exception ex){Bootstrap.Warn("STORY BLINK sample: "+ex.Message);}}
    private static void BlinkEnd(PlayerBlinkBehaviour __instance)=>StoryBlink.End(__instance);
    private static void Patch(Type type,string method,string handler)=>Hooks.Patch(AccessTools.DeclaredMethod(type,method),postfix:new HarmonyMethod(typeof(StoryColorEffect),handler));
    private static void SequenceFlash(){if(CameraRig.Current==null)return;State.Trigger(Time.realtimeSinceStartup);sampleFrame=-1;Bootstrap.Write("STORY COLOR flash requested");}
    private static void TimelineFlash()
    {if(CameraRig.Current==null)return;State.Pulse(Time.realtimeSinceStartup);sampleFrame=-1;Bootstrap.Write("STORY COLOR Timeline flash requested");}
    private static void Subscene(bool isFlashback)
    {if(CameraRig.Current==null)return;State.Observe(isFlashback,Time.realtimeSinceStartup);sampleFrame=-1;Bootstrap.Write("STORY COLOR active subscene flashback="+isFlashback);}
    private static void Flash(PlayerCameraEffectController __instance)
    {if(CameraRig.Current!=null&&__instance.GetOwner().IsPlayer)SequenceFlash();}
    private static void Changed(GameManager.FlashbackState state)
    {if(CameraRig.Current==null)return;State.Transition(state==GameManager.FlashbackState.Flashback,Time.realtimeSinceStartup);sampleFrame=-1;Bootstrap.Write("STORY COLOR native state="+state);}
    private static void Enter()=>Changed(GameManager.FlashbackState.Flashback);
    private static void Exit()=>Changed(GameManager.FlashbackState.Normal);
    private static void Reset(){State.Reset();sampleFrame=-1;}
    internal static void SceneChanged(){sampleFrame=-1;StoryBlink.Reset();}
    internal static void Tick()
    {foreach(var effect in Owned.Values)effect.KeepEnabled();}
    private static void Sample()
    {
        if(sampleFrame==Time.frameCount)return;
        sampleFrame=Time.frameCount;float now=Time.realtimeSinceStartup;
        bool memory=GameManager.Instance!=null&&GameManager.Instance.currentFlashbackState==GameManager.FlashbackState.Flashback;
        // Scene loading/checkpoints can select a flashback without calling the
        // old FlashbackSequence methods. Query native scene identity as well.
        var scenes=AddressableSceneGameManager.Instance;
        if(scenes!=null)memory|=scenes.IsCurrentActiveSceneFlashback();
        State.Observe(memory,now);
        sampleMemory=State.Memory;sampleWhite=State.White(now);
    }
    private void SampleColor()
    {
        Sample();if(colorFrame==Time.frameCount)return;colorFrame=Time.frameCount;
        frameBlack=StoryBlink.Amount;
        float volume=volumes.Read();bool scripted=CameraRig.Current?.Scripted==true;
        frameGray=StoryVolumeMath.Applied(volume,sampleMemory,scripted);
        int bits=(sampleMemory?1:0)|(frameGray>.001f?2:0)|(sampleWhite>.001f?4:0)|(volume>.001f?8:0);
        if(bits!=lastStateBits||Time.realtimeSinceStartup>=nextStateReport)
        {
            lastStateBits=bits;nextStateReport=Time.realtimeSinceStartup+30;
            Bootstrap.Write("STORY COLOR sample scene="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name+" memory="+sampleMemory+" gray="+frameGray+" volumeGray="+volume+(volume>.001f?" ("+(scripted?"cutscene: applied":"gameplay: ignored")+"; "+volumes.Contributors+")":"")+" flash="+sampleWhite+" callback="+callbackReported+" enabled="+Enabled);
        }
    }
    internal void KeepEnabled()
    {
        try
        {
        if(disposed||carrier==null)return;
        SampleColor();
        bool active=Enabled&&(warmupCallbacks>0||frameGray>.001f||sampleWhite>0||frameBlack>0)&&CameraRig.Current?.MainCamera==camera;
        if(carrier.enabled!=active)carrier.enabled=active;
        }
        catch(Exception ex)
        {
            if(Time.realtimeSinceStartup>=nextSampleWarning){nextSampleWarning=Time.realtimeSinceStartup+10;Bootstrap.Warn("STORY COLOR sampling retry; tracking continues: "+ex.Message);}
        }
    }
    private bool Render(RenderTexture source,RenderTexture destination)
    {
        if(!callbackReported){callbackReported=true;Bootstrap.Write("STORY IMAGE native callback camera="+camera.name+" source="+(source==null?"none":source.width+"x"+source.height+" "+source.dimension));}
        if(disposed||!Enabled||CameraRig.Current?.MainCamera!=camera||source==null)return false;
        SampleColor();
        if(source.dimension!=TextureDimension.Tex2D||camera.stereoTargetEye==StereoTargetEyeMask.None||UnityEngine.XR.XRSettings.stereoRenderingMode!=UnityEngine.XR.XRSettings.StereoRenderingMode.MultiPass)
        {if(!skipReported){skipReported=true;Bootstrap.Warn("STORY COLOR requires a multipass 2D eye image");}return false;}
        if(warmupCallbacks>0)warmupCallbacks--;
        // 0.1.162: nothing to grade - no shader search and no GPU probe (the
        // probe waits for the graphics card three times; it ran in the first
        // frame of every mission, already the longest one).
        if(frameGray<=.001f&&sampleWhite<=0&&frameBlack<=0)return false;
        if(!EnsureShader())return false;
        // Verify the shader variant with synthetic colors, never a captured eye.
        if(!probePassed&&(!probeAttempted||Time.realtimeSinceStartup>=nextProbe))
        {probeAttempted=true;nextProbe=Time.realtimeSinceStartup+10;probePassed=Probe();}
        if(!probePassed)return false;
        UpdateLut(frameGray,sampleWhite,frameBlack);
        if(destination!=null)Draw(source,destination);
        else
        {
            // Never ask a command buffer to choose an XR eye via CameraTarget.
            // Grade this source into a scoped 2D target, then let Unity's normal
            // final Blit route it to the callback's eye. No target survives it.
            var descriptor=source.descriptor;descriptor.depthBufferBits=0;descriptor.msaaSamples=1;
            var output=RenderTexture.GetTemporary(descriptor);
            try{Draw(source,output);Graphics.Blit(output,destination);}
            finally{RenderTexture.ReleaseTemporary(output);}
        }
        if(!reported){reported=true;Bootstrap.Write("STORY COLOR per-eye pass active camera="+camera.name+" memory="+sampleMemory+" white="+sampleWhite);}
        return true;
    }
    private bool EnsureShader()
    {
        if(sheet!=null)return true;
        if(Time.realtimeSinceStartup<nextFind)return false;
        nextFind=Time.realtimeSinceStartup+2;
        var shader=Shader.Find("Hidden/PostProcessing/Uber");
        if(shader==null)
        {
            foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<PostProcessResources>()))
            {var candidate=obj.TryCast<PostProcessResources>()?.shaders?.uber;if(candidate!=null&&candidate.isSupported){shader=candidate;break;}}
        }
        if(shader==null||!shader.isSupported)
        {if(!skipReported){skipReported=true;Bootstrap.Warn("STORY COLOR native Uber shader not loaded yet; retrying");}return false;}
        sheets=new PropertySheetFactory();sheet=sheets.Get(shader);sheet.ClearKeywords();sheet.properties.Clear();
        sheet.EnableKeyword("COLOR_GRADING_LDR_2D");
        sheet.properties.SetTexture(ShaderIDs.AutoExposureTex,Texture2D.whiteTexture);
        bool top=SystemInfo.graphicsUVStartsAtTop;
        sheet.properties.SetVector(ShaderIDs.UVTransform,new Vector4(1,top?-1:1,0,top?1:0));
        const int n=StoryColorLut.Size;
        lut=new Texture2D(n*n,n,TextureFormat.RGBA32,false,true);lut.name="XIII story color lookup";
        lut.filterMode=FilterMode.Bilinear;lut.wrapMode=TextureWrapMode.Clamp;
        pixels=new Il2CppStructArray<Color>(n*n*n);
        sheet.properties.SetTexture(LutId,lut);sheet.properties.SetVector(LutParams,new Vector4(1f/(n*n),1f/n,n-1,0));
        command=new CommandBuffer();command.name="XIII isolated eye story color";
        Bootstrap.Write("STORY COLOR shader ready: "+shader.name+"; no native volume or LUT baker required");
        return true;
    }
    private void UpdateLut(float grayscale,float white,float black=0)
    {
        if(lutWhite==white&&lutGray==grayscale&&lutBlack==black)return;
        const int n=StoryColorLut.Size;
        for(int g=0;g<n;g++)for(int b=0;b<n;b++)for(int r=0;r<n;r++)
        {
            StoryColorLut.Sample(r/(n-1f),g/(n-1f),b/(n-1f),grayscale,white,out float x,out float y,out float z);
            pixels![g*n*n+b*n+r]=new Color(x*(1-black),y*(1-black),z*(1-black),1);
        }
        lut!.SetPixels(pixels!);lut.Apply(false,false);lutGray=grayscale;lutWhite=white;lutBlack=black;
    }
    private void Draw(Texture source,RenderTexture destination)
    {
        command!.Clear();
        var dest=new RenderTargetIdentifier(destination);
        // IL2CPP value types are managed reference wrappers: default(Nullable)
        // is a null wrapper, not a boxed native Nullable with HasValue=false.
        // Supply the actual output viewport to avoid null unboxing in interop.
        var viewport=new Il2CppSystem.Nullable<Rect>(new Rect(0,0,destination.width,destination.height));
        RuntimeUtilities.BlitFullscreenTriangle(command,new RenderTargetIdentifier(source),dest,sheet!,0,false,viewport);
        Graphics.ExecuteCommandBuffer(command);
    }
    private bool Probe()
    {
        var saved=RenderTexture.active;Texture2D? input=null,readback=null;RenderTexture? inputTarget=null,output=null;
        try
        {
            input=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            input.SetPixels(new Color[]{Color.red,Color.red,Color.green,Color.green});input.Apply(false,false);
            inputTarget=new RenderTexture(2,2,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);inputTarget.Create();
            Graphics.Blit(input,inputTarget);
            output=new RenderTexture(2,2,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);output.Create();
            readback=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            UpdateLut(1,0);Draw(inputTarget,output);RenderTexture.active=output;readback.ReadPixels(new Rect(0,0,2,2),0,0,false);
            var a=readback.GetPixel(0,0);var b=readback.GetPixel(0,1);
            bool gray=MathF.Abs(a.r-a.g)<.03f&&MathF.Abs(a.g-a.b)<.03f&&a.r>.03f&&a.r<.95f;
            bool detail=MathF.Abs(a.r-b.r)>.08f;
            if(gray&&detail&&a.r>b.r)
            {
                bool top=SystemInfo.graphicsUVStartsAtTop;
                sheet!.properties.SetVector(ShaderIDs.UVTransform,new Vector4(1,top?1:-1,0,top?0:1));
                Draw(inputTarget,output);RenderTexture.active=output;readback.ReadPixels(new Rect(0,0,2,2),0,0,false);
                a=readback.GetPixel(0,0);b=readback.GetPixel(0,1);
            }
            bool orientation=b.r>a.r+.08f;
            UpdateLut(0,1);Draw(inputTarget,output);RenderTexture.active=output;readback.ReadPixels(new Rect(0,0,2,2),0,0,false);
            var w=readback.GetPixel(0,0);bool white=w.r>.95f&&w.g>.95f&&w.b>.95f;
            bool passed=gray&&detail&&white&&orientation;
            Bootstrap.Write("STORY COLOR GPU probe "+(passed?"PASS":"FAIL")+" grayscale="+gray+" detail="+detail+" flash="+white+" orientation="+orientation+" sample="+a);
            return passed;
        }
        catch(Exception ex){Bootstrap.Warn("STORY COLOR GPU probe failed; retry in 10 seconds: "+ex);return false;}
        finally
        {
            RenderTexture.active=saved;
            if(inputTarget!=null){inputTarget.Release();UnityEngine.Object.Destroy(inputTarget);}
            if(output!=null){output.Release();UnityEngine.Object.Destroy(output);}
            if(input!=null)UnityEngine.Object.Destroy(input);if(readback!=null)UnityEngine.Object.Destroy(readback);
        }
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        if(carrier!=null){carrier.enabled=false;Owned.Remove(carrierId);UnityEngine.Object.Destroy(carrier);carrier=null;}
        sheets?.Release();sheets=null;sheet=null;command?.Release();command=null;
        if(lut!=null)UnityEngine.Object.Destroy(lut);lut=null;pixels=null;
        if(Owned.Count==0){Reset();StoryBlink.Reset();}
    }
}
