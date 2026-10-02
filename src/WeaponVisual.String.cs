using System;
using System.Collections.Generic;
using UnityEngine;
namespace XiiiXR;
// 0.1.123: the crossbow's string moves with the reload. Its cocked shape is
// taken from a loaded crossbow (like the bolt's place on the rail, and kept
// in the config folder for a crossbow taken empty). Released (after a shot,
// while empty) it lies straight across the bow at the cams; the bolt drawn
// back along the rail carries it back until it is cocked again.
internal sealed partial class WeaponVisual
{
    private static string StringFile=>System.IO.Path.Combine(BepInEx.Paths.ConfigPath,"XIII-XR-crossbow-string.txt");
    private int[]? stringBones;private Matrix4x4[]? stringRelation,stringPose;private Vector3[]? stringRelease;
    private float stringNock,stringTip,stringShown=1,stringFrameAt;
    // The draw wanted this frame (0 released .. 1 cocked), set by the hands.
    internal float StringDraw=1;
    internal bool HasString=>stringBones!=null;
    // The draw that goes with the held bolt's rear this far ahead of the nock.
    internal float StringDrawFor(float railOffset)=>CrossbowStringMath.Draw(ArrowRear.z+railOffset,stringNock,stringTip);
    private void PrepareString(Part part,string?[] boneNames,int reference)
    {
        stringBones=null;
        var snapshot=part.Snapshot;if(snapshot==null)return;
        var list=new List<int>();
        for(int i=0;i<boneNames.Length;i++){var n=boneNames[i];if(n!=null&&n.ToLowerInvariant().Contains("string"))list.Add(i);}
        if(list.Count==0){Bootstrap.Write("CROSSBOW string: no string bones; it stays the game's");return;}
        var bones=list.ToArray();var names=new string[bones.Length];for(int k=0;k<names.Length;k++)names[k]=boneNames[bones[k]]??"";
        // Where the cocked string's bones lie along the gun (fitted z).
        var basis=fitMatrix*part.Matrix*(snapshot.Initial??snapshot.Live)[reference];
        float[]? Along(Matrix4x4[]? r,out float nockAt,out float tipAt)
        {
            nockAt=float.PositiveInfinity;tipAt=float.NegativeInfinity;if(r==null||r.Length!=bones.Length)return null;
            var at=new float[bones.Length];
            for(int k=0;k<bones.Length;k++)
            {
                var p=basis.MultiplyPoint3x4(r[k].GetColumn(3));
                if(!Finite(p))return null;
                at[k]=p.z;tipAt=Math.Max(tipAt,p.z);nockAt=Math.Min(nockAt,p.z);
            }
            return at;
        }
        // 0.1.162: the shape seen on a crossbow taken loaded only when its string is cocked (drawn MinDraw or more).
        var seen=LoadedAtBuild?snapshot.LoadedRelation(reference,bones):null;
        var seenZ=Along(seen,out float seenNock,out float seenTip);
        bool seenCocked=seenZ!=null&&CrossbowStringMath.Cocked(seenNock,seenTip);
        Matrix4x4[] relation;float[] z;float tip,nock;string from;
        if(seenCocked){relation=seen!;z=seenZ!;tip=seenTip;nock=seenNock;from="the loaded crossbow when taken";SaveRelation(StringFile,"string",names,seen!);}
        else
        {
            var saved=LoadRelation(StringFile,"string",names);var savedZ=Along(saved,out float savedNock,out float savedTip);
            if(seenZ!=null)Bootstrap.Write("CROSSBOW string: the crossbow was taken loaded but its string was not cocked (drawn "+(seenTip-seenNock).ToString("F3")+" m): not kept"+(savedZ!=null?"; the saved shape is used":""));
            if(savedZ==null||!CrossbowStringMath.Cocked(savedNock,savedTip))
            {Bootstrap.Write(saved==null?"CROSSBOW string: no cocked shape seen yet (crossbow taken empty); it stays the game's until a loaded one is taken":"CROSSBOW string: the saved shape is not cocked (drawn "+(savedTip-savedNock).ToString("F3")+" m); it stays the game's until a loaded one is taken");return;}
            relation=saved!;z=savedZ;tip=savedTip;nock=savedNock;from="the saved shape";
        }
        // The move of each bone, released, in the reference bone's frame.
        var inverse=basis.inverse;var release=new Vector3[bones.Length];
        for(int k=0;k<bones.Length;k++)release[k]=inverse.MultiplyVector(Vector3.forward*CrossbowStringMath.Shift(z[k],tip,0));
        stringBones=bones;stringRelation=relation;stringRelease=release;stringPose=new Matrix4x4[bones.Length];
        stringNock=nock;stringTip=tip;stringShown=StringDraw;stringFrameAt=Time.realtimeSinceStartup;
        Bootstrap.Write("CROSSBOW string bones="+bones.Length+" cocked shape from "+from+"; nock z="+nock.ToString("F3")+" released at z="+tip.ToString("F3")+" (draw "+(tip-nock).ToString("F3")+" m) boltRear z="+ArrowRear.z.ToString("F3"));
    }
    // The string's relations for the shown draw (null: the game's own string).
    private Matrix4x4[]? StringPose()
    {
        if(stringBones==null||stringRelation==null||stringRelease==null||stringPose==null)return null;
        float now=Time.realtimeSinceStartup;
        stringShown=CrossbowStringMath.Follow(stringShown,StringDraw,now-stringFrameAt);stringFrameAt=now;
        float open=1-stringShown;
        for(int k=0;k<stringPose.Length;k++)
            stringPose[k]=open>1e-4f?Matrix4x4.Translate(stringRelease[k]*open)*stringRelation[k]:stringRelation[k];
        return stringPose;
    }
}
