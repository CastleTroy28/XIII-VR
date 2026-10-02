using System;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
// Mission registration and visibility remain native. Owned VR indicators bypass
// the desktop widget hierarchy, clipping, canvas alpha and pixel scaling.
internal sealed class FocusMarkers:IDisposable
{
    private readonly System.Collections.Generic.Dictionary<int,OwnedFocusMarker> visuals=new();
    private readonly System.Collections.Generic.List<int> stale=new();
    private FocusHUDSystem? system;
    private float nextFind,nextError;private int findEpoch=int.MinValue;
    private bool objectivesWereOpen;
    private float nextReport;
    private int missionHidden;
    internal void Render(CameraRig rig)
    {
        foreach(var visual in visuals.Values)visual.Seen=false;
        if(rig.Frontend||rig.MovieActive){foreach(var visual in visuals.Values)visual.Show(false);return;}
        try
        {
            // 0.1.162: every 8 s and after a scene change (was every 3 s) while none is found.
            if((system==null||!system.isActiveAndEnabled)&&SceneScan.Due(ref nextFind,8,ref findEpoch))
            {
                long scanStart=SceneScan.Begin();
                foreach(var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<FocusHUDSystem>()))
                {var f=o.TryCast<FocusHUDSystem>();if(f!=null&&f.isActiveAndEnabled&&f.gameObject.scene.IsValid()){system=f;break;}}
                SceneScan.End("focus",scanStart);
            }
            if(system==null)return;
            bool open=GameUiControls.Current?.ObjectivesOpen==true;
            // The VR objective panel does not dispatch the desktop focus key.
            // Trigger the game's own visibility window; keep its mission rules.
            if(open&&!objectivesWereOpen)system.TriggerTrackedMarkers();
            if(open)system.m_triggerMarkersTimeLeft=Math.Max(system.m_triggerMarkersTimeLeft,.25f);
            bool report=open!=objectivesWereOpen||Time.realtimeSinceStartup>=nextReport;
            objectivesWereOpen=open;missionHidden=0;

            RenderGroup(system.m_trackedObjectiveMarkers,rig,open||system.m_triggerMarkersTimeLeft>0);
            RenderGroup(system.m_trackedAlarmBoxesVfXs,rig,open||system.m_triggerMarkersTimeLeft>0);
            stale.Clear();foreach(var pair in visuals)if(!pair.Value.Seen)stale.Add(pair.Key);
            foreach(int id in stale){visuals[id].Dispose();visuals.Remove(id);}
            if(report){nextReport=Time.realtimeSinceStartup+30;
                Bootstrap.Write("VR FOCUS tracked="+(system.m_trackedObjectiveMarkers?.Count??0)+" owned="+visuals.Count+" missionHidden="+missionHidden+" objectivePanel="+open+" timer="+system.m_triggerMarkersTimeLeft+" markerLayer=0 mainMask="+(rig.MainCamera==null?0:rig.MainCamera.cullingMask));}
        }
        catch(Exception ex)
        {if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+5;Bootstrap.Warn("VR focus markers: "+ex.Message);}}
    }
    private void RenderGroup(Il2CppSystem.Collections.Generic.Dictionary<FocusMarker,FocusHUDElement>? group,CameraRig rig,bool open)
    {
        if(group==null)return;
        foreach(var entry in group)
        {
            var marker=entry.Key;var widget=entry.Value;
            if(marker==null||marker.destroyed)continue;
            if(!marker.IsVisible){missionHidden++;continue;}
            if(!open&&!marker.isPermanent)continue;
            int id=marker.GetInstanceID();
            if(!visuals.TryGetValue(id,out var visual)){visual=new OwnedFocusMarker(marker,widget);visuals.Add(id,visual);}
            visual.Seen=true;
            var offset=marker.transform.position-rig.HeadPosition;
            var local=Quaternion.Inverse(rig.HeadRotation)*offset;
            var placement=MarkerProjection.Place(new System.Numerics.Vector3(local.x,local.y,local.z));
            // The objective card sits at 1.2 m; its background must not cover
            // the very markers requested by opening it.
            float depth=open?1.05f:1.7f,factor=depth/1.7f;
            var world=rig.HeadPosition+rig.HeadRotation*new Vector3(placement.x*factor,placement.y*factor,depth);
            visual.Pose(world,rig.HeadRotation,placement.edge,placement.angle,offset.magnitude);
        }
    }
    public void Dispose(){foreach(var v in visuals.Values)v.Dispose();visuals.Clear();system=null;nextFind=0;objectivesWereOpen=false;}
}
