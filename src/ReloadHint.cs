using System;
using UnityEngine;
using N=System.Numerics;
namespace XiiiXR;
// A small depth-tested diamond only while B is held. No collision target.
internal sealed class ReloadHint:IDisposable
{
    private readonly GameObject root;
    private readonly RigidMeshVisual mark;
    internal ReloadHint(Transform parent)
    {
        root=new GameObject("XIII magazine grab hint");root.transform.SetParent(parent,false);
        mark=new RigidMeshVisual(root.transform,"Magazine grab dot",true);
        var m=new HandMeshGeometry(true);var color=new N.Vector4(1,.72f,.22f,1);
        for(int i=0;i<4;i++)
        {
            float a=i*MathF.PI/2,b=(i+1)*MathF.PI/2;
            var p=new N.Vector3(MathF.Cos(a)*.008f,0,MathF.Sin(a)*.008f);
            var q=new N.Vector3(MathF.Cos(b)*.008f,0,MathF.Sin(b)*.008f);
            m.Tri(new N.Vector3(0,.01f,0),q,p,color);m.Tri(new N.Vector3(0,-.01f,0),p,q,color);
        }
        mark.Set(m);root.SetActive(false);
    }
    internal void Show(bool show,Vector3 magazine,bool exact=false)
    {root.transform.localPosition=magazine+(exact?Vector3.zero:Vector3.left*.025f);root.SetActive(show);}
    public void Dispose(){mark.Dispose();UnityEngine.Object.Destroy(root);}
}
