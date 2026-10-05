using System;
using System.Collections.Generic;
using UnityEngine;
namespace XiiiXR;
internal sealed partial class WeaponVisual
{
    private ReloadMesh? cylinder,cylinderEmpty;
    private GameObject? loaderRoot;
    private RigidMeshVisual? loader;
    private bool cylinderOpen;private int loaderRounds=-1;
    private readonly Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<RaycastHit> casingHits=new(16);
    private readonly List<(GameObject root,RigidMeshVisual mesh,Vector3 origin,Vector3 velocity,float time,string profile,ReloadAudio? audio)> casings=new();
    internal bool CylinderReady=>cylinder!=null;
    private ContactSphere[]? cylinderContacts;
    internal ContactSphere[]? ActiveContactShape=>cylinderOpen&&cylinderContacts!=null?cylinderContacts:ContactShape;
    // 0.1.243: the hand-made offsets grow with the revolver drawn bigger (WeaponGeometry.RevolverGrowth).
    private static float Grown(float metres)=>metres*WeaponGeometry.RevolverGrowth;
    internal Vector3 CylinderSocket=>cylinder==null?Vector3.zero:cylinder.Center+new Vector3(Grown(-.025f),0,Grown(-.025f));
    internal bool PrepareCylinder()
    {
        if(cylinder!=null)return true;
        if(reloadProbeDone)return false;reloadProbeDone=true;
        foreach(var part in animatedParts)
        {
            if(part.Snapshot==null)continue;var bones=part.Snapshot.Bones;int joint=-1;
            for(int i=0;i<bones.Length;i++)if(bones[i]!=null)
            {var n=bones[i].name.ToLowerInvariant();if(n.Contains("cylinder")||n.Contains("drum")||n.Contains("barrel_mag")){joint=i;break;}}
            if(joint<0)continue;
            var weights=part.Snapshot.Original.boneWeights;if(weights.Length!=part.Mesh.vertexCount)continue;
            // 0.1.164: decided once per bone (per vertex, with a name read each time, it took ~90 ms).
            var inCylinder=new bool[bones.Length];var round=new bool[bones.Length];
            for(int b=0;b<bones.Length;b++)
            {
                if(bones[b]==null)continue;
                inCylinder[b]=b==joint||bones[b].IsChildOf(bones[joint]);
                var name=bones[b].name.ToLowerInvariant();round[b]=name.Contains("cartridge")||name.Contains("bullet");
            }
            bool Match(int b)=>b>=0&&b<inCylinder.Length&&inCylinder[b];
            var selected=new bool[weights.Length];
            for(int i=0;i<selected.Length;i++){var w=weights[i];selected[i]=(Match(w.boneIndex0)?w.weight0:0)+(Match(w.boneIndex1)?w.weight1:0)+(Match(w.boneIndex2)?w.weight2:0)+(Match(w.boneIndex3)?w.weight3:0)>.5f||(Round(w.boneIndex0)?w.weight0:0)+(Round(w.boneIndex1)?w.weight1:0)+(Round(w.boneIndex2)?w.weight2:0)+(Round(w.boneIndex3)?w.weight3:0)>.25f;}
            cylinder=new ReloadMesh(part.Mesh,part.Source.sharedMaterials,fitMatrix*part.Matrix,selected);
            bool Round(int b)=>b>=0&&b<round.Length&&round[b];
            var empty=(bool[])selected.Clone();
            for(int i=0;i<empty.Length;i++){var w=weights[i];if((Round(w.boneIndex0)?w.weight0:0)+(Round(w.boneIndex1)?w.weight1:0)+(Round(w.boneIndex2)?w.weight2:0)+(Round(w.boneIndex3)?w.weight3:0)>.25f)empty[i]=false;}
            if(ContactShape!=null)
            {
                cylinderContacts=(ContactSphere[])ContactShape.Clone();
                var low=ContactWorld.V(cylinder.Center+cylinder.Min);var high=ContactWorld.V(cylinder.Center+cylinder.Max);
                for(int i=0;i<cylinderContacts.Length;i++)
                {var sphere=cylinderContacts[i];var p=sphere.Offset;
                    if(p.X>=low.X&&p.X<=high.X&&p.Y>=low.Y&&p.Y<=high.Y&&p.Z>=low.Z&&p.Z<=high.Z)
                        cylinderContacts[i]=new ContactSphere(p+new System.Numerics.Vector3(Grown(-.025f),0,0),sphere.Radius);}
            }
            cylinderEmpty=new ReloadMesh(part.Mesh,part.Source.sharedMaterials,fitMatrix*part.Matrix,empty);
            ReloadGripGeometry.Set("revolver",ReloadGripMath.Fit("revolver",new System.Numerics.Vector3(-.023f,-.023f,0),new System.Numerics.Vector3(.023f,.023f,.043f)));
            magazinePart=part;magazineKept=new int[part.Mesh.subMeshCount][];magazineFull=new int[part.Mesh.subMeshCount][];
            for(int sub=0;sub<magazineFull.Length;sub++)
            {
                int[] triangles=part.Mesh.GetTriangles(sub);magazineFull[sub]=triangles;var keep=new List<int>();
                for(int i=0;i+2<triangles.Length;i+=3)if(!(selected[triangles[i]]&&selected[triangles[i+1]]&&selected[triangles[i+2]])){keep.Add(triangles[i]);keep.Add(triangles[i+1]);keep.Add(triangles[i+2]);}
                magazineKept[sub]=keep.ToArray();
            }
            loaderRoot=new GameObject("XIII revolver speedloader");loader=new RigidMeshVisual(loaderRoot.transform,"Six cartridges");loader.Set(CartridgeGeometry.Speedloader());loaderRoot.SetActive(false);
            Bootstrap.Write("REVOLVER cylinder bound="+bones[joint].name+" socket="+CylinderSocket);return true;
        }
        Bootstrap.Warn("REVOLVER cylinder bone unavailable; use automatic reload in VR settings and send WEAPON BONES log");return false;
    }
    internal void PoseCylinder(bool open,bool held,Vector3 hand,Quaternion rotation,int rounds=0,bool empty=false)
    {
        TickCasings();
        if(cylinder==null)return;
        if(held&&rounds!=loaderRounds){loader?.Set(CartridgeGeometry.Speedloader(rounds));loaderRounds=rounds;}
        if(cylinderOpen!=open||magazineHidden!=(open||empty)){cylinderOpen=open;magazineHidden=open||empty;animatedFrame=-1;}
        cylinder.Hide();cylinderEmpty?.Hide();
        var visible=empty?cylinderEmpty:cylinder;
        if((open||empty)&&visible!=null)visible.Pose(FittedToWorld.MultiplyPoint3x4(visible.Center+(open?new Vector3(Grown(-.025f),0,0):Vector3.zero)),FittedToWorld.rotation);
        if(loaderRoot!=null){loaderRoot.SetActive(held);if(held)loaderRoot.transform.SetPositionAndRotation(hand+rotation*new Vector3(0,-.035f,.065f),rotation);}
    }
    internal void EjectCasings(int count,ReloadAudio? audio=null)
    {
        if(cylinder==null)return;
        for(int i=0;i<Math.Clamp(count,0,6);i++)
        {
            var go=new GameObject("XIII falling casing");var mesh=new RigidMeshVisual(go.transform,"Brass");mesh.Set(CartridgeGeometry.Case());
            float a=i*Mathf.PI/3;var p=FittedToWorld.MultiplyPoint3x4(CylinderSocket+new Vector3(Mathf.Cos(a)*Grown(.015f),Mathf.Sin(a)*Grown(.015f),0));
            go.transform.SetPositionAndRotation(p,FittedToWorld.rotation);casings.Add((go,mesh,p,FittedToWorld.MultiplyVector(Vector3.back)*.12f,Time.realtimeSinceStartup,"revolver",i==0?audio:null));
        }
    }
    internal void EjectShell(ReloadAudio? audio)
    {
        var go=new GameObject("XIII spent shotgun casing");var mesh=new RigidMeshVisual(go.transform,"Spent shell");mesh.Set(CartridgeGeometry.Case());
        var p=FittedToWorld.MultiplyPoint3x4(ReloadBolt+new Vector3(.035f,0,.025f));
        go.transform.SetPositionAndRotation(p,FittedToWorld.rotation);
        casings.Add((go,mesh,p,FittedToWorld.MultiplyVector(Vector3.right)*.55f,Time.realtimeSinceStartup,"shotgun",audio));
    }
    private void TickCasings()
    {
        for(int i=casings.Count-1;i>=0;i--)
        {
            var c=casings[i];float t=Time.realtimeSinceStartup-c.time;
            var next=c.origin+c.velocity*t+Vector3.down*(4.905f*t*t);
            var delta=next-c.root.transform.position;
            RaycastHit impact=default;bool hit=false;float nearest=delta.magnitude;
            if(delta.sqrMagnitude>1e-8f)
            {
                int count=Physics.RaycastNonAlloc(c.root.transform.position,delta.normalized,casingHits,nearest,~0,QueryTriggerInteraction.Ignore);
                var player=CameraRig.Current?.PlayerRoot;
                for(int j=0;j<count;j++)
                {var candidate=casingHits[j];if(candidate.collider==null||player!=null&&candidate.collider.transform.IsChildOf(player)||candidate.distance>nearest)continue;
                    impact=candidate;nearest=candidate.distance;hit=true;}
            }
            if(hit||t>2)
            {
                if(hit&&c.audio!=null)
                {
                    // Material family uses the native hit collider, not a random revolver surface.
                    string name=(impact.collider?.sharedMaterial?.name??impact.collider?.name??"").ToLowerInvariant();
                    c.audio.PlayCue(c.profile,c.profile=="shotgun"?7+UnityEngine.Random.Range(0,4):name.Contains("metal")?8:7);
                }
                c.mesh.Dispose();UnityEngine.Object.Destroy(c.root);casings.RemoveAt(i);
            }
            else c.root.transform.position=next;
        }
    }
    private void DisposeCylinder(){foreach(var c in casings){c.mesh.Dispose();UnityEngine.Object.Destroy(c.root);}casings.Clear();cylinder?.Dispose();cylinder=null;cylinderEmpty?.Dispose();cylinderEmpty=null;loader?.Dispose();loader=null;if(loaderRoot!=null)UnityEngine.Object.Destroy(loaderRoot);loaderRoot=null;}
}
