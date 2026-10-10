using System;using System.Numerics;using System.Linq;using System.IO;using System.Text.Json;using System.Collections.Generic;using XiiiXR;
class WearableTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static string Key(Vector3 p)=>$"{MathF.Round(p.X*1e6f)},{MathF.Round(p.Y*1e6f)},{MathF.Round(p.Z*1e6f)}";
    static void Main(string[] args)
    {
        var state=new ObjectivesState();state.Sample(true,true);Check(!state.Open,"held R3 opens on startup");
        state.Sample(false,true);state.Sample(true,true);Check(state.Open,"R3 press failed to open");
        state.Sample(true,true);Check(state.Open,"held R3 repeats toggle");
        state.Sample(false,true);state.Sample(true,true);Check(!state.Open,"second click failed to close");
        state.Sample(false,true);state.Sample(true,true);state.Sample(true,false);Check(!state.Open,"lock/focus loss left objectives open");
        state.Sample(true,true);Check(!state.Open,"held R3 reopens on resume");
        state.Sample(false,true);state.Sample(true,true);state.Close();state.Sample(true,true);Check(!state.Open,"Esc/chapter rebind does not require release");
        // 0.1.254: the 3D watch's model and face are checked by WatchFaceTests (the old built watch is gone).
        Check(WatchFaceGeometry.Clean("<color=#fff>12</color>")=="12","native text markup leaked into display");
        Check(WatchFaceGeometry.Clean("∞")=="∞","infinite ammunition lost");
        TestCrop();
        // 0.1.117: reload stages in every game language, drawable on the watch.
        foreach(var code in new[]{"en","ru","de","fr","es","it","pl","pt"})
            foreach(var stage in new[]{"DROP","INSERT","GRAB","POUCH","RACK","COVER","GL","CHEST"})
            {
                string word=UiLanguage.L(stage,code),shown=WatchFaceGeometry.Clean(word);
                if(code!="en"&&word==stage)throw new Exception("reload stage "+stage+" not translated for "+code);
                if(shown.Contains('?')||shown.Length<(stage=="GL"?1:3))throw new Exception("watch cannot draw "+code+" "+word+" -> "+shown);
            }
        if(WatchFaceGeometry.Clean("Вставь ё")!="BСТАBЬ E"||WatchFaceGeometry.Clean("Réserve")!="RESERVE")throw new Exception("watch look-alikes/accents: "+WatchFaceGeometry.Clean("Вставь ё")+" "+WatchFaceGeometry.Clean("Réserve"));
        Console.WriteLine("PASS: reload stages (drop/insert/grab/belt/bolt) translated for all 7 game languages and drawable on the watch; Cyrillic look-alikes and accents folded.");
        Console.WriteLine("PASS: R3 rearm; watch text markup and infinity; native mesh crop/UV/cap/ownership/cuff stabilization.");
        Console.WriteLine("Geometry only; original game mesh/material rendering and Pimax fit require in-game validation.");
    }
    static void TestCrop()
    {
        // Synthetic source mesh: two separate arm islands, one crossing wrist plane.
        var points=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        void Box(float x)
        {
            int start=points.Count;
            foreach(float z in new[]{-.16f,.15f})foreach(var p in new[]{new Vector2(-.03f,-.02f),new Vector2(.03f,-.02f),new Vector2(.03f,.02f),new Vector2(-.03f,.02f)}){points.Add(new Vector3(x+p.X,p.Y,z));uv.Add(new Vector2(p.X,z));}
            int[] t={0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7};tris.AddRange(t.Select(i=>i+start));
        }
        Box(0);Box(.28f);
        var array=points.ToArray();var triangles=new[]{tris.ToArray()};
        var own=NativeHandMesh.ConnectedOwnership(array,triangles,Vector3.Zero,new Vector3(.28f,0,0));
        Check(own.Take(8).All(x=>x)&&own.Skip(8).All(x=>!x),"opposite hand island was copied");
        var m=NativeHandMesh.Build(array,uv.ToArray(),triangles,own);
        Check(m.Points.Any(x=>x.Cap),"wrist cut has no cap");
        foreach(var p in m.Points)
        {
            Check(p.Rest.Z>=NativeHandMesh.Cut-1e-6f&&Math.Abs(p.Rest.X)<=.031f,"native forearm/opposite hand survived crop");
            Check(Math.Abs(p.Mix.X+p.Mix.Y+p.Mix.Z-1)<1e-6f,"clipped interpolation not affine");
            // 0.1.250: the cap has one texture point all over (a rim point's own), the rest their native ones.
            var texturedAt=p.Cap&&m.CapSource.HasValue?m.CapSource.Value:p;
            Check(Vector2.Distance(p.UV,new Vector2(texturedAt.Rest.X,texturedAt.Rest.Z))<1e-6f,"native texture interpolation damaged");
            var posed=NativeHandMesh.Evaluate(p,array);Check(Vector3.Distance(posed,p.Rest+new Vector3(0,0,NativeHandMesh.WristZ))<1e-6f,"rest geometry moved unexpectedly");
        }
        var bent=array.Select(p=>p+new Vector3(.01f,.02f,0)).ToArray();
        foreach(var p in m.Points.Where(x=>x.Cap))Check(Vector3.Distance(NativeHandMesh.Evaluate(p,bent),p.Rest+new Vector3(0,0,NativeHandMesh.WristZ))<1e-6f,"game elbow motion moved cut cuff");
        Check(m.Points.Where(p=>p.Rest.Z>.04f).Any(p=>Vector3.Distance(NativeHandMesh.Evaluate(p,bent),NativeHandMesh.Evaluate(p,array))>.01f),"native finger animation removed");
        foreach(var sub in m.Submeshes)for(int i=0;i<sub.Length;i+=3)
            if(m.Points[sub[i]].Cap&&m.Points[sub[i+1]].Cap&&m.Points[sub[i+2]].Cap)
                Check(Vector3.Cross(m.Points[sub[i+1]].Rest-m.Points[sub[i]].Rest,m.Points[sub[i+2]].Rest-m.Points[sub[i]].Rest).Z<0,"wrist cap faces inward");
    }
}
