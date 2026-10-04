using System;
using System.Numerics;
using XiiiXR;
internal static class AimInteractionTests
{
    static void Check(bool b,string name) { if(!b) throw new Exception(name); }
    static void Same(Quaternion a,Quaternion b,string name) => Check(MathF.Abs(Quaternion.Dot(a,b))>.99999f,name);
    static HandControls Hand(ulong held,bool valid=true) => new HandControls(valid,held,0,0);
    static void Main()
    {
        var raw=Quaternion.CreateFromAxisAngle(Vector3.UnitX,-MathF.PI/4);
        Same(AimMath.Apply(raw,AimMath.Pitch(45)),Quaternion.Identity,"downward correction should level upward raw pose");
        foreach(float yaw in new[]{-2.4f,0,.8f,2.8f})
        {
            raw=Quaternion.CreateFromYawPitchRoll(yaw+.3f,-.9f,.4f);
            var head=Quaternion.CreateFromYawPitchRoll(yaw,.3f,-.2f);
            var correction=AimMath.Calibrate(raw,head);
            Same(AimMath.Apply(raw,correction),Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw),"calibration must ignore head pitch/roll");
            var body=Quaternion.CreateFromAxisAngle(Vector3.UnitY,1.2f);
            Same(AimMath.Apply(body*raw,correction),body*Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw),"body turning changes local calibration");
        }
        bool invalid=false;try { AimMath.Calibrate(Quaternion.Identity,Quaternion.CreateFromAxisAngle(Vector3.UnitX,MathF.PI/2)); } catch(InvalidOperationException) { invalid=true; }
        Check(invalid,"vertical calibration should reject ambiguous yaw");
        Console.WriteLine("PASS: downward controller correction; neutral-wrist calibration; head pitch/roll excluded; turning/recentering invariance.");
        var p=new PistolSupport();var id=Quaternion.Identity;var socket=new Vector3(-.04f,-.015f,.025f);
        Same(p.Solve(Vector3.Zero,id,socket,socket,true,true,true,.18f,.01f),id,"support snapped aim on grab");
        Check(p.Held,"nearby pistol support cannot engage");
        foreach(var hand in new[]{new Vector3(.2f,.1f,0),new Vector3(0,.2f,-.2f),new Vector3(-.2f,0,.2f)})
            Same(p.Solve(Vector3.Zero,id,hand,socket,true,false,true,.18f,.01f),id,"secondary movement must not rotate pistol on any axis");
        var jitter=Quaternion.CreateFromAxisAngle(Vector3.UnitY,.03f);
        var filtered=p.Solve(Vector3.Zero,jitter,socket,socket,true,false,true,.18f,.01f);
        Check(MathF.Abs(filtered.Y)<MathF.Abs(jitter.Y)*.5f,"small primary jitter not attenuated");
        var turn=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI/2);
        for(int i=0;i<30;i++)filtered=p.Solve(Vector3.Zero,turn,socket,socket,true,false,true,.18f,.01f);
        Same(filtered,turn,"support locked primary orientation instead of stabilizing");
        Same(p.Solve(Vector3.Zero,id,socket,socket,true,false,false,.18f,.01f),id,"release doesn't return primary aim");
        Check(!p.Held,"release keeps support");
        p.Solve(Vector3.Zero,id,socket,socket,true,true,true,.18f,.01f);
        p.Solve(Vector3.Zero,id,socket,socket,false,false,true,.18f,.01f);Check(!p.Held,"tracking loss keeps support");
        p.Solve(Vector3.Zero,id,new Vector3(.7f,0,0),socket,true,true,true,.18f,.01f);Check(!p.Held,"remote hand grabs");
        // 0.1.174: the controllers touch first: 22 cm off the grip point still takes hold; a press just before arriving counts; a late one does not.
        {var q=new PistolSupport();q.Solve(Vector3.Zero,id,new Vector3(-.22f,0,0),socket,true,true,true,.18f,.01f);Check(q.Held,"support hand 22 cm off does not take hold");
         q=new PistolSupport();q.Solve(Vector3.Zero,id,new Vector3(-.4f,0,0),socket,true,true,true,.18f,.01f);Check(!q.Held,"support hand 40 cm off took hold");
         for(int i=0;i<30;i++)q.Solve(Vector3.Zero,id,new Vector3(-.4f+i*.006f,0,0),socket,true,false,true,.18f,.01f);Check(q.Held,"a grip pressed just before arriving does not take hold");
         q=new PistolSupport();q.Solve(Vector3.Zero,id,new Vector3(-.4f,0,0),socket,true,true,true,.18f,.01f);for(int i=0;i<80;i++)q.Solve(Vector3.Zero,id,new Vector3(-.4f,0,0),socket,true,false,true,.18f,.01f);
         q.Solve(Vector3.Zero,id,socket,socket,true,false,true,.18f,.01f);Check(!q.Held,"a grip held long before arriving took hold");}
        Console.WriteLine("PASS: pistol secondary hand never steers yaw/pitch/roll; primary jitter filtered; deliberate turns/release/tracking loss remain responsive.");
        var state=new InteractionState();ulong a=HandControls.A,g=HandControls.Grip;
        state.Sample(Hand(a|g),true);Check(!state.Action.Down&&!state.Action.Held,"startup held chord interacts");
        state.Sample(Hand(0),true);state.Sample(Hand(a),true);Check(!state.Action.Held,"A alone interacts");
        state.Sample(Hand(g),true);Check(!state.Action.Held,"grip alone interacts");
        state.Sample(Hand(a|g),true);Check(state.Action.Down&&state.Action.Held,"chord must interact");
        state.Sample(Hand(a|g),true);Check(!state.Action.Down&&state.Action.Held,"held chord repeats edge");
        state.Sample(Hand(g),true);Check(state.Action.Up&&!state.Action.Held,"A release must end action");
        state.Sample(Hand(a|g),true);Check(state.Action.Down,"A second press with held grip does not interact");
        state.Sample(Hand(a),true);Check(state.Action.Up&&!state.Action.Held,"grip release must end action");
        state.Sample(Hand(a|g),true);state.Sample(Hand(a|g,false),true);Check(state.Action.Up,"tracking loss must release");
        state.Sample(Hand(a|g),true);Check(!state.Action.Held,"reconnect reuses held chord");
        state.Sample(Hand(0),true);state.Sample(Hand(a|g),true);state.Sample(Hand(a|g),false);Check(state.Action.Up,"pause must release");
        state.Sample(Hand(a|g),true);Check(!state.Action.Held,"menu exit reuses held chord");
        // 0.1.82: a fresh right Grip alone picks up an item the hand points
        // at; doors (no pickup target) still need Grip+A; a held grip swept
        // onto an item does not grab it.
        var pick=new InteractionState();bool item=true;
        pick.Sample(Hand(0),true,()=>item);
        pick.Sample(new HandControls(true,g,g,0),true,()=>item);Check(pick.Action.Down&&pick.Action.Held,"grip alone does not pick up an item");
        pick.Sample(Hand(g),true,()=>item);Check(pick.Action.Held&&!pick.Action.Down,"held grip pickup repeats edge");
        pick.Sample(Hand(0),true,()=>item);Check(pick.Action.Up,"grip release does not end pickup");
        item=false;pick.Sample(new HandControls(true,g,g,0),true,()=>item);Check(!pick.Action.Held,"grip alone opens a door");
        item=true;pick.Sample(Hand(g),true,()=>item);Check(!pick.Action.Held,"held grip swept onto an item grabs it");
        pick.Sample(Hand(a|g),true,()=>item);Check(pick.Action.Down,"grip+A no longer picks up");
        Console.WriteLine("PASS: right grip+A chord, each modifier alone excluded, release either modifier, neutral rearm on startup/focus/menu; fresh grip alone picks up items, not doors.");
        // 0.1.215: at a key, card or lockpick lock a fresh stick click (R3, L3 left-handed) takes the item out; Grip + A still does.
        ulong st=HandControls.Stick;var lk=new InteractionState();bool lockAimed=true;
        lk.Sample(Hand(0),true,null,false,()=>lockAimed);
        lk.Sample(new HandControls(true,st,st,0),true,null,false,()=>lockAimed);Check(lk.Action.Down&&lk.Action.Held&&lk.LockHeld,"a stick click at a lock does not take the item out");
        lk.Sample(Hand(st),true,null,false,()=>lockAimed);Check(lk.Action.Held&&!lk.Action.Down,"the held stick click repeats its edge");
        lk.Sample(Hand(0),true,null,false,()=>lockAimed);Check(lk.Action.Up&&!lk.LockHeld,"letting go of the stick does not end it");
        lockAimed=false;lk.Sample(new HandControls(true,st,st,0),true,null,false,()=>lockAimed);Check(!lk.Action.Held&&!lk.LockHeld,"a stick click away from a lock interacts (it is the secondary fire there)");
        lk.Sample(Hand(0),true,null,false,()=>lockAimed);lk.Sample(Hand(st),true,null,false,()=>lockAimed);lockAimed=true;lk.Sample(Hand(st),true,null,false,()=>lockAimed);Check(!lk.Action.Held,"a stick held from before takes the item out when aimed at a lock");
        lk.Sample(Hand(0),true,null,false,()=>lockAimed);lk.Sample(Hand(a|g),true,null,false,()=>lockAimed);Check(lk.Action.Down,"Grip + A no longer takes the item out at a lock");
        var off=new InteractionState();off.Sample(Hand(0),true,null,true,()=>true);off.Sample(new HandControls(true,st,st,0),true,null,true,()=>true);Check(!off.Action.Held,"the other hand's stick takes the item out");
        Console.WriteLine("PASS: 0.1.215 a fresh stick click at a lock takes the key, card or lockpick out (not one held from before, not away from a lock, not the other hand's); Grip + A still does.");
    }
}
