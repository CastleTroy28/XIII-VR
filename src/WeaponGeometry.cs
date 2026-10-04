using System;
using System.Numerics;
namespace XiiiXR;
internal readonly struct WeaponFit
{
    internal readonly float Scale;
    internal readonly Vector3 Translation, Muzzle;
    internal WeaponFit(float scale,Vector3 translation,Vector3 muzzle) { Scale=scale; Translation=translation; Muzzle=muzzle; }
    internal Vector3 Point(Vector3 source) => source*Scale+Translation;
}
internal static class WeaponGeometry
{
    // 0.1.228: the bazooka's scale (barrel frame to fitted): the size it had in
    // nearly every game, its rocket in the tube (1.1 m with the rocket's head
    // ahead of the tube, 0.4026 in the barrel frame). Fitted by its length, a
    // bazooka whose rocket the game held further back (a second one picked up,
    // loaded) came out a third bigger, and its handles with it (8 cm by 4.8).
    // It is now fitted without its rocket (WeaponVisual.TrimRocket) at this scale.
    internal const float BazookaScale=1.1f/.402614f;
    // scale (0.1.228): this scale instead of the profile's length over the bounds.
    internal static WeaponFit Fit(Vector3 min,Vector3 max,string profile,float scale=float.NaN)
    {
        var size = max-min;
        if (!Finite(min) || !Finite(max) || !Finite(size) || size.Z < .0001f || size.Z > 1000 || size.X <= 0 || size.Y <= 0)
            throw new InvalidOperationException("Invalid weapon geometry bounds");
        if (EquipmentProfile.RequiresMuzzle(profile) && (size.X > size.Z*2 || size.Y > size.Z*2))
            throw new InvalidOperationException("Weapon bounds are not aligned with the muzzle; refusing oversized render copy");
        float length = EquipmentProfile.Length(profile);
        if(!(float.IsFinite(scale)&&scale>0))scale = length/(EquipmentProfile.RequiresMuzzle(profile)?size.Z:Math.Max(size.X,Math.Max(size.Y,size.Z)));
        var anchor = new Vector3((min.X+max.X)*.5f,max.Y-size.Y*.22f,max.Z);
        var muzzle = profile == "pistol" ? new Vector3(0,.045f,.14f)
            : profile == "revolver" ? new Vector3(0,.045f,.18f)
            : profile == "shotgun" ? new Vector3(0,.04f,.62f) : new Vector3(0,.04f,.5f);
        return new WeaponFit(scale,muzzle-anchor*scale,muzzle);
    }
    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
    // 0.1.194:
    // a screwed-on silencer is part of the pistol's mesh (hidden on the plain
    // pistol). It made the fitted gun 20% smaller and its handle move, so the
    // silenced pistol is fitted without it: the same size and handle as the
    // plain one, the silencer ahead of the muzzle.
    internal static bool Attachment(string? bone)=>bone!=null&&(bone.Contains("silencer",StringComparison.OrdinalIgnoreCase)||bone.Contains("suppressor",StringComparison.OrdinalIgnoreCase));
    // points: mesh vertices; attached: those of the attachment. The bounds of
    // the rest, only when the attachment is shown (not collapsed to a point:
    // at least `shown` of the rest's largest size across).
    internal static bool WithoutAttachment(Vector3[] points,bool[] attached,float shown,out Vector3 min,out Vector3 max,out Vector3 attachMin,out Vector3 attachMax)
    {
        min=max=attachMin=attachMax=Vector3.Zero;
        if(points.Length==0||points.Length!=attached.Length)return false;
        bool any=false,anyAttached=false;
        for(int i=0;i<points.Length;i++)
        {
            var p=points[i];if(!Finite(p))continue;
            if(attached[i]){if(!anyAttached){attachMin=attachMax=p;anyAttached=true;}else{attachMin=Vector3.Min(attachMin,p);attachMax=Vector3.Max(attachMax,p);}}
            else{if(!any){min=max=p;any=true;}else{min=Vector3.Min(min,p);max=Vector3.Max(max,p);}}
        }
        if(!any||!anyAttached)return false;
        var rest=max-min;var extra=attachMax-attachMin;
        float largest=Math.Max(rest.X,Math.Max(rest.Y,rest.Z));
        return largest>0&&Math.Max(extra.X,Math.Max(extra.Y,extra.Z))>=largest*shown;
    }
}
