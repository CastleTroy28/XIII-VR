using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace XiiiXR;
// Canonical (+Z fingers, +Y back of hand) joint poses. Operates on owned snapshots.
internal sealed partial class FingerPoseMath
{
    private readonly Matrix4x4[] rest;
    private readonly List<int[]> fingers=new();
    private readonly int[] thumb;
    private readonly Dictionary<string,Matrix4x4[]> authored=new();
    // 0.1.128: every weapon grip each native hand showed (left: index 0,
    // right: 1), by bone name, for the other hand's mirrored hold: the left
    // hand on a weapon's handle, the right hand on its fore-end. The left
    // hand's second pistol ("dual_pistol") is the right pistol grip mirrored.
    internal const string MirrorPrefix="mirror:";
    private static readonly Dictionary<string,(Dictionary<string,Matrix4x4> pose,Dictionary<string,Matrix4x4> rest)>[] sideGrips={new(),new()};
    private static readonly int[] sideRevision=new int[2];
    private int mirroredRevision=-1;
    private readonly string[] boneNames;
    private int indexFinger;
    private readonly bool rightHand;
    // 0.1.153: the two hands, so a hand can take the other's worked-out hold
    // (chair, ashtray, key pinch) mirrored.
    private static readonly FingerPoseMath?[] hands=new FingerPoseMath?[2];
    internal FingerPoseMath(string[] names,Matrix4x4[] neutral,bool right)
    {
        rightHand=right;rest=neutral;boneNames=names;string side=right?"R":"L";hands[right?1:0]=this;
        for(int n=1;n<=4;n++)
        {
            var chain=Enumerable.Range(1,3).Select(j=>Array.IndexOf(names,$"{side}_Finger_{n:00}_{j:00}SHJnt")).ToArray();
            if(chain.All(i=>i>=0))fingers.Add(chain);
        }
        thumb=Enumerable.Range(1,3).Select(j=>Array.IndexOf(names,$"{side}_Thumb_01_{j:00}SHJnt")).Where(i=>i>=0).ToArray();
        if(fingers.Count!=4||thumb.Length!=3)throw new InvalidOperationException("Native finger rig incomplete");
        float nearest=float.PositiveInfinity;
        for(int i=0;i<fingers.Count;i++)
        {float d=Vector3.DistanceSquared(rest[fingers[i][0]].Translation,rest[thumb[0]].Translation);if(d<nearest){nearest=d;indexFinger=i;}}
    }
    private int authoredRevision;
    internal int Revision=>authoredRevision+rimRevision+chairRevision+MedkitGeometry.Revision+ReloadGripGeometry.Revision+sideRevision[rightHand?0:1]+OtherProcedural;
    private int OtherProcedural{get{var o=hands[rightHand?0:1];return o==null||ReferenceEquals(o,this)?0:o.chairRevision+o.rimRevision+o.authoredRevision;}}
    internal bool Has(string profile)=>authored.ContainsKey(profile);
    internal void ResetGrips(){powerContacts.Clear();chairCache.Clear();chairPose=null;ashtrayTray=false;authored.Clear();authoredRevision++;int side=rightHand?1:0;sideGrips[side].Clear();sideRevision[side]++;}
    private static bool Mirrored(string profile)=>profile=="dual_pistol"||profile.StartsWith(MirrorPrefix,StringComparison.Ordinal);
    internal string CaptureFailure {get;private set;}="";
    internal bool Capture(string profile,Matrix4x4[] pose,bool allowOpen=false,bool requireGrasp=false,bool replace=false)
    {
        CaptureFailure="topology";
        if(pose.Length!=rest.Length||profile.Length==0)return false;
        float rigScale=1;
        if(allowOpen)
        {
            // Outfit animators may uniformly retarget the hand skeleton. Accept
            // that coherent scale, not independently stretched/collapsed bones.
            float low=float.PositiveInfinity,high=0;
            foreach(var chain in fingers.Append(thumb))foreach(int i in chain)
            foreach(var axis in new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ})
            {
                float length=Vector3.TransformNormal(axis,rest[i]).Length();
                float ratio=Vector3.TransformNormal(axis,pose[i]).Length()/length;
                if(!float.IsFinite(ratio)||ratio<.6f||ratio>1.6f){CaptureFailure="invalid retarget scale";return false;}
                low=Math.Min(low,ratio);high=Math.Max(high,ratio);
            }
            if(high-low>.03f*high){CaptureFailure="nonuniform retarget scale";return false;}
            rigScale=(low+high)*.5f;
        }
        // Paused/transition animation can collapse or scale individual bones.
        // Cache only anatomically consistent samples; never retain a distorted menu pose.
        foreach(var chain in fingers.Append(thumb))for(int j=0;j<chain.Length;j++)
        {
            int i=chain[j];var original=rest[i];var sampled=pose[i];
            CaptureFailure="scale/axes "+boneNames[i];
            if(!SameBoneShape(original,sampled,rigScale))return false;
            if(j>0)
            {
                float a=Vector3.Distance(rest[chain[j-1]].Translation,original.Translation)*rigScale;
                float b=Vector3.Distance(pose[chain[j-1]].Translation,sampled.Translation);
                CaptureFailure="segment "+boneNames[i]+" rest="+a+" live="+b;
                if(!float.IsFinite(b)||a<.001f||b<a*.85f||b>a*1.15f)return false;
            }
        }
        CaptureFailure="finger extent";
        float bend=0;
        foreach(var chain in fingers)
        {
            var a=pose[chain[1]].Translation-pose[chain[0]].Translation;
            var b=pose[chain[2]].Translation-pose[chain[1]].Translation;
            if(a.Length()<.002f||b.Length()<.002f)return false;
            bend+=MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(a),Vector3.Normalize(b)),-1,1));
            foreach(int i in chain)if(!NativeHandMesh.Finite(pose[i].Translation)||pose[i].Translation.Length()>.28f)return false;
        }
        // A pistol grip / idle boxing guard is NOT a closed fist. Previously
        // the 18-degree grip threshold permanently replaced the full fist with
        // a half-open live idle pose after the first weapon switch.
        CaptureFailure="bend="+(bend/fingers.Count);
        if(bend/fingers.Count<(profile=="fists"?1.12f:requireGrasp?.32f:allowOpen?0:.32f))return false;
        if(profile=="fists")foreach(var chain in fingers)
        {
            var a=Vector3.Normalize(pose[chain[1]].Translation-pose[chain[0]].Translation);
            var b=Vector3.Normalize(pose[chain[2]].Translation-pose[chain[1]].Translation);
            var neutralDirection=Vector3.Normalize(rest[chain[1]].Translation-rest[chain[0]].Translation);
            Matrix4x4.Invert(rest[chain[2]],out var distalInverse);
            var neutralTip=rest[chain[2]].Translation+(rest[chain[2]].Translation-rest[chain[1]].Translation)*.7f;
            var tip=Vector3.Transform(Vector3.Transform(neutralTip,distalInverse),pose[chain[2]]);
            if(Vector3.Dot(a,neutralDirection)>.57f || tip.Z>pose[chain[0]].Translation.Z+.012f)return false;
            if(Vector3.Dot(a,b)>.64f || pose[chain[2]].Translation.Y>pose[chain[0]].Translation.Y-.014f)return false;
        }
        foreach(int i in thumb)if(!NativeHandMesh.Finite(pose[i].Translation)||pose[i].Translation.Length()>.28f)return false;
        CaptureFailure="";
        if(!Mirrored(profile)&&profile!="fists"&&(replace||!authored.ContainsKey(profile)))
        {
            var bones=new Dictionary<string,Matrix4x4>();var bind=new Dictionary<string,Matrix4x4>();
            for(int i=0;i<boneNames.Length;i++){bones[boneNames[i]]=pose[i];bind[boneNames[i]]=rest[i];}
            int side=rightHand?1:0;sideGrips[side][profile]=(bones,bind);sideRevision[side]++;
        }
        if(replace||!authored.ContainsKey(profile)){authored[profile]=(Matrix4x4[])pose.Clone();authoredRevision++;}
        return true;
    }
    internal void Forget(string profile){if(authored.Remove(profile))authoredRevision++;}
    private static bool SameBoneShape(Matrix4x4 original,Matrix4x4 sampled,float rigScale)
    {
        float a=original.GetDeterminant(),b=sampled.GetDeterminant();
        if(!float.IsFinite(b)||Math.Abs(a)<1e-10f||a*b<=0)return false;
        foreach(var axis in new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ})
        {
            float restLength=Vector3.TransformNormal(axis,original).Length()*rigScale;
            float length=Vector3.TransformNormal(axis,sampled).Length();
            if(!float.IsFinite(length)||length<restLength*.85f||length>restLength*1.15f)return false;
        }
        return true;
    }
    internal Matrix4x4[] Pose(float grip,float trigger,string profile)
    {
        grip=float.IsFinite(grip)?Math.Clamp(grip,0,1):0;trigger=float.IsFinite(trigger)?Math.Clamp(trigger,0,1):0;
        if(profile.StartsWith("reload_",StringComparison.Ordinal))return AmmoPose(profile.Substring(7));
        if(profile=="medkit_s"||profile=="medkit_l")return MedkitPose(profile=="medkit_l");
        bool held=profile.Length!=0;
        int other=rightHand?0:1;
        if(mirroredRevision!=sideRevision[other])
        {
            foreach(var key in authored.Keys.Where(Mirrored).ToList())authored.Remove(key);
            mirroredRevision=sideRevision[other];
        }
        string? source=profile=="dual_pistol"?(rightHand?null:"pistol"):profile.StartsWith(MirrorPrefix,StringComparison.Ordinal)?profile.Substring(MirrorPrefix.Length):null;
        if(source!=null&&!authored.ContainsKey(profile)&&sideGrips[other].TryGetValue(source,out var sided))
        {
            var mirrored=(Matrix4x4[])rest.Clone();
            foreach(var chain in fingers.Append(thumb))foreach(int i in chain)
            {
                var name=(rightHand?"L_":"R_")+boneNames[i].Substring(2);
                if(sided.pose.TryGetValue(name,out var pose)&&sided.rest.TryGetValue(name,out var original))
                    mirrored[i]=DualGripMath.MirrorDeformation(rest[i],original,pose);
            }
            Capture(profile,mirrored,true);
        }
        // 0.1.153: the left hand worked out its own chair / ashtray hold with no
        // measured surface (the surface is measured for the right hand), so
        // its fingers closed on nothing. Now it takes the right hand's hold on
        // the measured thing, mirrored, the same way it takes a gun's grip.
        if(source!=null&&Procedural(source)&&OtherProceduralPose(source) is Matrix4x4[] theirs)return theirs;
        return ProceduralPose(profile)??HeldPose(grip,trigger,profile,held,source??profile);
    }
    private static bool Procedural(string profile)=>ChairProfile(profile)||profile.Contains("ashtray")||KeyGripGeometry.PinchProfile(profile)||profile==ScrewdriverGrip.Profile;
    private Matrix4x4[]? ProceduralPose(string profile)
    {
        if(ChairProfile(profile)||AshtrayTray(profile))return ChairPose();
        if(profile.Contains("ashtray")||KeyGripGeometry.PinchProfile(profile))return RimPose();
        if(profile==ScrewdriverGrip.Profile)return ScrewdriverPose();
        return null;
    }
    private Matrix4x4[]? mirroredFrom,mirroredProcedural;
    private Matrix4x4[]? OtherProceduralPose(string source)
    {
        var other=hands[rightHand?0:1];
        if(other==null||ReferenceEquals(other,this))return null;
        Matrix4x4[]? theirs;
        try{theirs=other.ProceduralPose(source);}catch(Exception){return null;}
        if(theirs==null||theirs.Length!=other.rest.Length)return null;
        if(ReferenceEquals(theirs,mirroredFrom)&&mirroredProcedural!=null)return mirroredProcedural;
        var result=(Matrix4x4[])rest.Clone();int found=0;
        foreach(var chain in fingers.Append(thumb))foreach(int i in chain)
        {
            int j=Array.IndexOf(other.boneNames,(rightHand?"L_":"R_")+boneNames[i].Substring(2));
            if(j<0)continue;
            result[i]=DualGripMath.MirrorDeformation(rest[i],other.rest[j],theirs[j]);found++;
        }
        if(found<fingers.Count*3)return null;
        mirroredFrom=theirs;mirroredProcedural=result;return result;
    }
    // base: the profile without "mirror:" (a mirrored prop is still a prop: no finger on a trigger).
    private Matrix4x4[] HeldPose(float grip,float trigger,string profile,bool held,string baseProfile)
    {
        bool cached=authored.TryGetValue(held?profile:"fists",out var captured);
        var result=(Matrix4x4[])rest.Clone();
        if(cached)
        {
            foreach(var chain in fingers)
                BlendChain(result,captured!,chain,held?1:Relaxed(grip));
            BlendChain(result,captured!,thumb,held?1:Relaxed(grip));
            // Keep the native index placement at the trigger. Do not replace it
            // with a synthetic curl from the flat bind pose.
            return result;
        }
        for(int i=0;i<fingers.Count;i++)
        {
            bool index=i==indexFinger;
            float amount=held?(index&&!baseProfile.StartsWith("prop",StringComparison.Ordinal)&&baseProfile!="knife"&&baseProfile!="grenade"&&(rightHand&&!profile.StartsWith(MirrorPrefix,StringComparison.Ordinal)||!rightHand&&Mirrored(profile))?.32f+trigger*.18f:HeldAmount(baseProfile)):Relaxed(index?Math.Max(grip,trigger):grip);
            Bend(result,fingers[i],amount,held,false);
            if(!held)CloseSpread(result,fingers[i],amount);
        }
        if(held)Bend(result,thumb,.85f,true,true);
        else PoseThumb(result,Relaxed(grip));
        return result;
    }
    // 0.1.109: a lockpick held like the small screwdriver in the reference
    // photo (lateral "key" grip): the fingers closed in a fist (the game's own
    // fist when it has been sampled), the handle along the hand against the
    // thumb side of the curled index finger, the thumb pad pressing on top.
    private Matrix4x4[]? screwPose;private int screwRevision=-1;private float screwThickness=.008f;
    private Vector3 screwAxis,screwThumb;
    internal float ScrewdriverThumbError {get;private set;}
    // Handle axis point under the thumb (hand-root space); the handle runs
    // along +Z (forward) through it.
    internal Vector3 ScrewdriverContact(float thickness)
    {
        thickness=float.IsFinite(thickness)?Math.Clamp(thickness,.004f,.03f):.008f;
        if(Math.Abs(thickness-screwThickness)>.00001f){screwThickness=thickness;screwPose=null;}
        _=ScrewdriverPose();return screwAxis+Vector3.UnitZ*NativeHandMesh.WristZ;
    }
    internal Vector3 ScrewdriverThumbTarget=>screwThumb+Vector3.UnitZ*NativeHandMesh.WristZ;
    private Matrix4x4[] ScrewdriverPose()
    {
        int revision=authoredRevision+rimRevision;
        if(screwPose!=null&&screwRevision==revision)return screwPose;
        var result=(Matrix4x4[])rest.Clone();
        if(authored.TryGetValue("fists",out var fist))
        {foreach(var chain in fingers)BlendChain(result,fist,chain,1);BlendChain(result,fist,thumb,1);}
        else
        {
            // Same closed fist as an empty hand squeezing the grip.
            foreach(var chain in fingers){Bend(result,chain,1,false,false);CloseSpread(result,chain,1);}
            PoseThumb(result,1);
        }
        // The handle rests on the thumb side of the curled index finger, just
        // past its middle knuckle; the thumb pad presses on top of it.
        var index=fingers[indexFinger];
        var joint=Vector3.Lerp(result[index[1]].Translation,result[index[2]].Translation,.3f);
        var across=new Vector3(rightHand?-1:1,0,0);
        float handle=screwThickness*.5f,finger=ChainRadius(index)[1];
        screwAxis=joint+across*(finger+handle);
        screwThumb=screwAxis+across*handle;
        ThumbAlongHandle(result,screwThumb,screwAxis);
        // Distance of the pad from the handle's top line at or ahead of the
        // hold point (a thumb resting further forward on the handle is fine).
        var padAt=Vector3.Transform(RimLocalPad(thumb),result[thumb[2]])-screwThumb;
        float ahead=Math.Max(0,padAt.Z);
        ScrewdriverThumbError=new Vector3(padAt.X,padAt.Y,padAt.Z-ahead).Length();
        screwPose=result;screwRevision=revision;return result;
    }
    // 0.1.111: a natural thumb on the handle (the free IK solve bent it at
    // odd angles): a gently, evenly curled thumb, as straight as the reach
    // allows, turned as a whole about its base so the pad lands on the
    // handle and faces it.
    private void ThumbAlongHandle(Matrix4x4[] pose,Vector3 target,Vector3 handleAxis)
    {
        var basePoint=rest[thumb[0]].Translation;int last=thumb[2];var local=RimLocalPad(thumb);
        Matrix4x4.Invert(rest[last],out var inverse);var normalLocal=Vector3.Normalize(Vector3.TransformNormal(-Vector3.UnitY,inverse));
        float want=Vector3.Distance(basePoint,target);
        var trial=(Matrix4x4[])pose.Clone();
        float Reach(float amount)
        {
            foreach(int b in thumb)trial[b]=rest[b];
            Bend(trial,thumb,amount,true,true);
            return Vector3.Distance(basePoint,Vector3.Transform(local,trial[last]));
        }
        // More curl = shorter reach. Keep the straightest curl that reaches,
        // never curled hard: if the hold point is too close even then, the
        // pad rests further forward along the handle instead.
        const float MaxCurl=1f;
        float lo=0,hi=MaxCurl,amount=lo;
        if(Reach(lo)>want)
        {
            if(Reach(hi)>=want)
            {
                amount=hi;float reach=Reach(hi);var d=target-basePoint;
                float t=-d.Z+MathF.Sqrt(Math.Max(0,d.Z*d.Z-d.LengthSquared()+reach*reach));
                var shift=Vector3.UnitZ*Math.Clamp(t,0,.045f);target+=shift;handleAxis+=shift;
            }
            else{for(int i=0;i<24;i++){float mid=(lo+hi)*.5f;if(Reach(mid)>want)lo=mid;else hi=mid;}amount=(lo+hi)*.5f;}
        }
        Reach(amount);
        var pad=Vector3.Transform(local,trial[last]);
        var from=pad-basePoint;var to=target-basePoint;
        if(from.LengthSquared()<1e-10f||to.LengthSquared()<1e-10f)return;
        var aim=FromTo(Vector3.Normalize(from),Vector3.Normalize(to));
        var axis=Vector3.Normalize(to);
        var normal=Vector3.Transform(Vector3.TransformNormal(normalLocal,trial[last]),aim);
        var wanted=handleAxis-target;
        normal-=axis*Vector3.Dot(normal,axis);wanted-=axis*Vector3.Dot(wanted,axis);
        var roll=Quaternion.Identity;
        if(normal.LengthSquared()>1e-10f&&wanted.LengthSquared()>1e-10f)
        {
            normal=Vector3.Normalize(normal);wanted=Vector3.Normalize(wanted);
            float angle=MathF.Atan2(Vector3.Dot(axis,Vector3.Cross(normal,wanted)),Vector3.Dot(normal,wanted));
            roll=Quaternion.CreateFromAxisAngle(axis,angle);
        }
        var turn=Matrix4x4.CreateTranslation(-basePoint)*Matrix4x4.CreateFromQuaternion(Quaternion.Concatenate(aim,roll))*Matrix4x4.CreateTranslation(basePoint);
        foreach(int b in thumb)pose[b]=trial[b]*turn;
    }
    private static Quaternion FromTo(Vector3 a,Vector3 b)
    {
        float dot=Math.Clamp(Vector3.Dot(a,b),-1,1);var cross=Vector3.Cross(a,b);
        if(cross.LengthSquared()<1e-12f)return dot>0?Quaternion.Identity:Quaternion.CreateFromAxisAngle(MathF.Abs(a.X)<.9f?Vector3.Normalize(Vector3.Cross(a,Vector3.UnitX)):Vector3.Normalize(Vector3.Cross(a,Vector3.UnitY)),MathF.PI);
        return Quaternion.CreateFromAxisAngle(Vector3.Normalize(cross),MathF.Acos(dot));
    }
    private readonly Dictionary<string,Matrix4x4[]> ammoPoses=new();
    private int ammoRevision=-1;
    private Matrix4x4[] AmmoPose(string profile)
    {
        if(ammoRevision!=ReloadGripGeometry.Revision){ammoPoses.Clear();ammoRevision=ReloadGripGeometry.Revision;}
        if(ammoPoses.TryGetValue(profile,out var found))return found;
        var result=(Matrix4x4[])rest.Clone();var geometry=ReloadGripGeometry.Get(profile);
        if(geometry==null)return result;
        // 0.1.133: the right hand holds it mirrored (the reload of a gun in the
        // left hand): the left hand's ammunition geometry across x.
        float sx=rightHand?-1:1;
        Vector3 M(Vector3 v)=>new(v.X*sx,v.Y,v.Z);
        bool SegmentClear(Vector3 a,Vector3 b)=>geometry.SegmentClear(M(a),M(b));
        var min=geometry.Min-Vector3.UnitZ*NativeHandMesh.WristZ;
        var max=geometry.Max-Vector3.UnitZ*NativeHandMesh.WristZ;
        if(rightHand){var lo=min.X;min.X=-max.X;max.X=-lo;}
        for(int i=0;i<fingers.Count;i++)
        {
            var chain=fingers[i];var p=rest[chain[0]].Translation;
            if(profile is "shotgun" or "crossbow")
            {
                if(i==indexFinger)AimTips(chain,new Vector3(.014f*sx,-.027f,.023f-NativeHandMesh.WristZ),false);
                else
                {
                    // The remaining fingers relax behind the brass base;
                    // they must not curl into the shell beside the index.
                    float curl=.28f;
                    for(int attempt=0;attempt<8;attempt++)
                    {
                        foreach(int bone in chain)result[bone]=rest[bone];
                        Bend(result,chain,curl,false,false);
                        var origin=Vector3.UnitZ*NativeHandMesh.WristZ;bool clear=true;
                        for(int k=1;k<chain.Length;k++)clear&=SegmentClear(result[chain[k-1]].Translation+origin,result[chain[k]].Translation+origin);
                        var last=chain[^1];Matrix4x4.Invert(rest[last],out var inv);
                        var tip=rest[last].Translation+(rest[last].Translation-rest[chain[^2]].Translation)*.7f;
                        clear&=SegmentClear(result[last].Translation+origin,Vector3.Transform(Vector3.Transform(tip,inv),result[last])+origin);
                        if(clear)break;curl=Math.Max(0,curl-.04f);
                    }
                }
            }
            else
            {
                // Pistol: cup the bottom, leaving its feed lips clear.
                // Rifle: wrap the side, below its upper insertion portion.
                var target=new Vector3(Math.Clamp(p.X,min.X+.004f,max.X-.004f),min.Y-.004f,Math.Clamp(.028f-NativeHandMesh.WristZ,min.Z+.006f,max.Z-.008f));
                if(profile is "ak47" or "m16")target=new Vector3(p.X,min.Y-.005f,.026f-NativeHandMesh.WristZ);
                AimTips(chain,target,false);
            }
        }
        var thumbTarget=profile is "shotgun" or "crossbow"?new Vector3(.036f*sx,-.027f,.023f-NativeHandMesh.WristZ)
            :new Vector3(Math.Clamp(rest[thumb[2]].Translation.X,min.X+.008f,max.X-.008f),max.Y+.004f,Math.Clamp(.024f-NativeHandMesh.WristZ,min.Z+.006f,max.Z-.008f));
        if(profile=="pistol")thumbTarget=new Vector3(rightHand?min.X-.004f:max.X+.004f,
            max.Y+.004f,min.Z+.004f);
        AimTips(thumb,thumbTarget,true);
        ammoPoses[profile]=result;return result;
        void AimTips(int[] chain,Vector3 target,bool isThumb)
        {
            int last=chain[^1];Matrix4x4.Invert(rest[last],out var inv);
            var tip=rest[last].Translation+(rest[last].Translation-rest[chain[^2]].Translation)*.7f;
            var local=Vector3.Transform(tip,inv);var total=new float[chain.Length];
            bool Clear(Matrix4x4[] pose)
            {
                var pivot=Vector3.UnitZ*NativeHandMesh.WristZ;
                for(int k=1;k<chain.Length;k++)if(!SegmentClear(pose[chain[k-1]].Translation+pivot,pose[chain[k]].Translation+pivot))return false;
                return SegmentClear(pose[last].Translation+pivot,Vector3.Transform(local,pose[last])+pivot);
            }
            if(!isThumb)
            {
                var trial=(Matrix4x4[])result.Clone();
                float splay=0;
                for(int pass=0;pass<64;pass++)
                {
                    bool changed=false;
                    for(int j=chain.Length-1;j>=0;j--)foreach(float step in new[]{.06f,-.06f})
                    {
                        float next=Math.Clamp(total[j]+step,0,j==0?1.55f:1.9f),angle=next-total[j];if(Math.Abs(angle)<1e-6f)continue;
                        var pivot=result[chain[j]].Translation;
                        var direction=rest[chain[Math.Min(j+1,chain.Length-1)]].Translation-rest[chain[Math.Max(0,j-1)]].Translation;
                        var axis=Vector3.Normalize(Vector3.Cross(direction,-Vector3.UnitY));
                        var turn=Matrix4x4.CreateTranslation(-pivot)*Matrix4x4.CreateFromAxisAngle(axis,angle)*Matrix4x4.CreateTranslation(pivot);
                        Array.Copy(result,trial,result.Length);for(int k=j;k<chain.Length;k++)trial[chain[k]]*=turn;
                        if(Vector3.DistanceSquared(Vector3.Transform(local,trial[last]),target)>=Vector3.DistanceSquared(Vector3.Transform(local,result[last]),target)-1e-8f||!Clear(trial))continue;
                        Array.Copy(trial,result,result.Length);total[j]=next;changed=true;
                    }
                    // Narrow pistol magazines require MCP adduction as well
                    // as curl. A fixed flexion plane left outer fingertips in air.
                    if(profile=="pistol")foreach(float step in new[]{.04f,-.04f})
                    {
                        float next=Math.Clamp(splay+step,-.48f,.48f),angle=next-splay;
                        var pivot=result[chain[0]].Translation;
                        var turn=Matrix4x4.CreateTranslation(-pivot)*Matrix4x4.CreateRotationY(angle)*Matrix4x4.CreateTranslation(pivot);
                        Array.Copy(result,trial,result.Length);foreach(int bone in chain)trial[bone]*=turn;
                        if(Vector3.DistanceSquared(Vector3.Transform(local,trial[last]),target)>=Vector3.DistanceSquared(Vector3.Transform(local,result[last]),target)-1e-8f||!Clear(trial))continue;
                        Array.Copy(trial,result,result.Length);splay=next;changed=true;
                    }
                    if(!changed)break;
                }
                return;
            }
            for(int pass=0;pass<24;pass++)for(int j=chain.Length-1;j>=0;j--)
            {
                var pivot=result[chain[j]].Translation;
                var a=Vector3.Transform(local,result[last])-pivot;var b=target-pivot;
                if(a.LengthSquared()<1e-10f||b.LengthSquared()<1e-10f)continue;
                var direction=rest[chain[Math.Min(j+1,chain.Length-1)]].Translation-rest[chain[Math.Max(0,j-1)]].Translation;
                var axis=isThumb?Vector3.Cross(a,b):Vector3.Cross(direction,-Vector3.UnitY);
                if(axis.LengthSquared()<1e-10f)continue;axis=Vector3.Normalize(axis);
                a-=axis*Vector3.Dot(a,axis);b-=axis*Vector3.Dot(b,axis);
                if(a.LengthSquared()<1e-10f||b.LengthSquared()<1e-10f)continue;
                a=Vector3.Normalize(a);b=Vector3.Normalize(b);
                float angle=Math.Clamp(MathF.Atan2(Vector3.Dot(axis,Vector3.Cross(a,b)),Vector3.Dot(a,b)),-.16f,.16f);
                float limit=isThumb?1.4f:j==0?1.55f:1.9f;
                float next=Math.Clamp(total[j]+angle,0,limit);angle=next-total[j];total[j]=next;
                var turn=Matrix4x4.CreateTranslation(-pivot)*Matrix4x4.CreateFromAxisAngle(axis,angle)*Matrix4x4.CreateTranslation(pivot);
                var trial=(Matrix4x4[])result.Clone();
                for(int k=j;k<chain.Length;k++)trial[chain[k]]*=turn;
                if(Clear(trial))Array.Copy(trial,result,result.Length);
                else total[j]-=angle;
            }
        }
    }
    private readonly Dictionary<bool,Matrix4x4[]> medkitPoses=new();
    private int medkitRevision=-1;
    private Matrix4x4[] MedkitPose(bool large)
    {
        if(medkitRevision!=MedkitGeometry.Revision){medkitPoses.Clear();medkitRevision=MedkitGeometry.Revision;}
        if(medkitPoses.TryGetValue(large,out var existing))return existing;
        var result=(Matrix4x4[])rest.Clone();var size=MedkitGeometry.Size(large);
        float face=MedkitGeometry.PalmFace-size.Y-.003f;
        float edge=MedkitGeometry.FarEdge-NativeHandMesh.WristZ;
        foreach(var chain in fingers)
        {
            var p=rest[chain[0]].Translation;
            // Four fingers wrap over the short edge to the opposite broad face.
            SolveFinger(chain,new Vector3(p.X,face,edge-.012f));
        }
        var index=rest[fingers[indexFinger][0]].Translation;
        // Thumb opposes them on the palm-side face, instead of a trigger pose.
        Solve(thumb,new Vector3(index.X*.6f,MedkitGeometry.PalmFace+.004f,edge-.020f),true);
        // A reachable fingertip alone does not mean the finger avoided the
        // case. Limit ALL phalanges (including the distal tip) against its box.
        // Preserve joint lengths and the case; never squash vertices out of it.
        foreach(var chain in new[]{thumb})
        {
            var desired=(Matrix4x4[])result.Clone();
            var safe=(Matrix4x4[])rest.Clone();float lastSafe=0;
            for(int step=1;step<=24;step++)
            {
                BlendChain(result,desired,chain,step/24f);
                if(!Clear(chain,result))break;
                lastSafe=step/24f;foreach(int i in chain)safe[i]=result[i];
            }
            if(lastSafe<1)
            {
                float low=lastSafe,high=Math.Min(1,lastSafe+1f/24);
                for(int pass=0;pass<10;pass++)
                {float mid=(low+high)*.5f;BlendChain(result,desired,chain,mid);
                    if(Clear(chain,result)){low=mid;foreach(int i in chain)safe[i]=result[i];}else high=mid;}
            }
            foreach(int i in chain)result[i]=safe[i];
        }
        medkitPoses[large]=result;return result;
        bool Clear(int[] chain,Matrix4x4[] pose)
        {
            for(int j=1;j<chain.Length;j++)if(!MedkitGeometry.SegmentClear(pose[chain[j-1]].Translation,pose[chain[j]].Translation,large))return false;
            int last=chain[^1];Matrix4x4.Invert(rest[last],out var inverse);
            var tip=rest[last].Translation+(rest[last].Translation-rest[chain[^2]].Translation)*.7f;
            tip=Vector3.Transform(Vector3.Transform(tip,inverse),pose[last]);
            return MedkitGeometry.SegmentClear(pose[last].Translation,tip,large);
        }
        void SolveFinger(int[] chain,Vector3 target)
        {
            int last=chain[^1];Matrix4x4.Invert(rest[last],out var inv);
            var tip=rest[last].Translation+(rest[last].Translation-rest[chain[^2]].Translation)*.7f;
            var localTip=Vector3.Transform(tip,inv);var angles=new float[chain.Length];
            var trial=(Matrix4x4[])result.Clone();
            // Start outside the case. Each joint step must keep every phalanx
            // outside it; the tip cannot take a shortcut through the lid.
            for(int pass=0;pass<48;pass++)
            {
                bool changed=false;
                for(int j=chain.Length-1;j>=0;j--)foreach(float step in new[]{.06f,-.06f})
                {
                    float next=Math.Clamp(angles[j]+step,0,j==0?1.4f:1.8f),delta=next-angles[j];if(Math.Abs(delta)<1e-5f)continue;
                    var pivot=result[chain[j]].Translation;
                    var direction=rest[chain[Math.Min(j+1,chain.Length-1)]].Translation-rest[chain[Math.Max(0,j-1)]].Translation;
                    var axis=Vector3.Normalize(Vector3.Cross(direction,-Vector3.UnitY));
                    var turn=Matrix4x4.CreateTranslation(-pivot)*Matrix4x4.CreateFromAxisAngle(axis,delta)*Matrix4x4.CreateTranslation(pivot);
                    Array.Copy(result,trial,result.Length);for(int k=j;k<chain.Length;k++)trial[chain[k]]*=turn;
                    float before=Vector3.DistanceSquared(Vector3.Transform(localTip,result[last]),target);
                    float after=Vector3.DistanceSquared(Vector3.Transform(localTip,trial[last]),target);
                    if(after>=before-1e-8f||!Clear(chain,trial))continue;
                    Array.Copy(trial,result,result.Length);angles[j]=next;changed=true;
                }
                if(!changed)break;
            }
        }
        void Solve(int[] chain,Vector3 target,bool isThumb)
        {
            int last=chain[^1];Matrix4x4.Invert(rest[last],out var inv);
            var tip=rest[last].Translation+(rest[last].Translation-rest[chain[^2]].Translation)*.7f;
            var localTip=Vector3.Transform(tip,inv);var angles=new float[chain.Length];
            // Bounded CCD preserves bone lengths. Fingers flex in their own
            // sagittal plane; only the thumb needs opposition in three axes.
            for(int pass=0;pass<16;pass++)for(int j=chain.Length-1;j>=0;j--)
            {
                var pivot=result[chain[j]].Translation;
                var end=Vector3.Transform(localTip,result[last]);var a=end-pivot;var b=target-pivot;
                if(a.LengthSquared()<1e-10f||b.LengthSquared()<1e-10f)continue;
                Vector3 axis;
                if(isThumb)axis=Vector3.Cross(a,b);
                else
                {
                    var direction=rest[chain[Math.Min(j+1,chain.Length-1)]].Translation-rest[chain[Math.Max(0,j-1)]].Translation;
                    axis=Vector3.Cross(direction,-Vector3.UnitY);
                }
                if(axis.LengthSquared()<1e-10f)continue;axis=Vector3.Normalize(axis);
                a-=axis*Vector3.Dot(a,axis);b-=axis*Vector3.Dot(b,axis);
                if(a.LengthSquared()<1e-10f||b.LengthSquared()<1e-10f)continue;
                a=Vector3.Normalize(a);b=Vector3.Normalize(b);
                float delta=Math.Clamp(MathF.Atan2(Vector3.Dot(axis,Vector3.Cross(a,b)),Vector3.Dot(a,b)),-.22f,.22f);
                if(!isThumb){float next=Math.Clamp(angles[j]+delta,0,j==0?1.65f:1.92f);delta=next-angles[j];angles[j]=next;}
                var turn=Matrix4x4.CreateTranslation(-pivot)*Matrix4x4.CreateFromAxisAngle(axis,delta)*Matrix4x4.CreateTranslation(pivot);
                for(int k=j;k<chain.Length;k++)result[chain[k]]*=turn;
            }
        }
    }
    private static float Relaxed(float grip)=>.20f+.80f*grip;
    private void CloseSpread(Matrix4x4[] result,int[] chain,float amount)
    {
        // Remove the bind pose's lateral splay before folding. Then bring the
        // outer fingers a few mm inward; knuckles and all bone lengths stay put.
        var basePoint=rest[chain[0]].Translation;
        var restAxis=rest[chain[1]].Translation-basePoint;
        float yaw=MathF.Atan2(restAxis.X,restAxis.Z);
        var unsplay=Matrix4x4.CreateTranslation(-basePoint)*Matrix4x4.CreateRotationY(-yaw*amount)*Matrix4x4.CreateTranslation(basePoint);
        foreach(int i in chain)result[i]*=unsplay;
        float center=fingers.Average(c=>rest[c[0]].Translation.X);
        var tip=result[chain[1]].Translation-basePoint;
        float shift=(center-basePoint.X)*.17f*amount;
        float roll=Math.Clamp(shift/Math.Max(.015f,-tip.Y),-.22f,.22f);
        var close=Matrix4x4.CreateTranslation(-basePoint)*Matrix4x4.CreateRotationZ(roll)*Matrix4x4.CreateTranslation(basePoint);
        foreach(int i in chain)result[i]*=close;
    }
    private void PoseThumb(Matrix4x4[] result,float amount)
    {
        // Thumb lies across the OUTSIDE (palmar surface) of the folded index
        // and middle fingers. Solve a fixed-length chain, never curl it under
        // the palm using the fingers' flexion axis.
        var closed=(Matrix4x4[])rest.Clone();
        for(int f=0;f<fingers.Count;f++){Bend(closed,fingers[f],1,false,false);CloseSpread(closed,fingers[f],1);}
        int middle=Enumerable.Range(0,fingers.Count).Where(i=>i!=indexFinger)
            .OrderBy(i=>Vector3.DistanceSquared(rest[fingers[i][0]].Translation,rest[fingers[indexFinger][0]].Translation)).First();
        Vector3 Fold(int f)=>(closed[fingers[f][1]].Translation+closed[fingers[f][2]].Translation)*.5f;
        var a=Fold(indexFinger);var b=Fold(middle);
        var target=Vector3.Lerp(a,b,.65f)-Vector3.UnitY*.012f;
        var across=b-a;across.Y=0;
        across=across.LengthSquared()>1e-8f?Vector3.Normalize(across):new Vector3(rightHand?1:-1,0,0);
        float l1=Vector3.Distance(rest[thumb[0]].Translation,rest[thumb[1]].Translation);
        float l2=Vector3.Distance(rest[thumb[1]].Translation,rest[thumb[2]].Translation);
        var basePoint=rest[thumb[0]].Translation;
        var end=target-across*(l2*.60f);var delta=end-basePoint;
        float distance=Math.Clamp(delta.Length(),Math.Abs(l1-l2)+.0001f,l1+l2-.0001f);
        var axis=delta.LengthSquared()>1e-8f?Vector3.Normalize(delta):Vector3.UnitZ;
        end=basePoint+axis*distance;
        var pole=Vector3.UnitZ-axis*Vector3.Dot(Vector3.UnitZ,axis);
        if(pole.LengthSquared()<1e-8f)pole=Vector3.UnitY;
        pole=Vector3.Normalize(pole);
        float along=(l1*l1+distance*distance-l2*l2)/(2*distance);
        var joint=basePoint+axis*along+pole*MathF.Sqrt(Math.Max(0,l1*l1-along*along));
        Aim(thumb[0],basePoint,joint-basePoint,rest[thumb[1]].Translation-rest[thumb[0]].Translation);
        Aim(thumb[1],joint,end-joint,rest[thumb[2]].Translation-rest[thumb[1]].Translation);
        Aim(thumb[2],end,across,rest[thumb[2]].Translation-rest[thumb[1]].Translation);
        BlendChain(result,closed,thumb,amount);
        void Aim(int index,Vector3 p,Vector3 desired,Vector3 original)
        {
            var from=Vector3.Normalize(original);var to=Vector3.Normalize(desired);
            float dot=Math.Clamp(Vector3.Dot(from,to),-1,1);var cross=Vector3.Cross(from,to);
            if(cross.LengthSquared()<1e-10f)cross=Math.Abs(from.Y)<.9f?Vector3.Cross(from,Vector3.UnitY):Vector3.Cross(from,Vector3.UnitX);
            var turn=Quaternion.CreateFromAxisAngle(Vector3.Normalize(cross),MathF.Acos(dot));
            closed[index]=rest[index]*Matrix4x4.CreateTranslation(-rest[index].Translation)*Matrix4x4.CreateFromQuaternion(turn)*Matrix4x4.CreateTranslation(p);
        }
    }
    private void BlendChain(Matrix4x4[] result,Matrix4x4[] pose,int[] chain,float amount)
    {
        for(int j=0;j<chain.Length;j++)
        {
            int i=chain[j];var a=rest[i];var b=pose[i];
            if(j>0)
            {
                Matrix4x4.Invert(rest[chain[j-1]],out var ra);Matrix4x4.Invert(pose[chain[j-1]],out var rb);
                a*=ra;b*=rb;
            }
            if(amount<=0)result[i]=a;
            else if(amount>=1)result[i]=b;
            else
            {
                SnapshotPoseMath.Decompose(a,Quaternion.Identity,out var pa,out var qa,out var sa);
                SnapshotPoseMath.Decompose(b,qa,out var pb,out var qb,out var sb);
                result[i]=Matrix4x4.CreateScale(Vector3.Lerp(sa,sb,amount))*Matrix4x4.CreateFromQuaternion(Quaternion.Slerp(qa,qb,amount))*Matrix4x4.CreateTranslation(Vector3.Lerp(pa,pb,amount));
            }
            if(j>0)result[i]*=result[chain[j-1]];
        }
    }
    private void Bend(Matrix4x4[] result,int[] chain,float amount,bool held,bool isThumb)
    {
        var delta=Matrix4x4.Identity;
        for(int j=0;j<chain.Length;j++)
        {
            int i=chain[j];var joint=rest[i].Translation;
            var direction=j+1<chain.Length?rest[chain[j+1]].Translation-joint:joint-rest[chain[j-1]].Translation;
            direction=Vector3.Normalize(direction);
            var thumbTarget=rest[fingers[indexFinger][0]].Translation+new Vector3(rightHand?.018f:-.018f,-.028f,-.018f);
            var toward=isThumb&&j==0?thumbTarget-joint:-Vector3.UnitY;
            var axis=Vector3.Cross(direction,toward);
            if(axis.LengthSquared()<1e-8f)axis=Vector3.UnitX;
            axis=Vector3.Normalize(axis);
            float degrees=isThumb?(j==0?Math.Clamp(MathF.Acos(Math.Clamp(Vector3.Dot(direction,Vector3.Normalize(toward)),-1,1))*180/MathF.PI,35,75):j==1?30:20):(j==0?(held?62:72):j==1?(held?80:100):55);
            var turn=Matrix4x4.CreateTranslation(-joint)*Matrix4x4.CreateFromAxisAngle(axis,degrees*amount*MathF.PI/180)*Matrix4x4.CreateTranslation(joint);
            delta=turn*delta;
            result[i]=rest[i]*delta;
        }
    }
}
