using System;using System.Linq;using System.Globalization;using XiiiXR;
class BoltRelationTextTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  CultureInfo.CurrentCulture=new CultureInfo("ru-RU");
  var a=Enumerable.Range(0,16).Select(i=>i*.125f-1.3f).ToArray();var b=Enumerable.Range(0,16).Select(i=>i==0?1e-7f:i*3.5f).ToArray();
  string text=BoltRelationText.Format(new[]{"wpn_crossbow_arrow_a_BND_JNT","x"},new[]{a,b});
  Check(!text.Contains(','),"locale decimal comma in the file");
  var back=BoltRelationText.Parse(text,new[]{"x","wpn_crossbow_arrow_a_BND_JNT"})!;
  Check(back!=null&&back[0].SequenceEqual(b)&&back[1].SequenceEqual(a),"round trip by bone name");
  Check(BoltRelationText.Parse(text,new[]{"missing"})==null,"a missing bone accepted");
  Check(BoltRelationText.Parse("x 1 2 3\ny NaN "+string.Join(" ",Enumerable.Repeat("0",15)),new[]{"x"})==null&&BoltRelationText.Parse("",new[]{"x"})==null,"broken file accepted");
  Console.WriteLine("PASS: crossbow bolt placement kept on disk: exact round trip, locale-independent, by bone name; broken or partial files ignored.");
 }
}
