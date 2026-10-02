using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
internal sealed class MenuPrompts
{
    private readonly List<ButtonPrompt> prompts=new();
    private float nextFind;
    internal bool Hit(Vector3 origin,Vector3 direction,out ButtonPrompt? target,out Vector3 point,out ushort key)
    {
        target=null;point=origin+direction*1.7f;key=0;
        if(SceneScan.Due(ref nextFind,.5f))
        {
            long scanStart=SceneScan.Begin();prompts.Clear();
            foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<ButtonPrompt>()))
            {var p=obj.TryCast<ButtonPrompt>();if(p!=null&&p.gameObject.scene.IsValid()&&MenuKeyboard.PromptKey(p.actionForPrompt)!=0)prompts.Add(p);}
            SceneScan.End("prompts",scanStart);
        }
        float nearest=float.PositiveInfinity;
        foreach(var p in prompts)
        {
            if(p==null||!p.isActiveAndEnabled)continue;
            bool visible=true;
            foreach(var c in p.GetComponentsInParent(Il2CppType.Of<CanvasGroup>(),true))
            {var g=c.TryCast<CanvasGroup>();if(g!=null&&g.alpha<.05f){visible=false;break;}}
            if(!visible)continue;
            var text=p.textRef;var image=p.imageRef;
            RectTransform? rect=text!=null&&text.isActiveAndEnabled&&text.color.a>.05f?text.rectTransform:
                image!=null&&image.isActiveAndEnabled&&image.color.a>.05f?image.rectTransform:null;
            if(rect==null)continue;
            if(!MenuRayMath.Plane(ContactWorld.V(origin),ContactWorld.V(direction),ContactWorld.V(rect.position),ContactWorld.V(rect.forward),out var hit,out float distance)||distance>nearest)continue;
            var local=rect.InverseTransformPoint(ContactWorld.U(hit));
            if(!rect.rect.Contains(new Vector2(local.x,local.y)))continue;
            nearest=distance;target=p;point=ContactWorld.U(hit);key=MenuKeyboard.PromptKey(p.actionForPrompt);
        }
        return target!=null;
    }
}
