using System.Collections.Generic;
namespace XiiiXR;
// 0.1.213: taking a hostage by pointing at him (GripCarry.Point.cs), testable
// without the game.
internal static class HostagePointMath
{
    // How far the hand's ray looks (m). Whether he can be taken at all is the
    // game's rule or the VR one (behind him, the head within 1.5 m).
    internal const float Reach=2.5f;
    internal enum Hit{Player,Passable,Npc,World}
    // The nearest thing along the ray that counts: the player's own colliders,
    // empty trigger volumes and loose things are passed through, a person is
    // the target, anything else (a wall, a table) is in the way.
    // Returns the index of the person's hit in the list, or -1.
    internal static int FirstNpc(IReadOnlyList<(float distance,Hit kind,int index)> hits)
    {
        int best=-1;float nearest=float.PositiveInfinity;bool blocked=false;float wall=float.PositiveInfinity;
        for(int i=0;i<hits.Count;i++)
        {
            var h=hits[i];if(!(h.distance>=0)||!float.IsFinite(h.distance))continue;
            if(h.kind==Hit.World&&h.distance<wall){wall=h.distance;blocked=true;}
            if(h.kind==Hit.Npc&&h.distance<nearest){nearest=h.distance;best=i;}
        }
        return best>=0&&(!blocked||nearest<=wall)?best:-1;
    }
    // 0.1.216: the grip is held, not just pressed: a fist closed to punch him
    // in the back (KnockoutMath) is swung within that time. Held still this
    // long (s), the hand within StillReach of where the grip closed (m,
    // the player's own space), still pointing at him: he is taken; the hand
    // swung, the grip opened or he is no longer pointed at: not.
    internal const float HoldTime=.3f,StillReach=.12f;
    internal enum Hold{Wait,Take,Cancel}
    internal static Hold HoldStep(bool held,bool pointing,bool same,float heldFor,float moved)
    {
        if(!held||!pointing||!same||!float.IsFinite(moved)||moved>StillReach)return Hold.Cancel;
        return float.IsFinite(heldFor)&&heldFor>=HoldTime?Hold.Take:Hold.Wait;
    }
    // Both hands on a hostage: the hand whose grip went down takes him; else
    // the one already showing keeps the prompt; else the right.
    internal static int Side(bool left,bool right,int pressed,int shown)
    {
        if(pressed==1&&right||pressed==0&&left)return pressed;
        if(shown==1&&right||shown==0&&left)return shown;
        return right?1:left?0:-1;
    }
}
