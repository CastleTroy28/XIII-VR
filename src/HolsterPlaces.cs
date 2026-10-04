using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
namespace XiiiXR;
// 0.1.234: the player's own weapon places (VR SETTINGS > Weapon places).
// Each place but the two on the back can be moved with the controller ray
// (HolsterPlaceEditor); how far it is moved is kept in the config
// ([WeaponPlaces], centimetres right, up, forward for a right-hander; a
// left-hander's places are the mirror, moved the mirror way). HolsterLayout
// adds the offset to the place's grip and to where the hand takes it.
internal static class HolsterPlaces
{
    internal static readonly HolsterSlot[] Movable={HolsterSlot.BeltRight,HolsterSlot.BeltLeft,HolsterSlot.ArmpitLeft,HolsterSlot.ArmpitRight,HolsterSlot.Belly,HolsterSlot.ChestLeft,HolsterSlot.ChestRight};
    internal static bool CanMove(HolsterSlot slot)=>slot!=HolsterSlot.None&&!HolsterLayout.OverShoulder(slot)&&(int)slot>=0&&(int)slot<Count;
    private const int Count=9;
    // Never more than this far from the default place on any axis.
    internal const float MaxOffset=.45f;
    // Where a place's hand point may be put (torso frame of a right-hander,
    // metres from the eyes): beside the body, from the chest to the thighs.
    internal static readonly Vector3 BoxMin=new(-.50f,-.90f,-.35f),BoxMax=new(.50f,-.05f,.50f);
    private static readonly Vector3[] offsets=new Vector3[Count];
    internal static Action<HolsterSlot,string>? Write;
    internal static Vector3 Offset(HolsterSlot slot)=>CanMove(slot)?offsets[(int)slot]:Vector3.Zero;
    internal static bool Moved(HolsterSlot slot)=>Offset(slot).LengthSquared()>1e-8f;
    internal static void Set(HolsterSlot slot,Vector3 offset)
    {
        if(!CanMove(slot))return;
        if(!float.IsFinite(offset.X)||!float.IsFinite(offset.Y)||!float.IsFinite(offset.Z))offset=Vector3.Zero;
        offsets[(int)slot]=Vector3.Clamp(offset,new Vector3(-MaxOffset),new Vector3(MaxOffset));
    }
    internal static void Reset(HolsterSlot slot){Set(slot,Vector3.Zero);Save(slot);}
    internal static void ResetAll(){foreach(var s in Movable)Reset(s);}
    internal static void Save(HolsterSlot slot){if(CanMove(slot))Write?.Invoke(slot,Format(Offset(slot)));}
    internal static void Load(HolsterSlot slot,string? text)=>Set(slot,TryParse(text,out var o)?o:Vector3.Zero);
    // The point a place is pointed to (torso frame as drawn, for this hand):
    // its offset from the default place (right-hander frame), the point kept
    // inside the box.
    internal static Vector3 OffsetTo(HolsterSlot slot,Vector3 drawnPoint,bool leftHanded)
    {
        var p=leftHanded?Mirror(drawnPoint):drawnPoint;
        p=Vector3.Clamp(p,BoxMin,BoxMax);
        return p-HolsterLayout.Pose(slot,false,false).Reach;
    }
    internal static void MoveTo(HolsterSlot slot,Vector3 drawnPoint,bool leftHanded)=>Set(slot,OffsetTo(slot,drawnPoint,leftHanded));
    internal static Vector3 Mirror(Vector3 v)=>new(-v.X,v.Y,v.Z);
    // "x,y,z" in centimetres (the config's text).
    internal static string Format(Vector3 metres)=>string.Join(",",new[]{metres.X,metres.Y,metres.Z}.Select(v=>MathF.Round(v*100,1).ToString("0.#",CultureInfo.InvariantCulture)));
    internal static bool TryParse(string? text,out Vector3 metres)
    {
        metres=Vector3.Zero;
        if(string.IsNullOrWhiteSpace(text))return false;
        var parts=text.Split(new[]{',',';',' '},StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length!=3)return false;
        var v=new float[3];
        for(int i=0;i<3;i++)if(!float.TryParse(parts[i],NumberStyles.Float,CultureInfo.InvariantCulture,out v[i])||!float.IsFinite(v[i]))return false;
        metres=new Vector3(v[0],v[1],v[2])/100;return true;
    }
    // The place's name as the player sees it (English source; translated where shown).
    internal static string Name(HolsterSlot slot,bool leftHanded)
    {
        bool right=slot is HolsterSlot.BeltRight or HolsterSlot.ArmpitRight;
        if(leftHanded)right=!right;
        return slot switch
        {
            HolsterSlot.BeltRight or HolsterSlot.BeltLeft=>right?"Pistol, right hip":"Pistol, left hip",
            HolsterSlot.ArmpitLeft or HolsterSlot.ArmpitRight=>right?"Pistol, under the right arm":"Pistol, under the left arm",
            HolsterSlot.Belly=>"Long gun, belly",
            HolsterSlot.ChestLeft=>"Knives",
            HolsterSlot.ChestRight=>"Grenades",
            _=>slot.ToString()
        };
    }
    // The place pointed at: the one nearest the ray within radius (ties: the
    // nearer one); -1: none. along: how far along the ray it is.
    internal static int Pick(Vector3 origin,Vector3 direction,IReadOnlyList<Vector3> centers,float radius,out float along)
    {
        along=0;int best=-1;float bestScore=float.PositiveInfinity;
        float l=direction.Length();if(!(l>1e-5f)||!float.IsFinite(l))return -1;
        var d=direction/l;
        for(int i=0;i<centers.Count;i++)
        {
            var v=centers[i]-origin;float t=Vector3.Dot(v,d);
            if(!float.IsFinite(t)||t<.03f||t>3)continue;
            float perp=(v-d*t).Length();if(perp>radius)continue;
            float score=perp+t*.02f;
            if(score<bestScore){bestScore=score;best=i;along=t;}
        }
        return best;
    }
    // The heading of the body the places hang on (BodyHolsters and the
    // editor share it): the head's heading, kept while looking steeply down.
    private static float torsoYaw;private static bool torsoSet;
    internal static float TorsoYawDegrees=>torsoYaw;
    internal static float Torso(Vector3 headForward)
    {
        float horizontal=headForward.X*headForward.X+headForward.Z*headForward.Z;
        if((!torsoSet||horizontal>.20f)&&horizontal>1e-6f){torsoYaw=MathF.Atan2(headForward.X,headForward.Z)*180/MathF.PI;torsoSet=true;}
        return torsoYaw;
    }
    internal static void ResetTorso(){torsoSet=false;torsoYaw=0;}
}
