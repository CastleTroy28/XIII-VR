using System;
using System.Collections.Generic;
using XiiiXR;
class VisualCacheTests
{
    sealed class Asset:IDisposable
    {internal bool Valid=true,Fail;internal int Disposed;public void Dispose(){if(++Disposed!=1)throw new Exception("double disposal");if(Fail)throw new InvalidOperationException("destroyed native source");}}
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Main()
    {
        var made=new List<Asset>();var cache=new VisualCache<Asset>(3);
        Asset Build(){var a=new Asset();made.Add(a);return a;}
        for(int pass=0;pass<100;pass++)for(int slot=0;slot<3;slot++)
        {cache.Get(slot,a=>a.Valid,Build,out bool reused);Check(reused==(pass>0),"repeated weapon selection rebuilt its assets");}
        Check(made.Count==3,"unbounded allocation across pistol/shotgun/AK cycles");
        made[0].Valid=false;cache.Get(0,a=>a.Valid,Build,out bool same);Check(!same&&made[0].Disposed==1,"destroyed source reused");
        cache.Get(4,a=>a.Valid,Build,out _);Check(made[1].Disposed==1&&made[2].Disposed==0,"LRU evicts recently active asset");
        int count=made.Count;try{cache.Get(8,a=>true,()=>throw new InvalidOperationException(),out _);}catch(InvalidOperationException){}
        Check(count==made.Count&&made[2].Disposed==0,"failed build evicts working assets");
        cache.Dispose();cache.Dispose();Check(made.TrueForAll(a=>a.Disposed==1),"chapter/reload leaks owned meshes");
        cache.Get(0,a=>a.Valid,Build,out same);Check(!same,"new chapter uses old source");cache.Dispose();
        var broken=cache.Get(10,a=>true,Build,out _);broken.Fail=true;
        var sibling=cache.Get(11,a=>true,Build,out _);
        try{cache.Dispose();throw new Exception("cleanup failure swallowed");}catch(InvalidOperationException){}
        Check(broken.Disposed==1&&sibling.Disposed==1,"one destroyed source leaked sibling visuals");
        cache.Dispose();cache.Get(10,a=>true,Build,out same);Check(!same,"failed scene cleanup retained stale visual");cache.Dispose();
        Console.WriteLine("PASS: 300 selections build three owned assets, invalid sources/evictions dispose once, failed build preserves working entries, chapter disposal and reuse are safe.");
    }
}
