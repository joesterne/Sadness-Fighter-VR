using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AfterHours.Editor
{
    // Before every player build, Meta's Immersive Debugger DevAgent (callback order 1) writes this computer's
    // LAN address and an AgentBridge access token into Assets/Resources/DevAgentSettings.asset.
    // Everything in Resources ships in the APK. Release builds clear both fields after that step; DevAgent
    // restores its own values when the build ends, so development builds and editor tooling are unaffected.
    class ReleaseBuildPrivacy : IPreprocessBuildWithReport
    {
        const string DevAgentSettingsPath="Assets/Resources/DevAgentSettings.asset";
        public int callbackOrder=>100;
        public void OnPreprocessBuild(BuildReport report)
        {
            if((report.summary.options&BuildOptions.Development)!=0)return;
            var settings=AssetDatabase.LoadAssetAtPath<ScriptableObject>(DevAgentSettingsPath);
            if(!settings)return;
            var so=new SerializedObject(settings);bool cleared=false;
            foreach(var name in new[]{"serverAddress","accessToken"})
            {
                var property=so.FindProperty(name);
                if(property!=null&&property.propertyType==SerializedPropertyType.String&&property.stringValue.Length>0){property.stringValue="";cleared=true;}
            }
            if(!cleared)return;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("After Hours: release build excludes the DevAgent network address and access token.");
        }
    }
}
