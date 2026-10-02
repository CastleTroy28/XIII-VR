using System;
using UnityEngine;
using PlayMagic.Weapons;
using Slot=PlayerEquipableInventory.ActiveEquipmentSlot;
namespace XiiiXR;
// 0.1.186. While one is owned, a tiny model of the small medkit
// sits on the left forearm's cut and of the large one on the right forearm's.
// The other hand's grip there takes it into that hand (the medkit's own
// size, its grip): held while the grip is held, the trigger of that hand
// uses it (the game's own healing), letting go puts it back.
internal sealed class ArmMedkits : IDisposable
{
    private readonly CameraRig rig;
    private readonly WheelItems items;
    private readonly HeldItemVisual?[] models=new HeldItemVisual?[2];
    private readonly float[] retryAt=new float[2];
    private readonly bool[] stocked=new bool[2],reported=new bool[2],placed=new bool[2],near=new bool[2];
    private readonly Vector3[] at=new Vector3[2];
    private float nextStock;
    internal ArmMedkits(CameraRig camera,WheelItems held){rig=camera;items=held;items.PutBack=PutBack;}
    // A medkit let go of: its model back on its forearm.
    private void PutBack(int slot,HeldItemVisual model)
    {
        int a=ArmMedkitMath.ArmOf(slot);
        if(a<0||models[a]!=null){model.Dispose();return;}
        model.Hide();models[a]=model;placed[a]=false;
    }
    private static string Side(int s)=>s==0?"left":"right";
    private static string Kind(int arm)=>ArmMedkitMath.Large(arm)?"large":"small";
    // Every frame of play (GameUiControls, before the weapons and the interactions).
    internal void Tick(bool allowed,PlayerEquipableInventory? inventory,InventoryWheel? wheel)
    {
        float now=Time.realtimeSinceStartup;
        if(inventory==null){stocked[0]=stocked[1]=false;near[0]=near[1]=false;return;}
        if(now>=nextStock)
        {
            nextStock=now+.25f;
            for(int a=0;a<2;a++)
            {
                bool have;try{have=inventory.HasStacksForConsumable((Slot)ArmMedkitMath.Slot(a));}catch(Exception){have=false;}
                if(have!=stocked[a]||!reported[a])
                {
                    reported[a]=true;
                    Bootstrap.Write(have?"ARM MEDKIT "+Kind(a)+" medkit on the "+Side(a)+" forearm's cut (the "+Side(1-a)+" hand's grip there takes it)":"ARM MEDKIT no "+Kind(a)+" medkit: nothing on the "+Side(a)+" forearm");
                }
                stocked[a]=have;
            }
        }
        // One model made a frame, never while its medkit is in a hand.
        for(int a=0;a<2;a++)
        {
            if(!stocked[a]||models[a]!=null||now<retryAt[a]||items.ArmSlot==ArmMedkitMath.Slot(a))continue;
            var slot=(Slot)ArmMedkitMath.Slot(a);var source=Source(inventory,wheel,slot);
            if(source==null){retryAt[a]=now+2;continue;}
            try{models[a]=HeldItemVisual.Create(source);models[a]!.Hide();}
            catch(Exception ex){retryAt[a]=now+10;Bootstrap.Warn("ARM MEDKIT "+Kind(a)+" model unavailable: "+ex.Message);}
            break;
        }
        // Not while climbing (the grips hold the ladder).
        if(!allowed||items.Pending||items.LeftHeld||InteractionDriver.Current?.ClimbingActive==true||!rig.SampleWorldHands(out var l,out var r,out bool leftValid)){near[0]=near[1]=false;return;}
        var hands=WeaponHands.Current;
        for(int h=1;h>=0;h--)
        {
            var c=h==0?rig.LeftControls:rig.RightControls;
            int arm=ArmMedkitMath.ArmFor(h);
            if(h==0&&!leftValid||!c.Valid||!stocked[arm]||!placed[arm]||models[arm]==null){near[h]=false;continue;}
            var hand=CameraRig.UnityPosition(h==0?l:r);
            bool close=ArmMedkitMath.Reach(ToN(hand),ToN(at[arm]))&&(hands==null||hands.HandTakesItem(h,hand));
            if(close&&!near[h])rig.ResistanceHaptics(.55f,h==1);
            near[h]=close;
            if(!close||(c.Down&HandControls.Grip)==0)continue;
            if(Take(arm,h,inventory))break;
        }
    }
    private bool Take(int arm,int hand,PlayerEquipableInventory inventory)
    {
        var slot=(Slot)ArmMedkitMath.Slot(arm);
        bool have;try{have=inventory.HasStacksForConsumable(slot);}catch(Exception){have=false;}
        var item=models[arm];
        if(!have||item==null){stocked[arm]=have;return false;}
        models[arm]=null;placed[arm]=false;near[0]=near[1]=false;
        items.TakeFromArm(slot,hand,item,inventory);
        rig.PunchHaptics(hand==1);
        Bootstrap.Write("ARM MEDKIT the "+Side(hand)+" grip takes the "+Kind(arm)+" medkit from the "+Side(arm)+" forearm (held while the grip is held; the "+Side(hand)+" trigger uses it; letting go puts it back)");
        return true;
    }
    private static Equipable? Source(PlayerEquipableInventory inventory,InventoryWheel? wheel,Slot slot)
    {
        try{var e=inventory.GetEquipableFromSlot(slot);if(e!=null)return e;}catch(Exception){}
        try
        {
            var slots=wheel?.consumableSlots;
            if(slots!=null)foreach(var s in slots)if(s!=null&&s.itemPrefab!=null&&s.itemPrefab.slot==slot)return s.itemPrefab;
        }
        catch(Exception){}
        return null;
    }
    // Before drawing, after the hands (WristHud): each model on its forearm's cut.
    internal void Render(GloveVisual? left,GloveVisual? right)
    {
        for(int a=0;a<2;a++)
        {
            placed[a]=false;var model=models[a];if(model==null)continue;
            var glove=a==0?left:right;
            if(!stocked[a]||glove==null||!glove.TryCropEnd(out var cut,out var outward,out var up)){model.Hide();continue;}
            bool large=ArmMedkitMath.Large(a);var size=MedkitGeometry.Size(large);float scale=ArmMedkitMath.Scale(large,size.X);
            var center=ArmMedkitMath.Center(ToN(cut),ToN(outward),size.Y*scale);
            var front=ArmMedkitMath.Front(ToN(up),ToN(outward));
            var fitted=MedkitGeometry.Center(large);
            at[a]=new Vector3(center.X,center.Y,center.Z);
            model.PoseSmall(at[a],Quaternion.LookRotation(new Vector3(front.X,front.Y,front.Z),outward),new Vector3(fitted.X,fitted.Y,fitted.Z),scale);
            placed[a]=true;
        }
    }
    internal void Hide(){for(int a=0;a<2;a++){placed[a]=false;models[a]?.Hide();}near[0]=near[1]=false;}
    internal void Reset()
    {
        for(int a=0;a<2;a++){models[a]?.Dispose();models[a]=null;placed[a]=false;stocked[a]=false;reported[a]=false;retryAt[a]=0;}
        near[0]=near[1]=false;nextStock=0;
    }
    public void Dispose()=>Reset();
    private static System.Numerics.Vector3 ToN(Vector3 v)=>new(v.x,v.y,v.z);
}
