using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
namespace XiiiXR;
// 0.1.122: where the crossbow bolt sits on its rail, relative to the grip
// bone, as seen once on a loaded crossbow; kept on disk so an empty crossbow
// taken later still shows (and hints) the bolt on the rail.
internal static class BoltRelationText
{
    internal static string Format(IReadOnlyList<string> bones,IReadOnlyList<float[]> matrices)
    {
        var text=new StringBuilder();
        for(int k=0;k<bones.Count&&k<matrices.Count;k++)
        {
            text.Append(bones[k]);
            foreach(float v in matrices[k])text.Append(' ').Append(v.ToString("R",CultureInfo.InvariantCulture));
            text.Append('\n');
        }
        return text.ToString();
    }
    internal static float[][]? Parse(string text,IReadOnlyList<string> bones)
    {
        if(string.IsNullOrEmpty(text))return null;
        var found=new Dictionary<string,float[]>(StringComparer.Ordinal);
        foreach(var line in text.Split('\n'))
        {
            var parts=line.Trim().Split(' ',StringSplitOptions.RemoveEmptyEntries);
            if(parts.Length!=17)continue;
            var m=new float[16];bool ok=true;
            for(int i=0;i<16&&ok;i++)ok=float.TryParse(parts[i+1],NumberStyles.Float,CultureInfo.InvariantCulture,out m[i])&&float.IsFinite(m[i]);
            if(ok)found[parts[0]]=m;
        }
        var result=new float[bones.Count][];
        for(int k=0;k<bones.Count;k++)if(!found.TryGetValue(bones[k],out result[k]!))return null;
        return result;
    }
}
