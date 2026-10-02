using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
namespace XiiiXR;
// Read the original animated volume saturation even while the desktop renderer
// is bypassed. Do not enable PostProcessLayer, build its commands or alter profiles.
internal sealed class StoryVolumeColor
{
    private sealed class Entry
    {
        internal PostProcessVolume Volume=null!;
        internal ColorGrading Color=null!;
        internal Collider[] Colliders=Array.Empty<Collider>();
    }
    private readonly Camera camera;
    private PostProcessLayer? layer;
    private readonly List<Entry> entries=new();
    private float nextFind,nextWarning;
    internal StoryVolumeColor(Camera camera){this.camera=camera;}
    // 0.1.122: which volumes gave the last saturation (for the log).
    internal string Contributors {get;private set;}=""; 
    internal float Read()
    {
        try
        {
            if(Time.realtimeSinceStartup>=nextFind){nextFind=Time.realtimeSinceStartup+1;Discover();}
            if(layer==null)return 0;
            var trigger=layer.volumeTrigger;
            float saturation=0;bool enabled=false;var names=new System.Text.StringBuilder();
            foreach(var entry in entries)
            {
                var v=entry.Volume;var color=entry.Color;
                if(v==null||color==null||!v.isActiveAndEnabled||!color.active||v.weight<=0)continue;
                float weight=Math.Clamp(v.weight,0,1);
                if(!v.isGlobal)
                {
                    if(trigger==null)continue;
                    float closest=float.PositiveInfinity;var position=trigger.position;
                    foreach(var c in entry.Colliders)
                        if(c!=null&&c.enabled)closest=Math.Min(closest,(c.ClosestPoint(position)-position).sqrMagnitude*.25f);
                    weight*=StoryVolumeMath.Influence(closest,v.blendDistance);
                }
                if(weight<=0||!float.IsFinite(weight))continue;
                // Native BoolParameter switches at any positive contribution;
                // saturation interpolates independently in priority order.
                if(color.enabled!=null&&color.enabled.overrideState)enabled=color.enabled.value;
                var parameter=color.saturation;
                if(parameter!=null&&parameter.overrideState)
                {
                    saturation=StoryVolumeMath.Blend(saturation,parameter.value,weight);
                    if(parameter.value<0&&names.Length<400)names.Append(v.name).Append(v.isGlobal?"(global":"(local").Append(" w=").Append(weight.ToString("F2")).Append(" s=").Append(parameter.value.ToString("F0")).Append(") ");
                }
            }
            Contributors=names.ToString().Trim();
            return enabled?Math.Clamp(-saturation/100f,0,1):0;
        }
        catch(Exception ex)
        {
            if(Time.realtimeSinceStartup>=nextWarning){nextWarning=Time.realtimeSinceStartup+10;Bootstrap.Warn("STORY COLOR volume sampling retry: "+ex.Message);}
            return 0;
        }
    }
    private void Discover()
    {
        if(layer==null)layer=camera.GetComponent(Il2CppType.Of<PostProcessLayer>())?.TryCast<PostProcessLayer>();
        if(layer==null)return;
        entries.Clear();int mask=layer.volumeLayer.value;
        // Use the engine's registered volume cache instead of searching every
        // Unity object once per second. This does not render or enable a layer.
        foreach(var volume in PostProcessManager.instance.GrabVolumes(layer.volumeLayer))
        {
            if(volume==null||!volume.gameObject.scene.IsValid()||(mask&(1<<volume.gameObject.layer))==0)continue;
            var profile=volume.profileRef;if(profile==null||profile.settings==null)continue;
            foreach(var setting in profile.settings)
            {
                var color=setting?.TryCast<ColorGrading>();if(color==null)continue;
                var colliders=new List<Collider>();
                if(!volume.isGlobal)foreach(var component in volume.GetComponents(Il2CppType.Of<Collider>()))
                {var collider=component.TryCast<Collider>();if(collider!=null)colliders.Add(collider);}
                entries.Add(new Entry{Volume=volume,Color=color,Colliders=colliders.ToArray()});break;
            }
        }
        entries.Sort((a,b)=>a.Volume.priority.CompareTo(b.Volume.priority));
    }
}
