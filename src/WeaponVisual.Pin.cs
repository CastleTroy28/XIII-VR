using System;
using UnityEngine;
namespace XiiiXR;
// 0.1.129: the hand grenade's safety pin (its pin and ring bones). Once the
// pin is pulled it is gone from the grenade (collapsed in the live bake) and
// a mesh of the pin alone goes with the hand that pulled it.
internal sealed partial class WeaponVisual
{
    private Part? pinPart;private bool[]? pinMask;private bool pinSearched,pinPulled;
    private void FindPin()
    {
        if(pinSearched)return;pinSearched=true;
        if(Profile!="grenade")return;
        foreach(var part in animatedParts)
        {
            var s=part.Snapshot;if(s==null)continue;
            var mask=new bool[s.Bones.Length];int count=0;
            for(int i=0;i<mask.Length;i++)
            {
                var b=s.Bones[i];
                if(b!=null&&b.name.IndexOf("safetyPin",StringComparison.OrdinalIgnoreCase)>=0){mask[i]=true;count++;}
            }
            if(count==0)continue;
            pinPart=part;pinMask=mask;
            Bootstrap.Write("GRENADE pin: "+count+" bones on "+(part.Source!=null?part.Source.name:"?"));
            return;
        }
        Bootstrap.Warn("GRENADE pin bones not found; the pin stays drawn on the grenade");
    }
    internal bool HasPin{get{FindPin();return pinMask!=null;}}
    // 0.1.132: a still copy's slide/bolt: the part drawn with `source`, baked
    // with its mechanism closed (travel 0) or cycled back (travel > 0).
    internal float MechanismTravel=>MechanismMath.Travel(Profile);
    internal bool BakeStillMechanism(Mesh source,Mesh output,float travel)
    {
        foreach(var part in animatedParts)
        {
            if(part.Filter==null||part.Filter.sharedMesh!=source||part.Mechanism==null||part.Snapshot==null)continue;
            return part.Mechanism.BakeOwned(part.Snapshot,output,part.Matrix,fitScale,travel,false);
        }
        return false;
    }
    internal bool PinPulled
    {
        get=>pinPulled;
        set{FindPin();if(pinPart?.Snapshot==null||pinPulled==value)return;pinPulled=value;pinPart.Snapshot.Hidden=value?pinMask:null;}
    }
    // The part holding the pin (for a still copy to swap its mesh).
    internal Mesh? PinSourceMesh{get{FindPin();return pinPart?.Filter!=null?pinPart.Filter.sharedMesh:null;}}
    // pinOnly: the pin alone; otherwise the grenade without its pin. world:
    // where that mesh is drawn now.
    // center: the middle of the pin bones in that mesh's space.
    internal bool BakePin(Mesh output,bool pinOnly,out Material[] materials,out Matrix4x4 world,out Vector3 center)
    {
        FindPin();materials=Array.Empty<Material>();world=Matrix4x4.identity;center=Vector3.zero;
        if(pinPart?.Snapshot==null||pinMask==null||pinPart.Copy==null)return false;
        pinPart.Snapshot.BakeMasked(output,pinMask,pinOnly);
        var live=pinPart.Snapshot.Live;int n=0;
        for(int i=0;i<pinMask.Length&&i<live.Length;i++)if(pinMask[i]){center+=(Vector3)live[i].GetColumn(3);n++;}
        if(n>0)center/=n;
        materials=pinPart.Source!=null?pinPart.Source.sharedMaterials:Array.Empty<Material>();
        world=pinPart.Copy.localToWorldMatrix;
        return true;
    }
}
