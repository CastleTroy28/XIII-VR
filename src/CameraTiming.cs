using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
namespace XiiiXR;
// 0.1.164: how long each of the game's cameras takes on the
// main thread (culling and drawing, both eyes: OnPreCull .. OnPostRender),
// per frame, in the PERF line (camMs=). Tells whether the world camera (its
// shadows, detail) or another camera costs the frame.
internal static class CameraTiming
{
    private const int Max=16;
    private static readonly string[] names=new string[Max];private static readonly double[] ms=new double[Max];private static int count;
    internal static int Slot(string name)
    {
        name=string.IsNullOrEmpty(name)?"camera":name.Replace(" ","");
        int k=Array.IndexOf(names,name,0,count);
        if(k<0&&count<Max){k=count++;names[k]=name;}
        return k;
    }
    internal static void Add(int slot,long start)
    {
        if(slot<0||slot>=count||start==0)return;
        double d=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
        if(double.IsFinite(d)&&d>=0&&d<1000)ms[slot]+=d;
    }
    // 0.1.165: when in
    // the frame the drawing starts (the game's logic, physics, animation and
    // the mod's work before it) and when it ends, from the frame's start
    // (Time.unscaledTime), per frame: drawStart= and drawEnd= avg/p95 ms.
    private static int frame=-1;private static float drawStart,drawEnd;
    private static readonly float[] starts=new float[1024],ends=new float[1024];private static int samples;
    internal static void PreCull(int currentFrame,float sinceFrameStart)
    {
        if(currentFrame==frame||!float.IsFinite(sinceFrameStart))return;
        Flush();frame=currentFrame;drawStart=drawEnd=sinceFrameStart;
    }
    internal static void PostRender(int currentFrame,float sinceFrameStart)
    {
        if(currentFrame==frame&&float.IsFinite(sinceFrameStart)&&sinceFrameStart>drawEnd)drawEnd=sinceFrameStart;
    }
    private static void Flush()
    {
        if(frame<0||samples>=starts.Length)return;
        if(drawStart<0||drawStart>1||drawEnd<drawStart||drawEnd>1)return;
        starts[samples]=drawStart*1000;ends[samples]=drawEnd*1000;samples++;
    }
    internal static string Frames()
    {
        if(samples==0)return "drawStartMs=none";
        string Stat(float[] a){var s=new float[samples];Array.Copy(a,s,samples);Array.Sort(s);double sum=0;foreach(var v in s)sum+=v;return (sum/samples).ToString("F1",CultureInfo.InvariantCulture)+"/"+s[Math.Clamp((int)Math.Ceiling(samples*.95)-1,0,samples-1)].ToString("F1",CultureInfo.InvariantCulture);}
        string text="drawStartMs="+Stat(starts)+" drawEndMs="+Stat(ends);samples=0;return text;
    }
    internal static string Report(int frames)
    {
        if(frames<=0)frames=1;var text=new StringBuilder();
        for(int k=0;k<count;k++){if(ms[k]>0)text.Append(text.Length==0?"camMs=":",").Append(names[k]).Append(':').Append((ms[k]/frames).ToString("F2",CultureInfo.InvariantCulture));ms[k]=0;}
        return text.Length==0?"camMs=none":text.ToString();
    }
}
