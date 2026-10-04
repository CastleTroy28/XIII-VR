namespace XiiiXR;
// 0.1.218: the player's allies (Jones and the like) are left alone by
// everything the mod adds: no hostage, punch, knockout, gun grab, body grab
// or hit reaction. An ally is one of the game's Ally characters, or one the
// game lets the player neither hurt nor take hostage (its own flags:
// canBeHurtByPlayer, canBeTakenHostageEvenIfCannotBeHurt).
// 0.1.230: everyone in a memory (a playable flashback): the game takes no
// hostage there (CanTakeHostage refuses while GameManager.IsInFlashback), and
// Kim in the last mission's memory, not an Ally to the game, was taken hostage
// by the VR rule.
internal static class AllyRule
{
    internal static bool Ally(bool allyClass,bool hurtByPlayer,bool hostageAnyway,bool inFlashback=false)=>inFlashback||allyClass||!hurtByPlayer&&!hostageAnyway;
    // The game's own rule for a hostage (PlayerHostageController.CanTakeHostage):
    // not in a flashback; one the player may hurt, or one marked to be taken even so.
    internal static bool HostageByGame(bool allyClass,bool hurtByPlayer,bool hostageAnyway,bool inFlashback=false)=>!inFlashback&&!allyClass&&(hurtByPlayer||hostageAnyway);
}
