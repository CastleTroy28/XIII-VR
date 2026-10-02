using System;using System.Linq;using System.Collections.Generic;using Mono.Cecil;
class InteropApiTests
{
    // Arguments (optional): the built plugin, the game's BepInEx folder (its interop and core folders).
    static void Main(string[] args)
    {
        string dll=args.Length>0?args[0]:"xiii-xr/XIII.XRBootstrap.dll",refs=args.Length>1?args[1]:"analysis_xiii/refs";
        var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(System.IO.Path.Combine(refs,"interop"));resolver.AddSearchDirectory(System.IO.Path.Combine(refs,"core"));
        using var plugin=ModuleDefinition.ReadModule(dll,new ReaderParameters{AssemblyResolver=resolver});
        var visited=new HashSet<string>();
        void Visit(MethodReference reference)
        {
            if(!(reference.DeclaringType.FullName.StartsWith("UnityEngine.")||reference.DeclaringType.FullName.StartsWith("FMOD.")||reference.DeclaringType.FullName.StartsWith("FMODUnity."))||!visited.Add(reference.FullName))return;
            var method=reference.Resolve()??throw new Exception("Missing Unity wrapper: "+reference.FullName);
            if(!method.HasBody)return;
            if(method.Body.Instructions.Any(i=>i.Operand is string s&&s.Contains("unstripping failed")))throw new Exception("Unavailable Unity method: "+method.FullName);
            foreach(var child in method.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>())Visit(child);
        }
        IEnumerable<TypeDefinition> Tree(TypeDefinition t)=>new[]{t}.Concat(t.NestedTypes.SelectMany(Tree));
        foreach(var type in plugin.Types.Where(t=>new[]{"PropImpactAudio","ChairImpactClip","GrappleVr","KeyUnlockGesture","StoryVideo","StoryColorEffect","StoryVolumeColor","ReloadAudio","WeaponMechanism","WeaponVisual","ReloadMesh","ReloadDrops","ReloadHint","AmmoPouch","WeaponHands","HapticOutput","EyeCapture","CinematicMask","PunchDriver","NativeFistSampler","NativeHandVisual","NativeBraceletVisual","WristHud","GloveVisual","FramePerformance","CameraRig","GameUiControls","ContactWorld","ContactRig","QualityMenu","WorldCanvas","FrontendMenu","VrMenuPointer","WheelItems","ContactFilter","MenuBeam","CinematicFrame","ColliderSurface","StartupLogos","MenuKeyboard","MenuPrompts","HeldItemVisual","HeldItemMeshSources","KeyPreviewFactory","CycleIcons","TeleportDriver","TeleportArc","RigidMeshVisual","FullscreenEffects","LocomotionDriver","TouchButtons","TouchControlReader"}.Contains(t.Name)).SelectMany(Tree))
            foreach(var method in type.Methods.Where(m=>m.HasBody))foreach(var call in method.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>())Visit(call);
        var punch=plugin.Types.Single(t=>t.Name=="PunchDriver");
        var calls=punch.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
        if(!calls.Any(c=>c.Name=="SphereCastNonAlloc")||calls.Any(c=>c.Name=="SphereCastAll"))throw new Exception("Punch query regression");
        var sampler=plugin.Types.Single(t=>t.Name=="NativeFistSampler");
        var sampleCalls=Tree(sampler).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
        if(sampleCalls.Any(m=>new[]{"Instantiate","AddComponent","SetTrigger","Play"}.Contains(m.Name))||!sampleCalls.Any(m=>m.Name=="SampleAnimation"))throw new Exception("Fist pose sampler must use plain private transforms");
        Console.WriteLine("PASS: "+visited.Count+" transitive Unity wrappers used by changed runtime adapters contain no unstripping-failed stubs; fist query uses native NonAlloc wrapper; private clip sampling clones no gameplay components.");
        Console.WriteLine("IL validation against the game's interop assemblies; engine ICall execution and native clip contents still require the game.");
    }
}
