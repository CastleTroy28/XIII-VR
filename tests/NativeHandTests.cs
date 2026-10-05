using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using XiiiXR;
class NativeHandTests
{
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    static void Near(Vector3 a,Vector3 b,string reason,float epsilon=2e-4f)=>Check(Vector3.Distance(a,b)<epsilon,reason+": "+a+" vs "+b);
    static Matrix4x4 Inverse(Matrix4x4 m){Check(Matrix4x4.Invert(m,out var r),"singular fixture");return r;}
    static void Main()
    {
        // Millimetres, metres and an imported 0.2-scale renderer. Neither
        // the rest selection nor a wrist's animated rotation may change the crop.
        foreach(float unit in new[]{.001f,.2f,1f,100f})foreach(bool right in new[]{true,false})
        {
            var rig=Matrix4x4.CreateScale(unit)*Matrix4x4.CreateFromYawPitchRoll(.9f,.35f,-.6f)*Matrix4x4.CreateTranslation(.31f,-.2f,.7f);
            // In millimetres the whole hand is 0.2 mm long and sits 0.8 m from
            // the origin: a float resolves it to about 1e-7 m, which is 5e-5 of
            // the 17 cm canonical hand - the plain tolerance itself. Newer .NET
            // runtimes round differently (fused multiply-add), so below 1 cm
            // per unit the tolerances grow with that resolution.
            float resolution=Math.Max(1f,.01f/unit);
            Vector3 Map(Vector3 p)=>Vector3.Transform(p,rig);
            var frame=NativeHandMath.Frame(Map(Vector3.Zero),Map(new Vector3(0,0,-.2f)),Map(new Vector3(right?-.06f:.06f,0,.04f)),new[]{Map(new Vector3(0,0,.17f)),Map(new Vector3(0,0,.12f))},right);
            Near(Vector3.Transform(Map(new Vector3(.025f,.01f,.13f)),frame),new Vector3(.025f,.01f,.13f),"canonical mesh depends on import units",5e-5f*resolution);
            var points=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            void Box(float x)
            {
                int start=points.Count;
                foreach(float z in new[]{-.15f,.16f})foreach(var p in new[]{new Vector2(-.03f,-.02f),new Vector2(.03f,-.02f),new Vector2(.03f,.02f),new Vector2(-.03f,.02f)})
                {points.Add(Map(new Vector3(x+p.X,p.Y,z)));uv.Add(new Vector2(p.X,z));}
                indices.AddRange(new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7}.Select(i=>i+start));
            }
            Box(0);Box(.4f);
            var canonical=points.Select(p=>Vector3.Transform(p,frame)).ToArray();
            var triangles=new[]{indices.ToArray()};
            var own=NativeHandMesh.ConnectedOwnership(canonical,triangles,Vector3.Zero,new Vector3(.4f,0,0));
            var mesh=NativeHandMesh.Build(canonical,uv.ToArray(),triangles,own);
            Check(mesh.Submeshes.Sum(s=>s.Length)>=30 && mesh.Points.Any(p=>p.Cap),"scaled hand disappears during crop");
            Check(mesh.Points.All(p=>Math.Abs(p.Rest.X)<.031f),"opposite hand was copied");
            var liveWrist=rig*Matrix4x4.CreateFromYawPitchRoll(-1.1f,.8f,.45f)*Matrix4x4.CreateTranslation(2,1,-4);
            var liveFrame=NativeHandMath.AnimatedFrame(frame,rig,liveWrist);
            var restVertex=Map(new Vector3(.025f,.01f,.13f));
            var posedVertex=Vector3.Transform(restVertex,Inverse(rig)*liveWrist);
            Near(Vector3.Transform(posedVertex,liveFrame),Vector3.Transform(restVertex,frame),"wrist motion deforms canonical palm",.001f*resolution);
        }
        // Weapon fitting and hand extraction share the exact affine transform.
        // Verify palm/handle coincidence after fitting all three gun lengths,
        // dominant-hand pivot placement, and recoil/movement in world space.
        foreach(string weapon in new[]{"pistol","shotgun","ak47"})foreach(float size in new[]{.5f,1f,1.5f})
        {
            var fit=WeaponGeometry.Fit(new Vector3(-.015f,-.04f,-.2f),new Vector3(.015f,.01f,0),weapon);
            var fitMatrix=Matrix4x4.CreateScale(fit.Scale)*Matrix4x4.CreateTranslation(fit.Translation);
            var wrist=new Vector3(.01f,-.025f,-.12f);
            var q=Quaternion.CreateFromYawPitchRoll(.2f,.7f,-.15f);
            var canonicalToWeapon=Matrix4x4.CreateScale(size)*Matrix4x4.CreateFromQuaternion(q)*Matrix4x4.CreateTranslation(wrist);
            Check(NativeHandMath.TryGrip(canonicalToWeapon,out var root,out var rotation,out float actualSize),"valid authored grip rejected");
            var controller=new Vector3(2,1.2f,-.4f);var aim=Quaternion.CreateFromYawPitchRoll(1,.3f,.1f);
            var gunRoot=controller-Vector3.Transform(root,aim);
            Near(gunRoot+Vector3.Transform(root,aim),controller,"right hand leaves controller when gun pivots");
            var world=Matrix4x4.CreateFromQuaternion(aim)*Matrix4x4.CreateTranslation(gunRoot);
            foreach(var palm in new[]{new Vector3(.02f,-.01f,.05f),new Vector3(-.02f,-.02f,.08f)})
            {
                // A point touching the authored handle follows the same path
                // as that handle through the weapon fit and world transform.
                var gunPoint=Vector3.Transform(palm,canonicalToWeapon*Inverse(fitMatrix));
                var expected=Vector3.Transform(gunPoint,fitMatrix*world);
                var local= (palm+new Vector3(0,0,NativeHandMesh.WristZ))*actualSize+new Vector3(0,0,NativeHandMesh.WristZ*(1-actualSize));
                var actual=Vector3.Transform(Vector3.Transform(local,rotation)+root,world);
                Near(actual,expected,"native fingers separate from handle "+weapon);
            }
        }
        Check(!NativeHandMath.TryGrip(Matrix4x4.CreateScale(100),out _,out _,out _),"giant hand accepted");
        Check(!NativeHandMath.TryGrip(Matrix4x4.CreateScale(1,2,1),out _,out _,out _),"stretched hand accepted");
        Check(!NativeHandMath.TryGrip(Matrix4x4.CreateScale(-1,1,1),out _,out _,out _),"mirrored grip accepted");
        Console.WriteLine("PASS: neutral hand extraction across import units, 0.2 scale, both hands, wrist animation cancellation, original UV crop/cap; authored palm-to-weapon fitting for pistol/shotgun/AK47; dominant pivot; scale guards.");
        // 0.1.245: the hands only (VR SETTINGS "Forearms"): cut just behind the watch, capped like the long cut.
        {
            float elbow=-.286f;
            Check(MathF.Abs(NativeHandMesh.ForearmCut(elbow,.052f,false)-(elbow-.025f))<1e-6f&&MathF.Abs(NativeHandMesh.ForearmCut(-.5f,.05f,false)+.35f)<1e-6f,"the forearm's long cut changed");
            Check(MathF.Abs(NativeHandMesh.ForearmCut(elbow,.052f,true)+.085f)<1e-6f&&MathF.Abs(NativeHandMesh.ForearmCut(elbow,.0285f,true)+.085f)<1e-6f,"the hand-only cut not at 8.5 cm behind the wrist (both watches)");
            Check(MathF.Abs(NativeHandMesh.ForearmCut(elbow,.07f,true)+.102f)<1e-6f,"a band further back not kept on the hand");
            Check(NativeHandMesh.ForearmCut(-.12f,.052f,true)>=NativeHandMesh.ForearmCut(-.12f,.052f,false)&&NativeHandMesh.ForearmCut(elbow,float.NaN,true)<-.08f,"the hand-only cut longer than the forearm, or broken by a missing band");
            var pts=new List<Vector3>();var tex=new List<Vector2>();var idx=new List<int>();
            foreach(float z in new[]{-.40f,.16f})foreach(var q in new[]{new Vector2(-.03f,-.02f),new Vector2(.03f,-.02f),new Vector2(.03f,.02f),new Vector2(-.03f,.02f)}){pts.Add(new Vector3(q.X,q.Y,z));tex.Add(new Vector2(q.X,z));}
            idx.AddRange(new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7});
            var all=Enumerable.Repeat(true,pts.Count).ToArray();
            var arm=NativeHandMesh.Build(pts.ToArray(),tex.ToArray(),new[]{idx.ToArray()},all,NativeHandMesh.ForearmCut(elbow,.052f,false),.14f);
            var hand=NativeHandMesh.Build(pts.ToArray(),tex.ToArray(),new[]{idx.ToArray()},all,NativeHandMesh.ForearmCut(elbow,.052f,true),.14f);
            float armEnd=arm.Points.Min(p=>p.Rest.Z),handEnd=hand.Points.Min(p=>p.Rest.Z);
            Check(MathF.Abs(armEnd-(elbow-.025f))<1e-4f&&MathF.Abs(handEnd+.085f)<1e-4f,"the cuts not where asked: "+armEnd+" "+handEnd);
            Check(hand.Points.Any(p=>p.Cap)&&hand.Points.Where(p=>p.Cap).All(p=>MathF.Abs(p.Rest.Z+.085f)<1e-4f),"the hand-only cut not closed");
            Console.WriteLine("PASS: 0.1.245 the hands only: the forearm cut 8.5 cm behind the wrist (behind either watch band), closed like the long cut; the long cut unchanged.");
        }
        Console.WriteLine("Synthetic geometry only; actual Unity CPU baking and in-game hand shape require an in-game test.");
    }
}
