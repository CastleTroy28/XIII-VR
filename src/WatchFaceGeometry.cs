using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
namespace XiiiXR;
// Luminous pixels are real triangles in the watch's local metre coordinates.
// No Canvas, font asset, camera-space transform or late UI batching is involved.
internal static class WatchFaceGeometry
{
    private static readonly Dictionary<char,string> Glyphs=new()
    {
        ['0']="01110/10001/10011/10101/11001/10001/01110", ['1']="00100/01100/00100/00100/00100/00100/01110",
        ['2']="01110/10001/00001/00010/00100/01000/11111", ['3']="11110/00001/00001/01110/00001/00001/11110",
        ['4']="00010/00110/01010/10010/11111/00010/00010", ['5']="11111/10000/10000/11110/00001/00001/11110",
        ['6']="01110/10000/10000/11110/10001/10001/01110", ['7']="11111/00001/00010/00100/01000/01000/01000",
        ['8']="01110/10001/10001/01110/10001/10001/01110", ['9']="01110/10001/10001/01111/00001/00001/01110",
        ['-']="00000/00000/00000/11111/00000/00000/00000", ['/']="00001/00001/00010/00100/01000/10000/10000",
        ['∞']="00000/00000/01010/10101/01010/00000/00000", ['?']="01110/10001/00001/00010/00100/00000/00100",
        ['Ж']="10101/10101/01110/00100/01110/10101/10101", ['И']="10001/10001/10011/10101/11001/10001/10001",
        ['З']="01110/10001/00001/00110/00001/10001/01110", ['Н']="10001/10001/10001/11111/10001/10001/10001",
        ['Ь']="10000/10000/10000/11110/10001/10001/11110", ['Б']="11111/10000/10000/11110/10001/10001/11110",
        ['Р']="11110/10001/10001/11110/10000/10000/10000", ['О']="01110/10001/10001/10001/10001/10001/01110",
        ['Я']="01111/10001/10001/01111/00101/01001/10001", ['П']="11111/10001/10001/10001/10001/10001/10001",
        ['А']="01110/10001/10001/11111/10001/10001/10001", ['Т']="11111/00100/00100/00100/00100/00100/00100",
        ['Ы']="10001/10001/10001/11101/10101/10101/11101", ['Л']="00111/01001/01001/01001/01001/01001/10001", ['С']="01111/10000/10000/10000/10000/10000/01111",
        // 0.1.117: the rest of the Cyrillic letters the watch labels use.
        ['Г']="11111/10000/10000/10000/10000/10000/10000", ['Д']="00110/01010/01010/01010/01010/11111/10001",
        ['Ч']="10001/10001/10001/01111/00001/00001/00001", ['Ш']="10101/10101/10101/10101/10101/10101/11111",
        ['Щ']="10101/10101/10101/10101/10101/11111/00001", ['Ю']="10010/10101/10101/11101/10101/10101/10010",
        ['Э']="11110/00001/00001/01111/00001/00001/11110", ['Ф']="00100/01110/10101/10101/10101/01110/00100",
        ['Ц']="10010/10010/10010/10010/10010/11111/00001", ['Ъ']="11000/01000/01000/01110/01001/01001/01110",
        ['Й']="01010/10001/10011/10101/11001/10001/10001"
    };
    // Cyrillic letters drawn like Latin ones.
    private static readonly Dictionary<char,char> LookAlike=new(){['В']='B',['Е']='E',['Ё']='E',['К']='K',['М']='M',['Х']='X',['У']='Y'};
    static WatchFaceGeometry()
    {
        string letters="ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string[] shapes={"01110/10001/10001/11111/10001/10001/10001","11110/10001/10001/11110/10001/10001/11110","01111/10000/10000/10000/10000/10000/01111","11110/10001/10001/10001/10001/10001/11110","11111/10000/10000/11110/10000/10000/11111","11111/10000/10000/11110/10000/10000/10000","01111/10000/10000/10111/10001/10001/01111","10001/10001/10001/11111/10001/10001/10001","01110/00100/00100/00100/00100/00100/01110","00111/00010/00010/00010/00010/10010/01100","10001/10010/10100/11000/10100/10010/10001","10000/10000/10000/10000/10000/10000/11111","10001/11011/10101/10101/10001/10001/10001","10001/11001/10101/10011/10001/10001/10001","01110/10001/10001/10001/10001/10001/01110","11110/10001/10001/11110/10000/10000/10000","01110/10001/10001/10001/10101/10010/01101","11110/10001/10001/11110/10100/10010/10001","01111/10000/10000/01110/00001/00001/11110","11111/00100/00100/00100/00100/00100/00100","10001/10001/10001/10001/10001/10001/01110","10001/10001/10001/10001/10001/01010/00100","10001/10001/10001/10101/10101/11011/10001","10001/10001/01010/00100/01010/10001/10001","10001/10001/01010/00100/00100/00100/00100","11111/00001/00010/00100/01000/10000/11111"};
        for(int i=0;i<letters.Length;i++)Glyphs[letters[i]]=shapes[i];
    }
    internal static string Clean(string? text)
    {
        if(string.IsNullOrWhiteSpace(text))return "-";
        var b=new StringBuilder();bool tag=false;
        foreach(char c in text)
        {
            if(c=='<'){tag=true;continue;}if(c=='>'){tag=false;continue;}if(tag)continue;
            if(c=='—'||c=='–')b.Append('-');else if(char.IsWhiteSpace(c))b.Append(' ');
            else
            {
                char u=char.ToUpperInvariant(c);
                if(!Glyphs.ContainsKey(u)&&LookAlike.TryGetValue(u,out var same))u=same;
                if(!Glyphs.ContainsKey(u)){var plain=u.ToString().Normalize(System.Text.NormalizationForm.FormD);if(plain.Length>0&&Glyphs.ContainsKey(plain[0]))u=plain[0];}
                b.Append(Glyphs.ContainsKey(u)?u:'?');
            }
            if(b.Length>=32)break;
        }
        return b.Length==0?"-":b.ToString().Trim();
    }
    // right: the ammunition face (else health and armour). 0.1.146: leftFirst:
    // two guns' rounds listed left hand first (a left-hander's watch).
    internal static HandMeshGeometry Build(bool right,string primary,string secondary,float health=1,float armor=1,bool leftFirst=false)
    {
        // Always use right=true for text so left wrist writing is not mirrored.
        var m=new HandMeshGeometry(true);var ink=new Vector4(.87f,.97f,1,1);
        Line(m,(right?UiLanguage.Watch(0)+(primary.Contains("/")?(leftFirst?UiLanguage.T(" Л/П"," L/R"):UiLanguage.T(" П/Л"," R/L")):""):UiLanguage.Watch(1)),.016f,.0033f,ink);
        Line(m,Clean(primary),.004f,.012f,ink);
        Line(m,right?UiLanguage.Watch(2)+" "+Clean(secondary):UiLanguage.Watch(3)+" "+Clean(secondary),-.012f,.0048f,ink);
        if(!right){Bar(m,.056f*Safe(health),-.005f,new(.7f,.74f,.66f,1));Bar(m,.056f*Safe(armor),-.019f,new(.54f,.58f,.52f,1));}
        return m;
    }
    private static float Safe(float x)=>float.IsFinite(x)?Math.Clamp(x,0,1):0;
    private static void Line(HandMeshGeometry m,string text,float z,float height,Vector4 ink)
    {
        float cell=Math.Min(height/7,.058f/Math.Max(1,text.Length*6-1));
        float start=-(text.Length*6-1)*cell*.5f;
        for(int i=0;i<text.Length;i++)
        {
            if(text[i]==' ')continue;
            string shape=Glyphs.TryGetValue(text[i],out var g)?g:Glyphs['?'];
            var rows=shape.Split('/');
            for(int row=0;row<7;row++)for(int col=0;col<5;col++)
                if(rows[row][col]=='1')Rect(m,start+(i*6+col)*cell,z+(3.5f-row)*cell,cell*.87f,cell*.87f,ink);
        }
    }
    private static void Bar(HandMeshGeometry m,float width,float z,Vector4 color)
    {if(width>.00001f)Rect(m,-.028f,z,width,.0009f,color);}
    private static void Rect(HandMeshGeometry m,float x,float z,float w,float h,Vector4 color)
    {
        float size=HandMeshGeometry.FaceSize;x*=size;z*=size;w*=size;h*=size;
        var p=HandMeshGeometry.ScreenPosition+new Vector3(x,0,z);
        // Local +Y is the outward face of the watch; reading top is +Z.
        m.Quad(p,p+new Vector3(0,0,h),p+new Vector3(w,0,h),p+new Vector3(w,0,0),color);
    }
}
