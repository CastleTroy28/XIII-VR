using System;
using UnityEngine;
using N=System.Numerics.Vector3;
namespace XiiiXR;
// One renderer for the whole arc, with no colliders and no per-segment objects.
internal sealed class TeleportArc : IDisposable
{
    private GameObject? root;
    private RigidMeshVisual? visual;
    internal void Show(Vector3[] points,int segments,bool valid)
    {
        if(root==null)
        {
            try{root=new GameObject("XIII teleport arc");root.layer=5;visual=new RigidMeshVisual(root.transform,"XIII teleport arc mesh",true);}
            catch{Dispose();throw;}
        }
        var mesh=new HandMeshGeometry(true);
        var color=valid?new System.Numerics.Vector4(.15f,1,.35f,1):new System.Numerics.Vector4(1,.18f,.06f,1);
        if(segments>0)
        {
            var p=points[segments];var c=new N(p.x,p.y+.02f,p.z);
            for(int i=0;i<24;i++)
            {
                var a=new N(MathF.Cos(i*MathF.PI/12),0,MathF.Sin(i*MathF.PI/12));
                var b=new N(MathF.Cos((i+1)*MathF.PI/12),0,MathF.Sin((i+1)*MathF.PI/12));
                mesh.Quad(c+a*.10f,c+b*.10f,c+b*.12f,c+a*.12f,color);
                mesh.Quad(c+a*.10f,c+b*.10f,c+b*.12f,c+a*.12f,color,true);
            }
        }
        visual!.Set(mesh);visual.Show(true);
    }
    internal void Hide()=>visual?.Show(false);
    public void Dispose(){visual?.Dispose();visual=null;if(root!=null)UnityEngine.Object.Destroy(root);root=null;}
}
