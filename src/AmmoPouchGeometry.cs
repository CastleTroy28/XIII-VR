using System;
using System.Numerics;
namespace XiiiXR;
internal static class AmmoPouchGeometry
{
    internal static Vector3 ShellPosition(int i)
    {float x=.12f+i*.032f,dx=(x-.19f)/.245f;return new(x,-.025f,-.19f+.185f*MathF.Sqrt(Math.Max(0,1-dx*dx))+.024f);}
    internal static HandMeshGeometry Build(int shells=8)
    {
        var m=new HandMeshGeometry(true);
        var leather=new Vector4(.27f,.16f,.075f,1);var seam=new Vector4(.12f,.068f,.029f,1);
        var canvas=new Vector4(.22f,.235f,.13f,1);var edge=new Vector4(.095f,.108f,.055f,1);
        var stitch=new Vector4(.56f,.42f,.24f,1);var brass=new Vector4(.46f,.35f,.14f,1);
        var bag=Quaternion.CreateFromYawPitchRoll(-.27f,.06f,-.10f);
        Vector3 Bag(Vector3 v)=>Vector3.Transform(v,bag)+new Vector3(-.007f,-.017f,.003f);
        // Soft, tapered leather walls: rounded oval sections, bulging belly,
        // gathered mouth and a closed rounded bottom. No overlapping box faces.
        float[] heights={-.11f,-.093f,-.047f,.023f,.075f};
        float[] widths={.049f,.073f,.087f,.083f,.073f};
        float[] depths={.022f,.038f,.046f,.043f,.033f};
        Vector3 Ring(int level,int index)
        {
            float a=index*MathF.PI/12;float sin=MathF.Sin(a),cos=MathF.Cos(a);
            return Bag(new Vector3(MathF.Sign(sin)*MathF.Pow(MathF.Abs(sin),.65f)*widths[level],heights[level],MathF.Sign(cos)*MathF.Pow(MathF.Abs(cos),.65f)*depths[level]));
        }
        for(int j=0;j<4;j++)for(int i=0;i<24;i++)
        {var color=i%6==0?leather*.87f:leather;color.W=1;m.Quad(Ring(j,i),Ring(j,i+1),Ring(j+1,i+1),Ring(j+1,i),color);}
        for(int i=0;i<24;i++)
        {
            m.Tri(Bag(new(0,-.11f,0)),Ring(0,i+1),Ring(0,i),seam);
            // Folded lip and interior lining remain visible when looking down.
            var a=Ring(4,i);var b=Ring(4,i+1);var innerA=Bag(new((Inverse(a)).X*.9f,.068f,(Inverse(a)).Z*.85f));
            var innerB=Bag(new((Inverse(b)).X*.9f,.068f,(Inverse(b)).Z*.85f));
            m.Quad(a,b,innerB,innerA,leather);
            m.Quad(innerA,innerB,Bag(new(Inverse(b).X*.85f,.025f,Inverse(b).Z*.8f)),Bag(new(Inverse(a).X*.85f,.025f,Inverse(a).Z*.8f)),seam);
            m.Tri(Bag(new(0,.025f,0)),Bag(new(Inverse(a).X*.85f,.025f,Inverse(a).Z*.8f)),Bag(new(Inverse(b).X*.85f,.025f,Inverse(b).Z*.8f)),seam);
        }
        // Rounded front flap, dark piped edge and individual stitches.
        for(int i=0;i<14;i++)
        {
            float a=-.071f+i*.010f,b=a+.010f;
            float ya=-.048f+.020f*MathF.Pow(a/.071f,4),yb=-.048f+.020f*MathF.Pow(b/.071f,4);
            var p0=Bag(new(a,.044f,.043f));var p1=Bag(new(b,.044f,.043f));
            var p2=Bag(new(b,yb,.050f));var p3=Bag(new(a,ya,.050f));
            m.Quad(p3,p2,p1,p0,new(.31f,.19f,.09f,1));
            var inset=Vector3.Transform(new Vector3(0,0,-.002f),bag);
            m.Quad(p3+inset,p0+inset,p1+inset,p2+inset,seam); // underside, separated from front
            Box(Bag(new((a+b)*.5f,(ya+yb)*.5f+.003f,.052f)),new(.005f,.0017f,.0017f),stitch,bag);
        }
        foreach(float x in new[]{-.066f,.066f})for(int i=0;i<7;i++)Box(Bag(new(x,-.027f+i*.009f,.050f)),new(.0017f,.004f,.0017f),stitch,bag);
        Box(Bag(new(0,-.012f,.055f)),new(.023f,.089f,.004f),seam,bag);
        Box(Bag(new(0,-.022f,.059f)),new(.031f,.022f,.004f),brass,bag);
        Box(Bag(new(0,-.022f,.062f)),new(.020f,.012f,.002f),seam,bag);
        foreach(float x in new[]{-.05f,.05f})Box(Bag(new(x,.076f,-.035f)),new(.018f,.06f,.009f),seam,bag);
        var waist=new Vector3(.19f,.015f,-.19f);
        for(int i=0;i<64;i++)
        {
            float a=i*MathF.PI/32,b=(i+1)*MathF.PI/32;
            var ao=new Vector3(MathF.Sin(a)*.245f,0,MathF.Cos(a)*.185f)+waist;
            var bo=new Vector3(MathF.Sin(b)*.245f,0,MathF.Cos(b)*.185f)+waist;
            var ai=new Vector3(MathF.Sin(a)*.236f,0,MathF.Cos(a)*.176f)+waist;
            var bi=new Vector3(MathF.Sin(b)*.236f,0,MathF.Cos(b)*.176f)+waist;
            var up=Vector3.UnitY*.028f;
            m.Quad(ao-up,bo-up,bo+up,ao+up,canvas);m.Quad(bi-up,ai-up,ai+up,bi+up,canvas);
            m.Quad(ao+up,bo+up,bi+up,ai+up,edge);m.Quad(ai-up,bi-up,bo-up,ao-up,edge);
        }
        // Move buckle away from the cartridge loops: nothing protrudes above shells.
        Box(waist+new Vector3(-.118f,0,.168f),new(.034f,.054f,.011f),brass,Quaternion.CreateFromAxisAngle(Vector3.UnitY,-.5f));
        for(int i=0;i<8;i++)
        {
            var c=ShellPosition(i);float yaw=MathF.Asin((c.X-.19f)/.245f);
            var q=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw);
            // Open canvas loop, empty when this reserve cartridge is absent.
            for(int j=0;j<20;j++)
            {
                float a=j*MathF.PI/10,b=(j+1)*MathF.PI/10;
                var u=new Vector3(MathF.Sin(a)*.013f,0,MathF.Cos(a)*.013f);
                var v=new Vector3(MathF.Sin(b)*.013f,0,MathF.Cos(b)*.013f);
                var dy=Vector3.UnitY*.022f;
                m.Quad(c+Vector3.Transform(u-dy,q),c+Vector3.Transform(v-dy,q),c+Vector3.Transform(v+dy,q),c+Vector3.Transform(u+dy,q),edge);
                m.Quad(c+Vector3.Transform(u-dy,q),c+Vector3.Transform(u+dy,q),c+Vector3.Transform(v+dy,q),c+Vector3.Transform(v-dy,q),canvas);
            }
            if(i>=Math.Clamp(shells,0,8))continue;
            Cylinder(c+new Vector3(0,.012f,0),.010f,.064f,new(.43f,.075f,.028f,1));
            Cylinder(c+new Vector3(0,.040f,0),.0108f,.009f,brass);
            Cylinder(c+new Vector3(0,.046f,0),.012f,.002f,brass);
            Cylinder(c+new Vector3(0,.0473f,0),.0035f,.001f,new(.23f,.20f,.13f,1));
            // Hull ribs and crimp, rather than a single featureless cylinder.
            for(int j=0;j<4;j++)Cylinder(c+new Vector3(0,-.017f+j*.005f,0),.01025f,.001f,new(.28f,.048f,.017f,1));
        }
        return m;
        Vector3 Inverse(Vector3 v)=>Vector3.Transform(v-new Vector3(-.007f,-.017f,.003f),Quaternion.Inverse(bag));
        void Cylinder(Vector3 c,float r,float h,Vector4 color)
        {for(int i=0;i<20;i++){float a=i*MathF.PI/10,b=(i+1)*MathF.PI/10;
            var x=new Vector3(MathF.Cos(a)*r,0,MathF.Sin(a)*r);var y=new Vector3(MathF.Cos(b)*r,0,MathF.Sin(b)*r);var u=Vector3.UnitY*h*.5f;
            m.Quad(c+x-u,c+x+u,c+y+u,c+y-u,color);m.Tri(c+u,c+y+u,c+x+u,color);m.Tri(c-u,c+x-u,c+y-u,color);}}
        void Box(Vector3 c,Vector3 size,Vector4 color,Quaternion q)
        {
            var h=size*.5f;Vector3 P(float x,float y,float z)=>c+Vector3.Transform(new Vector3(x*h.X,y*h.Y,z*h.Z),q);
            m.Quad(P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1),color);
            m.Quad(P(1,-1,-1),P(-1,-1,-1),P(-1,1,-1),P(1,1,-1),color);
            m.Quad(P(-1,-1,-1),P(-1,-1,1),P(-1,1,1),P(-1,1,-1),color);
            m.Quad(P(1,-1,1),P(1,-1,-1),P(1,1,-1),P(1,1,1),color);
            m.Quad(P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1),color);
            m.Quad(P(-1,-1,-1),P(1,-1,-1),P(1,-1,1),P(-1,-1,1),color);
        }
    }
}
