using System;
using Il2CppInterop.Runtime;
namespace XiiiXR;
internal sealed partial class WeaponHands
{
    private static bool LocalArms(PlayerArmsAnimationControl arms)
    {
        try{var c=Current;return c!=null&&c.enabled&&c.playerRoot!=null&&arms.owner.IsPlayer&&arms.owner.Id==c.playerId;}catch{return false;}
    }
    private static bool AllowIdleTimer(PlayerArmsAnimationControl __instance)=>!LocalArms(__instance);
    private static bool CanIdle(PlayerArmsAnimationControl __instance,ref bool __result)
    {if(!LocalArms(__instance))return true;__result=false;return false;}
    private static bool AllowWeaponIdle(PlayMagic.Weapons.AnimationComponent __instance)
    {
        try{var c=Current;return c==null||!c.enabled||c.playerRoot==null||!__instance.transform.IsChildOf(c.playerRoot);}catch{return true;}
    }
    private void StopIdleFlourish()
    {
        if(playerRoot==null)return;
        try
        {
        foreach(var obj in playerRoot.GetComponentsInChildren(Il2CppType.Of<PlayerArmsAnimationControl>(),true))
        {
            var arms=obj.TryCast<PlayerArmsAnimationControl>();if(arms==null||!LocalArms(arms))continue;
            arms.StopIdleTimer();arms.IdleBreak_AnimTrigger=false;arms.BodyIdleBreak_AnimTrigger=false;
            // Cancel an already-running flourish through the stock idle transition.
            if(arms.idleTriggered){arms.Idle_AnimTrigger=true;arms.idleTriggered=false;}
        }
        Bootstrap.Write("WEAPON IDLE automatic local-player flourish disabled");
        }catch(Exception ex){Bootstrap.Warn("WEAPON IDLE existing animation cancellation: "+ex.Message);}
    }
}
