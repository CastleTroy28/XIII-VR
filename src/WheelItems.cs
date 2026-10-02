using System;
using UnityEngine;
namespace XiiiXR;
// Hold the original item mesh without starting its native use/equip coroutine.
// A later trigger press starts native healing and weapon restoration.
internal sealed class WheelItems
{
    private readonly CameraRig rig;
    private InventoryWheel? wheel;
    private ConsumablesSlot? hovered;
    private readonly WheelGadgets gadgets=new();
    private Color previousColor;
    private PlayerEquipableInventory.ActiveEquipmentSlot? pending;
    private bool armed;
    private HeldItemVisual? visual;
    internal bool Pending=>pending.HasValue;
    // 0.1.186: a medkit taken from a forearm (ArmMedkits) by the grip of this
    // hand (0 left, 1 right): held while that grip is held (let go: it goes
    // back on the forearm), used with that hand's trigger. The game's weapon
    // stays in the other hand (only the wheel's item blocks it).
    private int armSide=-1;private PlayerEquipableInventory? armInventory;
    // The model let go of goes back to its forearm (ArmMedkits), not rebuilt.
    internal Action<int,HeldItemVisual>? PutBack=null;
    internal int ArmSide=>pending.HasValue?armSide:-1;
    internal bool ArmHeld(bool right)=>ArmSide==(right?1:0);
    internal int ArmSlot=>ArmSide>=0?(int)pending!.Value:-1;
    internal void TakeFromArm(PlayerEquipableInventory.ActiveEquipmentSlot slot,int side,HeldItemVisual item,PlayerEquipableInventory inventory)
    {
        ClearHover();visual?.Dispose();visual=item;toolKind=HandToolKind.None;armInventory=inventory;
        pending=slot;armSide=side;armed=false;Disarm(side);
    }
    // That hand's trigger does nothing else (a gun, a magazine at the belt) while it holds the medkit.
    private void Disarm(int side){if(side==0)rig.DisarmLeftTrigger();else rig.DisarmTrigger();}
    // 0.1.84: the grappling hook is held and fired with the LEFT hand so the
    // right hand keeps its weapon. It stays in the left hand while the native
    // hook is in use. The hook's inventory slot is "Gadget" (33) in the game
    // (0.1.84 log), so it is recognised by the item itself.
    // 0.1.195: the grappling hook and the zipline hook (slot 16,
    // until now taken for the grappling hook) are held in either hand: the
    // other hand's grip at the holding hand takes it over (as a weapon), the
    // right trigger fires the hook from the right hand. The game's weapon
    // goes to the other hand (WeaponHands.KeepWeaponOffTool).
    private HandToolKind toolKind;private int toolSide;
    internal bool HandTool=>pending.HasValue&&toolKind!=HandToolKind.None;
    internal HandToolKind Kind=>HandTool?toolKind:HandToolKind.None;
    internal int ToolSide=>HandTool?toolSide:inUse!=null?inUseSide:-1;
    internal bool LeftTool=>HandTool&&toolSide==0;
    internal bool RightTool=>HandTool&&toolSide==1;
    internal bool GrappleTool=>HandTool&&toolKind==HandToolKind.Grapple;
    internal bool ZiplineTool=>HandTool&&toolKind==HandToolKind.Zipline;
    private HeldItemVisual? inUse;private int inUseSide;private float inUseSince;
    internal bool LeftHeld=>LeftTool||inUse!=null&&inUseSide==0;
    internal bool RightHeld=>RightTool||inUse!=null&&inUseSide==1;
    internal bool HeldOn(bool right)=>right?RightHeld:LeftHeld;
    // Where the tool is held (for the other hand to take it over).
    private readonly Vector3[] toolAt=new Vector3[2];private readonly bool[] toolAtValid=new bool[2];
    // 0.1.103: the lockpick is held like a key (pinched handle, pick forward).
    internal bool PinchHeld=>pending==PlayerEquipableInventory.ActiveEquipmentSlot.Lockpick&&visual!=null&&toolKind==HandToolKind.None;
    internal void PrepareHand(NativeHandVisual hand)=>visual?.PreparePinch(hand);
    internal string GripProfile=>PinchHeld?ScrewdriverGrip.Profile:Tool?"gadget":pending==PlayerEquipableInventory.ActiveEquipmentSlot.MedkitL?"medkit_l":pending.HasValue?"medkit_s":"";
    private bool Tool=>pending.HasValue&&(int)pending.Value!=9&&(int)pending.Value!=10;
    internal string Notice=>GrappleTool?(toolSide==0?"Left trigger: grappling hook":"Right trigger: grappling hook"):ZiplineTool?"Point the hook at the zipline cable":Tool?"Grip+A / Trigger: use tool":"";
    internal bool Hovered=>hovered!=null||gadgets.Item!=null;
    internal Vector3 Point;
    internal WheelItems(CameraRig camera){rig=camera;}
    private float nextZiplineProbe;private bool ziplineRearm;
    internal void Bind(InventoryWheel? value){if(wheel==value)return;Clear();wheel=value;}
    internal void ClearHover()
    {if(hovered?.icon!=null)hovered.icon.color=previousColor;hovered=null;gadgets.Clear();}
    // A hook in a hand is kept (it stays in that hand while it is in use).
    internal void ClearTool(){if(Tool&&!HandTool)Clear();}
    internal void Clear(){ClearHover();pending=null;armSide=-1;armInventory=null;armed=false;visual?.Dispose();visual=null;inUse?.Dispose();inUse=null;toolKind=HandToolKind.None;}
    internal void Hover()
    {
        ConsumablesSlot? found=null;
        if(wheel!=null&&wheel.IsOpen&&wheel.consumableSlots!=null&&rig.SamplePointerHand(out var hand))
        {
            var origin=CameraRig.UnityPosition(hand);var direction=rig.PointerRotation(hand)*Vector3.forward;
            foreach(var slot in wheel.consumableSlots)
            {
                if(slot==null||slot.itemPrefab==null||slot.icon==null||!slot.icon.gameObject.activeInHierarchy)continue;
                var kind=slot.itemPrefab.slot;
                if(kind!=PlayerEquipableInventory.ActiveEquipmentSlot.MedkitS&&kind!=PlayerEquipableInventory.ActiveEquipmentSlot.MedkitL)continue;
                var r=slot.icon.rectTransform;
                if(!MenuRayMath.Plane(ContactWorld.V(origin),ContactWorld.V(direction),ContactWorld.V(r.position),ContactWorld.V(r.forward),out var p,out _))continue;
                var local=r.InverseTransformPoint(ContactWorld.U(p));
                if(!r.rect.Contains(new Vector2(local.x,local.y)))continue;
                found=slot;Point=ContactWorld.U(p);break;
            }
        }
        if(found!=hovered){ClearHover();hovered=found;if(hovered?.icon!=null){previousColor=hovered.icon.color;hovered.icon.color=new Color(1,.8f,.25f,1);}}
        if(found==null&&rig.SamplePointerHand(out var toolHand))
        {gadgets.Hover(wheel,CameraRig.UnityPosition(toolHand),rig.PointerRotation(toolHand)*Vector3.forward);if(gadgets.Item!=null)Point=gadgets.Point;}
        else gadgets.Clear();
    }
    internal bool Commit()
    {
        var source=hovered?.itemPrefab??gadgets.Item;
        if(source==null||wheel?.playerInventory==null)return false;
        var slot=source.slot;
        bool tool=(int)slot!=9&&(int)slot!=10;
        if(tool?!wheel.playerInventory.HasEquipableInSlot(slot):!wheel.playerInventory.HasStacksForConsumable(slot)){ClearHover();return false;}
        HeldItemVisual next;
        try{next=HeldItemVisual.Create(wheel.playerInventory.GetEquipableFromSlot(slot)??source);}
        catch(Exception ex){Bootstrap.Warn("HELD ITEM unavailable: "+ex.Message);return false;}
        visual?.Dispose();visual=next;
        var item=wheel.playerInventory.GetEquipableFromSlot(slot)??source;
        // The hovered wheel entry and the inventory's own item: either may carry the name.
        toolKind=HandTools.Kind((int)slot,(item.name??"")+" "+(item.identifier??"")+" "+(source.name??"")+" "+(source.identifier??""));
        // A hand tool starts in the hand that is not the weapon hand (the left one; the right one for a left-hander).
        if(toolKind!=HandToolKind.None)toolSide=WeaponHands.LeftHanded?1:0;
        pending=slot;armSide=-1;armed=false;rig.DisarmTrigger();if(toolKind!=HandToolKind.None)rig.DisarmLeftTrigger();
        Bootstrap.Write("WHEEL ITEM preview="+slot+(toolKind!=HandToolKind.None?" ("+toolKind+" in the "+(toolSide==0?"left":"right")+" hand; the other hand's grip at it takes it over)":"")+"; inventory unchanged; trigger release required");ClearHover();return true;
    }
    // 0.1.197: the renderer the held copy was built in (the zipline hook's hold).
    internal string HeldBodyName=>visual?.BodyName??"";
    // 0.1.198: the zipline hook held by its handle as a pistol by its grip:
    // the hand turned as on a pistol (GloveVisual asks), the hook in its frame.
    private readonly Vector3[] barAt=new Vector3[2];private readonly Quaternion[] barTurn={Quaternion.identity,Quaternion.identity};private readonly int[] barFrame={-10,-10};private float barScale=1;
    internal bool TryToolHand(bool right,PoseValue pose,out Vector3 position,out Quaternion rotation)
    {
        position=Vector3.zero;rotation=Quaternion.identity;int s=right?1:0;
        if(!HandTool||toolKind!=HandToolKind.Zipline||toolSide!=s||visual?.GripBar is not GripBarMath.Bar bar||ZiplineVr.Current?.Riding==true)return false;
        // At its own size (fitted to 20 cm as a gadget): its handle a fist thick.
        barScale=visual.TrueScale;bar=GripBarMath.Scaled(bar,barScale);
        var hands=WeaponHands.Current;if(hands==null||!hands.TryToolHold(right,pose,bar,out position,out rotation,out barAt[s],out barTurn[s]))return false;
        if(barFrame[s]<0)Bootstrap.Write("ZIPLINE hook held by its handle in the "+(right?"right":"left")+" hand, as a pistol by its grip ("+barScale.ToString("F2")+" x its fitted size, the handle "+bar.Thickness.ToString("F3")+" m thick)");
        barFrame[s]=Time.frameCount;return true;
    }
    internal void Render(Transform? hand,Transform? left=null)
    {
        bool previewed=false;
        toolAtValid[0]=toolAtValid[1]=false;
        // Fired some other way (native interaction): the hook is in use now.
        if(GrappleTool&&visual!=null&&GrappleVr.Current?.Active==true)
        {inUse?.Dispose();inUse=visual;inUseSide=toolSide;visual=null;inUseSince=Time.realtimeSinceStartup;ClearHover();pending=null;armed=false;toolKind=HandToolKind.None;Bootstrap.Write("GRAPPLE in use; the "+(inUseSide==0?"left":"right")+" hand keeps the hook");}
        if(inUse!=null)
        {
            // Keep the device in its hand while the native hook is used.
            // While the game's own hook is out, it is shown (in this hand) instead.
            bool active=GrappleVr.Current?.Active==true;var h=inUseSide==0?left:hand;
            if(active&&GrappleVr.Current!.DeviceShown)inUse.Hide();
            else if(active||Time.realtimeSinceStartup-inUseSince<1.5f)previewed|=PoseGrapple(inUse,h,inUseSide);
            else{inUse.Dispose();inUse=null;}
            if(h!=null){toolAt[inUseSide]=h.position;toolAtValid[inUseSide]=true;}
        }
        if(HandTool&&visual!=null)
        {
            var h=toolSide==0?left:hand;
            if(h!=null){toolAt[toolSide]=h.position;toolAtValid[toolSide]=true;}
            if(toolKind==HandToolKind.Grapple)previewed|=PoseGrapple(visual,h,toolSide);
            // 0.1.195: the zipline hook in its hand (on the cable while riding: ZiplineVr draws it there).
            else if(ZiplineVr.Current?.Riding==true)visual.Hide();
            // 0.1.198: by its handle, as a pistol by its grip (the hand posed so: TryToolHand);
            // 0.1.197: else as the game's own arm holds it; the fitted hold until that is known.
            else if(h!=null&&visual.GripBar!=null&&Time.frameCount-barFrame[toolSide]<=2)visual.PoseRoot(h.position+h.rotation*barAt[toolSide],h.rotation*barTurn[toolSide],barScale);
            else if(ZiplineVr.Current?.TryHeld(toolSide,visual)!=true)visual.PoseIn(h,toolSide==0);
        }
        if(!previewed)GrappleVr.Current?.HidePreview();
        if(!Pending){visual?.Hide();return;}
        if(HandTool)return;
        // 0.1.150: a left-hander holds medkits and the lockpick in the left hand.
        if(armSide>=0)visual?.PoseIn(armSide==0?left:hand,armSide==0);
        else visual?.PoseIn(WeaponHands.LeftHanded?left:hand,WeaponHands.LeftHanded);
    }
    // 0.1.88: a render-only copy of the game's own hook on the VR wrist
    // (same attachment as on the rope); the generic fit only until the game's
    // hook is known. 0.1.195: in either hand (the right one mirrored).
    private static bool PoseGrapple(HeldItemVisual item,Transform? hand,int side)
    {
        if(hand!=null&&GrappleVr.Current?.ShowPreview(hand,side)==true){item.Hide();return true;}
        item.PoseIn(hand,side==0);return false;
    }
    // The other hand's grip at the hand holding the tool takes it over.
    private void TickPass(bool allowed)
    {
        if(!allowed||!HandTool||ZiplineVr.Current?.Riding==true)return;
        int other=1-toolSide;var c=other==0?rig.LeftControls:rig.RightControls;
        if(!c.Valid||(c.Down&HandControls.Grip)==0||!toolAtValid[toolSide])return;
        if(!rig.SampleWorldHands(out var l,out var r,out bool leftValid)||other==0&&!leftValid)return;
        var at=CameraRig.UnityPosition(other==0?l:r);
        if(!HandTools.Reaches(ContactWorld.V(at),ContactWorld.V(toolAt[toolSide])))return;
        if(WeaponHands.Current?.CanTakeTool(other==1)==false){rig.ResistanceHaptics(.3f,other==1);return;}
        int from=toolSide;toolSide=other;armed=false;
        if(other==0)rig.DisarmLeftTrigger();else rig.DisarmTrigger();
        rig.ReloadHaptics(ReloadAction.TakeSupply,other==1);
        Bootstrap.Write("WHEEL ITEM "+toolKind+" passed from the "+(from==0?"left":"right")+" hand to the "+(other==0?"left":"right")+" hand (the game's weapon goes to the other hand)");
    }
    internal void Tick(bool allowed)
    {
        if(!Pending)return;
        int arm=armSide;
        if(arm>=0)
        {
            var hand=arm==0?rig.LeftControls:rig.RightControls;
            if(!hand.Valid||(hand.Held&HandControls.Grip)==0)
            {
                var back=pending!.Value;var model=visual;visual=null;Clear();
                if(model!=null){if(PutBack!=null)PutBack((int)back,model);else model.Dispose();}
                Bootstrap.Write("ARM MEDKIT "+back+" back on the forearm (the "+(arm==0?"left":"right")+" grip let go)");return;
            }
        }
        if(!allowed){armed=false;return;}
        TickPass(allowed);
        bool inHand=HandTool;int side=toolSide;
        // 0.1.195: the zipline hook starts the ride as soon as it points at the cable (its trigger too).
        if(inHand&&toolKind==HandToolKind.Zipline)
        {
            if(ZiplineVr.Current?.Riding==true){armed=false;ziplineRearm=true;return;}
            var zc=side==0?rig.LeftControls:rig.MenuRightControls;
            bool pressed=zc.Valid&&(zc.Down&HandControls.Trigger)!=0;
            if(pressed||Time.realtimeSinceStartup>=nextZiplineProbe)
            {
                nextZiplineProbe=Time.realtimeSinceStartup+.12f;
                // After a ride the hook first points away from the zipline (at its end
                // the cable is still there: no ride back by itself; the trigger still starts one).
                if(ziplineRearm&&!pressed)
                {
                    if(InteractionDriver.Current?.ZiplineAt(side)!=true){ziplineRearm=false;Bootstrap.Write("ZIPLINE the hook points away from the zipline: pointing at a cable rides again");}
                    return;
                }
                ziplineRearm=false;
                if(InteractionDriver.Current?.TryZipline(side,pressed)==true){ZiplineVr.Current?.StartedBy(side);Bootstrap.Write("ZIPLINE the hook in the "+(side==0?"left":"right")+" hand pointed at the cable: riding");}
            }
            return;
        }
        var input=arm>=0?(arm==0?rig.MenuLeftControls:rig.MenuRightControls):inHand?(side==0?rig.LeftControls:rig.MenuRightControls):WeaponHands.LeftHanded?rig.LeftControls:rig.MenuRightControls;
        if(!input.Valid){Clear();return;}
        if((input.Held&HandControls.Trigger)==0){armed=true;return;}
        if(!armed)return;armed=false;
        var slot=pending!.Value;var inventory=arm>=0?armInventory??wheel?.playerInventory:wheel?.playerInventory;
        if(Tool)
        {
            // The hook goes to the hand it is fired from (placed there before the game hangs it).
            if(inHand&&GrappleVr.Current!=null)GrappleVr.Current.Side=side;
            if(InteractionDriver.Current?.TryUseTool(slot,inHand?side:WeaponHands.LeftHanded?0:1)!=true)return;
            if(inHand)
            {
                if(visual!=null){inUse?.Dispose();inUse=visual;inUseSide=side;visual=null;inUseSince=Time.realtimeSinceStartup;}
                if(side==0)rig.DisarmLeftTrigger();else rig.DisarmTrigger();
                Bootstrap.Write("GRAPPLE fired from the "+(side==0?"left":"right")+" hand");
            }
            else rig.DisarmTrigger();
            ClearHover();pending=null;armed=false;toolKind=HandToolKind.None;visual?.Dispose();visual=null;return;
        }
        // A medkit from a forearm stays in the hand while the game cannot use it
        // (switching weapons): the trigger again once it can.
        if(arm>=0&&(inventory==null||inventory.IsInventoryBlocked||inventory.isInTransit))
        {Disarm(arm);Bootstrap.Write("ARM MEDKIT "+slot+" not used yet: the game does not allow it this moment (still in the hand)");return;}
        Clear();if(arm>=0)Disarm(arm);else rig.DisarmTrigger();
        if(inventory==null||inventory.IsInventoryBlocked||inventory.isInTransit||!inventory.HasStacksForConsumable(slot))return;
        inventory.TryUsingConsumable(slot);Bootstrap.Write(arm>=0?"ARM MEDKIT deliberate native use="+slot+" (the "+(arm==0?"left":"right")+" trigger)":"WHEEL ITEM deliberate native use="+slot);
    }
}
