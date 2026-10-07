using System;
using System.Collections.Generic;
using UnityEngine;
namespace XiiiXR;
// 0.1.123: the crossbow's string moves with the reload. Its cocked shape is
// taken from a loaded crossbow (like the bolt's place on the rail, and kept
// in the config folder for a crossbow taken empty). Released (after a shot,
// while empty) it lies straight across the bow at the cams; the bolt drawn
// back along the rail carries it back until it is cocked again.
// 0.1.251: the harpoon gun's two bands (its "line" bones) the same way:
// stretched to the harpoon's notch when it is loaded (their shape on a
// loaded harpoon gun), drawn back with the harpoon slid in, and let go
// (a third of their length, at the muzzle) once it is shot. They stayed
// the game's, slack after a harpoon was put in by hand.
internal sealed partial class WeaponVisual
{
    // The crossbow's file as before; each other crossbow its own (the tactical one's string, the harpoon gun's bands).
    private string StringFile=>ModelFile("string");
    private int[]? stringBones;private Matrix4x4[]? stringRelation,stringPose;private Vector3[]? stringRelease;
    private float stringNock,stringTip,stringShown=1,stringFrameAt;private bool stringBands;
    // The draw wanted this frame (0 released .. 1 cocked), set by the hands.
    internal float StringDraw=1;
    internal bool HasString=>stringBones!=null;
    // The draw that goes with the held bolt's rear this far ahead of the nock
    // (the bands: drawn back as far as the harpoon is slid in).
    internal float StringDrawFor(float railOffset)=>CrossbowStringMath.Draw((stringBands?stringNock:ArrowRear.z)+railOffset,stringNock,stringTip);
    private void PrepareString(Part part,string?[] boneNames,int reference)
    {
        stringBones=null;stringBands=false;
        var snapshot=part.Snapshot;if(snapshot==null)return;
        var list=new List<int>();bool bands=false;
        for(int i=0;i<boneNames.Length;i++){var n=boneNames[i];if(n!=null&&CrossbowStringMath.StringBone(n,out bool band)){list.Add(i);bands|=band;}}
        if(list.Count==0){Bootstrap.Write("CROSSBOW string: no string bones; it stays the game's");return;}
        string what=bands?"bands":"string";
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
        if(seenCocked){relation=seen!;z=seenZ!;tip=seenTip;nock=seenNock;from="the loaded "+ModelKey+" when taken";SaveRelation(StringFile,what,names,seen!);}
        else
        {
            var saved=LoadRelation(StringFile,what,names);var savedZ=Along(saved,out float savedNock,out float savedTip);
            if(seenZ!=null)Bootstrap.Write("CROSSBOW "+what+": the "+ModelKey+" was taken loaded but its "+what+" not drawn (drawn "+(seenTip-seenNock).ToString("F3")+" m): not kept"+(savedZ!=null?"; the saved shape is used":""));
            if(savedZ==null||!CrossbowStringMath.Cocked(savedNock,savedTip))
            {Bootstrap.Write(saved==null?"CROSSBOW "+what+": no drawn shape seen yet ("+ModelKey+" taken empty); it stays the game's until a loaded one is taken":"CROSSBOW "+what+": the saved shape is not drawn (drawn "+(savedTip-savedNock).ToString("F3")+" m); it stays the game's until a loaded one is taken");return;}
            relation=saved!;z=savedZ;tip=savedTip;nock=savedNock;from="the saved shape";
        }
        // The move of each bone, released, in the reference bone's frame.
        float rest=bands?CrossbowStringMath.BandRest:0;
        var inverse=basis.inverse;var release=new Vector3[bones.Length];
        for(int k=0;k<bones.Length;k++)release[k]=inverse.MultiplyVector(Vector3.forward*CrossbowStringMath.Shift(z[k],tip,0,rest));
        stringBones=bones;stringRelation=relation;stringRelease=release;stringPose=new Matrix4x4[bones.Length];stringBands=bands;
        stringNock=nock;stringTip=tip;stringShown=StringDraw;stringFrameAt=Time.realtimeSinceStartup;
        Bootstrap.Write("CROSSBOW "+what+" bones="+bones.Length+" drawn shape from "+from+"; nock z="+nock.ToString("F3")+" released at z="+tip.ToString("F3")+(bands?" (let go to "+(rest*100).ToString("F0")+"% of their length)":"")+" (draw "+(tip-nock).ToString("F3")+" m) boltRear z="+ArrowRear.z.ToString("F3"));
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
