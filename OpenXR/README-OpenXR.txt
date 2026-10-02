XIII VR - the OpenXR files (0.1.181)

GameFolder\XIII_Data\Plugins (installed into the game):
  UnityOpenXR.dll      Unity's OpenXR plugin, com.unity.xr.openxr 1.8.2 (the last for Unity 2020.3),
                       unchanged. (c) Unity Technologies: LICENSE-Unity-OpenXR-Plugin.md (Unity Package
                       Distribution License / Unity Companion License), notices in
                       Unity-OpenXR-Plugin-Third-Party-Notices.md.
  openxr_loader.dll    the Khronos OpenXR loader as shipped in that package (OpenXR-SDK-Source,
                       Apache-2.0; see the notices).
  xiii_openxr.dll      the mod's own OpenXR input/tracking helper (source: buildtools\openxr-helper).
GameFolder\XIII_Data\UnitySubsystems\UnityOpenXR\UnitySubsystemsManifest.json: from the same package.

OpenXR\openvr_api.dll: the runtime switch for the OpenVR way (source: buildtools\openvr-shim).
