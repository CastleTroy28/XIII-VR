using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
namespace XiiiXR;
// 0.1.162: times the steps of one call.
// A long call is written at once with its steps (PERF SLOW); every 30 s the
// average of each step per call is written (PERF STEPS), so the next log
// names what stops the game and what costs the most frame after frame.
internal sealed class StepClock
{
    private const int Max=24;
    private readonly string what;private readonly double threshold;
    private readonly string[] names=new string[Max];private readonly double[] ms=new double[Max];private readonly long[] bytes=new long[Max];
    // 0.1.165: the memory each step allocates too (written with a long call, or when a call allocates 8 MB or more).
    private long allocStart,allocLast;
    private readonly string[] totalNames=new string[Max];private readonly double[] totals=new double[Max];private int totalCount,calls;
    private int count,reports;private long start,last,nextSummary;
    internal StepClock(string what,double thresholdMs=25){this.what=what;threshold=thresholdMs;}
    internal void Begin(){count=0;start=last=Stopwatch.GetTimestamp();allocStart=allocLast=GC.GetAllocatedBytesForCurrentThread();}
    internal void Mark(string step)
    {
        if(start==0)return;
        long now=Stopwatch.GetTimestamp();double d=(now-last)*1000.0/Stopwatch.Frequency;last=now;
        long alloc=GC.GetAllocatedBytesForCurrentThread();long b=alloc-allocLast;allocLast=alloc;
        int k=Array.IndexOf(totalNames,step,0,totalCount);
        if(k<0&&totalCount<Max){k=totalCount++;totalNames[k]=step;}
        if(k>=0)totals[k]+=d;
        if(d<1&&b<(1<<20)||count>=Max)return;
        names[count]=step;ms[count]=d;bytes[count]=b;count++;
    }
    internal void End()
    {
        if(start==0)return;
        long now=Stopwatch.GetTimestamp();
        double total=(now-start)*1000.0/Stopwatch.Frequency;start=0;calls++;
        long allocated=GC.GetAllocatedBytesForCurrentThread()-allocStart;
        if((total>=threshold||allocated>=8L<<20)&&reports<60)
        {
            reports++;
            var text=new StringBuilder("PERF SLOW ").Append(what).Append(' ').Append(total.ToString("F0",CultureInfo.InvariantCulture)).Append(" ms");
            if(allocated>=1<<20)text.Append(", ").Append((allocated/1048576.0).ToString("F1",CultureInfo.InvariantCulture)).Append(" MB allocated");
            text.Append(':');
            for(int i=0;i<count;i++)
            {
                text.Append(i==0?" ":", ").Append(names[i]).Append(' ').Append(ms[i].ToString("F0",CultureInfo.InvariantCulture));
                if(bytes[i]>=1<<20)text.Append(" (").Append((bytes[i]/1048576.0).ToString("F1",CultureInfo.InvariantCulture)).Append(" MB)");
            }
            if(count==0)text.Append(" (no single step over 1 ms)");
            Bootstrap.Write(text.ToString());
        }
        if(nextSummary==0)nextSummary=now+30*Stopwatch.Frequency;
        if(now<nextSummary)return;
        nextSummary=now+30*Stopwatch.Frequency;
        var summary=new StringBuilder("PERF STEPS ").Append(what).Append(" per call (ms, ").Append(calls).Append(" calls):");
        bool any=false;
        for(int i=0;i<totalCount;i++)
        {
            double avg=totals[i]/Math.Max(1,calls);
            if(avg>=.02){summary.Append(any?", ":" ").Append(totalNames[i]).Append(' ').Append(avg.ToString("F2",CultureInfo.InvariantCulture));any=true;}
            totals[i]=0;
        }
        calls=0;
        if(any)Bootstrap.Write(summary.ToString());
    }
}
