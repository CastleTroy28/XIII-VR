using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
namespace XiiiXR;
// 0.1.97: SVD optical sight as a picture-in-picture lens. A narrow camera on
// the scope axis renders into a texture shown on the eyepiece, so the
// magnified view is visible whenever the lens can be seen — not only with the
// eye pressed to the (unmagnified) tube.
internal sealed partial class WeaponVisual
{
    // 0.1.103: R3 cycles the zoom. Vertical view angle of the lens picture.
    private static readonly float[] ScopeZoomFov={9f,5f,2.8f};   // ~4x, ~8x, ~14x
    private static int scopeZoom=1;
    private static float ScopeFov=>ScopeZoomFov[scopeZoom];
    internal void CycleScopeZoom()
    {
        scopeZoom=(scopeZoom+1)%ScopeZoomFov.Length;
        if(scopeCamera!=null)scopeCamera.fieldOfView=ScopeFov;
        Bootstrap.Write("SCOPE zoom level="+(scopeZoom+1)+"/"+ScopeZoomFov.Length+" fov="+ScopeFov);
    }
    private const int ScopeResolution=512;
    private GameObject? scopeLens,scopeEye;private Camera? scopeCamera;private RenderTexture? scopeTexture;
    private Material? scopeMaterial,reticleMaterial;private Texture2D? reticleTexture;private readonly List<Mesh> scopeMeshes=new();
    internal bool ScopeViewing=>scopeCamera!=null&&scopeCamera.enabled;
    private bool scopeTried;private Vector3 scopeLensLocal;private float scopeRadius;
    private void TickScope()
    {
        if(!EquipmentProfile.Scoped(Profile)||root==null)return;
        if(!scopeTried){scopeTried=true;try{BuildScope();}catch(Exception ex){DisposeScope();Bootstrap.Warn("SCOPE unavailable: "+ex.Message);}}
        if(scopeCamera==null)return;
        // Render only while the eyepiece can actually be seen (cost: one
        // extra 512x512 view).
        bool visible=root.activeInHierarchy;
        var rig=CameraRig.Current;
        if(visible&&rig!=null)
        {
            var lens=root.transform.TransformPoint(scopeLensLocal);
            visible=ScopeGeometry.Viewing(ContactWorld.V(lens),ContactWorld.V(rig.HeadPosition),ContactWorld.V(root.transform.forward));
        }
        if(scopeCamera.enabled!=visible)
        {
            scopeCamera.enabled=visible;
            // 0.1.121: dark glass while nobody looks through (no stale picture).
            if(scopeMaterial!=null)scopeMaterial.color=visible?Color.white:new Color(.05f,.06f,.07f,1);
        }
        if(!visible)return;
        // 0.1.103: a small dark ball sat in the centre of the picture — an
        // object close in front of the muzzle (not the world). Clip everything
        // closer than the first real surface ahead (at most 1.5 m) so only the
        // target area is drawn, and report what is on the line once.
        try
        {
            var t=scopeCamera.transform;float near=1.5f;
            foreach(var hit in Physics.RaycastAll(t.position,t.forward,1.5f,~0,QueryTriggerInteraction.Ignore))
            {
                var c=hit.collider;if(c==null||c.transform.IsChildOf(root.transform)||rig?.PlayerRoot!=null&&c.transform.IsChildOf(rig.PlayerRoot))continue;
                if(ContactFilter.NonPhysicalName(c.name)||c.name.StartsWith("prj_",StringComparison.OrdinalIgnoreCase))continue;
                near=Math.Min(near,hit.distance);
            }
            near=Mathf.Clamp(near-.03f,.05f,1.5f);
            if(Math.Abs(scopeCamera.nearClipPlane-near)>.005f)scopeCamera.nearClipPlane=near;
            if(scopeReports<2&&Time.realtimeSinceStartup>=nextScopeReport){scopeReports++;nextScopeReport=Time.realtimeSinceStartup+10;ReportScopeLine(t);}
        }
        catch(Exception ex){if(scopeReports<3){scopeReports=3;Bootstrap.Warn("SCOPE near clip: "+ex.Message);}}
    }
    private int scopeReports;private float nextScopeReport;
    private void ReportScopeLine(Transform t)
    {
        var ray=new Ray(t.position,t.forward);var found=new List<string>();
        foreach(var obj in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Renderer>()))
        {
            var r=obj.TryCast<Renderer>();if(r==null||!r.enabled||!r.gameObject.activeInHierarchy)continue;
            var b=r.bounds;if(b.size.magnitude>1.5f||!b.IntersectRay(ray,out float d)||d>8)continue;
            if(r.transform.IsChildOf(root!.transform))continue;
            string path=r.name;var p=r.transform.parent;for(int i=0;i<3&&p!=null;i++,p=p.parent)path=p.name+"/"+path;
            var m=r.sharedMaterial;
            found.Add(path+" d="+d.ToString("F2")+" size="+b.size.magnitude.ToString("F3")+" layer="+r.gameObject.layer+" shader="+(m!=null&&m.shader!=null?m.shader.name:"none"));
            if(found.Count>=12)break;
        }
        Bootstrap.Write("SCOPE line of sight: near="+scopeCamera!.nearClipPlane.ToString("F2")+" objects="+(found.Count==0?"none":string.Join(" | ",found)));
    }
    internal bool HasScope=>scopeCamera!=null;
    private void BuildScope()
    {
        if(Profile!="sniper"){BuildTubeScope();return;}
        Vector3? top=null,side=null;
        foreach(var part in animatedParts)
        {
            if(part.Snapshot==null)continue;var bones=part.Snapshot.Bones;
            for(int i=0;i<bones.Length;i++)
            {
                if(bones[i]==null)continue;string name=bones[i].name.ToLowerInvariant();
                if(name.Contains("knob_top"))top=GameWorldToFitted.MultiplyPoint3x4(bones[i].position);
                if(name.Contains("knob_side"))side=GameWorldToFitted.MultiplyPoint3x4(bones[i].position);
            }
        }
        if(top==null)throw new InvalidOperationException("scope turret bone not found");
        // Axis: under the elevation turret, at the height of the windage turret.
        var k=top.Value;float axisY=side?.y??k.y-.03f;
        float radius=Mathf.Clamp(side!=null?Math.Abs(side.Value.x-k.x)*.55f:.018f,.012f,.026f);
        // Tube extent and eyecup opening from the weapon's own vertices
        // (radial distance from the scope axis).
        float rear=float.PositiveInfinity,front=float.NegativeInfinity;int hits=0;
        var near=new List<(float z,float r)>();   // upper half only: the receiver is below
        var axial=new List<(float z,float r)>();  // every vertex close to the scope axis
        foreach(var part in animatedParts)
        {
            var mesh=part.Mesh;if(mesh==null)continue;var m=fitMatrix*part.Matrix;
            foreach(var v in mesh.vertices)
            {
                var p=m.MultiplyPoint3x4(v);
                if(Math.Abs(p.z-k.z)>.25f)continue;
                float r=MathF.Sqrt((p.x-k.x)*(p.x-k.x)+(p.y-axisY)*(p.y-axisY));
                if(r<.035f&&p.y>=axisY-.002f)near.Add((p.z,r));
                if(r<.03f)axial.Add((p.z,r));
                if(r>radius*1.25f)continue;
                rear=Math.Min(rear,p.z);front=Math.Max(front,p.z);hits++;
            }
        }
        if(hits<20||!(front>rear)){rear=k.z-.10f;front=k.z+.10f;}
        // 0.1.98: the lens sits INSIDE the eyecup, at the eyepiece, not behind
        // the rearmost vertex (it hung in the air behind the cup). Its radius
        // fills the cup's inner opening.
        float cupEnd=rear;foreach(var n in near)if(n.r<.024f&&n.z>rear-.04f)cupEnd=Math.Min(cupEnd,n.z);
        // 0.1.105: the opening = inner radius of the cup's REAR RIM only; deeper
        // inside sits the eyepiece glass (r~5 mm), which made the picture tiny.
        float inner=float.PositiveInfinity;foreach(var n in near)if(n.z<=cupEnd+.006f&&n.r>.004f)inner=Math.Min(inner,n.r);
        float lensRadius=Math.Clamp(float.IsFinite(inner)?inner*.97f:radius,.009f,.022f);
        float lensZ=Math.Min(cupEnd+.02f,(cupEnd+front)*.5f);
        // 0.1.104: the model's own eyepiece glass (a dark dome inside the cup)
        // sat in front of the picture — the "dot" in the middle of the scope.
        // Put the picture in front of the first surface inside the opening.
        float dome=float.PositiveInfinity;
        foreach(var a in axial)if(a.r<lensRadius*.95f&&a.z>cupEnd+.001f&&a.z<lensZ+.001f)dome=Math.Min(dome,a.z);
        if(float.IsFinite(dome))lensZ=Math.Max(cupEnd+.001f,dome-.0015f);
        scopeLensLocal=new Vector3(k.x,axisY,lensZ);scopeRadius=lensRadius;
        // 0.1.101: the picture is taken on the BORE line (not the scope's own
        // axis 5-6 cm higher): the reticle centre is exactly where the bullet
        // goes, at any distance.
        var cameraLocal=new Vector3(MuzzleOffset.x,MuzzleOffset.y,Math.Max(front,MuzzleOffset.z)+.03f);
        LogScope("SCOPE built turret="+k.ToString("F3")+" side="+(side?.ToString("F3")??"none")+" axisY="+axisY.ToString("F3")+" radius="+radius.ToString("F3")+" tube=["+rear.ToString("F3")+".."+front.ToString("F3")+"] cupEnd="+cupEnd.ToString("F3")+" inner="+(float.IsFinite(inner)?inner.ToString("F3"):"none")+" dome="+(float.IsFinite(dome)?dome.ToString("F3"):"none")+" lensZ="+lensZ.ToString("F3")+" lensR="+lensRadius.ToString("F3")+" hits="+hits+" camera="+cameraLocal.ToString("F3"));
        CreateScope(cameraLocal);
    }
    private void LogScope(string text)
    {
        var mainCamera=CameraRig.Current?.MainCamera;
        Bootstrap.Write(text+" fov="+ScopeFov+" mainNear="+(mainCamera!=null?mainCamera.nearClipPlane.ToString("F3"):"?"));
    }
    // 0.1.117: the crossbow has no scope bones: its tube is found in its mesh
    // (ScopeGeometry); the picture sits just inside the tube's rear end.
    private void BuildTubeScope()
    {
        // 0.1.251: searched at the size it was measured at (ScopeGeometry.ToTuned), grown back after.
        var anchor=new System.Numerics.Vector3(MuzzleOffset.x,MuzzleOffset.y,MuzzleOffset.z);
        float growth=Profile=="crossbow"&&fittedLength>0?fittedLength/EquipmentProfile.ScopeTunedLength:1;
        if(!(growth>.5f&&growth<2f))growth=1;
        var points=new List<System.Numerics.Vector3>();
        foreach(var part in animatedParts)
        {
            var mesh=part.Mesh;if(mesh==null)continue;var m=fitMatrix*part.Matrix;
            foreach(var v in mesh.vertices){var p=m.MultiplyPoint3x4(v);points.Add(ScopeGeometry.ToTuned(new System.Numerics.Vector3(p.x,p.y,p.z),anchor,growth));}
        }
        // 0.1.119: the rig's own scope bones (crossbow: scope_adjust, scope_lock) mark the scope.
        Vector3? hint=null;string hintName="none";
        foreach(var part in animatedParts)
        {
            if(part.Snapshot==null)continue;var bones=part.Snapshot.Bones;
            foreach(var bone in bones)
            {
                if(bone==null)continue;string name=bone.name.ToLowerInvariant();
                if(!name.Contains("scope"))continue;
                var at=GameWorldToFitted.MultiplyPoint3x4(bone.position);
                // Prefer the adjustment turret (on the tube), then any scope bone.
                if(hint==null||name.Contains("adjust")&&!hintName.Contains("adjust")){hint=at;hintName=bone.name;}
            }
        }
        System.Numerics.Vector3? tunedHint=hint==null?null:ScopeGeometry.ToTuned(new System.Numerics.Vector3(hint.Value.x,hint.Value.y,hint.Value.z),anchor,growth);
        var watch=System.Diagnostics.Stopwatch.StartNew();
        var tube=ScopeGeometry.Find(points,tunedHint);
        // 0.1.122: never the whole gun when the rig marks its scope (that found
        // the crossbow's rail); a plain tube under the marked turret instead.
        if(tube==null&&tunedHint!=null){Bootstrap.Warn("SCOPE "+Profile+" no tube at "+hintName+" "+hint!.Value.ToString("F3")+"; lens placed under that bone");tube=ScopeGeometry.FromHint(tunedHint.Value);}
        if(tube==null)throw new InvalidOperationException("no scope tube found in the "+Profile+" mesh ("+points.Count+" vertices, "+watch.ElapsedMilliseconds+" ms)");
        // 0.1.123: the lens at the eyepiece's own opening (not the tube's radius).
        // 0.1.252: on the eyepiece's own glass when it has one across its opening.
        var rim=ScopeGeometry.Eyepiece(points,tube.Value);var glass=ScopeGeometry.FindGlass(points,rim);
        var eye=ScopeGeometry.FromTuned(glass is ScopeGeometry.Glass found?ScopeGeometry.OnGlass(rim,found):rim,anchor,growth);
        string glassNote=glass is ScopeGeometry.Glass g?" glass z="+(anchor.Z+(g.Z-anchor.Z)*growth).ToString("F3")+" r="+(g.Radius*growth).ToString("F3")+" opening="+(g.Opening*growth).ToString("F3")+" (the picture on it; at the rim it was z="+(anchor.Z+(rim.Z-anchor.Z)*growth).ToString("F3")+" r="+(rim.Radius*growth).ToString("F3")+")":" glass=none";
        var t=ScopeGeometry.FromTuned(tube.Value,anchor,growth);
        scopeRadius=eye.Radius;
        scopeLensLocal=new Vector3(eye.X,eye.Y,eye.Z);
        var cameraLocal=new Vector3(MuzzleOffset.x,MuzzleOffset.y,Math.Max(t.Front,MuzzleOffset.z)+.03f);
        LogScope("SCOPE built "+Profile+" tube axis=("+t.X.ToString("F3")+","+t.Y.ToString("F3")+") r="+t.Radius.ToString("F3")+" z=["+t.Rear.ToString("F3")+".."+t.Front.ToString("F3")+"] sectors="+t.Sectors+" support="+t.Support
            +" eyepiece rear="+eye.Rear.ToString("F3")+" depth="+eye.Depth.ToString("F3")+" rimSectors="+eye.Sectors+" points="+eye.Points
            +" lens="+scopeLensLocal.ToString("F3")+" lensR="+scopeRadius.ToString("F3")+glassNote+" camera="+cameraLocal.ToString("F3")+" searchMs="+watch.ElapsedMilliseconds+" hint="+hintName+(hint!=null?" "+hint.Value.ToString("F3"):"")+(growth!=1?" (searched at "+EquipmentProfile.ScopeTunedLength.ToString("F2")+" m, the gun drawn "+fittedLength.ToString("F2")+" m)":""));
        CreateScope(cameraLocal);
    }
    // 0.1.133: the lens of each scoped kind (fitted frame), for a copy of it
    // held in a hand (raised to the eye it comes into play).
    internal static readonly Dictionary<string,Vector3> ScopeLenses=new();
    // 0.1.140: measured on screenshots: the cross sat about 6 deg
    // counter-clockwise from the crossbow's scope and limbs.
    internal const float CrossbowReticleRoll=-6f;
    private void CreateScope(Vector3 cameraLocal)
    {
        ScopeLenses[Profile]=scopeLensLocal;
        var mainCamera=CameraRig.Current?.MainCamera;
        scopeTexture=new RenderTexture(ScopeResolution,ScopeResolution,24);scopeTexture.name="XIII VR scope";scopeTexture.Create();
        var shader=Shader.Find("Sprites/Default")??Shader.Find("UI/Default")??throw new InvalidOperationException("no unlit texture shader");
        scopeMaterial=new Material(shader){mainTexture=scopeTexture};scopeMaterial.renderQueue=3000;scopeMaterial.color=new Color(.05f,.06f,.07f,1);
        reticleTexture=Reticle();reticleMaterial=new Material(shader){mainTexture=reticleTexture};reticleMaterial.renderQueue=3001;
        scopeLens=Disc("XIII VR scope lens",scopeMaterial,scopeLensLocal,scopeRadius);
        scopeEye=Disc("XIII VR scope reticle",reticleMaterial,scopeLensLocal-new Vector3(0,0,.0008f),scopeRadius);
        // 0.1.140: the cross lined up with the weapon itself. The picture needs no turn (it is a window).
        float roll=PairRollDegrees,trim=0;string from=pairRollCount+" left/right bone pairs";
        if(!float.IsFinite(roll)||Math.Abs(roll)>20){roll=0;from="no usable bone pairs";}
        // 0.1.252: the crossbow's own (its model's turn, measured on it); the tactical crossbow and the
        // harpoon gun are level by their bones: the cross stood 6 degrees clockwise in the tactical one.
        if(Profile=="crossbow"&&ModelKey=="crossbow"&&Math.Abs(roll)<1){roll=CrossbowReticleRoll;from+=", the crossbow's measured turn";}
        try{trim=Math.Clamp(WeaponOptions.ScopeReticleRoll.Value,-30,30);}catch(Exception){}
        scopeEye.transform.localRotation=Quaternion.AngleAxis(roll+trim,Vector3.forward);
        Bootstrap.Write("SCOPE "+Profile+" reticle turned "+(roll+trim).ToString("F1")+" deg (+ = counter-clockwise) to the weapon's own level ("+from+(trim!=0?"; trim "+trim.ToString("F1"):"")+")");
        var go=new GameObject("XIII VR scope camera");go.transform.SetParent(root!.transform,false);
        go.transform.localPosition=cameraLocal;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;
        scopeCamera=go.AddComponent(Il2CppType.Of<Camera>()).TryCast<Camera>()!;
        // 0.1.98: no CopyFrom. It also copied the VR camera's explicit
        // view/projection matrices, so the scope rendered one frozen view and
        // ignored its own position and zoom. Take only the look settings.
        var main=mainCamera;
        if(main!=null)
        {
            scopeCamera.cullingMask=main.cullingMask;scopeCamera.clearFlags=main.clearFlags;scopeCamera.backgroundColor=main.backgroundColor;
            scopeCamera.renderingPath=main.renderingPath;scopeCamera.allowHDR=main.allowHDR;scopeCamera.useOcclusionCulling=main.useOcclusionCulling;
            scopeCamera.depth=main.depth-1;
        }
        scopeCamera.ResetWorldToCameraMatrix();scopeCamera.ResetProjectionMatrix();scopeCamera.ResetCullingMatrix();
        scopeCamera.ResetStereoViewMatrices();scopeCamera.ResetStereoProjectionMatrices();
        scopeCamera.stereoTargetEye=StereoTargetEyeMask.None;scopeCamera.allowMSAA=false;scopeCamera.targetTexture=scopeTexture;
        scopeCamera.fieldOfView=ScopeFov;scopeCamera.aspect=1;scopeCamera.nearClipPlane=.05f;
        scopeCamera.farClipPlane=Math.Max(main!=null?main.farClipPlane:1000,600);
        scopeCamera.enabled=false;
    }
    private GameObject Disc(string name,Material material,Vector3 local,float radius)
    {
        const int segments=40;var vertices=new Vector3[segments+1];var uv=new Vector2[segments+1];var triangles=new int[segments*3];
        vertices[0]=Vector3.zero;uv[0]=new Vector2(.5f,.5f);
        for(int i=0;i<segments;i++)
        {
            float a=i*Mathf.PI*2/segments;var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
            vertices[i+1]=new Vector3(d.x*radius,d.y*radius,0);uv[i+1]=new Vector2(.5f+d.x*.5f,.5f+d.y*.5f);
            triangles[i*3]=0;triangles[i*3+1]=1+(i+1)%segments;triangles[i*3+2]=1+i;
        }
        var mesh=new Mesh();mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();scopeMeshes.Add(mesh);
        var go=new GameObject(name);go.transform.SetParent(root!.transform,false);go.transform.localPosition=local;go.transform.localRotation=Quaternion.identity;
        go.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>()!.sharedMesh=mesh;
        var renderer=go.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>()!;
        renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        return go;
    }
    // PSO-1 style: crosshair with a gap, three chevrons below the centre and
    // a soft dark edge; transparent elsewhere.
    private static Texture2D Reticle()
    {
        const int n=256;var t=new Texture2D(n,n,TextureFormat.RGBA32,false);t.wrapMode=TextureWrapMode.Clamp;t.name="XIII VR scope reticle";
        var pixels=new Color32[n*n];float c=(n-1)*.5f;
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            float dx=x-c,dy=y-c,r=MathF.Sqrt(dx*dx+dy*dy)/c;byte a=0;
            if(r>.86f)a=(byte)Math.Min(255,(r-.86f)/.14f*255);                       // vignette
            bool line=(Math.Abs(dx)<1.2f&&Math.Abs(dy)>10&&r<.86f)||(Math.Abs(dy)<1.2f&&Math.Abs(dx)>10&&r<.86f);
            for(int k=1;k<=3;k++){float cy=-k*16f;if(Math.Abs(Math.Abs(dx)-(cy-dy))<1.3f&&dy<=cy&&dy>cy-7)line=true;}
            if(line)a=230;
            pixels[y*n+x]=new Color32(0,0,0,a);
        }
        t.SetPixels32(pixels);t.Apply(false,false);return t;
    }
    private void DisposeScope()
    {
        if(scopeCamera!=null){scopeCamera.targetTexture=null;UnityEngine.Object.Destroy(scopeCamera.gameObject);}scopeCamera=null;
        if(scopeLens!=null)UnityEngine.Object.Destroy(scopeLens);scopeLens=null;
        if(scopeEye!=null)UnityEngine.Object.Destroy(scopeEye);scopeEye=null;
        if(scopeTexture!=null){scopeTexture.Release();UnityEngine.Object.Destroy(scopeTexture);}scopeTexture=null;
        if(scopeMaterial!=null)UnityEngine.Object.Destroy(scopeMaterial);scopeMaterial=null;
        if(reticleMaterial!=null)UnityEngine.Object.Destroy(reticleMaterial);reticleMaterial=null;
        if(reticleTexture!=null)UnityEngine.Object.Destroy(reticleTexture);reticleTexture=null;
        foreach(var m in scopeMeshes)if(m!=null)UnityEngine.Object.Destroy(m);scopeMeshes.Clear();
    }
}
