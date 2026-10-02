using System;
using System.Collections.Generic;
using UnityEngine;
namespace XiiiXR;
// Bounded render-only debris. It can settle on scenery but cannot create a
// collider that blocks the player, NPCs, hands or a doorway.
internal sealed class ReloadDrops:IDisposable
{
    private sealed class Item
    {
        internal ReloadMesh Mesh=null!;internal Vector3 Position,Velocity;
        internal Quaternion Rotation;internal float Expires;internal bool Resting;
    }
    private readonly List<Item> items=new();
    private readonly ContactWorld world=new();
    internal void Add(ReloadMesh template,Vector3 position,Quaternion rotation,Vector3 velocity)
    {
        if(items.Count>=8){items[0].Mesh.Dispose();items.RemoveAt(0);}
        var item=new Item{Mesh=template.Copy(),Position=position,Rotation=rotation,Velocity=velocity,Expires=Time.realtimeSinceStartup+12};
        items.Add(item);item.Mesh.Pose(position,rotation);
    }
    internal void Tick(Transform? player)
    {
        world.Player=player;float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
        for(int i=items.Count-1;i>=0;i--)
        {
            var a=items[i];if(Time.realtimeSinceStartup>=a.Expires){a.Mesh.Dispose();items.RemoveAt(i);continue;}
            if(a.Resting)continue;
            a.Velocity+=Vector3.down*(9.81f*dt);var end=a.Position+a.Velocity*dt;
            float t=world.Sweep(ContactWorld.V(a.Position),ContactWorld.V(end),.018f,out var normal);
            a.Position=Vector3.Lerp(a.Position,end,t);
            if(t<1)
            {
                var n=ContactWorld.U(normal);
                // 0.1.137: the magazine's knock on the surface.
                if(!a.Resting)WeaponImpactAudio.Current?.Hit(a.Position,n,-Vector3.Dot(a.Velocity,n),"magazine");
                a.Velocity=Vector3.Reflect(a.Velocity,n)*.18f;
                if(a.Velocity.magnitude<.5f)a.Resting=true;
            }
            if(!a.Resting)a.Rotation=Quaternion.AngleAxis(dt*150,Vector3.right)*a.Rotation;
            a.Mesh.Pose(a.Position,a.Rotation);
        }
    }
    public void Dispose(){foreach(var a in items)a.Mesh.Dispose();items.Clear();world.Dispose();}
}
