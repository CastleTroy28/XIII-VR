using System;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
// Rendering recovery only. Ownership and consumption always use the original
// inventory instance; never equip/instantiate the gameplay prefab to show a key.
internal static class KeyPreviewFactory
{
    internal static HeldItemVisual Create(Equipable owned,PlayerEquipableInventory inventory)
    {
        try{return HeldItemVisual.Create(owned);}
        catch(Exception e){Bootstrap.Warn("KEY instance preview unavailable: "+e.Message);}
        string id=owned.identifier??"";
        var prefabs=inventory.itemConfig?.itemPrefabs;
        if(id.Length>0&&prefabs!=null)
        {
            for(int i=0;i<Math.Min(prefabs.Count,128);i++)
            {
                var prefab=prefabs[i];
                if(prefab==null||prefab==owned||prefab.slot!=owned.slot||prefab.identifier!=id)continue;
                try
                {
                    var visual=HeldItemVisual.Create(prefab);
                    Bootstrap.Write("KEY preview recovered from matching item prefab id="+id);
                    return visual;
                }
                catch(Exception e){Bootstrap.Warn("KEY prefab preview unavailable id="+id+": "+e.Message);}
            }
        }
        if(id.Length>0)
        {
            // key_02 is present on the player's FPS rig in the failing log,
            // although neither eqp_key_02 Equipable exposes it. Borrow just its
            // mesh/materials; never unhide or reparent the native animated hand.
            var player=inventory.customCharacterController;
            var recovered=TryRoot(owned,player?.CurrentFpsRigReference?.transform,"FPS rig",id)
                ??TryRoot(owned,player?.tinyArmsMesh,"tiny arms",id);
            if(recovered!=null)return recovered;
            var pickups=inventory.itemConfig?.pickupPrefabs;
            if(pickups!=null&&pickups.TryGetValue(id,out var pickup)&&pickup!=null)
            {
                recovered=TryRoot(owned,pickup.transform,"matching pickup prefab",null);
                if(recovered!=null)return recovered;
            }
            // Presentation fallback for meshless metal keys only. The owned
            // key02 still supplies permission, sound and consumption in the
            // gesture driver. A card or arbitrary inventory item is never used.
            if(owned.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Key&&id!="eqp_key_01")
            {
                const string common="eqp_key_01";
                var all=inventory.allEquipment;
                if(all!=null&&all.TryGetValue(common,out var shared)&&shared!=null&&shared.slot==owned.slot&&shared.identifier==common)
                {
                    recovered=TryShared(owned,shared);if(recovered!=null)return recovered;
                }
                if(prefabs!=null)for(int i=0;i<Math.Min(prefabs.Count,128);i++)
                {
                    var prefab=prefabs[i];
                    if(prefab==null||prefab.slot!=owned.slot||prefab.identifier!=common)continue;
                    recovered=TryShared(owned,prefab);if(recovered!=null)return recovered;
                }
            }
        }
        throw new InvalidOperationException("No usable key/card model for owned item "+owned.name+" id="+id+" slot="+owned.slot);
    }
    private static HeldItemVisual? TryRoot(Equipable owned,Transform? root,string label,string? identifier)
    {
        if(root==null)return null;
        try
        {
            var sources=HeldItemMeshSources.FindIn(root,identifier);if(sources.Count==0)return null;
            var visual=HeldItemVisual.CreateFromRenderers(owned,sources);
            Bootstrap.Write("KEY preview recovered from "+label+" id="+owned.identifier+" renderer="+sources[0].name);
            return visual;
        }
        catch(Exception e){Bootstrap.Warn("KEY "+label+" preview unavailable id="+owned.identifier+": "+e.Message);return null;}
    }
    private static HeldItemVisual? TryShared(Equipable owned,Equipable shared)
    {
        try
        {
            var visual=HeldItemVisual.Create(shared);
            Bootstrap.Write("KEY preview uses shared native metal-key model="+shared.identifier+"; owned key unchanged="+owned.identifier);
            return visual;
        }
        catch(Exception e){Bootstrap.Warn("KEY shared model unavailable: "+e.Message);return null;}
    }
}
