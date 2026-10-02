using System;
using System.Numerics;
namespace XiiiXR;
internal static class PhysicalHandsMath
{
    internal static Vector3 Follow(Vector3 error,Vector3 handVelocity)
    {var v=error*18+handVelocity;return Limit(v,6);}
    internal static Vector3 Limit(Vector3 v,float max)
    {float n=v.Length();return !float.IsFinite(n)?Vector3.Zero:n>max?v*(max/n):v;}
    internal static float HingeDelta(Vector3 previous,Vector3 current,Vector3 pivot,Vector3 axis)
    {
        if(axis.LengthSquared()<.001f)return 0;axis=Vector3.Normalize(axis);
        var a=previous-pivot;var b=current-pivot;a-=axis*Vector3.Dot(a,axis);b-=axis*Vector3.Dot(b,axis);
        if(a.LengthSquared()<.0064f||b.LengthSquared()<.0064f)return 0;
        return Math.Clamp(MathF.Atan2(Vector3.Dot(axis,Vector3.Cross(a,b)),Vector3.Dot(a,b))*180/MathF.PI,-12,12);
    }
    internal static bool SmallProp(string name)
    {
        name=name.ToLowerInvariant();
        if(name.Contains("window")||name.Contains("wall")||name.Contains("door")||name.Contains("cabinet")||name.Contains("shelf")||name.Contains("shelves"))return false;
        foreach(string token in new[]{"broom","mop","shovel","kettle","basket","garbage","gas_tank","buoy","lifesaver","life_saver","gramaphone","record_player","rescue_can","rescue_float","canister","cup_","mug_","bottle","jar_","book","gramophone","phonograph","trash","wastebasket","bin_","extinguisher","chair","ashtray","surfboard","surf_board","lifebuoy","life_buoy","life_ring","gas_cylinder","gas_bottle","propane","cookie","biscuit","box_"})if(name.Contains(token))return true;
        // 0.1.149: food, drink cans, paper and packets.
        foreach(string token in new[]{"burger","sandwich","hotdog","hot_dog","pizza","donut","doughnut","bread","fruit","apple","banana","soda","cola","beer","tin_","paper","newspaper","napkin","plate_","glass_","carton","milk","cereal","wrapper","crumpled","cigarette","packet","snack","chips","food"})if(name.Contains(token))return true;
        if(System.Text.RegularExpressions.Regex.IsMatch(name,@"(^|_)cans?(\d|_|$)"))return true;
        return name.StartsWith("pot_")||name.Contains("_pot_")||name.StartsWith("pan_")||name.Contains("_pan_")||name.StartsWith("can_")||name.Contains("_can_")||name.Contains("bottle")||name.Contains("saucepan")||name.Contains("cooking_pot")||name.Contains("tin_can")||name.Contains("soda_can")||name.Contains("beer_can")||name.Contains("frying_pan");
    }
}
