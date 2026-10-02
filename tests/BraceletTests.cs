using System;using System.Linq;using System.Collections.Generic;using System.Numerics;using XiiiXR;
class BraceletTests
{
    static void Check(bool b,string s){if(!b)throw new Exception(s);}
    static void Main()
    {
        var p=new List<Vector3>();var triangles=new List<int>();
        void Tube(float z,float length,float rx,float ry,int segments=16)
        {
            int start=p.Count;
            foreach(float end in new[]{z-length/2,z+length/2})for(int i=0;i<segments;i++)p.Add(new Vector3(rx*MathF.Cos(i*2*MathF.PI/segments),ry*MathF.Sin(i*2*MathF.PI/segments),end));
            for(int i=0;i<segments;i++){int j=(i+1)%segments;triangles.AddRange(new[]{start+i,start+j,start+i+segments,start+j,start+j+segments,start+i+segments});}
        }
        Tube(-.16f,.50f,.032f,.020f); // whole arm, reject
        Tube(-.05f,.025f,.038f,.024f); // native wrist bracelet, accept
        Tube(-.24f,.045f,.07f,.05f); // sleeve cuff, reject
        var vertices=p.ToArray();var tris=new[]{triangles.ToArray()};
        var selected=NativeBraceletMath.Select(vertices,tris,Enumerable.Repeat(true,p.Count).ToArray());
        Check(selected.Take(32).All(x=>!x)&&selected.Skip(32).Take(32).All(x=>x)&&selected.Skip(64).All(x=>!x),"bracelet selector copied skin or sleeve / lost band");
        var merged=(int[])tris[0].Clone();merged[0]=32;var joined=NativeBraceletMath.Select(vertices,new[]{merged},Enumerable.Repeat(true,p.Count).ToArray());
        Check(joined.Skip(32).Take(32).All(x=>!x),"bracelet fused to skin must not be cut out");
        // A separate crown has only six vertices and sits outside the wrist
        // centre. It must leave with the case, without eating the sleeve.
        var crown=p.ToList();var extra=triangles.ToList();int first=crown.Count;
        crown.AddRange(new[]{new Vector3(.039f,.01f,-.05f),new Vector3(.043f,.01f,-.05f),new Vector3(.043f,.015f,-.05f),new Vector3(.039f,.01f,-.054f),new Vector3(.043f,.01f,-.054f),new Vector3(.043f,.015f,-.054f)});
        extra.AddRange(new[]{first,first+1,first+2,first+1,first+4,first+5,first+1,first+5,first+2,first,first+3,first+4});
        var crownSelection=NativeBraceletMath.Select(crown.ToArray(),new[]{extra.ToArray()},Enumerable.Repeat(true,crown.Count).ToArray());
        Check(crownSelection.Skip(first).All(x=>x)&&crownSelection.Take(32).All(x=>!x),"crown remnant stays / skin was removed");
        var coords=p.Select(v=>new Vector2(v.X,v.Z)).ToArray();var cropped=NativeHandMesh.Build(vertices,coords,tris,Enumerable.Repeat(true,p.Count).ToArray(),-.35f,.14f);
        Check(cropped.Points.Count<triangles.Count,"mesh compaction left every triangle corner duplicated");
        foreach(var sub in cropped.Submeshes)foreach(int i in sub)Check(i>=0&&i<cropped.Points.Count,"compaction damaged index buffer");
        foreach(var point in cropped.Points)Check(Vector3.Distance(NativeHandMesh.Evaluate(point,vertices),point.Rest+new Vector3(0,0,NativeHandMesh.WristZ))<1e-5f,"compaction damaged UV/pose mapping");
        Console.WriteLine("PASS: original accessory selection excludes full arm/sleeve and fused geometry; cropped mesh compaction preserves indices and pose mapping.");
    }
}
