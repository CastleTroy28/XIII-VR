namespace XiiiXR;
// 0.1.214: the next knife taken at once after a throw (WeaponHands.KnifeRetake).
internal static class KnifeRetakeMath
{
    // How long a throw let go waits for the game's knife to be ready (s).
    internal const float PressWait=1.2f;
    // Thrown by letting go of the grip and launched (the game's knife has
    // left), or the hand about to be emptied after such a throw.
    internal static bool Thrown(bool byGrip,float releasedAt,float launchedAt,bool emptying)=>byGrip&&releasedAt>0&&launchedAt>=releasedAt||emptying;
    // A grip press at the knife's place on the chest, with knives left
    // (left: -1 when the game's count is unknown).
    internal static bool Retake(bool thrown,bool gripDown,bool atPlace,int left)=>thrown&&gripDown&&atPlace&&left!=0;
}
