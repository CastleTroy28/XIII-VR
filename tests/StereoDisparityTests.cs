using System;using XiiiXR;
class StereoDisparityTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 // A textured scene: the right eye's picture is the left one moved by `far` px, and a box in the middle by `far-depth` px.
 static byte[] Pair(int w,int h,int far,int depth,bool same=false)
 {
  var rnd=new Random(7);var scene=new byte[(w+400)*h];for(int i=0;i<scene.Length;i++)scene[i]=(byte)rnd.Next(256);
  var box=new byte[w*h];for(int i=0;i<box.Length;i++)box[i]=(byte)rnd.Next(256);
  var rgb=new byte[w*2*h*3];
  byte Px(int x,int y,int shift){int bx=x-shift;bool inBox=bx>=w*2/5&&bx<w*3/5&&y>=h*2/5&&y<h*3/5;return inBox?box[y*w+bx]:scene[y*(w+400)+Math.Clamp(x-shift+200,0,w+399)];}
  for(int y=0;y<h;y++)for(int x=0;x<w;x++)
  {
   byte l=Px(x,y,0);int boxShift=far-depth;
   int bx=x-boxShift;bool inBox=bx>=w*2/5&&bx<w*3/5&&y>=h*2/5&&y<h*3/5;
   byte r=same?l:inBox?box[y*w+bx]:scene[y*(w+400)+Math.Clamp(x-far+200,0,w+399)];
   int a=(y*w*2+x)*3,b=(y*w*2+w+x)*3;rgb[a]=rgb[a+1]=rgb[a+2]=l;rgb[b]=rgb[b+1]=rgb[b+2]=r;
  }
  return rgb;
 }
 static void Main()
 {
  int w=240,h=200;
  var s=StereoDisparity.Measure(Pair(w,h,-50,0),w*2,h);
  Check(s.Blocks>20&&Math.Abs(s.MedianShift+50)<.6f,"a picture moved 50 px between the eyes not measured: "+s.MedianShift+" ("+s.Blocks+" places)");
  Check(s.Identical>StereoDisparity.SameBelow,"moved pictures taken for the same");
  var near=StereoDisparity.Measure(Pair(w,h,-50,14),w*2,h);
  Check(near.Blocks>20&&Math.Abs(near.CenterShift+64)<.6f,"a near thing in the middle not seen moving further: "+near.CenterShift);
  var same=StereoDisparity.Measure(Pair(w,h,0,0,true),w*2,h);
  Check(same.Identical<StereoDisparity.SameBelow,"the same picture in both eyes not found: "+same.Identical);
  var flat=StereoDisparity.Measure(new byte[w*2*h*3],w*2,h);
  Check(flat.Blocks==0,"a flat picture matched");
  Check(StereoDisparity.Measure(new byte[10],8,1).Blocks==0,"a too small picture measured");
  // Far ahead, with the eyes' asymmetric fields of view (m02 -0.2425 / +0.2425): a quarter of the width to the left in the right picture.
  Check(Math.Abs(StereoDisparity.FarShift(-.2425f,.2425f,480)+116.4f)<.1f,"far shift from the projections wrong");
  Console.WriteLine("PASS: 0.1.237 STEREO CHECK measures how far things move between the eyes' pictures (the whole picture, a near thing in the middle), finds two identical pictures and ignores flat ones; the far shift from the eyes' projections.");
 }
}
