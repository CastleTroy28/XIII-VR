using System;
namespace XiiiXR;
// 0.1.151: The pure part: when to clean up, and how the step follows the pauses.
internal static class GcPaceMath
{
    internal const long MinStep=2L<<20,MaxStep=12L<<20,StartStep=4L<<20;
    // Pauses aimed at (ms): a paced clean-up longer than High makes the step smaller, shorter than Low larger.
    internal const double Low=2,High=6,SlowMs=25;
    internal static bool Due(long allocatedSince,long step)=>step>0&&allocatedSince>=step;
    internal static long Adapt(long step,double averageMs)
    {
        if(!double.IsFinite(averageMs)||averageMs<=0)return Math.Clamp(step,MinStep,MaxStep);
        long next=averageMs>High?step*3/4:averageMs<Low?step*5/4:step;
        next=Math.Clamp(next,MinStep,MaxStep);
        return next>>20<<20==next?next:(next+(1L<<19))>>20<<20;
    }
}
