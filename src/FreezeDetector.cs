using System;
using System.Collections.Generic;
using System.Numerics;
namespace XiiiXR;
// 0.1.149: what stopped,
// told from one sample a frame (FreezeWatch gathers them in the game).
//  - FREEZE: a frame that took long; with what else happened in it (the
//    game's memory clean-up, the mod's own code, a part of the level loaded,
//    the frames after it);
//  - TRACKING FROZE / LOST: the headset's runtime gave the very same head
//    pose (or none) for a while - the picture stands still with the head;
//  - MOVE STALL: the stick is pushed, moving is allowed, yet the character
//    stands (against a wall is told apart);
//  - STUTTER: many slow frames close together (none long by itself);
//  - TIME STOPPED: the game's clock stopped outside the menus;
//  - FOCUS LOST: the game's window lost Windows focus (another program took
//    it) - a game that stops in the background stops then, and before
//    0.1.149 the mod's hands, weapons and walking stopped with it.
// A long frame is measured in real time: while the window has no focus the
// game's loop may stand still without counting it in its own frame time.
internal readonly record struct FrameSample(float Now,float Dt,Vector3 Head,Quaternion HeadRotation,bool HeadValid,float Stick,bool MoveAllowed,Vector2 Root,bool Blocked,
    float TimeScale,bool Menu,int GameGc,int ModGc,float ModMs,int Scenes,bool Focused=true,string? Foreground=null,int Frame=-1,bool Gameplay=true,string ModParts="");
