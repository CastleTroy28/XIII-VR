using System;
using Il2CppInterop.Runtime;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
internal sealed partial class WeaponHands
{
    private Equipable? consumedProp;
    private bool breakingProp;
    private bool PropConsumed=>weapon!=null&&consumedProp!=null&&weapon.Pointer==consumedProp.Pointer;
    internal string HandProfile=>visual?.GripProfile??profile;
    private static void SelectAwayFromProp(PlayerEquipableInventory __instance,PlayerEquipableInventory.ActiveEquipmentSlot slot,
        ref bool ignoreEnvironmentalSlotCheck,ref bool ignoreAnimation,out Equipable? __state)
    {
        __state=null;var c=Current;
        if(c==null||!c.enabled||c.disposed||c.inventory==null||c.inventory.Pointer!=__instance.Pointer||c.rig.Scripted)return;
        var item=__instance.currentEquipable;
        if(item==null||item.slot!=PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental||(int)slot<20||(int)slot>=34)return;
        // Native selection otherwise calls UseEnvironmentalWeapon and returns
        // false: VR physical melee never completes that animation-driven use.
        ignoreEnvironmentalSlotCheck=true;ignoreAnimation=true;
        if(!c.breakingProp)__state=item;
    }
    private static void SelectedAwayFromProp(PlayerEquipableInventory __instance,bool __result,Equipable? __state)
    {
        var c=Current;if(!__result||__state==null||c==null)return;
        c.ConsumePropVisual(__state);
        // Selection is committed before removal; never leave an empty inventory
        // when the native game refuses a switch (pause, ladder, cutscene, etc.).
        __instance.Remove(__state);
        Bootstrap.Write("PROP dismissed on deliberate weapon selection");
    }
    private void ConsumePropVisual(Equipable item)
    {
        consumedProp=item;GripCarry.Current?.Forget(item);
        if(weapon!=null&&weapon.Pointer==item.Pointer)
        {StopOwnedFire();ReleaseSupport();ClearThrow();visual?.Hide();poseValid=false;}
    }
    // 0.1.156: a thing the game would not drop, taken out of the hands (GripCarry puts it down).
    // false: not taken out (no inventory bound here).
    internal bool DismissProp(Equipable item)
    {
        if(item==null||inventory==null||item.slot!=PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental)return false;
        ConsumePropVisual(item);
        try{inventory.RemoveAndSwitch(item);}catch(Exception ex){Bootstrap.Warn("PROP put down: "+ex.Message);}
        return true;
    }
    // 0.1.150: a broom / shovel lasts MeleeDamageMath.PropDurability blows on enemies.
    private IntPtr wornItem;private int wornHits;
    internal void WearHeldProp(Equipable item,bool enemy)
    {
        if(item==null)return;
        if(wornItem!=item.Pointer){wornItem=item.Pointer;wornHits=0;}
        if(!enemy)return;
        wornHits++;
        if(wornHits<MeleeDamageMath.PropDurability){Bootstrap.Write("PROP "+item.identifier+" blow "+wornHits+" of "+MeleeDamageMath.PropDurability+" (it holds)");return;}
        Bootstrap.Write("PROP "+item.identifier+" breaks on its blow "+wornHits);
        wornItem=IntPtr.Zero;wornHits=0;BreakHeldProp(item);
    }
    internal void BreakHeldProp(Equipable item)
    {
        if(item==null||item.slot!=PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental||inventory==null||weapon==null
            ||weapon.Pointer!=item.Pointer||consumedProp!=null&&consumedProp.Pointer==item.Pointer)return;
        consumedProp=item;breakingProp=true;
        try
        {
            // Parts can live on child mesh objects. A missing fragment component
            // must never prevent consuming the held item and restoring a weapon.
            foreach(var component in item.GetComponentsInChildren(Il2CppType.Of<DestructableWeaponPart>(),true))
            {
                var part=component.TryCast<DestructableWeaponPart>();if(part==null)continue;
                try
                {
                    if(visual!=null&&part.pieces!=null)
                    {
                        var map=visual.FittedToWorld*visual.GameWorldToFitted;
                        foreach(var piece in part.pieces)if(piece!=null)
                        {
                            var transform=map*piece.transform.localToWorldMatrix;
                            piece.transform.SetPositionAndRotation(transform.MultiplyPoint3x4(Vector3.zero),transform.rotation);
                        }
                    }
                    part.Shatter(item);
                }
                catch(Exception ex){Bootstrap.Warn("PROP fragments: "+ex.Message);}
            }
        }
        finally
        {
            ConsumePropVisual(item);
            try{inventory.RemoveAndSwitch(item);Bootstrap.Write("PROP broken and native previous weapon requested");}
            finally{breakingProp=false;}
        }
    }
}
