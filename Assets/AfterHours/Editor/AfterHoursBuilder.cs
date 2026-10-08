using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AfterHours;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using Object=UnityEngine.Object;

namespace AfterHours.Editor
{
    public static class AfterHoursBuilder
    {
        public const string ScenePath="Assets/AfterHours/Scenes/AfterHours.unity";
        const string Folder="Assets/AfterHours/Art/";
        const string VignetteShader="Assets/AfterHours/Shaders/ComfortVignette.shader";
        const string AudioFolder="Assets/AfterHours/Audio";
        [MenuItem("After Hours/Build or rebuild the game scene")]
        public static void BuildScene()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before rebuilding.");
            // Preserve unrelated scenes, including their current edits.
            if(SceneManager.GetActiveScene().isDirty)EditorSceneManager.SaveOpenScenes();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Directory.CreateDirectory(Folder);Directory.CreateDirectory("Assets/AfterHours/Scenes");
            MakeArt();LoadSounds();
            var director=new GameObject("After Hours - game director").AddComponent<GameDirector>();
            director.audioSource=director.gameObject.AddComponent<AudioSource>();director.audioSource.spatialBlend=0;
            director.acknowledge=Chime("Acknowledgement",new[]{440f,554.37f},.35f);director.completeSound=Chime("A little more room",new[]{261.63f,329.63f,392f,523.25f},1.5f);
            BuildRig(director);WorldBuilder.Build(director);
            director.select=SoundBank.Get("SFX - Select");director.grab=SoundBank.Get("SFX - Grab");director.send=SoundBank.Get("SFX - Send");director.place=SoundBank.Get("SFX - Place");
            director.menuOpen=SoundBank.Get("SFX - Menu open");director.menuClose=SoundBank.Get("SFX - Menu close");director.teleport=SoundBank.Get("SFX - Teleport");director.turn=SoundBank.Get("SFX - Turn");
            director.row=SoundBank.Get("SFX - Row");director.spill=SoundBank.Get("SFX - Spill");director.pour=SoundBank.Get("SFX - Pour");
            director.shred=SoundBank.Get("SFX - Shred");director.unlock=SoundBank.Get("SFX - Unlock");
            Lighting();ConfigureQuest();
            foreach(var room in director.rooms)CombineRoom(room.root,director);
            CheckMovableObjects(director);
            PersistMeshes();
            director.player.transform.position=director.rooms[0].spawn.position;
            director.player.rig.centerEyeAnchor.localPosition=new Vector3(0,1.65f,0);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();
            // Reopen from disk so the editor shows exactly what ships. Meshes rewritten in place during the build can otherwise draw stale data in the editor.
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);var saved=Object.FindAnyObjectByType<GameDirector>();if(saved)Selection.activeGameObject=saved.gameObject;
            if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(new Vector3(0,1.8f,3),Quaternion.Euler(12,0,0),13);
            Debug.Log("After Hours: scene authored, six rooms, hands/controllers, Quest build settings ready.");
        }
        static Material Mat(string name,Color color,float metallic=0,float smoothness=.15f,Texture texture=null)
        {
            string path=Folder+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smoothness);m.enableInstancing=true;
            // Refined low poly: flat matte colour, no photographic textures. Bevelled edges and gradient light give the form.
            m.SetTexture("_BaseMap",texture);EditorUtility.SetDirty(m);return m;
        }
        static Color Hex(string value){ColorUtility.TryParseHtmlString(value,out var c);return c;}
        static void MakeArt()
        {
            Art.Ink=Mat("01 Midnight ink",Hex("#111F30"),0,.2f);Art.Navy=Mat("02 Deep blue",Hex("#283F51"),0,.15f);
            Art.Teal=Mat("03 Quiet teal",Hex("#377C80"),0,.3f);Art.Mint=Mat("04 Sea glass",Hex("#ABC4C5"),0,.3f);
            Art.Coral=Mat("05 Burnt coral",Hex("#C87863"),0,.3f);Art.Gold=Mat("06 Brass and daylight",Hex("#D8AC68"),.35f,.5f);
            // Floors stay matte: a glossy floor turns every bevelled seam into a line of sparkles.
            Art.Cream=Mat("07 Polished limestone",Hex("#DCD4C0"),0,.12f);
            Art.White=Mat("08 Warm porcelain",Hex("#F4EFDF"),0,.45f);
            Art.Wood=Mat("09 Oak grain",Hex("#BC9572"),0,.25f);
            Art.Metal=Mat("10 Brushed aluminium",Hex("#809397"),.3f,.45f);
            Art.Paper=Mat("11 Uncoated paper",Hex("#CFBC96"),0,.2f);Art.Water=Mat("12 Faceted water",Hex("#43868D"),.1f,.7f);
            Art.Glass=Mat("13 Memory glass",new Color(.7f,.88f,.9f,.12f),.15f,.9f);
            Art.Glass.SetFloat("_Surface",1);Art.Glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);Art.Glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);Art.Glass.SetFloat("_ZWrite",0);Art.Glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");Art.Glass.renderQueue=3000;
            // Bright surfaces use emission rather than extra real-time lights on Quest.
            Art.White.EnableKeyword("_EMISSION");Art.White.SetColor("_EmissionColor",Hex("#C8C1AA")*.24f);
            Art.Skins=new[]{"#F2D6C2","#E3B48C","#C98F63","#A66C45","#7A4B2E","#4F3121"}.Select((c,i)=>Mat("17 Skin tone "+(i+1),Hex(c),0,.32f)).ToArray();
            Art.Hairs=new[]{"#1F1B1B","#563823","#8B4126","#D2AC6B","#B9BDC2"}.Select((c,i)=>Mat("18 Hair "+Wardrobe.HairColours[i].ToLowerInvariant(),Hex(c),0,.28f)).ToArray();
            Art.Font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if(!Art.Font)Art.Font=TMP_Settings.defaultFontAsset;
        }
        // The game's original sound design (generated by Tools/make_audio.py). Loops stream compressed; short effects load into memory.
        static void LoadSounds()
        {
            if(!AssetDatabase.IsValidFolder(AudioFolder)){Debug.LogError("After Hours: "+AudioFolder+" is missing, so the rooms will be silent.");return;}
            foreach(var guid in AssetDatabase.FindAssets("t:AudioClip",new[]{AudioFolder}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as AudioImporter;if(!importer)continue;
                bool loop=!Path.GetFileName(path).StartsWith("SFX");
                var settings=importer.defaultSampleSettings;
                var wanted=new AudioImporterSampleSettings{loadType=loop?AudioClipLoadType.CompressedInMemory:AudioClipLoadType.DecompressOnLoad,compressionFormat=AudioCompressionFormat.Vorbis,quality=loop?.45f:.7f,sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate,preloadAudioData=true};
                if(!importer.forceToMono||settings.loadType!=wanted.loadType||settings.compressionFormat!=wanted.compressionFormat||Mathf.Abs(settings.quality-wanted.quality)>.01f)
                {importer.forceToMono=true;importer.loadInBackground=loop;importer.defaultSampleSettings=wanted;importer.SaveAndReimport();}
                SoundBank.Set(Path.GetFileNameWithoutExtension(path),AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }
        }
        static AudioClip Chime(string name,float[] tones,float duration)
        {
            string path=Folder+name+".wav";if(!File.Exists(path))
            {
                int rate=22050,count=(int)(rate*duration);using(var writer=new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
                    for(int i=0;i<count;i++){float t=i/(float)rate;float sample=0;for(int k=0;k<tones.Length;k++){float dt=t-k*.09f;if(dt>0)sample+=Mathf.Sin(dt*tones[k]*2*Mathf.PI)*Mathf.Exp(-dt*4)*Mathf.Min(dt*70,1);}writer.Write((short)(sample/tones.Length*9000));}
                }AssetDatabase.ImportAsset(path);
            }return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        static void BuildRig(GameDirector d)
        {
            var root=new GameObject("Player - hands and controllers");var player=root.AddComponent<VRPlayer>();d.player=player;
            var body=root.AddComponent<CharacterController>();body.radius=.24f;body.height=1.7f;body.center=new Vector3(0,.85f,0);body.stepOffset=.25f;body.skinWidth=.025f;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab");
            var rig=(GameObject)PrefabUtility.InstantiatePrefab(prefab);rig.transform.SetParent(root.transform,false);player.rig=rig.GetComponent<OVRCameraRig>();
            var manager=rig.GetComponent<OVRManager>();if(!manager)manager=rig.AddComponent<OVRManager>();manager.trackingOriginType=OVRManager.TrackingOrigin.FloorLevel;
            // Meta's Project Setup Tool recommendation: dynamic resolution, which also unlocks the highest GPU level on Quest.
            manager.enableDynamicResolution=true;manager.quest2MinDynamicResolutionScale=.7f;manager.quest2MaxDynamicResolutionScale=1.3f;manager.quest3MinDynamicResolutionScale=.7f;manager.quest3MaxDynamicResolutionScale=1.6f;
            player.leftHand=Hand(player.rig.leftHandAnchor,player.rig.trackingSpace,false);player.rightHand=Hand(player.rig.rightHandAnchor,player.rig.trackingSpace,true);
            foreach(var camera in rig.GetComponentsInChildren<Camera>(true)){camera.nearClipPlane=.05f;camera.farClipPlane=85;camera.allowHDR=false;camera.backgroundColor=Hex("#9AB5B7");camera.clearFlags=CameraClearFlags.Skybox;}
            var cam=player.rig.centerEyeAnchor.GetComponent<Camera>();cam.tag="MainCamera";if(!cam.GetComponent<AudioListener>())cam.gameObject.AddComponent<AudioListener>();
            player.hint=Art.Text("A quiet thought","",new Vector3(0,-.38f,1.5f),.043f,Hex("#FFF5DB"),1.3f,player.rig.centerEyeAnchor);player.hint.rectTransform.sizeDelta=new Vector2(1.3f,.32f);player.hint.gameObject.SetActive(false);
            player.rayMaterial=Mat("14 Interaction glow",Hex("#E4BD7E"));player.rayMaterial.EnableKeyword("_EMISSION");player.rayMaterial.SetColor("_EmissionColor",Hex("#E4BD7E")*.4f);
            var canvas=new GameObject("Comfort fade",typeof(Canvas));canvas.transform.SetParent(cam.transform,false);var c=canvas.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=cam;c.planeDistance=.07f;c.sortingOrder=100;
            var veil=new GameObject("Fade",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));veil.transform.SetParent(canvas.transform,false);var rect=veil.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;player.fade=veil.GetComponent<Image>();player.fade.color=new Color(.025f,.04f,.07f,0);player.fade.raycastTarget=false;
            player.vignette=Vignette(cam.transform);
        }
        // A quad just ahead of the eyes. Its shader measures the angle from each eye's own view direction, so the soft edge
        // sits in the same place for both eyes. It stays switched off unless the stick is walking.
        static Renderer Vignette(Transform eye)
        {
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(VignetteShader);
            if(!shader){Debug.LogError("After Hours: "+VignetteShader+" is missing, so walking has no comfort vignette.");return null;}
            string path=Folder+"16 Comfort vignette.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}
            m.shader=shader;m.SetColor("_Color",new Color(.025f,.04f,.07f,1));m.SetFloat("_Strength",1);m.SetFloat("_Inner",30);m.SetFloat("_Outer",58);m.renderQueue=4000;EditorUtility.SetDirty(m);
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name="Comfort vignette";Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(eye,false);quad.transform.localPosition=new Vector3(0,0,.3f);quad.transform.localScale=Vector3.one*1.4f;
            var r=quad.GetComponent<MeshRenderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.enabled=false;
            return r;
        }
        static OVRHand Hand(Transform anchor,Transform tracking,bool right)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRHandPrefab.prefab");var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab);obj.name=right?"Tracked right hand":"Tracked left hand";obj.transform.SetParent(anchor,false);
            var hand=obj.GetComponent<OVRHand>();var so=new SerializedObject(hand);so.FindProperty("HandType").intValue=right?1:0;so.FindProperty("_pointerPoseRoot").objectReferenceValue=tracking;so.ApplyModifiedPropertiesWithoutUndo();
            var skeleton=new SerializedObject(obj.GetComponent<OVRSkeleton>());skeleton.FindProperty("_skeletonType").intValue=right?1:0;skeleton.ApplyModifiedPropertiesWithoutUndo();
            var mesh=new SerializedObject(obj.GetComponent<OVRMesh>());mesh.FindProperty("_meshType").intValue=right?1:0;mesh.ApplyModifiedPropertiesWithoutUndo();return hand;
        }
        static void Lighting()
        {
            var light=new GameObject("Late afternoon sun").AddComponent<Light>();light.type=LightType.Directional;light.color=Hex("#FFF0D7");light.intensity=1.65f;light.transform.rotation=Quaternion.Euler(43,-32,0);light.shadows=LightShadows.Hard;light.shadowStrength=.5f;
            // Gradient ambient: up-facing facets read lighter and down-facing ones darker, so flat-shaded forms stay legible.
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=Hex("#B8C3C1");RenderSettings.ambientEquatorColor=Hex("#9CAAAC");RenderSettings.ambientGroundColor=Hex("#687578");RenderSettings.fog=true;RenderSettings.fogColor=Hex("#9FB6B7");RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=25;RenderSettings.fogEndDistance=72;
            var sky=AssetDatabase.LoadAssetAtPath<Material>(Folder+"15 Sky.mat");if(!sky){sky=new Material(Shader.Find("Skybox/Procedural"));AssetDatabase.CreateAsset(sky,Folder+"15 Sky.mat");}sky.SetColor("_SkyTint",Hex("#8FB7BB"));sky.SetColor("_GroundColor",Hex("#AAB9B7"));sky.SetFloat("_Exposure",1.1f);sky.SetFloat("_AtmosphereThickness",.7f);RenderSettings.skybox=sky;RenderSettings.sun=light;
        }
        public static void ConfigureQuest()
        {
            PlayerSettings.companyName="After Hours Studio";PlayerSettings.productName="After Hours";PlayerSettings.bundleVersion="0.3.0";PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.afterhours.mindoffice");
            // The Meta Quest Store (and the Developer Dashboard's release channels) expect Android API 34 as the target.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel32;PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});PlayerSettings.runInBackground=true;PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            var config=OVRProjectConfig.CachedProjectConfig;config.handTrackingSupport=OVRProjectConfig.HandTrackingSupport.ControllersAndHands;config.handTrackingFrequency=OVRProjectConfig.HandTrackingFrequency.HIGH;
            // The game uses no mixed-reality features. Each one adds Android permissions (scene data, anchors, headset cameras).
            config.anchorSupport=OVRProjectConfig.AnchorSupport.Disabled;config.sharedAnchorSupport=OVRProjectConfig.FeatureSupport.None;config.sceneSupport=OVRProjectConfig.FeatureSupport.None;
            config.colocationSessionSupport=OVRProjectConfig.FeatureSupport.None;config.boundaryVisibilitySupport=OVRProjectConfig.FeatureSupport.None;
            config.insightPassthroughSupport=OVRProjectConfig.FeatureSupport.None;config.isPassthroughCameraAccessEnabled=false;OVRProjectConfig.CommitProjectConfig(config);
            XRGeneralSettingsPerBuildTarget container=null;var guids=AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");if(guids.Length>0)container=AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if(!container){container=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();AssetDatabase.CreateAsset(container,"Assets/XR/AfterHours XR settings.asset");EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey,container,true);}
            foreach(var group in new[]{BuildTargetGroup.Android,BuildTargetGroup.Standalone})
            {
                if(!container.HasSettingsForBuildTarget(group))container.CreateDefaultSettingsForBuildTarget(group);if(!container.HasManagerSettingsForBuildTarget(group))container.CreateDefaultManagerSettingsForBuildTarget(group);
                var general=container.SettingsForBuildTarget(group);general.InitManagerOnStart=true;
                XRPackageMetadataStore.AssignLoader(general.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",group);EditorUtility.SetDirty(general);EditorUtility.SetDirty(general.Manager);
                var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(group);if(settings)
                {
                    settings.renderMode=OpenXRSettings.RenderMode.SinglePassInstanced;
                    foreach(var feature in settings.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>())
                    {string name=feature.GetType().Name;if(name=="OculusTouchControllerProximityProfile"||Array.IndexOf(UnusedMixedRealityFeatures,name)>=0)feature.enabled=false;if(name=="MetaXRFeature"||name=="OculusTouchControllerProfile"||name=="HandTracking"||name=="MetaHandTrackingAim"||name=="HandTrackingFeature")feature.enabled=true;}
                    EditorUtility.SetDirty(settings);
                }
            }
            ConfigureRendering();ConfigureDebugTools();
            EditorUtility.SetDirty(container);AssetDatabase.SaveAssets();
        }
        static readonly string[] UnusedMixedRealityFeatures={"ARSessionFeature","ARAnchorFeature","ARPlaneFeature","ARMeshFeature","ARBoundingBoxFeature","ARRaycastFeature","AROcclusionFeature","ARCameraFeature","ColocationDiscoveryFeature","BoundaryVisibilityFeature"};
        static void ConfigureRendering()
        {
            // Each quality level can carry its own URP asset. Quest uses Mobile_RPAsset while the editor previews with PC_RPAsset, so tune every one.
            var assets=new HashSet<UniversalRenderPipelineAsset>();
            if(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset main)assets.Add(main);
            for(int i=0;i<QualitySettings.count;i++)if(QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset level)assets.Add(level);
            foreach(var pipeline in assets){pipeline.msaaSampleCount=4;pipeline.renderScale=1;pipeline.shadowDistance=28;pipeline.supportsHDR=false;EditorUtility.SetDirty(pipeline);}
        }
        static void ConfigureDebugTools()
        {
            // Meta's Immersive Debugger defaults to B/Y, the game's return-to-lobby button, and the left menu button opens the game's menu.
            // Keep it out of release builds, and on a left thumbstick click in development builds.
            var settings=AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Resources/ImmersiveDebuggerSettings.asset");if(!settings)return;
            var so=new SerializedObject(settings);var debugOnly=so.FindProperty("enableOnlyInDebugBuild");var toggle=so.FindProperty("immersiveDebuggerToggleDisplayButton");
            if(debugOnly!=null)debugOnly.boolValue=true;if(toggle!=null)toggle.intValue=(int)OVRInput.Button.PrimaryThumbstick;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(settings);
        }
        // Anything that moves, rescales or switches on and off at runtime keeps its own renderer. Every room except the lobby
        // is inactive while the scene is built, so these checks must include inactive objects.
        static bool Dynamic(Transform t,Transform room,GameDirector d)
        {
            if(t.GetComponentInParent<Grabbable>(true)||t.GetComponentInParent<MirrorAvatar>(true))return true;
            for(var p=t;p&&p!=room;p=p.parent)if(!p.gameObject.activeSelf)return true;
            if(t.IsChildOf(d.ocean.horizon))return true;
            foreach(var c in d.kitchen.crowd)if(t.IsChildOf(c.transform))return true;
            foreach(var c in d.kitchen.spills)if(t.IsChildOf(c.transform))return true;
            return false;
        }
        static void CombineRoom(GameObject root,GameDirector d)
        {
            // Batches are split by material and shadow mode: tiled surfaces draw without shadows and cast them from an invisible slab.
            var byMaterial=new Dictionary<(Material,ShadowCastingMode),List<CombineInstance>>();var merged=new List<MeshRenderer>();
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!renderer.enabled||renderer.GetComponent<TMP_Text>()||Dynamic(renderer.transform,root.transform,d))continue;
                var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh||!renderer.sharedMaterial||renderer.sharedMaterial.renderQueue>=3000)continue;
                var key=(renderer.sharedMaterial,renderer.shadowCastingMode);
                if(!byMaterial.ContainsKey(key))byMaterial[key]=new List<CombineInstance>();
                byMaterial[key].Add(new CombineInstance{mesh=filter.sharedMesh,transform=root.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix});merged.Add(renderer);
            }
            // The merged originals keep their colliders and scripts but no longer need their own meshes.
            foreach(var renderer in merged){var filter=renderer.GetComponent<MeshFilter>();Object.DestroyImmediate(renderer);Object.DestroyImmediate(filter);}
            foreach(var pair in byMaterial)
            {
                var (material,shadows)=pair.Key;string label=material.name+(shadows==ShadowCastingMode.Off?" - no shadows":shadows==ShadowCastingMode.ShadowsOnly?" - shadow caster":"");
                var mesh=new Mesh{name=root.name+" - "+label,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray());
                var go=new GameObject("Batched geometry - "+label,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root.transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var batch=go.GetComponent<MeshRenderer>();batch.sharedMaterial=material;batch.shadowCastingMode=shadows;
            }
        }
        // Objects the player can pick up must keep their own visible mesh, or they appear frozen while only their labels move.
        static void CheckMovableObjects(GameDirector d)
        {
            int count=0;
            foreach(var item in d.rooms.SelectMany(r=>r.root.GetComponentsInChildren<Grabbable>(true)))
            {
                count++;
                foreach(var renderer in item.GetComponentsInChildren<MeshRenderer>(true))
                    if(!renderer.GetComponent<TMP_Text>()&&(!renderer.enabled||!renderer.GetComponent<MeshFilter>()))
                        Debug.LogError("After Hours: '"+renderer.name+"' on movable object '"+item.name+"' was merged into static scenery. It would not move in the headset.");
            }
            Debug.Log("After Hours: "+count+" movable objects keep their own meshes.");
        }
        static void PersistMeshes()
        {
            var saved=new Dictionary<Mesh,Mesh>();var created=new Dictionary<string,Mesh>();
            foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
            {
                var mesh=filter.sharedMesh;if(!mesh||!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh)))continue;
                if(saved.TryGetValue(mesh,out var existing)){filter.sharedMesh=existing;continue;}
                // Stable names prevent rebuild order from overwriting unrelated meshes. Never persist engine-owned primitives.
                using(var md5=System.Security.Cryptography.MD5.Create())
                {
                    string id=System.BitConverter.ToString(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(mesh.name))).Replace("-","").Substring(0,16);
                    string path=Folder+"Geometry-"+id+".asset";
                    // Meshes with the same name have the same geometry, so one asset serves them all.
                    if(created.TryGetValue(path,out var same)){filter.sharedMesh=same;saved[mesh]=same;continue;}
                    // Replace the asset rather than overwriting it in place: a mesh rewritten in place can keep drawing its old data in the editor.
                    if(AssetDatabase.LoadAssetAtPath<Mesh>(path))AssetDatabase.DeleteAsset(path);
                    var copy=Object.Instantiate(mesh);copy.name=mesh.name;AssetDatabase.CreateAsset(copy,path);filter.sharedMesh=copy;saved[mesh]=copy;created[path]=copy;
                }
            }
            foreach(var particles in Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include)){if(particles.mesh && saved.TryGetValue(particles.mesh,out var owned))particles.mesh=owned;}
            foreach(var collider in Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include)){if(collider.sharedMesh && saved.TryGetValue(collider.sharedMesh,out var owned))collider.sharedMesh=owned;}
            // Remove generated meshes left over from earlier builds that this scene no longer uses.
            var used=new HashSet<string>();
            foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))if(filter.sharedMesh)used.Add(AssetDatabase.GetAssetPath(filter.sharedMesh));
            foreach(var particles in Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include))if(particles.mesh)used.Add(AssetDatabase.GetAssetPath(particles.mesh));
            foreach(var collider in Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include))if(collider.sharedMesh)used.Add(AssetDatabase.GetAssetPath(collider.sharedMesh));
            int removed=0;
            foreach(var guid in AssetDatabase.FindAssets("t:Mesh",new[]{Folder.TrimEnd('/')}))
            {var path=AssetDatabase.GUIDToAssetPath(guid);if(Path.GetFileName(path).StartsWith("Geometry-")&&!used.Contains(path)&&AssetDatabase.DeleteAsset(path))removed++;}
            if(removed>0)Debug.Log("After Hours: removed "+removed+" unused generated meshes.");
        }
        // Development builds include Meta's test tooling (XR Operator layer, Immersive Debugger) for simulator and agent testing. Share only the release APK.
        [MenuItem("After Hours/Build Quest APK (development)")]
        public static void BuildQuest(){BuildQuest(false);}
        [MenuItem("After Hours/Build shareable Quest APK (release)")]
        public static void BuildQuestRelease(){BuildQuest(true);}
        static void BuildQuest(bool release)
        {
            // A custom build profile (such as Unity's "Meta Quest" profile) carries its own player, quality and XR settings.
            // The project's tested Quest settings live in the platform profile, so build from that.
            var profile=BuildProfile.GetActiveBuildProfile();
            if(profile){Debug.Log("After Hours: switching from build profile '"+profile.name+"' to the platform profile for this build.");BuildProfile.SetActiveBuildProfile(null);}
            ConfigureQuest();Directory.CreateDirectory("Builds/Quest");
            string path=release?"Builds/Quest/AfterHours-release.apk":"Builds/Quest/AfterHours.apk";
            // Gradle repackages its previous APK in place and leaves removed entries behind as dead space. Clear its packaged output so each APK is written fresh.
            const string gradleApks="Library/Bee/Android/Prj/IL2CPP/Gradle/launcher/build/outputs/apk";
            if(Directory.Exists(gradleApks))Directory.Delete(gradleApks,true);
            // Each upload to a Meta release channel needs a higher version code: minutes since 2026 always rise.
            PlayerSettings.Android.bundleVersionCode=Mathf.Max(PlayerSettings.Android.bundleVersionCode+1,(int)(DateTime.UtcNow-new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc)).TotalMinutes);
            bool signed=release&&ReleaseSigning.Apply();
            if(release&&!signed)Debug.LogWarning("After Hours: no release key yet, so this APK is signed with the debug key. The Meta Developer Dashboard rejects debug-signed APKs. Use After Hours → Create release signing key first.");
            try
            {
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=path,target=BuildTarget.Android,options=release?BuildOptions.None:BuildOptions.Development});
                Debug.Log("After Hours "+(release?"release":"development")+" APK: "+report.summary.result+" / "+report.summary.totalErrors+" errors / "+path+" / version "+PlayerSettings.bundleVersion+" ("+PlayerSettings.Android.bundleVersionCode+")"+(release?signed?" / signed with the release key":" / DEBUG-SIGNED":""));
            }
            finally{if(signed)ReleaseSigning.Clear();}
        }
    }
}




