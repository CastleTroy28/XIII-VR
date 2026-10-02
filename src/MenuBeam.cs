using System;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
// MeshRenderer and primitive meshes exist in XIII's stripped player. LineRenderer does not.
internal sealed class MenuBeam : IDisposable
{
    private GameObject? beam,dot;
    private Material? material;
    // 0.1.107: the death screen draws its ray on its own layer.
    private int layer=5;
    // 0.1.153: may change while the ray exists.
    internal int Layer{get=>layer;set{layer=value;if(beam!=null)beam.layer=value;if(dot!=null)dot.layer=value;}}
    internal const int RaySortingOrder=32000;
    internal void Show(Vector3 from,Vector3 to,bool hit)
    {
        if(beam==null||dot==null)
        {
            Dispose();
            try
            {
                var shader=Shader.Find("Sprites/Default");if(shader==null)shader=Shader.Find("Unlit/Color");
                if(shader==null)return;
                material=new Material(shader);material.color=new Color(.2f,.85f,1,1);material.renderQueue=4000;
                beam=Make(PrimitiveType.Cube,"XIII menu controller ray");
                dot=Make(PrimitiveType.Sphere,"XIII menu pointer");
            }
            catch{Dispose();throw;}
        }
        var delta=to-from;float length=delta.magnitude;
        if(length<.0001f){Hide();return;}
        beam.transform.SetPositionAndRotation((from+to)*.5f,Quaternion.LookRotation(delta));
        beam.transform.localScale=new Vector3(.002f,.002f,length);
        dot.transform.position=to;dot.transform.localScale=Vector3.one*(hit?.014f:.009f);
        beam.SetActive(true);dot.SetActive(true);
    }
    private GameObject Make(PrimitiveType type,string name)
    {
        var go=GameObject.CreatePrimitive(type);
        try
        {
            go.SetActive(false);go.name=name;go.layer=Layer;UnityEngine.Object.DontDestroyOnLoad(go);
            var collider=go.GetComponent(Il2CppType.Of<Collider>())?.TryCast<Collider>();
            if(collider!=null){collider.enabled=false;UnityEngine.Object.Destroy(collider);}
            var renderer=go.GetComponent(Il2CppType.Of<Renderer>())!.TryCast<Renderer>()!;
            renderer.sharedMaterial=material!;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            // 0.1.200: a world canvas is
            // sorted by its own order (the death panel's 300) before any render
            // queue, so it was drawn over the ray and its dot. The ray is drawn last.
            renderer.sortingOrder=RaySortingOrder;
            return go;
        }
        catch{UnityEngine.Object.Destroy(go);throw;}
    }
    internal void Hide(){if(beam!=null)beam.SetActive(false);if(dot!=null)dot.SetActive(false);}
    public void Dispose()
    {if(beam!=null)UnityEngine.Object.Destroy(beam);if(dot!=null)UnityEngine.Object.Destroy(dot);if(material!=null)UnityEngine.Object.Destroy(material);beam=dot=null;material=null;}
}
