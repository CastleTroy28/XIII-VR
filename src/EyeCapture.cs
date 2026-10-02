using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
namespace XiiiXR;
internal static class EyeCapture
{
    private static bool pending;
    internal static IEnumerator Capture(XRDisplaySubsystem display)
    {
        if (pending) { Bootstrap.Write("EYE CAPTURE already pending"); yield break; }
        pending = true;
        try
        {
            Bootstrap.Write("EYE CAPTURE queued; waiting for end of frame");
            yield return new WaitForEndOfFrame();
            Task? write=null;
            try { write=Save(display); }
            catch (Exception ex) { Bootstrap.Warn("EYE CAPTURE failed; XR kept running. " + ex); }
            if(write!=null)
            {
                while(!write.IsCompleted)yield return null;
                if(write.IsFaulted)Bootstrap.Warn("EYE CAPTURE save failed: "+write.Exception);
                else Bootstrap.Write("EYE CAPTURE saved LEFT | RIGHT (PNG compression on worker)");
            }
        }
        finally { pending = false; }
    }
    private static Task Save(XRDisplaySubsystem display)
    {
        if (!display.running) throw new InvalidOperationException("XR display is no longer running");
        int count = display.GetRenderPassCount();
        Bootstrap.Write("EYE CAPTURE renderPassCount=" + count);
        // This package configures Valve's multipass mode, which supplies pass 0
        // for left and pass 1 for right. Reject other layouts; never manufacture
        // a stereo image by copying the desktop mirror into two halves.
        if (count != 2) throw new NotSupportedException("Expected two OpenVR multipass render passes; got " + count);
        var left = display.GetRenderTextureForRenderPass(0);
        var right = display.GetRenderTextureForRenderPass(1);
        if (left == null || right == null) throw new InvalidOperationException("XR render texture unavailable");
        if (left.GetInstanceID() == right.GetInstanceID()) throw new NotSupportedException("XR passes share a texture; a layout-specific capture is required");
        if (left.dimension != TextureDimension.Tex2D || right.dimension != TextureDimension.Tex2D)
            throw new NotSupportedException("Expected two 2D eye textures; got " + left.dimension + "/" + right.dimension);
        int w = left.width, h = left.height;
        if (w != right.width || h != right.height || w <= 0 || h <= 0)
            throw new NotSupportedException("Unexpected eye dimensions");
        // Downsample only the diagnostic capture, never the headset render targets.
        float captureScale=Math.Min(1,Math.Min(2048f/w,MathF.Sqrt(8000000f/((float)w*h*2))));
        w=Math.Max(1,(int)(w*captureScale));h=Math.Max(1,(int)(h*captureScale));
        string directory = Path.Combine(BepInEx.Paths.GameRootPath, "XIII-VR-Captures");
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "XIII-EYES-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-L-R.png");
        Bootstrap.Write($"EYE CAPTURE sources leftID={left.GetInstanceID()} rightID={right.GetInstanceID()} each={w}x{h}");
        var previous = RenderTexture.active;
        RenderTexture? resolved = null;
        Texture2D? pixels = null;
        try
        {
            resolved = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 1);
            pixels = new Texture2D(w * 2, h, TextureFormat.RGB24, false);
            Graphics.Blit(left, resolved);
            RenderTexture.active = resolved;
            pixels.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            Graphics.Blit(right, resolved);
            RenderTexture.active = resolved;
            pixels.ReadPixels(new Rect(0, 0, w, h), w, 0, false);
            // 0.1.153: one block copy (the element-by-element conversion of 24 MB
            // held the game for most of a second at every capture).
            byte[] rgb=pixels.GetRawTextureData().AsSpan().ToArray();
            int width=w*2,height=h;
            Bootstrap.Write("EYE CAPTURE readback complete; encoding asynchronously: "+file);
            return Task.Run(()=>CapturePng.Write(file,width,height,rgb));
        }
        finally
        {
            RenderTexture.active = previous;
            if (resolved != null) RenderTexture.ReleaseTemporary(resolved);
            if (pixels != null) UnityEngine.Object.Destroy(pixels);
        }
    }
}
