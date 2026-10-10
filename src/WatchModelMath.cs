using System;
using System.Collections.Generic;
using System.Numerics;
namespace XiiiXR;
// 0.1.254: the 3D watch (WatchModel, the classic XIII mod's) on a wrist, in the wearable's axes (the watch
// root of GloveVisual, placed by WatchMountMath.BandPose): X across the wrist, Y out of its back (the face),
// Z along the forearm toward the hand; the strap's middle at Centre. The watch's own axes (X along the
// forearm, Y out of the face, Z along the strap) go to: Y up; X toward the hand on the left wrist and toward
// the elbow on the right, so its face is read across the forearm with the arm before the chest (the rows
// along it, the top away from the body); its strap round the wrist's fitted section (X and Z scaled by its
// width radius, Y by its height radius: the watch in its own proportions, the strap just clear of the skin).
internal readonly struct WatchShape
{
    internal readonly Vector3[] Points,Normals;
    internal readonly int[][] Tones;          // the triangles of each palette tone
    internal readonly Vector4[] Palette;      // 0..1
    internal readonly Vector3[] Face;         // the face's quad: top left, top right, bottom right, bottom left
    internal readonly Vector2[] FaceUv;       // for a picture stored bottom row first (as Unity keeps textures)
    internal readonly Vector3 FaceCentre,FaceNormal,ReadAlong,ReadUp;
    internal WatchShape(Vector3[] points,Vector3[] normals,int[][] tones,Vector4[] palette,Vector3[] face,Vector2[] faceUv,Vector3 centre,Vector3 normal,Vector3 along,Vector3 up)
    {Points=points;Normals=normals;Tones=tones;Palette=palette;Face=face;FaceUv=faceUv;FaceCentre=centre;FaceNormal=normal;ReadAlong=along;ReadUp=up;}
}
internal static class WatchModelMath
{
    // The strap's middle in the wearable's axes (WatchMountMath.BandPose puts it at the wrist's section).
    internal static readonly Vector3 Centre=new(0,-.008f,-.11f);
    // The face a hair over the case's screen; its quad a little wider than the screen.
    internal const float FaceLift=.04f,FaceMargin=.06f;
    private static float halfLength=-1;
    // The case each way from its middle along the forearm (the watch's units).
    internal static float HalfLength
    {
        get
        {
            if(halfLength<0){float m=0;for(int i=0;i<WatchModel.Points;i++)m=Math.Max(m,Math.Abs(WatchModel.Position[i*3]));halfLength=m;}
            return halfLength;
        }
    }
    // The watch's axes in the wearable's: along the forearm (its X), out of the face (Y), along the strap (Z).
    internal static (Vector3 x,Vector3 y,Vector3 z) Axes(bool rightWrist)=>rightWrist
        ?(new Vector3(0,0,-1),Vector3.UnitY,Vector3.UnitX)
        :(Vector3.UnitZ,Vector3.UnitY,new Vector3(-1,0,0));
    internal static Vector3 Place(Vector3 model,bool rightWrist,float radiusX,float radiusY)
    {
        var (x,y,z)=Axes(rightWrist);
        return Centre+x*(model.X*radiusX)+y*(model.Y*radiusY)+z*(model.Z*radiusX);
    }
    private static Vector3 Model(int i)=>new(WatchModel.Position[i*3],WatchModel.Position[i*3+1],WatchModel.Position[i*3+2]);
    internal static Vector3 FaceCentre(bool rightWrist,float radiusX,float radiusY)
    {
        var f=WatchModel.Face;
        return Place(new Vector3((f[1]+f[2])/2,f[0]+FaceLift,(f[3]+f[4])/2),rightWrist,radiusX,radiusY);
    }
    internal static WatchShape Build(bool rightWrist,float radiusX,float radiusY)
    {
        if(!(radiusX>0)||!(radiusY>0)||!float.IsFinite(radiusX)||!float.IsFinite(radiusY))throw new ArgumentException("Invalid wrist radii");
        var (ax,ay,az)=Axes(rightWrist);
        var points=new Vector3[WatchModel.Points];var normals=new Vector3[WatchModel.Points];
        for(int i=0;i<points.Length;i++)
        {
            points[i]=Place(Model(i),rightWrist,radiusX,radiusY);
            // Normals through the scale's inverse (the shape is stretched unevenly).
            var n=new Vector3(WatchModel.Normal[i*3]/radiusX,WatchModel.Normal[i*3+1]/radiusY,WatchModel.Normal[i*3+2]/radiusX);
            var w=ax*n.X+ay*n.Y+az*n.Z;
            normals[i]=w.LengthSquared()>1e-12f?Vector3.Normalize(w):ay;
        }
        int tones=WatchModel.Palette.Length/3;
        var lists=new List<int>[tones];for(int t=0;t<tones;t++)lists[t]=new List<int>();
        var pointList=new List<Vector3>(points);var normalList=new List<Vector3>(normals);
        var edges=new Dictionary<long,int>();
        for(int t=0;t<WatchModel.Triangles;t++)
        {
            int a=WatchModel.Triangle[t*3],b=WatchModel.Triangle[t*3+1],c=WatchModel.Triangle[t*3+2];
            int ta=WatchModel.Tone[a],tb=WatchModel.Tone[b],tc=WatchModel.Tone[c];
            if(ta==tb&&tb==tc){lists[ta].Add(a);lists[ta].Add(b);lists[ta].Add(c);continue;}
            // One material a tone: a triangle between tones cut into 16, each in the tone nearest the colour
            // there (the classic mod's colours blend across it), its edges' points shared with its neighbours.
            Subdivide(a,b,c,pointList,normalList,edges,lists);
        }
        var palette=new Vector4[tones];
        for(int t=0;t<tones;t++)palette[t]=new Vector4(WatchModel.Palette[t*3]/255f,WatchModel.Palette[t*3+1]/255f,WatchModel.Palette[t*3+2]/255f,1);
        var f=WatchModel.Face;
        float y=f[0]+FaceLift,x0=f[1]-FaceMargin,x1=f[2]+FaceMargin,z0=f[3]-FaceMargin,z1=f[4]+FaceMargin;
        // The picture's top along the watch's +Z, its rows along +X (as the classic mod draws it).
        var face=new[]{Place(new(x0,y,z1),rightWrist,radiusX,radiusY),Place(new(x1,y,z1),rightWrist,radiusX,radiusY),Place(new(x1,y,z0),rightWrist,radiusX,radiusY),Place(new(x0,y,z0),rightWrist,radiusX,radiusY)};
        var uv=new[]{new Vector2(0,1),new Vector2(1,1),new Vector2(1,0),new Vector2(0,0)};
        var tri=new int[tones][];for(int t=0;t<tones;t++)tri[t]=lists[t].ToArray();
        return new WatchShape(pointList.ToArray(),normalList.ToArray(),tri,palette,face,uv,FaceCentre(rightWrist,radiusX,radiusY),ay,ax,az);
    }
    internal const int Cuts=4;
    private static Vector3 ToneColour(int point){int t=WatchModel.Tone[point]*3;return new Vector3(WatchModel.Palette[t],WatchModel.Palette[t+1],WatchModel.Palette[t+2]);}
    private static int NearestTone(Vector3 colour)
    {
        int best=0;float bestD=float.MaxValue;
        for(int t=0;t*3<WatchModel.Palette.Length;t++)
        {float d=Vector3.DistanceSquared(colour,new Vector3(WatchModel.Palette[t*3],WatchModel.Palette[t*3+1],WatchModel.Palette[t*3+2]));if(d<bestD){bestD=d;best=t;}}
        return best;
    }
    private static void Subdivide(int a,int b,int c,List<Vector3> points,List<Vector3> normals,Dictionary<long,int> edges,List<int>[] lists)
    {
        const int n=Cuts;
        var grid=new int[n+1,n+1];
        Vector3 Pos(float u,float v)=>points[a]+(points[b]-points[a])*u+(points[c]-points[a])*v;
        Vector3 Nrm(float u,float v){var m=normals[a]*(1-u-v)+normals[b]*u+normals[c]*v;return m.LengthSquared()>1e-12f?Vector3.Normalize(m):normals[a];}
        int EdgePoint(int p,int q,int k)
        {
            // the k-th of the n-1 points from p to q, kept by the edge (smaller end first)
            if(p>q){(p,q)=(q,p);k=n-k;}
            long key=((long)p*65536+q)*16+k;
            if(edges.TryGetValue(key,out int i))return i;
            float t=(float)k/n;var m=normals[p]*(1-t)+normals[q]*t;
            points.Add(points[p]+(points[q]-points[p])*t);normals.Add(m.LengthSquared()>1e-12f?Vector3.Normalize(m):normals[p]);
            edges[key]=points.Count-1;return points.Count-1;
        }
        for(int i=0;i<=n;i++)for(int j=0;i+j<=n;j++)
        {
            int index;
            if(i==0&&j==0)index=a;else if(i==n)index=b;else if(j==n)index=c;
            else if(j==0)index=EdgePoint(a,b,i);else if(i==0)index=EdgePoint(a,c,j);else if(i+j==n)index=EdgePoint(b,c,j);
            else{points.Add(Pos((float)i/n,(float)j/n));normals.Add(Nrm((float)i/n,(float)j/n));index=points.Count-1;}
            grid[i,j]=index;
        }
        Vector3 ca=ToneColour(a),cb=ToneColour(b),cc=ToneColour(c);
        void Add(int p,int q,int r,float u,float v)
        {
            int tone=NearestTone(ca*(1-u-v)+cb*u+cc*v);
            lists[tone].Add(p);lists[tone].Add(q);lists[tone].Add(r);
        }
        for(int i=0;i<n;i++)for(int j=0;i+j<n;j++)
        {
            Add(grid[i,j],grid[i+1,j],grid[i,j+1],(i+1/3f)/n,(j+1/3f)/n);
            if(i+j<n-1)Add(grid[i+1,j],grid[i+1,j+1],grid[i,j+1],(i+2/3f)/n,(j+2/3f)/n);
        }
    }
    // The face's quad as two triangles (front faces toward +Y, as the model's own).
    internal static readonly int[] FaceTriangles={0,1,2,0,2,3};
}
