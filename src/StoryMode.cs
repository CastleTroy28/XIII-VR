namespace XiiiXR;
// The native flag includes the *playable* yacht flashback. It is not an input lock.
internal enum StoryMode { Frontend, Gameplay, PlayableFlashback, Cinematic, Movie }
internal static class StoryModePolicy
{
    internal static bool AuthoredCamera(string name)=>name.StartsWith("in_camera_cs_",System.StringComparison.OrdinalIgnoreCase)
        ||name.StartsWith("Camera_blink_cs",System.StringComparison.OrdinalIgnoreCase)
        ||name.StartsWith("vc_",System.StringComparison.OrdinalIgnoreCase)&&name.Contains("player camera animation",System.StringComparison.OrdinalIgnoreCase);
    internal static StoryMode Select(bool player,bool movie,bool flashbackOrCutscene,bool timeline,bool inputLocked,bool axisLocked,bool restricted,bool menu,bool authoredTimeline=false)
    {
        if(movie)return StoryMode.Movie;
        if(!player)return StoryMode.Frontend;
        // Pause/wheel/tutorial locks alone are not a cinematic camera request.
        if(authoredTimeline || timeline || (!menu && flashbackOrCutscene && (inputLocked||axisLocked||restricted)))return StoryMode.Cinematic;
        return flashbackOrCutscene?StoryMode.PlayableFlashback:StoryMode.Gameplay;
    }
}
