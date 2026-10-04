namespace XiiiXR;
// 0.1.210: the weapon wheel tutorial, testable without the game.
internal static class MenuInputMath
{
    // The game's own open wheel (its weapon wheel tutorial opens one and locks
    // the controls until it closes) is taken over while right A is held.
    internal static bool AdoptGameWheel(bool open,bool owned,bool aHeld,bool playerActive)=>open&&!owned&&aHeld&&playerActive;
}
// 0.1.214: the weapon wheel tutorial ended with right A (WheelTutorial).
internal static class WheelTutorialMath
{
    // How long after the tutorial step its hint may still be being written.
    internal const float HintWindow=3f;
    internal static bool Labels(bool forced,float sinceStep)=>forced||sinceStep>=0&&sinceStep<HintWindow;
    // Right A pressed (not held from before) while the tutorial holds the wheel open.
    internal static bool Ends(bool pressed,bool forced,bool focused)=>pressed&&forced&&focused;
}
