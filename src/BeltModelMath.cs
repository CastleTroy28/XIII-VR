using System;
using System.Collections.Generic;
using System.Numerics;
namespace XiiiXR;
// 0.1.255: the ammunition belt (BeltModel: an ammo belt made for the mods, worn; 0.1.256: its pouch in the middle
// of the front, a row of shells on each hip) in the pouch root's axes (AmmoPouch, placed by BeltAnchorMath;
// mirrored there for a left-hander): the middle of the waist at Waist. The reserve's shotgun shells shown in
// their loops (the easiest to reach first: the left hip's, front to back); a loop without its shell shows its
// empty mouth (a dark disc where the brass head stood).
internal static class BeltModelMath
{
    internal static readonly Vector3 Waist=new(.19f,.015f,-.19f);   // the middle of the waist from the root (the old belt's)
    internal const int Cuts=2;
    private static readonly Vector4 HoleColour=new(.09f,.085f,.05f,1);
    internal static int Shells=>BeltModel.Shells;
    // The shells shown for a reserve: the first `count` of BeltModel.Use.
    internal static int Shown(int reserve)=>Math.Clamp(reserve,0,BeltModel.Shells);
    internal static bool ShellShown(int shell,int reserve)
    {for(int i=0;i<Shown(reserve);i++)if(BeltModel.Use[i]==shell)return true;return false;}
    internal static Vector3 ShellTop(int shell)=>Waist+new Vector3(BeltModel.ShellTop[shell*3],BeltModel.ShellTop[shell*3+1],BeltModel.ShellTop[shell*3+2]);
    internal static Vector3 ShellLow(int shell)=>Waist+new Vector3(BeltModel.ShellLow[shell*3],BeltModel.ShellLow[shell*3+1],BeltModel.ShellLow[shell*3+2]);
    internal static Vector3 PouchCentre=>Waist+new Vector3(BeltModel.Pouch[0],BeltModel.Pouch[1],BeltModel.Pouch[2]);
    internal static Vector3 PouchHalf=>new(BeltModel.Pouch[3],BeltModel.Pouch[4],BeltModel.Pouch[5]);
    // A hand at the pouch (taking a magazine or a rocket): within a few centimetres of it.
    internal static bool AtPouch(Vector3 local)
    {
        var d=local-PouchCentre;var h=PouchHalf;
        return Math.Abs(d.X)<h.X+.07f&&d.Y>-(h.Y+.06f)&&d.Y<h.Y+.11f&&Math.Abs(d.Z)<h.Z+.06f;
    }
    // A hand at a shown shell's head (taking a round).
    internal static bool AtShell(Vector3 local,int reserve)
    {
        for(int i=0;i<Shown(reserve);i++){var top=ShellTop(BeltModel.Use[i]);if(Vector3.Distance(local,top+new Vector3(0,.02f,0))<.085f)return true;}
        return false;
    }
    internal static ToneMesh Build(int reserve)
    {
        var shown=new bool[BeltModel.Shells];for(int i=0;i<Shown(reserve);i++)shown[BeltModel.Use[i]]=true;
        var pal=new List<byte>(BeltModel.Palette);
        // the holes' tone: one more, dark (the inside of an empty loop)
        int hole=pal.Count/3;pal.Add((byte)(HoleColour.X*255));pal.Add((byte)(HoleColour.Y*255));pal.Add((byte)(HoleColour.Z*255));
        var b=new ToneMeshBuilder(pal.ToArray());
        for(int i=0;i<BeltModel.Points;i++)
        {
            var p=Waist+new Vector3(BeltModel.Position[i*3],BeltModel.Position[i*3+1],BeltModel.Position[i*3+2]);
            var n=new Vector3(BeltModel.Normal[i*3],BeltModel.Normal[i*3+1],BeltModel.Normal[i*3+2]);
            b.Point(p,n,BeltModel.Tone[i]);
        }
        for(int t=0;t<BeltModel.Triangles;t++)
        {
            int s=BeltModel.ShellOf[t];
            if(s>=0&&!shown[s])continue;
            b.Blended(BeltModel.Triangle[t*3],BeltModel.Triangle[t*3+1],BeltModel.Triangle[t*3+2],Cuts);
        }
        for(int s=0;s<BeltModel.Shells;s++)
        {
            if(shown[s])continue;
            // the empty loop's mouth: a disc a hair over the loop's top, facing up
            var c=ShellLow(s)+new Vector3(0,.001f,0);float r=BeltModel.ShellRadius[s]*.95f;
            int centre=b.Point(c,Vector3.UnitY,hole);var ring=new int[12];
            for(int k=0;k<12;k++){float a=k*MathF.PI/6;ring[k]=b.Point(c+new Vector3(MathF.Sin(a)*r,0,MathF.Cos(a)*r),Vector3.UnitY,hole);}
            // front faces turn as the model's (the cross toward the outside): clockwise seen from above
            for(int k=0;k<12;k++)b.Flat(centre,ring[k],ring[(k+1)%12],hole);
        }
        return b.Build();
    }
}
