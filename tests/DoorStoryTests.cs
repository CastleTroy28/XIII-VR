using System;using System.Collections.Generic;using XiiiXR;
class DoorStoryTests
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Main()
 {
  const int Input=2,Enter=4,All=-1;
  // The leaf's own animation is its motion; a far one (another door, a lift) is story.
  Check(DoorStoryMath.Classify("CustomAnimationToolHandle",Input,true)==DoorStoryMath.Kind.Motion,"own leaf animation counted as story");
  Check(DoorStoryMath.Classify("CustomAnimationToolHandle",Input,false)==DoorStoryMath.Kind.Story,"far animation not counted as story");
  // Sound and look move nothing on.
  foreach(var t in new[]{"SoundSender","EmitAISoundEvent","FmodEmitterToggleEvent","HighlightEventHandler","CameraShakeSet"})
   Check(DoorStoryMath.Classify(t,Input,false)==DoorStoryMath.Kind.Cosmetic,t+" counted as story");
  // Guards let in, enabled objects, timelines, dialogue, checkpoints: story.
  foreach(var t in new[]{"ActivateBehaviorTree","ObjectEnableStateSet","PlayableDirectorStart","DialogueEventHandler","CheckpointTrigger","UnityEventHandler","Callback"})
   Check(DoorStoryMath.Classify(t,Input,false)==DoorStoryMath.Kind.Story,t+" not counted as story");
  // Only what the door's interaction (Input) runs counts; All includes it.
  Check(DoorStoryMath.Classify("ActivateBehaviorTree",Enter,false)==null,"a trigger-volume event counted for the door's interaction");
  Check(DoorStoryMath.Classify("ActivateBehaviorTree",All,false)==DoorStoryMath.Kind.Story,"an All-trigger event ignored");
  var plain=new List<(string,DoorStoryMath.Kind)>{("CustomAnimationToolHandle",DoorStoryMath.Kind.Motion),("SoundSender",DoorStoryMath.Kind.Cosmetic)};
  string all=DoorStoryMath.Describe(plain,out string story);
  Check(story==""&&all=="door motion, SoundSender","ordinary door described wrongly: "+all+" / "+story);
  var hostage=new List<(string,DoorStoryMath.Kind)>(plain){("ActivateBehaviorTree",DoorStoryMath.Kind.Story),("ActivateBehaviorTree",DoorStoryMath.Kind.Story),("DialogueEventHandler",DoorStoryMath.Kind.Story)};
  all=DoorStoryMath.Describe(hostage,out story);
  Check(story=="ActivateBehaviorTree x2, DialogueEventHandler","story events described wrongly: "+story);
  Check(all=="door motion, ActivateBehaviorTree x2, DialogueEventHandler, SoundSender","all events described wrongly: "+all);
  Check(DoorStoryMath.Describe(new List<(string,DoorStoryMath.Kind)>(),out story)=="none"&&story=="","door without events described wrongly");
  Console.WriteLine("PASS: door events sorted into the leaf's own motion, sound/look and story (guards, objects, timelines, dialogue, checkpoints); only the door's own interaction counts; short descriptions for the log.");
 }
}
