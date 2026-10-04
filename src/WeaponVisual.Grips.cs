using System;
using System.Collections.Generic;
using UnityEngine;
namespace XiiiXR;
// 0.1.219: the bazooka's grips (its rig's own grip bones) as bars in the
// fitted frame: the hands close round them as round a pistol's grip
// (WeaponHands.BazookaGrip). The game's own hold of the bazooka has open,
// flat hands (a log of 0.1.217 and the player's report).
internal sealed partial class WeaponVisual
{
    // The front grip's top (its bracket to the tube) left out of its bar.
    internal const float FrontTrim=.3f;
    // 0.1.221: the bazooka's plane of symmetry (x, fitted frame): its tube's line.
    // 0.1.223: the handle's own middle first (the hand wraps round it).
    internal float? SymmetryX{get{FindGripBars();if(triggerGripBar is GripBarMath.Bar t)return t.Center.X;FindRocket();if(TubeMouth is Vector3 m)return m.x;return frontGripBar is GripBarMath.Bar b?b.Center.X:null;}}
    private bool gripBarsSearched;private GripBarMath.Bar? rearGripBar,frontGripBar,triggerGripBar;
    internal GripBarMath.Bar? RearGripBar{get{FindGripBars();return rearGripBar;}}
    // 0.1.223: the handle with the trigger (the rig's mid grip).
    internal GripBarMath.Bar? TriggerGripBar{get{FindGripBars();return triggerGripBar;}}
    internal GripBarMath.Bar? FrontGripBar{get{FindGripBars();return frontGripBar;}}
    // 0.1.231: the trigger's middle (fitted frame): the index fingertip goes level with it.
    private Vector3? triggerPoint;
    internal Vector3? TriggerPoint{get{FindGripBars();return triggerPoint;}}
    // 0.1.231: the handle's own top (its whole mesh, not the bar's trimmed top): how far along its bar from its middle.
    private float? triggerGripTop;
    internal float? TriggerGripTop{get{FindGripBars();return triggerGripTop;}}
    private void FindGripBars()
    {
        if(gripBarsSearched)return;gripBarsSearched=true;
        if(Profile!="bazooka")return;
        try
        {
            foreach(var part in animatedParts)
            {
                var s=part.Snapshot;if(s==null||part.Mesh==null)continue;
                var bones=s.Bones;int rear=-1,front=-1,mid=-1,trigger=-1;
                for(int i=0;i<bones.Length;i++){var n=bones[i]!=null?bones[i].name:null;if(rear<0&&BazookaTubeMath.GripBone(n,false))rear=i;if(front<0&&BazookaTubeMath.GripBone(n,true))front=i;if(mid<0&&BazookaTubeMath.TriggerGripBone(n))mid=i;if(trigger<0&&BazookaTubeMath.TriggerBone(n))trigger=i;}
                if(rear<0&&front<0&&mid<0)continue;
                var weights=s.Original.boneWeights;var vertices=part.Mesh.vertices;
                if(weights.Length!=vertices.Length){Bootstrap.Warn("BAZOOKA grips: weights unavailable");return;}
                static int Dominant(BoneWeight w)=>w.weight0>=w.weight1&&w.weight0>=w.weight2&&w.weight0>=w.weight3?w.boneIndex0:w.weight1>=w.weight2&&w.weight1>=w.weight3?w.boneIndex1:w.weight2>=w.weight3?w.boneIndex2:w.boneIndex3;
                var map=fitMatrix*part.Matrix;var r=new List<System.Numerics.Vector3>();var f=new List<System.Numerics.Vector3>();var m=new List<System.Numerics.Vector3>();
                var tSum=Vector3.zero;int tCount=0;
                for(int i=0;i<vertices.Length;i++)
                {
                    int b=Dominant(weights[i]);
                    if(b==trigger&&trigger>=0){tSum+=map.MultiplyPoint3x4(vertices[i]);tCount++;continue;}
                    if(b!=rear&&b!=front&&b!=mid)continue;
                    var p=map.MultiplyPoint3x4(vertices[i]);(b==rear?r:b==front?f:m).Add(new System.Numerics.Vector3(p.x,p.y,p.z));
                }
                triggerPoint=tCount>=4?tSum/tCount:null;
                rearGripBar=BazookaTubeMath.GripBar(r.ToArray());frontGripBar=BazookaTubeMath.GripBar(f.ToArray(),FrontTrim);triggerGripBar=BazookaTubeMath.GripBar(m.ToArray(),BazookaTubeMath.TriggerTrim);
                if(triggerGripBar is GripBarMath.Bar handle)triggerGripTop=BazookaTubeMath.TopAlong(handle,m);
                static string Describe(GripBarMath.Bar? bar,int points)=>bar is GripBarMath.Bar b?"at "+b.Center.ToString("F3",null)+" axis "+b.Axis.ToString("F2",null)+" "+(b.Length*100).ToString("F0")+" x "+(b.Thickness*100).ToString("F1")+" cm ("+points+" points)":"not a handle ("+points+" points)";
                Bootstrap.Write("BAZOOKA grips: the handle with the trigger "+Describe(triggerGripBar,m.Count)+"; the front grip "+Describe(frontGripBar,f.Count)+"; the rear grip (shoulder rest) "+Describe(rearGripBar,r.Count)
                    +"; the trigger "+(triggerPoint is Vector3 tp?"at "+tp.ToString("F3")+" ("+tCount+" points)":"not found ("+tCount+" points)")
                    +(triggerGripTop is float top?"; the handle's top "+(top*100).ToString("F1")+" cm above its bar's middle":""));
                return;
            }
            Bootstrap.Warn("BAZOOKA grip bones not found; the game's own hold stays");
        }
        catch(Exception ex){rearGripBar=frontGripBar=triggerGripBar=null;Bootstrap.Warn("BAZOOKA grips: "+ex.Message);}
    }
}
