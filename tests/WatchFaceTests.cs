using System;using System.IO;using System.Linq;using System.Numerics;using System.Collections.Generic;using XiiiXR;
// 0.1.254: the 3D watch (the classic XIII mod's model and face) - what it shows read from the HUD's texts, the
// face drawn in every game language, and the model placed on each wrist so it is read like a watch.
class WatchFaceTests
{
    static int checks;
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
    static (int olive,int ink,int dark) Count(byte[] px,int y0,int y1,int x0=0,int x1=WatchFacePixels.Width)
    {
        int olive=0,ink=0,dark=0;
        for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)
        {
            int p=(y*WatchFacePixels.Width+x)*4;int r=px[p],g=px[p+1],b=px[p+2];
            if(g>150&&r>140&&b<g-10)olive++;else if(g<80&&r<80&&b<80&&Math.Abs(r-g)<20&&g>=r)ink++;
            if(r<60&&g<60&&b<60)dark++;
        }
        return(olive,ink,dark);
    }
    static void Main(string[] args)
    {
        UiLanguage.ReadCode=()=>"en";
        // ---- the readings ----
        var r=WatchFacePixels.Read(false,"100","75");Check(!r.Ammo&&r.Top==100&&r.Bottom==75,"health and armour not read");
        r=WatchFacePixels.Read(false,"<b>42</b>","-");Check(r.Top==42&&r.Bottom==-1,"markup or a missing armour");
        r=WatchFacePixels.Read(true,"10","120");Check(r.Ammo&&r.Top==10&&r.Bottom==120&&!r.Pair&&!r.Stage&&r.Grenades<0,"rounds and the rest");
        r=WatchFacePixels.Read(true,"12 / 15","120");Check(r.Pair&&r.Top==12&&r.Bottom==15,"a pair of pistols (the game's)");
        r=WatchFacePixels.Read(true,"7/-","30/-",true);Check(r.Pair&&r.Top==7&&r.Bottom==-1&&r.LeftFirst,"a gun in each hand (a left-hander's)");
        r=WatchFacePixels.Read(true,"30 G3","90");Check(r.Top==30&&r.Bottom==90&&r.Grenades==3,"the M16's grenades");
        r=WatchFacePixels.Read(true,"30 Г2","90");Check(r.Top==30&&r.Grenades==2,"the M16's grenades (Russian)");
        r=WatchFacePixels.Read(true,"6","ВСТАВЬ");Check(r.Top==6&&r.Stage&&r.BottomWord=="BСТАBЬ","a reload step");
        r=WatchFacePixels.Read(true,"8","6 / 40");Check(r.Bottom==40&&!r.Pair,"a second count before the rest");
        r=WatchFacePixels.Read(true,"∞","∞");Check(r.Top==-1&&r.TopWord=="∞"&&r.BottomWord=="∞","infinite ammunition");
        r=WatchFacePixels.Read(true,"-","-");Check(r.Top==-1&&r.Bottom==-1&&r.TopWord==""&&r.BottomWord=="","no weapon");
        r=WatchFacePixels.Read(true,"1234","5");Check(r.Top==1234,"a large count");
        // ---- the faces ----
        string outDir=Path.Combine("xiii-xr","build");bool preview=args.Contains("--preview");
        var stages=new[]{"DROP","INSERT","GRAB","POUCH","RACK","COVER","CHEST","OPEN","CLOSE"};
        foreach(var code in WatchFacePixels.LanguageCodes)
        {
            UiLanguage.ReadCode=()=>code;
            foreach(WatchFacePixels.Label label in Enum.GetValues(typeof(WatchFacePixels.Label)))
            {var l=WatchFacePixels.LabelOf(code,label);Check(l.Length>0&&!WatchFaceGeometry.Clean(l).Contains('?'),"label "+label+" in "+code+" not drawable: "+l);}
            var health=WatchFacePixels.Draw(WatchFacePixels.Read(false,"100","75"),code);
            var ammo=WatchFacePixels.Draw(WatchFacePixels.Read(true,"30 "+UiLanguage.L("GL",code)+"3","120"),code);
            foreach(var px in new[]{health,ammo})
            {
                Check(px.Length==WatchFacePixels.Width*WatchFacePixels.Height*4&&Enumerable.Range(0,px.Length/4).All(i=>px[i*4+3]==255),"face not opaque RGBA");
                var top=Count(px,16,90);var bottom=Count(px,102,176);
                Check(top.olive>5000&&bottom.olive>5000,code+": the LCD's screen not seen ("+top.olive+", "+bottom.olive+")");
                Check(top.ink>1500&&bottom.ink>1500,code+": the numbers and icons not drawn ("+top.ink+", "+bottom.ink+")");
                Check(Count(px,0,4).dark>3*WatchFacePixels.Width,"the case round the screen");
            }
            foreach(var stage in stages)
            {
                var px=WatchFacePixels.Draw(WatchFacePixels.Read(true,"6",UiLanguage.L(stage,code)),code);
                var lower=Count(px,102,176);
                Check(lower.ink>400,code+": the reload step "+stage+" not written ("+lower.ink+")");
            }
            if(preview)
            {
                Png(Path.Combine(outDir,"watch3d-face-"+code+"-health.png"),health);Png(Path.Combine(outDir,"watch3d-face-"+code+"-ammo.png"),ammo);
                Png(Path.Combine(outDir,"watch3d-face-"+code+"-pair.png"),WatchFacePixels.Draw(WatchFacePixels.Read(true,"12 / 15","40"),code));
                Png(Path.Combine(outDir,"watch3d-face-"+code+"-stage.png"),WatchFacePixels.Draw(WatchFacePixels.Read(true,"0",UiLanguage.L("INSERT",code)),code));
            }
        }
        UiLanguage.ReadCode=()=>"en";
        // the unlit segments faint, the lit ones dark: 888 has more ink than 111; dashes for nothing
        int Ink(WatchReading w)=>Count(WatchFacePixels.Draw(w,"en"),16,90,110,240).ink;
        Check(Ink(WatchFacePixels.Read(true,"888","1"))>Ink(WatchFacePixels.Read(true,"111","1"))+800,"seven segments not lit by the digits");
        Check(Ink(WatchFacePixels.Read(true,"-","1"))<Ink(WatchFacePixels.Read(true,"111","1")),"dashes for no count");
        // the pair: the other hand's letter, no row of three cartridges
        var pairPx=WatchFacePixels.Draw(WatchFacePixels.Read(true,"12 / 15","40"),"en");var singlePx=WatchFacePixels.Draw(WatchFacePixels.Read(true,"12","40"),"en");
        Check(Count(pairPx,102,176,16,64).ink!=Count(singlePx,102,176,16,64).ink,"a pair drawn as one gun");
        // the parts that stay drawn once a layout: a shot draws only the numbers (and the same picture as before)
        var first=WatchFacePixels.Draw(WatchFacePixels.Read(true,"30","90"),"en");int bases=WatchFacePixels.BasesDrawn;
        var clock=System.Diagnostics.Stopwatch.StartNew();
        for(int i=0;i<60;i++)WatchFacePixels.Draw(WatchFacePixels.Read(true,(29-i%30).ToString(),"90"),"en");
        double ms=clock.Elapsed.TotalMilliseconds/60;
        Check(WatchFacePixels.BasesDrawn==bases,"the face's lasting parts drawn again for each shot");
        Check(WatchFacePixels.Draw(WatchFacePixels.Read(true,"30","90"),"en").SequenceEqual(first),"the same reading drawn otherwise");
        Check(ms<20,"a face drawn in "+ms.ToString("F2")+" ms");
        Console.WriteLine("a shot's face drawn in "+ms.ToString("F2")+" ms (its lasting parts once a layout)");
        // Unity's bottom row first
        var flipped=WatchFacePixels.BottomUp(singlePx);
        Check(flipped.Take(WatchFacePixels.Width*4).SequenceEqual(singlePx.Skip((WatchFacePixels.Height-1)*WatchFacePixels.Width*4).Take(WatchFacePixels.Width*4)),"the picture not stored bottom row first");
        // ---- the model on the wrists ----
        Check(WatchModel.Position.Length==WatchModel.Points*3&&WatchModel.Normal.Length==WatchModel.Points*3&&WatchModel.Tone.Length==WatchModel.Points&&WatchModel.Triangle.Length==WatchModel.Triangles*3,"the watch's data sizes");
        Check(WatchModel.Triangle.All(i=>i<WatchModel.Points)&&WatchModel.Tone.All(t=>t*3<WatchModel.Palette.Length),"the watch's indices");
        Check(WatchModelMath.HalfLength>.6f&&WatchModelMath.HalfLength<.7f,"the case's length");
        foreach(bool right in new[]{false,true})foreach(var (rx,ry) in new[]{(.025f,.026f),(.034f,.038f),(.036f,.030f)})
        {
            var s=WatchModelMath.Build(right,rx,ry);string side=right?"right":"left";
            int mixed=Enumerable.Range(0,WatchModel.Triangles).Count(t=>!(WatchModel.Tone[WatchModel.Triangle[t*3]]==WatchModel.Tone[WatchModel.Triangle[t*3+1]]&&WatchModel.Tone[WatchModel.Triangle[t*3+1]]==WatchModel.Tone[WatchModel.Triangle[t*3+2]]));
            Check(mixed>0&&s.Tones.Sum(t=>t.Length)==((WatchModel.Triangles-mixed)+mixed*WatchModelMath.Cuts*WatchModelMath.Cuts)*3&&s.Palette.Length==4,side+": every triangle in one tone (those between tones cut)");
            Check(s.Points.Length<65000&&s.Tones.All(t=>t.All(i=>i<s.Points.Length)),side+": the cut triangles' points");
            // the strap round the wrist's section, nothing deep inside it
            float inside=float.MaxValue,along=0;
            for(int i=0;i<s.Points.Length;i++)
            {
                var q=s.Points[i]-WatchModelMath.Centre;along=Math.Max(along,Math.Abs(q.Z));
                if(i<WatchModel.Points&&Math.Abs(WatchModel.Position[i*3])<.35f)inside=Math.Min(inside,q.X*q.X/(rx*rx)+q.Y*q.Y/(ry*ry));
                Check(float.IsFinite(s.Normals[i].X)&&Math.Abs(s.Normals[i].Length()-1)<1e-3f,side+": a normal not of unit length");
            }
            Check(inside>.72f,side+": the strap cuts into the wrist ("+inside+")");
            Check(along<=WatchModelMath.HalfLength*rx+1e-6f&&along>WatchModelMath.HalfLength*rx*.95f,side+": the case's length along the forearm");
            // the faces outward (the triangles' turn as their normals: Unity draws the clockwise side, the cross's side)
            int agree=0;foreach(var tone in s.Tones)for(int t=0;t<tone.Length;t+=3)
            {var a=s.Points[tone[t]];var n=s.Normals[tone[t]]+s.Normals[tone[t+1]]+s.Normals[tone[t+2]];if(Vector3.Dot(Vector3.Cross(s.Points[tone[t+1]]-a,s.Points[tone[t+2]]-a),n)>0)agree++;}
            int all=s.Tones.Sum(t=>t.Length)/3;Check(agree>all*.97f,side+": triangles turned inward ("+agree+" of "+all+")");
            // the face on the back of the wrist, just over the case's screen, facing out
            Check(s.FaceNormal==Vector3.UnitY&&Math.Abs(s.FaceCentre.Y-WatchModelMath.Centre.Y-(WatchModel.Face[0]+WatchModelMath.FaceLift)*ry)<1e-6f,side+": the face's height");
            var f=s.Face;var fi=WatchModelMath.FaceTriangles;
            for(int t=0;t<6;t+=3)Check(Vector3.Cross(f[fi[t+1]]-f[fi[t]],f[fi[t+2]]-f[fi[t]]).Y>0,side+": the face turned inward");
            Check(s.FaceUv[0]==new Vector2(0,1)&&s.FaceUv[2]==new Vector2(1,0),side+": the face's picture upside down");
            Check(Vector3.Dot(Vector3.Normalize(f[1]-f[0]),s.ReadAlong)>.9999f&&Vector3.Dot(Vector3.Normalize(f[0]-f[3]),s.ReadUp)>.9999f,side+": the picture's rows and top");
            // read like a watch: the forearm across before the chest, the back of the wrist toward the eyes (seen
            // looking along +Z, Unity's right +X, up +Y): the rows to the right, the top up
            var forward=right?-Vector3.UnitX:Vector3.UnitX;var up=-Vector3.UnitZ;
            var x=Vector3.Normalize(Vector3.Cross(up,forward));   // as WristFitMath builds the wrist's axes
            var frame=Quaternion.CreateFromRotationMatrix(new Matrix4x4(x.X,x.Y,x.Z,0,up.X,up.Y,up.Z,0,forward.X,forward.Y,forward.Z,0,0,0,0,1));
            var rows=Vector3.Transform(s.ReadAlong,frame);var top=Vector3.Transform(s.ReadUp,frame);var outward=Vector3.Transform(s.FaceNormal,frame);
            Check(Vector3.Dot(rows,Vector3.UnitX)>.999f&&Vector3.Dot(top,Vector3.UnitY)>.999f&&Vector3.Dot(outward,-Vector3.UnitZ)>.999f,side+": the face not read left to right, top up (rows "+rows+", top "+top+")");
            if(preview&&rx==.034f)
            {
                var json=System.Text.Json.JsonSerializer.Serialize(new{points=s.Points.Select(p=>new[]{p.X,p.Y,p.Z}),tones=s.Tones,palette=s.Palette.Select(c=>new[]{c.X,c.Y,c.Z}),face=s.Face.Select(p=>new[]{p.X,p.Y,p.Z}),frame=new[]{frame.X,frame.Y,frame.Z,frame.W},centre=new[]{WatchModelMath.Centre.X,WatchModelMath.Centre.Y,WatchModelMath.Centre.Z},rx,ry});
                File.WriteAllText(Path.Combine(outDir,"watch3d-"+side+".json"),json);
            }
        }
        Console.WriteLine("PASS: "+checks+" checks: the 3D watch's readings (health, armour, rounds and the rest, a gun in each hand, the M16's grenades, reload steps, infinity, nothing); its face in all 8 languages (labels drawable, the screen, the numbers in seven segments, the steps written); the model on each wrist (strap round the section, outward faces, the case within its length, the face on the back of the wrist read left to right, top up, with the arm before the chest).");
        Console.WriteLine("Pixels and geometry only; how the watch looks in the headset needs an in-game check.");
    }
    static void Png(string path,byte[] topDown)
    {
        int w=WatchFacePixels.Width,h=WatchFacePixels.Height;var rgb=new byte[w*h*3];var up=WatchFacePixels.BottomUp(topDown);
        for(int i=0;i<w*h;i++){rgb[i*3]=up[i*4];rgb[i*3+1]=up[i*4+1];rgb[i*3+2]=up[i*4+2];}
        CapturePng.Write(path,w,h,rgb);
    }
}
