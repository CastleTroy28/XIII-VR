using System.Numerics;
namespace XiiiXR;
internal static class CarryHoldMath
{
 // How far the wrist is on the near side of a hostage's neck: the hand root
 // (the palm) is this far from the wrist, so the palm is on the throat.
 internal const float HostageWrist=.065f;
 internal static (Vector3 position,Quaternion rotation,Vector3 elbow) Hand(Vector3 neck,Quaternion yaw,bool hostage,bool right)
 {
  float side=right?-1:1;
  // Wrist on the far side, forearm across the front of the neck, elbow on
  // the holding side. Canonical +Z points along the fingers, -Y is the palm.
  // 0.1.191: a hostage's is a
  // forearm right across the throat - the wrist just past its middle, the
  // hand round the far side of the neck (the wrist was 11 cm out: the hand
  // gripped the air beside the neck), the elbow in front of the near shoulder.
  // 0.1.193: the hand itself was still past the neck (the wrist at its middle
  // put the palm 8 cm beyond it). Now the palm is on the middle of the throat,
  // the fingers round its far side, the wrist and forearm on the near side.
  var wrist=neck+Vector3.Transform(hostage?new Vector3(-side*HostageWrist,-.01f,.08f):new Vector3(side*.11f,.12f,.075f),yaw);
  var elbow=neck+Vector3.Transform(hostage?new Vector3(-side*.27f,-.035f,.095f):new Vector3(-side*.17f,-.02f,.11f),yaw);
  var forward=new Vector3(side,0,0);var up=Vector3.UnitZ;
  var across=Vector3.Cross(up,forward);
  var basis=new Matrix4x4(across.X,across.Y,across.Z,0,up.X,up.Y,up.Z,0,forward.X,forward.Y,forward.Z,0,0,0,0,1);
  var rotation=Quaternion.Normalize(yaw*Quaternion.CreateFromRotationMatrix(basis));
  return (wrist-Vector3.Transform(new Vector3(0,0,-.065f),rotation),rotation,elbow);
 }
}
