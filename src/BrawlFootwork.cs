using System;
using System.Numerics;
namespace XiiiXR;
// World-space foot plants. Only one foot swings at a time; phase advances with time,
// not with the number of render callbacks. There is no walking-in-place sine wave.
internal sealed class BrawlFootwork
{
    internal readonly Vector3[] Feet=new Vector3[2];
    private Vector3 from,to,lastRoot;
    private float started,last=-1,nextStep;
    internal int Moving{get;private set;}=-1;
    private bool ready;private int previous=1;
    internal float Progress{get;private set;}
    internal void Reset(){ready=false;Moving=-1;last=-1;}
    internal void Advance(float now,Vector3 root,Quaternion yaw,Vector3 left,Vector3 right,bool leadLeft,float speed,float scale)
    {
        if(!float.IsFinite(now)||!Finite(root)||!Finite(left)||!Finite(right))return;
        if(ready&&now==last)return; // Two eyes/render callbacks cannot advance or re-decide a step.
        var velocity=ready&&last>=0&&now>last?(root-lastRoot)/Math.Clamp(now-last,.001f,.1f):Vector3.Zero;
        velocity.Y=0;if(velocity.LengthSquared()>6.25f)velocity=Vector3.Zero;
        if(!ready||Vector3.DistanceSquared(root,lastRoot)>2.25f)
        {Feet[0]=left;Feet[1]=right;Moving=-1;nextStep=now+.06f;ready=true;last=now;}
        lastRoot=root;float dt=last<0?0:Math.Clamp(now-last,0,.05f);last=now;
        float stagger=(speed>.2f?.13f:.18f)*scale;
        var anticipation=velocity*.10f;
        var desiredL=root+anticipation+Vector3.Transform(new Vector3(-.17f*scale,left.Y-root.Y,(leadLeft?stagger:-stagger)),yaw);
        var desiredR=root+anticipation+Vector3.Transform(new Vector3(.17f*scale,right.Y-root.Y,(leadLeft?-stagger:stagger)),yaw);
        if(Moving>=0)
        {
            Progress=Math.Clamp((now-started)/(.20f/Math.Clamp(1+speed*.55f,1,2.1f)),0,1);
            float s=Progress*Progress*(3-2*Progress);
            Feet[Moving]=Vector3.Lerp(from,to,s)+Vector3.UnitY*(MathF.Sin(Progress*MathF.PI)*.045f*scale);
            if(Progress>=1){previous=Moving;Moving=-1;nextStep=now+(speed>.2f?.015f:.045f);}
            return;
        }
        // Vertical ground changes follow the support surface gradually, keeping X/Z planted.
        Feet[0].Y=Toward(Feet[0].Y,left.Y,dt*.6f);Feet[1].Y=Toward(Feet[1].Y,right.Y,dt*.6f);
        if(now<nextStep)return;
        float dl=FlatDistance(Feet[0],desiredL),dr=FlatDistance(Feet[1],desiredR);
        if(Math.Max(dl,dr)<.12f*scale)return;
        int choice=dl>dr?0:1;
        if(choice==previous&&(choice==0?dr:dl)>.1f*scale)choice=1-choice;
        var target=choice==0?desiredL:desiredR;
        // A small anticipation shortens root travel while a foot is in the air.
        from=Feet[choice];to=target;Moving=choice;started=now;Progress=0;
    }
    private static float FlatDistance(Vector3 a,Vector3 b){a.Y=b.Y;return Vector3.Distance(a,b);}
    private static float Toward(float a,float b,float d)=>a+Math.Clamp(b-a,-d,d);
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
