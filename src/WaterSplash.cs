using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
// 0.1.111: slapping the water surface with a hand (empty or holding a weapon)
// splashes: the game's own water impact particles (lying on the water) and
// alternately the game's water splash sound and the mod's recording at
// the point where the hand crossed the surface, plus a short vibration.
// The surface is the game's bullet-splash shape on each water body
// (WaterProjectileDetection); a ray from the hand's previous position to the
// current one finds the crossing.
internal sealed class WaterSplash:IDisposable
{
    private readonly CameraRig rig;
    private readonly List<Collider> water=new();
    private readonly Vector3[] last=new Vector3[2];private readonly bool[] has=new bool[2];private readonly float[] cooldown=new float[2];
    private readonly List<(GameObject go,float until)> live=new();
    private ParticleSystem[]? particles;private string sound="";
    private readonly ChairImpactClip recording=new(WaterSplashSound.Wav,"water.m4a splash recording",.95f,16);
    private Il2CppSystem.Collections.Generic.List<SurfaceDetailsSFX.ParameterToAmount>? parameters;
    private readonly Il2CppSystem.Collections.Generic.List<SurfaceDetailsSFX.ParameterToAmount> none=new();
    private bool searched;private string scene="";private int scanEpoch=int.MinValue;private float nextScan,nextError,nextReport;
    internal WaterSplash(CameraRig owner){rig=owner;}
    internal void Tick()
    {
        try
        {
            float now=Time.realtimeSinceStartup;
            // 0.1.162: the water bodies stay where they are: every 30 s (0.1.164) and after a scene change (was every 3 s).
            if(SceneScan.Due(ref nextScan,30,ref scanEpoch)){long scanStart=SceneScan.Begin();Scan();SceneScan.End("water",scanStart);}
            Expire(now);
            if(water.Count==0||rig.Scripted||rig.Frontend||GameUiControls.Current?.BlocksGameplay==true||!rig.SampleWorldHands(out var left,out var right,out bool leftValid))
            {has[0]=has[1]=false;return;}
            float dt=Time.deltaTime;if(!(dt>0)){return;}
            if(leftValid)Sample(0,Palm(left,false),dt,now);else has[0]=false;
            Sample(1,Palm(right,true),dt,now);
        }
        catch(Exception ex)
        {
            has[0]=has[1]=false;
            if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+10;Bootstrap.Warn("WATER SPLASH: "+ex.Message);}
        }
    }
    private static Vector3 Palm(PoseValue pose,bool right)=>CameraRig.UnityPosition(pose)+GloveVisual.Rotation(pose,right)*new Vector3(0,-.015f,.04f);
    private void Sample(int i,Vector3 p,float dt,float now)
    {
        if(!has[i]){last[i]=p;has[i]=true;return;}
        var from=last[i];last[i]=p;var delta=p-from;float length=delta.magnitude;
        if(length<.004f||length>.6f)return;
        float speed=length/dt;
        if(now<cooldown[i]||!WaterSplashMath.Slap(speed,delta.y/length))return;
        var ray=new Ray(from,delta/length);
        foreach(var c in water)
        {
            if(c==null||!c.enabled||!c.gameObject.activeInHierarchy)continue;
            if(!c.Raycast(ray,out var hit,length+.02f))continue;
            cooldown[i]=now+.25f;Splash(hit.point,speed,i==1);return;
        }
    }
    private void Scan()
    {
        string current=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if(current!=scene){scene=current;searched=false;particles=null;sound="";parameters=null;}
        water.Clear();
        foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<WaterProjectileDetection>()))
        {
            var d=obj.TryCast<WaterProjectileDetection>();if(d==null||!d.gameObject.scene.IsValid())continue;
            foreach(var component in d.GetComponentsInChildren(Il2CppType.Of<Collider>(),true))
            {var c=component.TryCast<Collider>();if(c!=null)water.Add(c);}
        }
    }
    // The game's own impact tables: the first with a Water entry gives the
    // particles; the sound prefers a fist/melee table, then any with Water.
    private void FindAssets()
    {
        if(searched)return;searched=true;
        string vfxFrom="",sfxFrom="";int sfxRank=-1;
        foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<SurfaceHitVfxSfxParameters>()))
        {
            var table=obj.TryCast<SurfaceHitVfxSfxParameters>();if(table==null)continue;
            try
            {
                if(particles==null&&table.vfxDictionary!=null)
                    foreach(var entry in table.vfxDictionary)
                        if(entry?.keys!=null&&entry.keys.Contains(SurfaceDetection.SurfaceTypes.Water)&&entry.particleList!=null&&entry.particleList.Length>0)
                        {var list=new List<ParticleSystem>();foreach(var p in entry.particleList)if(p!=null)list.Add(p);if(list.Count>0){particles=list.ToArray();vfxFrom=table.name;break;}}
                if(table.sfxDictionary!=null&&!string.IsNullOrWhiteSpace(table.fmodEvent))
                    foreach(var entry in table.sfxDictionary)
                        if(entry?.keys!=null&&entry.keys.Contains(SurfaceDetection.SurfaceTypes.Water))
                        {
                            string n=(table.name+" "+table.fmodEvent).ToLowerInvariant();
                            int rank=n.Contains("fist")?3:n.Contains("melee")?2:1;
                            if(rank>sfxRank){sfxRank=rank;sound=table.fmodEvent;parameters=entry.paramToAmount;sfxFrom=table.name;}
                            break;
                        }
            }
            catch(Exception){}
        }
        if(sound.Length==0)
        {
            // Fallback: the player's own swim stroke sound.
            try
            {
                var handler=rig.PlayerRoot?.GetComponentInChildren(Il2CppType.Of<PlayerAudioHandler>(),true)?.TryCast<PlayerAudioHandler>();
                if(handler!=null&&!string.IsNullOrWhiteSpace(handler.swimEvent)){sound=handler.swimEvent;parameters=null;sfxFrom="player swim";}
            }
            catch(Exception){}
        }
        Bootstrap.Write("WATER SPLASH surfaces="+water.Count+" particles="+(particles?.Length??0)+" from "+(vfxFrom==""?"none":vfxFrom)+"; sound="+(sound==""?"none":sound)+" from "+(sfxFrom==""?"none":sfxFrom));
    }
    private void Splash(Vector3 point,float speed,bool right)
    {
        FindAssets();
        float strength=WaterSplashMath.Strength(speed);
        if(particles!=null)
            foreach(var prefab in particles)
            {
                try
                {
                    // 0.1.112: the game's impact particles face along their Z
                    // (the hit normal): point Z up so the rings lie on the water.
                    var go=UnityEngine.Object.Instantiate(prefab.gameObject,point,Quaternion.LookRotation(Vector3.up,Vector3.forward));
                    go.transform.localScale=prefab.transform.localScale*(.6f+.6f*strength);
                    var system=go.GetComponent(Il2CppType.Of<ParticleSystem>())?.TryCast<ParticleSystem>();
                    system?.Play(true);
                    live.Add((go,Time.realtimeSinceStartup+4));
                }
                catch(Exception ex){if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+10;Bootstrap.Warn("WATER SPLASH particles: "+ex.Message);}}
            }
        // 0.1.113: the game's own water splash and the mod's recording take
        // turns; if one cannot play, the other one does.
        bool useRecording=WaterSplashMath.RecordingTurn(ref turn);
        if(!(useRecording?recording.Play(point)||PlayNative(point):PlayNative(point)||recording.Play(point)))
            if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+10;Bootstrap.Warn("WATER SPLASH no sound available");}
        rig.SwimHaptics(.3f+.5f*strength);
        if(Time.realtimeSinceStartup>=nextReport){nextReport=Time.realtimeSinceStartup+2;Bootstrap.Write("WATER SPLASH hand="+(right?"R":"L")+" speed="+speed.ToString("F2")+" at "+point.ToString("F2"));}
    }
    private int turn;
    private bool PlayNative(Vector3 point)
    {
        if(sound.Length==0)return false;
        try{PropStudioSound.Play(sound,point,parameters??none);return true;}
        catch(Exception ex){if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+10;Bootstrap.Warn("WATER SPLASH sound: "+ex.Message);}return false;}
    }
    private void Expire(float now)
    {
        for(int i=live.Count-1;i>=0;i--)
        {
            if(live[i].go==null){live.RemoveAt(i);continue;}
            if(now>=live[i].until){UnityEngine.Object.Destroy(live[i].go);live.RemoveAt(i);}
        }
    }
    public void Dispose()
    {
        recording.Dispose();
        foreach(var (go,_) in live)if(go!=null)UnityEngine.Object.Destroy(go);
        live.Clear();water.Clear();
    }
}
