using System;
using System.Collections.Generic;
namespace XiiiXR;
// 0.1.215: a controller drawn in the hint with the button to press lit up
// (many players do not know what "Grip" is). A generic VR controller (the
// Quest / Pico / Pimax kind) seen from its face: tracking head with the stick
// and two buttons, the trigger at its front, the grip on the handle; the left
// controller is the mirror image. Drawn in the game's comic style: a white
// body with a thick black outline, the button yellow with a glow.
[Flags]
internal enum IconParts{None=0,Grip=1,Trigger=2,Lower=4,Upper=8,Stick=16}
// Lower: A on the right controller, X on the left; Upper: B / Y.
internal enum IconSide{Right,Left,Either}
internal readonly record struct IconSpec(IconSide Side,IconParts Parts)
{
    internal string Key=>Side+":"+(int)Parts;
}
internal static class ControllerIconMath
{
    // The English label a hint shows -> the controller and button drawn
    // beside it (none for gestures and multi-step hints).
    private static readonly Dictionary<string,IconSpec> labels=new(StringComparer.Ordinal)
    {
        ["Left/Right Grip"]=new(IconSide.Either,IconParts.Grip),
        ["Grip + swing"]=new(IconSide.Either,IconParts.Grip),
        ["Fist in the back, swing hard"]=new(IconSide.Either,IconParts.Grip),
        ["Grip: hold · swing and let go: throw"]=new(IconSide.Either,IconParts.Grip),
        ["Both Grips"]=new(IconSide.Either,IconParts.Grip),
        ["Release both Grips"]=new(IconSide.Either,IconParts.Grip),
        ["Left Grip"]=new(IconSide.Left,IconParts.Grip),
        ["Hold left Grip"]=new(IconSide.Left,IconParts.Grip),
        ["Release left Grip"]=new(IconSide.Left,IconParts.Grip),
        ["Left Grip: support weapon"]=new(IconSide.Left,IconParts.Grip),
        ["Right Grip"]=new(IconSide.Right,IconParts.Grip),
        ["Hold right Grip"]=new(IconSide.Right,IconParts.Grip),
        ["Right Grip: support weapon"]=new(IconSide.Right,IconParts.Grip),
        ["Right Grip + A"]=new(IconSide.Right,IconParts.Grip|IconParts.Lower),
        ["Left Grip + X"]=new(IconSide.Left,IconParts.Grip|IconParts.Lower),
        ["Right stick click"]=new(IconSide.Right,IconParts.Stick),
        ["Left stick click"]=new(IconSide.Left,IconParts.Stick),
        ["Right stick"]=new(IconSide.Right,IconParts.Stick),
        ["Right stick up"]=new(IconSide.Right,IconParts.Stick),
        ["Right stick down"]=new(IconSide.Right,IconParts.Stick),
        ["Right stick up/down"]=new(IconSide.Right,IconParts.Stick),
        ["Left stick"]=new(IconSide.Left,IconParts.Stick),
        ["Left stick up"]=new(IconSide.Left,IconParts.Stick),
        ["Left stick down"]=new(IconSide.Left,IconParts.Stick),
        ["Left stick up/down"]=new(IconSide.Left,IconParts.Stick),
        ["Left stick or arm strokes"]=new(IconSide.Left,IconParts.Stick),
        ["Right trigger"]=new(IconSide.Right,IconParts.Trigger),
        ["Right trigger (hook in right hand)"]=new(IconSide.Right,IconParts.Trigger),
        ["Left trigger"]=new(IconSide.Left,IconParts.Trigger),
        ["Left trigger (hook in left hand)"]=new(IconSide.Left,IconParts.Trigger),
        ["Left trigger while holding the fore-end"]=new(IconSide.Left,IconParts.Trigger),
        ["Right A"]=new(IconSide.Right,IconParts.Lower),
        ["Hold right A"]=new(IconSide.Right,IconParts.Lower),
        ["Right B"]=new(IconSide.Right,IconParts.Upper),
        ["Left X"]=new(IconSide.Left,IconParts.Lower),
        ["Left Y"]=new(IconSide.Left,IconParts.Upper),
    };
    internal static IconSpec? For(string english)=>english!=null&&labels.TryGetValue(english,out var spec)?spec:null;
    internal const int Size=128;
    // Colours (RGBA).
    internal static readonly (byte r,byte g,byte b) Body=(238,236,228),Ink=(16,16,18),Detail=(70,72,78),Lit=(255,206,32),LitInk=(150,70,0);
    // The shapes, on a unit square (u right, v down), for the right controller.
    private enum Shape{Circle,Capsule}
    private readonly record struct Part(Shape Shape,float Ax,float Ay,float Bx,float By,float R,IconParts Lights,bool Dark);
    private static readonly Part[] parts=
    {
        new(Shape.Capsule,.38f,.075f,.56f,.06f,.055f,IconParts.Trigger,false), // trigger, at the front
        new(Shape.Capsule,.36f,.56f,.38f,.80f,.065f,IconParts.Grip,false),     // grip, inner side of the handle
        new(Shape.Capsule,.52f,.46f,.55f,.87f,.125f,IconParts.None,false),     // handle
        new(Shape.Circle,.52f,.34f,0,0,.235f,IconParts.None,false),            // head (face)
        new(Shape.Circle,.43f,.31f,0,0,.09f,IconParts.None,true),              // stick well
        new(Shape.Circle,.43f,.31f,0,0,.058f,IconParts.Stick,false),           // stick
        new(Shape.Circle,.62f,.43f,0,0,.05f,IconParts.Lower,false),            // A / X
        new(Shape.Circle,.665f,.27f,0,0,.05f,IconParts.Upper,false),           // B / Y
    };
    // The point of each part (for checks): its middle, mirrored for the left.
    internal static (float u,float v) Center(IconParts part,IconSide side)
    {
        foreach(var p in parts)
            if(p.Lights==part)
            {
                float u=p.Shape==Shape.Circle?p.Ax:(p.Ax+p.Bx)*.5f,v=p.Shape==Shape.Circle?p.Ay:(p.Ay+p.By)*.5f;
                return (side==IconSide.Left?1-u:u,v);
            }
        return (.5f,.5f);
    }
    private static float Distance(in Part p,float u,float v)
    {
        if(p.Shape==Shape.Circle)return MathF.Sqrt((u-p.Ax)*(u-p.Ax)+(v-p.Ay)*(v-p.Ay))-p.R;
        float dx=p.Bx-p.Ax,dy=p.By-p.Ay,t=Math.Clamp(((u-p.Ax)*dx+(v-p.Ay)*dy)/(dx*dx+dy*dy),0,1);
        float x=u-(p.Ax+dx*t),y=v-(p.Ay+dy*t);
        return MathF.Sqrt(x*x+y*y)-p.R;
    }
    // RGBA pixels, rows from the bottom (as a Unity texture takes them).
    internal static byte[] Paint(IconSpec spec,int size=Size)
    {
        var px=new byte[size*size*4];float pixel=1f/size,ink=.024f,glow=.09f;
        bool mirror=spec.Side==IconSide.Left;
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float u=(x+.5f)/size,v=(y+.5f)/size;if(mirror)u=1-u;
            float r=0,g=0,b=0,a=0;
            void Over(float cr,float cg,float cb,float ca)
            {
                if(ca<=0)return;float na=ca+a*(1-ca);
                r=(cr*ca+r*a*(1-ca))/na;g=(cg*ca+g*a*(1-ca))/na;b=(cb*ca+b*a*(1-ca))/na;a=na;
            }
            // The glow behind the lit parts.
            foreach(var p in parts)
            {
                if((p.Lights&spec.Parts)==0||p.Lights==IconParts.None)continue;
                float d=Distance(p,u,v);float c=Math.Clamp(1-(d-ink)/glow,0,1);
                if(d>ink)Over(Lit.r,Lit.g,Lit.b,.8f*c*c);
            }
            foreach(var p in parts)
            {
                float d=Distance(p,u,v);
                bool lit=p.Lights!=IconParts.None&&(p.Lights&spec.Parts)!=0;
                var line=lit?LitInk:Ink;var fill=lit?Lit:p.Dark?Detail:Body;float width=lit?ink*1.35f:ink;
                Over(line.r,line.g,line.b,Math.Clamp(.5f-(d-width)/pixel,0,1));
                Over(fill.r,fill.g,fill.b,Math.Clamp(.5f-d/pixel,0,1));
            }
            int i=((size-1-y)*size+x)*4;
            px[i]=(byte)Math.Clamp(MathF.Round(r),0,255);px[i+1]=(byte)Math.Clamp(MathF.Round(g),0,255);px[i+2]=(byte)Math.Clamp(MathF.Round(b),0,255);px[i+3]=(byte)Math.Clamp(MathF.Round(a*255),0,255);
        }
        return px;
    }
    // One pixel (u right, v down) of painted pixels.
    internal static (byte r,byte g,byte b,byte a) At(byte[] px,float u,float v,int size=Size)
    {
        int x=Math.Clamp((int)(u*size),0,size-1),y=Math.Clamp((int)(v*size),0,size-1);int i=((size-1-y)*size+x)*4;
        return (px[i],px[i+1],px[i+2],px[i+3]);
    }
}
