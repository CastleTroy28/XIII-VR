using System;
using HarmonyLib;
namespace XiiiXR;
// 0.1.162: the game's own moments when a story movie player, a death screen
// or a weapon pickup appears, so the kept scene searches (SceneFind) get it at
// once and the searches themselves can be rare. Each hook only hands the
// object on; nothing of the game's is changed.
internal static class SceneHooks
{
    private static readonly Harmony patches=new("xiii.vr.xrbootstrap.scenehooks");
    private static bool installed;
    internal static Action<CutscenePlayer>? Video;
    internal static Action<DeathScreenControl>? Death;
    internal static Action<WeaponPickup>? Pickup;
    internal static bool VideoHooked{get;private set;}
    internal static bool DeathHooked{get;private set;}
    internal static bool PickupHooked{get;private set;}
    internal static void Install()
    {
        if(installed)return;installed=true;
        var done=new System.Collections.Generic.List<string>();
        bool Hook(Type type,string method,string handler)
        {
            try
            {
                var target=AccessTools.DeclaredMethod(type,method)??throw new InvalidOperationException("missing");
                patches.Patch(target,postfix:new HarmonyMethod(typeof(SceneHooks),handler));
                done.Add(type.Name+"."+method);return true;
            }
            catch(Exception ex){Bootstrap.Warn("SCENE HOOK "+type.Name+"."+method+" unavailable (found by the slower search instead): "+ex.Message);return false;}
        }
        VideoHooked=Hook(typeof(CutscenePlayer),"PlayVideoSequence",nameof(VideoSeen))|Hook(typeof(CutscenePlayer),"OnEnable",nameof(VideoSeen));
        DeathHooked=Hook(typeof(DeathScreenControl),"Initiate",nameof(DeathSeen))|Hook(typeof(DeathScreenControl),"OnEnable",nameof(DeathSeen));
        PickupHooked=Hook(typeof(WeaponPickup),"Start",nameof(PickupSeen));
        Bootstrap.Write("SCENE HOOKS "+(done.Count>0?string.Join(", ",done):"none")+"; scene searches kept between rare runs");
    }
    private static void VideoSeen(CutscenePlayer __instance){try{if(__instance!=null)Video?.Invoke(__instance);}catch(Exception){}}
    private static void DeathSeen(DeathScreenControl __instance){try{if(__instance!=null)Death?.Invoke(__instance);}catch(Exception){}}
    private static void PickupSeen(WeaponPickup __instance){try{if(__instance!=null)Pickup?.Invoke(__instance);}catch(Exception){}}
}
