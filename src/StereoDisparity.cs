using System;
using System.Collections.Generic;
namespace XiiiXR;
// 0.1.237: how far apart the two eyes' pictures are (STEREO CHECK). The
// picture of the left eye and of the right eye side by side (RGB, the left
// half the left eye): blocks of the left picture with detail in them are
// looked for along the same row of the right one; the shift that matches
// best is how far that thing moved between the eyes. Without any shift (or a
// shift that does not grow with the eye distance) the eyes are drawn from one
// place: no depth, the world flat and huge.
internal readonly record struct StereoShift(int Blocks,float MedianShift,float CenterShift,float Identical,int Width);
internal static class StereoDisparity
{
    internal const int Block=16;
    // Pixels of difference per pixel at no shift below which the two pictures are the same.
    internal const float SameBelow=1.5f;
    internal static StereoShift Measure(byte[] rgb,int width,int height)
    {
        int w=width/2,h=height;
        if(rgb==null||w<Block*4||h<Block*4||rgb.Length<width*height*3)return new StereoShift(0,float.NaN,float.NaN,float.NaN,w);
        var left=new float[w*h];var right=new float[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            int a=(y*width+x)*3,b=(y*width+w+x)*3;
            left[y*w+x]=.299f*rgb[a]+.587f*rgb[a+1]+.114f*rgb[a+2];
            right[y*w+x]=.299f*rgb[b]+.587f*rgb[b+1]+.114f*rgb[b+2];
        }
        // Mean difference at no shift over the middle (the same picture in both eyes: about 0).
        double same=0;int samples=0;
        for(int y=h/4;y<h*3/4;y++)for(int x=w/4;x<w*3/4;x++){same+=Math.Abs(left[y*w+x]-right[y*w+x]);samples++;}
        float identical=samples>0?(float)(same/samples):float.NaN;
        // An asymmetric field of view puts a thing far ahead at different places
        // in the two pictures (up to a third of the width), so the search is wide.
        int maxShift=w/3;
        var shifts=new List<int>();float center=float.NaN;float centerDistance=float.PositiveInfinity;
        for(int by=h/5;by+Block<=h*4/5;by+=Block)
        for(int bx=w/6;bx+Block<=w*5/6;bx+=Block)
        {
            // Only blocks with detail (a flat wall matches anywhere).
            double sum=0,sum2=0;
            for(int y=0;y<Block;y++)for(int x=0;x<Block;x++){float v=left[(by+y)*w+bx+x];sum+=v;sum2+=v*v;}
            double n=Block*Block,variance=sum2/n-(sum/n)*(sum/n);
            if(variance<36)continue;
            int best=0;double bestSad=double.PositiveInfinity;var sads=new double[2*maxShift+1];
            for(int d=-maxShift;d<=maxShift;d++)
            {
                double sad=double.PositiveInfinity;
                if(bx+d>=0&&bx+d+Block<=w)
                {
                    sad=0;
                    for(int y=0;y<Block;y++){int row=(by+y)*w;for(int x=0;x<Block;x++)sad+=Math.Abs(left[row+bx+x]-right[row+bx+d+x]);}
                }
                sads[d+maxShift]=sad;
                if(sad<bestSad){bestSad=sad;best=d;}
            }
            if(!double.IsFinite(bestSad))continue;
            // The best match must stand out (not a repeating pattern or a smooth gradient).
            double second=double.PositiveInfinity;
            for(int d=-maxShift;d<=maxShift;d++)if(Math.Abs(d-best)>2&&sads[d+maxShift]<second)second=sads[d+maxShift];
            if(double.IsFinite(second)&&bestSad>second*.8)continue;
            shifts.Add(best);
            float cx=bx+Block*.5f-w*.5f,cy=by+Block*.5f-h*.5f,dist=cx*cx+cy*cy;
            if(dist<centerDistance){centerDistance=dist;center=best;}
        }
        if(shifts.Count==0)return new StereoShift(0,float.NaN,float.NaN,identical,w);
        shifts.Sort();
        float median=shifts.Count%2==1?shifts[shifts.Count/2]:(shifts[shifts.Count/2-1]+shifts[shifts.Count/2])*.5f;
        return new StereoShift(shifts.Count,median,center,identical,w);
    }
    // Where a thing straight ahead and far away is in the right picture against
    // the left one, in pixels of one eye's picture, from the eyes' projections
    // (m02 of each: the offset of the picture's middle; positive is right).
    internal static float FarShift(float leftM02,float rightM02,int width)=>-(rightM02-leftM02)*width*.5f;
}
