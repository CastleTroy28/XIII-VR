using System;
using UnityEngine;
namespace XiiiXR;
// 0.1.198: the
// hook is held by its handle (its rubber grip, GripBarMath) the way a pistol
// is held by its grip - the hand as on the pistol, the bar along the pistol
// grip's line, deep in the closed fingers, the rest of the hook pointing
// forward from the fist. The left hand's hold is the right hand's mirrored.
internal sealed partial class WeaponHands
{
    // The closed hand's grip point shift, deeper into the curled fingers (settings).
    internal static Vector3 FistShift
    {
        get
        {
            int forward=QualityOptions.BarrelGripForward?.Value??ClubMath.DefaultForwardMm,up=QualityOptions.BarrelGripUp?.Value??ClubMath.DefaultUpMm;
            var d=ClubMath.Deeper(forward,up);return new Vector3(d.X,d.Y,d.Z);
        }
    }
    // 0.1.200: the zipline hook's handle shifted on top of that (settings).
    internal static Vector3 ToolShift
    {
        get
        {
            int forward=QualityOptions.ZiplineGripForward?.Value??ClubMath.ZiplineForwardMm,up=QualityOptions.ZiplineGripUp?.Value??ClubMath.ZiplineUpMm;
            var d=ClubMath.Deeper(forward,up);return new Vector3(d.X,d.Y,d.Z);
        }
    }
    // A hand's hold of a pistol (its turn on the gun, the right hand's), kept from playing.
    private static bool TryPistolHold(out Quaternion hold)
    {
        hold=Quaternion.identity;
        foreach(var key in new[]{"pistol","revolver","uzi"})if(handleGrips.TryGetValue(key,out var g)){hold=g.rotation;return true;}
        foreach(var entry in handleGrips)if(HolsterLayout.Firearm(entry.Key)){hold=entry.Value.rotation;return true;}
        return false;
    }
    // Hand `right` holding the zipline hook: where the hand is (at the
    // controller, turned as on a pistol) and the hook in that hand's frame.
    internal bool TryToolHold(bool right,PoseValue pose,GripBarMath.Bar bar,out Vector3 handAt,out Quaternion hand,out Vector3 itemAt,out Quaternion itemTurn)
    {
        handAt=itemAt=Vector3.zero;hand=itemTurn=Quaternion.identity;
        if(!TryPistolHold(out var hold))return false;
        if(!right)hold=MirrorQ(hold);
        hand=HandAim(pose,right)*hold;handAt=CameraRig.UnityPosition(pose);
        var contact=LongHandleContact(right?rightNative:leftNative,1)+FistShift+ToolShift;
        var inverse=Quaternion.Inverse(hold);
        var g=inverse*ToU(ClubMath.GripLine);var f=inverse*ToU(ClubMath.GripForward);
        var placed=GripBarMath.Hold(bar,ToN(g),ToN(f),ToN(contact));
        itemTurn=new Quaternion(placed.rotation.X,placed.rotation.Y,placed.rotation.Z,placed.rotation.W);itemAt=new Vector3(placed.position.X,placed.position.Y,placed.position.Z);
        return true;
    }
    private static Vector3 ToU(System.Numerics.Vector3 v)=>new(v.X,v.Y,v.Z);
}
