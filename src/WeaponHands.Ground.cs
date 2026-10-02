using System;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
// 0.1.126: weapons on the ground. Walking over one takes only its
// ammunition (once); the weapon itself stays where it lies, to be taken with
// the hand (near, or pointed at) - with the grip holding weapons (VR SETTINGS
// "Weapon in hand"; the old way picks weapons up as the game does).
// 0.1.128: the hook on BeginInteractionCheckAndPickUp never ran (the player's
// walk-over does not go through it): the weapon pickup itself (TryPickupItem /
// PickupItem) is held back unless the hand took it.
// 0.1.134: only the rounds of a kind already owned are taken on the walk.
internal sealed partial class WeaponHands
{
    private static bool deliberatePickup;
    private static float nextWalkReport;
    private static bool PlayerActor(IInteractionActor? other,Transform? root)
    {
        if(other==null)return false;
        try{if(other.GetActorTag()==ActorTag.Player)return true;}catch(Exception){}
        try{var go=other.GetGameObject();if(go!=null&&root!=null&&(go.transform==root||go.transform.IsChildOf(root)||root.IsChildOf(go.transform)))return true;}catch(Exception){}
        var me=InteractionDriver.Current?.Actor;
        try{return me!=null&&other.GetActorID()==me.GetActorID();}catch(Exception){return false;}
    }
    // True: the game picks the weapon up (hand pickup, the old way, not the player).
    private static bool AllowWeaponPickup(PickableItem item,IInteractionActor? other,string path)
    {
        var c=Current;
        try
        {
            if(c==null||deliberatePickup||GripMode==WeaponGripMode.Always||c.inventory==null||other==null)return true;
            var w=item.TryCast<WeaponPickup>();if(w==null||w.isAIOnlyWeapon)return true;
            if(!PlayerActor(other,c.playerRoot))return true;
            // 0.1.134: a kind not owned yet keeps its rounds: taken from it on
            // the walk they were lost (the game keeps no reserve of a kind the
            // player does not have), and the weapon came to the hand empty.
            bool owned=false;
            try{var kind=w.weaponPrefab;owned=kind!=null&&c.inventory.HasEquipableInSlot(kind.slot);}catch(Exception){}
            if(!owned)
            {
                if(Time.realtimeSinceStartup>=nextWalkReport){nextWalkReport=Time.realtimeSinceStartup+5;Bootstrap.Write("WALK OVER "+w.name+" ("+path+"): a kind not owned yet, its rounds stay in it (take it with the hand)");}
                return false;
            }
            if(!w.ammoWasTaken)
            {
                var prefab=w.weaponPrefab;var pool=c.inventory.playerAmmo;
                if(prefab!=null&&pool!=null)w.AddWeaponAmmoToPool(prefab,pool);
                w.ammoWasTaken=true;
                try{if(c.playerRoot!=null)w.PlayPickupSound(c.playerRoot.gameObject);}catch(Exception){}
                c.NotifyAmmo();
                Bootstrap.Write("WALK OVER "+w.name+" ("+path+"): ammunition taken, the weapon stays (take it with the hand)");
            }
            else if(Time.realtimeSinceStartup>=nextWalkReport){nextWalkReport=Time.realtimeSinceStartup+5;Bootstrap.Write("WALK OVER "+w.name+" ("+path+"): no ammunition left in it, the weapon stays");}
            return false;
        }
        catch(Exception ex){Bootstrap.Warn("WALK OVER pickup: "+ex.Message);return true;}
    }
    private static bool WalkOverPickup(PickableItem __instance,IInteractionActor other)=>AllowWeaponPickup(__instance,other,"interaction");
    private static bool WalkOverTry(WeaponPickup __instance,IInteractionActor actor,ref bool __result)
    {if(AllowWeaponPickup(__instance,actor,"try"))return true;__result=false;return false;}
    private static bool WalkOverPick(WeaponPickup __instance,IInteractionActor actor,ref bool __result)
    {if(AllowWeaponPickup(__instance,actor,"pick"))return true;__result=false;return false;}
    // Whatever path still hands a weapon over, the log says so.
    private static void WeaponGiven(WeaponPickup __instance)
    {try{Bootstrap.Write("PICKUP weapon given: "+__instance.name+" hand="+deliberatePickup+" mode="+GripMode);}catch(Exception){}}
}
