using System;
using System.Numerics;
namespace XiiiXR;
// Neutral-to-arm prevents the gameplay stick from selecting a menu entry.
internal sealed class MenuNavigation
{
    private bool armed;private ushort direction;private float next;
    internal void Reset(){armed=false;direction=0;}
    internal ushort Step(bool valid,Vector2 stick,float now)
    {
        if(!valid||!float.IsFinite(stick.LengthSquared())||!float.IsFinite(now)){Reset();return 0;}
        if(stick.LengthSquared()<.16f){armed=true;direction=0;return 0;}
        if(!armed||stick.LengthSquared()<.36f)return 0;
        ushort key=Math.Abs(stick.Y)>=Math.Abs(stick.X)?(ushort)(stick.Y>0?0x26:0x28):(ushort)(stick.X>0?0x27:0x25);
        if(key!=direction){direction=key;next=now+.38f;return key;}
        if(now<next)return 0;next=now+.16f;return key;
    }
}
