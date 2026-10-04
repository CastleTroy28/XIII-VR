using System;
using UnityEngine;
using N=System.Numerics.Vector3;
namespace XiiiXR;
// 0.1.214: the mark where a throw lands - a glowing ring with a dot, lying on
// what it hits. Its mesh is built once; each frame only moves it.
internal sealed class ThrowMarker : IDisposable
{
    private readonly string name;
    private GameObject? root;
    private RigidMeshVisual? visual;
    private bool shown;
    // 0.1.215: dot: a filled dot alone (the bazooka's aim point), drawn over
    // what is in front of it.
    private readonly bool dot;
    internal ThrowMarker(string name,bool dot=false){this.name=name;this.dot=dot;}
    internal bool Shown=>shown;
    internal void Show(Vector3 point,Vector3 normal,float radius)
    {
        if(root==null||visual==null)
        {
            // New, or gone with the scene.
            visual?.Dispose();visual=null;
            root=new GameObject(name);
            visual=new RigidMeshVisual(root.transform,name+" ring",true,dot);
            visual.Set(dot?BuildDot():Build());visual.Show(true);shown=true;
        }
        var up=normal.sqrMagnitude>1e-6f&&float.IsFinite(normal.sqrMagnitude)?normal.normalized:Vector3.up;
        root.transform.SetPositionAndRotation(point+up*.012f,Quaternion.FromToRotation(Vector3.up,up));
        root.transform.localScale=new Vector3(radius,radius,radius);
        if(!shown){root.SetActive(true);shown=true;}
    }
    internal void Hide()
    {
        if(!shown)return;shown=false;
        if(root!=null)root.SetActive(false);
    }
    // A ring of radius 1 (inner .78) and a dot (.2) in the XZ plane, both faces.
    private static HandMeshGeometry Build()
    {
        var mesh=new HandMeshGeometry(true);
        var ring=new System.Numerics.Vector4(1f,.78f,.22f,1);var dot=new System.Numerics.Vector4(1f,.93f,.62f,1);
        const int n=32;
        static N P(float r,float a)=>new(r*MathF.Cos(a),0,r*MathF.Sin(a));
        for(int i=0;i<n;i++)
        {
            float a0=i*2*MathF.PI/n,a1=(i+1)*2*MathF.PI/n;
            mesh.Quad(P(.78f,a0),P(1f,a0),P(1f,a1),P(.78f,a1),ring);
            mesh.Quad(P(.78f,a0),P(1f,a0),P(1f,a1),P(.78f,a1),ring,true);
            mesh.Tri(N.Zero,P(.2f,a1),P(.2f,a0),dot);
            mesh.Tri(N.Zero,P(.2f,a0),P(.2f,a1),dot);
        }
        return mesh;
    }
    // A red dot with a dark rim, radius 1.
    private static HandMeshGeometry BuildDot()
    {
        var mesh=new HandMeshGeometry(true);
        var red=new System.Numerics.Vector4(1f,.16f,.1f,1);var rim=new System.Numerics.Vector4(.25f,0,0,1);
        const int n=24;
        static N P(float r,float a)=>new(r*MathF.Cos(a),0,r*MathF.Sin(a));
        for(int i=0;i<n;i++)
        {
            float a0=i*2*MathF.PI/n,a1=(i+1)*2*MathF.PI/n;
            mesh.Quad(P(.72f,a0),P(1f,a0),P(1f,a1),P(.72f,a1),rim);mesh.Quad(P(.72f,a0),P(1f,a0),P(1f,a1),P(.72f,a1),rim,true);
            mesh.Tri(N.Zero,P(.72f,a1),P(.72f,a0),red);mesh.Tri(N.Zero,P(.72f,a0),P(.72f,a1),red);
        }
        return mesh;
    }
    public void Dispose()
    {
        visual?.Dispose();visual=null;
        if(root!=null)UnityEngine.Object.Destroy(root);
        root=null;shown=false;
    }
}
