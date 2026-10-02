using System;
using System.Collections.Generic;
namespace XiiiXR;
// 0.1.142: the bones of an NPC found by their names when the game's
// AIRigReference is not set on it. The game's
// skeletons are named like the player's: ROOTSHJnt, Spine_02SHJnt,
// Spine_TopSHJnt, Neck_01SHJnt, L_Arm_ShoulderSHJnt, R_Arm_ElbowSHJnt,
// L_Arm_WristSHJnt, R_Leg_HipSHJnt, L_Leg_KneeSHJnt ...; the usual other
// names (Spine1, LeftUpperArm, LeftForeArm, Head ...) are matched too.
internal static class NpcBoneNames
{
    internal enum Role{SpineLower,SpineMedium,SpineTop,NeckBase,NeckTop,Head,Jaw,Chin,ShoulderL,ShoulderR,ElbowL,ElbowR,WristL,WristR,HipL,HipR,KneeL,KneeR}
    internal static readonly int Roles=Enum.GetValues(typeof(Role)).Length;
    internal static string Key(string name)
    {
        var k=(name??"").ToLowerInvariant().Replace("(clone)","").Replace(" ","_").Replace("-","_").Replace(":","_");
        foreach(var suffix in new[]{"shjnt","_jnt","jnt","_bnd","_bone"})if(k.EndsWith(suffix,StringComparison.Ordinal))k=k.Substring(0,k.Length-suffix.Length);
        return k.Trim('_');
    }
    // Not bones of the body (anchors, armour, IK targets, sockets, ends).
    private static bool Skip(string k)=>k.Length==0||k.Contains("anchor")||k.Contains("armor")||k.Contains("armour")||k.Contains("socket")||k.Contains("target")
        ||k.Contains("collider")||k.Contains("mesh")||k.Contains("geo")||k.Contains("lod")||k.Contains("twist")||k.Contains("roll")||k.EndsWith("end",StringComparison.Ordinal)
        ||k.EndsWith("tip",StringComparison.Ordinal)||k.StartsWith("ik",StringComparison.Ordinal)||k.Contains("_ik")||k.Contains("helper")||k.Contains("weapon")||k.Contains("wpn");
    // -1 left, 1 right, 0 neither.
    internal static int Side(string k)
    {
        if(k.StartsWith("l_",StringComparison.Ordinal)||k.StartsWith("left",StringComparison.Ordinal)||k.Contains("_l_")||k.EndsWith("_l",StringComparison.Ordinal))return -1;
        if(k.StartsWith("r_",StringComparison.Ordinal)||k.StartsWith("right",StringComparison.Ordinal)||k.Contains("_r_")||k.EndsWith("_r",StringComparison.Ordinal))return 1;
        return 0;
    }
    private static bool Any(string k,params string[] words){foreach(var w in words)if(k.Contains(w))return true;return false;}
    // Index of each role's bone in the list (-1: not found). depth: the
    // bone's depth under the NPC; shjnt: its name is a skinned joint.
    internal static int[] Match(IReadOnlyList<(string name,int depth)> bones)
    {
        var found=new int[Roles];for(int i=0;i<found.Length;i++)found[i]=-1;
        var keys=new string[bones.Count];
        for(int i=0;i<bones.Count;i++)keys[i]=Key(bones[i].name);
        // Better of two candidates: a skinned joint, then the shallower one.
        bool Better(int a,int b)
        {
            if(b<0)return true;
            bool ja=(bones[a].name??"").ToLowerInvariant().EndsWith("shjnt",StringComparison.Ordinal),jb=(bones[b].name??"").ToLowerInvariant().EndsWith("shjnt",StringComparison.Ordinal);
            if(ja!=jb)return ja;
            return bones[a].depth<bones[b].depth;
        }
        void Take(Role r,int i){if(Better(i,found[(int)r]))found[(int)r]=i;}
        var spines=new List<int>();var necks=new List<int>();
        for(int i=0;i<keys.Length;i++)
        {
            var k=keys[i];if(Skip(k))continue;int side=Side(k);
            if(side==0)
            {
                if(k.StartsWith("spine",StringComparison.Ordinal)||k=="chest"||k=="upperchest"||k=="upper_chest")spines.Add(i);
                else if(k.StartsWith("neck",StringComparison.Ordinal))necks.Add(i);
                else if(k.StartsWith("head",StringComparison.Ordinal)&&!Any(k,"top","light"))Take(Role.Head,i);
                else if(k.StartsWith("jaw",StringComparison.Ordinal))Take(Role.Jaw,i);
                else if(k.StartsWith("chin",StringComparison.Ordinal))Take(Role.Chin,i);
                continue;
            }
            bool left=side<0;
            if(Any(k,"finger","thumb","index","middle","ring","pinky","toe","foot","ankle","clavicle","collar","eye","brow","lip","cheek"))continue;
            // Without its side: l_arm_elbow -> arm_elbow, leftforearm -> forearm.
            var b=k;foreach(var p in new[]{"left_","right_","left","right","l_","r_"})if(b.StartsWith(p,StringComparison.Ordinal)){b=b.Substring(p.Length);break;}
            if(b.EndsWith("_l",StringComparison.Ordinal)||b.EndsWith("_r",StringComparison.Ordinal))b=b.Substring(0,b.Length-2);
            if(Any(b,"arm_shoulder","upperarm","upper_arm")||b=="shoulder"||b=="arm")Take(left?Role.ShoulderL:Role.ShoulderR,i);
            else if(Any(b,"elbow","forearm","fore_arm","lowerarm","lower_arm"))Take(left?Role.ElbowL:Role.ElbowR,i);
            else if(Any(b,"wrist")||b=="hand")Take(left?Role.WristL:Role.WristR,i);
            else if(Any(b,"leg_hip","thigh","upleg","up_leg","upperleg","upper_leg")||b=="hip")Take(left?Role.HipL:Role.HipR,i);
            else if(Any(b,"knee","calf","lowerleg","lower_leg")||b=="leg")Take(left?Role.KneeL:Role.KneeR,i);
        }
        // The spine from the hips up: the lowest, the middle, the top one.
        spines.Sort((a,b)=>bones[a].depth!=bones[b].depth?bones[a].depth.CompareTo(bones[b].depth):string.CompareOrdinal(keys[a],keys[b]));
        spines=Distinct(spines,keys);
        if(spines.Count>0)
        {
            int at=spines.FindLastIndex(i=>keys[i].Contains("top"));int top=at>=0?spines[at]:spines[spines.Count-1];
            found[(int)Role.SpineTop]=top;
            if(spines[0]!=top)found[(int)Role.SpineLower]=spines[0];
            var middle=spines.FindAll(i=>i!=top&&i!=spines[0]);
            if(middle.Count>0)found[(int)Role.SpineMedium]=middle[middle.Count/2];
        }
        necks.Sort((a,b)=>bones[a].depth!=bones[b].depth?bones[a].depth.CompareTo(bones[b].depth):string.CompareOrdinal(keys[a],keys[b]));
        necks=Distinct(necks,keys);
        if(necks.Count>0){found[(int)Role.NeckBase]=necks[0];if(necks.Count>1)found[(int)Role.NeckTop]=necks[necks.Count-1];}
        return found;
    }
    // One bone per name (a skinned joint kept over a duplicate of it).
    private static List<int> Distinct(List<int> list,string[] keys)
    {
        var seen=new HashSet<string>();var result=new List<int>();
        foreach(var i in list)if(seen.Add(keys[i]))result.Add(i);
        return result;
    }
    // 0.1.146: a skeleton joint by its name (not an effect, a mesh or an anchor).
    internal static bool JointName(string lower)=>lower.Contains("jnt")||lower.Contains("joint")||lower.Contains("bone")||lower.Contains("bnd")||lower.Contains("head");
    // Enough for the reaction: the upper spine and the head (or the neck).
    internal static bool Usable(int[] found)=>found[(int)Role.SpineTop]>=0&&(found[(int)Role.Head]>=0||found[(int)Role.NeckBase]>=0);
}