internal readonly record struct FreezeEvent(string Kind,float Seconds,string Detail);
internal sealed class FreezeDetector
{
    internal const float LongFrame=.25f,FrozenPose=.25f,Resting=8f,LostPose=.15f,StallStick=.5f,StallSpeed=.08f,StallReport=.6f,TimeStopReport=.3f;
    internal const int AfterFrames=8;
    private FrameSample last;private bool has;
    private float poseFrozen,poseLost,stall,stallMaxDt,stopped,unfocused;private bool stallBlocked;private string? taker;
    // A long frame's report waits for the frames after it (a cascade shows there).
    private FreezeEvent pending;private int pendingLeft;private readonly List<float> after=new();
    private readonly float[] recent=new float[128];private int recentHead,recentCount;
    internal const float StutterWindow=1.5f,SlowFrame=.03f,StutterExcess=.5f;
    private readonly Queue<(float at,float gap)> window=new();private float stutterQuietUntil;
    internal List<FreezeEvent> Step(FrameSample s)
    {
        var events=new List<FreezeEvent>();
        if(!float.IsFinite(s.Dt)||s.Dt<0||!float.IsFinite(s.Now)){has=false;return events;}
        // Frames the watch did not see (its caller skipped them): no judgement across them.
        if(has&&s.Frame>=0&&last.Frame>=0&&s.Frame-last.Frame!=1){last=s;pendingLeft=0;return events;}
        float gap=has?Math.Max(0,s.Now-last.Now):s.Dt;
        if(pendingLeft>0)
        {
            after.Add(gap);
            if(--pendingLeft==0)events.Add(Finish());
        }
        if(has)
        {
            // A long frame (real time: a loop standing still is not counted by the game).
            // (Not in loading screens, films and cutscenes: long frames are normal there.)
            if(gap>=LongFrame&&(s.Gameplay||last.Gameplay))
            {
                if(pendingLeft>0)events.Add(Finish());
                var why=new List<string>();
                if(s.GameGc>last.GameGc&&last.GameGc>=0)why.Add("the game's memory clean-up (garbage collection) ran in it");
                if(s.ModGc>last.ModGc&&last.ModGc>=0)why.Add("the mod's memory clean-up ran in it");
                float mod=s.ModMs;
                if(mod>=gap*1000*.4f)why.Add("the mod's own code took "+mod.ToString("F0")+" ms of it"+(s.ModParts.Length>0?" ("+s.ModParts+")":""));
                if(s.Scenes!=last.Scenes)why.Add("a part of the level loaded or unloaded ("+last.Scenes+" -> "+s.Scenes+" scenes)");
                if(s.Dt<gap*.6f)why.Add("the game's loop stood still and did not count it (it counted "+(s.Dt*1000).ToString("F0")+" ms): Windows paused it"+(!s.Focused||!last.Focused||unfocused>0?" - its window had lost focus":" (a game window without focus is paused)"));
                if(why.Count==0)why.Add("not the mod's code ("+mod.ToString("F1")+" ms) and no memory clean-up: the game itself (loading, shaders, physics) or the headset's driver / Windows");
                pending=new FreezeEvent("FREEZE",gap,string.Join("; ",why)+"; before it the slowest of the last frames "+RecentMax().ToString("F0")+" ms"+(s.Menu?"; in a menu":"")+(last.Stick>=StallStick?"; walking":""));
                pendingLeft=AfterFrames;after.Clear();
            }
            // The head pose: the very same (a real headset always trembles a little), or none.
            if(s.HeadValid&&last.HeadValid&&s.Head==last.Head&&s.HeadRotation==last.HeadRotation)poseFrozen+=gap;
            else
            {
                if(poseFrozen>=FrozenPose)
                    events.Add(poseFrozen<Resting?new FreezeEvent("TRACKING FROZE",poseFrozen,"the headset's runtime gave the very same head pose all that time (the picture stood still with the head); frames kept coming"+(s.Menu?"; in a menu":""))
                        :new FreezeEvent("HEADSET STILL",poseFrozen,"the same head pose for a long time (the headset put down?)"));
                poseFrozen=0;
            }
            if(!s.HeadValid)poseLost+=gap;
            else{if(poseLost>=LostPose)events.Add(new FreezeEvent("TRACKING LOST",poseLost,"the headset's runtime gave no valid head pose"));poseLost=0;}
            // Pushing the stick, yet standing.
            bool pushing=s.Stick>=StallStick&&s.MoveAllowed&&!s.Menu&&s.TimeScale>.5f;
            float speed=gap>1e-4f?Vector2.Distance(s.Root,last.Root)/gap:0;
            if(pushing&&speed<StallSpeed){stall+=gap;stallMaxDt=Math.Max(stallMaxDt,gap);stallBlocked|=s.Blocked;}
            else
            {
                if(stall>=StallReport)events.Add(new FreezeEvent("MOVE STALL",stall,"the stick pushed and moving allowed, the character did not move"+(stallBlocked?" (it was against something: a wall or an object)":" (nothing in its way that the game reported)")+"; the slowest frame in it "+(stallMaxDt*1000).ToString("F0")+" ms"));
                stall=0;stallMaxDt=0;stallBlocked=false;
            }
            // The game's window without Windows focus.
            if(!s.Focused){unfocused+=gap;taker??=s.Foreground;}
            else if(!last.Focused)
            {
                events.Add(new FreezeEvent("FOCUS LOST",Math.Max(unfocused,gap),"the game's window lost Windows focus"+(taker!=null?" to "+taker:"")+" and got it back"));
                unfocused=0;taker=null;
            }
            // The game's clock stopped outside the menus.
            if(s.TimeScale<.05f&&!s.Menu)stopped+=gap;
            else{if(stopped>=TimeStopReport)events.Add(new FreezeEvent("TIME STOPPED",stopped,"the game's time stood still outside the menus (the game paused itself)"));stopped=0;}
        }
        // Many slow frames together (none long by itself): a stutter felt as a stop.
        if(has&&(s.Gameplay||last.Gameplay))
        {
            window.Enqueue((s.Now,gap));
            while(window.Count>0&&s.Now-window.Peek().at>StutterWindow)window.Dequeue();
            float excess=0,worst=0;int slow=0;
            foreach(var (_,g) in window){if(g>SlowFrame){excess+=g-SlowFrame;slow++;}worst=Math.Max(worst,g);}
            if(excess>=StutterExcess&&worst<LongFrame&&s.Now>=stutterQuietUntil)
            {
                stutterQuietUntil=s.Now+3;
                events.Add(new FreezeEvent("STUTTER",excess,slow+" slow frames within "+StutterWindow.ToString("F1")+" s, the slowest "+(worst*1000).ToString("F0")+" ms"+(s.GameGc>last.GameGc&&last.GameGc>=0?"; the game's memory clean-up ran":"")+(s.Menu?"; in a menu":"")));
            }
        }
        recent[recentHead]=gap*1000;recentHead=(recentHead+1)%recent.Length;recentCount=Math.Min(recentCount+1,recent.Length);
        last=s;has=true;
        return events;
    }
    private float RecentMax(){float m=0;for(int i=0;i<recentCount;i++)m=Math.Max(m,recent[i]);return m;}
    private FreezeEvent Finish()
    {
        pendingLeft=0;
        var text=new System.Text.StringBuilder();foreach(var d in after){if(text.Length>0)text.Append(',');text.Append((d*1000).ToString("F0"));}
        int slow=0;foreach(var d in after)if(d>=.05f)slow++;
        return new FreezeEvent(pending.Kind,pending.Seconds,pending.Detail+"; the frames after it (ms): "+text+(slow>=2?" (more slow frames followed: a cascade)":""));
    }
}
