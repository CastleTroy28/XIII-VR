using System;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using N=System.Numerics.Vector3;
namespace XiiiXR;
// 0.1.234: VR SETTINGS > Weapon places. Every weapon place but the two on
// the back is a ball around the player's body (where the hand takes the
// weapon from, as the holster hint shows it), with its name. Pointed at with
// the controller ray, the trigger held drags it along the ray (the stick
// up/down: further/nearer); let go, it is kept (HolsterPlaces, the config).
// The weapons on the body follow it while the game is paused. A small panel
// in front: reset the place last chosen, reset them all, back (also B).
internal sealed class HolsterPlaceEditor:IDisposable
{
    private const float Width=900,RowHeight=50,TitleHeight=70,FooterHeight=190;
    private const int Rows=3;
    private static float Height=>TitleHeight+RowHeight*Rows+FooterHeight;
    // Balls: 5 cm (6.5 cm pointed at or dragged); the ray takes one within 7 cm.
    private const float Ball=.05f,BallHot=.065f,PickRadius=.07f,StickSpeed=.6f,MinDrag=.08f,MaxDrag=2f;
    private readonly CameraRig rig;
    private GameObject? panel;private TextMeshProUGUI? title,footer;private Image? highlight;
    private readonly TextMeshProUGUI?[] rows=new TextMeshProUGUI?[Rows];
    private readonly GameObject?[] balls=new GameObject?[HolsterPlaces.Movable.Length];
    private readonly Material?[] ballMaterials=new Material?[HolsterPlaces.Movable.Length];
    private readonly GameObject?[] labels=new GameObject?[HolsterPlaces.Movable.Length];
    private readonly TextMeshProUGUI?[] labelTexts=new TextMeshProUGUI?[HolsterPlaces.Movable.Length];
    private TMP_FontAsset? font;
    private readonly MenuBeam beam=new();
    private readonly UiPointerState pointer=new();
    private bool placed,failed;private float nextError;
    private int hoverRow=-1,hoverPlace=-1,selected,dragging=-1;
    private float dragDistance;private Vector3 dragDelta;
    internal HolsterPlaceEditor(CameraRig owner){rig=owner;}
    private static bool LeftHanded=>WeaponHands.LeftHanded;
    private Quaternion TorsoYaw()
    {
        var f=rig.HeadRotation*Vector3.forward;
        return Quaternion.Euler(0,HolsterPlaces.Torso(new N(f.x,f.y,f.z)),0);
    }
    private Vector3 World(HolsterSlot slot,Quaternion torso)
    {
        var r=HolsterLayout.Pose(slot,LeftHanded).Reach;
        return rig.HeadPosition+torso*new Vector3(r.X,r.Y,r.Z);
    }
    // Every frame while the editor is open, before the menus' input.
    internal void Tick()
    {
        if(failed)return;
        try{TickCore();}
        catch(Exception ex){if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+5;Bootstrap.Warn("WEAPON PLACES editor: "+ex.Message);}}
    }
    private void TickCore()
    {
        hoverRow=-1;hoverPlace=-1;
        if(!rig.SamplePointerHand(out var hand)){EndDrag();pointer.Sample(false,false,0);beam.Hide();return;}
        var origin=CameraRig.UnityPosition(hand);var direction=rig.PointerRotation(hand)*Vector3.forward;
        var input=rig.MenuPointerControls;bool held=input.Valid&&(input.Held&HandControls.Trigger)!=0;
        var torso=TorsoYaw();
        if(dragging>=0)
        {
            if(held)
            {
                var stick=rig.PointerLeft?rig.LeftStick:rig.RightStick;
                if(stick.Valid&&Math.Abs(stick.Value.Y)>.25f)dragDistance=Mathf.Clamp(dragDistance+stick.Value.Y*StickSpeed*Mathf.Min(Time.unscaledDeltaTime,.05f),MinDrag,MaxDrag);
                var slot=HolsterPlaces.Movable[dragging];
                var dragged=origin+direction*dragDistance+dragDelta;
                var local=Quaternion.Inverse(torso)*(dragged-rig.HeadPosition);
                HolsterPlaces.MoveTo(slot,new N(local.x,local.y,local.z),LeftHanded);
                hoverPlace=dragging;
                pointer.Sample(true,true,100+dragging,true);
                beam.Show(origin,World(slot,torso),true);
                return;
            }
            EndDrag();
        }
        // Pointed at: a ball (nearer than the panel) or a row of the panel.
        var centers=new N[HolsterPlaces.Movable.Length];
        for(int i=0;i<centers.Length;i++){var w=World(HolsterPlaces.Movable[i],torso);centers[i]=new N(w.x,w.y,w.z);}
        int place=HolsterPlaces.Pick(new N(origin.x,origin.y,origin.z),new N(direction.x,direction.y,direction.z),centers,PickRadius,out float along);
        int row=-1;float panelDistance=float.PositiveInfinity;Vector3 panelHit=Vector3.zero;
        if(panel!=null&&placed&&panel.activeSelf)
        {
            var t=panel.transform;var normal=t.forward;float facing=Vector3.Dot(direction,normal);
            if(facing>.05f)
            {
                float distance=Vector3.Dot(t.position-origin,normal)/facing;
                if(distance>0&&distance<5)
                {
                    var hit=origin+direction*distance;var local=t.InverseTransformPoint(hit);
                    if(Math.Abs(local.x)<=Width*.5f&&Math.Abs(local.y)<=Height*.5f)
                    {
                        panelDistance=distance;panelHit=hit;
                        float fromTop=Height*.5f-local.y-TitleHeight;
                        if(fromTop>=0){int r=(int)(fromTop/RowHeight);if(r<Rows)row=r;}
                    }
                }
            }
        }
        int target=0;Vector3 end=origin+direction*1.5f;
        if(place>=0&&along<panelDistance){hoverPlace=place;target=100+place;end=origin+direction*along;}
        else if(float.IsFinite(panelDistance)){end=panelHit;if(row>=0){hoverRow=row;target=row+1;}}
        var step=pointer.Sample(true,held,target);
        beam.Show(origin,end,target!=0);
        if(step.Down&&target>=100)
        {
            dragging=target-100;selected=dragging;dragDistance=Mathf.Clamp(along,MinDrag,MaxDrag);
            dragDelta=World(HolsterPlaces.Movable[dragging],torso)-(origin+direction*dragDistance);
            rig.PunchHaptics(!rig.PointerLeft);
        }
        if(step.Click&&target>=1&&target<=Rows){Click(target-1);rig.PunchHaptics(!rig.PointerLeft);rig.DisarmMenuTriggers();}
    }
    private void EndDrag()
    {
        if(dragging<0)return;
        var slot=HolsterPlaces.Movable[dragging];dragging=-1;
        HolsterPlaces.Save(slot);
        var o=HolsterPlaces.Offset(slot);
        Bootstrap.Write("WEAPON PLACES "+slot+(LeftHanded?" (mirrored: left-handed)":"")+" moved "+Cm(o.X)+" right, "+Cm(o.Y)+" up, "+Cm(o.Z)+" forward of the mod's place (saved)");
    }
    private static string Cm(float metres)=>(metres*100).ToString("0.0");
    private void Click(int row)
    {
        var slot=HolsterPlaces.Movable[Math.Clamp(selected,0,HolsterPlaces.Movable.Length-1)];
        if(row==0){HolsterPlaces.Reset(slot);Bootstrap.Write("WEAPON PLACES "+slot+" back to the mod's place");}
        else if(row==1){HolsterPlaces.ResetAll();Bootstrap.Write("WEAPON PLACES all back to the mod's places");}
        else Back();
    }
    // Back to the VR SETTINGS rows (also B).
    internal void Back(){EndDrag();QualityMenu.EndPlaces();Hide();}
    // Before drawing, while the editor is open.
    internal void Render(TMP_FontAsset? menuFont)
    {
        if(failed)return;
        try
        {
            font??=menuFont??TMP_Settings.defaultFontAsset;
            if(panel==null&&!Build())return;
            var torso=TorsoYaw();
            if(!placed)
            {
                var yaw=Quaternion.Euler(0,rig.HeadRotation.eulerAngles.y,0);
                panel!.transform.SetPositionAndRotation(rig.HeadPosition+yaw*new Vector3(0,.02f,1.05f),yaw);placed=true;
            }
            panel!.SetActive(true);
            bool lefty=LeftHanded;
            var chosen=HolsterPlaces.Movable[Math.Clamp(selected,0,HolsterPlaces.Movable.Length-1)];
            if(title!=null)title.text=UiLanguage.L("WEAPON PLACES");
            string[] texts={UiLanguage.L("Reset this place")+": "+UiLanguage.L(HolsterPlaces.Name(chosen,lefty)),UiLanguage.L("Reset all places"),UiLanguage.L("Back")};
            for(int i=0;i<Rows;i++)
            {
                var r=rows[i];if(r==null)continue;
                if(r.text!=texts[i])r.text=texts[i];
                r.color=i==hoverRow?Color.white:new Color(.82f,.88f,.92f,1);
            }
            if(highlight!=null)
            {
                if(highlight.gameObject.activeSelf!=(hoverRow>=0))highlight.gameObject.SetActive(hoverRow>=0);
                if(hoverRow>=0)highlight.rectTransform.anchoredPosition=new Vector2(0,Height*.5f-TitleHeight-RowHeight*(hoverRow+.5f));
            }
            if(footer!=null)
            {
                string text=UiLanguage.L("Point at a ball and hold the trigger to move it. Stick up/down: further/nearer.")+"\n"
                    +UiLanguage.L("The weapons on your back stay where they are.")+"\n"+UiLanguage.L("B: back");
                if(footer.text!=text)footer.text=text;
            }
            for(int i=0;i<HolsterPlaces.Movable.Length;i++)
            {
                var slot=HolsterPlaces.Movable[i];var ball=balls[i];var label=labels[i];if(ball==null||label==null)continue;
                var at=World(slot,torso);bool hot=i==hoverPlace||i==dragging;
                ball.transform.SetPositionAndRotation(at,Quaternion.identity);ball.transform.localScale=Vector3.one*(hot?BallHot:Ball);
                var m=ballMaterials[i];
                if(m!=null)m.color=i==dragging?new Color(1,.55f,.1f,.95f):hot?new Color(1,1,1,.95f):i==selected?new Color(.35f,.9f,1,.9f):new Color(.3f,.65f,.85f,.75f);
                if(!ball.activeSelf)ball.SetActive(true);
                // The name beside the ball (outward), turned to the eyes.
                var local=Quaternion.Inverse(torso)*(at-rig.HeadPosition);
                float side=Math.Abs(local.x)<.05f?1:Math.Sign(local.x);
                var labelAt=at+torso*new Vector3(side*.045f,.02f,0);
                var look=labelAt-rig.HeadPosition;if(look.sqrMagnitude<1e-6f)look=torso*Vector3.forward;
                label.transform.SetPositionAndRotation(labelAt,Quaternion.LookRotation(look.normalized,Vector3.up));
                var text=labelTexts[i];
                if(text!=null)
                {
                    string name=UiLanguage.L(HolsterPlaces.Name(slot,lefty))+(HolsterPlaces.Moved(slot)?" *":"");
                    if(text.text!=name)text.text=name;
                    // Its near end at the ball: the text to the right of a ball on the right, to the left of one on the left.
                    text.alignment=side<0?TextAlignmentOptions.Right:TextAlignmentOptions.Left;
                    text.rectTransform.anchoredPosition=new Vector2(side<0?-260:260,0);
                    text.color=hot?Color.white:new Color(.8f,.9f,.95f,.9f);text.fontSize=hot?34:28;
                }
                if(!label.activeSelf)label.SetActive(true);
            }
        }
        catch(Exception ex){if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+5;Bootstrap.Warn("WEAPON PLACES draw: "+ex.Message);}}
    }
    internal void Hide()
    {
        EndDrag();placed=false;hoverRow=hoverPlace=-1;
        try
        {
            pointer.Sample(false,false,0);beam.Hide();
            if(panel!=null&&panel.activeSelf)panel.SetActive(false);
            foreach(var b in balls)if(b!=null&&b.activeSelf)b.SetActive(false);
            foreach(var l in labels)if(l!=null&&l.activeSelf)l.SetActive(false);
        }
        catch(Exception){}
    }
    private bool Build()
    {
        if(font==null)return false;
        try
        {
            panel=new GameObject("XIII VR weapon places page");panel.layer=5;UnityEngine.Object.DontDestroyOnLoad(panel);
            var rect=panel.AddComponent(Il2CppType.Of<RectTransform>()).TryCast<RectTransform>()!;
            var canvas=panel.AddComponent(Il2CppType.Of<Canvas>()).TryCast<Canvas>()!;canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=200;
            var bg=panel.AddComponent(Il2CppType.Of<Image>()).TryCast<Image>()!;bg.color=new Color(.04f,.09f,.12f,.94f);bg.raycastTarget=false;
            rect.sizeDelta=new Vector2(Width,Height);panel.transform.localScale=Vector3.one*.001f;
            var bar=Child(panel.transform,"Highlight",Vector2.zero,new Vector2(Width-20,RowHeight-4));
            highlight=bar.gameObject.AddComponent(Il2CppType.Of<Image>()).TryCast<Image>()!;highlight.color=new Color(.16f,.46f,.62f,.75f);highlight.raycastTarget=false;
            title=Label(panel.transform,"Title",new Vector2(0,Height*.5f-TitleHeight*.5f),new Vector2(Width-60,TitleHeight),34,TextAlignmentOptions.Center);
            title.color=new Color(.45f,.85f,1,1);
            for(int i=0;i<Rows;i++)rows[i]=Label(panel.transform,"Row "+i,new Vector2(0,Height*.5f-TitleHeight-RowHeight*(i+.5f)),new Vector2(Width-60,RowHeight),26,TextAlignmentOptions.Center);
            footer=Label(panel.transform,"Footer",new Vector2(0,-Height*.5f+FooterHeight*.5f),new Vector2(Width-60,FooterHeight-20),20,TextAlignmentOptions.Top);
            footer.color=new Color(.75f,.8f,.84f,1);footer.enableWordWrapping=true;
            panel.SetActive(false);
            var shader=Shader.Find("Sprites/Default")??Shader.Find("Unlit/Color");
            for(int i=0;i<balls.Length;i++)
            {
                var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);ball.SetActive(false);ball.name="XIII weapon place "+HolsterPlaces.Movable[i];ball.layer=5;UnityEngine.Object.DontDestroyOnLoad(ball);
                var collider=ball.GetComponent(Il2CppType.Of<Collider>())?.TryCast<Collider>();
                if(collider!=null){collider.enabled=false;UnityEngine.Object.Destroy(collider);}
                var renderer=ball.GetComponent(Il2CppType.Of<Renderer>())?.TryCast<Renderer>();
                if(renderer!=null&&shader!=null)
                {
                    var m=new Material(shader){renderQueue=3990};renderer.sharedMaterial=m;ballMaterials[i]=m;
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                }
                balls[i]=ball;
                var label=new GameObject("XIII weapon place name "+HolsterPlaces.Movable[i]);label.layer=5;UnityEngine.Object.DontDestroyOnLoad(label);
                var lr=label.AddComponent(Il2CppType.Of<RectTransform>()).TryCast<RectTransform>()!;
                var lc=label.AddComponent(Il2CppType.Of<Canvas>()).TryCast<Canvas>()!;lc.renderMode=RenderMode.WorldSpace;lc.sortingOrder=210;
                lr.sizeDelta=new Vector2(520,60);label.transform.localScale=Vector3.one*.0006f;
                var t=Label(label.transform,"Name",new Vector2(260,0),new Vector2(520,60),28,TextAlignmentOptions.Left);
                labelTexts[i]=t;labels[i]=label;label.SetActive(false);
            }
            return true;
        }
        catch(Exception ex){failed=true;Bootstrap.Warn("WEAPON PLACES editor unavailable: "+ex.Message);Dispose();return false;}
    }
    private static RectTransform Child(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name);go.layer=5;go.transform.SetParent(parent,false);
        var r=go.AddComponent(Il2CppType.Of<RectTransform>()).TryCast<RectTransform>()!;
        r.anchoredPosition=position;r.sizeDelta=size;return r;
    }
    private TextMeshProUGUI Label(Transform parent,string name,Vector2 position,Vector2 size,float points,TextAlignmentOptions alignment)
    {
        var text=Child(parent,name,position,size).gameObject.AddComponent(Il2CppType.Of<TextMeshProUGUI>()).TryCast<TextMeshProUGUI>()!;
        text.font=font;text.fontSize=points;text.color=Color.white;text.alignment=alignment;text.raycastTarget=false;text.richText=true;text.enableWordWrapping=false;
        return text;
    }
    public void Dispose()
    {
        try{EndDrag();beam.Dispose();}catch(Exception){}
        if(panel!=null)UnityEngine.Object.Destroy(panel);panel=null;
        for(int i=0;i<balls.Length;i++)
        {
            if(balls[i]!=null)UnityEngine.Object.Destroy(balls[i]);balls[i]=null;
            if(ballMaterials[i]!=null)UnityEngine.Object.Destroy(ballMaterials[i]);ballMaterials[i]=null;
            if(labels[i]!=null)UnityEngine.Object.Destroy(labels[i]);labels[i]=null;labelTexts[i]=null;
        }
    }
}
