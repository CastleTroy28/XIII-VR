using System;
using System.Collections.Generic;
using UnityEngine;
namespace XiiiXR;
// 0.1.194. The hunting shotgun's muzzle point faces about 35
// degrees off its barrels, so the whole gun was drawn askew in the hand; its
// parked reload shell made it look far taller than it is (a smaller gun). It
// is fitted along its barrels now, without its shells, and drawn from its own
// live pose held still (not the rig's bind pose), the barrels swinging open on
// their hinge for the hand reload and the chambers' shells shown as loaded.
internal sealed partial class WeaponVisual
{
    internal bool BreakAction{get;private set;}
    private Part? breakPart;
    private BreakRig.Found breakRig;
    private bool[]? breakBarrels,breakHide;
    private bool[][]? breakChamberBones;
    private readonly Vector3[] breakMouthSkin=new Vector3[2];
    private Vector3 breakHingeSkin,breakAxisSkin;
    private Matrix4x4[]? breakPose;
    private float breakSwing;private readonly bool[] breakShown={true,true};private bool breakShellsShown;
    internal const float BreakOpenDegrees=38f;
    private Bounds? breakBounds;
    // At build, in the muzzle frame: the turn that lays the barrels along +z
    // (identity when they already do), the gun's bounds without its shells
    // (barrel frame after that turn), the chambers and the hinge.
    private bool PrepareBreak(NativeSkinSnapshot s,Mesh baked,Matrix4x4 toFrame,out Matrix4x4 correction)
    {
        correction=Matrix4x4.identity;
        var bones=s.Bones;var names=new string?[bones.Length];
        for(int i=0;i<bones.Length;i++)names[i]=bones[i]!=null?bones[i].name:null;
        var rig=BreakRig.Find(names);if(rig.Tilt<0||rig.Root<0)return false;
        var barrels=new bool[bones.Length];var shell=new bool[bones.Length];var hide=new bool[bones.Length];
        var chambers=new[]{new bool[bones.Length],new bool[bones.Length]};
        for(int i=0;i<bones.Length;i++)
        {
            if(bones[i]==null)continue;
            barrels[i]=i==rig.Tilt||bones[i].IsChildOf(bones[rig.Tilt]);
            shell[i]=BreakRig.Shell(names[i]);
            for(int c=0;c<2;c++)chambers[c][i]=BreakRig.Chamber(names[i],c);
            hide[i]=shell[i]&&!chambers[0][i]&&!chambers[1][i];
        }
        var weights=s.Original.boneWeights;var vertices=baked.vertices;
        if(weights.Length!=vertices.Length)return false;
        int Dominant(BoneWeight w)=>w.weight0>=w.weight1&&w.weight0>=w.weight2&&w.weight0>=w.weight3?w.boneIndex0:w.weight1>=w.weight2&&w.weight1>=w.weight3?w.boneIndex1:w.weight2>=w.weight3?w.boneIndex2:w.boneIndex3;
        bool In(bool[] set,int b)=>b>=0&&b<set.Length&&set[b];
        // The barrels' long axis in the muzzle frame.
        var along=new List<System.Numerics.Vector3>();
        for(int i=0;i<vertices.Length;i++){int b=Dominant(weights[i]);if(In(barrels,b)&&!In(shell,b)){var p=toFrame.MultiplyPoint3x4(vertices[i]);along.Add(new System.Numerics.Vector3(p.x,p.y,p.z));}}
        var axis=BreakRig.Axis(along.ToArray());var a=new Vector3(axis.X,axis.Y,axis.Z);
        float off=Vector3.Angle(a,Vector3.forward);
        if(off>3&&off<80)correction=Matrix4x4.Rotate(Quaternion.FromToRotation(a,Vector3.forward));
        var frame=correction*toFrame;
        // The gun without its shells, in the (turned) barrel frame.
        bool any=false;Vector3 lo=default,hi=default;
        for(int i=0;i<vertices.Length;i++)
        {
            if(In(shell,Dominant(weights[i])))continue;
            var p=frame.MultiplyPoint3x4(vertices[i]);if(!Finite(p))continue;
            if(!any){lo=hi=p;any=true;}else{lo=Vector3.Min(lo,p);hi=Vector3.Max(hi,p);}
        }
        if(!any)return false;
        var bounds=new Bounds();bounds.SetMinMax(lo,hi);breakBounds=bounds;
        // Skin space: the barrels' axis, the hinge's crosswise axis, the hinge.
        var toSkin=frame.inverse;
        breakAxisSkin=toSkin.MultiplyVector(Vector3.right).normalized;
        s.Sample();var init=s.Initial??s.Live;
        breakHingeSkin=init[rig.Tilt].GetColumn(3);
        // The chambers: at the barrels' rear end, one each side (from the
        // barrels' own shape); the rig's chamber shells are used (and shown)
        // only where they really lie in them (the game parks its shells
        // outside the gun when they are not in its reload).
        float zlo=float.PositiveInfinity,zhi=float.NegativeInfinity;
        var barrelPoints=new List<Vector3>();
        for(int i=0;i<vertices.Length;i++){int b=Dominant(weights[i]);if(In(barrels,b)&&!In(shell,b)){var p=frame.MultiplyPoint3x4(vertices[i]);if(!Finite(p))continue;barrelPoints.Add(p);zlo=Math.Min(zlo,p.z);zhi=Math.Max(zhi,p.z);}}
        if(barrelPoints.Count<8)return false;
        float length=zhi-zlo;var slice=new List<Vector3>();
        foreach(var p in barrelPoints)if(p.z<zlo+length*.05f)slice.Add(p);
        var geometric=BreakRig.Chambers(slice.ConvertAll(p=>new System.Numerics.Vector3(p.x,p.y,p.z)).ToArray(),zlo);
        breakShellsShown=false;
        var full=new Mesh();
        try
        {
            s.BakePose(full,ShownPose(s,rig,chambers,hide,init,true,true));
            var shown=full.vertices;bool valid=true;var rear=new Vector3[2];
            for(int c=0;c<2;c++)
            {
                bool has=false;var sum=Vector3.zero;int n=0;float min=float.PositiveInfinity;
                for(int i=0;i<shown.Length&&i<weights.Length;i++)if(In(chambers[c],Dominant(weights[i]))){var p=frame.MultiplyPoint3x4(shown[i]);sum+=p;n++;min=Math.Min(min,p.z);has=true;}
                if(!has){valid=false;continue;}
                var center=sum/n;var g=geometric[c];
                rear[c]=new Vector3(center.x,center.y,min);
                valid&=BreakRig.InChamber(new System.Numerics.Vector3(center.x,center.y,center.z),g,length);
            }
            var mouths=new Vector3[2];
            for(int c=0;c<2;c++)
            {
                var g=geometric[c];mouths[c]=valid?rear[c]:new Vector3(g.X,g.Y,g.Z);
                breakMouthSkin[c]=toSkin.MultiplyPoint3x4(mouths[c]);
            }
            breakShellsShown=valid;
            Bootstrap.Write("BREAK ACTION chambers "+(valid?"with the rig's shells in them":"from the barrels' shape (the rig's shells are elsewhere: not drawn)")
                +": right="+mouths[0].ToString("F4")+" left="+mouths[1].ToString("F4")+" barrels z=["+zlo.ToString("F4")+".."+zhi.ToString("F4")+"] (barrel frame)");
        }
        finally{UnityEngine.Object.Destroy(full);}
        breakRig=rig;breakBarrels=barrels;breakHide=hide;breakChamberBones=chambers;BreakAction=true;
        var inside=new List<string>();for(int i=0;i<bones.Length;i++)if(barrels[i]&&names[i]!=null)inside.Add(names[i]!);
        Bootstrap.Write("BREAK ACTION "+s.Original.name+": barrels "+off.ToString("F1")+" deg off the muzzle's forward"+(correction==Matrix4x4.identity?"":" (the gun turned onto them)")
            +"; fitted without its shells size="+bounds.size.ToString("F4")+"; hinge bone "+names[rig.Tilt]+" carries "+string.Join(",",inside)
            +"; chambers "+(rig.Right>=0?names[rig.Right]:"none")+" / "+(rig.Left>=0?names[rig.Left]:"none"));
        return true;
    }
    // The gun's pose held still (its root where it was when built), the
    // parked shell hidden; the chambers' shells kept where the rig binds them
    // to the barrels (they follow the barrels), shown or hidden.
    private static Matrix4x4[] ShownPose(NativeSkinSnapshot s,BreakRig.Found rig,bool[][] chambers,bool[] hide,Matrix4x4[] init,bool right,bool left,Matrix4x4? hinge=null,bool[]? barrels=null)
    {
        var live=s.Live;var pose=new Matrix4x4[live.Length];
        var fix=init[rig.Root]*live[rig.Root].inverse;
        var collapse=Matrix4x4.Scale(new Vector3(.001f,.001f,.001f));
        for(int i=0;i<pose.Length;i++){pose[i]=fix*live[i];if(hide[i])pose[i]=pose[i]*collapse;}
        if(hinge is Matrix4x4 h&&barrels!=null)for(int i=0;i<pose.Length;i++)if(barrels[i])pose[i]=h*pose[i];
        var tilt=pose[rig.Tilt]*s.Bind[rig.Tilt];
        for(int i=0;i<pose.Length;i++)for(int c=0;c<2;c++)if(chambers[c][i])
        {
            pose[i]=tilt*s.Bind[i].inverse;
            if(!(c==0?right:left))pose[i]=pose[i]*collapse;
        }
        return pose;
    }
    // How far open (0..1) and which chambers show a shell.
    internal void PoseBreak(float swing,bool right,bool left)
    {
        swing=Math.Clamp(float.IsFinite(swing)?swing:0,0,1);
        if(Math.Abs(swing-breakSwing)>.0005f||breakShown[0]!=right||breakShown[1]!=left){breakSwing=swing;breakShown[0]=right;breakShown[1]=left;animatedFrame=-1;}
    }
    private Matrix4x4 BreakHinge=>Matrix4x4.Translate(breakHingeSkin)*Matrix4x4.Rotate(Quaternion.AngleAxis(BreakOpenDegrees*breakSwing,breakAxisSkin))*Matrix4x4.Translate(-breakHingeSkin);
    private bool BakeBreak(Part part)
    {
        var s=part.Snapshot;if(s==null||breakBarrels==null||breakHide==null||breakChamberBones==null)return false;
        s.Sample();var init=s.Initial??s.Live;
        bool shells=breakShellsShown;
        var pose=ShownPose(s,breakRig,breakChamberBones,breakHide,init,shells&&breakShown[0],shells&&breakShown[1],breakSwing>.001f?BreakHinge:null,breakBarrels);
        breakPose=pose;s.BakePose(part.Spare!,pose);return true;
    }
    // Where chamber c's mouth is now (fitted frame), and the barrels' axis.
    internal Vector3 BreakMouth(int c)
    {
        if(breakPart==null||c<0||c>1)return Vector3.zero;
        return fitMatrix.MultiplyPoint3x4(breakPart.Matrix.MultiplyPoint3x4(BreakHinge.MultiplyPoint3x4(breakMouthSkin[c])));
    }
    internal Vector3 BreakAxis=>Quaternion.AngleAxis(BreakOpenDegrees*breakSwing,Vector3.right)*Vector3.forward;
    private Matrix4x4 BreakCorrection(Part part)
    {
        var s=part.Snapshot;if(s==null||breakRig.Root<0)return Matrix4x4.identity;
        s.Sample();
        return NativeHandVisual.U(GripSpaceMath.FrozenRoot(NativeHandVisual.N((s.Initial??s.Live)[breakRig.Root]),NativeHandVisual.N(s.Live[breakRig.Root])));
    }
}
