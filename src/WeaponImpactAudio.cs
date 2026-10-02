using System;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
// 0.1.137: a thrown or dropped weapon (and a dropped magazine) makes a sound
// where it lands, chosen by the surface (the game's own SurfaceDetection)
// and by whether there is a ceiling above (indoors): the supplied gun-drop
// recording on hard floors and wood indoors, filtered copies of it for
// carpet, open hard ground and soft ground (snow, dirt, grass), pitched for
// metal and glass, the water splash recording in water (WeaponImpactMath).
internal sealed class WeaponImpactAudio:IDisposable
{
    internal static WeaponImpactAudio? Current{get;private set;}
    private readonly ChairImpactClip room=new(WeaponDropSounds.Room,"gun drop recording (hard floor)",1f,24);
    private readonly ChairImpactClip carpet=new(WeaponDropSounds.Carpet,"gun drop (carpet)",1f,16);
    private readonly ChairImpactClip ground=new(WeaponDropSounds.Ground,"gun drop (open ground)",1f,20);
    private readonly ChairImpactClip soft=new(WeaponDropSounds.Soft,"gun drop (soft ground)",1f,14);
    private readonly ChairImpactClip water=new(WaterSplashSound.Wav,"gun drop (water)",.5f,16);
    private SurfaceDetection? detector;private float nextFind,nextReport;private readonly System.Collections.Generic.HashSet<DropSound> reported=new();
    private readonly RaycastHit[] hits=new RaycastHit[8];
    private readonly System.Random random=new();
    internal WeaponImpactAudio(){Current=this;}
    internal void Prepare(){room.Prepare();}
    // speed: towards the surface (m/s); normal: the surface's.
    internal void Hit(Vector3 point,Vector3 normal,float speed,string profile)
    {
        float volume=WeaponImpactMath.Volume(speed);if(volume<=0)return;
        try
        {
            var player=CameraRig.Current?.PlayerRoot;
            if(normal.sqrMagnitude<1e-6f)normal=Vector3.up;normal.Normalize();
            int surface=Surface(point,normal,player,out bool terrain);
            bool roofed=Roofed(point,player);
            var kind=WeaponImpactMath.Pick(surface,roofed,terrain);
            var (gain,pitch)=WeaponImpactMath.Weight(profile);
            pitch*=WeaponImpactMath.Pitch(kind)*(1+(float)(random.NextDouble()-.5)*.08f);
            var clip=kind switch{DropSound.Carpet=>carpet,DropSound.Ground=>ground,DropSound.Soft=>soft,DropSound.Water=>water,_=>room};
            bool ok=clip.Play(point,Math.Min(1,volume*gain),pitch,true);
            if(reported.Add(kind)||Time.realtimeSinceStartup>=nextReport)
            {nextReport=Time.realtimeSinceStartup+10;Bootstrap.Write("WEAPON DROP SOUND "+profile+" on "+(surface>=0?((SurfaceDetection.SurfaceTypes)surface).ToString():"unknown")+(terrain?" (terrain)":"")+(roofed?" indoors":" outside")+" -> "+kind+" volume="+volume.ToString("F2")+" speed="+speed.ToString("F1")+(ok?"":" (not played)"));}
        }
        catch(Exception ex){if(Time.realtimeSinceStartup>=nextReport){nextReport=Time.realtimeSinceStartup+10;Bootstrap.Warn("WEAPON DROP SOUND: "+ex.Message);}}
    }
    // 0.1.146: two held guns knocked together: the gun recording, lower and
    // quieter (a dull metallic knock), heavier guns lower still.
    private float nextKnockReport;
    internal void Knock(Vector3 point,float speed,string a,string b)
    {
        float volume=GunKnock.Volume(speed);if(volume<=0)return;
        try
        {
            var (ga,pa)=WeaponImpactMath.Weight(a);var (gb,pb)=WeaponImpactMath.Weight(b);
            float pitch=Math.Min(pa,pb)*.78f*(1+(float)(random.NextDouble()-.5)*.1f);
            bool ok=room.Play(point,Math.Min(.85f,volume*Math.Max(ga,gb)),pitch,true);
            if(Time.realtimeSinceStartup>=nextKnockReport){nextKnockReport=Time.realtimeSinceStartup+5;Bootstrap.Write("GUN KNOCK SOUND "+a+"/"+b+" volume="+volume.ToString("F2")+" pitch="+pitch.ToString("F2")+(ok?"":" (not played)"));}
        }
        catch(Exception ex){if(Time.realtimeSinceStartup>=nextReport){nextReport=Time.realtimeSinceStartup+10;Bootstrap.Warn("GUN KNOCK SOUND: "+ex.Message);}}
    }
    private int Surface(Vector3 point,Vector3 normal,Transform? player,out bool terrain)
    {
        terrain=false;
        int count=Physics.RaycastNonAlloc(point+normal*.12f,-normal,hits,.35f,~0,QueryTriggerInteraction.Ignore);
        RaycastHit best=default;float nearest=float.PositiveInfinity;bool found=false;
        for(int i=0;i<count&&i<hits.Length;i++)
        {
            var c=hits[i].collider;if(c==null||player!=null&&c.transform.IsChildOf(player))continue;
            if(hits[i].distance<nearest){nearest=hits[i].distance;best=hits[i];found=true;}
        }
        if(!found)return -1;
        try{terrain=best.collider.GetIl2CppType().Name=="TerrainCollider";}catch(Exception){terrain=false;}
        if(detector==null&&Time.realtimeSinceStartup>=nextFind)
        {
            nextFind=Time.realtimeSinceStartup+5;
            detector=UnityEngine.Object.FindObjectOfType(Il2CppType.Of<SurfaceDetection>())?.TryCast<SurfaceDetection>();
            Bootstrap.Write("WEAPON DROP SOUND surface detection "+(detector!=null?"found":"not found (the room/open-ground guess only)"));
        }
        if(detector==null)return -1;
        try{return (int)detector.ObtainSurfaceTypeFromRaycastHit(best);}catch(Exception){detector=null;return -1;}
    }
    // A ceiling within 12 m straight above: indoors.
    private bool Roofed(Vector3 point,Transform? player)
    {
        int count=Physics.RaycastNonAlloc(point+Vector3.up*.25f,Vector3.up,hits,12f,~0,QueryTriggerInteraction.Ignore);
        for(int i=0;i<count&&i<hits.Length;i++)
        {var c=hits[i].collider;if(c!=null&&(player==null||!c.transform.IsChildOf(player)))return true;}
        return false;
    }
    public void Dispose()
    {
        if(Current==this)Current=null;
        room.Dispose();carpet.Dispose();ground.Dispose();soft.Dispose();water.Dispose();
    }
}
