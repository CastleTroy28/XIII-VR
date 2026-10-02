using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
namespace XiiiXR;
// 0.1.129: pulled grenade pins. The pin stays for a moment between the
// fingers that pulled it, then drops, falls and lies on the floor for a
// while (render-only, no collider; the fall is a swept kinematic drop).
internal sealed class GrenadePins:IDisposable
{
    private sealed class Pin
    {
        internal GameObject Root=null!;internal Mesh Mesh=null!;internal bool Right;internal Matrix4x4 Relative;
        internal bool Falling,Resting;internal Vector3 Position,Velocity,Spin;internal float ReleaseAt,Expires;
    }
    internal const float HoldSeconds=.8f,LifeSeconds=10;
    private readonly List<Pin> pins=new();
    private readonly ContactWorld world=new();
    internal int Count=>pins.Count;
    // mesh: owned from now on; world: where it is drawn now; center: the
    // pin's middle in that mesh's space; hand: the hand that pulled it.
    internal void Add(Mesh mesh,Material[] materials,Matrix4x4 worldMatrix,Vector3 center,Matrix4x4 hand,bool right)
    {
        if(pins.Count>=4)Remove(0);
        GameObject? go=null;
        try
        {
            go=new GameObject("XIII grenade pin");UnityEngine.Object.DontDestroyOnLoad(go);
            var part=new GameObject("pin mesh");part.transform.SetParent(go.transform,false);part.transform.localPosition=-center;
            part.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>()!.sharedMesh=mesh;
            var r=part.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>()!;r.sharedMaterials=materials;r.shadowCastingMode=ShadowCastingMode.Off;
        }
        catch{if(go!=null)UnityEngine.Object.Destroy(go);UnityEngine.Object.Destroy(mesh);throw;}
        var at=worldMatrix*Matrix4x4.Translate(center);
        float now=Time.realtimeSinceStartup;
        var p=new Pin{Root=go,Mesh=mesh,Right=right,Relative=hand.inverse*at,ReleaseAt=now+HoldSeconds,Expires=now+LifeSeconds,Spin=new Vector3(360,120,0)};
        pins.Add(p);Place(p,at);
    }
    // Every frame before drawing: held pins follow their hand; dropped ones fall.
    internal void Tick(Transform? player,Matrix4x4 left,Matrix4x4 right,Vector3 leftVelocity,Vector3 rightVelocity,bool leftValid)
    {
        if(pins.Count==0)return;
        world.Player=player;float now=Time.realtimeSinceStartup,dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
        for(int i=pins.Count-1;i>=0;i--)
        {
            var p=pins[i];
            if(p.Root==null||now>=p.Expires){Remove(i);continue;}
            if(!p.Falling)
            {
                if(now<p.ReleaseAt&&(p.Right||leftValid)){Place(p,(p.Right?right:left)*p.Relative);continue;}
                p.Falling=true;p.Velocity=Vector3.ClampMagnitude(p.Right?rightVelocity:leftVelocity,3);
            }
            if(p.Resting)continue;
            p.Velocity+=Vector3.down*(9.81f*dt);var end=p.Position+p.Velocity*dt;
            float t=world.Sweep(ContactWorld.V(p.Position),ContactWorld.V(end),.008f,out var normal);
            p.Position=Vector3.Lerp(p.Position,end,t);
            if(t<1){p.Velocity=Vector3.Reflect(p.Velocity,ContactWorld.U(normal))*.25f;if(p.Velocity.magnitude<.35f)p.Resting=true;}
            var turn=p.Resting?p.Root.transform.rotation:Quaternion.Euler(p.Spin*dt)*p.Root.transform.rotation;
            p.Root.transform.SetPositionAndRotation(p.Position,turn);
        }
    }
    private static void Place(Pin p,Matrix4x4 m)
    {
        p.Position=m.GetColumn(3);
        p.Root.transform.SetPositionAndRotation(p.Position,m.rotation);p.Root.transform.localScale=m.lossyScale;
    }
    private void Remove(int i)
    {
        var p=pins[i];pins.RemoveAt(i);
        if(p.Root!=null)UnityEngine.Object.Destroy(p.Root);if(p.Mesh!=null)UnityEngine.Object.Destroy(p.Mesh);
    }
    internal void Clear(){for(int i=pins.Count-1;i>=0;i--)Remove(i);}
    public void Dispose(){Clear();world.Dispose();}
}
