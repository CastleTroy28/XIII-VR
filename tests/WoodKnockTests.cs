using System;using XiiiXR;
class WoodKnockTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var w=WoodKnock.Wav;
  Check(w.Length==44+(int)(WoodKnock.Rate*WoodKnock.Seconds)*2&&System.Text.Encoding.ASCII.GetString(w,0,4)=="RIFF"&&System.Text.Encoding.ASCII.GetString(w,8,4)=="WAVE"&&System.Text.Encoding.ASCII.GetString(w,36,4)=="data","knock WAV layout");
  Check(BitConverter.ToUInt16(w,20)==1&&BitConverter.ToUInt16(w,22)==1&&BitConverter.ToInt32(w,24)==44100&&BitConverter.ToUInt16(w,34)==16&&BitConverter.ToInt32(w,4)==w.Length-8,"knock WAV format");
  Check(ReferenceEquals(w,WoodKnock.Wav),"knock made more than once");
  var s=WoodKnock.Samples();float peak=0,early=0,late=0;int n=s.Length;
  for(int i=0;i<n;i++){peak=Math.Max(peak,Math.Abs(s[i]));if(i<n/5)early+=s[i]*s[i];if(i>=n*4/5)late+=s[i]*s[i];}
  Check(Math.Abs(peak-.85f)<1e-3f,"knock not normalised: "+peak);
  Check(late<early*.001f,"knock rings on instead of dying away");
  Check(Math.Abs(s[0])<.01f,"knock starts with a click at full level");
  var again=WoodKnock.Samples();for(int i=0;i<n;i++)if(again[i]!=s[i])throw new Exception("knock not the same every time");
  Check(MeleeDamageMath.WoodenProp("wpn_ms_broom")&&MeleeDamageMath.WoodenProp("WPN_MS_MOP")&&!MeleeDamageMath.WoodenProp("wpn_ms_shovel")&&!MeleeDamageMath.WoodenProp("wpn_ms_chair")&&!MeleeDamageMath.WoodenProp(null),"wooden things chosen wrong");
  Console.WriteLine("PASS: wooden knock: 0.16 s 16-bit mono WAV, normalised, dies away (last fifth < 0.1 % of the first's energy), soft start, the same every time; brooms and mops are wooden, shovels and chairs not.");
 }
}
