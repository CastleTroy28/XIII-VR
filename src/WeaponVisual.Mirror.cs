using System;
using System.Collections.Generic;
using UnityEngine;
namespace XiiiXR;
// 0.1.133: the weapon's middle across (fitted x): the middle of its
// left/right bone pairs (logged).
// 0.1.135: the left hand's hold is mirrored across the middle of the part
// the right hand holds (HandleMiddleX), measured from the weapon's own points
// around the palm of the game's right-hand hold.
internal sealed partial class WeaponVisual
{
    private float? mirrorCenter;
    internal float MirrorCenterX{get{mirrorCenter??=FindMirrorCenter();return mirrorCenter.Value;}}
    private float FindMirrorCenter()
    {
        try
        {
            var at=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
            var fit=GameWorldToFitted;
            foreach(var part in animatedParts)
            {
                var s=part.Snapshot;if(s==null)continue;
                foreach(var b in s.Bones)if(b!=null&&!at.ContainsKey(b.name))at[b.name]=fit.MultiplyPoint3x4(b.position).x;
            }
            var middles=new List<float>();
            foreach(var kv in at)
            {
                var other=HandMirror.PairName(kv.Key);
                if(other!=null&&at.TryGetValue(other,out float x))middles.Add((kv.Value+x)*.5f);
            }
            float c=HandMirror.Center(middles);
            if(middles.Count>0)Bootstrap.Write("WEAPON MIRROR "+Profile+" middle x="+c.ToString("F4")+" from "+middles.Count+" left/right bone pairs");
            return c;
        }
        catch(Exception ex){Bootstrap.Warn("WEAPON MIRROR "+Profile+": "+ex.Message);return 0;}
    }
    // 0.1.140: the weapon's own roll about the barrel from its left/right
    // bone pairs (fitted frame), NaN without pairs; logged.
    private float? pairRoll;private int pairRollCount;
    internal float PairRollDegrees{get{pairRoll??=FindPairRoll();return pairRoll.Value;}}
    private float FindPairRoll()
    {
        try
        {
            var at=new Dictionary<string,Vector3>(StringComparer.OrdinalIgnoreCase);
            var fit=GameWorldToFitted;
            foreach(var part in animatedParts)
            {
                var s=part.Snapshot;if(s==null)continue;
                foreach(var b in s.Bones)if(b!=null&&!at.ContainsKey(b.name))at[b.name]=fit.MultiplyPoint3x4(b.position);
            }
            var pairs=new List<(System.Numerics.Vector3,System.Numerics.Vector3)>();
            foreach(var kv in at)
            {
                var other=HandMirror.PairName(kv.Key);
                if(other!=null&&at.TryGetValue(other,out var r))pairs.Add((new System.Numerics.Vector3(kv.Value.x,kv.Value.y,kv.Value.z),new System.Numerics.Vector3(r.x,r.y,r.z)));
            }
            pairRollCount=pairs.Count;
            return HandMirror.PairRoll(pairs);
        }
        catch(Exception ex){Bootstrap.Warn("WEAPON ROLL "+Profile+": "+ex.Message);return float.NaN;}
    }
    // grip, turn, size: the right hand's native hold (its root in the fitted
    // frame, its rotation, its scale). The palm is on the hand's -y side.
    // 0.1.200: the barrel's thickness at a point on its line (fitted frame; ClubMath.Thickness).
    internal float BarrelThickness(Vector3 at,out int count)
    {
        var points=new List<System.Numerics.Vector3>();
        foreach(var part in animatedParts)
        {
            var mesh=part.Mesh;if(mesh==null)continue;var m=fitMatrix*part.Matrix;
            foreach(var v in mesh.vertices){var p=m.MultiplyPoint3x4(v);if(Math.Abs(p.z-at.z)<=ClubMath.SliceHalf)points.Add(new System.Numerics.Vector3(p.x,p.y,p.z));}
        }
        count=points.Count;
        return ClubMath.Thickness(points,new System.Numerics.Vector3(at.x,at.y,at.z));
    }
    internal float HandleMiddleX(Vector3 grip,Quaternion turn,float size,out int count)
    {
        count=0;
        var probe=grip+turn*(new Vector3(0,-.03f,.01f)*size);float r2=.045f*.045f;
        var xs=new List<float>();
        foreach(var part in animatedParts)
        {
            var mesh=part.Mesh;if(mesh==null)continue;var m=fitMatrix*part.Matrix;
            foreach(var v in mesh.vertices){var p=m.MultiplyPoint3x4(v);if((p-probe).sqrMagnitude<r2)xs.Add(p.x);}
        }
        count=xs.Count;
        return HandMirror.HandleMiddle(xs);
    }
}
