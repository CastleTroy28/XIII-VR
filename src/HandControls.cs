using System;
namespace XiiiXR;
internal readonly struct HandControls
{
    internal const ulong Stick = 1UL << 63, Trigger = 1UL << 33, Grip = 1UL << 2, A = 1UL << 7, B = 1UL << 1;
    internal readonly ulong Held, Down, Up;
    internal readonly bool Valid;
    internal HandControls(bool valid, ulong held, ulong down, ulong up)
    { Valid = valid; Held = held; Down = down; Up = up; }
}
internal sealed class ControlEdges
{
    private ulong previous;
    private bool armed;
    internal HandControls Sample(bool valid, ulong buttons)
    {
        if (!valid) { var up = previous; previous = 0; armed = false; return new HandControls(false, 0, 0, up); }
        // Startup, reconnect and menu exit require a released trigger first.
        if (!armed) { armed = (buttons & HandControls.Trigger) == 0; buttons &= ~HandControls.Trigger; }
        var result = new HandControls(true, buttons, buttons & ~previous, previous & ~buttons);
        previous = buttons; return result;
    }
    internal void Disarm() { armed = false; }
}
// UI and gameplay consume the same physical sample independently. Suspending
// shooting must never erase the trigger used to confirm a menu selection.
internal sealed class RightControlChannels
{
    private readonly ControlEdges game=new(),menu=new();
    internal HandControls Gameplay,Menu;
    internal void Sample(bool valid,ulong buttons)
    {Gameplay=game.Sample(valid,buttons);Menu=menu.Sample(valid,buttons);}
    internal void DisarmGameplay()
    {
        game.Disarm();
        Gameplay=new HandControls(Gameplay.Valid,Gameplay.Held&~HandControls.Trigger,Gameplay.Down&~HandControls.Trigger,
            Gameplay.Up|(Gameplay.Held&HandControls.Trigger));
    }
}
// 0.1.215: a stick click at a key, card or lockpick lock takes the item out
// (R3; L3 left-handed): that click is the lock's, not the secondary fire, a
// scope's zoom or the sprint. InteractionDriver answers for the side.
internal static class LockStick
{
    internal static Func<bool,bool>? Query{get;set;}
    internal static bool Taken(bool right){try{return Query?.Invoke(right)==true;}catch(Exception){return false;}}
}
