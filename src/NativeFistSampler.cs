using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
// Sample native clip curves on plain owned Transforms, never on the player.
// No Animator, StateMachineBehaviour, event receiver or weapon component is cloned.
internal static class NativeFistSampler
{
    internal static void Capture(Transform player,SkinnedMeshRenderer source,NativeSkinSnapshot skin,
        Matrix4x4 restFrame,Matrix4x4 restWrist,int wrist,FingerPoseMath fingers)
    {
        GameObject? root=null;
        try
        {
            var control=player.GetComponentInChildren(Il2CppType.Of<PlayerArmsAnimationControl>(),true)?.TryCast<PlayerArmsAnimationControl>();
            var animator=control?.animator;if(animator==null||animator.runtimeAnimatorController==null)return;
            var clips=animator.runtimeAnimatorController.animationClips.Where(c=>c!=null &&
                c.legacy && (c.name.Contains("fist",StringComparison.OrdinalIgnoreCase)||c.name.Contains("punch",StringComparison.OrdinalIgnoreCase)))
                .OrderBy(c=>c.name.Contains("idle",StringComparison.OrdinalIgnoreCase)?0:1).Take(16).ToArray();
            Bootstrap.Write("NATIVE FIST clips="+string.Join(",",clips.Select(c=>c.name)));
            if(clips.Length==0)return;
            var originals=new List<Transform>();var owned=new List<Transform>();var map=new Dictionary<int,Transform>();
            root=new GameObject("XIII private fist clip sampling");map[animator.transform.GetInstanceID()]=root.transform;
            Transform Copy(Transform original)
            {
                if(map.TryGetValue(original.GetInstanceID(),out var copy))return copy;
                if(original.parent==null||!original.IsChildOf(animator.transform))throw new InvalidOperationException("Animation bone outside arms rig");
                var parent=Copy(original.parent);var go=new GameObject(original.name);copy=go.transform;copy.SetParent(parent,false);
                copy.localPosition=original.localPosition;copy.localRotation=original.localRotation;copy.localScale=original.localScale;
                map[original.GetInstanceID()]=copy;originals.Add(original);owned.Add(copy);return copy;
            }
            var bones=skin.Bones.Select(Copy).ToArray();var renderer=Copy(source.transform);
            var positions=originals.Select(t=>t.localPosition).ToArray();var rotations=originals.Select(t=>t.localRotation).ToArray();var scales=originals.Select(t=>t.localScale).ToArray();
            foreach(var clip in clips)foreach(float fraction in new[]{.35f,.65f,.1f})
            {
                for(int i=0;i<owned.Count;i++){owned[i].localPosition=positions[i];owned[i].localRotation=rotations[i];owned[i].localScale=scales[i];}
                root.transform.localPosition=Vector3.zero;root.transform.localRotation=Quaternion.identity;root.transform.localScale=Vector3.one;
                clip.SampleAnimation(root,clip.length*fraction);
                var r=renderer.worldToLocalMatrix;var live=bones.Select(t=>r*t.localToWorldMatrix).ToArray();
                var canonical=NativeHandVisual.U(NativeHandMath.AnimatedFrame(NativeHandVisual.N(restFrame),NativeHandVisual.N(restWrist),NativeHandVisual.N(live[wrist])));
                if(fingers.Capture("fists",live.Select(b=>NativeHandVisual.N(canonical*b)).ToArray()))
                {Bootstrap.Write("NATIVE FIST captured clip="+clip.name+" sample="+fraction+" wrist="+skin.Bones[wrist].name);return;}
            }
            Bootstrap.Warn("NATIVE FIST clips did not produce a closed pose; waiting for native live fist pose.");
        }
        catch(Exception ex){Bootstrap.Warn("NATIVE FIST sample unavailable; hand remains active: "+ex.Message);}
        finally{if(root!=null)UnityEngine.Object.Destroy(root);}
    }
}
