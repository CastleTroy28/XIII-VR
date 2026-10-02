using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;
using UI.HUD;
using PlayMagic.Weapons;
namespace XiiiXR;
// Present the native white Y-cycle icons; inventory/equipping remains native.
internal sealed class CycleIcons : IDisposable
{
    private sealed class Strip
    {
        internal WeaponInventoryIndicator Ui=null!;
        internal readonly Dictionary<int,PlayerEquipableInventory.ActiveEquipmentSlot> Slots=new();
        internal readonly Dictionary<PlayerEquipableInventory.ActiveEquipmentSlot,int> Equipment=new();
        internal Color PanelColor;
        internal bool Reused;
        internal readonly List<Image> Icons=new();
        internal CanvasGroup? Group;internal bool OwnGroup;internal float Alpha;
        internal Vector2 Size;internal bool HasSize,PanelSaved,PanelEnabled;internal int Index=-1;
    }
    private readonly List<(Image image,bool enabled,Color color,bool icon)> saved=new();
    private readonly Dictionary<int,Strip> strips=new();
    private readonly Harmony patches=new("xiii.vr.xrbootstrap.cycleicons");
    private static CycleIcons? current;
    private float showUntil;
    internal CycleIcons()
    {
        try
        {
            Patch("GenerateImageObject",nameof(Generated));
            Patch("PopulateEquipables",nameof(Populated));
            patches.Patch(AccessTools.DeclaredMethod(typeof(WeaponInventoryIndicator),"PopulateEquipables"),prefix:new HarmonyMethod(typeof(CycleIcons),nameof(Populating)));
            Patch("PositionSelectedIndicator",nameof(Selected));
            current=this;
        }
        catch(Exception ex){patches.UnpatchSelf();Bootstrap.Warn("native cycle-strip tracking unavailable: "+ex.Message);}
    }
    private void Patch(string method,string hook)=>patches.Patch(AccessTools.DeclaredMethod(typeof(WeaponInventoryIndicator),method),postfix:new HarmonyMethod(typeof(CycleIcons),hook));
    private Strip Get(WeaponInventoryIndicator ui)
    {
        int id=ui.GetInstanceID();if(!strips.TryGetValue(id,out var s)){s=new Strip{Ui=ui};strips[id]=s;}return s;
    }
    // PopulateEquipables contains an INLINED PositionSelectedIndicator in this
    // IL2CPP binary. Record each generated object's real slot instead of relying
    // on an index hook that is not called on a normal weapon change.
    private static void Generated(WeaponInventoryIndicator __instance,PlayerEquipableInventory.ActiveEquipmentSlot weaponSlot,GameObject __result)
    {if(current!=null&&__result!=null)current.Get(__instance).Slots[__result.GetInstanceID()]=weaponSlot;}
    private static void Populated(WeaponInventoryIndicator __instance,Equipable newWeapon)
    {
        if(current==null)return;var s=current.Get(__instance);
        if(s.Reused){current.HidePanel(s);return;}
        var r=__instance.transform.TryCast<RectTransform>();
        if(r!=null&&r.sizeDelta.x>0&&r.sizeDelta.y>0){s.Size=r.sizeDelta;s.HasSize=true;}
        var live=new HashSet<int>();if(__instance.displayedWeapons!=null)foreach(var o in __instance.displayedWeapons)if(o!=null)live.Add(o.GetInstanceID());
        var stale=new List<int>();foreach(var id in s.Slots.Keys)if(!live.Contains(id))stale.Add(id);foreach(int id in stale)s.Slots.Remove(id);
        current.DiscoverStrip(s);
        s.Equipment.Clear();var inventory=__instance.playerInventory??__instance.playerEquipableInventory;
        if(inventory?.activeEquipment!=null)foreach(var pair in inventory.activeEquipment)
            if(pair.Value!=null)s.Equipment[pair.Key]=pair.Value.GetInstanceID();
        current.HidePanel(s);
    }
    // Native PopulateEquipables destroys/reinstantiates the same icon strip on
    // every switch. Reuse it while the actual equipment set is unchanged.
    private static bool Populating(WeaponInventoryIndicator __instance)
    {
        if(current==null)return true;var s=current.Get(__instance);s.Reused=false;
        var inv=__instance.playerInventory??__instance.playerEquipableInventory;
        if(inv?.activeEquipment==null||s.Equipment.Count==0||s.Equipment.Count!=inv.activeEquipment.Count||__instance.displayedWeapons==null||__instance.displayedWeapons.Count==0)return true;
        foreach(var o in __instance.displayedWeapons)if(o==null)return true;
        foreach(var p in inv.activeEquipment)if(p.Value==null||!s.Equipment.TryGetValue(p.Key,out int id)||id!=p.Value.GetInstanceID())return true;
        current.HidePanel(s);s.Reused=true;return false;
    }
    private static void Selected(WeaponInventoryIndicator __instance,int activeWeaponIndex)
    {if(current!=null){var s=current.Get(__instance);s.Index=activeWeaponIndex;current.HidePanel(s);}}
    internal static void Request(){if(current!=null)current.showUntil=Time.realtimeSinceStartup+2;}
    internal static Vector2 LayoutSize(Canvas canvas,Vector2 fallback)
    {if(current!=null)foreach(var s in current.strips.Values)if(s.Ui!=null&&s.Ui.gameObject==canvas.gameObject&&s.HasSize)return s.Size;return fallback;}
    private int Index(Strip s)
    {
        var ui=s.Ui;var selected=(ui.playerInventory??ui.playerEquipableInventory)?.currentEquipable;
        if(selected!=null&&ui.displayedWeapons!=null)for(int i=0;i<ui.displayedWeapons.Count;i++)
        {var o=ui.displayedWeapons[i];if(o!=null&&s.Slots.TryGetValue(o.GetInstanceID(),out var slot)&&slot==selected.slot)return i;}
        return s.Index;
    }
    internal void Discover()
    {
        saved.RemoveAll(x=>x.image==null);
        var stale=new List<int>();foreach(var pair in strips)if(pair.Value.Ui==null)stale.Add(pair.Key);foreach(int id in stale)strips.Remove(id);
        foreach(var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<WeaponInventoryIndicator>()))
        {
            var ui=o.TryCast<WeaponInventoryIndicator>();if(ui!=null&&ui.gameObject.scene.IsValid())DiscoverStrip(Get(ui));
        }
    }
    private void DiscoverStrip(Strip s)
    {
        var ui=s.Ui;if(ui.inventoryGroup==null)return;
        if(!s.PanelSaved&&ui.selectedWeaponPanel!=null){s.PanelSaved=true;s.PanelEnabled=ui.selectedWeaponPanel.enabled;s.PanelColor=ui.selectedWeaponPanel.color;}
        if(s.Group==null)
        {
            s.Group=ui.inventoryGroup.GetComponent(Il2CppType.Of<CanvasGroup>())?.TryCast<CanvasGroup>();s.OwnGroup=s.Group==null;
            s.Group??=ui.inventoryGroup.AddComponent(Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>();s.Alpha=s.Group==null?1:s.Group.alpha;
        }
        var icons=new HashSet<int>();
        s.Icons.Clear();
        if(ui.displayedWeapons!=null)foreach(var o in ui.displayedWeapons)
        {var h=o?.GetComponentInChildren(Il2CppType.Of<UiWeaponIconHolder>(),true)?.TryCast<UiWeaponIconHolder>();if(h?.weaponSprite!=null)s.Icons.Add(h.weaponSprite);}
        foreach(var c in ui.inventoryGroup.GetComponentsInChildren(Il2CppType.Of<UiWeaponIconHolder>(),true))
        {var h=c.TryCast<UiWeaponIconHolder>();if(h?.weaponSprite!=null)icons.Add(h.weaponSprite.GetInstanceID());}
        foreach(var c in ui.inventoryGroup.GetComponentsInChildren(Il2CppType.Of<Image>(),true))
        {var image=c.TryCast<Image>();if(image==null||image==ui.selectedWeaponPanel||saved.Exists(v=>v.image==image))continue;bool isIcon=icons.Contains(image.GetInstanceID());saved.Add((image,image.enabled,image.color,isIcon));if(isIcon)image.color=Color.white;else image.enabled=false;}
    }
    internal void Render()
    {
        // A wheel selection also calls native DisplayMenu. It must not make this
        // second strip flash, even after the wheel closes: only Y requests it.
        if(GameUiControls.Current?.BlocksGameplay==true)showUntil=0;
        bool show=Time.realtimeSinceStartup<showUntil;
        foreach(var s in saved)if(s.image!=null&&!s.icon&&s.image.enabled)s.image.enabled=false;
        foreach(var s in strips.Values)
        {
            if(s.Ui==null)continue;if(s.Group!=null)s.Group.alpha=show?s.Alpha:0;
            HidePanel(s);
            // Highlight the original icon itself. No independently positioned
            // desktop selection rectangle can flash at the old screen origin.
            int index=Index(s);
            for(int i=0;i<s.Icons.Count;i++)
            {
                var icon=s.Icons[i];if(icon!=null){var c=Color.white;c.a=i==index?1:.42f;if(Math.Abs(icon.color.a-c.a)>.001f)icon.color=c;}
            }
        }
    }
    private void HidePanel(Strip s)
    {var p=s.Ui.selectedWeaponPanel;if(p==null)return;p.enabled=false;var c=p.color;c.a=0;p.color=c;}
    public void Dispose()
    {
        if(current==this)current=null;patches.UnpatchSelf();
        foreach(var s in strips.Values){if(s.Group!=null){if(s.OwnGroup)UnityEngine.Object.Destroy(s.Group);else s.Group.alpha=s.Alpha;}if(s.Ui!=null&&s.Ui.selectedWeaponPanel!=null){s.Ui.selectedWeaponPanel.enabled=s.PanelEnabled;s.Ui.selectedWeaponPanel.color=s.PanelColor;}}
        foreach(var s in saved)if(s.image!=null){s.image.enabled=s.enabled;s.image.color=s.color;}saved.Clear();strips.Clear();
    }
}
