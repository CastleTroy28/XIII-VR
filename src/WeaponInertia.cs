using System;
using System.Numerics;
namespace XiiiXR;
internal sealed class WeaponInertia
{
    private bool ready;
    private Vector3 position;
    private Quaternion rotation=Quaternion.Identity;
    internal void Reset()=>ready=false;
    internal (Vector3 position,Quaternion rotation) Step(Vector3 target,Quaternion aim,float dt,string profile,bool support,float strength)
    {
        strength=float.IsFinite(strength)?Math.Clamp(strength,0,2):1;
        bool valid=NativeHandMesh.Finite(target)&&float.IsFinite(aim.LengthSquared())&&aim.LengthSquared()>.5f;
        if(!valid){Reset();return(position,rotation);}
        aim=Quaternion.Normalize(aim);
        if(!ready||!float.IsFinite(dt)||dt<=0||dt>.1f||Vector3.Distance(position,target)>.45f||strength==0)
        {ready=true;position=target;rotation=aim;return(position,rotation);}
        float tau=(profile=="pistol"?.040f:profile=="shotgun"?.115f:.090f)*strength*(support?.65f:1);
        float alpha=1-MathF.Exp(-dt/Math.Max(.001f,tau));
        position=target;rotation=Quaternion.Normalize(Quaternion.Slerp(rotation,aim,alpha));
        float angle=2*MathF.Acos(Math.Clamp(Math.Abs(Quaternion.Dot(rotation,aim)),0,1));
        float maxAngle=(profile=="pistol"?8:18)*MathF.PI/180*strength;
        if(angle>maxAngle)rotation=Quaternion.Normalize(Quaternion.Slerp(aim,rotation,maxAngle/angle));
        return(position,rotation);
    }
}
