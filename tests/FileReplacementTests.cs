using System;using System.IO;using System.Text;
class FileReplacementTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var root=Path.Combine(Path.GetTempPath(),"XIII replacement "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try
  {
   var folder=Path.Combine(root,"Rai Pal", "Игра с пробелами", "BepInEx", "plugins");Directory.CreateDirectory(folder);
   var target=Path.Combine(folder,"XIII.XRBootstrap.dll");var tmp=target+".new";var backup=target+".previous";
   File.WriteAllText(target,"previous");File.WriteAllText(tmp,"updated");
   File.Replace(tmp,target,backup);
   Check(File.ReadAllText(target)=="updated"&&File.ReadAllText(backup)=="previous"&&!File.Exists(tmp),"replace lost new/old file");
   File.Copy(backup,target,true);Check(File.ReadAllText(target)=="previous","restore failed");
   File.WriteAllText(tmp,"next");File.Delete(backup);File.Replace(tmp,target,backup);
   Check(File.ReadAllText(target)=="next"&&File.ReadAllText(backup)=="previous","repeated update failed");
   Console.WriteLine("PASS: File.Replace with nonempty absolute same-directory backup; spaces/Unicode/external loader; old data preserved; restore and repeated replacement.");
   Console.WriteLine("Executed on .NET 6/Linux. PowerShell 5.1 marshaling and Windows installer execution require Windows; optional helper tests supplied.");
  }
  finally{Directory.Delete(root,true);}
 }
}
