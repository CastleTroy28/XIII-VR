using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.139: an NPC hit by the player's fist (or a held weapon/prop) reacts
// with its skeleton: a punch to the left of the belly
// (from the player) bends it over to that side as if in pain, an uppercut to
// the jaw throws the head and the upper body back, a hook turns the head, a
// punch to the arm swings the arm.
// Everything here is in the NPC's own frame: X right, Y up, Z forward (the way
// it faces), metres and radians; + angle about X bows forward, about Y turns
// right, about Z tilts the top to the left (Unity's rotation convention).
// Two parts per joint, each a damped spring back to the animated pose:
//  - the push: the punch's direction about the joint (r x d), a snap;
//  - the pain: a pose held for a moment (doubled over after a punch to the
//    belly, thrown back after an uppercut), then let go.
internal enum HitRegion{Head,Neck,Chest,Belly,Pelvis,UpperArmL,UpperArmR,ForearmL,ForearmR,LegL,LegR}
internal enum HitJoint{SpineLow=0,SpineMid=1,SpineTop=2,Neck=3,Head=4,ArmL=5,ArmR=6,ForearmL=7,ForearmR=8}
// Joint (and a few landmark) positions in the NPC's frame.
internal struct HitSkeleton
{
    internal Vector3 SpineLow,SpineMid,SpineTop,NeckBase,NeckTop,Head,Chin,ShoulderL,ShoulderR,ElbowL,ElbowR,WristL,WristR,HipL,HipR,KneeL,KneeR;
    internal Vector3 Joint(HitJoint j)=>j switch
    {
        HitJoint.SpineLow=>SpineLow,HitJoint.SpineMid=>SpineMid,HitJoint.SpineTop=>SpineTop,HitJoint.Neck=>NeckBase,HitJoint.Head=>Head,
        HitJoint.ArmL=>ShoulderL,HitJoint.ArmR=>ShoulderR,HitJoint.ForearmL=>ElbowL,_=>ElbowR
    };
}
internal sealed class HitPlan
{
    internal const int Joints=9;
    internal readonly Vector3[] Push=new Vector3[Joints];   // angular velocity added now (rad/s)
    internal readonly Vector3[] Pain=new Vector3[Joints];   // pose held for a moment (rad)
    internal float PainHold,Strength;
    // 0.1.141: how long the NPC is out of it (no shooting, no walking) — the
    // pain pose is held that long, then it comes to.
    internal float Stun;
    internal Vector3 Knockback;   // horizontal, metres (the NPC's frame)
    internal HitRegion Region;internal bool Uppercut,Hook;internal int Side;   // Side: +1 the NPC's right, -1 its left
    internal int ForearmSide=-1;  // 0 left, 1 right: a forearm/hand was hit (the weapon hand may drop it)
    internal HitRegion? Carried;  // 0.1.145: an arm hit: the body part behind it that took the blow too
}
internal static class HitReactionMath
{
    // Hand speed (m/s) to strength 0.1..1: 2 m/s about a quarter, 5 m/s full.
    internal const float SlowSpeed=1f,FullSpeed=5f,HeldBonus=1.25f;
    internal static float Strength(float speed,bool held)
    {
        if(!float.IsFinite(speed))return .1f;
        float s=Math.Clamp((speed-SlowSpeed)/(FullSpeed-SlowSpeed),.1f,1f);
        return held?Math.Min(1f,s*HeldBonus):s;
    }
    // The body part nearest to the point (each part with its own thickness).
    internal static HitRegion Classify(Vector3 p,in HitSkeleton k)
    {
        var up=Vector3.UnitY;
        var headTop=k.Head+up*.20f;
        float best=float.MaxValue;var region=HitRegion.Chest;
        void Try(HitRegion r,Vector3 a,Vector3 b,float thick){float d=Segment(p,a,b)-thick;if(d<best){best=d;region=r;}}
        Try(HitRegion.Head,k.NeckTop,headTop,.11f);
        Try(HitRegion.Neck,k.NeckBase,k.NeckTop,.06f);
        Try(HitRegion.Chest,Lerp(k.SpineMid,k.SpineTop,.5f),k.NeckBase,.15f);
        Try(HitRegion.Belly,k.SpineLow,Lerp(k.SpineMid,k.SpineTop,.5f),.15f);
        Try(HitRegion.Pelvis,k.HipL,k.HipR,.10f);
        Try(HitRegion.UpperArmL,k.ShoulderL,k.ElbowL,.05f);
        Try(HitRegion.UpperArmR,k.ShoulderR,k.ElbowR,.05f);
        Try(HitRegion.ForearmL,k.ElbowL,k.WristL+(k.WristL-k.ElbowL)*.4f,.05f);
        Try(HitRegion.ForearmR,k.ElbowR,k.WristR+(k.WristR-k.ElbowR)*.4f,.05f);
        Try(HitRegion.LegL,k.HipL,k.KneeL,.07f);
        Try(HitRegion.LegR,k.HipR,k.KneeR,.07f);
        // 0.1.146: the chest's thickness reached over the throat (a blow to
        // the throat counted as the chest): at the neck's height it is the neck.
        if(region==HitRegion.Chest&&p.Y>=k.NeckBase.Y-.03f)region=HitRegion.Neck;
        return region;
    }
    private static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*t;
    // The torso parts only (the body behind a hit arm).
    internal static HitRegion ClassifyBody(Vector3 p,in HitSkeleton k)
    {
        float y=p.Y,chest=(k.SpineMid.Y+k.SpineTop.Y)*.5f;
        if(y>=k.NeckBase.Y)return HitRegion.Chest;
        if(y>=chest-.05f)return HitRegion.Chest;
        if(y>=k.SpineLow.Y-.05f)return HitRegion.Belly;
        return HitRegion.Pelvis;
    }
    // The point on the body behind an arm at p: the spine's line at that
    // height (within the torso), in front by the torso's half depth.
    internal static Vector3 BodyBehind(Vector3 p,in HitSkeleton k)
    {
        float y=Math.Clamp(p.Y,Math.Min(k.SpineLow.Y,k.NeckBase.Y),Math.Max(k.SpineLow.Y,k.NeckBase.Y));
        var a=k.SpineLow;var b=k.NeckBase;float t=Math.Abs(b.Y-a.Y)<1e-4f?0:(y-a.Y)/(b.Y-a.Y);
        var spine=a+(b-a)*t;
        return new Vector3(spine.X+Math.Clamp(p.X-spine.X,-.12f,.12f),y,spine.Z+.10f);
    }
    internal static float Segment(Vector3 p,Vector3 a,Vector3 b)
    {
        var ab=b-a;float l=ab.LengthSquared();float t=l<1e-8f?0:Math.Clamp(Vector3.Dot(p-a,ab)/l,0,1);
        return Vector3.Distance(p,a+ab*t);
    }
    // 0.1.146: how hard a shot pushes a body lying still (N s), by the gun.
    internal static float ShotImpulse(string profile)=>profile switch
    {
        "pistol" or "revolver" or "uzi"=>9f,
        "shotgun"=>20f,
        "sniper"=>22f,
        "m60" or "heavy"=>14f,
        "crossbow"=>15f,
        _=>12f
    };
    // Push gains (rad/s at full strength) and the chain each region moves.
    private static readonly (HitJoint j,float gain)[] HeadChain={(HitJoint.SpineLow,1.2f),(HitJoint.SpineMid,2.2f),(HitJoint.SpineTop,4f),(HitJoint.Neck,8f),(HitJoint.Head,12f)};
    private static readonly (HitJoint j,float gain)[] NeckChain={(HitJoint.SpineMid,2f),(HitJoint.SpineTop,4f),(HitJoint.Neck,8f),(HitJoint.Head,5f)};
    private static readonly (HitJoint j,float gain)[] ChestChain={(HitJoint.SpineLow,2.5f),(HitJoint.SpineMid,4.5f),(HitJoint.SpineTop,5.5f)};
    private static readonly (HitJoint j,float gain)[] BellyChain={(HitJoint.SpineLow,4f),(HitJoint.SpineMid,3f)};
    private static readonly (HitJoint j,float gain)[] PelvisChain={(HitJoint.SpineLow,3f)};
    // A strike at point p (NPC frame) moving along d (NPC frame).
    // 0.1.145: an arm hit carries this much into the body behind it.
    internal const float ArmCarry=.8f;
    // 0.1.145: the bends were hardly seen: a little more of everything.
    internal const float Visible=1.5f;
    internal static HitPlan Plan(Vector3 p,Vector3 d,float speed,bool held,in HitSkeleton k)=>Plan(p,d,speed,held,k,1f);
    private static HitPlan Plan(Vector3 p,Vector3 d,float speed,bool held,in HitSkeleton k,float scale)
    {
        var plan=new HitPlan();
        if(d.LengthSquared()<1e-8f||!Finite(d)||!Finite(p))return plan;
        d=Vector3.Normalize(d);
        float s=Strength(speed,held)*scale;plan.Strength=Strength(speed,held);
        var region=scale<1?ClassifyBody(p,k):Classify(p,k);plan.Region=region;
        // Which side of the body (the spine) it landed on.
        float lateral=p.X-(k.SpineMid.X+k.SpineTop.X)*.5f;
        plan.Side=lateral>=0?1:-1;
        float strong=.5f+.5f*s;
        switch(region)
        {
            case HitRegion.Head:
            {
                PushChain(plan,HeadChain,p,d,s,k);
                // Up into the jaw from below: the head and the upper body go back.
                bool jaw=p.Y<k.Head.Y+.07f;
                if(d.Y>.35f&&(jaw||d.Y>.6f))
                {
                    plan.Uppercut=true;float u=s*Math.Min(1,d.Y/.7f);
                    plan.Push[(int)HitJoint.Head].X-=10f*u;plan.Push[(int)HitJoint.Neck].X-=7f*u;plan.Push[(int)HitJoint.SpineTop].X-=4f*u;plan.Push[(int)HitJoint.SpineMid].X-=2f*u;
                    Pain(plan,HitJoint.Head,new Vector3(-.30f,0,0)*strong);Pain(plan,HitJoint.Neck,new Vector3(-.22f,0,0)*strong);
                    Pain(plan,HitJoint.SpineTop,new Vector3(-.16f,0,0)*strong);Pain(plan,HitJoint.SpineMid,new Vector3(-.08f,0,0)*strong);
                    plan.PainHold=.25f+.25f*s;
                    plan.Knockback=Flat(d,.30f*s)+new Vector3(0,0,-.10f*s);
                }
                else
                {
                    // A hook (across the face) turns the head; a straight punch snaps it back.
                    plan.Hook=Math.Abs(d.X)>.55f;
                    Pain(plan,HitJoint.Head,new Vector3(.08f,0,-plan.Side*.06f)*strong);Pain(plan,HitJoint.Neck,new Vector3(.05f,0,0)*strong);
                    plan.PainHold=.15f+.2f*s;
                    plan.Knockback=Flat(d,.22f*s);
                }
                break;
            }
            case HitRegion.Neck:
                // 0.1.146:
                // the head drops forward and stays down a while, the shoulders hunch.
                PushChain(plan,NeckChain,p,d,s,k);
                plan.Push[(int)HitJoint.Neck].X+=5f*s;plan.Push[(int)HitJoint.Head].X+=6f*s;
                Pain(plan,HitJoint.Neck,new Vector3(.38f,0,0)*strong);Pain(plan,HitJoint.Head,new Vector3(.34f,0,0)*strong);
                Pain(plan,HitJoint.SpineTop,new Vector3(.14f,0,0)*strong);Pain(plan,HitJoint.SpineMid,new Vector3(.06f,0,0)*strong);
                plan.PainHold=.6f+.5f*s;plan.Knockback=Flat(d,.18f*s);
                break;
            case HitRegion.Chest:
                PushChain(plan,ChestChain,p,d,s,k);
                // A moment later a short bow (the wind knocked out).
                Pain(plan,HitJoint.SpineTop,new Vector3(.10f,plan.Side*.05f,0)*strong);Pain(plan,HitJoint.Neck,new Vector3(.08f,0,0)*strong);Pain(plan,HitJoint.Head,new Vector3(.06f,0,0)*strong);
                plan.PainHold=.15f+.2f*s;plan.Knockback=Flat(d,.22f*s);
                break;
            case HitRegion.Belly:
            case HitRegion.Pelvis:
            {
                PushChain(plan,region==HitRegion.Belly?BellyChain:PelvisChain,p,d,s,k);
                // Doubled over, bent towards the side that was hit (the right
                // side hit: -Z tilts the top to its right) and that side pulled
                // back (a turn towards it).
                float side=plan.Side;float f=region==HitRegion.Belly?1f:.6f;
                Pain(plan,HitJoint.SpineLow,new Vector3(.24f,side*.07f,-side*.09f)*strong*f);
                Pain(plan,HitJoint.SpineMid,new Vector3(.33f,side*.08f,-side*.18f)*strong*f);
                Pain(plan,HitJoint.SpineTop,new Vector3(.27f,side*.05f,-side*.15f)*strong*f);
                Pain(plan,HitJoint.Neck,new Vector3(.14f,0,0)*strong*f);Pain(plan,HitJoint.Head,new Vector3(.14f,0,-side*.06f)*strong*f);
                plan.PainHold=.35f+.45f*s;plan.Knockback=Flat(d,.18f*s);
                break;
            }
            case HitRegion.UpperArmL:case HitRegion.UpperArmR:
            case HitRegion.ForearmL:case HitRegion.ForearmR:
            {
                // 0.1.145: every punch in
                // the log landed on the arms held out in front (with the gun or
                // up as a guard) and only the arm twitched. The blow goes on
                // into the body behind them: the arm is knocked aside and the
                // body takes it like a punch to the chest or the belly
                // (whichever is at that height), a little softer.
                bool fore=region==HitRegion.ForearmL||region==HitRegion.ForearmR;
                bool right=region==HitRegion.UpperArmR||region==HitRegion.ForearmR;if(fore)plan.ForearmSide=right?1:0;
                var arm=right?HitJoint.ArmR:HitJoint.ArmL;
                // 0.1.146: a much harder swing of the arm, on softer springs
                // (HitReactionState: the arms swing out and back).
                if(fore)Push(plan,right?HitJoint.ForearmR:HitJoint.ForearmL,p,d,30f*s,k);
                Push(plan,arm,p,d,(fore?15f:26f)*s,k);
                var body=BodyBehind(p,k);float t=ArmCarry;
                var inner=Plan(body,d,speed,held,k,t);
                for(int j=0;j<HitPlan.Joints;j++){plan.Push[j]+=inner.Push[j];plan.Pain[j]+=inner.Pain[j];}
                plan.PainHold=Math.Max(inner.PainHold,.15f);plan.Knockback=inner.Knockback;
                plan.Carried=inner.Region;
                break;
            }
            default:
                Push(plan,HitJoint.SpineLow,p,d,2f*s,k);
                Pain(plan,HitJoint.SpineMid,new Vector3(.08f,0,0)*strong);plan.PainHold=.2f;
                break;
        }
        if(scale>=1)
        {
            for(int j=0;j<HitPlan.Joints;j++){plan.Push[j]*=Visible;plan.Pain[j]*=Visible;}
            plan.PainHold*=.42f;
            plan.Stun=StunSeconds(plan);
        }
        // 0.1.141: the pain pose is held while it is stunned.
        plan.PainHold=Math.Max(plan.PainHold,plan.Stun-PainRise);
        return plan;
    }
    // 0.1.187: brief hit stun (~0.15..0.95 s). Strength and hit location
    // still matter; a hard head/body hit interrupts more than a glancing limb hit.
    internal const float MaxStun=.95f;
    internal static float StunSeconds(HitPlan plan)
    {
        float s=Math.Clamp(float.IsFinite(plan.Strength)?plan.Strength:0,0,1);
        float t=.16f+.58f*s;
        var region=plan.Carried??plan.Region;
        float f=region switch
        {
            HitRegion.Head=>plan.Uppercut?1.3f:1.1f,HitRegion.Neck=>1.2f,HitRegion.Chest=>.9f,HitRegion.Belly=>1.25f,HitRegion.Pelvis=>.8f,
            HitRegion.UpperArmL or HitRegion.UpperArmR=>.4f,HitRegion.ForearmL or HitRegion.ForearmR=>.3f,_=>.6f
        };
        return Math.Min(MaxStun,t*f*(plan.Carried!=null?ArmCarry:1f));
    }
    private static void PushChain(HitPlan plan,(HitJoint j,float gain)[] chain,Vector3 p,Vector3 d,float s,in HitSkeleton k)
    {foreach(var (j,gain) in chain)Push(plan,j,p,d,gain*s,k);}
    // The punch about a joint: axis r x d (for a point above the joint and a
    // push backwards this leans the part back), as strong as the lever allows.
    private static void Push(HitPlan plan,HitJoint j,Vector3 p,Vector3 d,float gain,in HitSkeleton k)
    {
        var r=p-k.Joint(j);float l=r.Length();if(l<1e-4f)return;
        var w=Vector3.Cross(r,d)/Math.Max(l,.12f);
        plan.Push[(int)j]+=w*gain;
    }
    private static void Pain(HitPlan plan,HitJoint j,Vector3 angles)=>plan.Pain[(int)j]+=angles;
    private static Vector3 Flat(Vector3 d,float metres){var h=new Vector3(d.X,0,d.Z);float l=h.Length();return l<1e-4f?Vector3.Zero:h/l*metres;}
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
    // The pain pose over time: after a short delay (the push comes first) it
    // rises, holds, and fades out.
    // 0.1.187: react promptly, then release the pain pose over 0.30 s.
    internal const float PainDelay=.035f,PainRise=.16f,PainFade=.30f;
    internal static float Envelope(float t,float hold)
    {
        if(!(t>PainDelay))return 0;t-=PainDelay;
        if(t<PainRise){float u=t/PainRise;return u*u*(3-2*u);}
        t-=PainRise;if(t<hold)return 1;t-=hold;
        if(t<PainFade){float u=1-t/PainFade;return u*u*(3-2*u);}
        return 0;
    }
}
// All joints of one NPC: springs back to the animated pose, pains held for a
// moment, a knockback spread over a quarter of a second.
internal sealed class HitReactionState
{
    // 0.1.141: softer and better damped (slower, fuller movements; it was a twitch).
    // 0.1.147:
    // every spring softer and less damped - the body sways into a blow and
    // settles with a little overshoot.
    internal const float ResponseRate=1.8f;
    internal const float Stiffness=34f,Damping=7f,MaxAngle=.95f,KnockSeconds=.22f;
    // 0.1.146: the arms on softer, less damped springs (a hit arm flies out
    // and swings back over a second) and further; the neck and head a little softer.
    internal const float ArmStiffness=16f,ArmDamping=3.5f,ArmMaxAngle=1.5f,HeadStiffness=26f,HeadDamping=5.5f;
    internal static float StiffnessOf(int j)=>(j>=(int)HitJoint.ArmL?ArmStiffness:j>=(int)HitJoint.Neck?HeadStiffness:Stiffness)*ResponseRate*ResponseRate;
    internal static float DampingOf(int j)=>(j>=(int)HitJoint.ArmL?ArmDamping:j>=(int)HitJoint.Neck?HeadDamping:Damping)*ResponseRate;
    internal static float MaxAngleOf(int j)=>j>=(int)HitJoint.ArmL?ArmMaxAngle:MaxAngle;
    private readonly Vector3[] angle=new Vector3[HitPlan.Joints],rate=new Vector3[HitPlan.Joints];
    private readonly System.Collections.Generic.List<(HitPlan plan,float at)> pains=new();
    private Vector3 knock;private float knockLeft;
    internal float LastHit{get;private set;}=-100;
    internal Vector3 Angle(int j)=>angle[j];
    internal void Add(HitPlan plan,float now)
    {
        for(int j=0;j<HitPlan.Joints;j++)rate[j]+=plan.Push[j]*ResponseRate;
        if(pains.Count>=4)pains.RemoveAt(0);
        pains.Add((plan,now));LastHit=now;
        if(plan.Knockback.LengthSquared()>1e-6f){float f=knockLeft/KnockSeconds;knock=knock*(f*f)+plan.Knockback;knockLeft=KnockSeconds;}
    }
    // Advances by dt; returns how far the NPC is pushed this step (its frame).
    internal Vector3 Step(float dt,float now)
    {
        if(!(dt>0))return Vector3.Zero;
        dt=Math.Min(dt,.05f);
        var target=new Vector3[HitPlan.Joints];
        for(int i=pains.Count-1;i>=0;i--)
        {
            var (plan,at)=pains[i];float e=HitReactionMath.Envelope(now-at,plan.PainHold);
            if(e<=0&&now-at>HitReactionMath.PainDelay+HitReactionMath.PainRise+plan.PainHold+HitReactionMath.PainFade){pains.RemoveAt(i);continue;}
            for(int j=0;j<HitPlan.Joints;j++)target[j]+=plan.Pain[j]*e;
        }
        for(int j=0;j<HitPlan.Joints;j++)
        {
            // Semi-implicit steps (stable at 30..144 Hz).
            rate[j]+=(StiffnessOf(j)*(target[j]-angle[j])-DampingOf(j)*rate[j])*dt;
            angle[j]+=rate[j]*dt;
            float max=MaxAngleOf(j);
            float l=angle[j].Length();if(l>max){angle[j]*=max/l;float along=Vector3.Dot(rate[j],angle[j]/max);if(along>0)rate[j]-=angle[j]/max*along;}
        }
        if(knockLeft<=0)return Vector3.Zero;
        // Ease out: most of the push at once.
        float before=knockLeft/KnockSeconds;knockLeft=Math.Max(0,knockLeft-dt);float after=knockLeft/KnockSeconds;
        float moved=before*before-after*after;
        var step=knock*moved;if(knockLeft<=0)knock=Vector3.Zero;
        return step;
    }
    internal bool Settled
    {
        get
        {
            if(pains.Count>0||knockLeft>0)return false;
            for(int j=0;j<HitPlan.Joints;j++)if(angle[j].LengthSquared()>1e-6f||rate[j].LengthSquared()>1e-4f)return false;
            return true;
        }
    }
}
