using System.Numerics;
namespace XiiiXR;
// Row-vector skin matrices. A helper/tip follows its nearest posed ancestor.
internal static class HandSkinPose
{
 internal static Matrix4x4[] Complete(Matrix4x4[] neutral,Matrix4x4[] posed,int[] drivers,bool[] hand,Matrix4x4[]? authored)
 {
  // Native skin can weight helper/deformer bones outside the named finger
  // hierarchy. Preserve the complete captured skin; cuff freezing is applied
  // by NativeHandMesh after baking and cropping.
  if(authored!=null)return (Matrix4x4[])authored.Clone();
  var result=(Matrix4x4[])posed.Clone();
  for(int i=0;i<result.Length;i++)
  {
   if(!hand[i])continue;
   int driver=drivers[i];
   if(driver>=0&&driver!=i&&Matrix4x4.Invert(neutral[driver],out var inverse))
    result[i]=neutral[i]*inverse*posed[driver];
  }
  return result;
 }
}
