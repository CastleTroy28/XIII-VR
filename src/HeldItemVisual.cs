using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using PlayMagic.Weapons;
using UnityEngine;
using UnityEngine.Rendering;
namespace XiiiXR;
// Borrow the real item mesh/materials from this installation. The preview
// contains only renderers; selecting it cannot heal, lock input or equip an item.
internal sealed class HeldItemVisual : IDisposable
{
    private GameObject? root;
    private string pinchProfile="";private Vector3 pinchEdge,pinchOffset;private Quaternion pinchRotation=Quaternion.identity;
    private float pinchThickness;
    // 0.1.109: lockpick handle point (item space) under the thumb.
    private Vector3 screwHandle;private bool screwReported;
    internal bool ScrewdriverGripped=>pinchProfile==ScrewdriverGrip.Profile;
    // 0.1.150: a pinched item (key, card, lockpick) fitted in the holding hand's own frame (PreparePinch with that hand).
    internal bool Pinched=>pinchProfile.Length>0;
    // In the left hand: a pinch is already that hand's own; anything else is the right-hand fit mirrored.
    internal void PoseIn(Transform? hand,bool left)=>Pose(hand,left&&!Pinched);
    internal void PreparePinch(NativeHandVisual hand)
    {
        if(pinchProfile.Length==0)return;
        if(ScrewdriverGripped)
        {
            // Pick along the hand (+Z, where the controller points); the
            // handle axis under the thumb, on the side of the curled index.
            var axis=hand.ScrewdriverContact(pinchThickness);
            pinchRotation=Quaternion.identity;pinchOffset=axis-screwHandle;
            if(!screwReported){screwReported=true;Bootstrap.Write("LOCKPICK screwdriver grip contact="+axis.ToString("F4")+" "+hand.ScrewdriverReport);}
            return;
        }
        var contact=hand.PropRimContact(pinchProfile,pinchThickness);
        pinchRotation=Quaternion.Inverse(hand.PropRimHandRotation);
        pinchOffset=contact-pinchRotation*pinchEdge;
    }
    private readonly List<Mesh> meshes=new();
    internal static HeldItemVisual Create(Equipable source)
    {
        var v=new HeldItemVisual();try{v.Build(source);return v;}catch{v.Dispose();throw;}
    }
    internal static HeldItemVisual CreateFromRenderers(Equipable source,List<Renderer> renderers)
    {
        var v=new HeldItemVisual();try{v.Build(source,renderers);return v;}catch{v.Dispose();throw;}
    }
    private void Build(Equipable source,List<Renderer>? supplied=null)
    {
        root=new GameObject("XIII held item preview");root.SetActive(false);
        var parts=new List<(Mesh mesh,Renderer source)>();
        bool found=false;Bounds bounds=default;
        Renderer? body=null;int mostVertices=0;
        var sources=supplied??HeldItemMeshSources.Find(source);
        foreach(var r in sources)
        {
            Mesh mesh;var skin=r.TryCast<SkinnedMeshRenderer>();
            if(skin!=null)
            {
                mesh=new Mesh();meshes.Add(mesh);
                using(var snapshot=new NativeSkinSnapshot(skin))snapshot.Bake(mesh,true);
            }
            else
            {
                var original=r.GetComponent(Il2CppType.Of<MeshFilter>())?.TryCast<MeshFilter>()?.sharedMesh;
                if(original==null)continue;
                mesh=UnityEngine.Object.Instantiate(original).TryCast<Mesh>()!;meshes.Add(mesh);
            }
            if(mesh.vertexCount==0||mesh.vertexCount>200000)continue;
            // Detached geometry has no transform relationship with the logical
            // inventory object (which can be parked at zero scale). Keep it in
            // renderer space until the body frame is known.
            int verticesCount=mesh.vertexCount;if(verticesCount>mostVertices){mostVertices=verticesCount;body=r;}
            var matrix=supplied!=null?Matrix4x4.identity:source.transform.worldToLocalMatrix*r.localToWorldMatrix;
            var vertices=mesh.vertices;
            for(int i=0;i<vertices.Length;i++)
            {
                var p=matrix.MultiplyPoint3x4(vertices[i]);
                if(!float.IsFinite(p.x)||!float.IsFinite(p.y)||!float.IsFinite(p.z))throw new InvalidOperationException("Invalid item vertices: "+source.name);
                vertices[i]=p;if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);
            }
            mesh.vertices=vertices;mesh.RecalculateBounds();mesh.RecalculateNormals();parts.Add((mesh,r));
        }
        if(!found||parts.Count==0)throw new InvalidOperationException("No usable item mesh: "+source.name+" id="+source.identifier+" slot="+source.slot);
        if(body!=null)
        {
            found=false;
            foreach(var part in parts)
            {
                var toBody=supplied==null?body.worldToLocalMatrix*source.transform.localToWorldMatrix:
                    part.source==body?Matrix4x4.identity:body.worldToLocalMatrix*part.source.localToWorldMatrix;
                var vertices=part.mesh.vertices;for(int i=0;i<vertices.Length;i++)
                {var p=toBody.MultiplyPoint3x4(vertices[i]);vertices[i]=p;if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);}
                part.mesh.vertices=vertices;}
        }
        // 0.1.104: lockpick without its tension wrench.
        if(source.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Lockpick)
        {
            var input=new List<(System.Numerics.Vector3[] vertices,int[] triangles)>();
            foreach(var part in parts){var vs=part.mesh.vertices;var n=new System.Numerics.Vector3[vs.Length];for(int i=0;i<vs.Length;i++)n[i]=new System.Numerics.Vector3(vs[i].x,vs[i].y,vs[i].z);input.Add((n,part.mesh.triangles));}
            var keep=LockpickShape.Filter(input,out string report);
            for(int p=0;p<parts.Count;p++)
            {
                var tris=keep[p];if(tris==null)continue;
                // Compact: unused wrench vertices must not stretch the fit.
                var old=parts[p].mesh.vertices;var oldUv=parts[p].mesh.uv;var map=new Dictionary<int,int>();var nv=new List<Vector3>();var nuv=new List<Vector2>();
                var nt=new int[tris.Length];
                for(int i=0;i<tris.Length;i++){if(!map.TryGetValue(tris[i],out int m)){m=nv.Count;map[tris[i]]=m;nv.Add(old[tris[i]]);if(oldUv.Length==old.Length)nuv.Add(oldUv[tris[i]]);}nt[i]=m;}
                var mesh=parts[p].mesh;mesh.triangles=Array.Empty<int>();mesh.subMeshCount=1;
                mesh.vertices=nv.ToArray();if(nuv.Count==nv.Count)mesh.uv=nuv.ToArray();mesh.triangles=nt;
            }
            parts.RemoveAll(x=>x.mesh.vertexCount==0||x.mesh.triangles.Length==0);
            if(parts.Count==0)throw new InvalidOperationException("Lockpick filter removed everything");
            found=false;foreach(var part in parts)foreach(var v in part.mesh.vertices){if(!found){bounds=new Bounds(v,Vector3.zero);found=true;}else bounds.Encapsulate(v);}
            Bootstrap.Write("LOCKPICK shape: "+report);
        }
        bool large=source.slot==PlayerEquipableInventory.ActiveEquipmentSlot.MedkitL;
        bool medkit=large||source.slot==PlayerEquipableInventory.ActiveEquipmentSlot.MedkitS;
        var center=bounds.center;var extent=bounds.size;
        var fit=medkit?NativeHandVisual.U(MedkitGeometry.Fit(new System.Numerics.Vector3(center.x,center.y,center.z),
            new System.Numerics.Vector3(extent.x,extent.y,extent.z),large)):Matrix4x4.identity;
        if(!medkit)
        {
            float longest=Math.Max(extent.x,Math.Max(extent.y,extent.z));
            float size=source.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Card?.09f:source.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Key?.10f:.20f;
            float scale=size/Math.Max(.001f,longest);
            // 0.1.84: gadgets sit below the palm, clear of the curled fingers
            // (the grappling hook went through the hand).
            bool gadget=(int)source.slot==16||(int)source.slot==14||(int)source.slot==15||(int)source.slot==33;
            var place=gadget?new Vector3(0,-.022f-Math.Min(extent.y*scale*.5f,.03f)-.012f,.075f):new Vector3(0,-.022f,.085f);
            fit=Matrix4x4.TRS(place-center*scale,Quaternion.identity,Vector3.one*scale);
            if(gadget)Bootstrap.Write("HELD ITEM gadget bounds="+extent.ToString("F4")+" scale="+scale.ToString("F3")+" place="+place.ToString("F3"));
        }
        if(source.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Key||source.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Card
            ||source.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Lockpick)
        {
            var points=new List<System.Numerics.Vector3>();var indices=new List<int>();
            foreach(var part in parts)
            {
                int start=points.Count;foreach(var v in part.mesh.vertices)points.Add(new System.Numerics.Vector3(v.x,v.y,v.z));
                foreach(int i in part.mesh.triangles)indices.Add(start+i);
            }
            bool isCard=source.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Card;
            bool isPick=source.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Lockpick;
            var grip=KeyGripGeometry.Fit(points.ToArray(),indices.ToArray(),isCard,isPick);
            fit=NativeHandVisual.U(grip.fit);pinchEdge=new Vector3(grip.edge.X,grip.edge.Y,grip.edge.Z);pinchThickness=grip.thickness;pinchProfile=isCard?"card":isPick?ScrewdriverGrip.Profile:"key";
            // The thumb holds the handle near its front end, on its axis.
            if(isPick)
            {
                var fitted=points.Select(p=>System.Numerics.Vector3.Transform(p,grip.fit)).ToArray();
                var (start,end)=KeyGripGeometry.PickHandle(fitted,indices.ToArray(),.15f);
                float z=ScrewdriverGrip.HoldPoint(start,end);if(!float.IsFinite(z))z=grip.edge.Z+AshtrayRimGeometry.Purchase;
                screwHandle=new Vector3(grip.edge.X,grip.edge.Y,z);
            }
            Bootstrap.Write(isPick?"LOCKPICK screwdriver grip handle="+screwHandle.ToString("F4")+" thickness="+pinchThickness.ToString("F4"):
                "KEY PINCH "+(isCard?"card":"key")+" surface="+pinchEdge.ToString("F6")+" thickness="+pinchThickness.ToString("F6"));
        }
        BodyName=body?.name??"";Fit=fit;
        bool zipline=(int)source.slot==16;var allPoints=new List<System.Numerics.Vector3>();var allTriangles=new List<int>();var groups=new List<int[]>();
        foreach(var part in parts)
        {
            var go=new GameObject("XIII held item mesh");go.transform.SetParent(root.transform,false);
            var vertices=part.mesh.vertices;
            for(int i=0;i<vertices.Length;i++)
            {
                vertices[i]=fit.MultiplyPoint3x4(vertices[i]);
                if(!boxed){fittedBox=new Bounds(vertices[i],Vector3.zero);boxed=true;}else fittedBox.Encapsulate(vertices[i]);
            }
            if(zipline)
            {
                int start=allPoints.Count;foreach(var v in vertices)allPoints.Add(new System.Numerics.Vector3(v.x,v.y,v.z));foreach(int t in part.mesh.triangles)allTriangles.Add(start+t);
                // 0.1.198: each material's triangles too (a rubber grip of its own).
                try{for(int sub=0;sub<part.mesh.subMeshCount;sub++){int[] g=part.mesh.GetTriangles(sub);for(int k=0;k<g.Length;k++)g[k]+=start;groups.Add(g);}}catch(Exception){}
            }
            part.mesh.vertices=vertices;part.mesh.RecalculateBounds();part.mesh.RecalculateNormals();
            go.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>()!.sharedMesh=part.mesh;
            var renderer=go.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>()!;
            renderer.sharedMaterials=part.source.sharedMaterials;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        Bootstrap.Write("HELD ITEM native mesh="+source.name+" parts="+parts.Count+" rigid neutral mesh; fitted item grip; no gameplay components cloned");
        // 0.1.198: the zipline hook's handle (its rubber grip), held as a pistol's grip.
        if(zipline)
        {
            try
            {
                var points=allPoints.ToArray();var triangles=allTriangles.ToArray();
                GripBar=GripBarMath.Find(points,triangles,groups);
                float k=TrueScale;
                Bootstrap.Write((GripBar is GripBarMath.Bar bar?"HELD ITEM zipline hook handle found: "+(bar.Length*k).ToString("F3")+" m long, "+(bar.Thickness*k).ToString("F3")+" m thick at "+bar.Center.ToString()+" along "+bar.Axis.ToString()+" (fitted frame); held as a pistol's grip, the hook forward, at "+k.ToString("F2")+" x its fitted size"
                    :"HELD ITEM zipline hook handle not found: held as the game holds it (or fitted)")+"; "+groups.Count+" materials, "+GripBarMath.Report(points,triangles,groups));
                DumpMesh("zipline",source.name,points,triangles);
            }
            catch(Exception ex){GripBar=null;Bootstrap.Warn("HELD ITEM zipline handle: "+ex.Message);}
        }
    }
    // 0.1.198: the zipline hook's handle in the fitted item (its root) frame.
    internal GripBarMath.Bar? GripBar {get;private set;}
    // The fitted item drawn at its own size (gadgets are fitted to 20 cm), at most 40 cm long.
    internal float TrueScale
    {
        get
        {
            float fitted=new Vector3(Fit.m00,Fit.m10,Fit.m20).magnitude;if(!(fitted>1e-4f)||!float.IsFinite(fitted))return 1;
            return GripBarMath.TrueScale(fitted);
        }
    }
    // 0.1.203: points on the drawn item in the world - the corners, the
    // middles of the faces and the middle of its box in its own (fitted)
    // frame (a card held to a reader). 0 while it is not drawn.
    private Bounds fittedBox;private bool boxed;
    internal const int ProbePoints=15;
    internal int WorldProbe(Vector3[] into)
    {
        if(root==null||!root.activeInHierarchy||!boxed||into.Length<ProbePoints)return 0;
        var t=root.transform;var c=fittedBox.center;var e=fittedBox.extents;int n=0;
        for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)into[n++]=t.TransformPoint(c+new Vector3(x*e.x,y*e.y,z*e.z));
        into[n++]=t.TransformPoint(c);
        into[n++]=t.TransformPoint(c+new Vector3(e.x,0,0));into[n++]=t.TransformPoint(c-new Vector3(e.x,0,0));
        into[n++]=t.TransformPoint(c+new Vector3(0,e.y,0));into[n++]=t.TransformPoint(c-new Vector3(0,e.y,0));
        into[n++]=t.TransformPoint(c+new Vector3(0,0,e.z));into[n++]=t.TransformPoint(c-new Vector3(0,0,e.z));
        return n;
    }
    internal void PoseRoot(Vector3 position,Quaternion rotation,float scale=1)
    {if(root==null)return;root.transform.SetPositionAndRotation(position,rotation);root.transform.localScale=Vector3.one*scale;root.SetActive(true);}
    private static readonly HashSet<string> dumped=new();
    private static void DumpMesh(string kind,string name,System.Numerics.Vector3[] points,int[] triangles)
    {
        if(!dumped.Add(kind))return;
        try
        {
            var inv=System.Globalization.CultureInfo.InvariantCulture;var sb=new System.Text.StringBuilder();
            sb.Append("# XIII VR mesh ").Append(name).Append(" held item frame; v x y z; t a b c\n");
            foreach(var p in points)sb.Append("v ").Append(p.X.ToString("F5",inv)).Append(' ').Append(p.Y.ToString("F5",inv)).Append(' ').Append(p.Z.ToString("F5",inv)).Append('\n');
            for(int t=0;t+2<triangles.Length;t+=3)sb.Append("t ").Append(triangles[t]).Append(' ').Append(triangles[t+1]).Append(' ').Append(triangles[t+2]).Append('\n');
            var file=System.IO.Path.Combine(BepInEx.Paths.ConfigPath,"XIII-XR-mesh-"+kind+".txt");System.IO.File.WriteAllText(file,sb.ToString());
            Bootstrap.Write("WEAPON MESH written: "+file);
        }
        catch(Exception ex){Bootstrap.Warn("WEAPON MESH dump: "+ex.Message);}
    }
    internal void Pose(Vector3 position,Quaternion rotation)
    {if(root==null)return;root.transform.SetPositionAndRotation(position,rotation);root.SetActive(true);}
    internal void Pose(Transform? hand,bool left=false)
    {
        if(root==null)return;if(hand==null||!hand.gameObject.activeInHierarchy){Hide();return;}
        // Use the collision-constrained hand root, not a second raw controller pose.
        // Left hand: the right-hand fit mirrored across the hand's X (Unity
        // flips the winding of negatively scaled renderers).
        var offset=left?new Vector3(-pinchOffset.x,pinchOffset.y,pinchOffset.z):pinchOffset;
        var turn=left?new Quaternion(pinchRotation.x,-pinchRotation.y,-pinchRotation.z,pinchRotation.w):pinchRotation;
        root.transform.SetPositionAndRotation(hand.position+hand.rotation*offset,hand.rotation*turn);
        root.transform.localScale=left?new Vector3(-1,1,1):Vector3.one;
        root.SetActive(true);
    }
    // 0.1.186: the fitted item as a tiny model (a medkit on a forearm's cut):
    // its fitted middle `fitted` placed at `center`, scaled down.
    internal void PoseSmall(Vector3 center,Quaternion rotation,Vector3 fitted,float scale)
    {
        if(root==null)return;
        root.transform.SetPositionAndRotation(center-rotation*(fitted*scale),rotation);
        root.transform.localScale=Vector3.one*scale;root.SetActive(true);
    }
    // Renderer whose space the mesh was built in, and the fit applied to it.
    internal string BodyName {get;private set;}="";
    internal Matrix4x4 Fit {get;private set;}=Matrix4x4.identity;
    // Place the fitted mesh so its body space lands on `bodyWorld` (the same
    // renderer of the game's own copy of the item).
    // 0.1.197: the same from a full matrix that may mirror (the game's hook on
    // its mirrored left wrist, or a hold mirrored into the other hand).
    internal void PoseMatrix(Matrix4x4 bodyWorld)
    {
        if(root==null)return;var m=bodyWorld*Fit.inverse;
        var d=ItemPlacement.Decompose(new System.Numerics.Vector3(m.m00,m.m10,m.m20),new System.Numerics.Vector3(m.m01,m.m11,m.m21),new System.Numerics.Vector3(m.m02,m.m12,m.m22));
        if(d==null){Hide();return;}
        var q=d.Value.rotation;var s=d.Value.scale;
        root.transform.SetPositionAndRotation(m.GetColumn(3),new Quaternion(q.X,q.Y,q.Z,q.W));
        root.transform.localScale=new Vector3(s.X,s.Y,s.Z);root.SetActive(true);
    }
    internal void PoseBody(Matrix4x4 bodyWorld)
    {
        if(root==null)return;var m=bodyWorld*Fit.inverse;
        root.transform.SetPositionAndRotation(m.GetColumn(3),m.rotation);
        root.transform.localScale=m.lossyScale;root.SetActive(true);
    }
    internal void Hide(){if(root!=null)root.SetActive(false);}
    public void Dispose(){if(root!=null)UnityEngine.Object.Destroy(root);root=null;foreach(var m in meshes)if(m!=null)UnityEngine.Object.Destroy(m);meshes.Clear();}
}
