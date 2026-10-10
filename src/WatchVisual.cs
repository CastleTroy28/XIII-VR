using System;
using System.Linq;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
namespace XiiiXR;
// 0.1.254: the 3D digital wristwatch on a VR hand (the classic XIII mod's model and face): the watch's mesh
// (WatchModelMath: its strap round the fitted wrist, one material a tone, lit as the game's things) and its
// face, a picture (WatchFacePixels) on a quad over the case's screen, lit and glowing a little so it reads
// in the dark too. Both children of the hand's wearable root: they move with the hand, never as UI.
internal sealed class WatchVisual : IDisposable
{
    private GameObject? root,faceRoot;
    private Mesh? mesh,faceMesh;
    private MeshRenderer? renderer,faceRenderer;
    private Material[]? materials;
    private Material? faceMaterial;
    private Texture2D? faceTexture;
    private Color32[]? facePixels;
    private bool fitted;private float fitX,fitY;private bool fitRight;
    internal WatchVisual(Transform parent,string name)
    {
        try
        {
            root=new GameObject(name);root.layer=parent.gameObject.layer;root.transform.SetParent(parent,false);
            root.transform.localPosition=Vector3.zero;root.transform.localRotation=Quaternion.identity;root.transform.localScale=Vector3.one;
            mesh=new Mesh(){hideFlags=HideFlags.DontUnloadUnusedAsset};mesh.name=name;mesh.indexFormat=IndexFormat.UInt32;
            root.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>()!.sharedMesh=mesh;
            renderer=root.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>()!;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            faceRoot=new GameObject(name+" face");faceRoot.layer=root.layer;faceRoot.transform.SetParent(root.transform,false);
            faceRoot.transform.localPosition=Vector3.zero;faceRoot.transform.localRotation=Quaternion.identity;faceRoot.transform.localScale=Vector3.one;
            faceMesh=new Mesh(){hideFlags=HideFlags.DontUnloadUnusedAsset};faceMesh.name=name+" face";
            faceRoot.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>()!.sharedMesh=faceMesh;
            faceRenderer=faceRoot.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>()!;
            faceRenderer.shadowCastingMode=ShadowCastingMode.Off;faceRenderer.receiveShadows=false;
            faceTexture=new Texture2D(WatchFacePixels.Width,WatchFacePixels.Height,TextureFormat.RGBA32,true);
            faceTexture.name=name+" face";faceTexture.wrapMode=TextureWrapMode.Clamp;faceTexture.filterMode=FilterMode.Trilinear;faceTexture.anisoLevel=4;
            faceTexture.hideFlags=HideFlags.DontUnloadUnusedAsset;
            faceMaterial=new Material(Opaque()){hideFlags=HideFlags.DontUnloadUnusedAsset};
            faceMaterial.color=Color.white;faceMaterial.mainTexture=faceTexture;
            if(faceMaterial.HasProperty("_Glossiness"))faceMaterial.SetFloat("_Glossiness",.05f);
            if(faceMaterial.HasProperty("_SpecularHighlights")){faceMaterial.SetFloat("_SpecularHighlights",0);faceMaterial.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");}
            if(faceMaterial.HasProperty("_GlossyReflections")){faceMaterial.SetFloat("_GlossyReflections",0);faceMaterial.EnableKeyword("_GLOSSYREFLECTIONS_OFF");}
            if(faceMaterial.HasProperty("_EmissionColor"))
            {
                faceMaterial.EnableKeyword("_EMISSION");faceMaterial.SetColor("_EmissionColor",new Color(.5f,.5f,.5f,1));
                if(faceMaterial.HasProperty("_EmissionMap"))faceMaterial.SetTexture("_EmissionMap",faceTexture);
            }
            faceRenderer.sharedMaterial=faceMaterial;
        }
        catch{Dispose();throw;}
    }
    private static Shader Opaque()=>UnityEngine.Shader.Find("Standard")??UnityEngine.Shader.Find("Legacy Shaders/Diffuse")??throw new InvalidOperationException("No opaque material for the watch");
    // The watch on this wrist's fitted section (built again only when it changes).
    internal void Fit(bool rightWrist,float radiusX,float radiusY)
    {
        if(mesh==null||renderer==null||faceMesh==null)return;
        if(fitted&&fitRight==rightWrist&&fitX==radiusX&&fitY==radiusY)return;
        var shape=WatchModelMath.Build(rightWrist,radiusX,radiusY);
        var vertices=new Vector3[shape.Points.Length];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];
        for(int i=0;i<vertices.Length;i++){var p=shape.Points[i];var n=shape.Normals[i];vertices[i]=new Vector3(p.X,p.Y,p.Z);normals[i]=new Vector3(n.X,n.Y,n.Z);uv[i]=new Vector2(.5f,.5f);}
        mesh.Clear();mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.subMeshCount=shape.Tones.Length;
        if(materials==null||materials.Length!=shape.Palette.Length)
        {
            if(materials!=null)foreach(var m in materials)if(m!=null)UnityEngine.Object.Destroy(m);
            materials=new Material[shape.Palette.Length];
        }
        for(int t=0;t<shape.Tones.Length;t++)
        {
            var c=shape.Palette[t];
            if(materials[t]==null)
            {
                var m=new Material(Opaque()){hideFlags=HideFlags.DontUnloadUnusedAsset};m.mainTexture=Texture2D.whiteTexture;
                if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",.3f);
                materials[t]=m;
            }
            materials[t].color=new Color(c.X,c.Y,c.Z,1);
            mesh.SetTriangles(shape.Tones[t],t,false,0);
        }
        mesh.RecalculateBounds();renderer.sharedMaterials=materials;
        var face=new Vector3[4];var faceUv=new Vector2[4];var faceNormals=new Vector3[4];
        for(int i=0;i<4;i++){var p=shape.Face[i];face[i]=new Vector3(p.X,p.Y,p.Z);faceUv[i]=new Vector2(shape.FaceUv[i].X,shape.FaceUv[i].Y);faceNormals[i]=new Vector3(shape.FaceNormal.X,shape.FaceNormal.Y,shape.FaceNormal.Z);}
        faceMesh.Clear();faceMesh.vertices=face;faceMesh.uv=faceUv;faceMesh.normals=faceNormals;faceMesh.triangles=WatchModelMath.FaceTriangles;faceMesh.RecalculateBounds();
        if(!fitted||fitRight!=rightWrist)
            Bootstrap.Write("WATCH 3D on the "+(rightWrist?"right":"left")+" wrist: "+shape.Points.Length+" points, "+shape.Tones.Sum(t=>t.Length)/3+" triangles in "+shape.Palette.Length+" tones; strap radii "+radiusX.ToString("F4")+", "+radiusY.ToString("F4")+" m");
        fitted=true;fitRight=rightWrist;fitX=radiusX;fitY=radiusY;
    }
    // The face's picture (WatchFacePixels: RGBA, rows top to bottom).
    internal void SetFace(byte[] topDown)
    {
        if(faceTexture==null)return;
        var bottomUp=WatchFacePixels.BottomUp(topDown);
        facePixels??=new Color32[WatchFacePixels.Width*WatchFacePixels.Height];
        for(int i=0;i<facePixels.Length;i++)facePixels[i]=new Color32(bottomUp[i*4],bottomUp[i*4+1],bottomUp[i*4+2],255);
        faceTexture.SetPixels32(facePixels);faceTexture.Apply(true,false);
    }
    internal void ShowFace(bool visible){if(faceRoot!=null&&faceRoot.activeSelf!=visible)faceRoot.SetActive(visible);}
    public void Dispose()
    {
        if(root!=null)UnityEngine.Object.Destroy(root);root=null;faceRoot=null;
        if(mesh!=null)UnityEngine.Object.Destroy(mesh);mesh=null;
        if(faceMesh!=null)UnityEngine.Object.Destroy(faceMesh);faceMesh=null;
        if(materials!=null)foreach(var m in materials)if(m!=null)UnityEngine.Object.Destroy(m);materials=null;
        if(faceMaterial!=null)UnityEngine.Object.Destroy(faceMaterial);faceMaterial=null;
        if(faceTexture!=null)UnityEngine.Object.Destroy(faceTexture);faceTexture=null;
        renderer=faceRenderer=null;
    }
}
