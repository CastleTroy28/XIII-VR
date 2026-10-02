using System;
using System.Numerics;
using XiiiXR;
class HandSkinPoseTests
{
 static void Main()
 {
  var neutral=new[]{Matrix4x4.Identity,Matrix4x4.CreateTranslation(0,0,.04f),Matrix4x4.CreateTranslation(0,0,.07f),Matrix4x4.CreateTranslation(.02f,0,.07f)};
  var pose=(Matrix4x4[])neutral.Clone();var turn=Matrix4x4.CreateRotationX(1.2f);
  pose[1]=neutral[1]*turn;
  var corrected=HandSkinPose.Complete(neutral,pose,new[]{-1,1,1,-1},new[]{false,true,true,false},null);
  if(Vector3.Distance(corrected[2].Translation,Vector3.Transform(neutral[2].Translation,turn))>1e-5f)throw new Exception("weighted fingertip helper stays in bind pose");
  if(corrected[3]!=neutral[3])throw new Exception("unrelated bone changed for procedural pose");
  var native=(Matrix4x4[])pose.Clone();native[2]=neutral[2]*turn;native[3]=neutral[3]*turn;
  var exact=HandSkinPose.Complete(neutral,pose,new[]{-1,1,1,-1},new[]{false,true,true,false},native);
  for(int i=0;i<native.Length;i++)if(exact[i]!=native[i])throw new Exception("native deformer omitted from captured skin pose");
  exact[1]=Matrix4x4.Identity;if(native[1]==Matrix4x4.Identity)throw new Exception("capture mutated");
  Console.WriteLine("PASS complete native skin includes unlisted deformer bones; procedural helper inherits posed finger; source cache is immutable");
 }
}
