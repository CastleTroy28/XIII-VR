namespace XiiiXR;
// 0.1.162: a movie player the game starts is handed over at once (SceneHooks).
internal sealed partial class StoryVideo
{
    partial void Listen()
    {
        SceneHooks.Video=c=>
        {
            foreach(var k in players.Items)if(k!=null&&k.Pointer==c.Pointer)return;
            players.Add(c);nextFind=0;
        };
    }
}
