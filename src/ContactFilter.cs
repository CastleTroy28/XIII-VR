using System;
using System.Collections.Generic;
using UnityEngine;
namespace XiiiXR;
// Level navigation barriers and bullet query shapes are not touchable surfaces.
// This filters VR queries only. Their native colliders remain enabled.
internal sealed class ContactFilter
{
    private readonly Dictionary<int,(Collider collider,bool excluded)> cache=new();
    internal static bool NonPhysicalName(string name)=>name.StartsWith("XIII ",StringComparison.OrdinalIgnoreCase)||name.EndsWith(" VR",StringComparison.OrdinalIgnoreCase)||name.Equals("path_blocker",StringComparison.OrdinalIgnoreCase)
        ||name.StartsWith("path_blocker (",StringComparison.OrdinalIgnoreCase)
        ||name.Equals("collider_player",StringComparison.OrdinalIgnoreCase)
        ||name.StartsWith("collider_player (",StringComparison.OrdinalIgnoreCase)
        // 0.1.205: the same invisible walls for walking named with a space
        // ("collider player (57)"): they held a weapon 0.4-2 m off where
        // nothing is seen and took blows meant for an enemy.
        ||name.Equals("collider player",StringComparison.OrdinalIgnoreCase)
        ||name.StartsWith("collider player (",StringComparison.OrdinalIgnoreCase)
        ||name.StartsWith("prj_bullet_",StringComparison.OrdinalIgnoreCase)
        ||name.Equals("prj_shotgun",StringComparison.OrdinalIgnoreCase)
        ||name.StartsWith("prj_shotgun(Clone)",StringComparison.OrdinalIgnoreCase)
        // 0.1.110: the water surface's bullet-splash shape and swim volumes
        // held the VR hands above the water while diving.
        ||name.StartsWith("projectileDetection",StringComparison.OrdinalIgnoreCase)
        ||name.IndexOf("SwimVolume",StringComparison.OrdinalIgnoreCase)>=0
        ||name.IndexOf("swim_volume",StringComparison.OrdinalIgnoreCase)>=0;
    internal bool Excluded(Collider c)
    {
        int id=c.GetInstanceID();
        if(cache.TryGetValue(id,out var v)&&v.collider==c)return v.excluded;
        if(cache.Count>=2048)cache.Clear();
        bool excluded=false;
        for(Transform? t=c.transform;t!=null;t=t.parent)
            if(NonPhysicalName(t.name)){excluded=true;break;}
        cache[id]=(c,excluded);return excluded;
    }
}
