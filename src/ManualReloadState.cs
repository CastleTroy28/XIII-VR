using System;
using System.Numerics;
namespace XiiiXR;
internal enum ReloadAction { None, DropInstalled, TakeInstalled, TakeSupply, DropHeld, Insert, RackBack, Chamber, OpenCover, CloseCover }
internal sealed class ManualReloadState
{
    internal float FullTravel=>shell?.0735f:rifle?.050f:lidded?.060f:.045f;
    internal const float CoverReach=.12f,BoxReach=.13f;
    internal bool Installed{get;private set;}=true;
    internal bool NeedsRack{get;private set;}
    internal bool Holding{get;private set;}
    internal bool DiscardOnly{get;private set;}
    internal int HeldRounds{get;private set;}
    internal bool Hint{get;private set;}
    internal float RackTravel{get;private set;}
    private float maxBack;
    internal bool Racking=>racking;
    internal bool SlideLocked{get;private set;}
    internal float VisualRackTravel=>SlideLocked?Math.Max(FullTravel,RackTravel):RackTravel;
    internal void ObserveRounds(int rounds)
    {if(!shell&&!rifle&&!arrow&&!lidded&&Installed&&!racking&&rounds==0){SlideLocked=true;NeedsRack=true;}}
    internal bool Active=>Holding||Hint||pressing||CoverOpen||(racking&&(!shell||NeedsRack||RackTravel>.008f))||!Installed||NeedsRack;
    internal bool BlocksFire=>!Installed||NeedsRack||pressing||CoverOpen||(racking&&(!shell||NeedsRack||RackTravel>.008f));
    // 0.1.119: M60 — B opens the top cover; with it open the ammunition box
    // is taken off / a new one put on with the left trigger; the cover is
    // closed by taking it with the left trigger; then the charging handle.
    internal bool CoverOpen{get;private set;}
    // 0.1.122: crossbow — the held bolt is laid on the rail at the muzzle end
    // (its rear within 5 cm of the rail line, aligned with it) and drawn back
    // until its rear reaches the string. RailOffset: how far the rear still is
    // ahead of the string (the resistance haptics follow its change).
    internal bool OnRail{get;private set;}
    internal float RailOffset{get;private set;}
    internal const float RailCatch=.05f,RailLose=.09f;
    // 0.1.146: under the rail (into the stock) the bolt is held up by it: laid
    // on from at most RailDeep below, kept down to RailSink below.
    internal const float RailDeep=.06f,RailSink=.18f;
    // The bolt's offset from the rail line (across it): off = aside or
    // above it; below = how far under the rail (the stock holds it up).
    internal static void RailSplit(Vector3 across,Vector3 axis,out float off,out float below)
    {
        var up=Vector3.UnitY-axis*Vector3.Dot(Vector3.UnitY,axis);
        if(up.LengthSquared()<1e-6f){off=across.Length();below=0;return;}
        up=Vector3.Normalize(up);
        float height=Vector3.Dot(across,up);var aside=across-up*height;
        below=Math.Max(0,-height);
        off=MathF.Sqrt(aside.LengthSquared()+Math.Max(0,height)*Math.Max(0,height));
    }
    private bool pressing,racking,insertEntered,pulled,previousTipValid;
    private bool supplyArmed=true;
    // 0.1.223: the M60's box in the hand has been away from its place (it goes in where it is taken from).
    private bool boxAway;
    private float pressAt;
    private Vector3 rackStart,insertStart,previousTip;
    // 0.1.117: arrow = crossbow: one bolt pushed along the rail like a shell,
    // but nothing to rack afterwards and no magazine to drop.
    private readonly bool shell,rifle,arrow,lidded;
    private bool Single=>shell||arrow;
    internal ManualReloadState(bool shotgun=false,bool assaultRifle=false,bool crossbow=false,bool cover=false)
    {shell=shotgun;rifle=assaultRifle;arrow=crossbow&&!shotgun;lidded=cover&&!shotgun&&!crossbow;}
    private bool spentCase;
    internal bool TakeSpentCase(){bool result=spentCase;spentCase=false;return result;}
    internal void OnShot(){if(shell){spentCase=true;NeedsRack=true;pulled=false;RackTravel=0;}}
    internal bool Pulled=>pulled;
    // 0.1.136: the shotgun pumped with one hand holding its fore-end
    // (InertialPump gives how far the pump is back): back far enough, the
    // spent shell is out; closed again, the next round is in.
    // 0.1.138: the hand at the pump no longer follows it by the grip (the
    // other hand let go of the handle: the gun hangs by the pump): where the
    // pump is stays, the gun's inertia moves it from now on.
    internal void EndRacking(){racking=false;}
    internal ReloadAction InertialRack(float travel)
    {
        if(!shell||!Installed||racking||Holding)return ReloadAction.None;
        if(!NeedsRack&&!pulled){RackTravel=0;return ReloadAction.None;}
        float full=FullTravel;RackTravel=Math.Clamp(float.IsFinite(travel)?travel:0,0,full);
        if(!pulled&&RackTravel>=full*.85f){pulled=true;SlideLocked=false;return ReloadAction.RackBack;}
        if(pulled&&RackTravel<=full*.2f){NeedsRack=pulled=false;RackTravel=0;return ReloadAction.Chamber;}
        return ReloadAction.None;
    }
    internal void Supply(int rounds){racking=false;Holding=true;DiscardOnly=false;HeldRounds=Math.Max(0,rounds);insertEntered=previousTipValid=false;OnRail=false;RailOffset=0;boxAway=true;}
    // 0.1.242: a magazine taken out with rounds left leaves a round in the
    // chamber (as in a real gun): the next magazine goes in ready to fire, no
    // bolt or slide to work. Only an emptied gun (its slide or bolt back, or
    // never chambered) needs racking after the new magazine (and an empty
    // magazine put in: the game counts no round to fire).
    internal bool ChamberKept{get;private set;}
    internal void Detach(int rounds,bool take)
    {
        ObserveRounds(rounds);
        ChamberKept=!shell&&!arrow&&Installed&&!NeedsRack&&!pulled&&!SlideLocked&&rounds>0;
        insertEntered=previousTipValid=false;Installed=false;NeedsRack=true;if(take){Holding=true;DiscardOnly=false;HeldRounds=Math.Max(0,rounds);}pressing=Hint=false;boxAway=false;
    }
    // 0.1.223: the M60's open cover pressed shut by a hand (CoverPush).
    internal ReloadAction PushCoverShut(){if(!lidded||!CoverOpen||Holding)return ReloadAction.None;CoverOpen=false;return ReloadAction.CloseCover;}
    internal void ConsumeHeld(){Holding=false;DiscardOnly=false;HeldRounds=0;insertEntered=previousTipValid=false;OnRail=false;RailOffset=0;}
    internal int ReturnHeld(){int refund=Holding?HeldRounds:0;ConsumeHeld();return refund;}
    internal void Inserted(int previousRounds)
    {supplyArmed=true;Installed=true;NeedsRack=!arrow&&(shell?NeedsRack||previousRounds==0:!(ChamberKept&&HeldRounds>0));if(!NeedsRack&&!shell){SlideLocked=false;RackTravel=0;}ChamberKept=false;ConsumeHeld();}
    // 0.1.183: a full magazine put in at once and the slide let go forward
    // (a pistol struck against the chest; a weapon reloaded by itself).
    internal void QuickLoad()
    {
        ConsumeHeld();Installed=true;NeedsRack=false;SlideLocked=false;ChamberKept=false;
        pressing=Hint=racking=pulled=false;RackTravel=0;maxBack=0;spentCase=false;
    }
    internal int Suspend()
    {
        int refund=ReturnHeld();pressing=Hint=racking=false;supplyArmed=true;
        // A half-cycled shotgun remains open across focus loss/weapon swaps.
        if(!shell)pulled=false;RackTravel=shell&&pulled?FullTravel:0;return refund;
    }
    // 0.1.221: the gun wants rounds from the belt: its magazine out (a box
    // gun: its cover open, no box in), or a single-round gun not full.
    internal bool WantsSupply(int rounds,int capacity)=>!Holding&&(Single?rounds<capacity:!Installed)&&(!lidded||CoverOpen&&!Installed);
    // takeDown/takeHeld (0.1.221): the hand's grip takes, holds and lets go of
    // the magazine or rounds (and the M60's box and cover) in place of its
    // trigger; 0.1.223: it racks a bolt too (the trigger no longer does). From
    // the belt only when the gun wants rounds (the left belt's holster place
    // takes a pistol otherwise).
    internal ReloadAction Step(float now,bool bDown,bool bHeld,bool bUp,bool triggerDown,bool triggerHeld,
        bool atPouch,Vector3 hand,Vector3 magazine,Vector3 port,Vector3 bolt,Vector3 insertAxis,int rounds,int capacity,
        bool gripHeld=false,Vector3? tip=null,bool aligned=true,Vector3? cover=null,float railLength=0,bool? takeDown=null,bool? takeHeld=null)
    {
        if(!float.IsFinite(now)||!float.IsFinite(hand.LengthSquared()))return ReloadAction.None;
        bool gripTakes=takeDown.HasValue;bool tDown=takeDown??triggerDown,tHeld=takeHeld??triggerHeld;
        // Taken once per press at the belt; armed again away from it (held on
        // after a round went in, it takes the next).
        if(!tHeld||!atPouch)supplyArmed=true;
        if(lidded)
        {
            if(bDown&&!CoverOpen&&!Holding&&!racking){CoverOpen=true;return ReloadAction.OpenCover;}
            if(CoverOpen&&!Holding&&tDown)
            {
                float toCover=cover is Vector3 c?Vector3.Distance(hand,c):float.PositiveInfinity;
                float toBox=Installed?Vector3.Distance(hand,magazine):float.PositiveInfinity;
                if(toCover<CoverReach&&toCover<=toBox){CoverOpen=false;return ReloadAction.CloseCover;}
                if(toBox<BoxReach)return ReloadAction.TakeInstalled;
            }
        }
        if(bDown&&!Single&&!lidded&&Installed){pressing=true;pressAt=now;}
        Hint=pressing&&bHeld&&now-pressAt>=.3f;
        if(Hint&&tDown&&!Holding&&Vector3.Distance(hand,magazine)<.11f)return ReloadAction.TakeInstalled;
        if(pressing&&(bUp||!bHeld))
        {
            pressing=Hint=false;
            if(now-pressAt<.3f&&Installed)return ReloadAction.DropInstalled;
        }
        if(Holding)
        {
            racking=false;
            if(!tHeld)return ReloadAction.DropHeld;
            if(DiscardOnly||(!Single&&Installed)||(Single&&rounds>=capacity)||lidded&&!CoverOpen){OnRail=false;return ReloadAction.None;}
            // 0.1.223: the M60's box goes in when brought to its place (where it
            // is taken from), however it is turned - not pushed along a feed line
            // to a point behind it (that took a long time to find).
            if(lidded)
            {
                float seat=Vector3.Distance(hand,magazine);
                if(seat>BoxReach*1.3f)boxAway=true;
                return boxAway&&seat<BoxReach?ReloadAction.Insert:ReloadAction.None;
            }
            var point=tip??hand;var d=point-port;
            if(arrow)
            {
                // point = the held bolt's rear end; port = its place at the string.
                float length=railLength>.05f?railLength:.40f;
                float along=Vector3.Dot(d,insertAxis);
                // 0.1.146: the rail holds the
                // bolt from below - going down into the stock does not take
                // it off; only lifting it off or drawing it aside does.
                RailSplit(d-insertAxis*along,insertAxis,out float off,out float below);
                if(!OnRail){if(aligned&&off<RailCatch&&below<RailDeep&&along>length*.45f&&along<length+.08f)OnRail=true;}
                else if(off>RailLose||below>RailSink||along>length+.15f)OnRail=false;
                if(!OnRail){RailOffset=0;return ReloadAction.None;}
                RailOffset=Math.Clamp(along,0,length);
                if(along<=.02f){OnRail=false;RailOffset=0;return ReloadAction.Insert;}
                return ReloadAction.None;
            }
            float lateral=(d-insertAxis*Vector3.Dot(d,insertAxis)).Length();
            float radius=shell?.065f:rifle||lidded?.060f:arrow?.050f:.045f;
            // Test the feed-end path as well as the endpoint. At low frame
            // rates a valid push can cross the socket between two samples.
            bool crossed=false;
            if(aligned&&previousTipValid&&Vector3.DistanceSquared(point,previousTip)<.30f*.30f)
            {
                float before=Vector3.Dot(previousTip-port,insertAxis),after=Vector3.Dot(d,insertAxis);
                if(before<-.025f&&after>=-.025f&&after-before>.014f)
                {
                    var at=Vector3.Lerp(previousTip,point,(-.025f-before)/(after-before))-port;
                    crossed=(at-insertAxis*Vector3.Dot(at,insertAxis)).Length()<radius;
                }
            }
            previousTip=point;previousTipValid=aligned;
            if(crossed)return ReloadAction.Insert;
            if(d.Length()<.20f&&aligned)
            {
                if(!insertEntered){insertEntered=true;insertStart=point;}
                // Keep the furthest approach sample: a failed side approach must
                // not poison subsequent insertions until B is pressed again.
                if(Vector3.Dot(point-insertStart,insertAxis)<0)insertStart=point;
                if(lateral<radius&&Math.Abs(Vector3.Dot(d,insertAxis))<.055f&&Vector3.Dot(point-insertStart,insertAxis)>.014f)return ReloadAction.Insert;
            }
            else insertEntered=false;
            return ReloadAction.None;
        }
        // 0.1.161: held low (a gun
        // stock), the AK's charging handle is by the belt pouch. The left
        // trigger at the bolt took a magazine from the pouch instead - also
        // half way through a pull, which left the bolt back and unchambered,
        // and every try after that at the bolt took a magazine again. The bolt
        // comes first: a pull under way, or a gun waiting for one with the
        // hand at its bolt, never takes from the pouch.
        bool nearBolt=NeedsRack&&Installed&&!CoverOpen&&Vector3.Distance(hand,bolt-Vector3.UnitZ*VisualRackTravel)<.14f;
        if(tHeld&&atPouch&&supplyArmed&&!Holding&&!(!shell&&(racking||nearBolt))&&(!Single||rounds<capacity)&&(!lidded||CoverOpen&&!Installed)&&(!gripTakes||WantsSupply(rounds,capacity)))
        {supplyArmed=false;racking=false;return ReloadAction.TakeSupply;}
        // 0.1.223: with the grip taking rounds, the grip also works the bolt (was the trigger).
        bool rackHeld=shell?gripHeld:gripTakes?tHeld:triggerHeld;
        // Grip may already be held as support when the shotgun fires.
        if(!racking&&nearBolt&&(shell?gripHeld:gripTakes?tDown:triggerDown))
        {racking=true;rackStart=hand+Vector3.UnitZ*(SlideLocked?FullTravel-.008f:RackTravel);maxBack=0;}
        if(racking)
        {
            var delta=hand-rackStart;
            if(!rackHeld)
            {
                racking=false;
                if(!shell&&pulled){pulled=false;NeedsRack=false;RackTravel=0;return ReloadAction.Chamber;}
                RackTravel=shell&&pulled?FullTravel:0;return ReloadAction.None;
            }
            if(delta.Length()>.35f)
            {
                racking=false;
                // 0.1.161: pulled back and let slip (the hand far away): it snaps forward, chambered.
                if(!shell&&pulled){pulled=false;NeedsRack=false;RackTravel=0;return ReloadAction.Chamber;}
                RackTravel=shell&&pulled?FullTravel:0;return ReloadAction.None;
            }
            if(shell&&!NeedsRack&&!pulled){RackTravel=0;rackStart=hand;return ReloadAction.None;}
            float full=FullTravel;
            // 0.1.174: a shorter stroke for the hand (45 % of the pump's
            // own, was 65 %: 2.7 cm back racks it, was 4.1), more room to the
            // side and up/down (15 cm, was 10), and forward it closes once the
            // hand comes 70 % of that stroke back from its furthest point (it
            // no longer has to find where the pull started).
            float physicalTravel=shell?full*.45f:full,side=shell?.15f:.10f;
            float pull=-delta.Z*(full/physicalTravel);
            RackTravel=Math.Clamp(pull,0,full);
            if(-delta.Z>maxBack)maxBack=-delta.Z;
            if(!pulled&&pull>=(SlideLocked?full:full*(shell?.8f:.85f))&&Math.Abs(delta.X)<side&&Math.Abs(delta.Y)<side)
            {pulled=true;SlideLocked=false;return ReloadAction.RackBack;}
            float closed=shell?Math.Max(.0165f,maxBack-physicalTravel*.7f):.015f;
            if(pulled&&-delta.Z<closed&&Math.Abs(delta.X)<side&&Math.Abs(delta.Y)<side)
            {racking=shell;NeedsRack=pulled=false;RackTravel=0;rackStart=hand;maxBack=0;return ReloadAction.Chamber;}
        }
        return ReloadAction.None;
    }
}
