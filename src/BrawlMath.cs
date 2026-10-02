using System;
namespace XiiiXR;
// 0.1.145: when a
// disarmed enemy comes at the player, faces them and swings, and whether a
// swing lands. Distances are flat (metres), from the enemy to the player's head.
internal static class BrawlMath
{
    internal enum Step{Wait,Chase,Face,Swing}
    // Swings within this reach; a swing lands within LandReach (the player
    // may step back while it comes; 0.2 s since 0.1.148).
    internal const float Reach=1.3f,LandReach=1.55f,ChaseFrom=1.1f,GiveUp=14f;
    internal const float FaceDot=.55f,LandDot=.3f,HitDelay=.28f,MinGap=1.2f,MaxGap=2.0f;
    // The game's own punch hurt the player this recently: no extra swing of ours.
    internal const float GameHurtHolds=2.5f;
    internal static Step Decide(float distance,float facingDot,bool stunned,bool gameHurtRecently,float now,float nextSwing)
    {
        if(stunned||!float.IsFinite(distance)||distance>GiveUp)return Step.Wait;
        if(distance>Reach)return Step.Chase;
        if(facingDot<FaceDot)return Step.Face;
        if(gameHurtRecently||now<nextSwing)return Step.Face;
        return Step.Swing;
    }
    internal static bool Lands(float distance,float facingDot)=>float.IsFinite(distance)&&distance<=LandReach&&facingDot>=LandDot;
    // Seconds to the next swing; random01 in 0..1.
    internal static float Gap(float random01)=>MinGap+(MaxGap-MinGap)*Math.Clamp(float.IsFinite(random01)?random01:.5f,0,1);
    // Damage of one punch: percent (1..30, 7 by default) of the player's full health.
    internal const float DefaultPercent=7;
    internal static float Damage(float maxHealth,float percent)
    {
        if(!float.IsFinite(percent)||percent<=0)percent=DefaultPercent;
        percent=Math.Clamp(percent,1,30);
        if(!float.IsFinite(maxHealth)||maxHealth<=0)maxHealth=100;
        return maxHealth*percent/100f;
    }
    // A readable 120 ms wind-up, extension at 280 ms and recovery before the
    // next strike. BrawlPose uses the same curve; contact is only active on extension.
    internal const float WindUp=.12f,Hold=.04f,Back=.22f;
    // The body steps into the punch this far (m) and the torso turns this much (rad) at full reach.
    internal const float Lunge=.13f,TorsoTurn=.45f,Lean=.12f;
    internal static float Punch(float t,out bool pulling)
    {
        pulling=false;
        if(!float.IsFinite(t)||t<0)return 0;
        if(t<WindUp){pulling=true;return 0;}
        if(t<HitDelay){float u=1-(t-WindUp)/(HitDelay-WindUp);return 1-u*u*u;}
        if(t<HitDelay+Hold)return 1;
        if(t<HitDelay+Hold+Back){float u=1-(t-HitDelay-Hold)/Back;return u*u*(3-2*u);}
        return 0;
    }
    // How far the fist is drawn back while cocking (0..1, 1 at the end of the wind-up).
    internal static float Cock(float t)=>float.IsFinite(t)&&t>=0&&t<WindUp?MathF.Sin(t/WindUp*MathF.PI*.5f):0;
    // A hook swings out to the side on its way (0 at the guard and at the
    // player, most half way).
    internal static float HookArc(float reach)=>float.IsFinite(reach)?MathF.Sin(Math.Clamp(reach,0,1)*MathF.PI):0;
    internal const float HookChance=.35f,HookWidth=.30f;
    // Which fist: alternating, the first one by the side the player stands on.
    internal static bool LeftFirst(float playerSideDot)=>playerSideDot<0;
}
// 0.1.147: its fight as a
// small state machine. Step() returns how fast it moves along the line to
// the player (m/s, + towards, - away); a punch starts when SwingStarted.
internal enum BrawlAct{Guard,Close,StepIn,Swing,StepBack,Block,Recover,Feint}
// 0.1.153: each brawler its own way of fighting,
// picked once at random: how fast it moves and punches, how eager it is, how
// much it circles the player, its stance (which fist leads, the body turned
// side on), how high and wide it holds its guard, how it bobs.
internal sealed class BrawlStyle
{
    internal float Speed=1,Aggression=1,Circle,PunchSpeed=1,Hook=BrawlMath.HookChance;
    internal bool LeadLeft=true;internal float Blade,GuardHigh,GuardWide,GuardForward,BobRate=2.1f,BobSize=1,Crouch,Pace=1,Phase;
    internal static readonly BrawlStyle Neutral=new();
    // Circling the player at most this fast (m/s), turning the other way every 1-2.6 s.
    internal const float CircleSpeed=.72f;
    internal static BrawlStyle From(Func<float> random01)
    {
        float R(float a,float b){float r=random01();return a+(b-a)*Math.Clamp(float.IsFinite(r)?r:.5f,0,1);}
        var s=new BrawlStyle
        {
            Speed=R(.95f,1.15f),Aggression=R(.95f,1.45f),Circle=R(.35f,1f),PunchSpeed=R(.88f,1.15f),Hook=R(.15f,.55f),
            LeadLeft=random01()<.7f,Blade=R(10,24),GuardHigh=R(-.04f,.05f),GuardWide=R(-.03f,.04f),GuardForward=R(-.03f,.05f),
            BobRate=R(1.5f,2.8f),BobSize=R(.6f,1.5f),Crouch=R(0,.07f),Pace=R(.9f,1.12f),Phase=R(0,6.28f),
        };
        return s;
    }
}
// All choices are made on state transitions; presentation and damage do not drive this clock.
internal sealed class BrawlPlan
{
    internal const float CloseFrom=2.3f,Keep=1.25f,TooClose=.65f,Attack=1.55f,Contact=.9f;
    internal const float CloseSpeed=1.65f,DriftSpeed=.65f,StepInSpeed=1.8f,StepBackSpeed=1.15f,BackOff=.95f;
    internal const float StepInTime=.28f,StepBackTime=.30f,SwingTime=.62f,BlockTime=.26f,CounterChance=.28f;
    internal BrawlAct Act{get;private set;}=BrawlAct.Guard;
    internal readonly BrawlStyle Style;
    internal BrawlPlan(BrawlStyle? style=null,float readyAt=0)
    {Style=style??BrawlStyle.Neutral;LeftFist=!Style.LeadLeft;until=readyAt;if(readyAt>0)Act=BrawlAct.Recover;}
    internal float Side{get;private set;}
    private float circleDir=1,circleFlip,until,last=-1;
    private int swingsLeft;
    internal float Since{get;private set;}
    internal bool SwingStarted{get;private set;}
    internal bool LeftFist{get;private set;}
    internal int Swings{get;private set;}
    internal float Stamina{get;private set;}=1;
    private void Set(BrawlAct act,float now,float end=0){Act=act;Since=now;until=end;}
    internal void Interrupt(float now)
    {SwingStarted=false;swingsLeft=0;Side=0;Set(BrawlAct.Recover,now,now+.14f);}
    internal float Step(float now,float distance,bool stunned,bool struck,float r1,float r2,bool canAttack=true)
    {
        SwingStarted=false;Side=0;
        if(!float.IsFinite(now))return 0;
        float dt=last<0?0:Math.Clamp(now-last,0,.1f);last=now;
        Stamina=Math.Clamp(Stamina+dt*(Act==BrawlAct.Swing?.04f:.23f),0,1);
        r1=Unit(r1);r2=Unit(r2);
        if(stunned||!float.IsFinite(distance)||distance>BrawlMath.GiveUp)
        {Interrupt(now);return 0;}
        if(struck){Interrupt(now);Set(BrawlAct.Block,now,now+BlockTime);return 0;}
        float along=Along(now,distance,r1,r2,canAttack);
        if(now>=circleFlip){circleFlip=now+1.4f+1.8f*r1;if(r1<.55f)circleDir=-circleDir;}
        // A short lateral adjustment, then plant both feet; no perpetual orbit.
        bool shift=circleFlip-now>.8f;
        float side=BrawlStyle.CircleSpeed*Style.Circle*Style.Speed;
        if(Act==BrawlAct.Guard&&distance>=TooClose&&distance<=CloseFrom&&shift)Side=circleDir*side*.65f;
        else if(Act==BrawlAct.Close&&distance<4)Side=circleDir*side*.22f;
        else if(Act==BrawlAct.StepBack)Side=circleDir*side*.65f;
        return along*Style.Speed;
    }
    private float Along(float now,float d,float r1,float r2,bool canAttack)
    {
        switch(Act)
        {
            case BrawlAct.Recover:
                if(now>=until)Set(BrawlAct.Guard,now,now+.08f);
                return 0;
            case BrawlAct.Guard:
                if(d<TooClose){Set(BrawlAct.StepBack,now);return -BackOff;}
                if(d>CloseFrom){Set(BrawlAct.Close,now);return CloseSpeed;}
                if(canAttack&&now>=until&&d<Attack&&Stamina>.38f)
                {
                    if(r1<.2f){Set(BrawlAct.Feint,now,now+.32f);return 0;}
                    Attack_(now,d,r2);return Act==BrawlAct.StepIn?StepInSpeed:0;
                }
                return d>Keep+.2f?DriftSpeed:0;
            case BrawlAct.Close:
                if(d<=Keep){Set(BrawlAct.Guard,now,now+(.16f+.25f*r1)/Style.Aggression);return 0;}
                return CloseSpeed;
            case BrawlAct.Feint:
                if(now>=until){Set(BrawlAct.Guard,now,now+.12f);}
                return d<TooClose?-BackOff:0;
            case BrawlAct.StepIn:
                if(!canAttack||d>Attack+.3f){Set(BrawlAct.Guard,now,now+.3f);return 0;}
                if(d<=Contact||now-Since>=StepInTime)
                {if(d<=Keep)Punch(now);else Set(BrawlAct.Guard,now,now+.25f);return 0;}
                return StepInSpeed;
            case BrawlAct.Swing:
                if(now-Since>=SwingTime/Style.PunchSpeed)
                {
                    if(swingsLeft>0&&canAttack&&d<=Keep&&d>TooClose&&Stamina>.2f)Punch(now);
                    else{Set(BrawlAct.StepBack,now);return -StepBackSpeed;}
                }
                return 0; // A planted punch, never skate at the target during extension.
            case BrawlAct.StepBack:
                if(now-Since>=StepBackTime||d>Attack+.15f)
                {Set(BrawlAct.Guard,now,now+(.25f+.35f*r1)/Style.Aggression);return 0;}
                return -StepBackSpeed;
            default: // Cover after taking a hit, then reassess; no instant retaliation through stun.
                if(now>=until)Set(BrawlAct.Guard,now,now+.10f);
                return d<TooClose?-BackOff*.5f:0;
        }
    }
    private void Attack_(float now,float distance,float r)
    {
        swingsLeft=Stamina>.85f&&r>.85f?3:Stamina>.55f&&r>.3f?2:1;
        if(distance>Contact+.08f)Set(BrawlAct.StepIn,now);else Punch(now);
    }
    private void Punch(float now)
    {swingsLeft--;Swings++;Stamina=Math.Max(0,Stamina-.22f);LeftFist=!LeftFist;SwingStarted=true;Set(BrawlAct.Swing,now);}
    private static float Unit(float x)=>Math.Clamp(float.IsFinite(x)?x:.5f,0,1);
}
