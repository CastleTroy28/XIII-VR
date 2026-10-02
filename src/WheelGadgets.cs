using System;
using UnityEngine;
using PlayMagic.Weapons;
namespace XiiiXR;
// Gadget list entries are display-only in the native wheel. Make their exact
// configured sprite selectable without assuming a fixed order across missions.
internal sealed class WheelGadgets
{
    private UnityEngine.UI.Image? icon;private Color oldColor;
    internal Equipable? Item{get;private set;}
    internal Vector3 Point;
    internal void Clear(){if(icon!=null)icon.color=oldColor;icon=null;Item=null;}
    internal void Hover(InventoryWheel? wheel,Vector3 origin,Vector3 direction)
    {
        Clear();if(wheel==null||!wheel.IsOpen||wheel.gadgetList?.gadgetContainers==null||wheel.weaponIcons?.icons==null||wheel.playerInventory==null)return;
        foreach(var container in wheel.gadgetList.gadgetContainers)
        {
            var image=container?.icon;if(image==null||!image.gameObject.activeInHierarchy||image.sprite==null)continue;
            var r=image.rectTransform;
            if(!MenuRayMath.Plane(ContactWorld.V(origin),ContactWorld.V(direction),ContactWorld.V(r.position),ContactWorld.V(r.forward),out var hit,out _))continue;
            var local=r.InverseTransformPoint(ContactWorld.U(hit));if(!r.rect.Contains(new Vector2(local.x,local.y)))continue;
            foreach(int slot in new[]{11,16,33})
            {
                var item=wheel.playerInventory.GetEquipableFromSlot((PlayerEquipableInventory.ActiveEquipmentSlot)slot);if(item==null)continue;
                foreach(var entry in wheel.weaponIcons.icons)
                {
                    if(entry==null||entry.identifier!=item.identifier||entry.normalIcon!=image.sprite&&entry.hoverIcon!=image.sprite)continue;
                    Item=item;icon=image;oldColor=image.color;image.color=new Color(1,.8f,.25f,1);Point=ContactWorld.U(hit);return;
                }
            }
        }
    }
}
