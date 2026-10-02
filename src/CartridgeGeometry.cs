using System;
using System.Numerics;
namespace XiiiXR;
internal static class CartridgeGeometry
{
    internal static HandMeshGeometry Speedloader(int rounds=6)
    {
        var m=new HandMeshGeometry(true);
        Cylinder(m,Vector3.Zero,.023f,.012f,new(.10f,.10f,.11f,1));
        for(int i=0;i<Math.Clamp(rounds,0,6);i++){float a=i*MathF.PI/3;var p=new Vector3(MathF.Cos(a)*.015f,MathF.Sin(a)*.015f,.012f);Cylinder(m,p,.0048f,.023f,new(.65f,.49f,.22f,1));Nose(m,p+Vector3.UnitZ*.023f);}
        return m;
    }
    internal static HandMeshGeometry Case(){var m=new HandMeshGeometry(true);Cylinder(m,Vector3.Zero,.0048f,.023f,new(.65f,.49f,.22f,1));return m;}
    private static void Nose(HandMeshGeometry m,Vector3 p)
    {
        for(int ring=0;ring<6;ring++)
        {
            float z0=ring*.0015f,z1=(ring+1)*.0015f;
            float r0=.0043f*MathF.Cos(ring*MathF.PI/12),r1=.0043f*MathF.Cos((ring+1)*MathF.PI/12);
            for(int i=0;i<24;i++){float a=i*MathF.PI/12,b=(i+1)*MathF.PI/12;
                Vector3 V(float angle,float radius,float z)=>p+new Vector3(radius*MathF.Cos(angle),radius*MathF.Sin(angle),z);
                if(ring==5)m.Tri(V(a,r0,z0),V(b,r0,z0),p+Vector3.UnitZ*z1,new(.46f,.30f,.17f,1));
                else m.Quad(V(a,r0,z0),V(b,r0,z0),V(b,r1,z1),V(a,r1,z1),new(.46f,.30f,.17f,1));}
        }
    }
    private static void Cylinder(HandMeshGeometry m,Vector3 p,float r,float length,Vector4 color)
    {
        for(int i=0;i<16;i++){float a=i*MathF.PI/8,b=(i+1)*MathF.PI/8;var x=p+new Vector3(r*MathF.Cos(a),r*MathF.Sin(a),0);var y=p+new Vector3(r*MathF.Cos(b),r*MathF.Sin(b),0);var z=Vector3.UnitZ*length;m.Quad(x,y,y+z,x+z,color);m.Tri(p,y,x,color);m.Tri(p+z,x+z,y+z,color);}
    }
}
