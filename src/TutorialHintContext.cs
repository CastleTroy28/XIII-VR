using System;
using System.Text.RegularExpressions;
namespace XiiiXR;
internal enum TutorialHintContext { None, Hostage, Body, ReleaseBody }
internal static class TutorialHintMeaning
{
    internal static TutorialHintContext Detect(string term,string translation)
    {
        string s=Regex.Replace((term+" "+translation).ToLowerInvariant().Replace('_',' '),"<[^>]+>"," ");
        bool hostage=s.Contains("hostage")||s.Contains("заложник");
        bool body=Regex.IsMatch(s,@"\b(bod(?:y|ies)|corpse|тело|тела|телом|труп\w*)\b");
        if(!hostage&&!body)return TutorialHintContext.None;
        bool release=Regex.IsMatch(s,@"drop|release|let go|отпуст|брос|полож|сброс");
        // Some introduction hints mention both taking and releasing a hostage.
        // Their Interact parameter still describes acquisition.
        bool take=Regex.IsMatch(s,@"take|grab|pick.?up|capture|взять|возьм|захват|подним|поднять|подбер");
        if(release&&!take)return TutorialHintContext.ReleaseBody;
        return hostage?TutorialHintContext.Hostage:TutorialHintContext.Body;
    }
}
