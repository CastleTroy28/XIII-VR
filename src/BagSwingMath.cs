using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.98: a hanging punching bag as a damped pendulum. The swing is a rotation
// vector (axis * angle, horizontal) about the top mount.
internal static class BagSwingMath
{
    internal const float FistToBag=4f/30f;   // effective fist mass / bag mass
    internal const float MaxAngle=.6f;       // ~34 degrees
    internal const float Damping=.35f;       // 1/s
    internal static Vector3 Hit(Vector3 angularVelocity,Vector3 fromPivot,Vector3 velocity,float length)
    {
        length=Math.Max(.3f,length);
        var delta=Vector3.Cross(fromPivot,velocity)*(FistToBag/(length*length));
        delta.Y=0;                                   // no spin about the chain
        if(delta.Length()>2.5f)delta=Vector3.Normalize(delta)*2.5f;
        var w=angularVelocity+delta;w.Y=0;
        if(w.Length()>3.5f)w=Vector3.Normalize(w)*3.5f;
        return w;
    }
    // Returns true while the bag is still moving.
    internal static bool Step(ref Vector3 angle,ref Vector3 angularVelocity,float length,float dt)
    {
        if(!(dt>0))return angle.LengthSquared()>1e-8f||angularVelocity.LengthSquared()>1e-8f;
        length=Math.Max(.3f,length);float k=9.81f/length;
        int steps=Math.Max(1,(int)Math.Ceiling(dt/(1f/120)));float h=Math.Min(dt,.1f)/steps;
        for(int i=0;i<steps;i++)
        {
            angularVelocity+=(-k*angle-Damping*angularVelocity)*h;
            angle+=angularVelocity*h;
            angle.Y=0;angularVelocity.Y=0;
            float a=angle.Length();
            if(a>MaxAngle)
            {
                var n=angle/a;angle=n*MaxAngle;
                float outward=Vector3.Dot(angularVelocity,n);if(outward>0)angularVelocity-=n*outward;
            }
        }
        bool moving=angle.Length()>.002f||angularVelocity.Length()>.01f;
        if(!moving){angle=Vector3.Zero;angularVelocity=Vector3.Zero;}
        return moving;
    }
    internal static bool BagName(string name)
    {
        name=name.ToLowerInvariant();
        if(name.Contains("sandbag")||name.Contains("sand_bag")||name.Contains("handbag")||name.Contains("garbage")||name.Contains("trash"))return false;
        return name.Contains("punch")||name.Contains("boxing")||name.Contains("boxsack")||name.Contains("box_sack")||name.Contains("boxsak")||name.Contains("heavybag")||name.Contains("heavy_bag")||name.Contains("speedbag")||name.Contains("speed_bag")
            ||name.Contains("boxbag")||name.Contains("box_bag")||name.Contains("training_bag")||name.Contains("trainingbag")||name.Contains("bag");
    }
    // Plain "bag" (without punch/boxing) must actually hang: nothing solid
    // right under it.
    internal static bool NeedsHangingCheck(string name)
    {
        name=name.ToLowerInvariant();
        return !(name.Contains("punch")||name.Contains("boxing")||name.Contains("boxsack")||name.Contains("box_sack")||name.Contains("boxsak")||name.Contains("heavybag")||name.Contains("heavy_bag")||name.Contains("speedbag")||name.Contains("speed_bag")||name.Contains("training"));
    }
}
