using System;
namespace XiiiXR;
// 0.1.194: the double-barrelled (hunting) shotgun reloaded by hand
// like a real break-action gun. B (Y for the gun in the left hand) opens it:
// the barrels drop on their hinge and only the fired cases fly out, an unfired
// shell stays in its chamber. A shell taken from the belt is pushed into an
// open empty chamber. A small flick of the gun up and down swings the barrels
// shut by their own weight (B again closes it too). It does not fire open.
internal enum BreakChamber{Empty,Live,Spent}
internal sealed class BreakActionState
{
    // 0 = right barrel (fired first), 1 = left barrel.
    internal readonly BreakChamber[] Chambers={BreakChamber.Live,BreakChamber.Live};
    internal bool Open{get;private set;}
    internal bool Holding{get;private set;}
    // How far open the barrels are drawn (0 shut, 1 fully open).
    internal float Swing{get;private set;}
    internal const float OpenSeconds=.14f,CloseSeconds=.10f;
    internal bool BlocksFire=>Open||Swing>.02f;
    internal int LiveRounds{get{int n=0;foreach(var c in Chambers)if(c==BreakChamber.Live)n++;return n;}}
    internal bool AnyEmpty=>Array.IndexOf(Chambers,BreakChamber.Empty)>=0;
    internal bool AnySpent=>Array.IndexOf(Chambers,BreakChamber.Spent)>=0;
    // A new gun: its chambers from the game's loaded rounds (right barrel first).
    internal void Load(int rounds)
    {
        Open=false;Holding=false;Swing=0;
        for(int c=0;c<2;c++)Chambers[c]=c<Math.Clamp(rounds,0,2)?BreakChamber.Live:BreakChamber.Empty;
    }
    // Opens the gun; the chambers whose cases fly out (their fired cases only).
    internal int[] OpenGun()
    {
        if(Open)return Array.Empty<int>();
        Open=true;
        int n=0;foreach(var c in Chambers)if(c==BreakChamber.Spent)n++;
        var ejected=new int[n];int k=0;
        for(int c=0;c<2;c++)if(Chambers[c]==BreakChamber.Spent){Chambers[c]=BreakChamber.Empty;ejected[k++]=c;}
        return ejected;
    }
    internal bool CloseGun(){if(!Open)return false;Open=false;flickUp=-10;return true;}
    internal bool Take(){if(Holding)return false;Holding=true;return true;}
    // The shell held goes back (let go of): the rounds to give back.
    internal int LetGo(){if(!Holding)return 0;Holding=false;return 1;}
    // The held shell pushed into chamber c (open and empty only).
    internal bool Insert(int c)
    {
        if(!Open||!Holding||c<0||c>1||Chambers[c]!=BreakChamber.Empty)return false;
        Chambers[c]=BreakChamber.Live;Holding=false;return true;
    }
    // Follows the game's loaded rounds: a shot fired a live chamber (the right
    // one first); rounds the game put in fill the empty chambers.
    internal int Observe(int rounds)
    {
        rounds=Math.Clamp(rounds,0,2);int fired=0;
        while(LiveRounds>rounds)
        {
            int c=Chambers[0]==BreakChamber.Live?0:1;Chambers[c]=BreakChamber.Spent;fired++;
        }
        while(LiveRounds<rounds)
        {
            int c=Chambers[0]!=BreakChamber.Live?0:1;Chambers[c]=BreakChamber.Live;
        }
        return fired;
    }
    // Swings the barrels towards open or shut.
    internal void Tick(float dt)
    {
        if(!float.IsFinite(dt)||dt<=0)return;
        Swing=Open?Math.Min(1,Swing+dt/OpenSeconds):Math.Max(0,Swing-dt/CloseSeconds);
    }
    // The flick that shuts it: the gun moved up (or its muzzle raised) fast,
    // then stopped or went down within a moment. up: the gun hand's speed
    // upwards (m/s); raise: how fast its muzzle rises (degrees a second).
    internal const float FlickUp=.8f,FlickRaise=160f,FlickStop=.6f,FlickWindow=.35f;
    private float flickUp=-10,peakUp,peakRaise;
    internal bool Flick(float now,float up,float raise)
    {
        // Only once fully open (not the motion that opened it).
        if(!Open||Swing<1||!float.IsFinite(up)||!float.IsFinite(raise)||!float.IsFinite(now))return false;
        if(up>=FlickUp||raise>=FlickRaise)
        {
            if(now-flickUp>FlickWindow){peakUp=up;peakRaise=raise;}
            else{peakUp=Math.Max(peakUp,up);peakRaise=Math.Max(peakRaise,raise);}
            flickUp=now;return false;
        }
        if(now-flickUp>FlickWindow)return false;
        // Stopped: the speed fell well below its peak (or reversed).
        bool stopped=peakUp>=FlickUp&&up<=peakUp-FlickStop||peakRaise>=FlickRaise&&raise<=peakRaise*.35f;
        if(!stopped)return false;
        flickUp=-10;return CloseGun();
    }
}
// The game's hunting shotgun rig (log of 0.1.192): wpn_shotgun_hunting_SH_BND_JNT
// (root), _lock_ (top lever), _tilt_ (the barrels' hinge), _shell_right_ /
// _shell_left_ (the chambers' shells, each under its _zero_ joint) and
// _shell_middle_ (the shell the game's reload puts in the hand).
internal static class BreakRig
{
    internal readonly record struct Found(int Root,int Tilt,int Lock,int Right,int Left,int Middle);
    internal static bool Is(System.Collections.Generic.IReadOnlyList<string?> names)=>Find(names).Tilt>=0;
    internal static Found Find(System.Collections.Generic.IReadOnlyList<string?> names)
    {
        int tilt=-1,lk=-1,right=-1,left=-1,middle=-1;
        for(int i=0;i<names.Count;i++)
        {
            var raw=names[i];if(string.IsNullOrEmpty(raw))continue;string n=raw.ToLowerInvariant();
            if(n.Contains("zero",StringComparison.Ordinal))continue;
            if(n.Contains("_tilt_",StringComparison.Ordinal))tilt=i;
            else if(n.Contains("_lock_",StringComparison.Ordinal))lk=i;
            else if(n.Contains("shell_right",StringComparison.Ordinal))right=i;
            else if(n.Contains("shell_left",StringComparison.Ordinal))left=i;
            else if(n.Contains("shell_middle",StringComparison.Ordinal))middle=i;
        }
        return new Found(tilt>=0?ReloadBones.CommonRoot(names):-1,tilt,lk,right,left,middle);
    }
    // Chamber c's shell: its bone or its zero joint (0 = right, 1 = left).
    internal static bool Chamber(string? name,int c)=>name!=null&&name.ToLowerInvariant().Contains(c==0?"shell_right":"shell_left",StringComparison.Ordinal);
    internal static bool Shell(string? name)=>name!=null&&name.ToLowerInvariant().Contains("shell",StringComparison.Ordinal);
    // The two chambers (0 right, 1 left) at the barrels' rear end: slice is
    // the barrels' rearmost points (barrel frame), side by side at their top.
    internal static System.Numerics.Vector3[] Chambers(System.Numerics.Vector3[] slice,float rear)
    {
        if(slice.Length==0)return new[]{new System.Numerics.Vector3(0,0,rear),new System.Numerics.Vector3(0,0,rear)};
        float xlo=float.PositiveInfinity,xhi=float.NegativeInfinity,yhi=float.NegativeInfinity;
        foreach(var p in slice){xlo=Math.Min(xlo,p.X);xhi=Math.Max(xhi,p.X);yhi=Math.Max(yhi,p.Y);}
        float w=xhi-xlo,mid=(xlo+xhi)*.5f,y=yhi-w*.25f;
        return new[]{new System.Numerics.Vector3(mid+w*.25f,y,rear),new System.Numerics.Vector3(mid-w*.25f,y,rear)};
    }
    // A rig's shell (its middle) lies in a chamber: beside its axis and just
    // ahead of the barrels' rear end (length: the barrels' length).
    internal static bool InChamber(System.Numerics.Vector3 shell,System.Numerics.Vector3 chamber,float length)
    {
        if(!(length>0)||!float.IsFinite(shell.X+shell.Y+shell.Z))return false;
        float across=MathF.Sqrt((shell.X-chamber.X)*(shell.X-chamber.X)+(shell.Y-chamber.Y)*(shell.Y-chamber.Y));
        return across<length*.04f&&shell.Z>chamber.Z-length*.02f&&shell.Z<chamber.Z+length*.25f;
    }
    // The long axis of a set of points (the barrels): the largest principal
    // direction, turned to point along +z.
    internal static System.Numerics.Vector3 Axis(System.Numerics.Vector3[] points)
    {
        if(points.Length<3)return System.Numerics.Vector3.UnitZ;
        var mean=System.Numerics.Vector3.Zero;foreach(var p in points)mean+=p;mean/=points.Length;
        float xx=0,xy=0,xz=0,yy=0,yz=0,zz=0;
        foreach(var p in points){var d=p-mean;xx+=d.X*d.X;xy+=d.X*d.Y;xz+=d.X*d.Z;yy+=d.Y*d.Y;yz+=d.Y*d.Z;zz+=d.Z*d.Z;}
        var v=System.Numerics.Vector3.UnitZ;
        for(int k=0;k<64;k++)
        {
            var n=new System.Numerics.Vector3(xx*v.X+xy*v.Y+xz*v.Z,xy*v.X+yy*v.Y+yz*v.Z,xz*v.X+yz*v.Y+zz*v.Z);
            float len=n.Length();if(!(len>1e-12f))return System.Numerics.Vector3.UnitZ;v=n/len;
        }
        return v.Z<0?-v:v;
    }
}
