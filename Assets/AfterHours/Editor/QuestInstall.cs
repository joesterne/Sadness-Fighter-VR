using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Debug=UnityEngine.Debug;

namespace AfterHours.Editor
{
    // Installs a built APK on the Quest connected over USB and starts it. Every step is reported in the Console.
    public static class QuestInstall
    {
        const string ReleaseApk="Builds/Quest/AfterHours-release.apk";
        const string DevelopmentApk="Builds/Quest/AfterHours.apk";
        const string Activity="com.unity3d.player.UnityPlayerGameActivity";

        [MenuItem("After Hours/Install release APK on connected Quest",false,2000)]
        public static void InstallRelease(){Install(ReleaseApk);}
        [MenuItem("After Hours/Install development APK on connected Quest",false,2001)]
        public static void InstallDevelopment(){Install(DevelopmentApk);}

        public static bool Install(string apk)
        {
            if(!File.Exists(apk)){Debug.LogError("[QuestInstall] "+apk+" does not exist. Build it first.");return false;}
            string adb=FindAdb();
            if(adb==null){Debug.LogError("[QuestInstall] Could not find adb. Install Android Build Support for this Unity version.");return false;}
            try
            {
                EditorUtility.DisplayProgressBar("After Hours","Looking for the Quest…",.1f);
                StartServer(adb);
                string serial=FindHeadset(adb);
                if(serial==null)return false;
                string package=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
                string path=Path.GetFullPath(apk);
                EditorUtility.DisplayProgressBar("After Hours","Installing "+Path.GetFileName(apk)+" on the Quest…",.3f);
                var install=Run(adb,"-s "+serial+" install -r \""+path+"\"",240);
                if(install.output.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE")||install.output.Contains("INSTALL_FAILED_VERSION_DOWNGRADE"))
                {
                    Debug.LogWarning("[QuestInstall] The copy already on the headset was signed differently. Uninstalling it first; its saved progress is removed.\n"+install.output);
                    Run(adb,"-s "+serial+" uninstall "+package,60);
                    install=Run(adb,"-s "+serial+" install -r \""+path+"\"",240);
                }
                if(!install.output.Contains("Success")){Debug.LogError("[QuestInstall] Install failed:\n"+install.output);return false;}
                EditorUtility.DisplayProgressBar("After Hours","Starting After Hours on the Quest…",.9f);
                Run(adb,"-s "+serial+" shell am force-stop "+package,30);
                var start=Run(adb,"-s "+serial+" shell am start -n "+package+"/"+Activity,30);
                if(start.output.Contains("Error")){Debug.LogError("[QuestInstall] Installed, but the app did not start:\n"+start.output);return false;}
                Debug.Log("[QuestInstall] Installed "+Path.GetFileName(apk)+" on headset "+serial+" and started "+package+". Put the headset on.\n"+install.output+start.output);
                return true;
            }
            finally{EditorUtility.ClearProgressBar();}
        }

        static string FindHeadset(string adb)
        {
            var result=Run(adb,"devices",30);
            var lines=result.output.Split('\n').Select(l=>l.Trim()).Where(l=>l.Length>0&&!l.StartsWith("List of devices")&&!l.StartsWith("*")).ToArray();
            var ready=lines.Where(l=>l.EndsWith("device")).Select(l=>l.Split('\t',' ')[0]).ToArray();
            if(ready.Length==1){Debug.Log("[QuestInstall] Found headset "+ready[0]+".");return ready[0];}
            if(ready.Length>1)Debug.LogError("[QuestInstall] More than one Android device is connected ("+string.Join(", ",ready)+"). Unplug the others and try again.");
            else if(lines.Any(l=>l.EndsWith("unauthorized")))Debug.LogError("[QuestInstall] The Quest is connected, but this PC is not authorized. Put the headset on, accept 'Allow USB debugging' and tick 'Always allow from this computer', then try again.\n"+result.output);
            else if(lines.Any(l=>l.EndsWith("offline")))Debug.LogError("[QuestInstall] The Quest is offline. Unplug the USB cable, plug it back in, and try again.\n"+result.output);
            else Debug.LogError("[QuestInstall] No Quest found. Connect it with a USB data cable, make sure Developer Mode is on, and try again.\n"+result.output);
            return null;
        }

        static string FindAdb()
        {
            string configured=null;
            try
            {
                var settings=Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions");
                configured=settings?.GetProperty("sdkRootPath",BindingFlags.Public|BindingFlags.Static)?.GetValue(null) as string;
            }
            catch(Exception){}
            string exe=Application.platform==RuntimePlatform.WindowsEditor?"adb.exe":"adb";
            foreach(var root in new[]{configured,Path.Combine(EditorApplication.applicationContentsPath,"PlaybackEngines","AndroidPlayer","SDK")})
            {
                if(string.IsNullOrEmpty(root))continue;
                string candidate=Path.Combine(root,"platform-tools",exe);
                if(File.Exists(candidate))return candidate;
            }
            return null;
        }

        // Start the adb server without redirected output, so the long-lived server never holds our pipes open.
        static void StartServer(string adb)
        {
            using var p=Process.Start(new ProcessStartInfo(adb,"start-server"){UseShellExecute=false,CreateNoWindow=true});
            p?.WaitForExit(30000);
        }

        static (int code,string output) Run(string exe,string args,int timeoutSeconds)
        {
            var info=new ProcessStartInfo(exe,args){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
            using var p=Process.Start(info);
            var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();
            if(!p.WaitForExit(timeoutSeconds*1000)){try{p.Kill();}catch(Exception){}return (-1,"adb "+args+" timed out after "+timeoutSeconds+" s");}
            return (p.ExitCode,stdout.Result+stderr.Result);
        }
    }
}
