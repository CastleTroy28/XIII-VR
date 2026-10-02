using System;using System.Collections.Generic;using System.Linq;using System.Reflection;using XiiiXR;using UnityEngine;using UnityEngine.UI;using UI.HUD;
class UiVisualsTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static object? Invoke(Type t,string method,params object[] args)=>t.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
 static void Main()
 {
  var external=new Image{name="Other graphic",sprite=new Sprite{name="hud_killcam_frame_cutscene_test"}};Resources.Items.Add(external);
  var frame=new Image{name="Frame"};var raw=new RawImage{name="Cinematic border"};var subtitle=new Image{name="Subtitle frame",Subtitle=true};var button=new Image{name="Button border",Selectable=true};var comic=new Image{name="Comic art"};var cutscene=new CutsceneUI();cutscene.Children.AddRange(new UnityEngine.Object[]{frame,raw,subtitle,button,comic});Resources.Items.Add(cutscene);
  var materialFrame=new RawImage{name="Generic image",material=new Material{name="Cutscene frame",shader=new Shader{name="UI/Default"}}};Resources.Items.Add(materialFrame);
  using(var f=new CinematicFrame())
  {
   f.Discover();f.Render(false);Check(!materialFrame.enabled,"material-driven raw frame outside CutsceneUI survives");Check(!external.enabled&&!external.gameObject.activeSelf,"same cinematic asset outside CutsceneUI survives");Check(!frame.gameObject.activeSelf,"decorative leaf remains active");Check(!frame.enabled&&!raw.enabled&&subtitle.enabled&&comic.enabled&&button.enabled,"frame suppression hides story content");
   frame.gameObject.SetActive(true);frame.enabled=true;frame.canvasRenderer.HasMesh=true;Invoke(typeof(CinematicFrame),"Rebuilt",frame);Check(!frame.canvasRenderer.HasMesh&&frame.canvasRenderer.cull,"cached frame survives renderer clear");var mesh=new VertexHelper();Check(!(bool)Invoke(typeof(CinematicFrame),"Populate",frame,mesh)!&&mesh.Cleared,"animation re-enable bypasses geometry suppression");
   Check((bool)Invoke(typeof(CinematicFrame),"Populate",comic,new VertexHelper())!,"comic mesh suppressed");
  }
  Check(frame.gameObject.activeSelf&&frame.color.a==1&&frame.enabled&&raw.enabled&&subtitle.enabled,"frame originals not restored");
  // Reproduce the 10-second discovery freeze: managed wrappers remain
  // non-null after their native sprite/material/shader/texture has been freed.
  Resources.Items.Clear();
  var staleSprite=new Sprite{Destroyed=true};var staleMat=new Material{Destroyed=true};
  var staleShader=new Shader{Destroyed=true};var staleTexture=new Texture{Destroyed=true};
  var changed=new Image{overrideSprite=staleSprite,sprite=new Sprite{name="hud_killcam_frame_cutscene"},material=staleMat};
  var changedShader=new Image{material=new Material{shader=staleShader}};
  var staleRaw=new RawImage{texture=staleTexture};
  var nativeNameThrows=new Image{sprite=new Sprite{ThrowName=true}};
  var nativeGetterThrows=new Image{ThrowMaterial=true};
  var afterBroken=new Image{sprite=new Sprite{name="hud_killcam_frame_cutscene"}};
  Resources.Items.AddRange(new UnityEngine.Object[]{changed,changedShader,staleRaw,nativeNameThrows,nativeGetterThrows,afterBroken});
  using(var f=new CinematicFrame())
  {
   f.Discover();f.Render(false);
   Check(!changed.enabled&&!afterBroken.enabled,"stale asset aborts discovery before valid cinematic frames");
   Check(changedShader.enabled&&staleRaw.enabled&&nativeNameThrows.enabled,"invalid asset accidentally hides live UI");
   Check(UnityEngine.Object.DeadNameReads==0,"destroyed native Object.name accessed through live managed wrapper");
   Check((bool)Invoke(typeof(CinematicFrame),"Populate",nativeGetterThrows,new VertexHelper())!,"faulting graphic escapes mesh hook or suppresses unrelated geometry");
   // Scene unload between discovery and render must also be harmless.
   afterBroken.Destroyed=true;f.Render(true);f.Discover();
  }
  Check(changed.enabled,"restoration skipped remaining live frame after stale graphic");
  Resources.Items.Clear();
  var icon=new Image{name="Native white pistol",sprite=new Sprite{name="original_pistol"},color=new(.7f,.7f,.7f,1)};var background=new Image{name="stretched panel"};
  var holder=new UiWeaponIconHolder{weaponSprite=icon};var objectHolder=new GameObject();objectHolder.Children.Add(holder);
  icon.rectTransform.position=new(2,3,4);icon.rectTransform.sizeDelta=new(110,60);var panel=new Image();panel.rectTransform.localPosition=new(700,200,0);
  var indicator=new WeaponInventoryIndicator{selectedWeaponPanel=panel};indicator.displayedWeapons.Add(objectHolder);indicator.inventoryGroup.Children.AddRange(new UnityEngine.Object[]{holder,icon,background,panel});Resources.Items.Add(indicator);
  using(var c=new CycleIcons())
  {
   c.Discover();var selected=new PlayMagic.Weapons.Equipable{slot=PlayerEquipableInventory.ActiveEquipmentSlot.Pistol};indicator.playerInventory=new PlayerEquipableInventory{currentEquipable=selected};indicator.playerInventory.activeEquipment[selected.slot]=selected;
   Invoke(typeof(CycleIcons),"Generated",indicator,selected.slot,objectHolder);Invoke(typeof(CycleIcons),"Populated",indicator,selected);
   // Native binary inlines selected-indicator positioning: NEVER call Selected.
   CycleIcons.Request();c.Render();
   Check(icon.enabled&&icon.color.r==1&&icon.sprite.name=="original_pistol"&&!background.enabled,"native Y strip sprite changed");
   Check(!panel.enabled&&panel.color.a==0&&icon.color.a==1,"stray selection rectangle still visible");
   Check(!(bool)Invoke(typeof(CycleIcons),"Populating",indicator)!,"unchanged native icons rebuilt");
   Invoke(typeof(CycleIcons),"Populated",indicator,selected);
   indicator.playerInventory.activeEquipment[selected.slot]=new PlayMagic.Weapons.Equipable{slot=selected.slot};
   Check((bool)Invoke(typeof(CycleIcons),"Populating",indicator)!,"changed inventory incorrectly reuses old icons");
   panel.enabled=true;panel.color=Color.white;panel.rectTransform.position=new(500,0,0);c.Render();Check(!panel.enabled&&panel.color.a==0,"native layout resurrects desktop rectangle");
   XiiiXR.GameUiControls.Current=new(){BlocksGameplay=true};c.Render();Check(!panel.enabled,"quick strip overlays radial wheel");
   XiiiXR.GameUiControls.Current=null;c.Render();Check(!panel.enabled,"wheel close resurrects stale strip");CycleIcons.Request();c.Render();Check(indicator.inventoryGroup.Children.OfType<CanvasGroup>().Single().alpha==1&&icon.color.a==1&&!panel.enabled,"Y cannot show native icons after wheel");
  }
  Check(background.enabled&&icon.color.r==.7f&&panel.rectTransform.localPosition.x==700,"cycle UI not restored");
  var effects=new PlayerCameraEffectController();var healing=new Image{name="Healing",sprite=new Sprite{name="old_heal"},color=new(1,1,1,0)};
  healing.rectTransform.localScale=new(0,0,0);effects.healthAndArmorEffectsCanvas.gameObject.Children.Add(healing);Resources.Items.Add(effects);
  var canvas=effects.healthAndArmorEffectsCanvas.transform;canvas.sizeDelta=new(1920,1080);
  var damage=new Image{name="damage_overlay_health",sprite=new Sprite{name="ui_hud_damage_on_screen_effect"}};effects.healthAndArmorEffectsCanvas.gameObject.Children.Add(damage);
  var rig=new CameraRig();
  using(var e=new FullscreenEffects())
  {e.Discover();e.Render(rig);Check(damage.overrideSprite==null,"damage health classified as healing");Check(healing.overrideSprite?.texture?.width==1024,"healing texture not improved");Check(healing.color.a==0&&healing.rectTransform.localScale.x==0,"effect animation overwritten and healing appears at rest");Check(canvas.localScale.x*1920>3.5f,"wide headset effect remains a small panel");}
  Check(healing.overrideSprite==null&&canvas.sizeDelta.x==1920,"native effects not restored");
  Console.WriteLine("PASS: frame and RawImage geometry suppression survives re-enable, preserves subtitles/comic/buttons; white icons are reused until inventory changes and stale selection rectangle stays hidden; healing covers wide view while preserving alpha/scale animation, uses 1024 texture, restores on stop.");
 }
}
class CutsceneUI:UnityEngine.Object{internal GameObject gameObject=new();internal List<UnityEngine.Object> Children=new();internal UnityEngine.Object[] GetComponentsInChildren(Type t,bool a)=>Children.Where(t.IsInstanceOfType).ToArray();}
class SubtitleCard:UnityEngine.Object{}
class PlayerCameraEffectController:UnityEngine.Object{internal Canvas healthAndArmorEffectsCanvas=new();}
namespace UI.HUD{class UiWeaponIconHolder:UnityEngine.Object{internal Image weaponSprite=null!;}class WeaponInventoryIndicator:UnityEngine.Object{internal GameObject gameObject=new(),inventoryGroup=new();internal RectTransform transform=new();internal PlayerEquipableInventory? playerInventory,playerEquipableInventory=null;internal Image? selectedWeaponPanel;internal List<GameObject> displayedWeapons=new();}}
namespace HarmonyLib{class Harmony{internal Harmony(string s){}internal void Patch(MethodInfo? m,HarmonyMethod? prefix=null,HarmonyMethod? postfix=null){}internal void UnpatchSelf(){}}class HarmonyMethod{internal HarmonyMethod(Type t,string m){}}static class AccessTools{internal static MethodInfo DeclaredMethod(Type t,string m,Type[]? args=null)=>typeof(object).GetMethod("ToString")!;}}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace UnityEngine
{
 class Object
 {
  private static int sequence;private readonly int id=++sequence;private string objectName="";
  internal bool Destroyed=false,ThrowName=false;internal static int DeadNameReads;
  internal string name{get{if(Destroyed){DeadNameReads++;throw new NullReferenceException("Object.get_name: freed native object");}if(ThrowName)throw new NullReferenceException("Object.get_name: invalid native object");return objectName;}set=>objectName=value;}
  public static bool operator==(Object? a,Object? b){bool an=ReferenceEquals(a,null)||a.Destroyed;bool bn=ReferenceEquals(b,null)||b.Destroyed;return an||bn?an==bn:ReferenceEquals(a,b);}
  public static bool operator!=(Object? a,Object? b)=>!(a==b);
  public override bool Equals(object? other)=>ReferenceEquals(this,other);public override int GetHashCode()=>id;
  internal int GetInstanceID()=>id;internal T? TryCast<T>() where T:class=>this as T;internal static void Destroy(Object o){}
 }
 class GameObject:Object{internal bool activeSelf=true;internal void SetActive(bool a)=>activeSelf=a;internal Object? GetComponent(System.Type t)=>Children.FirstOrDefault(t.IsInstanceOfType);internal Object AddComponent(System.Type t){var o=(Object)Activator.CreateInstance(t)!;Children.Add(o);return o;}internal Scene scene=new();internal List<Object> Children=new();internal Object[] GetComponentsInChildren(Type t,bool a)=>Children.Where(t.IsInstanceOfType).ToArray();internal Object? GetComponentInChildren(Type t,bool a)=>Children.FirstOrDefault(t.IsInstanceOfType);}
 class Scene{internal bool IsValid()=>true;}
 static class Resources{internal static List<Object> Items=new();internal static Object[] FindObjectsOfTypeAll(Type t)=>Items.Where(t.IsInstanceOfType).ToArray();}
 class Texture:Object{internal int width;}
 class Texture2D:Texture{internal TextureWrapMode wrapMode{get;set;}internal FilterMode filterMode{get;set;}internal Texture2D(int w,int h,TextureFormat f,bool m){width=w;}internal void SetPixels32(Color32[] p){}internal void Apply(bool a,bool b){}}
 enum TextureWrapMode{Clamp}enum FilterMode{Bilinear}enum TextureFormat{RGBA32}enum SpriteMeshType{FullRect}
 class Sprite:Object{internal Texture2D? texture;internal static Sprite CreateSprite(Texture2D t,Rect r,Vector2 p,float pixels,uint e,SpriteMeshType m,Vector4 border,bool physics)=>new(){texture=t};}
 struct Color32{internal Color32(byte a,byte b,byte c,byte d){}}
 struct Color{internal float r,g,b,a;internal Color(float x,float y,float z,float w){r=x;g=y;b=z;a=w;}internal static Color white=>new(1,1,1,1);}
 struct Vector2{internal float x,y;internal Vector2(float a,float b){x=a;y=b;}internal static Vector2 zero=>new();internal static Vector2 one=>new(1,1);}
 struct Vector3{internal float x,y,z;internal Vector3(float a,float b,float c){x=a;y=b;z=c;}internal static Vector3 one=>new(1,1,1);public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);}
 struct Vector4{internal static Vector4 zero=>new();}struct Quaternion{public static Vector3 operator *(Quaternion q,Vector3 v)=>v;}
 class Transform:Object{internal Transform? parent{get;set;}internal Vector3 position,localPosition,localScale=Vector3.one;internal Vector3 lossyScale=>localScale;internal Quaternion rotation,localRotation;internal void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;rotation=q;}internal Vector3 TransformPoint(Vector2 p)=>position+new Vector3(p.x,p.y,0);}
 class RectTransform:Transform{internal Vector2 sizeDelta=new(100,100),anchorMin,anchorMax,pivot,anchoredPosition;internal Rect rect=>new(0,0,sizeDelta.x,sizeDelta.y);}
 class Rect{internal float width,height;internal Vector2 center=>new();internal Rect(float x,float y,float w,float h){width=w;height=h;}}
 class CanvasGroup:Object{internal float alpha=1;}
 class Material:UnityEngine.Object{internal Shader? shader;}
 class Shader:UnityEngine.Object{}
 class CanvasRenderer:Object{internal bool cull,HasMesh=true;internal void Clear()=>HasMesh=false;}
 enum CanvasUpdate{PreRender}
 static class Time{internal static float realtimeSinceStartup=1;}
 class Canvas:Object{internal GameObject gameObject=new();internal RectTransform transform=new();internal Object[] GetComponentsInChildren(Type t,bool b)=>gameObject.GetComponentsInChildren(t,b);}
}
namespace UnityEngine.UI
{
 class Selectable:UnityEngine.Object{}class VertexHelper{internal bool Cleared;internal void Clear()=>Cleared=true;}
 class Graphic:UnityEngine.Object{internal GameObject gameObject=new();internal CanvasRenderer canvasRenderer=new();private Material? mat;internal bool ThrowMaterial=false;internal Material? material{get{if(ThrowMaterial)throw new NullReferenceException("Graphic.get_material");return mat;}set=>mat=value;}internal bool enabled=true;internal Color color=new(1,1,1,1);internal RectTransform rectTransform=new();internal bool Subtitle,Selectable;internal UnityEngine.Object? GetComponentInParent(Type t)=>t==typeof(SubtitleCard)&&Subtitle?new SubtitleCard():t==typeof(Selectable)&&Selectable?new Selectable():null;internal void SetVerticesDirty(){}}
 class Image:Graphic{internal enum Type{Simple}internal Type type{get;set;}internal bool preserveAspect{get;set;}internal Sprite sprite=new();internal Sprite? overrideSprite{get;set;}}
 class RawImage:Graphic{internal Texture? texture{get;set;}}
}
namespace XiiiXR
{
 class GameUiControls{internal static GameUiControls? Current;internal bool BlocksGameplay;}
 static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){}}
 class CameraRig{internal Vector3 HeadPosition=>new();internal Quaternion HeadRotation=>new();internal EyeFrustum FrustumLeft=>new(-1.5f,1.5f,-1,1);internal EyeFrustum FrustumRight=>FrustumLeft;internal PoseValue EyeLeft=>new(new(-.035f,0,0),System.Numerics.Quaternion.Identity);internal PoseValue EyeRight=>new(new(.035f,0,0),System.Numerics.Quaternion.Identity);}
}

class PlayerEquipableInventory{internal enum ActiveEquipmentSlot{Fist,Pistol}internal PlayMagic.Weapons.Equipable? currentEquipable;internal Dictionary<ActiveEquipmentSlot,PlayMagic.Weapons.Equipable> activeEquipment=new();}
namespace PlayMagic.Weapons{class Equipable:UnityEngine.Object{internal PlayerEquipableInventory.ActiveEquipmentSlot slot;}}
