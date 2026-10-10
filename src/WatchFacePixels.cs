using System;
using System.Globalization;
namespace XiiiXR;
// 0.1.254: the 3D watches' faces, the classic XIII mod's (watchface.c), drawn on the CPU into RGBA pixels
// (rows top to bottom, opaque): an LCD in two rows on a light olive screen with a red line round it, the
// numbers in seven segments (the unlit ones faint), shapes filled with 4x4 samples a pixel.
//   the health watch:  a cross, its label and the health; a shield, its label and the armour
//   the ammo watch:    a cartridge, its label and the rounds in the weapon; three, their label and the rest
//                      (a gun in each hand: the other hand's rounds, its letter; a reload step: its word
//                      across the lower row; the M16's grenades small beside the rounds)
// The labels in the game's language, in the watch font (WatchFaceGeometry's glyphs).
internal readonly record struct WatchReading(bool Ammo,int Top,int Bottom,string TopWord,string BottomWord,bool Pair,bool LeftFirst,int Grenades,bool Stage);
internal static class WatchFacePixels
{
    internal const int Width=256,Height=192;
    // ---- what the watch shows, from the texts the HUD has (GloveVisual.Readout) ----
    internal static WatchReading Read(bool ammo,string main,string extra,bool leftFirst=false)
    {
        main=WatchFaceGeometry.Clean(main);extra=WatchFaceGeometry.Clean(extra);
        if(!ammo)
        {
            Value(main,out int hp,out _);Value(extra,out int ap,out _);
            return new WatchReading(false,hp<0?-1:hp,ap,"","",false,false,-1,false);
        }
        // The M16's grenades after the rounds: a word of letters then digits (G3, Г3).
        int grenades=-1;
        int space=main.LastIndexOf(' ');
        if(space>0)
        {
            string last=main[(space+1)..];int digit=0;while(digit<last.Length&&!char.IsDigit(last[digit]))digit++;
            if(digit>0&&digit<last.Length&&AllDigits(last[digit..])&&int.TryParse(last[digit..],NumberStyles.None,CultureInfo.InvariantCulture,out int g)){grenades=g;main=main[..space].Trim();}
        }
        bool stage=IsWord(extra);
        int top,bottom;string topWord,bottomWord;bool pair=false;
        var parts=main.Split('/');
        if(parts.Length>=2)
        {
            pair=true;Value(parts[0].Trim(),out top,out topWord);Value(parts[1].Trim(),out bottom,out bottomWord);
        }
        else{Value(main,out top,out topWord);bottom=-1;bottomWord="";}
        if(stage){bottom=-1;bottomWord=extra;pair=false;}
        else if(!pair)
        {
            // A second count before the rest ("6 / 40"): the rest is the last.
            var rest=extra.Split('/');Value(rest[^1].Trim(),out bottom,out bottomWord);
        }
        return new WatchReading(true,top,bottom,topWord,bottomWord,pair,leftFirst,grenades,stage);
    }
    private static bool AllDigits(string s){foreach(char c in s)if(!char.IsDigit(c))return false;return s.Length>0;}
    // A reload step's word (letters, not a count or the infinity sign).
    private static bool IsWord(string s){foreach(char c in s)if(char.IsLetter(c))return true;return false;}
    // A count (its first number), or a sign the digits cannot show (∞), or nothing (-).
    private static void Value(string s,out int number,out string word)
    {
        number=-1;word="";
        int i=0;while(i<s.Length&&!char.IsDigit(s[i]))i++;
        if(i<s.Length)
        {
            int j=i;while(j<s.Length&&char.IsDigit(s[j]))j++;
            if(int.TryParse(s[i..j],NumberStyles.None,CultureInfo.InvariantCulture,out int n))number=n;
            return;
        }
        if(s.Length>0&&s!="-"&&s!="?")word=s;
    }
    // ---- the labels (short: the font has no accents; WatchFaceGeometry folds them) ----
    internal enum Label{Health,Armor,Clip,Reserve,Left,Right}
    private static readonly string[] Codes={"en","ru","de","fr","es","it","pl","pt"};
    private static readonly string[][] Labels=
    {
        new[]{"HP","ARM","MAG","RES","L","R"},
        new[]{"ЖИЗНЬ","БРОНЯ","МАГ","ЗАПАС","Л","П"},
        new[]{"LP","PZ","MAG","VOR","L","R"},
        new[]{"VIE","ARM","CHG","RES","G","D"},
        new[]{"VIDA","ARM","CAR","RES","I","D"},
        new[]{"VITA","ARM","CAR","RIS","S","D"},
        new[]{"ZDR","PANC","MAG","ZAP","L","P"},
        new[]{"VIDA","ARM","CAR","RES","E","D"},
    };
    internal static string LabelOf(string code,Label label)
    {
        int i=Array.IndexOf(Codes,(code??"en").Split('-','_')[0].ToLowerInvariant());
        return Labels[i<0?0:i][(int)label];
    }
    internal static string[] LanguageCodes=>(string[])Codes.Clone();
    // ---- drawing ----
    private readonly struct Col{internal readonly float R,G,B;internal Col(float r,float g,float b){R=r;G=g;B=b;}}
    private static readonly Col Case=new(40,40,43),Red=new(168,40,32),LcdTop=new(172,180,146),LcdBottom=new(190,197,162),Ink=new(28,34,24);
    private sealed class Canvas
    {
        internal readonly byte[] Px;internal float Slant;
        internal Canvas(byte[] px){Px=px;}
        internal void Blend(int x,int y,Col c,float a)
        {
            if(x<0||y<0||x>=Width||y>=Height||!(a>.002f))return;
            if(a>1)a=1;int p=(y*Width+x)*4;
            float r=Px[p],g=Px[p+1],b=Px[p+2];
            r+=(c.R-r)*a;g+=(c.G-g)*a;b+=(c.B-b)*a;
            Px[p]=(byte)(r+.5f);Px[p+1]=(byte)(g+.5f);Px[p+2]=(byte)(b+.5f);Px[p+3]=255;
        }
        // A convex polygon (points in order), its coverage sampled 4x4 a pixel: each row of samples crosses it
        // in one run, each pixel covered by the samples of the four rows' runs it has.
        internal void Poly(float[] xy,int n,Col c,float alpha)
        {
            float y0=1e9f,y1=-1e9f,area=0;
            for(int i=0;i<n;i++){y0=Math.Min(y0,xy[2*i+1]);y1=Math.Max(y1,xy[2*i+1]);int j=(i+1)%n;area+=xy[2*i]*xy[2*j+1]-xy[2*j]*xy[2*i+1];}
            float sign=area<0?-1:1;
            int ya=Math.Max(0,(int)y0),yb=Math.Min(Height-1,(int)y1);
            Span<int> gl=stackalloc int[4],gr=stackalloc int[4];
            for(int y=ya;y<=yb;y++)
            {
                int lo=4*Width,hi=-1;
                for(int r=0;r<4;r++)
                {
                    float sy=y+(r+.5f)/4,xl=-1e9f,xr=1e9f;
                    for(int i=0;i<n&&xl<=xr;i++)
                    {
                        int j=(i+1)%n;
                        float ex=xy[2*j]-xy[2*i],ey=xy[2*j+1]-xy[2*i+1];
                        float a=ex*(sy-xy[2*i+1])+ey*xy[2*i],b=ey*sign;   // inside: sign * (a - ey * sx) >= 0
                        if(b>0)xr=Math.Min(xr,a/ey);else if(b<0)xl=Math.Max(xl,a/ey);else if(a*sign<0)xr=-1e9f;
                    }
                    if(xl>xr||xr<0||xl>Width){gl[r]=1;gr[r]=0;continue;}
                    float fl=Math.Max(xl,-1)*4-.5f,fr=Math.Min(xr,Width+1)*4-.5f;
                    int il=(int)MathF.Ceiling(fl),ir=(int)MathF.Floor(fr);
                    gl[r]=il;gr[r]=ir;
                    if(il<=ir){lo=Math.Min(lo,il);hi=Math.Max(hi,ir);}
                }
                if(hi<lo)continue;
                int xa=Math.Max(0,lo>>2),xb=Math.Min(Width-1,hi>>2);
                for(int x=xa;x<=xb;x++)
                {
                    int hits=0;
                    for(int r=0;r<4;r++){int a=Math.Max(gl[r],4*x),b=Math.Min(gr[r],4*x+3);if(b>=a)hits+=b-a+1;}
                    if(hits>0)Blend(x,y,c,alpha*hits/16);
                }
            }
        }
        // A rectangle with rounded corners: a polygon of its corners' arcs.
        internal void RRect(float x0,float y0,float x1,float y1,float rad,Col c,float alpha)
        {
            var pts=new float[64];int n=0;
            rad=Math.Min(rad,Math.Min((x1-x0)/2,(y1-y0)/2));
            float[] cx={x1-rad,x0+rad,x0+rad,x1-rad},cy={y0+rad,y0+rad,y1-rad,y1-rad};
            for(int k=0;k<4;k++)for(int s=0;s<8;s++)
            {
                float a=k*MathF.PI/2+s/7f*MathF.PI/2;   // each corner a quarter turn (y down: counterclockwise on screen)
                pts[2*n]=cx[k]+rad*MathF.Cos(a);pts[2*n+1]=cy[k]-rad*MathF.Sin(a);n++;
            }
            Poly(pts,n,c,alpha);
        }
        // ---- seven segments ----
        private void Slanted(float[] p,float baseY){for(int i=0;i<p.Length/2;i++)p[2*i]+=Slant*(baseY-p[2*i+1]);}
        private void HSeg(float cx,float cy,float len,float t,float baseY,Col c,float a)
        {
            var p=new[]{cx-len/2,cy,cx-len/2+t/2,cy-t/2,cx+len/2-t/2,cy-t/2,cx+len/2,cy,cx+len/2-t/2,cy+t/2,cx-len/2+t/2,cy+t/2};
            Slanted(p,baseY);Poly(p,6,c,a);
        }
        private void VSeg(float cx,float cy,float len,float t,float baseY,Col c,float a)
        {
            var p=new[]{cx,cy-len/2,cx+t/2,cy-len/2+t/2,cx+t/2,cy+len/2-t/2,cx,cy+len/2,cx-t/2,cy+len/2-t/2,cx-t/2,cy-len/2+t/2};
            Slanted(p,baseY);Poly(p,6,c,a);
        }
        private static readonly byte[] Digits={0x3F,0x06,0x5B,0x4F,0x66,0x6D,0x7D,0x07,0x7F,0x6F};
        internal static int SegMask(char ch)=>ch>='0'&&ch<='9'?Digits[ch-'0']:ch=='-'?0x40:0;
        internal void Digit(float x0,float y0,float w,float h,float t,int mask,Col c,float a)
        {
            float baseY=y0+h,gap=t*.18f,hl=w-t-2*gap,vl=h/2-t/2-2*gap;
            float hx=x0+w/2,vx0=x0+t/2,vx1=x0+w-t/2;
            if((mask&0x01)!=0)HSeg(hx,y0+t/2,hl,t,baseY,c,a);
            if((mask&0x02)!=0)VSeg(vx1,y0+t/2+gap+vl/2,vl,t,baseY,c,a);
            if((mask&0x04)!=0)VSeg(vx1,y0+h/2+gap+vl/2,vl,t,baseY,c,a);
            if((mask&0x08)!=0)HSeg(hx,y0+h-t/2,hl,t,baseY,c,a);
            if((mask&0x10)!=0)VSeg(vx0,y0+h/2+gap+vl/2,vl,t,baseY,c,a);
            if((mask&0x20)!=0)VSeg(vx0,y0+t/2+gap+vl/2,vl,t,baseY,c,a);
            if((mask&0x40)!=0)HSeg(hx,y0+h/2,hl,t,baseY,c,a);
        }
        // A number right-aligned at x1 in three places over its unlit segments (Unlit: faint eights); v < 0: dashes.
        internal void Number(int v,float x1,float y0,float h)
        {
            string s=v<0?"--":Math.Min(v,999).ToString(CultureInfo.InvariantCulture);
            float w=h*.52f,t=h*.13f,step=w+h*.16f;
            Slant=.08f;
            for(int k=0;k<3&&k<s.Length;k++)Digit(x1-(k+1)*step+h*.16f,y0,w,h,t,SegMask(s[s.Length-1-k]),Ink,.92f);
            Slant=0;
        }
        internal void Unlit(float x1,float y0,float h)
        {
            float w=h*.52f,t=h*.13f,step=w+h*.16f;
            Slant=.08f;
            for(int k=0;k<3;k++)Digit(x1-(k+1)*step+h*.16f,y0,w,h,t,0x7F,Ink,.07f);
            Slant=0;
        }
        // ---- the icons, in a 40-pixel box ----
        internal void Cross(float x,float y){RRect(x+3,y+14,x+37,y+26,2,Ink,.92f);RRect(x+14,y+3,x+26,y+37,2,Ink,.92f);}
        private static readonly float[] ShieldPoints={20,2,36,8,35,22,28,33,20,38,12,33,5,22,4,8};
        internal void Shield(float x,float y)
        {
            var outer=new float[16];var inner=new float[16];
            for(int i=0;i<8;i++)
            {
                outer[2*i]=x+ShieldPoints[2*i];outer[2*i+1]=y+ShieldPoints[2*i+1];
                inner[2*i]=x+20+(ShieldPoints[2*i]-20)*.72f;inner[2*i+1]=y+20+(ShieldPoints[2*i+1]-20)*.72f;
            }
            Poly(outer,8,Ink,.92f);Poly(inner,8,LcdBottom,1);
        }
        internal void Cartridge(float cx,float y,float s)
        {
            var tip=new[]{cx-6*s,y+14*s,cx-6*s,y+9*s,cx-3.5f*s,y+3*s,cx,y+1*s,cx+3.5f*s,y+3*s,cx+6*s,y+9*s,cx+6*s,y+14*s};
            Poly(tip,7,Ink,.92f);
            RRect(cx-6*s,y+15.5f*s,cx+6*s,y+38*s,1,Ink,.92f);
        }
        // Text in the watch font, centred at (cx, cy); at most `width` wide (smaller cells for long words).
        internal void Text(string s,float cx,float cy,float cell,float width)
        {
            s=WatchFaceGeometry.Clean(s);int n=s.Length;if(n<=0)return;
            cell=Math.Min(cell,width/Math.Max(1,n*6-1));
            float x=cx-(n*6-1)*cell/2,y=cy-3.5f*cell,dot=cell*.9f;
            for(int i=0;i<n;i++,x+=6*cell)
            {
                if(s[i]==' ')continue;
                string rows=WatchFaceGeometry.GlyphRows(s[i]);
                for(int r=0;r<7;r++)for(int c=0;c<5;c++)
                    if(rows[r*5+c]=='1')RRect(x+c*cell,y+r*cell,x+c*cell+dot,y+r*cell+dot,cell*.15f,Ink,.92f);
            }
        }
    }
    // The parts that stay (the screen, the icons, the labels, the unlit segments) drawn once for each layout
    // and language; each change draws only the lit numbers (and the words) over a copy.
    private static readonly System.Collections.Generic.Dictionary<string,byte[]> bases=new();
    internal static int BasesDrawn{get;private set;}
    internal static byte[] Draw(WatchReading reading,string code)
    {
        bool topWord=reading.TopWord.Length>0,bottomWord=reading.BottomWord.Length>0;
        string key=(reading.Ammo?"A":"H")+(reading.Pair?"P":"")+(reading.Stage?"S":"")+(reading.LeftFirst?"L":"")+(topWord?"t":"")+(bottomWord?"b":"")+code;
        if(!bases.TryGetValue(key,out var baseLayer))
        {
            if(bases.Count>=24)bases.Clear();
            baseLayer=Base(reading,code,topWord,bottomWord);bases[key]=baseLayer;BasesDrawn++;
        }
        var px=(byte[])baseLayer.Clone();var k=new Canvas(px);
        float[] row={16,Height/2f+6};
        Lit(k,reading.Top,reading.TopWord,row[0]);
        if(reading.Stage)k.Text(reading.BottomWord,Width/2f,row[1]+36,6,Width-44);   // the reload step across the row
        else Lit(k,reading.Bottom,reading.BottomWord,row[1]);
        // The M16's grenades, small between the cartridge and the rounds.
        if(reading.Ammo&&reading.Grenades>=0)k.Text(UiLanguage.L("GL",code)+Math.Min(reading.Grenades,99),90,row[0]+56,2.6f,44);
        return px;
    }
    private static byte[] Base(WatchReading reading,string code,bool topWord,bool bottomWord)
    {
        var px=new byte[Width*Height*4];var k=new Canvas(px);
        for(int i=0;i<Width*Height;i++){px[i*4]=(byte)Case.R;px[i*4+1]=(byte)Case.G;px[i*4+2]=(byte)Case.B;px[i*4+3]=255;}
        // The red line, the screen inside it (lighter toward the bottom).
        k.RRect(5,5,Width-5,Height-5,12,Red,1);
        k.RRect(9,9,Width-9,Height-9,9,LcdTop,1);
        for(int y=10;y<Height-10;y++)
        {
            float t=(y-10f)/(Height-20);
            var c=new Col(LcdTop.R+(LcdBottom.R-LcdTop.R)*t,LcdTop.G+(LcdBottom.G-LcdTop.G)*t,LcdTop.B+(LcdBottom.B-LcdTop.B)*t);
            for(int x=12;x<Width-12;x++)k.Blend(x,y,c,1);
        }
        k.RRect(18,Height/2-1.5f,Width-18,Height/2+1.5f,1,Ink,.8f);
        float[] row={16,Height/2f+6};
        string topLabel,bottomLabel;
        if(!reading.Ammo)
        {
            k.Cross(20,row[0]+6);k.Shield(20,row[1]+6);
            topLabel=LabelOf(code,Label.Health);bottomLabel=LabelOf(code,Label.Armor);
        }
        else
        {
            k.Cartridge(40,row[0]+6,1);
            if(reading.Stage){}
            else if(reading.Pair)k.Cartridge(40,row[1]+6,1);
            else for(int i=-1;i<=1;i++)k.Cartridge(40+i*11,row[1]+12,.72f);
            topLabel=LabelOf(code,Label.Clip);
            bottomLabel=reading.Pair?LabelOf(code,reading.LeftFirst?Label.Right:Label.Left):LabelOf(code,Label.Reserve);
        }
        k.Text(topLabel,40,row[0]+61,2.1f,56);
        if(!reading.Stage)k.Text(bottomLabel,40,row[1]+61,2.1f,56);
        if(!topWord)k.Unlit(Width-20,row[0]+6,60);
        if(!reading.Stage&&!bottomWord)k.Unlit(Width-20,row[1]+6,60);
        return px;
    }
    private static void Lit(Canvas k,int value,string word,float rowY)
    {
        if(word.Length>0)k.Text(word,177,rowY+36,8,118);   // a sign the digits cannot show (∞)
        else k.Number(value,Width-20,rowY+6,60);
    }
    // Unity keeps a texture's bottom row first.
    internal static byte[] BottomUp(byte[] topDown)
    {
        var o=new byte[topDown.Length];int stride=Width*4;
        for(int y=0;y<Height;y++)Buffer.BlockCopy(topDown,y*stride,o,(Height-1-y)*stride,stride);
        return o;
    }
}
