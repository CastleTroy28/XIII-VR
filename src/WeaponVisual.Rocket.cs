using System;
using System.Collections.Generic;
using UnityEngine;
namespace XiiiXR;
// 0.1.217: the bazooka's own rocket in its tube (the rocket bones of its
// rig). The game hides it when it fires and shows it again with its reload
// animation; a rocket put in by hand (WeaponHands.Bazooka) plays no such
// animation, so it stayed hidden though the bazooka was loaded. With the
// hand reload the mod draws it: in the tube while loaded (where a loaded
// bazooka has it), gone while empty. Also the tube's real mouth (fitted
// frame), where the rocket goes in.
internal sealed partial class WeaponVisual
{
    private Part? rocketPart;private int[]? rocketBones;private bool[]? rocketMask;private int rocketReference=-1;
    private Matrix4x4[]? rocketRelation;private bool rocketSearched;private int rocketDrawn=-1;
    internal Vector3? TubeMouth{get;private set;}
    private static string RocketFile=>System.IO.Path.Combine(BepInEx.Paths.ConfigPath,"XIII-XR-bazooka-rocket.txt");
    private void FindRocket()
    {
        if(rocketSearched)return;rocketSearched=true;
        if(Profile!="bazooka")return;
        try
        {
            foreach(var part in animatedParts)
            {
                var s=part.Snapshot;if(s==null)continue;
                var bones=s.Bones;var names=new string?[bones.Length];
                for(int i=0;i<bones.Length;i++)names[i]=bones[i]!=null?bones[i].name:null;
                var mask=new bool[bones.Length];var list=new List<int>();
                for(int i=0;i<bones.Length;i++)if(BazookaTubeMath.RocketBone(names[i])){mask[i]=true;list.Add(i);}
                // Bones under the rocket go with it.
                for(int i=0;i<bones.Length;i++)if(!mask[i]&&bones[i]!=null)foreach(int r in list.ToArray())if(bones[r]!=null&&bones[i].IsChildOf(bones[r])){mask[i]=true;list.Add(i);break;}
                if(list.Count==0)continue;
                int reference=BazookaTubeMath.Root(names);if(reference<0||mask[reference]){Bootstrap.Warn("BAZOOKA rocket: no tube bone to hold it to");return;}
                var picked=list.ToArray();var boneNames=new string[picked.Length];for(int k=0;k<picked.Length;k++)boneNames[k]=names[picked[k]]??"";
                rocketPart=part;rocketBones=picked;rocketMask=mask;rocketReference=reference;
                // Its place in the tube: as the loaded bazooka had it when taken, else as saved
                // from an earlier one, else the rig's bind pose - the first of them in the tube.
                var taken=LoadedAtBuild?s.LoadedRelation(reference,picked):null;var saved=LoadRelation(RocketFile,"rocket",boneNames);
                var tried=new List<(Matrix4x4[]? relation,string from)>();
                if(taken!=null)tried.Add((taken,"the loaded bazooka when taken"));
                if(saved!=null)tried.Add((saved,"the saved placement"));
                tried.Add((null,"the rig's bind pose"));
                string outcome="";bool placed=false;
                foreach(var (relation,from) in tried)
                {
                    rocketRelation=relation;
                    if(MeasureTube(part,s,mask,from,out string report)){placed=true;outcome=report;if(relation==taken&&taken!=null)SaveRelation(RocketFile,"rocket",boneNames,taken);break;}
                    outcome+=(outcome.Length>0?"; ":"")+report;
                }
                if(!placed){rocketRelation=tried[0].relation;TubeMouth=null;Bootstrap.Warn("BAZOOKA rocket: no placement lies in the tube ("+outcome+"); the first one is used, the glow at the aim point");}
                else Bootstrap.Write("BAZOOKA rocket: "+picked.Length+" bones held to "+(names[reference]??"?")+"; "+outcome);
                return;
            }
            Bootstrap.Warn("BAZOOKA rocket bones not found; the rocket in the tube stays as the game draws it");
        }
        catch(Exception ex){rocketMask=null;rocketPart=null;Bootstrap.Warn("BAZOOKA rocket bones: "+ex.Message);}
    }
    // The tube's mouth from the bazooka drawn loaded: the front of the tube on
    // the rocket's line. False: that placement does not put the rocket in the tube.
    private bool MeasureTube(Part part,NativeSkinSnapshot s,bool[] mask,string from,out string report)
    {
        var temp=new Mesh();report=from+": ";
        try
        {
            // 0.1.229: in the pose the bazooka is drawn in (HeldStill: as taken).
            var basePose=HeldStill&&s.Initial!=null?s.Initial:s.Live;
            if(rocketRelation!=null)s.BakeRelation(temp,basePose,rocketReference,rocketBones!,rocketRelation);
            else if(HeldStill&&s.Initial!=null)s.BakeRelation(temp,basePose,rocketReference,rocketBones!,s.BindRelation(rocketReference,rocketBones!));
            else s.BakeRestored(temp,rocketReference,rocketBones!,null,null,null);
            var weights=s.Original.boneWeights;var vertices=temp.vertices;
            if(weights.Length!=vertices.Length){report+="weights unavailable ("+weights.Length+" for "+vertices.Length+" points)";return false;}
            static int Dominant(BoneWeight w)=>w.weight0>=w.weight1&&w.weight0>=w.weight2&&w.weight0>=w.weight3?w.boneIndex0:w.weight1>=w.weight2&&w.weight1>=w.weight3?w.boneIndex1:w.weight2>=w.weight3?w.boneIndex2:w.boneIndex3;
            var map=fitMatrix*part.Matrix;var tube=new List<System.Numerics.Vector3>();var rocket=new List<System.Numerics.Vector3>();
            float lo=float.PositiveInfinity,hi=float.NegativeInfinity;
            for(int i=0;i<vertices.Length;i++)
            {
                var p=map.MultiplyPoint3x4(vertices[i]);var n=new System.Numerics.Vector3(p.x,p.y,p.z);
                int b=Dominant(weights[i]);
                if(b>=0&&b<mask.Length&&mask[b]){rocket.Add(n);lo=Math.Min(lo,p.z);hi=Math.Max(hi,p.z);}else tube.Add(n);
            }
            report+=rocket.Count+" points from z="+lo.ToString("F3")+" to "+hi.ToString("F3");
            if(!BazookaTubeMath.InTube(rocket,tube)){report+=" (not in the tube)";return false;}
            var mouth=BazookaTubeMath.Mouth(tube,rocket);
            TubeMouth=float.IsFinite(mouth.Z)?new Vector3(mouth.X,mouth.Y,mouth.Z):null;
            report+=", in the tube; its mouth "+(TubeMouth is Vector3 m?m.ToString("F3"):"not found (the aim point is used)")+" (aim point "+MuzzleOffset.ToString("F3")+")";
            return true;
        }
        catch(Exception ex){report+=ex.Message;return false;}
        finally{UnityEngine.Object.Destroy(temp);}
    }
    // 0.1.229: how far the game's animation has the bazooka's tube from where
    // it is drawn (HeldStill), in the fitted frame: metres and degrees.
    internal bool StillDrift(out float meters,out float degrees)
    {
        meters=degrees=0;FindRocket();
        if(!HeldStill||rocketPart?.Snapshot is not NativeSkinSnapshot s||!s.StillDrift(rocketReference,out var offset,out degrees))return false;
        meters=(fitMatrix*rocketPart.Matrix).MultiplyVector(offset).magnitude;return true;
    }
    // owned: the hand reload draws the rocket (in the tube when loaded, else none);
    // not owned: the game's own animation does.
    internal void RocketInTube(bool owned,bool loaded)
    {
        FindRocket();var s=rocketPart?.Snapshot;if(s==null||rocketMask==null)return;
        int want=!owned?-1:loaded?1:0;if(want==rocketDrawn)return;rocketDrawn=want;
        if(want==1)s.Keep(rocketReference,rocketBones,rocketRelation);else s.Keep(-1,null,null);
        s.Hidden=want==0?rocketMask:null;
        animatedFrame=-1;
    }
}
