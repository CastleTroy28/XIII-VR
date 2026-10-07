using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace XiiiXR;
// 0.1.248: an aim dot (VR SETTINGS "Aim dot": off, the default; a dot; a dot
// and a laser). The game's crosshair is drawn for a flat screen, in front of
// the eyes, and the mod hides it: a gun is aimed along its own sights. A
// player who wants help sees a small red dot where the gun's shot goes (the
// same line the shot takes: from the muzzle along the gun, stopped by what
// stops a shot), and with the laser a thin red line from the muzzle to it.
// Firearms only (not a knife, a grenade or a thing held), drawn once a frame.
internal sealed class AimDot:IDisposable
{
    private GameObject? dot,beam;private Material? dotMaterial,beamMaterial;
    private readonly Il2CppStructArray<RaycastHit> hits=new(32);
    private bool failed;private int shownFrame=-1;
    private static bool reported;
    // One gun's dot (the game's weapon, or the second pistol): from its muzzle along it.
    internal void Show(Vector3 origin,Vector3 forward,int mask,Transform? player,int mode)
    {
        if(failed)return;
        mode=AimDotMath.Mode(mode);
        if(mode==0||!float.IsFinite(origin.x)||forward.sqrMagnitude<1e-6f){Hide();return;}
        try
        {
            Ensure();
            forward=forward.normalized;float nearest=AimDotMath.Range;bool hit=false;Vector3 point=origin+forward*AimDotMath.LaserMiss;
            int n=Physics.RaycastNonAlloc(origin,forward,hits,AimDotMath.Range,mask,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n&&i<hits.Length;i++)
            {
                var h=hits[i];var c=h.collider;
                if(c==null||player!=null&&c.transform.IsChildOf(player)||h.distance<=0||h.distance>=nearest)continue;
                nearest=h.distance;point=h.point;hit=true;
            }
            if(hit)
            {
                var at=point-forward*Math.Min(AimDotMath.SurfaceGap,nearest*.5f);
                dot!.transform.position=at;dot.transform.localScale=Vector3.one*AimDotMath.DotSize(nearest);dot.SetActive(true);
            }
            else dot!.SetActive(false);
            if(mode==2)
            {
                var to=hit?point:origin+forward*AimDotMath.LaserMiss;var delta=to-origin;float length=delta.magnitude;
                if(length>.01f){beam!.transform.SetPositionAndRotation((origin+to)*.5f,Quaternion.LookRotation(delta));beam.transform.localScale=new Vector3(AimDotMath.LaserWidth,AimDotMath.LaserWidth,length);beam.SetActive(true);}
                else beam!.SetActive(false);
            }
            else beam!.SetActive(false);
            shownFrame=Time.frameCount;
            if(!reported){reported=true;Bootstrap.Write("AIM DOT shown ("+(mode==2?"dot and laser":"dot")+"): where the gun's shot goes, from its muzzle along it (VR SETTINGS \"Aim dot\")");}
        }
        catch(Exception ex){failed=true;Dispose();Bootstrap.Warn("AIM DOT unavailable: "+ex.Message);}
    }
    // Not shown this frame (no gun, the option off): hidden.
    internal void HideUnlessShown(){if(shownFrame!=Time.frameCount)Hide();}
    internal void Hide(){if(dot!=null)dot.SetActive(false);if(beam!=null)beam.SetActive(false);}
    private void Ensure()
    {
        if(dot!=null&&beam!=null)return;
        Dispose();
        var shader=Shader.Find("Sprites/Default");if(shader==null)shader=Shader.Find("Unlit/Color");
        if(shader==null)throw new InvalidOperationException("no unlit shader");
        dotMaterial=new Material(shader){color=new Color(1f,.08f,.05f,.95f)};dotMaterial.renderQueue=3100;
        beamMaterial=new Material(shader){color=new Color(1f,.1f,.05f,.35f)};beamMaterial.renderQueue=3100;
        dot=Make(PrimitiveType.Sphere,"XIII aim dot",dotMaterial);
        beam=Make(PrimitiveType.Cube,"XIII aim laser",beamMaterial);
    }
    private static GameObject Make(PrimitiveType type,string name,Material material)
    {
        var go=GameObject.CreatePrimitive(type);
        try
        {
            go.SetActive(false);go.name=name;go.layer=0;UnityEngine.Object.DontDestroyOnLoad(go);
            var collider=go.GetComponent(Il2CppType.Of<Collider>())?.TryCast<Collider>();
            if(collider!=null){collider.enabled=false;UnityEngine.Object.Destroy(collider);}
            var renderer=go.GetComponent(Il2CppType.Of<Renderer>())!.TryCast<Renderer>()!;
            renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            return go;
        }
        catch{UnityEngine.Object.Destroy(go);throw;}
    }
    public void Dispose()
    {
        if(dot!=null)UnityEngine.Object.Destroy(dot);if(beam!=null)UnityEngine.Object.Destroy(beam);
        if(dotMaterial!=null)UnityEngine.Object.Destroy(dotMaterial);if(beamMaterial!=null)UnityEngine.Object.Destroy(beamMaterial);
        dot=beam=null;dotMaterial=beamMaterial=null;
    }
}
