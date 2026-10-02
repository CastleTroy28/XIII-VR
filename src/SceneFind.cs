using System;
using System.Collections.Generic;
namespace XiiiXR;
// 0.1.162: a scene search costs about 4 ms
// (it walks every object of the game), and about six ran each second of
// play - each one made its frame late for the headset, and the headset
// software doubled the picture to cover it. What a search found is now kept:
// the search runs again after a scene load or unload, when asked (Soon), or
// rarely (every `every` seconds). In between, the users check the kept
// objects, which costs next to nothing. The game's own hooks (SceneHooks)
// add a new object the moment it appears.
internal sealed class SceneFind<T> where T:UnityEngine.Object
{
    private readonly List<T> items=new();
    private readonly string name;private readonly float every;private readonly Action<List<T>> search;
    private float next,last=float.NegativeInfinity;private int epoch=int.MinValue;
    internal SceneFind(string name,float every,Action<List<T>> search){this.name=name;this.every=Math.Max(.1f,every);this.search=search;}
    internal List<T> Items=>items;
    internal int Searches{get;private set;}
    internal float Every=>every;
    // Runs the search when it is due; true when it ran.
    internal bool Refresh()=>Refresh(UnityEngine.Time.realtimeSinceStartup,UnityEngine.Time.frameCount);
    internal bool Refresh(float now,int frame)
    {
        if(!SceneScan.Due(ref next,every,ref epoch,now,frame))return false;
        Run(now);return true;
    }
    // At once, even with another search in this frame (a player's own action, rare).
    internal void RefreshNow(){float now=UnityEngine.Time.realtimeSinceStartup;next=now+every;epoch=SceneScan.Epoch;Run(now);}
    private void Run(float now)
    {
        long start=SceneScan.Begin();
        try{items.Clear();search(items);}
        catch(Exception){items.Clear();throw;}
        finally{Searches++;last=now;SceneScan.End(name,start);}
    }
    // The next search as soon as a frame is free of others, but not sooner
    // than `gap` seconds after the last one.
    internal void Soon(float gap)
    {
        float at=last+Math.Max(0,gap);
        if(at<next)next=at;
    }
    // The next search at once (a new player, a new level).
    internal void Reset(){next=0;}
    // An object the game just made (a hook); the next search keeps it anyway.
    internal void Add(T item){if(item!=null)items.Add(item);}
    // Drops the kept objects the game has destroyed.
    internal void Prune()
    {
        for(int i=items.Count-1;i>=0;i--)
        {
            bool gone;try{gone=items[i]==null;}catch(Exception){gone=true;}
            if(gone)items.RemoveAt(i);
        }
    }
}
