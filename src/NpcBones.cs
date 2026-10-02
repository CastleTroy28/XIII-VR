using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using PlayMagic.AI;
using UnityEngine;
namespace XiiiXR;
// 0.1.142: an NPC's bones for the hit reactions and the gun grab: from the
// game's AIRigReference (on the NPC, or anywhere under it), else found by
// their names (NpcBoneNames), else from a humanoid Animator.
internal sealed class NpcBones
{
    internal Transform? spineLower,spineMedium,spineTop,neckBase,neckTop,head,jaw,chin,leftShoulder,rightShoulder,leftElbow,rightElbow,leftWrist,rightWrist,leftHip,rightHip,leftKnee,rightKnee;
    internal string Source="";
    // 0.1.147: the NPC's skinned meshes (drawn by NpcSkinOverlay while it reacts).
    internal List<SkinnedMeshRenderer> Skins=new();
    internal bool Usable=>spineTop!=null&&(head!=null||neckBase!=null);
    // The head, or the top of the neck when the head bone was not found.
    internal Transform Head=>head!=null?head:neckTop!=null?neckTop:neckBase!;
    internal int Found{get{int n=0;foreach(var t in new[]{spineLower,spineMedium,spineTop,neckBase,neckTop,head,leftShoulder,rightShoulder,leftElbow,rightElbow,leftWrist,rightWrist,leftHip,rightHip,leftKnee,rightKnee})if(t!=null)n++;return n;}}
    internal static NpcBones? Of(NPC npc,out string why)
    {
        why="";
        AIRigReference? rig=null;
        try{rig=npc.RigReference;}catch(Exception){}
        if(rig==null)try{rig=npc.GetComponentInChildren(Il2CppType.Of<AIRigReference>(),true)?.TryCast<AIRigReference>();}catch(Exception){}
        if(rig==null)try{var root=npc.transform.parent;if(root!=null)rig=root.GetComponentInChildren(Il2CppType.Of<AIRigReference>(),true)?.TryCast<AIRigReference>();}catch(Exception){}
        if(rig!=null&&rig.spineTop!=null&&rig.head!=null)
        {
            return new NpcBones{Source="the game's rig reference",spineLower=rig.spineLower,spineMedium=rig.spineMedium,spineTop=rig.spineTop,neckBase=rig.neckBase,neckTop=rig.neckTop,head=rig.head,
                jaw=rig.jaw,chin=rig.chin,leftShoulder=rig.leftShoulder,rightShoulder=rig.rightShoulder,leftElbow=rig.leftElbow,rightElbow=rig.rightElbow,
                leftWrist=rig.leftWrist,rightWrist=rig.rightWrist,leftHip=rig.leftHip,rightHip=rig.rightHip,leftKnee=rig.leftKnee,rightKnee=rig.rightKnee};
        }
        why=rig==null?"no rig reference":"its rig reference has no spine/head";
        // 0.1.147: the bones the NPC's
        // skinned meshes are drawn with come first - the reactions must be
        // on exactly those; then every transform under the NPC by name.
        var skins=new List<SkinnedMeshRenderer>();
        try
        {
            var skinBones=new List<Transform>();var seen=new HashSet<IntPtr>();
            foreach(var o in npc.GetComponentsInChildren(Il2CppType.Of<SkinnedMeshRenderer>(),true))
            {
                var smr=o.TryCast<SkinnedMeshRenderer>();if(smr==null)continue;skins.Add(smr);
                var bones=smr.bones;if(bones==null)continue;
                foreach(var t in bones)if(t!=null&&seen.Add(t.Pointer))skinBones.Add(t);
            }
            if(skinBones.Count>=8)
            {
                var b=ByName(npc,skinBones,"the skinned meshes' bones ("+skinBones.Count+" bones of "+skins.Count+" meshes)",ref why);
                if(b!=null){b.Skins=skins;return b;}
            }
            else why+="; the skinned meshes ("+skins.Count+") list "+skinBones.Count+" bones";
        }
        catch(Exception ex){why+="; skinned meshes failed ("+ex.Message+")";}
        try
        {
            var transforms=new List<Transform>();
            foreach(var o in npc.transform.GetComponentsInChildren(Il2CppType.Of<Transform>(),true)){var t=o.TryCast<Transform>();if(t!=null)transforms.Add(t);}
            var b=ByName(npc,transforms,"bones by name ("+transforms.Count+" transforms)",ref why);
            if(b!=null){b.Skins=skins;return b;}
        }
        catch(Exception ex){why+="; by name failed ("+ex.Message+")";}
        try
        {
            var anim=npc.CharacterAnimator;
            if(anim!=null&&anim.isHuman)
            {
                Transform? H(HumanBodyBones b){try{return anim.GetBoneTransform(b);}catch(Exception){return null;}}
                var b=new NpcBones{Source="the humanoid animator",spineLower=H(HumanBodyBones.Spine),spineMedium=H(HumanBodyBones.Chest),spineTop=H(HumanBodyBones.UpperChest)??H(HumanBodyBones.Chest),
                    neckBase=H(HumanBodyBones.Neck),head=H(HumanBodyBones.Head),jaw=H(HumanBodyBones.Jaw),leftShoulder=H(HumanBodyBones.LeftUpperArm),rightShoulder=H(HumanBodyBones.RightUpperArm),
                    leftElbow=H(HumanBodyBones.LeftLowerArm),rightElbow=H(HumanBodyBones.RightLowerArm),leftWrist=H(HumanBodyBones.LeftHand),rightWrist=H(HumanBodyBones.RightHand),
                    leftHip=H(HumanBodyBones.LeftUpperLeg),rightHip=H(HumanBodyBones.RightUpperLeg),leftKnee=H(HumanBodyBones.LeftLowerLeg),rightKnee=H(HumanBodyBones.RightLowerLeg)};
                if(b.Usable){b.Skins=skins;return b;}
            }
            why+="; animator "+(anim==null?"none":anim.isHuman?"humanoid without spine/head":"not humanoid");
        }
        catch(Exception ex){why+="; animator failed ("+ex.Message+")";}
        return null;
    }
    // Roles by name among these transforms (depth: under the NPC).
    private static NpcBones? ByName(NPC npc,List<Transform> transforms,string label,ref string why)
    {
        var list=new List<(string name,int depth)>();var start=npc.transform;
        foreach(var t in transforms){int depth=0;for(var p=t;p!=null&&p!=start&&depth<64;p=p.parent)depth++;list.Add((t.name,depth));}
        var found=NpcBoneNames.Match(list);
        Transform? T(NpcBoneNames.Role r)=>found[(int)r]>=0?transforms[found[(int)r]]:null;
        if(!NpcBoneNames.Usable(found))
        {
            why+="; "+label+": spine="+(T(NpcBoneNames.Role.SpineTop)?.name??"none")+" head="+(T(NpcBoneNames.Role.Head)?.name??"none")+" neck="+(T(NpcBoneNames.Role.NeckBase)?.name??"none");
            return null;
        }
        // 0.1.145: the head by name was not found on the NPCs (only
        // the neck). 0.1.146: their skeleton is Neck_01 - Neck_Top - Head_Top
        // (the end): the top neck joint carries the skull (the 0.1.145 guess,
        // the neck's child with the most under it, took a sound effect). A
        // child of the neck is taken only if it is a joint (its name says so).
        string headNote="";
        var headT=T(NpcBoneNames.Role.Head);
        var neckBase=T(NpcBoneNames.Role.NeckBase);var neckTop=T(NpcBoneNames.Role.NeckTop);
        if(headT==null&&neckTop!=null&&neckBase!=null&&neckTop!=neckBase){headT=neckTop;headNote="; head = the top neck joint "+neckTop.name;}
        if(headT==null)
        {
            var neck=neckTop??neckBase;
            if(neck!=null)
            {
                Transform? best=null;int most=-1;
                for(int i=0;i<neck.childCount;i++)
                {
                    var c=neck.GetChild(i);var k=NpcBoneNames.Key(c.name);var raw=c.name.ToLowerInvariant();
                    if(NpcBoneNames.Side(k)!=0||!NpcBoneNames.JointName(raw)||k.Contains("anchor")||k.Contains("armor")||k.Contains("clavicle")||k.Contains("vfx")||k.Contains("sound"))continue;
                    int n=c.GetComponentsInChildren(Il2CppType.Of<Transform>(),true).Length;
                    if(n>most){most=n;best=c;}
                }
                if(best!=null){headT=best;headNote="; head = the neck's child joint "+best.name;}
            }
        }
        if(headT==null||headNote.Length>0)
        {
            var names=new List<string>();
            foreach(var t in transforms){var k=t.name.ToLowerInvariant();if(k.Contains("head")||k.Contains("skull")||k.Contains("neck"))names.Add(t.name);if(names.Count>=12)break;}
            headNote+=" (head-like names: "+string.Join(",",names)+")";
        }
        return new NpcBones{Source=label+headNote,head=headT,spineLower=T(NpcBoneNames.Role.SpineLower),spineMedium=T(NpcBoneNames.Role.SpineMedium),spineTop=T(NpcBoneNames.Role.SpineTop),
            neckBase=neckBase,neckTop=neckTop,jaw=T(NpcBoneNames.Role.Jaw),chin=T(NpcBoneNames.Role.Chin),
            leftShoulder=T(NpcBoneNames.Role.ShoulderL),rightShoulder=T(NpcBoneNames.Role.ShoulderR),leftElbow=T(NpcBoneNames.Role.ElbowL),rightElbow=T(NpcBoneNames.Role.ElbowR),
            leftWrist=T(NpcBoneNames.Role.WristL),rightWrist=T(NpcBoneNames.Role.WristR),leftHip=T(NpcBoneNames.Role.HipL),rightHip=T(NpcBoneNames.Role.HipR),
            leftKnee=T(NpcBoneNames.Role.KneeL),rightKnee=T(NpcBoneNames.Role.KneeR)};
    }
}
