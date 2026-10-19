using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace AfterHours
{
    public enum Headset { Unknown, Quest2, QuestPro, Quest3, Quest3S }
    // One build runs on Quest 2, Quest Pro, Quest 3 and Quest 3S. The Quest 2 has a weaker chip and a lower-resolution
    // display, so on it the game asks for a lighter picture. Every headset is also asked for foveated rendering: the edges
    // of each eye's picture, where the lens is soft anyway, are shaded more coarsely, which buys a steadier frame rate.
    // Nothing about how the game plays changes.
    public static class DeviceProfile
    {
        public static Headset Current { get; private set; } = Headset.Unknown;
        public static bool Applied { get; private set; }
        // What was in force before a profile changed it, so it can be put back.
        static float savedScale, savedShadow, savedFrequency; static UniversalRenderPipelineAsset saved;
        public const float Quest2RenderScale=.9f, Quest2ShadowDistance=20f, Quest2Frequency=72f;

        // Foveated rendering. The level is a ceiling, not a fixed amount: with dynamic foveation on, the headset uses none while
        // the scene is cheap to draw and only reaches for more when the graphics chip is running out of time, so the
        // sharp picture is kept whenever there is room for it. The Quest 2 has the least room, so its ceiling is higher.
        public const OVRManager.FoveatedRenderingLevel Quest2Foveation=OVRManager.FoveatedRenderingLevel.High, NewerFoveation=OVRManager.FoveatedRenderingLevel.Medium;
        // How many half-second checks to wait for the runtime to offer foveation before giving up and leaving the picture as it is.
        public const int FoveationAttempts=40;
        public static OVRManager.FoveatedRenderingLevel Foveation { get; private set; } = OVRManager.FoveatedRenderingLevel.Off;
        public static bool FoveationDynamic { get; private set; }
        public static bool FoveationApplied { get; private set; }
        public static bool FoveationSettled { get; private set; }
        static int foveationTries;
        // The two things foveation needs from the headset runtime. Tests swap them for stand-ins; on a headset they are the real thing.
        // Only a headset build ever reaches the runtime, so the editor and the tests never change the picture of a connected headset.
        public static System.Func<bool> FoveationSupported=RuntimeSupportsFoveation;
        public static System.Action<OVRManager.FoveatedRenderingLevel,bool> FoveationSetter=SetRuntimeFoveation;
        static bool RuntimeSupportsFoveation(){return Application.platform==RuntimePlatform.Android&&OVRManager.fixedFoveatedRenderingSupported;}
        static void SetRuntimeFoveation(OVRManager.FoveatedRenderingLevel level,bool dynamic)
        {
            OVRManager.foveatedRenderingLevel=level;OVRManager.useDynamicFoveatedRendering=dynamic;
            // Reading it back makes the headset log say whether the runtime really took the request.
            Debug.Log("[DeviceProfile] Foveation reads back as "+OVRManager.foveatedRenderingLevel+(OVRManager.useDynamicFoveatedRendering?" (dynamic).":" (fixed)."));
        }

        public static Headset Classify(OVRPlugin.SystemHeadset type)
        {
            switch(type)
            {
                case OVRPlugin.SystemHeadset.Oculus_Quest_2: case OVRPlugin.SystemHeadset.Oculus_Link_Quest_2: return Headset.Quest2;
                case OVRPlugin.SystemHeadset.Meta_Quest_Pro: case OVRPlugin.SystemHeadset.Meta_Link_Quest_Pro: return Headset.QuestPro;
                case OVRPlugin.SystemHeadset.Meta_Quest_3: case OVRPlugin.SystemHeadset.Meta_Link_Quest_3: return Headset.Quest3;
                case OVRPlugin.SystemHeadset.Meta_Quest_3S: case OVRPlugin.SystemHeadset.Meta_Link_Quest_3S: return Headset.Quest3S;
                default: return Headset.Unknown;
            }
        }
        public static Headset Detect()
        {
            try{return Classify(OVRPlugin.GetSystemHeadsetType());}catch(System.Exception){return Headset.Unknown;}
        }
        public static OVRManager.FoveatedRenderingLevel FoveationFor(Headset headset)
        {
            switch(headset)
            {
                case Headset.Quest2: return Quest2Foveation;
                case Headset.QuestPro: case Headset.Quest3: case Headset.Quest3S: return NewerFoveation;
                default: return OVRManager.FoveatedRenderingLevel.Off;
            }
        }
        // Every question the profile asks of the runtime is settled: the headset is known and foveation is either on or given up on.
        public static bool Settled { get { return Applied&&FoveationSettled; } }
        // Detects the headset (it can take a few frames after start-up for the runtime to say) and applies its profile once,
        // then keeps asking for foveation until the runtime offers it. Returns true when every question is settled.
        public static bool TryApply()
        {
            if(!Applied)
            {
                var headset=Detect();if(headset==Headset.Unknown)return false;
                Apply(headset);
            }
            else TryApplyFoveation();
            return Settled;
        }
        public static void Apply(Headset headset)
        {
            Current=headset;Applied=true;foveationTries=0;FoveationSettled=false;
            if(headset==Headset.Quest2)LightenQuest2();
            TryApplyFoveation();
        }
        static void LightenQuest2()
        {
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(pipeline)
            {
                saved=pipeline;savedScale=pipeline.renderScale;savedShadow=pipeline.shadowDistance;
                pipeline.renderScale=Mathf.Min(savedScale,Quest2RenderScale);pipeline.shadowDistance=Mathf.Min(savedShadow,Quest2ShadowDistance);
            }
            try
            {
                // Only a rate the headset really offers is requested.
                var display=OVRManager.display;var offered=display!=null?display.displayFrequenciesAvailable:null;
                if(offered!=null&&System.Array.IndexOf(offered,Quest2Frequency)>=0){savedFrequency=display.displayFrequency;display.displayFrequency=Quest2Frequency;}
            }
            catch(System.Exception){}
            Debug.Log("[DeviceProfile] Quest 2 detected: render scale "+Quest2RenderScale+", shadows to "+Quest2ShadowDistance+" m, "+Quest2Frequency+" Hz.");
        }
        // Asks the runtime for foveated rendering at the ceiling this headset gets. The runtime may not be ready in the first
        // moments after start-up, so this is called again every half second until it takes the request, up to FoveationAttempts
        // times. Anything unexpected from the runtime is logged and the picture is left as it is: foveation is never worth a crash.
        public static bool TryApplyFoveation()
        {
            if(FoveationSettled)return true;
            var wanted=FoveationFor(Current);
            if(wanted==OVRManager.FoveatedRenderingLevel.Off){FoveationSettled=true;return true;}
            foveationTries++;
            try
            {
                if(FoveationSupported())
                {
                    FoveationSetter(wanted,true);
                    Foveation=wanted;FoveationDynamic=true;FoveationApplied=true;FoveationSettled=true;
                    Debug.Log("[DeviceProfile] "+Current+": foveated rendering up to "+wanted+", adapted to the load by the headset.");
                    return true;
                }
            }
            catch(System.Exception e)
            {
                FoveationSettled=true;Debug.LogWarning("[DeviceProfile] Foveated rendering could not be turned on, so the picture is left as it is: "+e.Message);
                return true;
            }
            if(foveationTries>=FoveationAttempts){FoveationSettled=true;Debug.Log("[DeviceProfile] This runtime does not offer foveated rendering; the picture is left as it is.");}
            return FoveationSettled;
        }
        // Puts the render settings back to what they were. Used by tests so the shared URP asset is never left changed.
        public static void Undo()
        {
            if(saved){saved.renderScale=savedScale;saved.shadowDistance=savedShadow;saved=null;}
            Applied=false;Current=Headset.Unknown;
            Foveation=OVRManager.FoveatedRenderingLevel.Off;FoveationDynamic=false;FoveationApplied=false;FoveationSettled=false;foveationTries=0;
            FoveationSupported=RuntimeSupportsFoveation;FoveationSetter=SetRuntimeFoveation;
        }
    }
}
