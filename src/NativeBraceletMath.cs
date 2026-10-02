using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace XiiiXR;
internal static class NativeBraceletMath
{
    // Keep whole disconnected accessories, never slice skin into a fake band.
    internal static bool[] Select(Vector3[] points,int[][] triangles,bool[] left,bool wholeBand=false)
    {
        int[] parent=Enumerable.Range(0,points.Length).ToArray();
        int Find(int x){while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x;}
        var weld=new Dictionary<(int,int,int),int>();
        for(int i=0;i<points.Length;i++)if(left[i])
        {
            var p=points[i];var key=((int)MathF.Round(p.X*100000),(int)MathF.Round(p.Y*100000),(int)MathF.Round(p.Z*100000));
            if(weld.TryGetValue(key,out int k))parent[Find(i)]=Find(k);else weld[key]=i;
        }
        foreach(var sub in triangles)for(int i=0;i+2<sub.Length;i+=3)
        {int a=sub[i],b=sub[i+1],c=sub[i+2];if(left[a]&&left[b]&&left[c]){parent[Find(b)]=Find(a);parent[Find(c)]=Find(a);}}
        var islands=Enumerable.Range(0,points.Length).Where(i=>left[i]).GroupBy(Find).Select(g=>g.ToArray()).ToArray();
        var chosen=new HashSet<int>();
        foreach(var island in islands)
        {
            if(island.Length<12)continue;
            var min=island.Select(i=>points[i]).Aggregate(Vector3.Min);var max=island.Select(i=>points[i]).Aggregate(Vector3.Max);var size=max-min;
            // Wrist band/case must straddle the hand centre, be short along the
            // forearm, and be separate from the long skin/sleeve island.
            if(min.Z<-.15f||max.Z>.025f||size.Z>.095f||size.X<.025f||size.X>.15f||size.Y>.13f
                ||min.X>-.008f||max.X<.008f||max.Z<-.10f)continue;
            foreach(int i in island)chosen.Add(i);
        }
        // Crowns/buttons are separate tiny islands, often <12 vertices and
        // entirely on one side of the wrist. Remove only those adjacent to a
        // positively identified case; never slice a connected skin/sleeve mesh.
        if(chosen.Count>0)
        {
            var lo=chosen.Select(i=>points[i]).Aggregate(Vector3.Min)-new Vector3(.012f);
            var hi=chosen.Select(i=>points[i]).Aggregate(Vector3.Max)+new Vector3(.012f);
            foreach(var island in islands)
            {
                if(island.Length<3||island.Any(chosen.Contains))continue;
                var min=island.Select(i=>points[i]).Aggregate(Vector3.Min);var max=island.Select(i=>points[i]).Aggregate(Vector3.Max);var size=max-min;
                if(size.X>.023f||size.Y>.023f||size.Z>.023f||min.X<lo.X||min.Y<lo.Y||min.Z<lo.Z||max.X>hi.X||max.Y>hi.Y||max.Z>hi.Z)continue;
                foreach(int i in island)chosen.Add(i);
            }
        }
        if(wholeBand&&chosen.Count>0)
        {
            // Replace disconnected wrist accessories only. The skin/sleeve
            // island extends well beyond this short wrist interval.
            var lo=chosen.Select(i=>points[i]).Aggregate(Vector3.Min);
            var hi=chosen.Select(i=>points[i]).Aggregate(Vector3.Max);
            foreach(var island in islands)
            {
                if(island.Length<3)continue;
                var min=island.Select(i=>points[i]).Aggregate(Vector3.Min);var max=island.Select(i=>points[i]).Aggregate(Vector3.Max);
                if(min.Z>=lo.Z-.018f&&max.Z<=hi.Z+.018f&&max.Z-min.Z<.10f&&min.X>-.10f&&max.X<.10f&&min.Y>-.10f&&max.Y<.10f)
                    foreach(int i in island)chosen.Add(i);
            }
        }
        return Enumerable.Range(0,points.Length).Select(chosen.Contains).ToArray();
    }
}
