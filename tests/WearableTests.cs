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
        foreach(bool right in new[]{false,true})
        {
            var m=HandMeshGeometry.BuildWatch(right);var edges=new Dictionary<string,int>();double volume=0;bool bottom=false;
            for(int i=0;i<m.Triangles.Count;i+=3)
            {
                var a=m.Vertices[m.Triangles[i]];var b=m.Vertices[m.Triangles[i+1]];var c=m.Vertices[m.Triangles[i+2]];
                var normal=Vector3.Cross(b-a,c-a);Check(normal.LengthSquared()>1e-18f,"degenerate watch triangle");
                volume+=Vector3.Dot(a,Vector3.Cross(b,c))/6;
                foreach(var pair in new[]{(a,b),(b,c),(c,a)}){var keys=new[]{Key(pair.Item1),Key(pair.Item2)};Array.Sort(keys,StringComparer.Ordinal);var key=string.Join(";",keys);edges[key]=edges.GetValueOrDefault(key)+1;}
                if(Math.Abs(a.Y-.0345f)<1e-6 && Math.Abs(b.Y-.0345f)<1e-6 && Math.Abs(c.Y-.0345f)<1e-6){Check(normal.Y<0,"back plate faces inward");bottom=true;}
            }
            Check(edges.Values.All(x=>x==2),"watch has an open boundary/nonmanifold edge");Check(bottom&&volume>0,"watch shell or underside missing");
            foreach(bool nativeBand in new[]{true,false}){
                var band=HandMeshGeometry.BuildBand(right,.03f,.04f,nativeBand);var bandEdges=new Dictionary<string,int>();
                for(int k=0;k<band.Triangles.Count;k+=3){var a=band.Vertices[k];var b=band.Vertices[k+1];var c=band.Vertices[k+2];
                    foreach(var pair in new[]{(a,b),(b,c),(c,a)}){var keys=new[]{Key(pair.Item1),Key(pair.Item2)};Array.Sort(keys,StringComparer.Ordinal);var key=string.Join(";",keys);bandEdges[key]=bandEdges.GetValueOrDefault(key)+1;}}
                Check(bandEdges.Values.All(x=>x==2),"strap bridge has open end caps");
            }
            foreach(var pair in new[]{("10","120"),("100","75"),("<b>6</b>","20 / 400"),("∞","—"),(new string('9',32),"  ")})
            {
                var face=WatchFaceGeometry.Build(right,pair.Item1,pair.Item2,float.NaN,2);
                Check(face.Vertices.Count>0,"empty watch face");
                foreach(var p in face.Vertices)
                {
                    var q=p-HandMeshGeometry.ScreenPosition;
                    Check(NativeHandMesh.Finite(p)&&Math.Abs(q.X)<.0321f*HandMeshGeometry.FaceSize&&Math.Abs(q.Z)<.0221f*HandMeshGeometry.FaceSize&&Math.Abs(q.Y)<1e-6,"digits escaped physical display bounds");
                }
                for(int i=0;i<face.Triangles.Count;i+=3){var a=face.Vertices[face.Triangles[i]];var b=face.Vertices[face.Triangles[i+1]];var c=face.Vertices[face.Triangles[i+2]];Check(Vector3.Cross(b-a,c-a).Y>0,"display faces inside case");}
            }
        }
        Check(WatchFaceGeometry.Clean("<color=#fff>12</color>")=="12","native text markup leaked into display");
        Check(WatchFaceGeometry.Clean("∞")=="∞","infinite ammunition lost");
        TestCrop();
        if(args.Contains("--preview"))
        {
            var watch=HandMeshGeometry.BuildWatch(true);var face=WatchFaceGeometry.Build(true,"10","120");
            for(int i=0;i<face.Vertices.Count;i++)face.Vertices[i]+=Vector3.UnitY*.0025f;
            File.WriteAllText("xiii-xr/build/watch-preview.json",JsonSerializer.Serialize(new{vertices=watch.Vertices,colors=watch.Colors,triangles=watch.Triangles,face=face.Vertices,faceColors=face.Colors,faceTriangles=face.Triangles},new JsonSerializerOptions{IncludeFields=true}));
        }
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
        Console.WriteLine("PASS: R3 rearm; watch shells closed/manifold on both sides; outward back plate; physical digit bounds, winding and markup; native mesh crop/UV/cap/ownership/cuff stabilization.");
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
            Check(Vector2.Distance(p.UV,new Vector2(p.Rest.X,p.Rest.Z))<1e-6f,"native texture interpolation damaged");
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
