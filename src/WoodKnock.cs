using System;
namespace XiiiXR;
// 0.1.161: the game's own broom hit event stays
// silent here, so a short wooden knock is made once, in memory: a few
// quickly dying tones of a struck wooden stick (a low thump, the stick's
// ring) and a click at the start; 16-bit mono WAV, played like the supplied
// recordings (ChairImpactClip), a little higher or lower each time.
internal static class WoodKnock
{
    internal const int Rate=44100;internal const float Seconds=.16f;
    // (frequency Hz, amplitude, decay s)
    private static readonly (float f,float a,float d)[] Modes={(190f,.40f,.045f),(640f,1f,.026f),(1170f,.55f,.017f),(1890f,.32f,.010f),(2950f,.18f,.006f)};
    private static byte[]? wav;
    internal static byte[] Wav=>wav??=Make();
    internal static float[] Samples()
    {
        int n=(int)(Rate*Seconds);var s=new float[n];var random=new Random(1961);float previousNoise=0;
        for(int i=0;i<n;i++)
        {
            float t=i/(float)Rate;float v=0;
            foreach(var (f,a,d) in Modes)v+=a*MathF.Exp(-t/d)*MathF.Sin(2*MathF.PI*f*t);
            float noise=(float)(random.NextDouble()*2-1);
            v+=.6f*(noise-previousNoise)*MathF.Exp(-t/.0018f);previousNoise=noise;
            v*=Math.Min(1f,t/.0008f);
            s[i]=v;
        }
        float peak=0;foreach(var v in s)peak=Math.Max(peak,Math.Abs(v));
        if(peak>0)for(int i=0;i<n;i++)s[i]*=.85f/peak;
        return s;
    }
    private static byte[] Make()
    {
        var s=Samples();int data=s.Length*2;var b=new byte[44+data];
        void Text(int at,string x){for(int i=0;i<4;i++)b[at+i]=(byte)x[i];}
        void I32(int at,int v){b[at]=(byte)v;b[at+1]=(byte)(v>>8);b[at+2]=(byte)(v>>16);b[at+3]=(byte)(v>>24);}
        void I16(int at,int v){b[at]=(byte)v;b[at+1]=(byte)(v>>8);}
        Text(0,"RIFF");I32(4,36+data);Text(8,"WAVE");Text(12,"fmt ");I32(16,16);I16(20,1);I16(22,1);I32(24,Rate);I32(28,Rate*2);I16(32,2);I16(34,16);Text(36,"data");I32(40,data);
        for(int i=0;i<s.Length;i++){int v=(int)MathF.Round(Math.Clamp(s[i],-1f,1f)*32767);I16(44+i*2,v);}
        return b;
    }
}
