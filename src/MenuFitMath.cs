using System;
namespace XiiiXR;
// 0.1.155: the
// game's menu list with one more button must take no more room than it had.
// First the gaps between the buttons shrink (not below `minSpacing`), then
// the whole list is scaled down; afterwards its top is put back where it was.
internal static class MenuFitMath
{
    internal const float MinScale=.72f;
    // oldSpan: the list's height before; newSpan: with the added button;
    // gaps: gaps between buttons now. Returns the new spacing and the scale.
    internal static (float spacing,float scale) Plan(float oldSpan,float newSpan,float spacing,int gaps,float minSpacing=0)
    {
        if(!float.IsFinite(oldSpan)||!float.IsFinite(newSpan)||!float.IsFinite(spacing)||oldSpan<=0||newSpan<=oldSpan+.5f)return (spacing,1);
        float over=newSpan-oldSpan;
        if(!float.IsFinite(minSpacing))minSpacing=0;
        float give=gaps>0&&spacing>minSpacing?Math.Min(spacing-minSpacing,over/gaps):0;
        float s=spacing-give;float left=newSpan-give*Math.Max(0,gaps);
        float scale=left>oldSpan+.5f?Math.Max(MinScale,oldSpan/left):1;
        return (s,scale);
    }
}
