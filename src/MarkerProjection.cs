using System;
using System.Numerics;
namespace XiiiXR;
internal static class MarkerProjection
{
    internal static (float x,float y,bool edge,float angle) Place(Vector3 direction)
    {
        const float width=.96f,height=.49f,depth=1.7f;
        float z=Math.Max(.05f,Math.Abs(direction.Z));
        float x=direction.X*depth/z,y=direction.Y*depth/z;
        bool edge=direction.Z<=0||Math.Abs(x)>width||Math.Abs(y)>height;
        if(edge)
        {
            if(Math.Abs(x)+Math.Abs(y)<.00001f)x=width;
            float factor=Math.Max(Math.Abs(x)/width,Math.Abs(y)/height);
            x/=factor;y/=factor;
        }
        return(x,y,edge,90-MathF.Atan2(y,x)*180/MathF.PI);
    }
}
