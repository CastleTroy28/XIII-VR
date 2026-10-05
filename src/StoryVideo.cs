using System;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
namespace XiiiXR;
// Route only the game's story movie player. Do not start/seek/stop video, audio
// or timelines. The decoder's original timing and skip/end callbacks remain native.
internal sealed partial class StoryVideo : IDisposable
{
    private CutscenePlayer? owner;
    private VideoPlayer? video;
    private VideoRenderMode originalMode;
    private bool borrowed;
    private GameObject? root;
    private Canvas? canvas;
    private RawImage? screen;
    private RectTransform? rect;
    private float nextFind,nextReport;
    // 0.1.162: the game's movie players, kept between rare searches (every
    // 30 s since 0.1.164, and at once after a scene change); a player that starts a movie
    // is also handed over by the game's hook (StoryVideo.Hook). The kept ones
    // are checked 4 times a second, which costs nothing.
    private readonly SceneFind<CutscenePlayer> players=new("video",30,found=>
    {
        foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<CutscenePlayer>()))
        {var c=obj.TryCast<CutscenePlayer>();if(c!=null&&c.gameObject.scene.IsValid())found.Add(c);}
    });
    internal StoryVideo(){Listen();}
    partial void Listen();
    internal bool Active=>video!=null&&owner!=null&&owner.gameObject.activeInHierarchy&&!owner.m_videoIsComplete&&(video.isPlaying||video.isPaused);
    internal bool Skip()
    {
        if(!Active||owner==null)return false;
        bool allowed=owner.m_anyPlayerCanSkip;
        try{owner.m_anyPlayerCanSkip=true;owner.TryStopCurrentVideo();return true;}
        finally{owner.m_anyPlayerCanSkip=allowed;}
    }
    internal void Tick()
    {
        try
        {
            if(video!=null&&!Active)ReleasePlayer();
            // 0.1.121: never with another scene search in the frame.
            // 0.1.162: the kept players 4 times a second; the search itself rarely.
            if(!Active)
            {
                bool searched=players.Refresh();
                if(searched||Time.realtimeSinceStartup>=nextFind)
                {
                    nextFind=Time.realtimeSinceStartup+.25f;
                    foreach(var c in players.Items)
                    {
                        if(c==null||!c.gameObject.activeInHierarchy||c.m_videoIsComplete)continue;
                        var v=c.m_videoPlayer;
                        if(v==null||(!v.isPlaying&&!v.isPaused))continue;
                        ReleasePlayer();owner=c;video=v;originalMode=v.renderMode;borrowed=true;
                        Bootstrap.Write("STORY VIDEO begin owner="+c.name+" source="+v.name+" nativeMode="+originalMode);
                        break;
                    }
                }
            }
            if(!Active){if(root!=null)root.SetActive(false);return;}
            EnsureScreen();
            video!.renderMode=VideoRenderMode.APIOnly;
            screen!.texture=video.texture;
            root!.SetActive(screen.texture!=null);
            if(Time.realtimeSinceStartup>=nextReport)
            {
                nextReport=Time.realtimeSinceStartup+3;
                var t=screen.texture;
                Bootstrap.Write("STORY VIDEO frame="+video.frame+" prepared="+video.isPrepared+" texture="+(t==null?"none":t.width+"x"+t.height));
            }
        }
        catch(Exception ex){ReleasePlayer();players.Prune();nextFind=Time.realtimeSinceStartup+2;Bootstrap.Warn("STORY VIDEO presentation retry: "+ex.Message);}
    }
    private void EnsureScreen()
    {
        if(root!=null&&screen!=null&&canvas!=null)return;
        DestroyScreen();
        root=new GameObject("XIII story movie screen");root.layer=5;UnityEngine.Object.DontDestroyOnLoad(root);
        canvas=root.AddComponent(Il2CppType.Of<Canvas>()).TryCast<Canvas>()!;
        canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=-100;
        rect=root.transform.TryCast<RectTransform>()!;
        rect.pivot=new Vector2(.5f,.5f);
        var child=new GameObject("XIII decoded video");child.layer=5;child.transform.SetParent(root.transform,false);
        screen=child.AddComponent(Il2CppType.Of<RawImage>()).TryCast<RawImage>()!;
        screen.raycastTarget=false;screen.color=Color.white;
        var r=screen.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
        r.localPosition=Vector3.zero;r.localRotation=Quaternion.identity;r.localScale=Vector3.one;
    }
    internal void Render(CameraRig rig)
    {
        if(!Active||root==null||screen==null||rect==null||canvas==null||screen.texture==null)return;
        var t=screen.texture;float aspect=t.height>0?(float)t.width/t.height:16f/9;
        // Same angular scale as the native UI plane. Original subtitles and
        // skip UI are in front of this screen, on their original common plane.
        const float depth=1.72f;float width=2.2f*depth/1.7f;
        canvas.worldCamera=rig.MainCamera!;rect.sizeDelta=new Vector2(1920,1920/aspect);
        rect.localScale=Vector3.one*(width/1920);
        // 0.1.245: on the screen that stands still (CameraRig.PoseMovieScreen), not in front of the head.
        rect.SetPositionAndRotation(rig.CinemaPosition+rig.CinemaRotation*new Vector3(0,0,depth),rig.CinemaRotation);
    }
    private void ReleasePlayer()
    {
        if(borrowed)
        {
            try{if(video!=null&&video.renderMode==VideoRenderMode.APIOnly)video.renderMode=originalMode;}
            catch(Exception ex){Bootstrap.Warn("STORY VIDEO restore: "+ex.Message);}
            Bootstrap.Write("STORY VIDEO end; native presentation restored");
        }
        borrowed=false;owner=null;video=null;
        if(screen!=null)screen.texture=null;
        if(root!=null)root.SetActive(false);
    }
    private void DestroyScreen(){if(root!=null)UnityEngine.Object.Destroy(root);root=null;canvas=null;screen=null;rect=null;}
    public void Dispose(){ReleasePlayer();DestroyScreen();}
}
