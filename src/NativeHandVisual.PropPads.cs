using System;using System.Collections.Generic;using System.Linq;using UnityEngine;
using NVector=System.Numerics.Vector3;
namespace XiiiXR;
internal sealed partial class NativeHandVisual
{
    private bool propPadsReady;
    private void EnsurePropPads()
    {
        if(propPadsReady||fingers==null||snapshot==null)return;propPadsReady=true;
        // Production outfit meshes strip CPU boneWeights. The private baker
        // still evaluates their skin. Probe one distal joint at a time to find
        // the vertices it actually drives; never move the game's own skeleton.
        var missing=fingers.RimDistalBones.Where(b=>!rimPadVertices.ContainsKey(b)).ToArray();
        if(missing.Length==0)return;
        try
        {
            var bake=EnsureBakeTarget();var inverse=restFrame.inverse;var frame=N(restFrame);
            foreach(int bone in missing)
            {
                var canonical=(System.Numerics.Matrix4x4[])canonicalRest.Clone();
                var shift=System.Numerics.Matrix4x4.CreateTranslation(.02f,0,0);
                for(int i=0;i<canonical.Length;i++)if(i==bone||skinDrivers[i]==bone)canonical[i]*=shift;
                var probe=canonical.Select(m=>inverse*U(m)).ToArray();snapshot.BakePose(bake,probe);
                var vertices=bake.vertices;var ids=new List<int>();
                for(int i=0;i<sourceCount;i++)
                {
                    var delta=NVector.Transform(N(vertices[i]),frame)-restPoints[i];
                    if(DistalSkinProbe.Follows(delta))ids.Add(i);
                }
                var selected=fingers.SetRimPadCloud(bone,ids.Select(i=>restPoints[i]).ToArray());
                if(selected.Length>0)rimPadVertices[bone]=selected.Select(i=>ids[i]).ToArray();
            }
            // The shared scratch mesh was used by the probe, not by a render.
            ResetRenderPose();
            Bootstrap.Write("PROP SKIN baked weight probe side="+(rightHand?"R":"L")+" pads="+fingers.RimMeasuredPads);
        }
        catch(Exception ex){ResetRenderPose();Bootstrap.Warn("PROP SKIN probe unavailable: "+ex.Message);}
    }
    // 0.1.109: lockpick handle point under the thumb (screwdriver grip).
    internal Vector3 ScrewdriverContact(float thickness){EnsurePropPads();return fingers==null?new Vector3(-.045f,-.025f,.045f):U(fingers.ScrewdriverContact(thickness));}
    internal string ScrewdriverReport=>fingers==null?"unavailable":"thumbErrorMm="+(fingers.ScrewdriverThumbError*1000).ToString("F1");
    // 0.1.200: the fingers' closure round a long thing `radius` thick (hand units) and how far its middle moves with them.
    internal float WrapCurl(float radius,out Vector3 shift)
    {
        shift=Vector3.zero;EnsurePropPads();if(fingers==null)return FingerPoseMath.HeldCurl;
        float curl=fingers.WrapCurl(radius,out var s);shift=new Vector3(s.X,s.Y,s.Z);return curl;
    }
    internal Vector3 PropPowerContact(string profile,float thickness,ChairGripSurface? surface=null)
    {
        EnsurePropPads();return fingers==null?new Vector3(0,-.025f,.073f):U(FingerPoseMath.TrayProfile(profile)?fingers.ChairContact(profile,thickness,surface):fingers.PowerContact(profile));
    }
}
