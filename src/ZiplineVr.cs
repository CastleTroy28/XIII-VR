using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
// 0.1.195: the zipline in VR. The zipline hook taken from the
// weapon wheel is held in a hand (passed between hands like a weapon);
// pointed at a zipline it starts the game's own ride (WheelItems /
// InteractionDriver.TryZipline). The game's own "fake" arm with its hook is
// not drawn (no third hand); the other hand keeps the weapon and shoots.
// 0.1.197: while riding, the hand holding the hook is not drawn
// at all (no hand pinned to the cable): only the free hand is there. The hook
// lies in the hand as the game's own arm holds it on the cable (HoldOf).
internal sealed class ZiplineVr : IDisposable
{
    internal static ZiplineVr? Current;
    private readonly CameraRig rig;
    private readonly Harmony patches=new("xiii.vr.xrbootstrap.zipline");
    private Transform? player;private PlayMagic.CustomCharacterController? character;private ZiplineController? controller;private float nextFind;
    private Zipline? ride;private Transform? from,to;private float rideStart=-1,nextCollect;private int side;
    private int requestedSide=-1;private float requestedAt=-10;
    private readonly Dictionary<int,(Renderer renderer,bool enabled)> hidden=new();private readonly List<Renderer> gameParts=new();
    internal bool Riding=>ride!=null;
    internal bool HandBusy(bool right)=>ride!=null&&side==(right?1:0);
    // The hand that held the hook is not drawn while riding.
    internal bool HidesHand(bool right)=>HandBusy(right);
    internal ZiplineVr(CameraRig camera)
    {
        rig=camera;Current=this;LoadHold();
        try{patches.Patch(AccessTools.DeclaredMethod(typeof(Zipline),"MovePlayerToOtherPoint"),postfix:new HarmonyMethod(typeof(ZiplineVr),nameof(Started)));}
        catch(Exception ex){Bootstrap.Warn("ZIPLINE ride hook unavailable (the game's own ride and arm stay): "+ex.Message);}
    }
    private static void Started(Zipline __instance,Transform originPoint)
    {
        var c=Current;if(c==null)return;
        try{c.Begin(__instance,originPoint);}catch(Exception ex){Bootstrap.Warn("ZIPLINE ride start: "+ex.Message);}
    }
    // The hand whose hook pointed at the cable.
    internal void StartedBy(int s)
    {
        requestedSide=s;requestedAt=Time.realtimeSinceStartup;
        if(ride!=null&&Time.realtimeSinceStartup-rideStart<1)side=s;
    }
    private void Begin(Zipline z,Transform origin)
    {
        if(ride!=null)End("a new ride");
        float now=Time.realtimeSinceStartup;
        ride=z;from=origin;to=z.pointA!=null&&origin!=null&&z.pointA.Pointer==origin.Pointer?z.pointB:z.pointA;rideStart=now;nextCollect=0;
        holdSamples=0;nextHoldSample=now+.35f;
        var items=GameUiControls.Current?.Items;
        side=requestedSide>=0&&now-requestedAt<1?requestedSide:items?.ZiplineTool==true?items.ToolSide:WeaponHands.LeftHanded?1:0;
        CollectGameParts(z);
        Bootstrap.Write("ZIPLINE ride "+z.name+": the "+(side==0?"left":"right")+" hand with the hook is not drawn while riding (the other hand is free)"
            +"; from "+(from!=null?from.position.ToString("F1"):"?")+" to "+(to!=null?to.position.ToString("F1"):"?")+"; the game's arm hidden ("+gameParts.Count+" parts)");
    }
    internal void Tick(Transform? root)
    {
        try
        {
            if(root!=player){End("another player");player=root;character=null;controller=null;nextFind=0;staticTried=false;}
            if(root==null)return;
            float now=Time.realtimeSinceStartup;
            if((character==null||controller==null)&&now>=nextFind)
            {
                nextFind=now+1;
                character=root.GetComponent(Il2CppType.Of<PlayMagic.CustomCharacterController>())?.TryCast<PlayMagic.CustomCharacterController>();
                controller=root.GetComponentInChildren(Il2CppType.Of<ZiplineController>(),true)?.TryCast<ZiplineController>();
            }
            if(controller!=null&&!staticTried)StaticHold();
            if(ride==null)return;
            // The game shows its arm and hook once the ride is under way: looked for again.
            if(now>=nextCollect){nextCollect=now+.25f;CollectGameParts(ride);}
            // How the game's own arm holds the hook, taken while it holds it.
            if(holdSamples<HoldSamplesWanted&&now>=nextHoldSample){nextHoldSample=now+.1f;SampleHold("the ride");}
            bool transit=false;
            try{transit=ride.isInTransit||character?.isDoingZipline==true;}catch(Exception){}
            if(!transit&&now-rideStart>.6f)End("the ride ended");
            else if(now-rideStart>180)End("too long");
        }
        catch(Exception ex){End("error");Bootstrap.Warn("ZIPLINE tick: "+ex.Message);}
    }
    internal void Render(Transform? left,Transform? right)
    {
        if(ride==null)return;
        foreach(var r in gameParts)
        {
            if(r==null)continue;int id=r.GetInstanceID();
            if(!hidden.ContainsKey(id))hidden.Add(id,(r,r.enabled));
            r.enabled=false;
        }
    }
    // The game's own arm and hook shown while riding (its "fake arms").
    private void CollectGameParts(Zipline z)
    {
        gameParts.Clear();
        void Add(GameObject? g){if(g==null)return;foreach(var c in g.GetComponentsInChildren(Il2CppType.Of<Renderer>(),true)){var r=c.TryCast<Renderer>();if(r!=null&&r.TryCast<ParticleSystemRenderer>()==null)gameParts.Add(r);}}
        try{Add(z.fakeArms);}catch(Exception){}
        try{var c=controller??z.ziplineController;Add(c?.ziplineInstance);Add(c?.fakeArmsController?.fakeArms);}catch(Exception){}
    }
    private void End(string why)
    {
        if(ride==null)return;
        foreach(var e in hidden.Values)if(e.renderer!=null)e.renderer.enabled=e.enabled;
        hidden.Clear();gameParts.Clear();
        Bootstrap.Write("ZIPLINE ride over ("+why+"): the "+(side==0?"left":"right")+" hand holds the hook again");
        ride=null;from=to=null;requestedSide=-1;
    }
    // ---- 0.1.197: the hook in the hand as the game's arm holds it ----
    // The game spawns its hook on its fake arm (ZiplineController.fakeArmsAnchor).
    // Its main part (the renderer the held copy was built in) in that hand's
    // own frame - the same skeleton as the VR hands (NativeHandVisual canonical
    // frame) - is the hold; the other VR hand holds it mirrored.
    private const int HoldSamplesWanted=6;
    private int holdSide=-1;private Matrix4x4 holdCanonical=Matrix4x4.identity;private string holdBody="";private string holdFrom="";
    private int holdSamples;private float nextHoldSample;private bool staticTried,holdReported,bodyWarned,noWristReported;
    private static string HoldFile=>Path.Combine(BepInEx.Paths.ConfigPath,"XIII-XR-zipline-hold.txt");
    // At the start: when the game's hook sits on the fake arm's wrist (a child
    // of it), its hold is known at once, before any ride.
    private void StaticHold()
    {
        staticTried=true;
        try
        {
            var instance=controller?.ziplineInstance;
            if(instance==null){Bootstrap.Write("ZIPLINE hook on the game's arm: not spawned yet");staticTried=false;nextFind=Time.realtimeSinceStartup+1;return;}
            var t=instance.transform;bool onWrist=false;
            for(var p=t.parent;p!=null;p=p.parent){var n=p.name;if(n.Contains("Wrist",StringComparison.OrdinalIgnoreCase)){onWrist=true;break;}}
            Bootstrap.Write("ZIPLINE hook on the game's arm: "+PathOf(t)+" local="+t.localPosition.ToString("F3")+" rot="+t.localRotation.eulerAngles.ToString("F0")+" scale="+t.localScale.ToString("F3")+(onWrist?" (on a wrist: held so at once)":" (not on a wrist: its hold is taken on the first ride)"));
            if(onWrist&&holdSide<0)SampleHold("the game's wrist");
        }
        catch(Exception ex){Bootstrap.Warn("ZIPLINE hook hold: "+ex.Message);}
    }
    private void SampleHold(string why)
    {
        try
        {
            var instance=controller?.ziplineInstance;var arms=controller?.fakeArmsController?.fakeArms;var hands=WeaponHands.Current;
            if(instance==null||arms==null||hands==null)return;
            Renderer? body=null;int best=-1;string want=GameUiControls.Current?.Items.HeldBodyName??holdBody;
            foreach(var c in instance.GetComponentsInChildren(Il2CppType.Of<Renderer>(),true))
            {
                var r=c.TryCast<Renderer>();if(r==null||r.TryCast<ParticleSystemRenderer>()!=null)continue;
                if(want.Length>0&&r.name==want){body=r;break;}
                int n=0;try{n=r.GetComponent(Il2CppType.Of<MeshFilter>())?.TryCast<MeshFilter>()?.sharedMesh?.vertexCount??0;}catch(Exception){}
                if(n>best){best=n;body=r;}
            }
            if(body==null)return;
            int chosen=-1;Matrix4x4 canonical=Matrix4x4.identity;float nearest=float.PositiveInfinity;
            for(int s=0;s<2;s++)
            {
                var native=s==0?hands.LeftNative:hands.RightNative;
                if(native!=null&&native.TryCanonicalFromRig(arms.transform,body.localToWorldMatrix,out var m,out float d)&&d<nearest){nearest=d;chosen=s;canonical=m;}
            }
            if(chosen<0||!(nearest<.35f))
            {
                if(!noWristReported){noWristReported=true;Bootstrap.Write("ZIPLINE hook hold not taken ("+why+"): "+(chosen<0?"the game's arm has no wrist of the VR hands' skeleton":"its hand is "+nearest.ToString("F2")+" m from the hook")+"; the fitted hold stays");}
                return;
            }
            bool steady=holdSide==chosen&&Vector3.Distance(holdCanonical.GetColumn(3),canonical.GetColumn(3))<.004f;
            holdSide=chosen;holdCanonical=canonical;holdBody=body.name;holdFrom=why;holdSamples=steady?holdSamples+1:1;
            if(why!="the ride"||holdSamples==HoldSamplesWanted)
            {
                SaveHold();
                Bootstrap.Write("ZIPLINE hook held as the game's "+(chosen==0?"left":"right")+" hand holds it ("+why+"): "+body.name+" at "+((Vector3)canonical.GetColumn(3)).ToString("F3")+" in that hand, wrist "+nearest.ToString("F2")+" m away; the other hand holds it mirrored");
            }
        }
        catch(Exception ex){holdSamples=HoldSamplesWanted;Bootstrap.Warn("ZIPLINE hook hold sample: "+ex.Message);}
    }
    // Where the held copy's main part goes in hand `s` (world); false: not known yet.
    internal bool TryHeld(int s,HeldItemVisual item)
    {
        if(holdSide<0||s<0||s>1)return false;
        if(item.BodyName.Length>0&&holdBody.Length>0&&item.BodyName!=holdBody)
        {
            if(!bodyWarned){bodyWarned=true;Bootstrap.Warn("ZIPLINE held hook built from "+item.BodyName+", the game's hold is of "+holdBody+": the fitted hold is used");}
            return false;
        }
        var native=s==0?WeaponHands.Current?.LeftNative:WeaponHands.Current?.RightNative;
        var canonical=s==holdSide?holdCanonical:Matrix4x4.Scale(new Vector3(-1,1,1))*holdCanonical;
        if(native==null||!native.TryCanonicalWorld(canonical,out var world))return false;
        item.PoseMatrix(world);
        if(!holdReported){holdReported=true;Bootstrap.Write("ZIPLINE hook in the "+(s==0?"left":"right")+" hand as the game holds it"+(s==holdSide?"":" (its hold mirrored)")+" ("+holdFrom+")");}
        return true;
    }
    private void LoadHold()
    {
        try
        {
            if(!File.Exists(HoldFile))return;
            var parts=File.ReadAllText(HoldFile).Trim().Split(' ');
            if(parts.Length!=19||parts[0]!="v1")return;
            int s=int.Parse(parts[1],CultureInfo.InvariantCulture);if(s<0||s>1)return;
            var m=new Matrix4x4();for(int i=0;i<16;i++){float v=float.Parse(parts[3+i],CultureInfo.InvariantCulture);if(!float.IsFinite(v))return;m[i]=v;}
            holdSide=s;holdBody=parts[2];holdCanonical=m;holdFrom="kept from an earlier ride";
            Bootstrap.Write("ZIPLINE hook hold kept from an earlier ride: the game's "+(s==0?"left":"right")+" hand, "+holdBody);
        }
        catch(Exception ex){Bootstrap.Warn("ZIPLINE hook hold file: "+ex.Message);}
    }
    private void SaveHold()
    {
        if(holdSide<0||holdBody.Contains(' '))return;
        try
        {
            var sb=new System.Text.StringBuilder("v1 ");sb.Append(holdSide).Append(' ').Append(holdBody);
            for(int i=0;i<16;i++)sb.Append(' ').Append(holdCanonical[i].ToString("R",CultureInfo.InvariantCulture));
            File.WriteAllText(HoldFile,sb.ToString());
        }
        catch(Exception ex){Bootstrap.Warn("ZIPLINE hook hold not saved: "+ex.Message);}
    }
    private static string PathOf(Transform? t)
    {
        if(t==null)return "none";var s=t.name;
        for(var p=t.parent;p!=null&&s.Length<200;p=p.parent)s=p.name+"/"+s;return s;
    }
    public void Dispose(){End("closed");patches.UnpatchSelf();if(Current==this)Current=null;}
}
