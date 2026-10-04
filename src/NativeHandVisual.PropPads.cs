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
    // 0.1.226: round a thicker thing (FingerPoseMath.WrapCurlThick).
    internal float WrapCurlThick(float radius,out Vector3 shift)
    {
        shift=Vector3.zero;EnsurePropPads();if(fingers==null)return FingerPoseMath.HeldCurl;
        float curl=fingers.WrapCurlThick(radius,out var s);shift=new Vector3(s.X,s.Y,s.Z);return curl;
    }
    // 0.1.227: a long thing `radius` thick and reaching `halfLength` either
    // side of the hand's middle (hand units) held in this hand: the fingers'
    // closure round it, where its line lies and its way toward the little
    // finger (canonical frame) and the profile
    // to draw the hand with (FingerPoseMath.FitGrip: each finger on the thing,
    // the thumb clear of it), kept for the hand as measured now.
    // (Worked out again when the hand's measures change; at most once a second each.)
    private readonly Dictionary<(string,int,int,bool),(int revision,float at,float curl,NVector channel,NVector little,string profile)> gripChannels=new();
    internal bool GripFit(string prefix,float radius,float halfLength,bool trigger,out float curl,out NVector channel,out NVector little,out string profile)
    {
        curl=FingerPoseMath.HeldCurl;channel=NVector.Zero;little=new NVector(rightHand?1:-1,0,0);profile="";
        EnsurePropPads();if(fingers==null||!float.IsFinite(radius)||!float.IsFinite(halfLength))return false;
        var key=(prefix,(int)MathF.Round(radius*10000),(int)MathF.Round(Math.Min(halfLength,1)*1000),trigger);
        int revision=fingers.Revision-fingers.FitRevision;
        float now=Time.realtimeSinceStartup;
        if(gripChannels.TryGetValue(key,out var kept)&&(kept.revision==revision||now-kept.at<1)&&fingers.GripFit(kept.profile,out _,out _))
        {curl=kept.curl;channel=kept.channel;little=kept.little;profile=kept.profile;return true;}
        var name=fingers.FitGrip(prefix,radius,halfLength,trigger,out curl,out channel,out little);
        if(name==null)return false;profile=name;
        if(gripChannels.Count>=16)gripChannels.Clear();
        gripChannels[key]=(revision,now,curl,channel,little,name);return true;
    }
    // 0.1.231: the index fingertip (canonical frame, from the wrist) as drawn with `profile`, kept per profile and hand measures.
    private (string profile,int revision,NVector pad,NVector knuckle) indexPad=("",-1,default,default);
    internal bool IndexPad(string profile,out NVector pad,out NVector knuckle)
    {
        pad=knuckle=NVector.Zero;EnsurePropPads();if(fingers==null||string.IsNullOrEmpty(profile))return false;
        if(indexPad.profile==profile&&indexPad.revision==fingers.Revision){pad=indexPad.pad;knuckle=indexPad.knuckle;return true;}
        try{pad=fingers.IndexPad(profile,out knuckle);}catch(Exception){return false;}
        if(!float.IsFinite(pad.X)||!float.IsFinite(pad.Y)||!float.IsFinite(pad.Z)||!float.IsFinite(knuckle.X+knuckle.Y+knuckle.Z))return false;
        indexPad=(profile,fingers.Revision,pad,knuckle);return true;
    }
    // The fit drawn with `profile` (for the log): each finger's closure and the thumb's.
    internal string GripFitReport(string profile)=>fingers!=null&&fingers.GripFit(profile,out var f,out float t)?"fingers "+string.Join("/",f.Select(a=>float.IsFinite(a)?a.ToString("F2"):"trigger"))+", thumb "+t.ToString("F2"):"no fit";
    internal Vector3 PropPowerContact(string profile,float thickness,ChairGripSurface? surface=null)
    {
        EnsurePropPads();return fingers==null?new Vector3(0,-.025f,.073f):U(FingerPoseMath.TrayProfile(profile)?fingers.ChairContact(profile,thickness,surface):fingers.PowerContact(profile));
    }
}
