using System;
namespace XiiiXR;
internal static class PropFitPolicy
{
    internal static string GripKey(string identifier,string name)
    {
        var value=string.IsNullOrWhiteSpace(identifier)?name:identifier;
        return "prop_"+value.Replace("(Clone)","",StringComparison.OrdinalIgnoreCase).Trim().ToLowerInvariant();
    }
    // 0.1.140: long-handled props held like the mop (near the end, the other
    // hand further up the stick for a two-hand hold).
    internal static bool LongHandle(string label)
    {
        label=label.ToLowerInvariant();
        return label.Contains("broom")||label.Contains("mop")||label.Contains("shovel")||label.Contains("spade")||label.Contains("rake");
    }
    internal static float Extent(string label)
    {
        label=label.ToLowerInvariant();
        if(label.Contains("broom")||label.Contains("mop"))return 1.45f;
        // 0.1.140: the shovel was fitted like a small prop (35 cm, hanging in
        // the air); a real one is about a metre, held like the mop.
        if(label.Contains("shovel")||label.Contains("spade"))return 1.05f;
        if(label.Contains("rake"))return 1.40f;
        if(label.Contains("chair")||label.Contains("stool"))return .85f;
        if(label.Contains("ashtray"))return .23f;
        if(label.Contains("bottle"))return .32f;
        return .35f;
    }
}
