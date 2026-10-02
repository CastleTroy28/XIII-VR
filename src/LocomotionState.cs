using System;
using System.Numerics;
namespace XiiiXR;
internal readonly struct StickSample
{
    internal readonly bool Valid;
    internal readonly Vector2 Value;
    internal readonly bool Clicked;
    internal StickSample(bool valid, Vector2 value, bool clicked=false) { Valid = valid; Value = value; Clicked=clicked; }
    internal static bool AxisClick(ulong pressed,int axis) => axis>=0 && axis<5 && (pressed & (1UL<<(32+axis)))!=0;
}
internal readonly struct ActionEdge
{
    internal readonly bool Held, Down, Up;
    internal ActionEdge(bool held, bool previous) { Held = held; Down = held && !previous; Up = !held && previous; }
}
internal sealed class LocomotionState
{
    internal Vector2 Move { get; private set; }
    internal float Turn { get; private set; }
    internal ActionEdge Jump, Crouch, Sprint;
    private bool leftArmed, rightArmed, sprintArmed;
    private int rightMode; // 0 neutral, 1 horizontal, 2 up, 3 down; locked until neutral.
    internal void Reset()
    {
        Move = Vector2.Zero; Turn = 0; leftArmed = rightArmed = sprintArmed = false; rightMode = 0;
        Jump = new ActionEdge(false, Jump.Held); Crouch = new ActionEdge(false, Crouch.Held);
        Sprint = new ActionEdge(false,Sprint.Held);
    }
    // 0.1.173: entering or leaving the water cleared
    // everything, and the left stick, still held forward from the run-up, did
    // nothing until it was let go (the log: stick 0,1, move 0,0). Now only the
    // right stick's up/down starts over (jump and swim-up are different there);
    // the left stick keeps moving, turning keeps turning.
    internal void WaterChanged()
    {
        if (rightMode == 2 || rightMode == 3) rightMode = 4;
        Jump = new ActionEdge(false, Jump.Held); Crouch = new ActionEdge(false, Crouch.Held);
    }
    internal static Vector2 Deadzone(Vector2 raw, float deadzone)
    {
        if (!float.IsFinite(raw.X) || !float.IsFinite(raw.Y)) return Vector2.Zero;
        float length = raw.Length();
        if (length <= deadzone || length < 0.00001f) return Vector2.Zero;
        return raw / length * Math.Clamp((length - deadzone) / (1 - deadzone), 0, 1);
    }
    internal static float TurnDegrees(float turn, float degreesPerSecond, float deltaTime)
    {
        if (!float.IsFinite(turn) || !float.IsFinite(degreesPerSecond) || !float.IsFinite(deltaTime)) return 0;
        return Math.Clamp(turn,-1,1) * Math.Clamp(degreesPerSecond,15,180) * Math.Clamp(deltaTime,0,0.05f);
    }
    internal void Sample(StickSample left, StickSample right, bool allowed, float deadzone)
    {
        if (!allowed) { Reset(); return; }
        deadzone = float.IsFinite(deadzone) ? Math.Clamp(deadzone,0.05f,0.4f) : 0.2f;
        Move = Vector2.Zero; Turn = 0;
        bool jump = false, crouch = false;
        bool validLeft = left.Valid && Finite(left.Value), validRight = right.Valid && Finite(right.Value);
        if (!validLeft) { leftArmed = false; sprintArmed=false; }
        else
        {
            if(!left.Clicked) sprintArmed=true;
            if (left.Value.Length() <= deadzone) leftArmed = true;
            if (leftArmed) Move = Deadzone(left.Value, deadzone);
        }
        if (!validRight) { rightArmed = false; rightMode = 0; }
        else
        {
            float x = Math.Clamp(right.Value.X,-1,1), y = Math.Clamp(right.Value.Y,-1,1);
            float ax = MathF.Abs(x), ay = MathF.Abs(y);
            if (MathF.Max(ax,ay) <= 0.35f) rightMode = 0;
            if (MathF.Max(ax,ay) <= deadzone) rightArmed = true;
            if (rightArmed)
            {
                if (rightMode == 0)
                {
                    if (ax > deadzone && ax > ay + 0.12f) rightMode = 1;
                    else if (ay >= 0.65f && ay > ax + 0.12f) rightMode = y > 0 ? 2 : 3;
                }
                if (rightMode == 1) Turn = MathF.CopySign(Math.Clamp((ax-deadzone)/(1-deadzone),0,1),x);
                if ((rightMode == 2 && y <= 0.35f) || (rightMode == 3 && y >= -0.35f)) rightMode = 4;
                // Mode 4 waits for neutral after sliding out of a vertical gesture.
                jump = rightMode == 2 && y > 0.35f;
                crouch = rightMode == 3 && y < -0.35f;
            }
        }
        Jump = new ActionEdge(jump,Jump.Held); Crouch = new ActionEdge(crouch,Crouch.Held);
        Sprint = new ActionEdge(validLeft && leftArmed && sprintArmed && left.Clicked,Sprint.Held);
    }
    private static bool Finite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);
}
