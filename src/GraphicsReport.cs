using System;
using System.Globalization;
using System.Text;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
// 0.1.164: once per
// level, the game's own graphics options (as its menu sets them) and what
// Unity runs with (shadows, their distance, detail, the eye resolution), so
// the log tells which setting to lower.
internal static class GraphicsReport
{
    private static IntPtr reported;
    internal static void Level(Transform? root)
    {
        if(root==null||root.Pointer==reported)return;
        reported=root.Pointer;
        var text=new StringBuilder("GRAPHICS");
        try
        {
            var game=Find();var s=game?.Setting;
            if(game!=null&&s!=null)
                text.Append(" game: preset=").Append(game.pcPresetLevel).Append(" shadows=").Append(s.shadowQuality).Append(" detail=").Append(s.lodBiasLevel)
                    .Append(" ambientOcclusion=").Append(s.ambientOcclusion).Append(" bloom=").Append(s.bloom).Append(" motionBlur=").Append(s.motionBlurLevel)
                    .Append(" chromaticAberration=").Append(s.chromaticAbberation).Append(" textures=").Append(s.selectedTextureQualityOption).Append(" anisotropic=").Append(s.ansitropicFiltering).Append(';');
            else text.Append(" game options not found;");
        }
        catch(Exception ex){text.Append(" game options unreadable ("+ex.Message+");");}
        try
        {
            int level=QualitySettings.GetQualityLevel();var names=QualitySettings.names;
            text.Append(" unity: quality=").Append(names!=null&&level>=0&&level<names.Length?names[level]:level.ToString(CultureInfo.InvariantCulture))
                .Append(" shadows=").Append(QualitySettings.shadows).Append(" shadowResolution=").Append(QualitySettings.shadowResolution)
                .Append(" shadowDistance=").Append(QualitySettings.shadowDistance.ToString("F0",CultureInfo.InvariantCulture)).Append(" cascades=").Append(QualitySettings.shadowCascades)
                .Append(" lodBias=").Append(QualitySettings.lodBias.ToString("F2",CultureInfo.InvariantCulture)).Append(" pixelLights=").Append(QualitySettings.pixelLightCount)
                .Append(" msaa=").Append(QualitySettings.antiAliasing);
        }
        catch(Exception ex){text.Append(" unity quality unreadable ("+ex.Message+")");}
        try
        {
            text.Append(" eye=").Append(UnityEngine.XR.XRSettings.eyeTextureWidth).Append('x').Append(UnityEngine.XR.XRSettings.eyeTextureHeight)
                .Append(" eyeScale=").Append(UnityEngine.XR.XRSettings.eyeTextureResolutionScale.ToString("F2",CultureInfo.InvariantCulture))
                .Append(" stereo=").Append(UnityEngine.XR.XRSettings.stereoRenderingMode);
        }
        catch(Exception){}
        // 0.1.166: how the game's memory clean-up runs (a slice of each frame while it is on).
        try{text.Append(" gc=").Append(UnityEngine.Scripting.GarbageCollector.isIncremental?"incremental":"full").Append(" slice=").Append((UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds/1e6).ToString("F1",CultureInfo.InvariantCulture)).Append("ms");}
        catch(Exception){}
        try{text.Append(" physicsStep=").Append((Time.fixedDeltaTime*1000).ToString("F1",CultureInfo.InvariantCulture)).Append("ms targetFps=").Append(Application.targetFrameRate).Append(" vsync=").Append(QualitySettings.vSyncCount);}
        catch(Exception){}
        Bootstrap.Write(text.ToString());
    }
    private static GraphicOptions? Find()
    {
        foreach(var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<GraphicOptions>()))
        {var g=o.TryCast<GraphicOptions>();if(g!=null&&g.Setting!=null)return g;}
        return null;
    }
}
