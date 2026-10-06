# Unity 6000.6 compatibility

The starting project contained Unity OpenXR Meta 2.2.0 and Inference Engine 2.3.0. A fresh compilation in Unity 6000.6.3f1 failed in their source before game scripts could run.

To keep fixes durable rather than modifying Library/PackageCache, these two packages are embedded under Packages with their licenses intact. Three source sites changed:

- `com.unity.xr.meta-openxr/Editor/SoftShadowsMetaOpenXRValidationRules.cs`: use `urpAsset.GetEntityId()` so editor asset opening uses the current overloads.
- `com.unity.ai.inference/Runtime/Core/PluginInterfaces.cs`: use `UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies()` to avoid accessing unloaded assemblies.
- `com.unity.ai.inference/Editor/Visualizer/Editor/ModelAssetOpenHandler.cs`: accept `UnityEngine.EntityId` in the asset-open callback and call `EditorUtility.EntityIdToObject`.

These patches target the project's declared editor version. When upgrading to package releases compatible with Unity 6000.6, compare these files before removing the embedded copies.

**Inference Engine was removed on October 2, 2026.** The game never used it, but its Resources folder put about 75 MB of compute shaders into every APK. It is no longer in `Packages/manifest.json`. The patched embedded copy was moved to `_RemovedPackages/com.unity.ai.inference`, outside the folders Unity loads. You can delete that folder. Meta's AI Building Blocks compile without the package, because they only use it behind `UNITY_INFERENCE_INSTALLED`. If you add the package back, reuse the two patches above.

The optional Oculus Touch proximity interaction feature is disabled: simulator v207 rejected its trigger-proximity binding. Standard Oculus Touch, Meta XR, and hand tracking remain enabled.
