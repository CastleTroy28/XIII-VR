using UnityEngine;
namespace XiiiXR;
internal sealed partial class NativeHandVisual
{
    private Mesh? baked,mesh;
    private NativeSkinSnapshot? snapshot;
    // The bake target can be repaired in-place. Missing visible geometry or
    // private bones instead requires GloveVisual.BindNative to rebuild the hand.
    private bool RenderResourcesValid=>mesh!=null&&snapshot?.Valid==true;
    private Mesh EnsureBakeTarget()
    {
        if(baked!=null)return baked;
        bool recovered=!ReferenceEquals(baked,null);
        baked=new Mesh(){name="XIII "+(rightHand?"R":"L")+" hand pose buffer",hideFlags=HideFlags.DontUnloadUnusedAsset};
        ResetRenderPose();
        if(recovered)Bootstrap.Write("NATIVE HAND pose buffer restored side="+(rightHand?"R":"L")+"; rebuilding current fingers");
        return baked;
    }
    private void DisposeSkinResources()
    {
        if(mesh!=null)UnityEngine.Object.Destroy(mesh);mesh=null;
        if(baked!=null)UnityEngine.Object.Destroy(baked);baked=null;
        snapshot?.Dispose();snapshot=null;
    }
}
