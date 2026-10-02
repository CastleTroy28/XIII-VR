using System;
using UnityEngine;
using Slot=PlayerEquipableInventory.ActiveEquipmentSlot;
namespace XiiiXR;
// 0.1.195: the grappling hook and the zipline hook in either hand;
// riding a zipline, one hand on the hook, the other one shoots. The hand that
// holds a hook does not hold the game's weapon: it goes to the other hand
// (as the left hand holds it when it takes it), and back when the hook leaves.
internal sealed partial class WeaponHands
{
    private bool toolMovedWeapon;
    // Hand `right` may take a hook over from the other hand.
    internal bool CanTakeTool(bool right)
    {
        int s=right?1:0,o=1-s;
        if(copyKey[s]>=0||GripCarry.Current?.HidesHand(right)==true||DualActive)return false;
        if(right?ReloadHandHolding(true):LeftReloadHolding)return false;
        // The game's weapon leaves that hand for the other one: that one must be free.
        if(weapon!=null&&GameSide(CurrentKey)==s&&(copyKey[o]>=0||GripCarry.Current?.HidesHand(o==1)==true))return false;
        return true;
    }
    private void KeepWeaponOffTool()
    {
        var ui=GameUiControls.Current;if(ui==null||inventory==null)return;
        bool right=ui.RightItemHeld,left=ui.LeftItemHeld;int key=CurrentKey;
        bool movable=key>=0&&weapon!=null&&!DualActive&&(HolsterLayout.Firearm(profile)||key==(int)Slot.Enviromental);
        if(right&&!left)
        {
            if(!movable||copyKey[0]>=0||GripCarry.Current?.HidesLeft==true)return;
            if(leftGameKey!=key)
            {
                leftGameKey=key;leftTriggerLocked=true;toolMovedWeapon=true;
                Bootstrap.Write("HANDS the right hand holds a hook: "+profile+" goes to the left hand (the left trigger fires it)");
            }
            leftGameSince=Time.realtimeSinceStartup;
            return;
        }
        if(left&&!right&&movable&&leftGameKey==key&&copyKey[1]<0)
        {
            RightHandHas(key);Bootstrap.Write("HANDS the left hand holds a hook: "+profile+" goes to the right hand");return;
        }
        if(toolMovedWeapon&&!right)
        {
            toolMovedWeapon=false;
            if(movable&&leftGameKey==key&&!LeftHanded&&copyKey[1]<0){RightHandHas(key);Bootstrap.Write("HANDS the hook left the right hand: "+profile+" back in the right hand");}
        }
    }
}
