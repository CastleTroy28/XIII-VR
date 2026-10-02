using System;
namespace XiiiXR;
// Color data only, never camera pixels. Native LDR lookup uses sRGB coordinates.
internal static class StoryColorLut
{
    internal const int Size=16;
    internal static float Linear(float s)=>s<=.04045f?s/12.92f:MathF.Pow((s+.055f)/1.055f,2.4f);
    internal static float Srgb(float l)=>l<=.0031308f?l*12.92f:1.055f*MathF.Pow(l,1/2.4f)-.055f;
    internal static void Sample(float r,float g,float b,bool memory,float white,out float x,out float y,out float z)
        =>Sample(r,g,b,memory?1f:0f,white,out x,out y,out z);
    internal static void Sample(float r,float g,float b,float grayscale,float white,out float x,out float y,out float z)
    {
        r=Linear(r);g=Linear(g);b=Linear(b);white=Math.Clamp(white,0,1);
        grayscale=Math.Clamp(grayscale,0,1);
        float l=.2126f*r+.7152f*g+.0722f*b;
        r+=(l-r)*grayscale;g+=(l-g)*grayscale;b+=(l-b)*grayscale;
        x=Srgb(r+(1-r)*white);y=Srgb(g+(1-g)*white);z=Srgb(b+(1-b)*white);
    }
}
