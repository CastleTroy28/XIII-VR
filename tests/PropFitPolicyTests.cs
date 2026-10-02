using System;
using System.Linq;
using XiiiXR;
class PropFitPolicyTests
{
 static void Main(){
 var keys=new[]{"ashtray","chair","bottle","broom"}.Select(x=>PropFitPolicy.GripKey(x,"ignored")).ToArray();
 if(keys.Distinct().Count()!=4)throw new Exception("prop poses share keys");
 if(PropFitPolicy.GripKey("","Broom(Clone)")!="prop_broom")throw new Exception("clone identity");
 if(PropFitPolicy.Extent("BROOM")<1||PropFitPolicy.Extent("bottle")>.4f||PropFitPolicy.Extent("ashtray")!=.23f||PropFitPolicy.Extent("chair")!=.85f)throw new Exception("prop dimensions");
 // 0.1.140: the shovel is a long tool held like the mop (was fitted to 35 cm).
 if(PropFitPolicy.Extent("prop_shovel_02")<.9f||PropFitPolicy.Extent("prop_shovel_02")>1.2f||!PropFitPolicy.LongHandle("prop_Shovel_02")||!PropFitPolicy.LongHandle("prop_broom")||!PropFitPolicy.LongHandle("mop")||PropFitPolicy.LongHandle("prop_chair")||PropFitPolicy.LongHandle("bottle"))throw new Exception("shovel not a long tool");
 Console.WriteLine("PASS distinct prop poses and long-tool scale; the shovel is about a metre and held like the mop");
 }
}
