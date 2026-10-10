using System.Numerics;
namespace XiiiXR;
// 0.1.257: a gun drawn larger than the game draws it against its own hand (EquipmentProfile.Growth): the hand
// holding it is drawn at the size it had (the game's size over the growth), its fist where it was on the gun
// (the wrist moved toward the fist by the hand's shrinking), so taking the gun does not make the hand grow.
internal static class GrownGrip
{
    // wrist, turn and size: the hand's hold in the gun's fitted space as the game's hand gives it; fist: where the
    // fist closes round a handle in the hand's own space at size 1.
    internal static (Vector3 wrist,float size) Hand(Vector3 wrist,Quaternion turn,float size,Vector3 fist,float growth)
    {
        if(!(growth>0)||growth==1||!float.IsFinite(growth)||!float.IsFinite(size)||size<=0)return(wrist,size);
        float kept=size/growth;
        return(wrist+Vector3.Transform(fist*(size-kept),turn),kept);
    }
}
