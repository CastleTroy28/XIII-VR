using System;
namespace XiiiXR;
// One clock for both eyes. Switching the active flashback subscene must not reset it.
internal sealed class StoryEffectState
{
    internal bool Memory { get; private set; }
    private bool known, rising;
    private float started=-100, released=-100, pulseAt=-100;
    // Timeline clips can request a flash without changing GameManager's state.
    // Such a clip is a short pulse, not the sequence's wait-for-teleport whiteout.
    internal void Pulse(float now){if(now-pulseAt>.15f)pulseAt=now;}
    internal void Trigger(float now)
    {
        if(rising)return; // two native hooks can describe the same transition
        rising=true;started=now;released=-100;
    }
    internal void Observe(bool memory,float now)
    {
        if(known&&memory!=Memory)Transition(memory,now);
        else {Memory=memory;known=true;}
    }
    internal void Transition(bool memory,float now)
    {Memory=memory;known=true;rising=false;released=now;}
    internal float White(float now)
    {
        if(rising&&now-started>3){rising=false;released=now;}
        float sequence=rising?Math.Clamp((now-started)/.25f,0,1):Math.Clamp(1-(now-released)/.45f,0,1);
        float age=now-pulseAt;
        float pulse=age<.15f?Math.Clamp(age/.15f,0,1):Math.Clamp(1-(age-.15f)/.45f,0,1);
        return Math.Max(sequence,pulse);
    }
    internal void Reset(){known=false;Memory=false;rising=false;started=released=pulseAt=-100;}
}
