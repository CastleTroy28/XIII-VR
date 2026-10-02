using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
namespace XiiiXR;
// 0.1.152:
// the hand's hold of each weapon, measured from the game's own right hand when
// that weapon is the game's, is what a hand holding a still copy (the left
// hand's knife) is posed with, mirrored for the left hand. It is kept between
// games in a small file, so a copy is held right even before the right hand
// has held that weapon in this game.
internal static class HandGripMemory
{
    internal const float Moved=.004f;
    internal static string Format(string profile,Vector3 point,Quaternion rotation,float size)=>string.Join("|",profile,F(point.X),F(point.Y),F(point.Z),F(rotation.X),F(rotation.Y),F(rotation.Z),F(rotation.W),F(size));
    private static string F(float v)=>v.ToString("R",CultureInfo.InvariantCulture);
    internal static bool Parse(string line,out string profile,out Vector3 point,out Quaternion rotation,out float size)
    {
        profile="";point=default;rotation=Quaternion.Identity;size=1;
        var p=line.Split('|');if(p.Length!=9||p[0].Length==0||p[0].Length>40)return false;
        var v=new float[8];
        for(int i=0;i<8;i++)if(!float.TryParse(p[i+1],NumberStyles.Float,CultureInfo.InvariantCulture,out v[i])||!float.IsFinite(v[i]))return false;
        point=new Vector3(v[0],v[1],v[2]);rotation=new Quaternion(v[3],v[4],v[5],v[6]);size=v[7];
        if(point.Length()>2||!(size>.2f&&size<5)||Math.Abs(rotation.Length()-1)>.02f)return false;
        rotation=Quaternion.Normalize(rotation);profile=p[0];return true;
    }
    // Worth writing again: a new weapon or a hold that moved.
    internal static bool Changed(bool known,Vector3 oldPoint,Vector3 newPoint)=>!known||Vector3.Distance(oldPoint,newPoint)>Moved;
    internal static Dictionary<string,(Vector3 point,Quaternion rotation,float size)> Read(IEnumerable<string> lines)
    {
        var result=new Dictionary<string,(Vector3,Quaternion,float)>();
        foreach(var line in lines)if(Parse(line.Trim(),out var profile,out var point,out var rotation,out var size))result[profile]=(point,rotation,size);
        return result;
    }
}
