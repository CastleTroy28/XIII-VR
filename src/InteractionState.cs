namespace XiiiXR;
// Native "interact": right Grip alone picks up items (weapons, bottles,
// chairs, quest items) when the hand points at one; doors, cabinets and
// lockers keep Grip + A so a physical door grab never opens them by mistake.
// 0.1.215: a key, card or lockpick lock: the stick click of that hand (R3,
// or L3 left-handed) takes the item out; Grip + A still does too.
internal sealed class InteractionState
{
    private bool armed,previous,gripLatch,lockLatch;
    internal ActionEdge Action { get; private set; }
    // The stick click held that started a lock's interaction.
    internal bool LockHeld=>lockLatch;
    internal void Reset() { armed=false; previous=false; gripLatch=false; lockLatch=false; Action=default; }
    // 0.1.151: pickupsOnly - the other hand's grip: only a grip press on a thing (no Grip + A doors).
    internal void Sample(HandControls hand,bool allowed,System.Func<bool>? pickupTarget=null,bool pickupsOnly=false,System.Func<bool>? lockTarget=null)
    {
        if (!allowed || !hand.Valid) { Action=new ActionEdge(false,previous); previous=false; armed=false; gripLatch=false; lockLatch=false; return; }
        bool combo=(hand.Held & (HandControls.Grip|HandControls.A))==(HandControls.Grip|HandControls.A);
        bool grip=(hand.Held&HandControls.Grip)!=0;
        // Only a fresh grip press on a pickup: sweeping a closed hand over an
        // item does not grab it.
        if(!grip)gripLatch=false;
        else if((hand.Down&HandControls.Grip)!=0&&!combo&&pickupTarget?.Invoke()==true)gripLatch=true;
        // Only a fresh stick click at a lock (a click held from before is not one).
        if((hand.Held&HandControls.Stick)==0)lockLatch=false;
        else if((hand.Down&HandControls.Stick)!=0&&!pickupsOnly&&lockTarget?.Invoke()==true)lockLatch=true;
        bool pressed=(combo&&!pickupsOnly)||gripLatch||lockLatch;
        if(!armed) { armed=!pressed; Action=default; return; }
        Action=new ActionEdge(pressed,previous); previous=pressed;
    }
}
