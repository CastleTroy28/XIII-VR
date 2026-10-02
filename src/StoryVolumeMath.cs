using System;
namespace XiiiXR;
internal static class StoryVolumeMath
{
    internal static float Influence(float distanceSquared,float blendDistance)
    {
        if(!float.IsFinite(distanceSquared)||!float.IsFinite(blendDistance))return 0;
        distanceSquared=Math.Max(0,distanceSquared);
        float range=blendDistance*blendDistance;
        if(distanceSquared>range)return 0;
        return range>0?1-distanceSquared/range:1;
    }
    // 0.1.122: a level volume's saturation greys the view only in a cutscene
    // or a memory. In plain gameplay the whole Emerald base mission went grey
    // (a desaturating volume the game itself does not show there).
    internal static float Applied(float volume,bool memory,bool scripted)=>Math.Max(memory?1:0,scripted&&float.IsFinite(volume)?Math.Clamp(volume,0,1):0);
    internal static float Blend(float current,float target,float weight)
        =>float.IsFinite(target)&&float.IsFinite(weight)?current+(target-current)*Math.Clamp(weight,0,1):current;
}
