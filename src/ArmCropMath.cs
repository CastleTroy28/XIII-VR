using System;
using System.Collections.Generic;
namespace XiiiXR;
// 0.1.120: the game's own arms on a stationary gun are cut at the elbow: only
// the forearms and the hands (the elbow joint and every bone under it) stay.
internal static class ArmCropMath
{
    internal static bool Elbow(string name)
    {
        var n=name.ToLowerInvariant();
        return n.Contains("elbow")||n.Contains("forearm")||n.Contains("fore_arm")||n.Contains("lowerarm")||n.Contains("lower_arm")||n.Contains("lowarm");
    }
    private static bool Wrist(string name)
    {
        var n=name.ToLowerInvariant();
        return (n.Contains("wrist")||n.Contains("hand"))&&!n.Contains("handle")&&!n.Contains("finger")&&!n.Contains("thumb");
    }
    // parents: each bone's nearest ancestor among the bones (-1: none).
    // Null when no elbow can be told (the arms are then left whole).
    internal static bool[]? KeepBones(string[] names,int[] parents,out string how)
    {
        int n=names.Length;var elbow=new bool[n];int found=0;how="elbow names";
        for(int i=0;i<n;i++)if(Elbow(names[i])){elbow[i]=true;found++;}
        if(found==0)
        {
            how="wrist parents";
            for(int i=0;i<n;i++){int p=i<parents.Length?parents[i]:-1;if(Wrist(names[i])&&p>=0&&p<n&&!elbow[p]){elbow[p]=true;found++;}}
        }
        if(found==0){how="none";return null;}
        var keep=new bool[n];
        for(int i=0;i<n;i++)
            for(int b=i,guard=0;b>=0&&b<n&&guard<=n;b=b<parents.Length?parents[b]:-1,guard++)
                if(elbow[b]){keep[i]=true;break;}
        return keep;
    }
    // A triangle stays when all three corners follow the kept bones at least
    // by the threshold (the cut runs round the elbow).
    internal static int[] Triangles(int[] triangles,float[] keep,float threshold=.5f)
    {
        var kept=new List<int>(triangles.Length);
        for(int i=0;i+2<triangles.Length;i+=3)
        {
            int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
            if(a<0||b<0||c<0||a>=keep.Length||b>=keep.Length||c>=keep.Length)continue;
            if(keep[a]>=threshold&&keep[b]>=threshold&&keep[c]>=threshold){kept.Add(a);kept.Add(b);kept.Add(c);}
        }
        return kept.ToArray();
    }
}
