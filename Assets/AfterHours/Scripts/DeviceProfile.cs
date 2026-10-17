using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace AfterHours
{
    public enum Headset { Unknown, Quest2, QuestPro, Quest3, Quest3S }
    // One build runs on Quest 2, Quest Pro, Quest 3 and Quest 3S. The Quest 2 has a weaker chip and a lower-resolution
    // display, so on it the game asks for a lighter picture. Nothing about how the game plays changes.
    public static class DeviceProfile
    {
        public static Headset Current { get; private set; } = Headset.Unknown;
        public static bool Applied { get; private set; }
        // What was in force before a profile changed it, so it can be put back.
        static float savedScale, savedShadow, savedFrequency; static UniversalRenderPipelineAsset saved; static bool frequencyChanged;
        public const float Quest2RenderScale=.9f, Quest2ShadowDistance=20f, Quest2Frequency=72f;

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
        // Detects the headset (it can take a few frames after start-up for the runtime to say) and applies its profile once.
        // Returns true when the question is settled.
        public static bool TryApply()
        {
            if(Applied)return true;
            var headset=Detect();if(headset==Headset.Unknown)return false;
            Apply(headset);return true;
        }
        public static void Apply(Headset headset)
        {
            Current=headset;Applied=true;
            if(headset!=Headset.Quest2)return;
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
                if(offered!=null&&System.Array.IndexOf(offered,Quest2Frequency)>=0){savedFrequency=display.displayFrequency;display.displayFrequency=Quest2Frequency;frequencyChanged=true;}
            }
            catch(System.Exception){}
            Debug.Log("[DeviceProfile] Quest 2 detected: render scale "+Quest2RenderScale+", shadows to "+Quest2ShadowDistance+" m, "+Quest2Frequency+" Hz.");
        }
        // Puts the render settings and the refresh rate back to what they were. Used by tests so the shared URP asset is never left changed.
        public static void Undo()
        {
            if(saved){saved.renderScale=savedScale;saved.shadowDistance=savedShadow;saved=null;}
            if(frequencyChanged){frequencyChanged=false;try{var display=OVRManager.display;if(display!=null)display.displayFrequency=savedFrequency;}catch(System.Exception){}}
            Applied=false;Current=Headset.Unknown;
        }
    }
}
