using System;
using System.Collections.Generic;
namespace XiiiXR;
// Bounded ownership of expensive render-only assets. Native gameplay objects
// are never cached here. A chapter/player rebind disposes the entire cache.
internal sealed class VisualCache<T> : IDisposable where T:class,IDisposable
{
    private readonly int capacity;
    private readonly Dictionary<int,T> values=new();
    private readonly LinkedList<int> order=new();
    internal VisualCache(int limit){capacity=limit;}
    internal T Get(int key,Func<T,bool> valid,Func<T> build,out bool reused)
    {
        if(values.TryGetValue(key,out var value))
        {
            order.Remove(key);
            if(valid(value)){order.AddLast(key);reused=true;return value;}
            values.Remove(key);value.Dispose();
        }
        reused=false;value=build();
        while(values.Count>=capacity){int old=order.First!.Value;order.RemoveFirst();values[old].Dispose();values.Remove(old);}
        values.Add(key,value);order.AddLast(key);return value;
    }
    public void Dispose()
    {
        Exception? first=null;
        try{foreach(var value in values.Values){try{value.Dispose();}catch(Exception ex){first??=ex;}}}
        finally{values.Clear();order.Clear();}
        if(first!=null)throw new InvalidOperationException("A visual failed to restore during scene cleanup; all cache entries were released.",first);
    }
}
